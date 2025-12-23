using Unity.Entities;
using Mpr.Blobs;
using Unity.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Mpr.Expr;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Mpr.Query
{
	public struct QSData
	{
		/// <summary>
		/// Baked data for all expressions in the query graph
		/// </summary>
		public ExprData exprData;

		/// <summary>
		/// List of passes for this query graph, in evaluation order.
		/// </summary>
		/// <remarks>
		/// Passes are evaluated with their generators and filters until
		/// the desired amount of items has been accepted by filters. This
		/// means that further passes could still have generated better-scoring
		/// items but they will not be considered.
		/// </remarks>
		public BlobArray<QSPass> passes;

		/// <summary>
		/// Stable type hash for the query result item type
		/// </summary>
		public ulong itemTypeHash;

		/// <summary>
		/// An expression to evaluate to determine the desired result item count
		/// </summary>
		public ExprNodeRef resultCount;

		// TODO: fptr support

		/// <summary>
		/// Raw data storage for query nodes
		/// </summary>
		public BlobArray<byte> nodeStorage;
		public BlobArray<NodeHeader> nodeHeaders;
		bool runtimeInitComplete;
	}

	[ChunkSerializable]
	public struct NodeHeader
	{
		public ulong stableTypeHash;
		public IntPtr func;
		public int offset;
	}

	public struct QSPass
	{
		/// <summary>
		/// Generators for the pass. At the start of pass evaluation, all generators are evaluated.
		/// </summary>
		public BlobArray<QSGenerator> generators;

		/// <summary>
		/// Filters for the pass. After generators have been run, all generated items are filtered.
		/// If this doesn't result in enough items, further passes will be evaluated to generate
		/// and filter more items.
		/// </summary>
		public BlobArray<QSFilter> filters;

		/// <summary>
		/// Scorers for the pass. After filters have been run, all generated items are scored.
		/// </summary>
		public BlobArray<QSScorer> scorers;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct QSCurrentItemStorage
	{
		[FieldOffset(0)] double4 data0;
		[FieldOffset(32)] double4 data1;
		[FieldOffset(64)] double4 data2;
		[FieldOffset(96)] double4 data3;
	}

	/// <summary>
	/// Temporary stack-bound state for query execution
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct QSTempState
	{
		QSCurrentItemStorage currentItemStorage;

		public ref T GetCurrentItem<T>() where T : unmanaged
		{
			if(UnsafeUtility.SizeOf<T>() > UnsafeUtility.SizeOf<QSCurrentItemStorage>())
				throw new Exception();

			if(UnsafeUtility.AlignOf<T>() > UnsafeUtility.AlignOf<QSCurrentItemStorage>())
				throw new Exception();

			unsafe
			{
				fixed(QSCurrentItemStorage* data = &currentItemStorage)
					return ref *((T*)data);
			}
		}
	}

	public struct QSItem
	{
		public float score;
		public int itemIndex;

		public struct ScoreComparer : IComparer<QSItem>
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(QSItem x, QSItem y) => y.score.CompareTo(x.score);
		}
	}

	public struct QSGenerator
	{
		public Data data;
		public GeneratorType generatorType;

		[StructLayout(LayoutKind.Explicit)]
		public struct Data
		{
			[FieldOffset(0)]
			public Float2Rectangle float2Rectangle;
		}

		public enum GeneratorType
		{
			Float2Rectangle,
		}

		public struct Float2Rectangle
		{
			public ExprNodeRef center;
			public ExprNodeRef size;
			public ExprNodeRef orientation;
			public ExprNodeRef spacing;

			public void Generate(ref QSData data, NativeList<float2> items, Span<UnsafeComponentReference> components)
			{
				float2 center = this.center.Evaluate<float2>(ref data.exprData, components);
				float2 size = this.size.Evaluate<float2>(ref data.exprData, components);
				float orientation = this.orientation.Evaluate<float>(ref data.exprData, components);
				float spacing = this.spacing.Evaluate<float>(ref data.exprData, components);

				math.sincos(orientation, out var s, out var c);
				var basis = spacing * new float2(c - s, s + c);

				var extent = 0.5f * size;

				int yi = 0;
				for(float y = 0; y < extent.y; y += spacing, yi++)
				{
					int xi = 0;
					for(float x = 0; x < extent.x; x += spacing, xi++)
					{
						items.Add(center + new float2( basis.x * xi,  basis.y * yi));
						if(xi > 0 && yi > 0)
						{
							items.Add(center + new float2( basis.x * xi, -basis.y * yi));
							items.Add(center + new float2(-basis.x * xi,  basis.y * yi));
							items.Add(center + new float2(-basis.x * xi, -basis.y * yi));
						}
					}
				}
			}
		}

		static void CheckType<TExpected, TActual>()
		{
			if(typeof(TExpected) != typeof(TActual))
				throw new Exception();
		}

		public void Generate<TItem>(ref QSData data, NativeList<TItem> items, Span<UnsafeComponentReference> components) where TItem : unmanaged
		{
			switch(generatorType)
			{
				case GeneratorType.Float2Rectangle:
					CheckType<float2, TItem>();
					unsafe
					{
						this.data.float2Rectangle.Generate(ref data, *(NativeList<float2>*)&items, components);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}

	public struct QSFilter
	{
		public ExprNodeRef expr;
		public int nodeIndex;
		public FilterType type;

		public enum FilterType
		{
			Expression,

			// TODO
			// Function,
		}

		public void Pass<TItem>(ref QSData data, ref QSTempState tempState, Span<TItem> items, NativeBitArray passBits, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			switch(type)
			{
				case FilterType.Expression:
				{
					// TODO: vectorized expressions
					int i = 0;
					foreach (ref var item in items)
					{
						tempState.GetCurrentItem<TItem>() = item;
						passBits.Set(i, expr.Evaluate<bool>(ref data.exprData, componentPtrs));
					}

					break;
				}

				default:
					throw new NotImplementedException();
			}
		}
	}

	public struct QSScorer
	{
		public ExprNodeRef expr;
		public ScorerType type;

		public enum ScorerType
		{
			Expression,
		}

		public void Score<TItem>(ref ExprData exprData, ref QSTempState tempState, Span<TItem> items, Span<QSItem> scores, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			switch(type)
			{
				case ScorerType.Expression:
					// TODO: vectorized expressions
					for(int i = 0; i < items.Length; ++i)
					{
						tempState.GetCurrentItem<TItem>() = items[i];
						scores[i].score += expr.Evaluate<float>(ref exprData, componentPtrs);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}

	public static class QueryExecution
	{
		/// <summary>
		/// Execute a query, getting the N best items based on query data.
		/// </summary>
		/// <typeparam name="TItem"></typeparam>
		/// <param name="data"></param>
		/// <param name="componentPtrs"></param>
		/// <param name="allocator"></param>
		/// <returns></returns>
		public static NativeList<TItem> Execute<TItem>(ref QSData data, Span<UnsafeComponentReference> componentPtrs, AllocatorManager.AllocatorHandle allocator) where TItem : unmanaged
		{
			int resultCount = data.resultCount.Evaluate<int>(ref data.exprData, componentPtrs);

			var items = new NativeList<TItem>(Allocator.Temp);
			QSTempState tempState = default;

			Span<UnsafeComponentReference> supComponentPtrs = stackalloc UnsafeComponentReference[componentPtrs.Length + 1];
			componentPtrs.CopyTo(supComponentPtrs);
			supComponentPtrs[^1] = UnsafeComponentReference.Make(ref tempState);

			int passIndex = 0;
			int passItemStartIndex = 0;

			NativeBitArray passBits = new  NativeBitArray(resultCount, Allocator.Temp);
			NativeList<int> itemsPerPass = new NativeList<int>(Allocator.Temp);

			// generate and filter items until we've gone through enough passes to accept the desired amount of items
			for(; passIndex < data.passes.Length; ++passIndex)
			{
				ref var pass = ref data.passes[passIndex];

				foreach(ref var generator in pass.generators.AsSpan())
					generator.Generate(ref data, items, componentPtrs);

				var unfilteredItems = items.AsArray().AsSpan().Slice(passItemStartIndex);
				int newItemCount = unfilteredItems.Length;
				passBits.Resize(newItemCount, NativeArrayOptions.UninitializedMemory);

				foreach(ref var filter in pass.filters.AsSpan())
					filter.Pass(ref data, ref tempState, unfilteredItems, passBits, componentPtrs);

				for (int i = passItemStartIndex; i < items.Length;)
				{
					int passItemIndex = i - passItemStartIndex;
					if (!passBits.IsSet(passItemIndex))
					{
						// passBits.RemoveAtSwapBack(passItemIndex);
						passBits.Set(passItemIndex, passBits.IsSet(passBits.Length - 1));
						passBits.Resize(passBits.Length - 1);

						items.RemoveAtSwapBack(i);
					}
					else
					{
						++i;
					}
				}

				itemsPerPass.Add(items.Length - passItemStartIndex);

				passItemStartIndex = items.Length;

				if(items.Length >= resultCount)
					break; // got enough items, skipping further fallback passes
			}

			var scores = new NativeList<QSItem>(items.Length, Allocator.Temp);
			scores.ResizeUninitialized(items.Length);
			for(int i = 0; i < scores.Length; ++i)
				scores.ElementAt(i).itemIndex = i;

			int passCount = passIndex + 1;
			passItemStartIndex = 0;
			for(passIndex = 0; passIndex < passCount; ++passIndex)
			{
				ref var pass = ref data.passes[passIndex];
				int passItemCount = itemsPerPass[passIndex];
				var passItems = items.AsArray().AsSpan().Slice(passItemStartIndex, passItemCount);
				var passScores = scores.AsArray().AsSpan().Slice(passItemStartIndex, passItemCount);

				foreach(ref var scorer in pass.scorers.AsSpan())
				{
					scorer.Score(ref data.exprData, ref tempState, passItems, passScores, componentPtrs);
				}

				passItemStartIndex += passItemCount;
			}

			// TODO: this is O(n log n + k) but can be done in O(kn) or O(n log k)
			scores.Sort(default(QSItem.ScoreComparer));

			resultCount = math.min(resultCount, items.Length);

			var result = new NativeList<TItem>(resultCount, allocator);

			for(int i = 0; i < scores.Length && i < resultCount; ++i)
				result.Add(items[scores[i].itemIndex]);

			return result;
		}
	}
}

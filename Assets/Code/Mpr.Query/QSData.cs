using Mpr.Blobs;
using Mpr.Burst;
using Mpr.Expr;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

/*
 * TODO: v2 with entity-level batching and parallelism
 *
 * suppose you want every enemy in a survivor-like to query the nearest player for their AI
 * - the generator returns 1-4 items per enemy entity
 * - the filter and scorer process 1-4 items each
 *
 * in this situation it would clearly be beneficial to be able to vectorize the loops over many enemy entities so
 *
 * instead of this:
 * ----------------
 * 
 * foreach entity in monsters:
 *	items = generator(entity)
 *  filter(items)
 *  scores = score(items)
 *  sort(scores)
 *  mark(scores, items, N)
 *
 * you'd structure the loop like this:
 * -----------------------------------
 *
 * foreach batch in batched(monsters):
 *  items, indices = generator(batch) // if we want to get *really* fancy we could even detect that this doesn't depend on the entity and run it only once
 *  filter(items)
 *  scores = score(items)
 *  foreach span in indices:
 *	  sort(span, scores)
 *  foreach span in indices:
 *    mark(span, scores, items, N)
 *
 * but to do this we'd really need a vectorizable expression evaluator as well
 */

namespace Mpr.Query
{
	public struct QSData
	{
		/// <summary>
		/// Baked data for all expressions in the query graph
		/// </summary>
		public BlobExpressionData exprData;

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
		/// Result item type
		/// </summary>
		public ExpressionValueType itemType;

		/// <summary>
		/// An expression to evaluate to determine the desired result item count
		/// </summary>
		public ExpressionRef resultCount;
	}

	public struct QSEntityQueryReference : IBufferElementData
	{
		public BlobAssetReference<BlobEntityQueryDesc> entityQueryDesc;

		/// <summary>
		/// Gets a runtime key that can be used to look up the results for a query
		/// </summary>
		/// <returns></returns>
		public IntPtr GetRuntimeKey()
		{
			unsafe
			{
				return (IntPtr)entityQueryDesc.GetUnsafePtr();
			}
		}
	}

	[ChunkSerializable]
	[BurstCompile]
	public struct QSEntityQuery : ISharedComponentData, IEquatable<QSEntityQuery>
	{
		public BlobAssetReference<BlobEntityQueryDesc> entityQueryDesc;

		/// <summary>
		/// Runtime-resolved query for this description
		/// </summary>
		public EntityQuery runtimeEntityQuery;

		/// <summary>
		/// Most recent results for evaluating the entity query.
		/// </summary>
		public NativeList<Entity> results;

		#region IEquatable for SharedComponent keying
		[BurstCompile]
		public bool Equals(QSEntityQuery other)
		{
			return entityQueryDesc.Equals(other.entityQueryDesc);
		}

		public override bool Equals(object obj)
		{
			return obj is QSEntityQuery other && Equals(other);
		}

		[BurstCompile]
		public override int GetHashCode()
		{
			return entityQueryDesc.GetHashCode();
		}
		#endregion

		/// <summary>
		/// Gets a runtime key that can be used to look up the results for a query
		/// </summary>
		/// <returns></returns>
		public IntPtr GetRuntimeKey()
		{
			unsafe
			{
				return (IntPtr)entityQueryDesc.GetUnsafePtr();
			}
		}
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

	public struct QSCurrentItemStorage
	{
		double4 data0;
		double4 data1;
		double4 data2;
		double4 data3;
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

			[FieldOffset(0)] public Entities entities;
		}

		public enum GeneratorType
		{
			Float2Rectangle,
			Entities,
		}

		public struct Float2Rectangle
		{
			public ExpressionRef center;
			public ExpressionRef size;
			public ExpressionRef orientation;
			public ExpressionRef spacing;

			public void Generate(
				ref QSData data,
				in ExpressionEvalContext ctx,
				DynamicBuffer<QSEntityQueryReference> entityQueries,
				NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup,
				NativeList<float2> items)
			{
				float2 center = this.center.Evaluate<float2>(in ctx);
				float2 size = this.size.Evaluate<float2>(in ctx);
				float orientation = this.orientation.Evaluate<float>(in ctx);
				float spacing = this.spacing.Evaluate<float>(in ctx);

				math.sincos(orientation, out var s, out var c);
				var basis = spacing * new float2(c - s, s + c);

				var extent = 0.5f * size;

				int yi = 0;
				for(float y = 0; y < extent.y; y += spacing, yi++)
				{
					int xi = 0;
					for(float x = 0; x < extent.x; x += spacing, xi++)
					{
						items.Add(center + new float2(basis.x * xi, basis.y * yi));
						if(xi > 0 && yi > 0)
						{
							items.Add(center + new float2(basis.x * xi, -basis.y * yi));
							items.Add(center + new float2(-basis.x * xi, basis.y * yi));
							items.Add(center + new float2(-basis.x * xi, -basis.y * yi));
						}
					}
				}
			}
		}

		public struct Entities
		{
			public int queryIndex;

			public void Generate(ref QSData data, in ExpressionEvalContext ctx, DynamicBuffer<QSEntityQueryReference> entityQueries, NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup, NativeList<Entity> items)
			{
				if(queryResultLookup.TryGetValue(entityQueries[queryIndex].GetRuntimeKey(), out var results))
					items.CopyFrom(results);
			}
		}

		static void CheckType<TExpected, TActual>()
		{
			if(typeof(TExpected) != typeof(TActual))
				throw new Exception();
		}

		public void Generate<TItem>(ref QSData data, in ExpressionEvalContext ctx, DynamicBuffer<QSEntityQueryReference> entityQueries, NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup, NativeList<TItem> items) where TItem : unmanaged
		{
			switch(generatorType)
			{
				case GeneratorType.Float2Rectangle:
					CheckType<float2, TItem>();
					unsafe
					{
						this.data.float2Rectangle.Generate(ref data, in ctx, entityQueries, queryResultLookup, *(NativeList<float2>*)&items);
					}
					break;

				case GeneratorType.Entities:
					CheckType<Entity, TItem>();
					unsafe
					{
						this.data.entities.Generate(ref data, in ctx, entityQueries, queryResultLookup, *(NativeList<Entity>*)&items);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}

	public struct QSFilter
	{
		public ExpressionRef expr;
		public int nodeIndex;
		public FilterType type;

		public enum FilterType
		{
			Expression,

			// TODO
			// Function,
		}

		public void Pass<TItem>(ref QSData data, in ExpressionEvalContext ctx, ref QSTempState tempState, Span<TItem> items, NativeBitArray passBits) where TItem : unmanaged
		{
			switch(type)
			{
				case FilterType.Expression:
					{
						// TODO: vectorized expressions
						int i = 0;
						foreach(ref var item in items)
						{
							tempState.GetCurrentItem<TItem>() = item;
							passBits.Set(i, expr.Evaluate<bool>(in ctx));
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
		public ExpressionRef expr;
		public ScorerType type;

		public enum ScorerType
		{
			Expression,
		}

		public void Score<TItem>(in ExpressionEvalContext ctx, ref QSTempState tempState, Span<TItem> items, Span<QSItem> scores, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			switch(type)
			{
				case ScorerType.Expression:
					// TODO: vectorized expressions
					for(int i = 0; i < items.Length; ++i)
					{
						tempState.GetCurrentItem<TItem>() = items[i];
						scores[i].score += expr.Evaluate<float>(in ctx);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}

	[InternalBufferCapacity(8)]
	public struct QSResultItemStorage : IBufferElementData
	{
		public long storage;
	}

	public static class QueryExecution
	{
		/// <summary>
		/// Execute a query, getting the N best items based on query data.
		/// </summary>
		/// <typeparam name="TItem"></typeparam>
		/// <param name="data"></param>
		/// <param name="componentPtrs">Components on the querier entity</param>
		/// <param name="entityQueries">List of resolved generator entity queries attached to the querier entity</param>
		/// <param name="results">Result item storage buffer</param>
		/// <param name="tempAlloc">Temporary allocator for working data within the Execute function</param>
		/// <returns></returns>
		public static void Execute<TItem>(
			ref QSData data,
			NativeArray<UnsafeComponentReference> componentPtrs,
			DynamicBuffer<QSEntityQueryReference> entityQueries,
			NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup,
			DynamicBuffer<QSResultItemStorage> results,
			Allocator tempAlloc = Allocator.Temp
			) where TItem : unmanaged
		{
			var exprContext = new ExpressionEvalContext(ref data.exprData, componentPtrs, default);

			int resultCount = data.resultCount.Evaluate<int>(in exprContext);

			var items = new NativeList<TItem>(tempAlloc);
			QSTempState tempState = default;

			NativeArray<UnsafeComponentReference> supComponentPtrs = new NativeArray<UnsafeComponentReference>(componentPtrs.Length + 1, Allocator.Temp);
			componentPtrs.CopyTo(supComponentPtrs);
			supComponentPtrs[^1] = UnsafeComponentReference.Make(ref tempState);

			int passIndex = 0;
			int passItemStartIndex = 0;

			NativeBitArray passBits = new NativeBitArray(resultCount, tempAlloc);
			var scores = new NativeList<QSItem>(tempAlloc);

			// generate and filter items until we've gone through enough passes to accept the desired amount of items
			for(; passIndex < data.passes.Length; ++passIndex)
			{
				ref var pass = ref data.passes[passIndex];

				// generate items
				foreach(ref var generator in pass.generators.AsSpan())
					generator.Generate(ref data, in exprContext, entityQueries, queryResultLookup, items);

				var unfilteredItems = items.AsArray().AsSpan().Slice(passItemStartIndex);
				int newItemCount = unfilteredItems.Length;
				passBits.Resize(newItemCount, NativeArrayOptions.UninitializedMemory);

				// compute filters
				foreach(ref var filter in pass.filters.AsSpan())
					filter.Pass(ref data, in exprContext, ref tempState, unfilteredItems, passBits);

				// remove filtered items
				for(int i = passItemStartIndex; i < items.Length;)
				{
					int passItemIndex = i - passItemStartIndex;
					if(!passBits.IsSet(passItemIndex))
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

				// score remaining items
				var filteredItems = items.AsArray().AsSpan().Slice(passItemStartIndex);
				var passScores = scores.AsArray().AsSpan().Slice(passItemStartIndex);

				scores.ResizeUninitialized(items.Length);

				foreach(ref var scorer in pass.scorers.AsSpan())
					scorer.Score(in exprContext, ref tempState, filteredItems, passScores, componentPtrs);

				passItemStartIndex = items.Length;

				if(items.Length >= resultCount)
					break; // got enough items, skipping further fallback passes
			}

			// TODO: this is O(n log n + k) but can be done in O(kn) or O(n log k)
			scores.Sort(default(QSItem.ScoreComparer));

			resultCount = math.min(resultCount, items.Length);

			results.Clear();
			results.ResizeUninitialized(1 + (resultCount * UnsafeUtility.SizeOf<TItem>()) / UnsafeUtility.SizeOf<QSResultItemStorage>());
			results.ElementAt(0).storage = resultCount;

			var resultBuffer = results.AsNativeArray().AsSpan().Slice(1).Cast<QSResultItemStorage, TItem>();

			for(int i = 0; i < scores.Length && i < resultCount; ++i)
				resultBuffer[i] = items[scores[i].itemIndex];

			items.Dispose();
			passBits.Dispose();
			scores.Dispose();
		}
	}
}

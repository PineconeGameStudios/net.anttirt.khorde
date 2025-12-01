#if false
#define FPTR_SUPPORT
#endif

using Unity.Entities;
using Mpr.Blobs;
using Unity.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Mpr.Expr;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using Unity.Mathematics;

#if FPTR_SUPPORT
using AOT;
using System.Reflection;
using UnityEngine.Scripting;
#endif

namespace Mpr.Query
{
	public partial struct QSData
	{
		public ExprData exprData;
		public BlobArray<QSPass> passes;
		public ulong itemTypeHash;
		public ExprNodeRef resultCount;

		// TODO: fptr support
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
		public BlobArray<QSGenerator> generators;
		public BlobArray<QSFilter> filters;
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
#if FPTR_SUPPORT
			Function,
#endif
		}

		public bool Pass<TItem>(ref QSData data, ref QSTempState tempState, ref TItem item, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			switch(type)
			{
				case FilterType.Expression:
					return expr.Evaluate<bool>(ref data.exprData, componentPtrs);

#if FPTR_SUPPORT
				case FilterType.Function:
					return data.InvokeFilter(nodeIndex, ref data.exprData, ref item, componentPtrs);
#endif

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

		public void Score<TItem>(ref ExprData exprData, ref QSTempState tempState, NativeList<TItem> items, NativeList<QSItem> scores, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			switch(type)
			{
				case ScorerType.Expression:
					// TODO: expression array evaluation
					for(int i = 0; i < items.Length; ++i)
					{
						tempState.GetCurrentItem<TItem>() = items.ElementAt(i);
						scores.ElementAt(i).score += expr.Evaluate<float>(ref exprData, componentPtrs);
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
			int unfilteredStartIndex = 0;

			// generate and filter items until we've gone through enough passes to accept the desired amount of items
			for(; passIndex < data.passes.Length; ++passIndex)
			{
				ref var pass = ref data.passes[passIndex];

				foreach(ref var generator in pass.generators.AsSpan())
					generator.Generate(ref data, items, componentPtrs);

				// TODO: pass items array directly to filter so it can have a tight inner loop
				for(int i = unfilteredStartIndex; i < items.Length;)
				{
					bool passedFilter = true;

					tempState.GetCurrentItem<TItem>() = items.ElementAt(i);

					foreach(ref var filter in pass.filters.AsSpan())
					{
						if(!filter.Pass(ref data, ref tempState, ref items.ElementAt(i), componentPtrs))
						{
							passedFilter = false;
							break;
						}
					}

					if(!passedFilter)
					{
						items.RemoveAtSwapBack(i);
						continue;
					}

					i++;
				}

				unfilteredStartIndex = items.Length;

				if(items.Length >= resultCount)
					break; // got enough items, skipping further fallback passes
			}

			var scores = new NativeList<QSItem>(items.Length, Allocator.Temp);
			scores.ResizeUninitialized(items.Length);
			for(int i = 0; i < scores.Length; ++i)
				scores.ElementAt(i).itemIndex = i;

			int passCount = passIndex + 1;
			for(passIndex = 0; passIndex < passCount; ++passIndex)
			{
				ref var pass = ref data.passes[passIndex];

				foreach(ref var scorer in pass.scorers.AsSpan())
				{
					scorer.Score(ref data.exprData, ref tempState, items, scores, componentPtrs);
				}
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

#if FPTR_SUPPORT
	namespace Internal
	{
		public interface IQueryNode { }
	}

	public interface IQueryFilter<TItem> : Internal.IQueryNode
	{
		bool Pass(in TItem item); // TODO: support passing in components like IJobEntity
	}

	unsafe delegate void GeneratorDelegate(void* @this, void* itemsList, ref ExprData data, Span<UnsafeComponentReference> componentPtrs);
	unsafe delegate bool FilterDelegate(void* @this, void* item, ref ExprData data, Span<UnsafeComponentReference> componentPtrs);
	unsafe delegate float ScorerDelegate(void* @this, void* item, ref ExprData data, Span<UnsafeComponentReference> componentPtrs);

	public partial struct QSData
	{
		// populate function pointers
		public void RuntimeInitialize()
		{
			if(runtimeInitComplete)
				return;

			foreach(ref var nodeHeader in nodeHeaders.AsSpan())
				nodeHeader.func = QuerySystemInit.s_functionLookup[nodeHeader.stableTypeHash];

			runtimeInitComplete = true;
		}

		internal void InvokeGenerator<TItem>(int index, ref ExprData data, NativeList<TItem> itemsList, Span<UnsafeComponentReference> componentPtrs)
			where TItem : unmanaged
		{
			var fptr = new FunctionPointer<GeneratorDelegate>(nodeHeaders[index].func);
			unsafe
			{
				void* nodeData = ((byte*)nodeStorage.GetUnsafePtr()) + nodeHeaders[index].offset;
				void* itemsListPtr = &itemsList;
				fptr.Invoke(nodeData, itemsListPtr, ref data, componentPtrs);
			}
		}

		internal bool InvokeFilter<TItem>(int index, ref ExprData data, ref TItem item, Span<UnsafeComponentReference> componentPtrs)
			where TItem : unmanaged
		{
			var fptr = new FunctionPointer<FilterDelegate>(nodeHeaders[index].func);
			unsafe
			{
				void* nodeData = ((byte*)nodeStorage.GetUnsafePtr()) + nodeHeaders[index].offset;
				fixed(void* itemPtr = &item)
					return fptr.Invoke(nodeData, itemPtr, ref data, componentPtrs);
			}
		}

		internal float InvokeScorer<TItem>(int index, ref ExprData data, ref TItem item, Span<UnsafeComponentReference> componentPtrs)
			where TItem : unmanaged
		{
			var fptr = new FunctionPointer<ScorerDelegate>(nodeHeaders[index].func);
			unsafe
			{
				void* nodeData = ((byte*)nodeStorage.GetUnsafePtr()) + nodeHeaders[index].offset;
				fixed(void* itemPtr = &item)
					return fptr.Invoke(nodeData, itemPtr, ref data, componentPtrs);
			}
		}
	}

	public static class QuerySystemInit
	{
		public static Dictionary<ulong, IntPtr> s_functionLookup = new();

		public static void Init()
		{
			var filterTypes = TypeCache.GetTypesDerivedFrom<Internal.IQueryNode>();
			foreach(var type in filterTypes)
			{
				var (stableTypeHash, fptr, _) = ((ulong, IntPtr, object))type
					.GetMethod("MakeCallInfo", BindingFlags.Static | BindingFlags.NonPublic)
					.Invoke(null, Array.Empty<object>());

				s_functionLookup[stableTypeHash] = fptr;
			}
		}
	}

	public partial struct MyTestFilter : IQueryFilter<float2>
	{
		public float maxLength;

		[BurstCompile]
		public bool Pass(in float2 item)
		{
			return math.length(item) >= maxLength;
		}
	}

	[BurstCompile] // only if the function has [BurstCompile] but the struct doesn't
	public partial struct MyTestFilter
	{
		[Preserve] // Burst version
		static (ulong, IntPtr, object) MakeCallInfo()
		{
			var typeHash = TypeManager.GetTypeInfo<MyTestFilter>().StableTypeHash;

			unsafe
			{
				FilterDelegate filterDelegate = InvokePass;
				var fptr = BurstCompiler.CompileFunctionPointer(filterDelegate);
				return (typeHash, fptr.Value, filterDelegate);
			}
		}

		// [Preserve] // Non-Burst version
		// static (ulong, IntPtr, object) MakeCallInfo_NoBurst()
		// {
		// 	var typeHash = TypeManager.GetTypeInfo<MyTestFilter>().StableTypeHash;

		// 	unsafe
		// 	{
		// 		FilterDelegate filterDelegate = InvokePass;
		// 		var fptr = Marshal.GetFunctionPointerForDelegate(filterDelegate);
		// 		return (typeHash, fptr, filterDelegate);
		// 	}
		// }

		[BurstCompile]
		[MonoPInvokeCallback(typeof(FilterDelegate))]
		static unsafe bool InvokePass(void* @this, void* itemPtr, ref ExprData data, Span<UnsafeComponentReference> components)
		{
			// TODO: pass ExprData in case the node wants to evaluate expressions
			// TODO: pass components
			return ((MyTestFilter*)@this)->Pass(in *(float2*)itemPtr);
		}
	}
#endif
}

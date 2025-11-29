using Unity.Entities;
using Mpr.Blobs;
using Unity.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Mpr.Expr;

namespace Mpr.Query
{
	public struct QSData
	{
		public BlobArray<QSGenerator> generators;
		public BlobArray<QSFilter> filters;
		public BlobArray<QSScorer> scorers;
		public BlobArray<QSCollector> collector;
		public int itemCount;
		public BTExprData exprData;
	}

	public struct QSItem<TItem>
	{
		public float score;
		public TItem item;

		public struct ScoreComparer : IComparer<QSItem<TItem>>
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(QSItem<TItem> x, QSItem<TItem> y) => y.score.CompareTo(x.score);
		}
	}

	public struct QSGenerator
	{
		// TODO: hand-craft some generators for different types: spatial for float2/float3, Entity for player, etc.

		public void Generate<TItem>(ref QSData data, NativeList<QSItem<TItem>> items) where TItem : unmanaged
		{
			throw new System.NotImplementedException();
		}
	}

	public struct QSFilter
	{
		// just use a boolean expression from bt exprs here?
		// we could have a pseudo-variable expression with no inputs and one output that is the current item
		// we could even allow reads from blackboard variables and components (but not writes)
		// "subgraph inputs" could be query parameters that can be filled in within the bt
		public BTExprNodeRef expr;

		// TODO: implement some filter expressions

		public bool Pass<TItem>(ref BTExprData data, in TItem item, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			// TODO: place the current item in a special location for the "Current Item" expr node to find it
			return expr.Evaluate<bool>(ref data, componentPtrs);
		}
	}

	public struct QSScorer
	{
		// just use a floating point expression from bt exprs here?
		public BTExprNodeRef expr;

		public float Score<TItem>(ref BTExprData data, in TItem item, Span<UnsafeComponentReference> componentPtrs) where TItem : unmanaged
		{
			// TODO: place the current item in a special location for the "Current Item" expr node to find it
			return expr.Evaluate<float>(ref data, componentPtrs);
		}
	}

	public struct QSCollector
	{
		public int generatorIndex;
		public BlobArray<byte> filters;
		public BlobArray<byte> scorers;
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
		public static NativeList<QSItem<TItem>> Execute<TItem>(ref QSData data, Span<UnsafeComponentReference> componentPtrs, AllocatorManager.AllocatorHandle allocator) where TItem : unmanaged
		{
			var items = new NativeList<QSItem<TItem>>(allocator);
			int startIndex = 0;

			foreach(ref var collector in data.collector.AsSpan())
			{
				ref var generator = ref data.generators[collector.generatorIndex];
				generator.Generate(ref data, items);

				for(int i = startIndex; i < items.Length;)
				{
					bool pass = true;

					foreach(ref var filterIndex in collector.filters.AsSpan())
					{
						ref var filter = ref data.filters[filterIndex];
						if(!filter.Pass(ref data.exprData, in items.ElementAt(i).item, componentPtrs))
						{
							pass = false;
							break;
						}
					}

					if(!pass)
					{
						items.RemoveAtSwapBack(i);
						continue;
					}

					foreach(ref var scorerIndex in collector.scorers.AsSpan())
					{
						ref var scorer = ref data.scorers[scorerIndex];
						ref var pair = ref items.ElementAt(i);
						pair.score = scorer.Score(ref data.exprData, in pair.item, componentPtrs);
					}

					i++;
				}

				startIndex = items.Length;

				if(items.Length >= data.itemCount)
					break; // got enough, not using fallback collectors
			}

			items.Sort(default(QSItem<TItem>.ScoreComparer));

			if(items.Length > data.itemCount)
				items.Resize(items.Length, NativeArrayOptions.ClearMemory);

			return items;
		}
	}

}

using Mpr.Blobs;
using Mpr.Expr;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace Mpr.Query;

public unsafe ref struct QueryExecutionContext
{
	readonly QSData* pData;
	public ref QSData data => ref *pData;
	public NativeArray<UnsafeComponentReference> componentPtrs;
	public NativeArray<UntypedComponentLookup> lookups;
	public NativeHashMap<Hash128, NativeList<Entity>> queryResultLookup;

	/// <summary>
	/// Create a query execution context
	/// </summary>
	/// <param name="data"></param>
	/// <param name="componentPtrs">Components on the querier entity</param>
	/// <param name="queryResultLookup">Result lookup for entity queries if any are present</param>
	public QueryExecutionContext(
		ref QSData data,
		NativeArray<UnsafeComponentReference> componentPtrs,
		NativeArray<UntypedComponentLookup> lookups,
		NativeHashMap<Hash128, NativeList<Entity>> queryResultLookup
	)
	{
		fixed(QSData* pData = &data)
			this.pData = pData;
		this.componentPtrs = componentPtrs;
		this.lookups = lookups;
		this.queryResultLookup = queryResultLookup;
	}

	/// <summary>
	/// Execute a query, getting the N best items based on query data.
	/// </summary>
	/// <typeparam name="TItem"></typeparam>
	/// <param name="blackboard">The blackboard buffer containing input variables</param>
	/// <param name="results">The buffer to store the results in</param>
	/// <param name="tempAlloc">Temporary allocator for working data within the Execute function</param>
	/// <returns>Number of result items</returns>
	public int Execute<TItem>(
		DynamicBuffer<ExpressionBlackboardStorage> blackboard,
		ref ExpressionBlackboardLayout blackboardLayout,
		DynamicBuffer<QSResultItemStorage> results,
		ExpressionBlackboardLayout.Slice resultSlice,
		Allocator tempAlloc = Allocator.Temp) where TItem : unmanaged
	{
		data.exprData.CheckExpressionComponents(componentPtrs, lookups);

		var items = new NativeList<TItem>(tempAlloc);
		QSTempState tempState = default;

		NativeArray<UnsafeComponentReference> supComponentPtrs = new NativeArray<UnsafeComponentReference>(componentPtrs.Length + 1, Allocator.Temp);
		componentPtrs.CopyTo(supComponentPtrs.GetSubArray(0, componentPtrs.Length));
		supComponentPtrs[^1] = UnsafeComponentReference.Make(ref tempState);

		NativeArray<byte> blackboardBytes = default;
		if(blackboard.IsCreated)
		{
			blackboardBytes = blackboard.AsNativeArray()
				.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());
		}

		var exprContext = new ExpressionEvalContext(ref data.exprData, supComponentPtrs, lookups, blackboardBytes, ref blackboardLayout);
		int resultCount = data.resultCount.Evaluate<int>(in exprContext);

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
				generator.Generate(in this, in exprContext, items);

			var unfilteredItems = items.AsArray().GetSubArray(passItemStartIndex, items.Length - passItemStartIndex);

			//foreach (var unfilteredItem in unfilteredItems)
			//{
			//    Debug.Log($"generated {unfilteredItem}");
			//}

			int newItemCount = unfilteredItems.Length;
			passBits.Resize(newItemCount, NativeArrayOptions.UninitializedMemory);

			// compute filters
			foreach(ref var filter in pass.filters.AsSpan())
				filter.Pass(in this, in exprContext, ref tempState, unfilteredItems, passBits);

			//for (int i = passItemStartIndex; i < passItemStartIndex + newItemCount; ++i)
			//{
			//    int passItemIndex = i - passItemStartIndex;
			//    Debug.Log($"{unfilteredItems[i]}: pass: {passBits.IsSet(passItemIndex)}");
			//}

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

			scores.Resize(items.Length, NativeArrayOptions.ClearMemory);

			// score remaining items
			var filteredItems = items.AsArray().GetSubArray(passItemStartIndex, items.Length - passItemStartIndex);
			var passScores = scores.AsArray().GetSubArray(passItemStartIndex, items.Length - passItemStartIndex);

			//foreach (var filteredItem in filteredItems)
			//    Debug.Log($"generated {filteredItem}");

			foreach(ref var scorer in pass.scorers.AsSpan())
				scorer.Score(in this, in exprContext, ref tempState, filteredItems, passScores);

			passItemStartIndex = items.Length;

			if(items.Length >= resultCount)
				break; // got enough items, skipping further fallback passes
		}

		for(int i = 0; i < scores.Length; ++i)
			scores.ElementAt(i).itemIndex = i;

		// TODO: this is O(n log n + k) but can be done in O(kn) or O(n log k)
		if(data.scoringDirection == QueryScoringDirection.LargestWins)
			scores.Sort(default(QSItem.ScoreComparerGreater));
		else
			scores.Sort(default(QSItem.ScoreComparerLess));

		//int scoreIndex = 0;
		//foreach (var score in scores)
		//{
		//    Debug.Log($"#{scoreIndex+1}: [{score.itemIndex}] {items[score.itemIndex]} (@{score.score})");
		//    scoreIndex++;
		//}

		resultCount = math.min(resultCount, items.Length);

		if(resultSlice.length > 0)
		{
			// write to blackboard
			var resultBytes = blackboardBytes.GetSubArray(resultSlice.offset, resultSlice.length);
			if(resultSlice.array)
			{
				// length-prefixed array result
				// TODO
				throw new NotImplementedException();
			}
			else
			{
				// scalar result
				if(resultCount > 0)
				{
					resultCount = 1;
					items.AsArray().GetSubArray(scores[0].itemIndex, 1).Reinterpret<byte>(UnsafeUtility.SizeOf<TItem>()).CopyTo(resultBytes);
				}
			}
		}
		else
		{
			// write to result buffer
			var resultBuffer = results.AllocateResultArray<TItem>(resultCount);

			for(int i = 0; i < scores.Length && i < resultCount; ++i)
				resultBuffer[i] = items[scores[i].itemIndex];
		}

		items.Dispose();
		passBits.Dispose();
		scores.Dispose();

		return resultCount;
	}
}
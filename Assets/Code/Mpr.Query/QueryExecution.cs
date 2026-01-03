using System;
using Mpr.Blobs;
using Mpr.Burst;
using Mpr.Expr;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Mpr.Query;

public unsafe ref struct QueryExecutionContext
{
    readonly QSData* pData;
    public ref QSData data => ref *pData;
    public NativeArray<UnsafeComponentReference> componentPtrs;
    public NativeArray<QSEntityQueryReference> entityQueries;
    public NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup;

    /// <summary>
    /// Create a query execution context
    /// </summary>
    /// <param name="data"></param>
    /// <param name="componentPtrs">Components on the querier entity</param>
    /// <param name="entityQueries">List of resolved generator entity queries attached to the querier entity</param>
    /// <param name="queryResultLookup">Result lookup for entity queries if any are present</param>
    /// <param name="results">Result item storage buffer</param>
    public QueryExecutionContext(
        ref QSData data,
        NativeArray<UnsafeComponentReference> componentPtrs,
        NativeArray<QSEntityQueryReference> entityQueries,
        NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup
    )
    {
        fixed(QSData* pData = &data)
            this.pData =  pData;
        this.componentPtrs = componentPtrs;
        this.entityQueries = entityQueries;
        this.queryResultLookup = queryResultLookup;
    }
    
    /// <summary>
    /// Execute a query, getting the N best items based on query data.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    /// <param name="results">The buffer to store the results in</param>
    /// <param name="tempAlloc">Temporary allocator for working data within the Execute function</param>
    /// <returns></returns>
    public void Execute<TItem>(DynamicBuffer<QSResultItemStorage> results, Allocator tempAlloc = Allocator.Temp) where TItem : unmanaged
    {
        var exprContext = new ExpressionEvalContext(ref data.exprData, componentPtrs, default);

        int resultCount = data.resultCount.Evaluate<int>(in exprContext);

        var items = new NativeList<TItem>(tempAlloc);
        QSTempState tempState = default;

        NativeArray<UnsafeComponentReference> supComponentPtrs = new NativeArray<UnsafeComponentReference>(componentPtrs.Length + 1, Allocator.Temp);
        componentPtrs.CopyTo(supComponentPtrs.GetSubArray(0, componentPtrs.Length));
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
                generator.Generate(in this, in exprContext, items);

            var unfilteredItems = items.AsArray().AsSpan().Slice(passItemStartIndex);
            int newItemCount = unfilteredItems.Length;
            passBits.Resize(newItemCount, NativeArrayOptions.UninitializedMemory);

            // compute filters
            foreach(ref var filter in pass.filters.AsSpan())
                filter.Pass(in this, in exprContext, ref tempState, unfilteredItems, passBits);

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
                scorer.Score(in this, in exprContext, ref tempState, filteredItems, passScores);

            passItemStartIndex = items.Length;

            if(items.Length >= resultCount)
                break; // got enough items, skipping further fallback passes
        }

        // TODO: this is O(n log n + k) but can be done in O(kn) or O(n log k)
        scores.Sort(default(QSItem.ScoreComparer));

        resultCount = math.min(resultCount, items.Length);

        var resultBuffer = results.AllocateResultArray<TItem>(resultCount);

        for(int i = 0; i < scores.Length && i < resultCount; ++i)
            resultBuffer[i] = items[scores[i].itemIndex];

        items.Dispose();
        passBits.Dispose();
        scores.Dispose();
    }
}
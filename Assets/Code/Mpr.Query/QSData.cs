using Mpr.Blobs;
using Mpr.Expr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public const int SchemaVersion = 1;
        
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
        /// Which way to select results
        /// </summary>
        public QueryScoringDirection scoringDirection;

        /// <summary>
        /// Result item type
        /// </summary>
        public ExpressionValueType itemType;

        /// <summary>
        /// An expression to evaluate to determine the desired result item count
        /// </summary>
        public ExpressionRef resultCount;

        /// <summary>
        /// Entity queries for the Entities generator
        /// </summary>
        public BlobArray<BlobEntityQueryDesc> entityQueries;
    }

    public enum QueryScoringDirection
    {
        SmallestWins,
        LargestWins
    }

    [ChunkSerializable]
    [BurstCompile]
    public struct QSEntityQuery : ISharedComponentData, IEquatable<QSEntityQuery>
    {
        public UnityObjectRef<BlobAsset<BlobEntityQueryDesc>> queryDesc;
        
        /// <summary>
        /// Hash of the query data to deduplicate and look up query results
        /// </summary>
        public Hash128 hash;

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
            return hash == other.hash;
        }

        public override bool Equals(object obj)
        {
            return obj is QSEntityQuery other && Equals(other);
        }

        [BurstCompile]
        public override int GetHashCode()
        {
            return hash.GetHashCode();
        }
        #endregion
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
    public struct QSTempState : IComponentData
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

        public struct ScoreComparerLess : IComparer<QSItem>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(QSItem x, QSItem y) => x.score.CompareTo(y.score);
        }
        
        public struct ScoreComparerGreater : IComparer<QSItem>
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
                in QueryExecutionContext qctx,
                in ExpressionEvalContext ctx,
                NativeList<float2> items)
            {
                float2 center = this.center.Evaluate<float2>(in ctx);
                float2 size = this.size.Evaluate<float2>(in ctx);
                float orientation = this.orientation.Evaluate<float>(in ctx);
                float spacing = this.spacing.Evaluate<float>(in ctx);

                math.sincos(orientation, out var s, out var c);
                var basis = spacing * new float2(c - s, s + c);

                var extent = 0.5f * size;

                int xi = 0;
                for (float x = 0; x <= extent.x; x += spacing, xi++)
                    ;
				
                int yi = 0;
                for (float y = 0; y <= extent.y; y += spacing, yi++)
                    ;

                int itemCount = (xi * 2 - 1) * (yi * 2 - 1);
				
                if(items.Capacity < itemCount)
                    items.SetCapacity(itemCount);
				
                items.Add(center);

                yi = 0;
                for(float y = 0; y <= extent.y; y += spacing, yi++)
                {
                    xi = 1;
                    for(float x = spacing; x <= extent.x; x += spacing, xi++)
                    {
                        items.Add(center + new float2(basis.x * xi, basis.y * yi));
                        items.Add(center + new float2(basis.x * xi, -basis.y * yi));
                        items.Add(center + new float2(-basis.x * xi, basis.y * yi));
                        items.Add(center + new float2(-basis.x * xi, -basis.y * yi));
                    }
                }
            }
        }

        public struct Entities
        {
            public Hash128 queryHash;

            public void Generate(in QueryExecutionContext qctx, in ExpressionEvalContext ctx, NativeList<Entity> items)
            {
                if (qctx.queryResultLookup.TryGetValue(queryHash, out var results))
                    items.CopyFrom(results);
                else
                    throw new InvalidOperationException($"results for query with hash {queryHash} not available");
            }
        }

        static void CheckType<TExpected, TActual>()
        {
            if(typeof(TExpected) != typeof(TActual))
                throw new Exception();
        }

        public void Generate<TItem>(in QueryExecutionContext qctx, in ExpressionEvalContext ctx, NativeList<TItem> items) where TItem : unmanaged
        {
            switch(generatorType)
            {
                case GeneratorType.Float2Rectangle:
                    CheckType<float2, TItem>();
                    unsafe
                    {
                        data.float2Rectangle.Generate(in qctx, in ctx, *(NativeList<float2>*)&items);
                    }
                    break;

                case GeneratorType.Entities:
                    CheckType<Entity, TItem>();
                    unsafe
                    {
                        data.entities.Generate(in qctx, in ctx, *(NativeList<Entity>*)&items);
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

        public void Pass<TItem>(
            in QueryExecutionContext qctx,
            in ExpressionEvalContext ctx,
            ref QSTempState tempState,
            NativeArray<TItem> items,
            NativeBitArray passBits) where TItem : unmanaged
        {
            switch(type)
            {
                case FilterType.Expression:
                {
                    // TODO: vectorized expressions
                    int i = 0;
                    foreach(var item in items)
                    {
                        tempState.GetCurrentItem<TItem>() = item;
                        passBits.Set(i++, expr.Evaluate<bool>(in ctx));
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
        public Normalizer normalizer;
        public bool negate;

        public enum ScorerType
        {
            Expression,
        }

        public enum Normalizer
        {
            None,
            Reinhard,
            Sigmoid,
            Saturate,
        }

        public void Score<TItem>(in QueryExecutionContext qctx, in ExpressionEvalContext ctx, ref QSTempState tempState, NativeArray<TItem> items, NativeArray<QSItem> scores) where TItem : unmanaged
        {
            switch(type)
            {
                case ScorerType.Expression:
                    // TODO: vectorized expressions
                    for(int i = 0; i < items.Length; ++i)
                    {
                        tempState.GetCurrentItem<TItem>() = items[i];
                        float raw = expr.Evaluate<float>(in ctx);
                        float score = raw;

                        if (negate)
                            score = -score;

                        switch (normalizer)
                        {
                            case Normalizer.None:
                                break;

                            case Normalizer.Reinhard:
                            {
                                float s = math.sign(score);
                                float a = math.abs(score);
                                score = 2 * (1 + s * (a / (1 + a)));
                            } break;

                            case Normalizer.Sigmoid:
                            {
                                score = 1 / (1 + math.exp(-score));
                            } break;

                            case Normalizer.Saturate:
                            {
                                score = math.saturate(score);
                            } break;
                        }

                        //UnityEngine.Debug.Log($"{raw} {normalizer} -> {score}");
                        scores.UnsafeElementAt(i).score += score;
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

    public static class QSResultExt
    {
        /// <summary>
        /// Allocate space for <paramref name="resultCount"/> results and return an array view for the results with uninitialized memory
        /// </summary>
        /// <param name="results"></param>
        /// <param name="resultCount"></param>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        public static NativeArray<TItem> AllocateResultArray<TItem>(ref this DynamicBuffer<QSResultItemStorage> results, int resultCount) where TItem : unmanaged
        {
            int itemSize = UnsafeUtility.SizeOf<TItem>();
            int elemSize = UnsafeUtility.SizeOf<QSResultItemStorage>();
            int storageElemCount = 1 + (resultCount * itemSize + elemSize - 1) / elemSize;
            results.ResizeUninitialized(storageElemCount);
            results.ElementAt(0).storage = resultCount;
            return GetResultSubArray<TItem>(results, resultCount);
        }
        
        /// <summary>
        /// Get an array view of results of the given type
        /// </summary>
        /// <param name="results"></param>
        /// <typeparam name="TItem"></typeparam>
        /// <returns></returns>
        public static NativeArray<TItem> AsResultArray<TItem>(ref this DynamicBuffer<QSResultItemStorage> results) where TItem : unmanaged
        {
            if (results.Length == 0)
            {
                return results.AsNativeArray().Reinterpret<TItem>(UnsafeUtility.SizeOf<QSResultItemStorage>());
            }
            
            int resultCount = (int)results.ElementAt(0).storage;
            CheckResultRange<TItem>(results, resultCount);
            return GetResultSubArray<TItem>(results, resultCount);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckResultRange<TItem>(DynamicBuffer<QSResultItemStorage> results, int resultCount) where TItem : unmanaged
        {
            int itemSize = UnsafeUtility.SizeOf<TItem>();
            int elemSize = UnsafeUtility.SizeOf<QSResultItemStorage>();
            int storageElemCount = 1 + (resultCount * itemSize + elemSize - 1) / elemSize;
            if (results.Length < storageElemCount)
                throw new InvalidOperationException($"corrupted QSResultItemStorage buffer; result count {resultCount} x {itemSize}b does not fit in {results.Length} x {elemSize}b storage elements");
        }

        private static NativeArray<TItem> GetResultSubArray<TItem>(DynamicBuffer<QSResultItemStorage> results, int resultCount)
            where TItem : unmanaged
        {
            var raw = results.AsNativeArray();
            
            NativeArray<TItem> typedResults;
            unsafe
            {
                QSResultItemStorage* ptr = 1 + (QSResultItemStorage*)raw.GetUnsafePtr();
                typedResults = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TItem>(ptr, resultCount, Allocator.None);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref typedResults, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(raw));
#endif
            return typedResults;
        }
    }
}
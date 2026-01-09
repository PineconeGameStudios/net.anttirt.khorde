using System;
using Mpr.Blobs;
using Mpr.Expr;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Query;

/// <summary>
/// Singleton for preparing assets used by the query system. All assets must be registered before they can be used.
/// </summary>
public struct QuerySystemAssets : IComponentData, IDisposable
{
    public NativeHashMap<UnityObjectRef<EntityQueryAsset>, EntityQueryMetaData> entityQueries;
    public NativeHashMap<UnityObjectRef<QueryGraphAsset>, QueryMetaData> queryGraphs;

    public struct EntityQueryMetaData
    {
        public EntityQuery query;
        public Hash128 hash;
    }
	
    public struct QueryMetaData : IDisposable
    {
        public NativeList<ExprSystemTypeHandleHolder> typeHandles;
        public NativeList<ExprSystemComponentLookupHolder> lookups;
        public EntityQuery jobQuery;

        public void Dispose()
        {
            typeHandles.Dispose();
            lookups.Dispose();
        }
    }

    public QuerySystemAssets(Allocator allocator)
    {
        entityQueries = new(0, allocator);
        queryGraphs = new(0, allocator);
    }

    /// <summary>
    /// Register a query graph asset. Also registers all entity query assets used by the graph.
    /// </summary>
    /// <param name="queryGraph"></param>
    public void Register(UnityObjectRef<QueryGraphAsset> queryGraph)
    {
        queryGraphs.TryAdd(queryGraph, default);
        foreach (var entityQueryAsset in QueryGraphAsset.GetQueries(queryGraph))
            entityQueries.TryAdd(entityQueryAsset, default);
    }

    public void Update(ref SystemState state, NativeList<QueryAssetRegistration> regs)
    {
        foreach (var queryAssetRegistration in regs)
        {
            foreach (var asset in queryAssetRegistration.Assets)
            {
                if (!queryGraphs.ContainsKey(asset))
                {
                    Register(asset);
                }
            }
        }
		
        foreach (var query in entityQueries)
        {
            if (query.Value.query == default)
            {
                query.Value.query = query.Key.GetValue().CreateQuery(state.EntityManager);
                query.Value.hash = query.Key.GetDataHash();
            }
        }

        NativeList<UnityObjectRef<QueryGraphAsset>> failures = default;

        foreach (var pair in queryGraphs)
        {
            ref var holder = ref pair.Value;
			
            if (holder.typeHandles.IsCreated)
            {
                foreach (ref var typeHandle in holder.typeHandles.AsArray().AsSpan())
                    typeHandle.typeHandle.Update(ref state);
				
                foreach (ref var lookup in holder.lookups.AsArray().AsSpan())
                    lookup.componentLookup.Update(ref state);
            }
            else
            {
                holder.typeHandles = new(Allocator.Persistent);
                holder.lookups = new(Allocator.Persistent);
                ref var data = ref pair.Key.GetValue<QSData, QueryGraphAsset>(QSData.SchemaVersion);

                var builder = new EntityQueryBuilder(Allocator.Temp);

                var instanceComponents = new NativeList<ComponentType>(Allocator.Temp)
                {
                    ComponentType.ReadWrite<QSResultItemStorage>(),
                    ComponentType.ReadWrite<PendingQuery>(),
                };

                if (!ExpressionSystemUtility.TryAddQueriesAndComponents(ref state, ref data.exprData,
                        ref holder.typeHandles, ref holder.lookups, instanceComponents))
                {
                    if(!failures.IsCreated)
                        failures = new(1, Allocator.Temp);
					
                    failures.Add(pair.Key);
					
                    continue;
                }

                builder.WithAll(ref instanceComponents);

                holder.jobQuery = builder.Build(state.EntityManager);
            }
        }

        if (failures.IsCreated)
        {
            foreach (var failure in failures)
                queryGraphs.Remove(failure);
        }
    }

    public void Dispose()
    {
        entityQueries.Dispose();
        foreach(var pair in queryGraphs)
            pair.Value.Dispose();
        queryGraphs.Dispose();
    }
}

/// <summary>
/// All assets referenced by <see cref="QueryAssetRegistration"/> are
/// automatically registered and usable with <see cref="QuerySystem"/>.
/// Alternatively, assets must be registered using
/// <see cref="SystemAPI.GetSingleton{QuerySystemAssets}"/> before use.
/// </summary>
public struct QueryAssetRegistration : ISharedComponentData
{
    UnityObjectRef<QueryGraphAsset> asset0;
    UnityObjectRef<QueryGraphAsset> asset1;
    UnityObjectRef<QueryGraphAsset> asset2;
    UnityObjectRef<QueryGraphAsset> asset3;
    UnityObjectRef<QueryGraphAsset> asset4;
    UnityObjectRef<QueryGraphAsset> asset5;
    UnityObjectRef<QueryGraphAsset> asset6;
    UnityObjectRef<QueryGraphAsset> asset7;
	
    public const int Capacity = 8;

    unsafe UnityObjectRef<QueryGraphAsset>* GetData()
    {
        fixed (UnityObjectRef<QueryGraphAsset>* ptr = &asset0)
            return ptr;
    }

    public unsafe int Length
    {
        get
        {
            var data = GetData();
			
            for(int i = 0; i < Capacity; ++i)
                if (data[i] == default)
                    return i;
			
            return Capacity;
        }
    }
	
    public unsafe void Add(UnityObjectRef<QueryGraphAsset> asset)
    {
        var data = GetData();

        int length = Length;
        if(length < Capacity)
            data[length] = asset;
        else
            throw new InvalidOperationException("max supported queries reached");
    }

    public unsafe NativeArray<UnityObjectRef<QueryGraphAsset>> Assets
    {
        get
        {
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UnityObjectRef<QueryGraphAsset>>(
                GetData(), Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return result;
        }
    }
}

using Mpr.Expr;
using System;
using Mpr.Blobs;
using Mpr.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		// 1. query per EntityQueryAsset, keyed by Hash128
		// 2. query per QSData, keyed by ???
		//	-> new asset type for QSData that can also be keyed
		// 3. 
		
		/// <summary>
		/// Results for entity queries used in query generators
		/// </summary>
		private NativeHashMap<Hash128, NativeList<Entity>> entityQueryResultLookup;
		
		/// <summary>
		/// Query requests queue
		/// </summary>
		private NativeQueue<QueryQueueEntry> incoming;

		private NativeHashMap<UnityObjectRef<EntityQueryAsset>, EntityQuery> entityQueries;

		private NativeHashMap<UnityObjectRef<QueryGraphAsset>, Entity> queryHolders;

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
			entityQueryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(1, Allocator.Persistent);
			state.EntityManager.AddComponentData(state.SystemHandle, new QueryQueue
			{
				queue = this.incoming = new NativeQueue<QueryQueueEntry>(Allocator.Persistent),
			});
			
			state.AddDependency<QueryQueue>(isReadOnly: false);
			
			entityQueries = new(0, Allocator.Persistent);
			
			queryHolders =  new(0, Allocator.Persistent);
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			entityQueryResultLookup.Clear();

			foreach(ref var component in components.AsArray().AsSpan())
			{
				if(component.runtimeEntityQuery == default && component.queryDesc.IsValid())
				{
					component.runtimeEntityQuery = component.queryDesc
						.GetValue(BlobEntityQueryDesc.SchemaVersion)
						.CreateQuery(state.EntityManager);
				}

				if(component.runtimeEntityQuery != default)
				{
					component.results = component.runtimeEntityQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var dep);
					state.Dependency = dep;
					entityQueryResultLookup[component.hash] = component.results;
				}
			}

			// TODO: jobify this so we don't have to wait for queue writers

			state.EntityManager.CompleteDependencyBeforeRW<QueryQueue>();

			var entries = incoming.ToArray(state.WorldUpdateAllocator);
			entries.Sort(default(QueryQueueEntry.Comparer));

			var entityQueryAssets = new NativeHashSet<UnityObjectRef<EntityQueryAsset>>(0, state.WorldUpdateAllocator);
			var slices = new NativeHashMap<UnityObjectRef<QueryGraphAsset>, NativeArray<QueryQueueEntry>>(0, state.WorldUpdateAllocator);

			int start = 0;
			for (int i = 0; i < entries.Length; i++)
			{
				int end = i + 1;
				if (i == entries.Length - 1 || entries[end].query != entries[i].query)
				{
					// run queries for [start..end)
					var slice = entries.GetSubArray(start, end - start);
					slices[slice[0].query] = slice;

					foreach(var desc in QueryGraphAsset.GetQueries(slice[0].query))
						entityQueryAssets.Add(desc);

					// update start
					start = end;
				}
			}

			var entityJobs = new NativeArray<JobHandle>(entityQueryAssets.Count, state.WorldUpdateAllocator);

			int entityJobIndex = 0;
			foreach (var entityQueryAsset in entityQueryAssets)
			{
				if (!entityQueries.TryGetValue(entityQueryAsset, out var entityQuery))
				{
					entityQuery = entityQueryAsset.GetValue().CreateQuery(state.EntityManager);
					entityQueries.Add(entityQueryAsset, entityQuery);
				}
				
				var entities = entityQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var dep);
				entityJobs[entityJobIndex++] = dep;
				entityQueryResultLookup[entityQueryAsset.GetDataHash()] = entities;
			}

			JobHandle entityJobHandle = JobHandle.CombineDependencies(entityJobs);
			
			var queryJobHandles = new NativeArray<JobHandle>(slices.Count, state.WorldUpdateAllocator);
			int queryJobIndex = 0;
			
			foreach (var pair in slices)
			{
				if (GetQueryHolder(ref state, pair.Key, out var queryHolder))
				{
					var queries = pair.Value;

					

					var job = new RunQueryJob
					{
						entries = queries,
					}.Schedule(queries.Length, 1, entityJobHandle);
					
					queryJobHandles[queryJobIndex++] = job;
				}
			}

			state.Dependency = JobHandle.CombineDependencies(queryJobHandles);
		}

		[BurstCompile]
		partial struct RunQueryJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeArray<QueryQueueEntry> entries;
			
			public void Execute(int index)
			{
			}
		}

		void ISystem.OnDestroy(ref SystemState state)
		{
			entityQueryResultLookup.Dispose();
			entityQueries.Dispose();
			incoming.Dispose();
			queryHolders.Dispose();
		}

		private bool GetQueryHolder(ref SystemState state, UnityObjectRef<QueryGraphAsset> asset, out Entity queryHolder)
		{
			if (queryHolders.TryGetValue(asset, out queryHolder))
			{
				return true;
			}
			
			Span<ComponentType> types = stackalloc ComponentType[2];
			types[0] = ComponentType.ReadWrite<ExprSystemTypeHandleHolder>();
			types[1] = ComponentType.ReadWrite<ExprSystemComponentLookupHolder>();

			queryHolder = state.EntityManager.CreateEntity(types);

			var typeHandles = state.EntityManager.GetBuffer<ExprSystemTypeHandleHolder>(queryHolder);
			var lookups = state.EntityManager.GetBuffer<ExprSystemComponentLookupHolder>(queryHolder);

			ref var btData = ref asset.GetValue<QSData, QueryGraphAsset>(QSData.SchemaVersion);

			if (!ExpressionSystemUtility.TryAddQueriesAndComponents(ref state, ref btData.exprData, typeHandles, lookups, default))
			{
				return false;
			}

			return true;
		}
	}
}

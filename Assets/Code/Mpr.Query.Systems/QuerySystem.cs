using System;
using Mpr.Blobs;
using Mpr.Entities;
using Mpr.Expr;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		/// <summary>
		/// Results for Entity generators
		/// </summary>
		private NativeHashMap<Hash128, NativeList<Entity>> entityQueryResultLookup;

		private QuerySystemAssets assets;

		public void OnCreate(ref SystemState state)
		{
			entityQueryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(1, Allocator.Persistent);

			assets = new QuerySystemAssets(Allocator.Persistent);

			// add as a component so this can be accessed as a singleton from other systems
			state.EntityManager.AddComponentData(state.SystemHandle, assets);
		}

		public void OnDestroy(ref SystemState state)
		{
			assets.Dispose();
			entityQueryResultLookup.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QueryAssetRegistration>(out var regs,
				state.WorldUpdateAllocator);

			assets.Update(ref state, regs);

			var entityQueryJobHandles =
				new NativeHashMap<UnityObjectRef<EntityQueryAsset>, JobHandle>(0, state.WorldUpdateAllocator);

			foreach (var pair in assets.queryGraphs)
			{
				var asset = pair.Key;
				ref var metaData = ref pair.Value;
				foreach (var entityQueryAsset in QueryGraphAsset.GetQueries(asset))
				{
					if (!entityQueryJobHandles.ContainsKey(entityQueryAsset) &&
					    assets.entityQueries.TryGetValue(entityQueryAsset, out var entityQueryMetaData))
					{
						var results = entityQueryMetaData.query.ToEntityListAsync(state.WorldUpdateAllocator,
							state.Dependency, out var entityQueryJobHandle);
						entityQueryJobHandles.Add(entityQueryAsset, entityQueryJobHandle);
						entityQueryResultLookup[entityQueryMetaData.hash] = results;
					}
				}
			}

			var entityQueriesJob =
				JobHandle.CombineDependencies(entityQueryJobHandles.GetValueArray(state.WorldUpdateAllocator));

			var jobHandles = new NativeList<JobHandle>(state.WorldUpdateAllocator);

			foreach (var pair in assets.queryGraphs)
			{
				var asset = pair.Key;
				ref var metaData = ref pair.Value;

				if (asset.GetObjectId() == default)
					throw new InvalidOperationException("query graph asset reference is null");

				var data = asset.GetHandle<QSData, QueryGraphAsset>(QSData.SchemaVersion);
				if (!data.IsCreated)
					throw new InvalidOperationException("failed to get data handle from query graph asset");
				
				var job = new ExecuteQueryJob
				{
					query = asset,
					data = data,
					pendingQuery = SystemAPI.GetComponentTypeHandle<PendingQuery>(),
					resultItemStorage = SystemAPI.GetBufferTypeHandle<QSResultItemStorage>(),
					queryResultLookup = entityQueryResultLookup,
				};

				// TODO: optimize dependencies to enable different queries to run in parallel
				// Currently the QuerySystem gets ComponentTypeHandles and ComponentLookups
				// via SystemState APIs which add a dependency to state.Dependency, meaning
				// all query jobs get the union of all of their dependencies, precluding them
				// from running in parallel. The code should instead construct the type handles
				// and component lookups without the system's involvement, and use
				// metaData.jobQuery.GetDependency() to get the proper dependency.

				foreach (ref var holder in metaData.typeHandles.AsArray().AsSpan())
					job.typeHandles.AddType(holder);

				foreach (ref var holder in metaData.lookups.AsArray().AsSpan())
					job.componentLookups.AddLookup(holder);

				jobHandles.Add(job.ScheduleParallelByRef(metaData.jobQuery, entityQueriesJob));
			}

			state.Dependency = JobHandle.CombineDependencies(jobHandles.AsArray());
		}

		[BurstCompile]
		struct ExecuteQueryJob : IJobChunk
		{
			public UnityObjectRef<QueryGraphAsset> query;
			[ReadOnly] public BlobAssetHandle<QSData> data;
			public ExprJobComponentTypeHandles typeHandles;
			public ExprJobComponentLookups componentLookups;
			public ComponentTypeHandle<PendingQuery> pendingQuery;
			public BufferTypeHandle<QSResultItemStorage> resultItemStorage;

			// need to disable safety because the results of the entity
			// query job go into a nested NativeList and nested containers
			// are not supported by the safety system
			[NativeDisableContainerSafetyRestriction]
			public NativeHashMap<Hash128, NativeList<Entity>> queryResultLookup;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				typeHandles.Initialize(chunk);
				var pendingEnabled = chunk.GetEnabledMask(ref pendingQuery);
				var pendingQueries = chunk.GetNativeArray(ref pendingQuery);
				var resultBuffers = chunk.GetBufferAccessor(ref resultItemStorage);

				switch (data.ValueRO.itemType)
				{
					case ExpressionValueType.Unknown: break;
					case ExpressionValueType.Entity:
						ExecuteImpl<Entity>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Bool:
						ExecuteImpl<bool>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Bool2:
						ExecuteImpl<bool2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Bool3:
						ExecuteImpl<bool3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Bool4:
						ExecuteImpl<bool4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Int:
						ExecuteImpl<int>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Int2:
						ExecuteImpl<int2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Int3:
						ExecuteImpl<int3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Int4:
						ExecuteImpl<int4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Float:
						ExecuteImpl<float>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Float2:
						ExecuteImpl<float2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Float3:
						ExecuteImpl<float3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Float4:
						ExecuteImpl<float4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					case ExpressionValueType.Quaternion:
						ExecuteImpl<quaternion>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							resultBuffers); break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				;
			}

			private void ExecuteImpl<TItem>(in ArchetypeChunk chunk, bool useEnabledMask, in v128 chunkEnabledMask,
				EnabledMask pendingEnabled,
				NativeArray<PendingQuery> pendingQueries, BufferAccessor<QSResultItemStorage> resultBuffers)
				where TItem : unmanaged
			{
				var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
				while (enumerator.NextEntityIndex(out var entityIndex))
				{
					if (pendingEnabled.GetBit(entityIndex) && pendingQueries[entityIndex].query == query)
					{
						chunk.SetComponentEnabled(ref pendingQuery, entityIndex, false);

						var qctx = new QueryExecutionContext(
							ref data.ValueRO,
							typeHandles.GetComponents(entityIndex),
							componentLookups.Lookups,
							queryResultLookup);

						var results = resultBuffers[entityIndex];
						qctx.Execute<TItem>(results);
					}
				}
			}
		}
	}
}
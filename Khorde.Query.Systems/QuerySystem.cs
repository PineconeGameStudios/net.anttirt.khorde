using Khorde.Blobs;
using Khorde.Entities;
using Khorde.Expr;
using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Khorde.Query
{
	public partial struct QuerySystem : ISystem
	{
		/// <summary>
		/// Results for Entity generators
		/// </summary>
		private NativeHashMap<Hash128, NativeList<Entity>> entityQueryResultLookup;
		private NativeHashSet<Entity> warnedEntities;
		private NativeList<Entity> warnPendingEntities;
		private QuerySystemAssets assets;
		private EntityQuery debugEnableQuery;
		private EntityQuery pendingEntitiesQuery;

		public void OnCreate(ref SystemState state)
		{
			entityQueryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(1, Allocator.Persistent);
			assets = new QuerySystemAssets(Allocator.Persistent);
			warnedEntities = new NativeHashSet<Entity>(0, Allocator.Persistent);
			warnPendingEntities = default;
			debugEnableQuery = SystemAPI.QueryBuilder().WithAll<QueryDebugEnable>().WithOptions(EntityQueryOptions.IncludeSystems).Build();
			pendingEntitiesQuery = SystemAPI.QueryBuilder().WithAll<PendingQuery>().Build();

			if(UnityEngine.Application.isEditor)
			{
				state.EntityManager.AddComponent<QueryDebugEnable>(state.SystemHandle);
			}

			// add as a component so this can be accessed as a singleton from other systems
			state.EntityManager.AddComponentData(state.SystemHandle, assets);

			state.IgnoreCreateQueryInOnUpdateWarning();
		}

		public void OnDestroy(ref SystemState state)
		{
			assets.Dispose();
			entityQueryResultLookup.Dispose();
			warnedEntities.Dispose();
			warnPendingEntities.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QueryAssetRegistration>(out var regs,
				state.WorldUpdateAllocator);

			assets.Update(ref state, regs);

			if(warnPendingEntities.IsCreated)
			{
				state.EntityManager.CompleteAllTrackedJobs();
				DebugLogging.LogEntityMissingWarningsPtr.Data.Invoke(ref this, ref state);
				warnPendingEntities.Dispose();

				assets.Update(ref state, regs);
			}

			var entityQueryJobHandles =
				new NativeHashMap<BlobAssetReference<BlobEntityQueryDesc>, JobHandle>(0, state.WorldUpdateAllocator);

			entityQueryJobHandles[default] = state.Dependency;

			foreach(var pair in assets.entityQueries)
			{
				var asset = pair.Key;
				ref var metaData = ref pair.Value;
				if(!entityQueryJobHandles.ContainsKey(pair.Key) &&
					assets.entityQueries.TryGetValue(pair.Key, out var entityQueryMetaData))
				{
					var results = entityQueryMetaData.query.ToEntityListAsync(state.WorldUpdateAllocator,
						state.Dependency, out var entityQueryJobHandle);
					entityQueryJobHandles.Add(pair.Key, entityQueryJobHandle);
					entityQueryResultLookup[entityQueryMetaData.hash] = results;
				}
			}

			var entityQueriesJob =
				JobHandle.CombineDependencies(entityQueryJobHandles.GetValueArray(state.WorldUpdateAllocator));

			state.Dependency = entityQueriesJob;

			//var jobHandles = new NativeList<JobHandle>(state.WorldUpdateAllocator)
			//{
			//	entityQueriesJob,
			//};

			foreach(var pair in assets.queryGraphs)
			{
				var asset = pair.Key;
				ref var metaData = ref pair.Value;

				if(!asset.IsCreated)
					throw new InvalidOperationException("query graph asset reference is null");

				var job = new ExecuteQueryJob
				{
					data = asset,
					pendingQuery = SystemAPI.GetComponentTypeHandle<PendingQuery>(),
					resultItemStorage = SystemAPI.GetBufferTypeHandle<QSResultItemStorage>(),
					entities = SystemAPI.GetEntityTypeHandle(),
					queryResultLookup = entityQueryResultLookup,
					blackboards = SystemAPI.GetBufferTypeHandle<ExpressionBlackboardStorage>(),
					blackboardLayoutsTypeHandle = SystemAPI.GetSharedComponentTypeHandle<ExpressionBlackboardLayouts>(),
					dataHash = asset.GetHash(),
				};

				// TODO: optimize dependencies to enable different queries to run in parallel
				// Currently the QuerySystem gets ComponentTypeHandles and ComponentLookups
				// via SystemState APIs which add a dependency to state.Dependency, meaning
				// all query jobs get the union of all of their dependencies, precluding them
				// from running in parallel. The code should instead construct the type handles
				// and component lookups without the system's involvement, and use
				// metaData.jobQuery.GetDependency() to get the proper dependency.

				foreach(ref var holder in metaData.typeHandles.AsArray().AsSpan())
					job.typeHandles.AddType(holder);

				foreach(ref var holder in metaData.lookups.AsArray().AsSpan())
					job.componentLookups.AddLookup(holder);

				var queryJobHandle = job.ScheduleParallelByRef(metaData.jobQuery, state.Dependency);
				//jobHandles.Add(queryJobHandle);
				state.Dependency = queryJobHandle;
			}

			//state.Dependency = JobHandle.CombineDependencies(jobHandles.AsArray());

			if(!debugEnableQuery.IsEmpty)
			{
				// Once all jobs from this system are complete, there should be no
				// more PendingQuery[Enabled=true]. Any such leftovers were missed
				// by the query execution jobs due to entity query filtering and
				// are probably data bugs.
				warnPendingEntities = pendingEntitiesQuery
					.ToEntityListAsync(Allocator.Persistent, state.Dependency, out var resultDep);

				state.Dependency = resultDep;
			}
		}

		[BurstCompile]
		struct ExecuteQueryJob : IJobChunk
		{
			[ReadOnly] public BlobAssetReference<QSData> data;
			public ExprJobComponentTypeHandles typeHandles;
			public ExprJobComponentLookups componentLookups;
			public ComponentTypeHandle<PendingQuery> pendingQuery;
			public BufferTypeHandle<ExpressionBlackboardStorage> blackboards;
			public BufferTypeHandle<QSResultItemStorage> resultItemStorage;
			public EntityTypeHandle entities;
			public SharedComponentTypeHandle<ExpressionBlackboardLayouts> blackboardLayoutsTypeHandle;
			public Hash128 dataHash;

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
				var blackboardBuffers = chunk.GetBufferAccessor(ref blackboards);
				var resultBuffers = chunk.GetBufferAccessor(ref resultItemStorage);
				var layouts = chunk.GetSharedComponent(blackboardLayoutsTypeHandle);
				var entitiesArray = chunk.GetNativeArray(entities);
				ref var layout = ref layouts.FindLayout(dataHash);

				switch(data.Value.itemType)
				{
					case ExpressionValueType.Unknown: break;
					case ExpressionValueType.Entity:
						ExecuteImpl<Entity>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Bool:
						ExecuteImpl<bool>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Bool2:
						ExecuteImpl<bool2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Bool3:
						ExecuteImpl<bool3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Bool4:
						ExecuteImpl<bool4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Int:
						ExecuteImpl<int>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Int2:
						ExecuteImpl<int2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Int3:
						ExecuteImpl<int3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Int4:
						ExecuteImpl<int4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Float:
						ExecuteImpl<float>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Float2:
						ExecuteImpl<float2>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Float3:
						ExecuteImpl<float3>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Float4:
						ExecuteImpl<float4>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					case ExpressionValueType.Quaternion:
						ExecuteImpl<quaternion>(chunk, useEnabledMask, chunkEnabledMask, pendingEnabled, pendingQueries,
							blackboardBuffers, ref layout, resultBuffers, entitiesArray); break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				;
			}

			private void ExecuteImpl<TItem>(in ArchetypeChunk chunk, bool useEnabledMask, in v128 chunkEnabledMask,
				EnabledMask pendingEnabled,
				NativeArray<PendingQuery> pendingQueries,
				BufferAccessor<ExpressionBlackboardStorage> blackboardBuffers,
				ref ExpressionBlackboardLayout blackboardLayout,
				BufferAccessor<QSResultItemStorage> resultBuffers,
				NativeArray<Entity> entities)
				where TItem : unmanaged
			{
				var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
				var selectedEntity = SelectedEntity.Value;
				while(enumerator.NextEntityIndex(out var entityIndex))
				{
					if(pendingEnabled.GetBit(entityIndex))
					{
						ref var pendingQuery = ref pendingQueries.UnsafeElementAt(entityIndex);
						if(pendingQuery.query == data)
						{
							chunk.SetComponentEnabled(ref this.pendingQuery, entityIndex, false);
							pendingQuery.complete = true;

							bool isSelectedEntity = selectedEntity == entities[entityIndex];

							var qctx = new QueryExecutionContext(
								ref data.Value,
								typeHandles.GetComponents(entityIndex),
								componentLookups.Lookups,
								queryResultLookup);

							var blackboard = blackboardBuffers[entityIndex];

							var results = resultBuffers[entityIndex];
							int resultCount = qctx.Execute<TItem>(blackboard, ref blackboardLayout, results, pendingQuery.results, Allocator.Temp, isSelectedEntity);
							pendingQuery.resultCount = resultCount;
						}
					}
				}
			}
		}

		#region Debug Logging
		static class DebugLogging
		{
			public delegate void LogEntityMissingWarningsDelegate(ref QuerySystem system, ref SystemState state);
			private static LogEntityMissingWarningsDelegate s_logEntityMissingWarningsGC;
			public static readonly SharedStatic<FunctionPointer<LogEntityMissingWarningsDelegate>> LogEntityMissingWarningsPtr
				= SharedStatic<FunctionPointer<LogEntityMissingWarningsDelegate>>.GetOrCreate<LogEntityMissingWarningsDelegate>();
			private static StringBuilder s_logEntityMissingWarningsSB;

			[AOT.MonoPInvokeCallback(typeof(LogEntityMissingWarningsDelegate))]
			private static void LogEntityMissingWarnings(ref QuerySystem system, ref SystemState state)
			{
				var sb = s_logEntityMissingWarningsSB;

				foreach(var entity in system.warnPendingEntities)
				{
					if(!system.warnedEntities.Add(entity))
						continue;

					if(!state.EntityManager.TryGetComponentData<PendingQuery>(entity, out var pendingQuery))
						continue;

					if(!system.assets.queryGraphs.TryGetValue(pendingQuery.query, out var metaData))
					{
						UnityEngine.Debug.LogError($"entity {entity} has PendingQuery{{query={pendingQuery.query.GetHash()}}} but the query was not registered and will not run");
						continue;
					}

					var descs = metaData.jobQuery.GetEntityQueryDescs();

					sb.Clear();

					FixedString128Bytes name = default;
					pendingQuery.query.Value.exprData.assetName.CopyTo(ref name);
					sb.Append($"entity {entity} wants to run QueryGraph{{{name}}} but is missing the required components [");
					string intr = "";

					foreach(var type in descs[0].All)
					{
						if(!state.EntityManager.HasComponent(entity, type))
						{
							sb.Append(intr);
							sb.Append(type.GetManagedType().FullName);
							intr = ", ";
						}
					}

					sb.Append("]");

					UnityEngine.Debug.LogError(sb.ToString());
				}
			}

			[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]
			static void StaticInit()
			{
				s_logEntityMissingWarningsGC = LogEntityMissingWarnings;
				s_logEntityMissingWarningsSB = new();
				LogEntityMissingWarningsPtr.Data = new(Marshal.GetFunctionPointerForDelegate(s_logEntityMissingWarningsGC));
			}
		}
		#endregion
	}
}
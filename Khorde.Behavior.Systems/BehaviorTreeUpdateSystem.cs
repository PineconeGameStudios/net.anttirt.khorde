using Khorde.Blobs;
using Khorde.Entities;
using Khorde.Expr;
using Khorde.Query;
using System;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Khorde.Behavior
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial struct BehaviorTreeUpdateSystem : ISystem
	{
		Entity traceHolder;

		void ISystem.OnCreate(ref SystemState state)
		{
			traceHolder = state.EntityManager.CreateSingletonBuffer<BTExecTrace>();
		}

		[BurstCompile]
		struct UpdateJob : IJobChunk
		{
			public ExprJobComponentTypeHandles typeHandles;
			public ExprJobComponentLookups componentLookups;
			public BlobAssetReference<BTData> btData;
			public ComponentTypeHandle<BTState> stateTypeHandle;
			public BufferTypeHandle<BTStackFrame> stackTypeHandle;
			public BufferTypeHandle<ExpressionBlackboardStorage> blackboardTypeHandle;
			public SharedComponentTypeHandle<ExpressionBlackboardLayouts> blackboardLayoutsTypeHandle;
			public SharedComponentTypeHandle<QueryAssetRegistration> queriesTypeHandle;
			public ComponentTypeHandle<PendingQuery> pendingQueryHandle;
			public Hash128 dataHash;
			public float now;

			public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				typeHandles.Initialize(chunk);

				var states = chunk.GetNativeArray(ref stateTypeHandle).AsSpan();
				var stacks = chunk.GetBufferAccessor(ref stackTypeHandle);
				var blackboards = chunk.GetBufferAccessor(ref blackboardTypeHandle);
				var lookups = componentLookups.Lookups;
				var layouts = chunk.GetSharedComponent(blackboardLayoutsTypeHandle);
				ref var layout = ref layouts.FindLayout(dataHash);

				NativeArray<UnityObjectRef<QueryGraphAsset>> queries = default;
				EnabledMask pendingQueryEnabledMask = default;
				NativeArray<PendingQuery> pendingQueries = default;

				if(btData.Value.hasQueries)
				{
					queries = chunk.GetSharedComponent(queriesTypeHandle).Assets;
					pendingQueryEnabledMask = chunk.GetEnabledMask(ref pendingQueryHandle);
					pendingQueries = chunk.GetNativeArray(ref pendingQueryHandle);
				}

				PendingQuery defaultValue = default;

				var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
				while(enumerator.NextEntityIndex(out var entityIndex))
				{
					EnabledRefRW<PendingQuery> pendingQueryEnabled = default;
					if(btData.Value.hasQueries)
						pendingQueryEnabled = pendingQueryEnabledMask.GetEnabledRefRW<PendingQuery>(entityIndex);

					BehaviorTreeExecution.Execute(
						ref btData.Value,
						ref states[entityIndex],
						stacks[entityIndex],
						blackboards[entityIndex].AsNativeArray(),
						ref layout,
						queries,
						pendingQueryEnabled,
						ref (pendingQueries.IsCreated ? ref pendingQueries.UnsafeElementAt(entityIndex) : ref defaultValue),
						typeHandles.GetComponents(entityIndex),
						lookups,
						now,
						default
						);
				}
			}
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			if(!CreateQueries(ref state))
			{
				return;
			}

			foreach(var (queryHolder, typeHandleHolder, lookupHolder, tree) in SystemAPI.Query<BTQueryHolder, DynamicBuffer<ExprSystemTypeHandleHolder>, DynamicBuffer<ExprSystemComponentLookupHolder>, BehaviorTree>())
			{
				var job = new UpdateJob
				{
					btData = tree.tree.GetHandle<BTData, BehaviorTreeAsset>(BTData.SchemaVersion).Reference,
					now = (float)SystemAPI.Time.ElapsedTime,
					stateTypeHandle = SystemAPI.GetComponentTypeHandle<BTState>(),
					stackTypeHandle = SystemAPI.GetBufferTypeHandle<BTStackFrame>(),
					blackboardTypeHandle = SystemAPI.GetBufferTypeHandle<ExpressionBlackboardStorage>(),
					blackboardLayoutsTypeHandle = SystemAPI.GetSharedComponentTypeHandle<ExpressionBlackboardLayouts>(),
					queriesTypeHandle = SystemAPI.GetSharedComponentTypeHandle<QueryAssetRegistration>(),
					pendingQueryHandle = SystemAPI.GetComponentTypeHandle<PendingQuery>(),
					dataHash = tree.tree.GetDataHash(),
				};

				foreach(ref var holder in typeHandleHolder.AsNativeArray().AsSpan())
				{
					holder.typeHandle.Update(ref state);
					job.typeHandles.AddType(holder);
				}

				foreach(ref var holder in lookupHolder.AsNativeArray().AsSpan())
				{
					holder.componentLookup.Update(ref state);
					job.componentLookups.AddLookup(holder);
				}

				// int entityCount = queryHolder.query.CalculateEntityCount();
				// if(entityCount == 0)
				// {
				// 	var descs = queryHolder.query.GetEntityQueryDescs();
				// 	Debug.Log($"no entities for query [{string.Join(", ", descs.Select(d => "[" + string.Join(", ", d.All.Select(c => c.GetManagedType().FullName)) + "]"))}]");
				// }

				state.Dependency = job.ScheduleParallel(queryHolder.query, state.Dependency);
			}
		}

		private bool CreateQueries(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<BehaviorTree>(out var values, Allocator.Temp);

			var holderQuery = SystemAPI.QueryBuilder()
				.WithAllRW<BTQueryHolder>()
				.WithAllRW<ExprSystemTypeHandleHolder>()
				.WithAllRW<ExprSystemComponentLookupHolder>()
				.WithAll<BehaviorTree>()
				.Build();

			bool clientWorld = (state.WorldUnmanaged.Flags & WorldFlags.GameClient) == WorldFlags.GameClient;

			foreach(var value in values)
			{
				if(value.tree.GetObjectId() == default)
					continue;

				holderQuery.AddSharedComponentFilter(value);

				if(holderQuery.IsEmpty)
				{
					// create a query-holder entity matching this behaviortree asset

					Span<ComponentType> types = stackalloc ComponentType[3];
					types[0] = ComponentType.ReadOnly<BTQueryHolder>();
					types[1] = ComponentType.ReadWrite<ExprSystemTypeHandleHolder>();
					types[2] = ComponentType.ReadWrite<ExprSystemComponentLookupHolder>();

					var queryHolder = state.EntityManager.CreateEntity(types);

					var builder = new EntityQueryBuilder(Allocator.Temp);

					var instanceComponents = new NativeList<ComponentType>(Allocator.Temp)
					{
						ComponentType.ReadOnly<BehaviorTree>(),
						ComponentType.ReadWrite<BTState>(),
						ComponentType.ReadWrite<BTStackFrame>(),
						ComponentType.ReadWrite<ExpressionBlackboardStorage>(),
						ComponentType.ReadWrite<ExpressionBlackboardLayouts>(),
					};

					if(clientWorld)
					{
						instanceComponents.Add(ComponentType.ReadOnly<PredictedGhost>());
						instanceComponents.Add(ComponentType.ReadOnly<Simulate>());
					}

					ref var btData = ref value.tree.GetValue<BTData, BehaviorTreeAsset>(BTData.SchemaVersion);

					if(btData.hasQueries)
					{
						instanceComponents.Add(ComponentType.ReadOnly<QueryAssetRegistration>());
					}

					var typeHandles = state.EntityManager.GetBuffer<ExprSystemTypeHandleHolder>(queryHolder);
					var lookups = state.EntityManager.GetBuffer<ExprSystemComponentLookupHolder>(queryHolder);

					if(!ExpressionSystemUtility.TryAddQueriesAndComponents(ref state, ref btData.exprData, ref typeHandles, ref lookups, instanceComponents))
					{
						Debug.LogError("Failed to create queries / components");
						state.Enabled = false;
						return false;
					}

					builder.WithAll(ref instanceComponents);

					if(btData.hasQueries)
					{
						builder.WithPresentRW<PendingQuery>();
					}

					var btQuery = state.GetEntityQuery(builder);
					//var btQuery = builder.Build(state.EntityManager);
					btQuery.AddSharedComponentFilter(value);

					builder.Reset();
					builder.WithAll<BehaviorTree>();
					var debugQuery = state.GetEntityQuery(builder);
					debugQuery.SetSharedComponentFilter(value);

					state.EntityManager.AddSharedComponent(queryHolder, value);
					state.EntityManager.SetComponentData(queryHolder, new BTQueryHolder
					{
						query = btQuery,
						debugQuery = debugQuery,
					});
				}

				holderQuery.ResetFilter();
			}

			return true;
		}
	}

#if UNITY_EDITOR
	partial class BehaviorTreeDebugSystem : SystemBase
	{
		NativeHashSet<Entity> warnedEntities;
		StringBuilder sb;

		protected override void OnCreate()
		{
			warnedEntities = new(0, Allocator.Persistent);
			sb = new();
		}

		protected override void OnDestroy()
		{
			warnedEntities.Dispose();
		}

		protected override void OnUpdate()
		{
			foreach(var (queryHolder, typeHandleHolder, lookupHolder, tree) in SystemAPI.Query<BTQueryHolder, DynamicBuffer<ExprSystemTypeHandleHolder>, DynamicBuffer<ExprSystemComponentLookupHolder>, BehaviorTree>())
			{
				EntityQueryDesc[] descs = null;
				if(queryHolder.query.CalculateEntityCount() != queryHolder.debugQuery.CalculateEntityCount())
				{
					foreach(var entity in queryHolder.query.ToEntityArray(Allocator.Temp))
					{
						if(!warnedEntities.Add(entity))
							continue;

						descs ??= queryHolder.query.GetEntityQueryDescs();

						sb.Clear();
						
						sb.Append($"entity {entity} has BehaviorTree{{{tree.tree.Value.name}}} but is missing the required components [");
						string intr = "";

						foreach(var type in descs[0].All)
						{
							sb.Append(intr);
							sb.Append(type.GetManagedType().FullName);
							intr = ", ";
						}

						sb.Append("]");
					}

				}
			}
		}
	}
#endif

	public struct BTQueryHolder : IComponentData
	{
		/// <summary>
		/// Query matching all entities with a <see cref="BehaviorTree"/>
		/// component with a particular value. Each distinct BehaviorTree
		/// (corresponding to a distinct BT Graph) gets its own query with a
		/// configured shared component filter. Used to update behavior trees
		/// with an IJobChunk.
		/// </summary>
		public EntityQuery query;

		/// <summary>
		/// Query matching all entities with a <see cref="BehaviorTree"/>
		/// component with a particular value. This query does not contain any
		/// other components required by the tree, and can be used to detect
		/// missing required components from entities with a behavior tree.
		/// </summary>
		public EntityQuery debugQuery;
	}
}
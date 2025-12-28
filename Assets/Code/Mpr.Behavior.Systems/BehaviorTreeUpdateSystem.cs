using Mpr.Expr;
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.NetCode.LowLevel.Unsafe;

namespace Mpr.Behavior
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
		partial struct UpdateJob : IJobChunk
		{
			public ExprJobComponentTypeHandles typeHandles;
			public ExprJobComponentLookups componentLookups;
			public BlobAssetReference<BTData> btData;
			public ComponentTypeHandle<BTState> stateTypeHandle;
			public BufferTypeHandle<BTStackFrame> stackTypeHandle;
			public float now;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				typeHandles.Initialize(chunk);

				var states = chunk.GetNativeArray(ref stateTypeHandle).AsSpan();
				var stacks = chunk.GetBufferAccessor(ref stackTypeHandle);

				var lookups = componentLookups.Lookups;

				var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
				while(enumerator.NextEntityIndex(out var entityIndex))
				{
					BehaviorTreeExecution.Execute(
						ref btData.Value,
						ref states[entityIndex],
						stacks[entityIndex],
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
					btData = tree.tree,
					now = (float)SystemAPI.Time.ElapsedTime,
					stateTypeHandle = SystemAPI.GetComponentTypeHandle<BTState>(),
					stackTypeHandle = SystemAPI.GetBufferTypeHandle<BTStackFrame>(),
				};

				foreach(ref var holder in typeHandleHolder.AsNativeArray().AsSpan())
				{
					holder.typeHandle.Update(ref state);
					job.typeHandles.AddType(holder);
				}

				foreach(var holder in lookupHolder.AsNativeArray().AsSpan())
				{
					holder.componentLookup.Update(ref state);
					job.componentLookups.AddLookup(holder);
				}

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
				if(!value.tree.IsCreated)
					continue;

				holderQuery.AddSharedComponentFilter(value);

				if(holderQuery.IsEmpty)
				{
					// create a query-holder entity matching this behaviortree asset

					Span<ComponentType> types = stackalloc ComponentType[3];
					types[0] = ComponentType.ReadOnly<BTQueryHolder>();
					types[1] = ComponentType.ReadWrite<ExprSystemTypeHandleHolder>();
					types[2] = ComponentType.ReadWrite<ExprSystemComponentLookupHolder>();

					var entity = state.EntityManager.CreateEntity(types);

					var builder = new EntityQueryBuilder(Allocator.Temp);
					var components = new NativeList<ComponentType>(Allocator.Temp)
					{
						ComponentType.ReadOnly<BehaviorTree>(),
						ComponentType.ReadWrite<BTState>(),
						ComponentType.ReadWrite<BTStackFrame>(),
					};

					if(clientWorld)
					{
						components.Add(ComponentType.ReadOnly<PredictedGhost>());
						components.Add(ComponentType.ReadOnly<Simulate>());
					}

					ref var btData = ref value.tree.Value;
					ref var componentTypes = ref btData.exprData.localComponents;

					var typeHandles = state.EntityManager.GetBuffer<ExprSystemTypeHandleHolder>(entity);
					for(int i = 0; i < componentTypes.Length; ++i)
					{
						var type = componentTypes[i].ResolveComponentType();
						if(type.TypeIndex == TypeIndex.Null)
						{
							UnityEngine.Debug.LogError($"type with stableTypeHash={componentTypes[i].stableTypeHash} required by BehaviorTree not found");
							state.Enabled = false;
							return false;
						}

						components.Add(type);
						state.AddDependency(type);

						typeHandles.Add(new ExprSystemTypeHandleHolder
						{
							typeHandle = state.EntityManager.GetDynamicComponentTypeHandle(type),
							typeIndex = type.TypeIndex,
							stableTypeHash = componentTypes[i].stableTypeHash,
							typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
						});
					}

					ref var lookupTypes = ref btData.exprData.lookupComponents;

					var lookups = state.EntityManager.GetBuffer<ExprSystemComponentLookupHolder>(entity);
					for(int i = 0; i < lookupTypes.Length; ++i)
					{
						var type = lookupTypes[i].ResolveComponentType();
						if(type.TypeIndex == TypeIndex.Null)
						{
							UnityEngine.Debug.LogError($"type with stableTypeHash={componentTypes[i].stableTypeHash} required by BehaviorTree not found");
							state.Enabled = false;
							return false;
						}
						
						state.AddDependency(type);

						lookups.Add(new ExprSystemComponentLookupHolder
						{
							componentLookup = state.GetUntypedComponentLookup(type.TypeIndex, true),
							stableTypeHash = lookupTypes[i].stableTypeHash,
							typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
						});
					}

					builder.WithAll(ref components);

					var btQuery = builder.Build(state.EntityManager);
					btQuery.AddSharedComponentFilter(value);

					state.EntityManager.AddSharedComponent(entity, value);
					state.EntityManager.SetComponentData(entity, new BTQueryHolder
					{
						query = btQuery,
					});
				}

				holderQuery.ResetFilter();
			}

			return true;
		}
	}

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
	}
}
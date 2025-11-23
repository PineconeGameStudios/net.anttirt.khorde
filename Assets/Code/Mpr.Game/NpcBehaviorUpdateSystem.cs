using System;
using System.Runtime.InteropServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Mpr.Game
{
	struct BTQueryHolder : IComponentData
	{
		public EntityQuery query;
	}

	struct BTTypeHandleHolder : IBufferElementData
	{
		public DynamicComponentTypeHandle typeHandle;
		public ulong stableTypeHash;
		public int typeSize;
	}

	public partial struct BehaviorTreeUpdateSystem : ISystem
	{
		Entity traceHolder;

		void ISystem.OnCreate(ref SystemState state)
		{
			traceHolder = state.EntityManager.CreateSingletonBuffer<AI.BT.BTExecTrace>();
		}

		[StructLayout(LayoutKind.Sequential)]
		struct JobTypeHandles
		{
			DynamicComponentTypeHandle type0;
			DynamicComponentTypeHandle type1;
			DynamicComponentTypeHandle type2;
			DynamicComponentTypeHandle type3;
			DynamicComponentTypeHandle type4;
			DynamicComponentTypeHandle type5;
			DynamicComponentTypeHandle type6;
			DynamicComponentTypeHandle type7;
			DynamicComponentTypeHandle type8;
			DynamicComponentTypeHandle type9;

			public Span<DynamicComponentTypeHandle> Values
			{
				get
				{
					unsafe
					{
						fixed(DynamicComponentTypeHandle* handlesPtr = &type0)
							return new Span<DynamicComponentTypeHandle>(handlesPtr, 10);
					}
				}
			}
		}

		partial struct UpdateJob : IJobChunk
		{
			JobTypeHandles typeHandles;
			FixedList64Bytes<int> typeSizes;
			FixedList128Bytes<ulong> typeHashes;

			public BlobAssetReference<AI.BT.BTData> btData;
			public ComponentTypeHandle<AI.BT.BehaviorTreeState> stateTypeHandle;
			public BufferTypeHandle<AI.BT.BTStackFrame> stackTypeHandle;
			public float now;

			public void AddType(BTTypeHandleHolder holder)
			{
				if(typeSizes.Length >= 10)
					throw new Exception("max supported component type count is 10");

				typeHandles.Values[typeSizes.Length] = holder.typeHandle;
				typeSizes.Add(holder.typeSize);
				typeHashes.Add(holder.stableTypeHash);
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				Span<IntPtr> basePointers = stackalloc IntPtr[typeSizes.Length];
				Span<AI.BT.UnsafeComponentReference> components = stackalloc AI.BT.UnsafeComponentReference[typeSizes.Length];

				for(int i = 0; i < typeSizes.Length; ++i)
				{
					ref var handle = ref typeHandles.Values[i];
					var data = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, typeSizes[i]);

					unsafe
					{
						basePointers[i] = (IntPtr)data.GetUnsafePtr();
					}

					components[i] = new AI.BT.UnsafeComponentReference
					{
						stableTypeHash = typeHashes[i],
						typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(typeHashes[i]),
						length = typeSizes[i],
					};
				}

				var states = chunk.GetNativeArray(ref stateTypeHandle);
				var stacks = chunk.GetBufferAccessor(ref stackTypeHandle);


				var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
				while(enumerator.NextEntityIndex(out var entityIndex))
				{
					for(int i = 0; i < components.Length; ++i)
						components[i].data = basePointers[i] + entityIndex * typeSizes[i];

					AI.BT.BehaviorTreeExecution.Execute(
						ref btData.Value,
						ref states.AsSpan()[entityIndex],
						stacks[entityIndex],
						components,
						now,
						default
						);
				}
			}
		}

		void ISystem.OnUpdate(ref SystemState state)
		{
			if(!CreateQueries(ref state))
			{
				return;
			}

			foreach(var (queryHolder, typeHandleHolder, tree) in SystemAPI.Query<BTQueryHolder, DynamicBuffer<BTTypeHandleHolder>, AI.BT.BehaviorTree>())
			{
				var job = new UpdateJob
				{
					btData = tree.tree,
					now = (float)SystemAPI.Time.ElapsedTime,
					stateTypeHandle = SystemAPI.GetComponentTypeHandle<AI.BT.BehaviorTreeState>(),
					stackTypeHandle = SystemAPI.GetBufferTypeHandle<AI.BT.BTStackFrame>(),
				};

				for(int i = 0; i < typeHandleHolder.Length; ++i)
				{
					ref var holder = ref typeHandleHolder.ElementAt(i);
					holder.typeHandle.Update(ref state);
					job.AddType(holder);
				}

				state.Dependency = job.Schedule(queryHolder.query, state.Dependency);
			}
		}

		private bool CreateQueries(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<AI.BT.BehaviorTree>(out var values, Allocator.Temp);

			var holderQuery = SystemAPI.QueryBuilder().WithAllRW<BTQueryHolder, BTTypeHandleHolder>().WithAll<AI.BT.BehaviorTree>().Build();

			foreach(var value in values)
			{
				if(!value.tree.IsCreated)
					continue;

				holderQuery.AddSharedComponentFilter(value);

				if(holderQuery.CalculateEntityCount() == 0)
				{
					// create a query-holder component

					Span<ComponentType> types = stackalloc ComponentType[2];
					types[0] = ComponentType.ReadOnly<BTQueryHolder>();
					types[1] = ComponentType.ReadWrite<BTTypeHandleHolder>();

					var entity = state.EntityManager.CreateEntity(types);

					var builder = new EntityQueryBuilder(Allocator.Temp);
					var components = new NativeList<ComponentType>(Allocator.Temp)
					{
						ComponentType.ReadOnly<AI.BT.BehaviorTree>(),
						ComponentType.ReadWrite<AI.BT.BehaviorTreeState>(),
						ComponentType.ReadWrite<AI.BT.BTStackFrame>(),
					};

					var typeHandles = state.EntityManager.GetBuffer<BTTypeHandleHolder>(entity);

					ref var btData = ref value.tree.Value;
					for(int i = 0; i < btData.componentTypes.Length; ++i)
					{
						ulong stableTypeHash = btData.componentTypes[i];
						var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(stableTypeHash);
						if(typeIndex == TypeIndex.Null)
						{
							UnityEngine.Debug.LogError($"type with stableTypeHash={stableTypeHash} required by BehaviorTree not found");
							state.Enabled = false;
							return false;
						}

						var type = ComponentType.FromTypeIndex(typeIndex);
						// TODO: set some types as read-only based on graph access patterns
						// type.AccessModeType = ComponentType.AccessMode.ReadOnly;

						components.Add(type);

						typeHandles.Add(new BTTypeHandleHolder
						{
							typeHandle = state.EntityManager.GetDynamicComponentTypeHandle(type),
							stableTypeHash = stableTypeHash,
							typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize,
						});
					}

					foreach(var component in components)
						state.AddDependency(component);

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
}
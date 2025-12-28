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

				var lookups = componentLookups.Values;

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

			var holderQuery = SystemAPI.QueryBuilder().WithAllRW<BTQueryHolder, ExprSystemTypeHandleHolder>().WithAll<BehaviorTree>().Build();

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
					ref var componentTypes = ref btData.exprData.componentTypes;

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

						typeHandles.Add(new ExprSystemTypeHandleHolder
						{
							typeHandle = state.EntityManager.GetDynamicComponentTypeHandle(type),
							typeIndex = type.TypeIndex,
							stableTypeHash = componentTypes[i].stableTypeHash,
							typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
						});
					}

					ref var lookupTypes = ref btData.exprData.componentLookupTypes;

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

						components.Add(type);

						lookups.Add(new ExprSystemComponentLookupHolder
						{
							//typeHandle = state.EntityManager.GetDynamicComponentTypeHandle(type),
							//componentLookup = new UntypedComponentLookup()
							componentLookup = state.GetUntypedComponentLookup(type.TypeIndex, true),
							stableTypeHash = lookupTypes[i].stableTypeHash,
							typeSize = TypeManager.GetTypeInfo(type.TypeIndex).TypeSize,
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

	public struct ExprSystemTypeHandleHolder : IBufferElementData
	{
		/// <summary>
		/// Type handle for passing component data references to BT evaluation during IJobChunk iteration
		/// </summary>
		public DynamicComponentTypeHandle typeHandle;

		/// <summary>
		/// Stable type hash of the component type
		/// </summary>
		public ulong stableTypeHash;

		public TypeIndex typeIndex;

		/// <summary>
		/// Size in bytes of the component type
		/// </summary>
		public int typeSize;
	}

	public struct ExprSystemComponentLookupHolder : IBufferElementData
	{
		/// <summary>
		/// Component lookup for accessing component data on other entities during BT evaluation
		/// </summary>
		public UntypedComponentLookup componentLookup;

		/// <summary>
		/// Stable type hash of the component type
		/// </summary>
		public ulong stableTypeHash;

		/// <summary>
		/// Size in bytes of the component type
		/// </summary>
		public int typeSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ExprJobComponentTypeHandles
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
		FixedList512Bytes<UnsafeComponentReference> components;
		FixedList128Bytes<IntPtr> basePointers;
		const int kMaxHandles = 10;

		public int Length => components.Length;

		public int GetTypeSize(int index) => components[index].typeSize;

		Span<DynamicComponentTypeHandle> Handles
		{
			get
			{
				unsafe
				{
					fixed(DynamicComponentTypeHandle* ptr = &type0)
						return new Span<DynamicComponentTypeHandle>(ptr, kMaxHandles);
				}
			}
		}

		public ReadOnlySpan<UnsafeComponentReference> GetComponents(int entityIndex)
		{
			for(int i = 0; i < components.Length; ++i)
				components.ElementAt(i).data = basePointers[i] + entityIndex * components[i].typeSize;

			return components.ToReadOnlySpan(components.Length);
		}

		public void Initialize(in ArchetypeChunk chunk)
		{
			basePointers.Length = Length;

			for(int i = 0; i < Length; ++i)
			{
				ref var handle = ref Handles[i];
				var data = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, GetTypeSize(i));

				unsafe
				{
					basePointers[i] = handle.IsReadOnly ? (IntPtr)data.GetUnsafeReadOnlyPtr() : (IntPtr)data.GetUnsafePtr();
				}
			}
		}

		public void AddType(ExprSystemTypeHandleHolder holder)
		{
			if(components.Length >= kMaxHandles)
				throw new Exception("max supported component type count is 10");

			Handles[components.Length] = holder.typeHandle;
			components.Add(new UnsafeComponentReference
			{
				data = default,
				typeSize = holder.typeSize,
				stableTypeHash = holder.stableTypeHash,
				typeIndex = holder.typeIndex,
			});
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ExprJobComponentLookups
	{
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup0;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup1;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup2;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup3;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup4;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup5;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup6;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup7;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup8;
		[NativeDisableContainerSafetyRestriction] UntypedComponentLookup lookup9;

		FixedList64Bytes<int> componentTypeSizes;
		FixedList128Bytes<ulong> componentTypeHashes;

		public Span<UntypedComponentLookup> Values
		{
			get
			{
				unsafe
				{
					fixed(UntypedComponentLookup* handlesPtr = &lookup0)
						return new Span<UntypedComponentLookup>(handlesPtr, 10);
				}
			}
		}

		public void AddLookup(ExprSystemComponentLookupHolder holder)
		{
			if(componentTypeSizes.Length >= 10)
				throw new Exception("max supported component type count is 10");

			Values[componentTypeSizes.Length] = holder.componentLookup;
			componentTypeSizes.Add(holder.typeSize);
			componentTypeHashes.Add(holder.stableTypeHash);
		}
	}
}
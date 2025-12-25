using Mpr.Expr;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		private NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup;
		
		// TODO: turn this into IJobChunk to pass dynamic expression component data as in BTUpdateSystem
		[BurstCompile]
		partial struct ExecuteQueriesJob : IJobEntity
		{
			[NativeDisableContainerSafetyRestriction]
			public NativeHashMap<IntPtr, NativeList<Entity>> queryResultLookup;
			
			public void Execute(Query query,
				DynamicBuffer<QSEntityQueryReference> entityQueries,
				DynamicBuffer<QSResultItemStorage> results
				)
			{
				if(!query.query.IsCreated)
					return;
				
				Span<UnsafeComponentReference> components = stackalloc UnsafeComponentReference[1];
				QueryExecution.Execute<float2>(ref query.query.Value, components, entityQueries, queryResultLookup, results);
			}
		}

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
			queryResultLookup = new NativeHashMap<IntPtr, NativeList<Entity>>(1,  Allocator.Persistent);
		}

		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			queryResultLookup.Clear();

			foreach (ref var component in components.AsArray().AsSpan())
			{
				if (component.runtimeEntityQuery == default && component.entityQueryDesc.IsCreated)
				{
					component.runtimeEntityQuery = component.entityQueryDesc.Value.CreateQuery(state.EntityManager);
				}
				
				if (component.runtimeEntityQuery != default)
				{
					component.results = component.runtimeEntityQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var dep);
					state.Dependency = dep;
					queryResultLookup[component.GetRuntimeKey()] = component.results;
				}
			}

			state.Dependency = new ExecuteQueriesJob
			{
				queryResultLookup = queryResultLookup,
			}.ScheduleParallel(state.Dependency);
		}

		void ISystem.OnDestroy(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			foreach (ref var component in components.AsArray().AsSpan())
			{
				component.runtimeEntityQuery.Dispose();
			}
			
			queryResultLookup.Dispose();
		}
	}
}

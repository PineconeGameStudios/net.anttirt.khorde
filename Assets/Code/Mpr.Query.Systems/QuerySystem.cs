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

				var components = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
				switch (query.query.Value.itemType)
				{
					case ExpressionValueType.Entity: QueryExecution.Execute<Entity>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Bool: QueryExecution.Execute<bool>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Bool2: QueryExecution.Execute<bool2>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Bool3: QueryExecution.Execute<bool3>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Bool4: QueryExecution.Execute<bool4>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Int: QueryExecution.Execute<int>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Int2: QueryExecution.Execute<int2>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Int3: QueryExecution.Execute<int3>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Int4: QueryExecution.Execute<int4>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Float: QueryExecution.Execute<float>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Float2: QueryExecution.Execute<float2>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Float3: QueryExecution.Execute<float3>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Float4: QueryExecution.Execute<float4>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
					case ExpressionValueType.Quaternion: QueryExecution.Execute<quaternion>(ref query.query.Value, components, entityQueries, queryResultLookup, results); break;
				}
			}
		}

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
			queryResultLookup = new NativeHashMap<IntPtr, NativeList<Entity>>(1, Allocator.Persistent);
		}

		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			queryResultLookup.Clear();

			foreach(ref var component in components.AsArray().AsSpan())
			{
				if(component.runtimeEntityQuery == default && component.entityQueryDesc.IsCreated)
				{
					component.runtimeEntityQuery = component.entityQueryDesc.Value.CreateQuery(state.EntityManager);
				}

				if(component.runtimeEntityQuery != default)
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
			queryResultLookup.Dispose();
		}
	}
}

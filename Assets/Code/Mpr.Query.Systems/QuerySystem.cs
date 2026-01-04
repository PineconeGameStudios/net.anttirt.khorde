using Mpr.Expr;
using System;
using Mpr.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		private NativeHashMap<Hash128, NativeList<Entity>> queryResultLookup;

		// TODO: turn this into IJobChunk to pass dynamic expression component data as in BTUpdateSystem
		[BurstCompile]
		partial struct ExecuteQueriesJob : IJobEntity
		{
			// NOTE: have to disable safety here because
			// ToEntityListAsync returns a NativeList
			[NativeDisableContainerSafetyRestriction]
			public NativeHashMap<Hash128, NativeList<Entity>> queryResultLookup;

			public void Execute(Query query, DynamicBuffer<QSResultItemStorage> results)
			{
				if(!query.query.IsCreated)
					return;

				var components = new NativeArray<UnsafeComponentReference>(0, Allocator.Temp);
				var lookups = new NativeArray<UntypedComponentLookup>(0, Allocator.Temp);
				
				var qctx = new QueryExecutionContext(ref query.query.Value, components, lookups, queryResultLookup);
				
				switch (query.query.Value.itemType)
				{
					case ExpressionValueType.Entity: qctx.Execute<Entity>(results); break;
					case ExpressionValueType.Bool: qctx.Execute<bool>(results); break;
					case ExpressionValueType.Bool2: qctx.Execute<bool2>(results); break;
					case ExpressionValueType.Bool3: qctx.Execute<bool3>(results); break;
					case ExpressionValueType.Bool4: qctx.Execute<bool4>(results); break;
					case ExpressionValueType.Int: qctx.Execute<int>(results); break;
					case ExpressionValueType.Int2: qctx.Execute<int2>(results); break;
					case ExpressionValueType.Int3: qctx.Execute<int3>(results); break;
					case ExpressionValueType.Int4: qctx.Execute<int4>(results); break;
					case ExpressionValueType.Float: qctx.Execute<float>(results); break;
					case ExpressionValueType.Float2: qctx.Execute<float2>(results); break;
					case ExpressionValueType.Float3: qctx.Execute<float3>(results); break;
					case ExpressionValueType.Float4: qctx.Execute<float4>(results); break;
					case ExpressionValueType.Quaternion: qctx.Execute<quaternion>(results); break;
				}
			}
		}

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
			queryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(1, Allocator.Persistent);
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			queryResultLookup.Clear();

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
					queryResultLookup[component.hash] = component.results;
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

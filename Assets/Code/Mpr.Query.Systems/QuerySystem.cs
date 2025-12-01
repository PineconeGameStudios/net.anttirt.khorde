using Mpr.Expr;
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		[BurstCompile]
		partial struct QueryJob : IJobEntity
		{
			public void Execute(Query query)
			{
				if(!query.query.IsCreated)
					return;
				Span<UnsafeComponentReference> components = stackalloc UnsafeComponentReference[1];
				QueryExecution.Execute<float2>(ref query.query.Value, components, Allocator.Temp);
			}
		}

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
		}

		void ISystem.OnUpdate(ref SystemState state)
		{
			new QueryJob().Schedule();
		}
	}
}

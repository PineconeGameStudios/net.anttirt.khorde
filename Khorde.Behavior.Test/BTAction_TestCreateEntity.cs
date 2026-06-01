using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Khorde.Behavior.Test
{
	class BTAction_TestCreateEntity : BehaviorTreeAction
	{
		public BehaviorTreeActionParam<float3> position;
		public BehaviorTreeActionParam<quaternion> rotation;
		public BehaviorTreeActionParam<float3> scale;

		public override void Invoke(ref SystemState state, Entity btEntity, Entity actionEntity, in BehaviorTreeInvocation call)
		{
			var newEntity = state.EntityManager.CreateEntity(typeof(ActionTestComponent), typeof(LocalToWorld));

			state.EntityManager.SetComponentData(newEntity, new ActionTestComponent
			{
				value = 42
			});

			state.EntityManager.SetComponentData(newEntity, new LocalToWorld
			{
				Value = float4x4.TRS(call.Get(position), call.Get(rotation), call.Get(scale))
			});
		}
	}

	struct ActionTestComponent : IComponentData
	{
		public int value;
	}
}

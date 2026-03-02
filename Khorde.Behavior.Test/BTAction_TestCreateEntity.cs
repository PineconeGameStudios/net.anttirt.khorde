using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Khorde.Behavior.Test
{
	class BTAction_TestCreateEntity : BehaviorTreeAction
	{
		public override void Invoke(ref SystemState state, Entity entity)
		{
			var newEntity = state.EntityManager.CreateEntity(typeof(ActionTestComponent));
			state.EntityManager.SetComponentData(newEntity, new ActionTestComponent
			{
				value = 42
			});
		}
	}

	struct ActionTestComponent : IComponentData
	{
		public int value;
	}
}

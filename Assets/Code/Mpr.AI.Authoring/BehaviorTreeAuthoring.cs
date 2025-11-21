using Unity.Entities;
using UnityEngine;

namespace Mpr.AI.BT
{
	public class BehaviorTreeAuthoring : MonoBehaviour
	{
		public BehaviorTreeAsset behaviorTree;

		class Baker : Baker<BehaviorTreeAuthoring>
		{
			public override void Bake(BehaviorTreeAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.None);
				AddComponent(entity, new BehaviorTree
				{
					// TODO: bake tree into blob asset
					// tree = 
				});
			}
		}
	}
}

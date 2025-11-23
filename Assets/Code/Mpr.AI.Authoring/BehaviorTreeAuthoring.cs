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
				if(authoring.behaviorTree == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				var tree = authoring.behaviorTree.LoadPersistent();
				AddBlobAsset(ref tree, out _);

				AddComponent(entity, new BehaviorTree
				{
					tree = tree,
				});
			}
		}
	}
}

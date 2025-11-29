using Unity.Entities;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeAuthoring : MonoBehaviour
	{
		public BehaviorTreeAsset behaviorTree;

		class Baker : Baker<BehaviorTreeAuthoring>
		{
			public override void Bake(BehaviorTreeAuthoring authoring)
			{
				DependsOn(authoring.behaviorTree);

				if(authoring.behaviorTree == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				var tree = authoring.behaviorTree.LoadPersistent();
				AddBlobAsset(ref tree, out _);

				AddSharedComponent(entity, new BehaviorTree
				{
					tree = tree,
				});

				AddBuffer<BTStackFrame>(entity);
				AddComponent(entity, new BTState
				{
				});
			}
		}
	}
}

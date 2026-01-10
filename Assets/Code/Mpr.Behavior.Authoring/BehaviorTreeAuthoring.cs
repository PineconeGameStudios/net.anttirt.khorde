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

				//var treeHandle = authoring.behaviorTree.LoadPersistent(BTData.SchemaVersion);
				//var tree = treeHandle.Reference;
				//AddBlobAsset(ref tree, out _);

				AddSharedComponent(entity, new BehaviorTree
				{
					tree = authoring.behaviorTree,
				});

				AddBuffer<BTStackFrame>(entity);
				AddComponent(entity, new BTState
				{
				});
			}
		}
	}
}

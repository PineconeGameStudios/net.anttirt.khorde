using Mpr.Query;
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

				AddSharedComponent(entity, new BehaviorTree
				{
					tree = authoring.behaviorTree,
				});

				AddBuffer<BTStackFrame>(entity);
				AddComponent(entity, new BTState
				{
				});

				if (authoring.behaviorTree.Queries.Count > 0)
				{
					var reg = new QueryAssetRegistration();
					foreach (var query in authoring.behaviorTree.Queries)
						reg.Add(query);
					AddSharedComponent(entity, reg);
				}
			}
		}
	}
}

using Unity.Entities;
using UnityEngine;

namespace Khorde.Behavior.Authoring
{
	class BehaviorTreeDebugAuthoring : MonoBehaviour
	{
		class Baker : Baker<BehaviorTreeDebugAuthoring>
		{
			public override void Bake(BehaviorTreeDebugAuthoring authoring)
			{
				AddComponent<BehaviorTreeDebugEnable>(GetEntity(authoring, TransformUsageFlags.None));
			}
		}
	}
}
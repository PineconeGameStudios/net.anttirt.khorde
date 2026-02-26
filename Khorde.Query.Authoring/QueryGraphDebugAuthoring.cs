using Khorde.Query;
using Unity.Entities;
using UnityEngine;

namespace Khorde.Behavior.Authoring
{
	class QueryGraphDebugAuthoring : MonoBehaviour
	{
		class Baker : Baker<QueryGraphDebugAuthoring>
		{
			public override void Bake(QueryGraphDebugAuthoring authoring)
			{
				AddComponent<QueryDebugEnable>(GetEntity(authoring, TransformUsageFlags.None));
			}
		}
	}
}

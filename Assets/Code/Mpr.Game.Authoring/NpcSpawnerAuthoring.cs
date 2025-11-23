using Unity.Entities;
using UnityEngine;

namespace Mpr.Game
{
	class NpcSpawnerAuthoring : MonoBehaviour
	{
		public NpcSpawnerConfig config;
		public GameObject prefab;

		class Baker : Baker<NpcSpawnerAuthoring>
		{
			public override void Bake(NpcSpawnerAuthoring authoring)
			{
				var config = authoring.config;
				config.prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic);

				var entity = GetEntity(authoring, TransformUsageFlags.None);
				AddComponent(entity, config);
			}
		}
	}
}
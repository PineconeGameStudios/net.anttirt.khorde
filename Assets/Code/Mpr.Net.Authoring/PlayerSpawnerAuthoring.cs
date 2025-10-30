using Unity.Entities;
using UnityEngine;

namespace Mpr.Net
{
	public class PlayerSpawnerAuthoring : MonoBehaviour
	{
		public Game.PlayerControllerAuthoring prefab;

		class Baker : Baker<PlayerSpawnerAuthoring>
		{
			public override void Bake(PlayerSpawnerAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, new PlayerSpawner { prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic) });
			}
		}
	}
}

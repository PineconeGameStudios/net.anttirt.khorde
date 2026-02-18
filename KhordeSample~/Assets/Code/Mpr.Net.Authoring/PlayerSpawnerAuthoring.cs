using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Mpr.Net
{
	public class PlayerSpawnerAuthoring : MonoBehaviour
	{
		public Game.PlayerControllerAuthoring prefab;
		public Game.PlayerControllerAuthoring netPrefab;

		class Baker : Baker<PlayerSpawnerAuthoring>
		{
			public override void Bake(PlayerSpawnerAuthoring authoring)
			{
				if(authoring.prefab != null && authoring.prefab.GetComponent<GhostAuthoringComponent>() != null)
					Debug.LogError("non-net prefab should not have a ghost authoring component");

				if(authoring.netPrefab != null && authoring.netPrefab.GetComponent<GhostAuthoringComponent>() == null)
					Debug.LogError("net prefab should have a ghost authoring component");

				DependsOn(authoring.prefab);
				DependsOn(authoring.netPrefab);

				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, new PlayerSpawner
				{
					prefab = authoring.prefab != null ? GetEntity(authoring.prefab, TransformUsageFlags.Dynamic) : default,
					netPrefab = authoring.netPrefab != null ? GetEntity(authoring.netPrefab, TransformUsageFlags.Dynamic) : default,
				});
			}
		}
	}
}

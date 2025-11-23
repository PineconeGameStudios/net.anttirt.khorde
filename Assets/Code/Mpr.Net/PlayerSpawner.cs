using Unity.Entities;

namespace Mpr.Net
{
	public struct PlayerSpawner : IComponentData
	{
		public Entity prefab;
		public Entity netPrefab;
	}
}

using Unity.Entities;
using UnityEngine;

namespace Mpr.Game
{
	public class PlayerControllerAuthoring : MonoBehaviour
	{
		class Baker : Baker<PlayerControllerAuthoring>
		{
			public override void Bake(PlayerControllerAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, new PlayerController
				{

				});
				AddComponent(entity, new PlayerInput
				{

				});
			}
		}
	}
}

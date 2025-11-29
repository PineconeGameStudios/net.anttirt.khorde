using Mpr.AI.BT.Nodes;
using Mpr.Expr.Authoring;
using System;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Game
{
	public class PlayerControllerAuthoring : MonoBehaviour
	{
		public PlayerController controller;

		class Baker : Baker<PlayerControllerAuthoring>
		{
			public override void Bake(PlayerControllerAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, authoring.controller);
				AddComponent(entity, new PlayerInput
				{

				});
			}
		}
	}

	[Serializable] class ReadPlayerController : ComponentReaderNode<PlayerController> { }
	[Serializable] class WritePlayerController : ComponentWriterNode<PlayerController> { }
}

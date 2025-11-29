using Mpr.AI.BT.Nodes;
using Mpr.Expr;
using System;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Game
{
	public class NpcControllerAuthoring : MonoBehaviour
	{
		public NpcController controller;
		public MoveTarget moveTarget;

		class Baker : Baker<NpcControllerAuthoring>
		{
			public override void Bake(NpcControllerAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, authoring.controller);
				AddComponent(entity, authoring.moveTarget);
			}
		}
	}

	[Serializable] class ReadMoveTarget : ComponentReaderNode<MoveTarget> { }
	[Serializable] class WriteMoveTarget : ComponentWriterNode<MoveTarget> { }
}
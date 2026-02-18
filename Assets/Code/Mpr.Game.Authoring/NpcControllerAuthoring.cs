using Khorde.Behavior.Authoring;
using Khorde.Expr.Authoring;
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
				AddComponent(entity, new NpcTargetEntity { });
			}
		}
	}

	[Serializable] class ReadMoveTarget : ComponentReaderNode<MoveTarget> { }
	[Serializable] class WriteMoveTarget : ComponentWriterNode<MoveTarget> { }

	[Serializable] class ReadNpcTargetEntity : ComponentReaderNode<NpcTargetEntity> { }
	[Serializable] class WriteNpcTargetEntity : ComponentWriterNode<NpcTargetEntity> { }

	[Serializable] class ReadNpcController : ComponentReaderNode<NpcController> { }
	[Serializable] class WriteNpcController : ComponentWriterNode<NpcController> { }
}
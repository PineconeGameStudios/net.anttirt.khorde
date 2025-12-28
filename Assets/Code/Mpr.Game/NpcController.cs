using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Mpr.Game
{
	[Serializable]
	public struct NpcController : IComponentData
	{
		public float speed;
	}

	[Serializable]
	public struct MoveTarget : IComponentData
	{
		[GhostField] public float3 position;
		[GhostField] public float tolerance;
		[GhostField] public bool enabled;
	}

	public struct NpcTargetEntity : IComponentData
	{
		[GhostField] public Entity target;
	}
}
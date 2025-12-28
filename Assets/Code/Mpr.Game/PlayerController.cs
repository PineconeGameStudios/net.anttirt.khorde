using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Mpr.Game
{
	[Serializable]
	public struct PlayerController : IComponentData
	{
		public float speed;
	}

	public struct PlayerInput : IInputComponentData
	{
		public float2 move;
		public float rotate;
	}
}

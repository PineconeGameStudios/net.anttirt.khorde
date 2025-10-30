using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Mpr.Game
{
	public struct PlayerController : IComponentData
	{
	}

	public struct PlayerInput : IInputComponentData
	{
		public float2 move;
	}
}

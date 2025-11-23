using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Mpr.Game
{
	[UpdateInGroup(typeof(GhostInputSystemGroup))]
	public partial class GatherInputsSystem : SystemBase
	{
		Input.InputActions inputActions;

		protected override void OnCreate()
		{
			inputActions = new Input.InputActions();
			inputActions.Enable();

			RequireForUpdate<PlayerInput>();
		}

		protected override void OnUpdate()
		{
			float2 move = inputActions.Player.Move.ReadValue<Vector2>();

			foreach(var input in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
			{
				input.ValueRW.move = move;
			}

			foreach(var input in SystemAPI.Query<RefRW<PlayerInput>>().WithNone<GhostOwner>())
			{
				input.ValueRW.move = move;
			}
		}

		protected override void OnDestroy()
		{
			inputActions.Disable();
			inputActions.Dispose();
		}
	}
}

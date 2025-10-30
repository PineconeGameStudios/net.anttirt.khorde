using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Mpr.Game
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial class PlayerControllerSystem : SystemBase
	{
		protected override void OnCreate()
		{
			RequireForUpdate<PlayerInput>();
		}

		protected override void OnUpdate()
		{
			foreach(var (input, controller, ltw) in SystemAPI.Query<PlayerInput, PlayerController, RefRW<LocalTransform>>())
			{
				ltw.ValueRW.Position.xy += input.move * SystemAPI.Time.DeltaTime * controller.speed;
			}
		}
	}

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
			foreach(var input in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
			{
				input.ValueRW.move = inputActions.Player.Move.ReadValue<Vector2>();
			}
		}

		protected override void OnDestroy()
		{
			inputActions.Disable();
			inputActions.Dispose();
		}
	}
}

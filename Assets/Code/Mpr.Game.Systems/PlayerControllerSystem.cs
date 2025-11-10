using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Mpr.Game
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial struct PlayerControllerSystem : ISystem
	{
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerInput>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			foreach(var (input, controller, ltw) in SystemAPI.Query<PlayerInput, PlayerController, RefRW<LocalTransform>>())
			{
				ltw.ValueRW.Position.xy += input.move * SystemAPI.Time.DeltaTime * controller.speed;
			}
		}
	}
}

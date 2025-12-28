using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
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
		partial struct MoveJob : IJobEntity
		{
			public float deltaTime;

			public void Execute(PlayerInput input, PlayerController controller, ref LocalTransform transform)
			{
				transform.Position.xy += input.move * deltaTime * controller.speed;
				transform = transform.Rotate(quaternion.AxisAngle(new float3(0, 0, -1), input.rotate * deltaTime * math.PI2));
			}
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.Dependency = new MoveJob { deltaTime = SystemAPI.Time.DeltaTime }.Schedule(state.Dependency);
		}
	}
}

using Unity.Burst;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

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
				var dp = input.move * deltaTime * controller.speed;
				if(controller.up == PlayerController.UpAxis.Z)
					transform.Position.xy += dp;
				else if(controller.up == PlayerController.UpAxis.Y)
					transform.Position.xz += dp;
				else if(controller.up == PlayerController.UpAxis.X)
					transform.Position.yz += dp;

				transform = transform.Rotate(quaternion.AxisAngle(new float3(0, 0, -1), input.rotate * deltaTime * math.PI2));
			}
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.Dependency = new MoveJob { deltaTime = SystemAPI.Time.DeltaTime }.Schedule(state.Dependency);
		}
	}

	[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
	public partial class PlayerCameraSystem : SystemBase
	{
		CinemachineCamera camera;

		protected override void OnUpdate()
		{
			if(camera == null)
				camera = Object.FindFirstObjectByType<CinemachineCamera>();

			foreach(var (pc, lt) in SystemAPI.Query<RefRW<PlayerController>, RefRO<LocalTransform>>())
			{
				Transform target = pc.ValueRW.cameraTarget;

				if(target == null)
					pc.ValueRW.cameraTarget = target = new GameObject().transform;

				target.position = lt.ValueRO.Position;

				camera.Target.TrackingTarget = target;
			}
		}
	}
}

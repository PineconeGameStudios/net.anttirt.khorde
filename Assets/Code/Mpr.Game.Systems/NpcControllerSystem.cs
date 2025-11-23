using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Mpr.Game
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial struct NpcControllerSystem : ISystem
	{
		[BurstCompile]
		partial struct MoveJob : IJobEntity
		{
			public float deltaTime;

			public void Execute(NpcController controller, ref MoveTarget target, ref LocalTransform transform)
			{
				if(target.enabled)
				{
					var delta = target.position - transform.Position;
					var distanceSq = math.lengthsq(delta);

					if(distanceSq <= target.tolerance * target.tolerance)
					{
						target.enabled = false;
					}
					else
					{
						var distance = math.sqrt(distanceSq);
						var travel = math.min(distance, controller.speed * deltaTime);
						if(travel == distance)
						{
							transform.Position = target.position;
							target.enabled = false;
						}
						else
						{
							transform.Position += travel * (delta / distance);
							delta = target.position - transform.Position;
							distanceSq = math.lengthsq(delta);

							if(distanceSq <= target.tolerance * target.tolerance)
							{
								target.enabled = false;
							}
						}
					}
				}
			}
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.Dependency = new MoveJob { deltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}
	}
}
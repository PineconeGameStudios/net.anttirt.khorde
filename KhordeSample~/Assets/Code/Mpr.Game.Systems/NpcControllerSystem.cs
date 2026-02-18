using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Mpr.Game
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial struct NpcControllerSystem : ISystem
	{
		EntityQuery moveQuery;

		void ISystem.OnCreate(ref SystemState state)
		{
			var moveQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
				.WithAll<NpcController>()
				.WithAllRW<MoveTarget, LocalTransform>()
				.WithAllRW<NpcTargetEntity>()
				;

			bool clientWorld = (state.WorldUnmanaged.Flags & WorldFlags.GameClient) == WorldFlags.GameClient;

			if(clientWorld)
			{
				moveQueryBuilder = moveQueryBuilder.WithAll<PredictedGhost, Simulate>();
			}

			moveQuery = moveQueryBuilder.Build(ref state);
		}

		[BurstCompile]
		partial struct MoveJob : IJobEntity
		{
			public float deltaTime;
			public Entity playerEntity;

			public void Execute(NpcController controller, ref MoveTarget target, ref LocalTransform transform, ref NpcTargetEntity targetEntity)
			{
				targetEntity.target = playerEntity;

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
			SystemAPI.TryGetSingletonEntity<PlayerController>(out var playerEntity);
			state.Dependency = new MoveJob
			{
				deltaTime = SystemAPI.Time.DeltaTime,
				playerEntity = playerEntity,
			}.ScheduleParallel(moveQuery, state.Dependency);
		}
	}
}
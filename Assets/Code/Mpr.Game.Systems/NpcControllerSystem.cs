using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Mpr.Game
{
	public partial struct NpcControllersystem : ISystem
	{
		void ISystem.OnUpdate(ref SystemState state)
		{
			foreach(var (controller, target_, transform_) in SystemAPI.Query<NpcController, RefRW<MoveTarget>, RefRW<LocalTransform>>())
			{
				ref var target = ref target_.ValueRW;

				if(target.enabled)
				{
					ref var transform = ref transform_.ValueRW;
					var delta = target.position - transform.Position;
					var distanceSq = math.lengthsq(delta);

					if(distanceSq <= target.tolerance * target.tolerance)
					{
						target.enabled = false;
					}
					else
					{
						var distance = math.sqrt(distanceSq);
						var travel = math.min(distance, controller.speed * SystemAPI.Time.DeltaTime);
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
	}
}
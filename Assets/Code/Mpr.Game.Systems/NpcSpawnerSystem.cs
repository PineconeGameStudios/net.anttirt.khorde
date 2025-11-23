using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Mpr.Game
{
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
	partial struct NpcSpawnerSystem : ISystem
	{
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<NpcSpawnerConfig>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			var config = SystemAPI.GetSingleton<NpcSpawnerConfig>();

			var entities = new NativeArray<Entity>(config.gridSize.x * config.gridSize.y, Allocator.Temp);
			state.EntityManager.Instantiate(config.prefab, entities);

			for(int y = 0; y < config.gridSize.y; y++)
			{
				for(int x = 0; x < config.gridSize.x; x++)
				{
					var entity = entities[y * config.gridSize.x + x];
					ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(entity).ValueRW;
					transform.Position = config.origin + new float3(
						(x - (config.gridSize.x / 2)) * config.gridSpacing,
						(y - (config.gridSize.y / 2)) * config.gridSpacing,
						0
						);
				}
			}

			state.Enabled = false;
		}
	}
}
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Mpr.Net
{

	[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
	partial struct GoInGameClientSystem : ISystem
	{
		[BurstCompile]
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate(
				SystemAPI.QueryBuilder()
					.WithAll<NetworkId>()
					.WithNone<NetworkStreamInGame>()
					.Build()
				);

			state.RequireForUpdate<PlayerSpawner>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
			foreach(var (id, entity) in SystemAPI.Query<NetworkId>().WithEntityAccess().WithNone<NetworkStreamInGame>())
			{
				ecb.AddComponent<NetworkStreamInGame>(entity);

				var req = ecb.CreateEntity();
				ecb.AddComponent<GoInGameRequest>(req);
				ecb.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
			}
			ecb.Playback(state.EntityManager);
		}
	}

	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	partial struct GoInGameServerSystem : ISystem
	{
		[BurstCompile]
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate(
				SystemAPI.QueryBuilder()
					.WithAll<GoInGameRequest>()
					.WithAll<ReceiveRpcCommandRequest>()
					.Build()
				);

			state.RequireForUpdate<PlayerSpawner>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			var prefab = SystemAPI.GetSingleton<PlayerSpawner>().netPrefab;
			state.EntityManager.GetName(prefab, out var prefabName);
			var worldName = new FixedString32Bytes(state.WorldUnmanaged.Name);
			var ecb = new EntityCommandBuffer(Allocator.Temp);
			foreach(var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<GoInGameRequest>().WithEntityAccess())
			{
				ecb.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
				var networkId = SystemAPI.GetComponentLookup<NetworkId>()[reqSrc.ValueRO.SourceConnection];
				UnityEngine.Debug.Log($"'{worldName}' setting connection '{networkId.Value}' to in game and spawning prefab '{prefabName}'");
				var player = ecb.Instantiate(prefab);
				ecb.SetComponent(player, new GhostOwner { NetworkId = networkId.Value });
				ecb.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup { Value = player });
				ecb.DestroyEntity(reqEntity);
			}
			ecb.Playback(state.EntityManager);
		}
	}

	[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
	partial struct LocalSpawnPlayerSystem : ISystem
	{
		[BurstCompile]
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerSpawner>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.Instantiate(SystemAPI.GetSingleton<PlayerSpawner>().prefab);
			state.Enabled = false;
		}
	}
}
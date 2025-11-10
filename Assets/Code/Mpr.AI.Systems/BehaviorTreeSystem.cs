using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Mpr.AI.BT
{
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	public partial struct BehaviorTreeSystem : ISystem
	{
		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<BehaviorTree>();
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			foreach(var (bt, ltw) in SystemAPI.Query<BehaviorTree, RefRW<LocalTransform>>())
			{
				// TODO: evaluate bt
			}
		}
	}
}

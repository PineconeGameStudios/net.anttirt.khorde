using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;

namespace Khorde.Behavior.Systems
{
	// NOTE: Update *before* the bt update system, since this system is likely to
	// cause a sync point and the bt system starts new jobs.
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	[UpdateBefore(typeof(BehaviorTreeUpdateSystem))]
	public partial class BehaviorTreeActionSystem : SystemBase
	{
		List<BehaviorTreeAction> resolved = new();

		protected override void OnUpdate()
		{
			// the actions may perform structural changes so we can't use SystemAPI.Query here
			foreach(var entity in SystemAPI.QueryBuilder().WithAll<BTInvokeQueue, BehaviorTreeActionRef>().Build().ToEntityArray(WorldUpdateAllocator))
			{
				resolved.Clear();

				var queue = EntityManager.GetBuffer<BTInvokeQueue>(entity);
				var actions = EntityManager.GetBuffer<BehaviorTreeActionRef>(entity);

				foreach(var index in queue)
					resolved.Add(actions[index.actionIndex].value.Value);

				queue.Clear();

				SystemAPI.SetBufferEnabled<BTInvokeQueue>(entity, false);

				foreach(var action in resolved)
					action.Invoke(ref CheckedStateRef, entity);
			}
		}
	}
}
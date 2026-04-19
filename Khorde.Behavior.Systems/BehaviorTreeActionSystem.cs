using System.Diagnostics;
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
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
		static void LogExecption(System.Exception e)
		{
			UnityEngine.Debug.LogException(e);
		}

		protected override void OnUpdate()
		{
			// the actions may perform structural changes so we can't use SystemAPI.Query here
			foreach(var entity in SystemAPI.QueryBuilder().WithAll<BehaviorTreeInvocation, BehaviorTreeActionRef>().Build().ToEntityArray(WorldUpdateAllocator))
			{
				var queue = EntityManager.GetBuffer<BehaviorTreeInvocation>(entity);
				var actions = EntityManager.GetBuffer<BehaviorTreeActionRef>(entity);

				for(int i = 0; i < queue.Length; ++i)
				{
					ref var call = ref queue.ElementAt(i);
					var action = actions[call.actionIndex];

					try
					{
						action.value.Value.Invoke(ref CheckedStateRef, entity, in call);
					}
					catch(System.Exception e)
					{
						LogExecption(e);
					}
				}

				queue.Clear();

				SystemAPI.SetBufferEnabled<BehaviorTreeInvocation>(entity, false);
			}
		}
	}
}
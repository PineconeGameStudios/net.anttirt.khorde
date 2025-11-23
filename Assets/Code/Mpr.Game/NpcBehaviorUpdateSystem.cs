using Mpr.AI.BT;
using System;
using Unity.Entities;
using Unity.Transforms;

namespace Mpr.Game
{
	public partial struct BehaviorTreeUpdateSystem : ISystem
	{
		Entity traceHolder;

		void ISystem.OnCreate(ref SystemState state)
		{
			traceHolder = state.EntityManager.CreateSingletonBuffer<BTExecTrace>();
		}

		void ISystem.OnUpdate(ref SystemState state)
		{
			foreach(var (tree, state_, stack, move_, transform) in SystemAPI.Query<
				AI.BT.BehaviorTree,
				RefRW<AI.BT.BehaviorTreeState>,
				DynamicBuffer<AI.BT.BTStackFrame>,
				RefRW<MoveTarget>,
				LocalTransform
				>())
			{
				Span<AI.BT.UnsafeComponentReference> componentPtrs = stackalloc AI.BT.UnsafeComponentReference[2];

				componentPtrs[0] = AI.BT.UnsafeComponentReference.Make(ref move_.ValueRW);
				var lt = transform;
				componentPtrs[1] = AI.BT.UnsafeComponentReference.Make(ref lt);

				var trace = SystemAPI.GetSingletonBuffer<BTExecTrace>();
				trace.Clear();

				try
				{
					AI.BT.BehaviorTreeExecution.Execute(
						tree.tree,
						ref state_.ValueRW,
						stack,
						componentPtrs,
						(float)SystemAPI.Time.ElapsedTime,
						trace
						);

				}
				catch
				{
					UnityEngine.Debug.Log(string.Join("\n", trace.AsNativeArray().AsSpan().ToArray()));
					throw;
				}
			}
		}
	}
}
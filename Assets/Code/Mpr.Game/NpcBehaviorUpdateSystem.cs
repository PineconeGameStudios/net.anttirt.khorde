using System;
using Unity.Entities;
using Unity.Transforms;

namespace Mpr.Game
{
	public partial struct BehaviorTreeUpdateSystem : ISystem
	{
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

				AI.BT.BehaviorTreeExecution.Execute(
					tree.tree,
					ref state_.ValueRW,
					stack,
					componentPtrs,
					(float)SystemAPI.Time.ElapsedTime,
					default
					);
			}
		}
	}
}
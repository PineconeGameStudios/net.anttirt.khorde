using System;
using Unity.Entities;

namespace Mpr.Game
{
	public partial struct BehaviorTreeUpdateSystem : ISystem
	{
		void ISystem.OnUpdate(ref SystemState state)
		{
			foreach(var (tree, state_, stack, move_) in SystemAPI.Query<AI.BT.BehaviorTree, RefRW<AI.BT.BehaviorTreeState>, DynamicBuffer<AI.BT.BTStackFrame>, RefRW<MoveTarget>>())
			{
				Span<IntPtr> componentPtrs = stackalloc IntPtr[1];

				unsafe
				{
					ref var move = ref move_.ValueRW;
					fixed(void* movePtr = &move)
						componentPtrs[0] = (IntPtr)movePtr;
				}

				AI.BT.BehaviorTreeExecution.Execute(
					ref tree.tree.Value,
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
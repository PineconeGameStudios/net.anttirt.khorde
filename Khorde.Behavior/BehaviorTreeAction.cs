using Unity.Entities;
using UnityEngine;

namespace Khorde.Behavior
{
	public abstract class BehaviorTreeAction : ScriptableObject
	{
		public abstract void Invoke(ref SystemState state, Entity entity);
	}

	public struct BehaviorTreeActionRef : IBufferElementData
	{
		public UnityObjectRef<BehaviorTreeAction> value;
	}

	public struct BTInvokeQueue : IBufferElementData, IEnableableComponent
	{
		public int actionIndex;
	}
}
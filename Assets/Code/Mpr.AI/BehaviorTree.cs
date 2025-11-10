using System;
using Unity.Entities;

namespace Mpr.AI.BT
{
	[Serializable]
	public struct BehaviorTree : IComponentData
	{
		public BlobAssetReference<BehaviorTree> tree;
	}

	public struct BehaviorTreeData
	{
	}
}

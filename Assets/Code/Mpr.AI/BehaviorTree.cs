using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Mpr.Expr;

namespace Mpr.AI.BT
{
	public struct BehaviorTree : ISharedComponentData
	{
		public BlobAssetReference<BTData> tree;
	}

	public struct BTState : IComponentData
	{
	}

	[InternalBufferCapacity(8)]
	public struct BTStackFrame : IBufferElementData
	{
		public BTExecNodeId nodeId;
		public byte childIndex;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BTStackFrame(BTExecNodeId nodeId) => new BTStackFrame { nodeId = nodeId };
	}

	public struct BTExecTrace : IBufferElementData
	{
		public BTExecNodeId nodeId;
		public BTExec.BTExecType type;
		public Event @event;
		public int depth;
		public int cycle;

		public BTExecTrace(BTExecNodeId nodeId, BTExec.BTExecType type, Event @event, int depth, int cycle)
		{
			this.nodeId = nodeId;
			this.type = type;
			this.@event = @event;
			this.depth = depth;
			this.cycle = cycle;
		}

		public enum Event : byte
		{
			Init,
			Start,
			Call,
			Return,
			Fail,
			Catch,
			Yield,
			Wait,
		}

		public override string ToString() => $"[{type}.{nodeId.index}] {depth}> {@event} @{cycle}";

		#region Equality
		public bool Equals(in BTExecTrace other) =>
			nodeId.index == other.nodeId.index &&
			type == other.type &&
			@event == other.@event &&
			depth == other.depth;

		public override int GetHashCode()
		{
			int hashCode = 17;
			hashCode = hashCode * 23 + nodeId.index.GetHashCode();
			hashCode = hashCode * 23 + type.GetHashCode();
			hashCode = hashCode * 23 + @event.GetHashCode();
			hashCode = hashCode * 23 + depth.GetHashCode();
			return hashCode;
		}

		public override bool Equals(object obj) => obj is BTExecTrace trace && Equals(trace);
		#endregion
	}

	public struct BTData
	{
		public BTExprData exprData;
		public BlobArray<BTExec> execs;
		public BlobArray<UnityEngine.Hash128> execNodeIds;
		public BlobArray<BlobArray<UnityEngine.Hash128>> execNodeSubgraphStacks;

		public BTExecNodeId Root
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				// the root is always at index 1; index 0 is reserved for Nop
				return new(1);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExec GetNode(BTExecNodeId id) => ref execs[id.index];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExpr GetNode(BTExprNodeRef nodeRef) => ref exprData.exprs[nodeRef.index];
	}
}

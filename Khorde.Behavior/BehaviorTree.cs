using Khorde.Expr;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.NetCode;

namespace Khorde.Behavior
{
	public struct BehaviorTree : ISharedComponentData
	{
		public UnityObjectRef<BehaviorTreeAsset> tree;
	}

	public struct BTState : IComponentData
	{
		// only one stack at a time can execute a query; others must wait
		int queryExecutorStackIndexPlusOne;

		public int QueryExecutorThreadIndex
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => queryExecutorStackIndexPlusOne - 1;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => queryExecutorStackIndexPlusOne = value + 1;
		}
	}

	[InternalBufferCapacity(2)]
	public struct BTThread : IBufferElementData
	{
		/// <summary>
		/// Offset into the <see cref="BTStackFrame"/> buffer where this thread's stack starts
		/// </summary>
		public int frameOffset;

		/// <summary>
		/// Number of frames currently in this thread's stack
		/// </summary>
		public int frameCount;

		public int ownerThreadIndex;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetEndOffset() => frameOffset + frameCount;

		// below: misc. per-stack state

		/// <summary>
		/// Start time for the current Wait operation, if there is one on this thread
		/// </summary>
		public float waitStartTime;
	}

	[InternalBufferCapacity(8)]
	public struct BTStackFrame : IBufferElementData
	{
		[GhostField] public BTExecNodeId nodeId;
		[GhostField] public byte childIndex;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BTStackFrame(BTExecNodeId nodeId) => new() { nodeId = nodeId };
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
		public const int SchemaVersion = 1;

		public BlobExpressionData exprData;
		public BlobArray<BTExec> execs;
		public BlobArray<UnityEngine.Hash128> execNodeIds;
		public BlobArray<BlobArray<UnityEngine.Hash128>> execNodeSubgraphStacks;
		public bool hasQueries;

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
	}
}

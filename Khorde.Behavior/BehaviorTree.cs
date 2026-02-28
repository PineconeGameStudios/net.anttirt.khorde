using Khorde.Expr;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Khorde.Behavior
{
	public struct BehaviorTree : ISharedComponentData
	{
		public BlobAssetReference<BTData> tree;
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

		public int threadIdCounter;
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

		/// <summary>
		/// Thread id, currently just used for tracing and debugging.
		/// </summary>
		public int threadId;

		/// <summary>
		/// Index of the owning thread.
		/// </summary>
		public int ownerThreadIndex;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetEndOffset() => frameOffset + frameCount;

		// below: misc. per-thread state

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
		public int threadId;
		public int depth;
		public int cycle;

		public BTExecTrace(BTExecNodeId nodeId, BTExec.BTExecType type, Event @event, int threadId, int depth, int cycle)
		{
			this.nodeId = nodeId;
			this.type = type;
			this.@event = @event;
			this.threadId = threadId;
			this.depth = depth;
			this.cycle = cycle;
		}

		public enum Event : byte
		{
			Spawn,
			Start,
			Call,
			Return,
			Fail,
			Catch,
			Yield,
			Wait,
			Abort,
		}

		public override string ToString() => $"{{{threadId}}} [{type}.{nodeId.index}] {depth}> {@event} @{cycle}";

		public void AppendTo(System.Text.StringBuilder sb)
		{
			sb.Append('{');
			sb.Append(threadId);
			sb.Append("} [");
			sb.Append(type);
			sb.Append('.');
			sb.Append(nodeId.index);
			sb.Append("] ");
			sb.Append(depth);
			sb.Append("> ");
			sb.Append(@event);
			sb.Append(" @");
			sb.Append(cycle);
		}

		#region Equality
		public bool Equals(in BTExecTrace other) =>
			nodeId.index == other.nodeId.index &&
			type == other.type &&
			@event == other.@event &&
			threadId == other.threadId &&
			depth == other.depth;

		public override int GetHashCode()
		{
			int hashCode = 17;
			hashCode = hashCode * 23 + nodeId.index.GetHashCode();
			hashCode = hashCode * 23 + type.GetHashCode();
			hashCode = hashCode * 23 + @event.GetHashCode();
			hashCode = hashCode * 23 + threadId.GetHashCode();
			hashCode = hashCode * 23 + depth.GetHashCode();
			return hashCode;
		}

		public override bool Equals(object obj) => obj is BTExecTrace trace && Equals(trace);
		#endregion
	}

	public struct BTData
	{
		public const int SchemaVersion = 7
			| (BlobExpressionData.SchemaVersion << 16);

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

	/// <summary>
	/// Create an entity with this tag component in the world to allow the <see
	/// cref="BehaviorTreeDebugSystem"/> to run. The system creates the tag
	/// automatically when running in the editor.
	/// </summary>
	public struct BehaviorTreeDebugEnable : IComponentData { }
}

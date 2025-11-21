using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Mpr.AI.BT
{
	public struct BehaviorTree : IComponentData
	{
		public BlobAssetReference<BTData> tree;
	}

	public struct BehaviorTreeState : IComponentData
	{
		public bool selected;
	}

	[InternalBufferCapacity(8)]
	public struct BTStackFrame : IBufferElementData
	{
		public BTExecNodeId nodeId;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator BTStackFrame(BTExecNodeId nodeId) => new BTStackFrame { nodeId = nodeId };
	}

	public struct BTExecTrace : IBufferElementData
	{
		public BTExecNodeId nodeId;
		public BTExec.Type type;
		public Event @event;
		public int depth;
		public int cycle;

		public BTExecTrace(BTExecNodeId nodeId, BTExec.Type type, Event @event, int depth, int cycle)
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

	public readonly record struct BTExecNodeId(ushort index);
	public readonly record struct BTExprNodeRef(ushort index, byte outputIndex)
	{
		public readonly T Evaluate<T>(ref BTData data, ReadOnlySpan<IntPtr> componentPtrs) where T : unmanaged
		{
			return data.GetNode(this).Evaluate<T>(ref data, outputIndex, componentPtrs);
		}
	}

	public struct BTExec
	{
		public Type type;
		[Tooltip("Index of this node within its parent Sequence")]
		public byte childIndex;
		public Data data;

		public enum Type : byte
		{
			Nop,
			Root,
			Sequence,
			Selector,
			WriteField,
			Wait,
			Fail,
			Optional,
			Catch,
		}

		[StructLayout(LayoutKind.Explicit, Pack = 8)]
		public struct Data
		{
			[FieldOffset(0)] public Root root;
			[FieldOffset(0)] public Sequence sequence;
			[FieldOffset(0)] public Selector selector;
			[FieldOffset(0)] public WriteField writeField;
			[FieldOffset(0)] public Wait wait;
			[FieldOffset(0)] public Fail fail;
			[FieldOffset(0)] public Optional optional;
			[FieldOffset(0)] public Catch @catch;
		}
	}

	public struct ConditionalBlock
	{
		public BTExprNodeRef condition;
		public BTExecNodeId nodeId;
	}

	// TODO
	// public struct UtilityBlock
	// {
	// 	public UtilityData utility;
	// 	public BTExecNodeId nodeId;
	// }

	public struct Root { public BTExecNodeId child; }
	public struct Sequence { public BlobArray<BTExecNodeId> children; }
	public struct Selector { public BlobArray<ConditionalBlock> children; }
	public struct WriteField
	{
		public BTExprNodeRef input;
		public byte componentIndex;
		public ushort fieldOffset;
		public byte fieldSize;

		public readonly void Evaluate(ref BTData data, ReadOnlySpan<IntPtr> componentPtrs)
		{
			ref var inputExpr = ref data.exprs[input.index];

			unsafe
			{
				byte* fieldPtr = (byte*)componentPtrs[componentIndex].ToPointer() + fieldOffset;
				inputExpr.Evaluate(ref data, input.outputIndex, componentPtrs, new Span<byte>(fieldPtr, fieldSize));
			}
		}
	}

	public struct Wait { public BTExprNodeRef condition; }
	public struct Fail { }
	public struct Optional { public BTExprNodeRef condition; public BTExecNodeId child; }
	public struct Catch { public BTExecNodeId child; }

	public interface IBTExpr
	{
		void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<IntPtr> componentPtrs, Span<byte> result);
	}

	public struct BTExpr : IBTExpr
	{
		public Data data;
		public byte index;

		public readonly T Evaluate<T>(ref BTData data, byte outputIndex, ReadOnlySpan<IntPtr> componentPtrs) where T : unmanaged
		{
			T result = default;
			unsafe
			{
				Evaluate(ref data, outputIndex, componentPtrs, new Span<byte>(&result, sizeof(T)));
			}
			return result;
		}

		public readonly void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<IntPtr> componentPtrs, Span<byte> result)
		{
			switch(index)
			{
				case 0: this.data.readField.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case 1: this.data.@bool.Evaluate(ref data, outputIndex, componentPtrs, result); return;
			}
#if DEBUG
			throw new Exception();
#endif
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct Data
		{
			[FieldOffset(0)] public ReadField readField;
			[FieldOffset(0)] public Bool @bool;
		}

		public struct ReadField : IBTExpr
		{
			public byte componentIndex;
			public ushort fieldOffset;

			public readonly void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<IntPtr> componentPtrs, Span<byte> result)
			{
				unsafe
				{
					byte* fieldPtr = (byte*)componentPtrs[componentIndex].ToPointer() + fieldOffset;
					fixed(byte* resultPtr = result)
						UnsafeUtility.MemCpy(resultPtr, fieldPtr, result.Length);
				}
			}
		}

		public struct Bool : IBTExpr
		{
			public readonly record struct Const(bool value);
			public readonly record struct Not(BTExprNodeRef inner);
			public readonly record struct And(BTExprNodeRef left, BTExprNodeRef right);
			public readonly record struct Or(BTExprNodeRef left, BTExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Const @const;
				[FieldOffset(0)] public Not not;
				[FieldOffset(0)] public And and;
				[FieldOffset(0)] public Or or;
			}

			public Data data;
			public byte index;

			public readonly bool Evaluate(ref BTData btData, ReadOnlySpan<IntPtr> componentPtrs)
			{
				switch(index)
				{
					case 0: return data.@const.value;
					case 1: return !data.not.inner.Evaluate<bool>(ref btData, componentPtrs);
					case 2: return data.and.left.Evaluate<bool>(ref btData, componentPtrs) && data.and.right.Evaluate<bool>(ref btData, componentPtrs);
					case 3: return data.or.left.Evaluate<bool>(ref btData, componentPtrs) || data.or.right.Evaluate<bool>(ref btData, componentPtrs);
				}
#if DEBUG
				Debug.Log($"invalid BTBoolExpr type index {index}");
				throw new Exception();
#else
			return false;
#endif
			}

			public readonly void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<IntPtr> componentPtrs, Span<byte> result)
			{
				unsafe
				{
					fixed(byte* resultPtr = result)
						*(bool*)resultPtr = Evaluate(ref data, componentPtrs);
				}
			}
		}
	}

	public struct BTData
	{
		public BlobArray<BTExec> execs;
		public BlobArray<BTExpr> exprs;

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
		public ref BTExpr GetNode(BTExprNodeRef nodeRef) => ref exprs[nodeRef.index];
	}

	public struct BTDebugNodeState
	{
		public float lastVisitTime;
	}

	public struct BTDebugState : IComponentData
	{
		public NativeList<BTDebugNodeState> execs;
	}

	public static class BehaviorTreeExecution
	{
		public static void Execute(this BlobAssetReference<BTData> asset, ref BehaviorTreeState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<IntPtr> componentPtrs, float now, DynamicBuffer<BTExecTrace> trace)
			=> Execute(ref asset.Value, ref state, stack, componentPtrs, now, trace);

		public static void Execute(ref BTData data, ref BehaviorTreeState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<IntPtr> componentPtrs, float now, DynamicBuffer<BTExecTrace> trace)
		{
			if(stack.Length == 0)
			{
				if(trace.IsCreated)
					trace.Add(new(data.Root, BTExec.Type.Root, BTExecTrace.Event.Init, stack.Length, -1));

				stack.Add(data.Root);
			}

			bool rootVisited = false;

			// NOTE: we can only be here at either a Root or a Wait node, so we
			// can always start with nextChildIndex = 0
			int nextChildIndex = 0;

			for(int cycle = 0; ; ++cycle)
			{
				if(cycle > 10000)
					throw new Exception("max cycle count exceeded; almost certainly a bug in the implementation");

				var nodeId = stack[^1].nodeId;

				ref BTExec node = ref data.GetNode(nodeId);

				if(trace.IsCreated && cycle == 0)
					trace.Add(new(nodeId, node.type, BTExecTrace.Event.Start, stack.Length, cycle));

				if(cycle == 0 && node.type != BTExec.Type.Root && node.type != BTExec.Type.Wait)
					throw new InvalidOperationException($"BUG: Execute() started with node type {node.type}");

				void Trace(ref BTExec node, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(nodeId, node.type, @event, stack.Length, cycle));
				}

				void Trace1(ref BTData data, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(nodeId, data.GetNode(nodeId).type, @event, stack.Length, cycle));
				}

				void Trace2(ref BTData data, int stackIndex, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(stack[stackIndex].nodeId, data.GetNode(stack[stackIndex].nodeId).type, @event, stackIndex + 1, cycle));
				}

				void Fail(ref BTData data, ref BTExec node)
				{
					Trace(ref node, BTExecTrace.Event.Fail);

					for(int i = stack.Length - 1; i > 0; --i)
					{
						ref var stackNode = ref data.GetNode(stack[i].nodeId);
						if(stackNode.type == BTExec.Type.Catch)
						{
							Trace2(ref data, i, BTExecTrace.Event.Catch);
							stack.RemoveRange(i, stack.Length - i);
							nextChildIndex = stackNode.childIndex + 1;
							return;
						}
					}

					stack.Clear();
					stack.Add(data.Root);
					nextChildIndex = 0;
				}

				void Return(ref BTData data, ref BTExec node)
				{
					Trace(ref node, BTExecTrace.Event.Return);

					stack.RemoveAt(stack.Length - 1);
					nextChildIndex = node.childIndex + 1;
				}

				void Call(ref BTData data, BTExecNodeId node)
				{
					Trace1(ref data, BTExecTrace.Event.Call);

					stack.Add(node);
					nextChildIndex = 0;
				}

				if(state.selected)
				{
					// TODO
					// DebugState.Data.execs.ElementAt(nodeId).lastVisitTime = now;
				}

				switch(node.type)
				{
					case BTExec.Type.Nop:
						Return(ref data, ref node);
						break;

					case BTExec.Type.Root:
						if(stack.Length != 1)
							throw new Exception($"Root should always be the first stack frame, found at {stack.Length}");

						if(rootVisited)
						{
							// visit the root node at most once per frame to avoid getting stuck here
							Trace(ref node, BTExecTrace.Event.Yield);
							return;
						}

						rootVisited = true;

						Call(ref data, node.data.root.child);
						break;

					case BTExec.Type.Sequence:
						if(nextChildIndex < node.data.sequence.children.Length)
						{
							Call(ref data, node.data.sequence.children[nextChildIndex]);
						}
						else
						{
							Return(ref data, ref node);
						}

						break;

					case BTExec.Type.Selector:
						if(nextChildIndex == 0)
						{
							bool any = false;

							for(int childIndex = 0; childIndex < node.data.selector.children.Length; ++childIndex)
							{
								ref var child = ref node.data.selector.children[childIndex];
								if(child.condition.Evaluate<bool>(ref data, componentPtrs))
								{
									any = true;
									Call(ref data, child.nodeId);
									break;
								}
							}

							if(!any)
							{
								// none of the options worked
								Fail(ref data, ref node);
							}
						}
						else
						{
							// already executed one of our children, go back to parent
							Return(ref data, ref node);
						}
						break;

					case BTExec.Type.WriteField:
						node.data.writeField.Evaluate(ref data, componentPtrs);
						Return(ref data, ref node);
						break;

					case BTExec.Type.Wait:
						if(node.data.wait.condition.Evaluate<bool>(ref data, componentPtrs))
						{
							Return(ref data, ref node);
						}
						else
						{
							// still waiting, can't execute any more nodes until input data changes
							Trace(ref node, BTExecTrace.Event.Wait);
							return;
						}

						break;

					case BTExec.Type.Fail:
						Fail(ref data, ref node);
						break;

					case BTExec.Type.Optional:
						if(nextChildIndex == 0 && node.data.optional.condition.Evaluate<bool>(ref data, componentPtrs))
						{
							Call(ref data, node.data.optional.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					case BTExec.Type.Catch:
						if(nextChildIndex == 0)
						{
							Call(ref data, node.data.@catch.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					default:
						throw new NotImplementedException($"BTExec node type {node.type} not implemented");
				}
			}
		}
	}
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
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
		public byte childIndex;

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
	public struct BTExprNodeRef
	{
		public ushort index;
		public byte outputIndex;
		public bool constant;

		public override bool Equals(object obj) => obj is BTExprNodeRef other && this == other;
		public bool Equals(BTExprNodeRef other) => this == other;

		public BTExprNodeRef(ushort index, byte outputIndex, bool constant)
		{
			this.index = index;
			this.outputIndex = outputIndex;
			this.constant = constant;
		}

		public static BTExprNodeRef Node(ushort index, byte outputIndex) => new BTExprNodeRef(index, outputIndex, false);
		public static BTExprNodeRef Const(ushort offset) => new BTExprNodeRef(offset, 0, true);

		public override int GetHashCode()
		{
			int hashCode = 17;
			hashCode = hashCode * 17 + index.GetHashCode();
			hashCode = hashCode * 17 + outputIndex.GetHashCode();
			hashCode = hashCode * 17 + constant.GetHashCode();
			return hashCode;
		}

		public static bool operator ==(BTExprNodeRef left, BTExprNodeRef right) =>
			left.index == right.index &&
			left.outputIndex == right.outputIndex &&
			left.constant == right.constant;

		public static bool operator !=(BTExprNodeRef left, BTExprNodeRef right) => !(left == right);

		public T Evaluate<T>(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
		{
			if(constant)
			{
				return MemoryMarshal.Cast<byte, T>(data.constData.AsSpan().Slice(index, UnsafeUtility.SizeOf<T>()))[0];
			}

			return data.GetNode(this).Evaluate<T>(ref data, outputIndex, componentPtrs);
		}

		public void Evaluate(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
		{
			if(constant)
			{
				data.constData.AsSpan().Slice(index, result.Length).CopyTo(result);
				return;
			}

			data.GetNode(this).Evaluate(ref data, outputIndex, componentPtrs, result);
		}
	}

	static class BlobExt
	{
		public static ReadOnlySpan<T> AsSpan<T>(ref this BlobArray<T> array) where T : unmanaged
		{
			unsafe
			{
				if(array.Length == 0)
					return default;

				return new ReadOnlySpan<T>(array.GetUnsafePtr(), array.Length);
			}
		}
	}

	public struct BTExec
	{
		public Type type;
		[Tooltip("Index of this node within its parent Sequence")]
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

	public struct UnsafeComponentReference
	{
		public IntPtr data;
		public int length;

		public static UnsafeComponentReference Make<T>(ref T component) where T : unmanaged
		{
			unsafe
			{
				fixed(T* p = &component)
					return new UnsafeComponentReference { data = (IntPtr)p, length = UnsafeUtility.SizeOf<T>() };
			}
		}

		public Span<byte> AsSpan()
		{
			unsafe
			{
				return new Span<byte>(data.ToPointer(), length);
			}
		}
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
		public byte componentIndex;

		public struct Field
		{
			public BTExprNodeRef input;
			public ushort offset;
			public ushort size;
		}

		public BlobArray<Field> fields;

		public void Evaluate(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
		{
			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = componentPtrs[componentIndex].AsSpan().Slice(field.offset, field.size);
				field.input.Evaluate(ref data, componentPtrs, fieldSpan);
			}
		}
	}

	public struct Wait { public BTExprNodeRef until; }
	public struct Fail { }
	public struct Optional { public BTExprNodeRef condition; public BTExecNodeId child; }
	public struct Catch { public BTExecNodeId child; }

	public interface IBTExpr
	{
		void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result);
	}

	public struct BTExpr : IBTExpr
	{
		public Data data;
		public ExprType type;

		public enum ExprType : byte
		{
			ReadField,
			Bool,
			Float3,
		}

		public T Evaluate<T>(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
		{
			Span<T> result = stackalloc T[1];
			Evaluate(ref data, outputIndex, componentPtrs, MemoryMarshal.AsBytes(result));
			return result[0];
		}

		public void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
		{
			switch(type)
			{
				case ExprType.ReadField: this.data.readField.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case ExprType.Bool: this.data.@bool.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case ExprType.Float3: this.data.@float3.Evaluate(ref data, outputIndex, componentPtrs, result); return;
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
			[FieldOffset(0)] public Float3 @float3;
		}

		public struct ReadField : IBTExpr
		{
			public byte componentIndex;
			public BlobArray<Field> fields;

			public struct Field
			{
				public ushort offset;
				public ushort length;

				public static implicit operator Field(System.Reflection.FieldInfo fieldInfo)
				{
					return new Field
					{
						offset = (ushort)UnsafeUtility.GetFieldOffset(fieldInfo),
						length = (ushort)UnsafeUtility.SizeOf(fieldInfo.FieldType),
					};
				}
			}

			public void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				ref var field = ref fields[outputIndex];
				componentPtrs[outputIndex].AsSpan().Slice(field.offset, field.length).CopyTo(result);
			}
		}

		public struct Bool : IBTExpr
		{
			public readonly record struct Not(BTExprNodeRef inner);
			public readonly record struct And(BTExprNodeRef left, BTExprNodeRef right);
			public readonly record struct Or(BTExprNodeRef left, BTExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Not not;
				[FieldOffset(0)] public And and;
				[FieldOffset(0)] public Or or;
			}

			public enum BoolType
			{
				Not,
				And,
				Or,
			}

			public Data data;
			public BoolType index;

			public bool Evaluate(ref BTData btData, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
			{
				switch(index)
				{
					case BoolType.Not: return !data.not.inner.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.And: return data.and.left.Evaluate<bool>(ref btData, componentPtrs) && data.and.right.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.Or: return data.or.left.Evaluate<bool>(ref btData, componentPtrs) || data.or.right.Evaluate<bool>(ref btData, componentPtrs);
				}
#if DEBUG
				Debug.Log($"invalid BTBoolExpr type index {index}");
				throw new Exception();
#else
			return false;
#endif
			}

			public void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				MemoryMarshal.Cast<byte, bool>(result)[0] = Evaluate(ref data, componentPtrs);
			}
		}

		public struct Float3 : IBTExpr
		{
			public readonly record struct Add(BTExprNodeRef left, BTExprNodeRef right);
			public readonly record struct Sub(BTExprNodeRef left, BTExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Add add;
				[FieldOffset(0)] public Sub sub;
			}

			public Data data;
			public Float3Type index;

			public enum Float3Type
			{
				Add,
				Sub,
			}

			public float3 Evaluate(ref BTData btData, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
			{
				switch(index)
				{
					case Float3Type.Add: return data.add.left.Evaluate<float3>(ref btData, componentPtrs) + data.add.right.Evaluate<float3>(ref btData, componentPtrs);
					case Float3Type.Sub: return data.sub.left.Evaluate<float3>(ref btData, componentPtrs) + data.sub.right.Evaluate<float3>(ref btData, componentPtrs);
				}
#if DEBUG
				Debug.Log($"invalid BTBoolExpr type index {index}");
				throw new Exception();
#else
			return false;
#endif
			}

			public void Evaluate(ref BTData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				MemoryMarshal.Cast<byte, float3>(result)[0] = Evaluate(ref data, componentPtrs);
			}
		}
	}

	public struct BTData
	{
		public BlobArray<BTExec> execs;
		public BlobArray<BTExpr> exprs;
		public BlobArray<byte> constData;

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
		public static void Execute(this BlobAssetReference<BTData> asset, ref BehaviorTreeState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<UnsafeComponentReference> componentPtrs, float now, DynamicBuffer<BTExecTrace> trace)
			=> Execute(ref asset.Value, ref state, stack, componentPtrs, now, trace);

		public static void Execute(ref BTData data, ref BehaviorTreeState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<UnsafeComponentReference> componentPtrs, float now, DynamicBuffer<BTExecTrace> trace)
		{
			if(stack.Length == 0)
			{
				if(trace.IsCreated)
					trace.Add(new(data.Root, BTExec.Type.Root, BTExecTrace.Event.Init, stack.Length, -1));

				stack.Add(data.Root);
			}

			bool rootVisited = false;

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
							return;
						}
					}

					stack.Clear();
					stack.Add(data.Root);
				}

				void Return(ref BTData data, ref BTExec node)
				{
					Trace(ref node, BTExecTrace.Event.Return);

					stack.RemoveAt(stack.Length - 1);
				}

				void Call(ref BTData data, BTExecNodeId node)
				{
					Trace1(ref data, BTExecTrace.Event.Call);

					stack.ElementAt(stack.Length - 1).childIndex++;
					stack.Add(node);
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
						if(stack[^1].childIndex < node.data.sequence.children.Length)
						{
							Call(ref data, node.data.sequence.children[stack[^1].childIndex]);
						}
						else
						{
							Return(ref data, ref node);
						}

						break;

					case BTExec.Type.Selector:
						if(stack[^1].childIndex == 0)
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
						if(node.data.wait.until.Evaluate<bool>(ref data, componentPtrs))
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
						if(stack[^1].childIndex == 0 && node.data.optional.condition.Evaluate<bool>(ref data, componentPtrs))
						{
							Call(ref data, node.data.optional.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					case BTExec.Type.Catch:
						if(stack[^1].childIndex == 0)
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

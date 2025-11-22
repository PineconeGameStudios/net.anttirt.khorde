using Mpr.AI.BT.Nodes;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEngine.Pool;
using static Mpr.AI.BT.BTExec;

namespace Mpr.AI.BT
{
	public static class BehaviorTreeAuthoringExt
	{
		public static void SetData(ref this BTExec self, in Root value) { self.type = Type.Root; self.data.root = value; }
		public static void SetSequence(ref this BTExec self, ref BlobBuilder builder, BlobBuilderArray<BTExec> execs, params ushort[] childNodeIds)
		{
			self.type = Type.Sequence;
			var array = builder.Allocate(ref self.data.sequence.children, childNodeIds.Length);
			for(int i = 0; i < childNodeIds.Length; i++)
			{
				array[i] = new BTExecNodeId(childNodeIds[i]);
			}
		}
		public static void SetSelector(ref this BTExec self, ref BlobBuilder builder, BlobBuilderArray<BTExec> execs, params (ushort, BTExprNodeRef)[] childNodeIds)
		{
			self.type = Type.Selector;
			var array = builder.Allocate(ref self.data.selector.children, childNodeIds.Length);
			for(int i = 0; i < childNodeIds.Length; i++)
			{
				array[i] = new ConditionalBlock { nodeId = new BTExecNodeId(childNodeIds[i].Item1), condition = childNodeIds[i].Item2 };
			}
		}
		public static void SetWriteField(ref this BTExec self, ref BlobBuilder builder, byte componentIndex, params WriteField.Field[] fields)
		{
			self.type = Type.WriteField;
			self.data.writeField.componentIndex = componentIndex;
			var blobFields = builder.Allocate(ref self.data.writeField.fields, fields.Length);
			for(int i = 0; i < fields.Length; ++i)
			{
				blobFields[i] = fields[i];
			}
		}

		public static void SetData(ref this BTExec self, in Wait value) { self.type = Type.Wait; self.data.wait = value; }
		public static void SetData(ref this BTExec self, in Fail value) { self.type = Type.Fail; self.data.fail = value; }
		public static void SetData(ref this BTExec self, in Optional value) { self.type = Type.Optional; self.data.optional = value; }
		public static void SetData(ref this BTExec self, in Catch value) { self.type = Type.Catch; self.data.@catch = value; }

		public static BTExecNodeId GetTargetNodeId(this IPort port, Nodes.BakingContext context)
		{
			BTExecNodeId nodeId = default;
			if(port.isConnected)
			{
				var ports = ListPool<IPort>.Get();
				try
				{
					port.GetConnectedPorts(ports);
					if(ports.Count > 1)
					{
						context.errors.Add($"node {port.GetNode()} port {port} is connected to multiple destinations; use Sequence or Selector for multiple targets insead");
					}

					nodeId = context.execNodeMap[ports[0].GetNode()];
				}
				finally
				{
					ListPool<IPort>.Release(ports);
				}
			}

			return nodeId;
		}

		public static BTExprNodeRef GetExprNodeRef(this IPort port, BakingContext context)
		{
			BTExprNodeRef nodeId = default;
			if(port.isConnected)
			{
				var ports = ListPool<IPort>.Get();
				try
				{
					port.GetConnectedPorts(ports);
					if(ports.Count > 1)
					{
						context.errors.Add($"node {port.GetNode()} port {port} is connected to multiple sources");
					}

					var sourceNode = ports[0].GetNode();
					nodeId = context.exprNodeMap[sourceNode];

					for(int i = 0; i < sourceNode.outputPortCount; ++i)
					{
						if(sourceNode.GetOutputPort(i) == ports[0])
						{
							return BTExprNodeRef.Node(nodeId.index, (byte)i);
						}
					}

					return default;
				}
				finally
				{
					ListPool<IPort>.Release(ports);
				}
			}
			else if(port.TryGetValue(out var value))
			{
				// TODO: fold multiple copies of the same constant
				ushort offset = WriteConstant(value, context.constStorage);
				return BTExprNodeRef.Const(offset);
			}
			else
			{
				throw new System.Exception("unassigned port and couldn't get constant value");
			}
		}

		public static ushort WriteConstantImpl<T>(T value, NativeList<byte> storage) where T : unmanaged
		{
			int align = UnsafeUtility.AlignOf<T>();
			int size = UnsafeUtility.SizeOf<T>();
			int rem = storage.Length % align;
			int offset = storage.Length;
			if(rem != 0)
				offset += align - rem;

			if(offset > ushort.MaxValue)
				throw new System.Exception("too many constants, max 65536 bytes storage");

			storage.ResizeUninitialized(offset + size);

			unsafe
			{
				byte* src = (byte*)&value;
				byte* dst = storage.GetUnsafePtr() + offset;
				UnsafeUtility.MemCpy(dst, src, size);
			}

			return (ushort)offset;
		}

		public static ushort WriteConstant(object value, NativeList<byte> storage)
		{
			if(value is bool @bool)
				return WriteConstantImpl(@bool, storage);
			else if(value is byte @byte)
				return WriteConstantImpl(@byte, storage);
			else if(value is short @short)
				return WriteConstantImpl(@short, storage);
			else if(value is ushort @ushort)
				return WriteConstantImpl(@ushort, storage);
			else if(value is int @int)
				return WriteConstantImpl(@int, storage);
			else if(value is uint @uint)
				return WriteConstantImpl(@uint, storage);
			else if(value is long @long)
				return WriteConstantImpl(@long, storage);
			else if(value is ulong @ulong)
				return WriteConstantImpl(@ulong, storage);
			else if(value is float @float)
				return WriteConstantImpl(@float, storage);
			else if(value is float2 @float2)
				return WriteConstantImpl(@float2, storage);
			else if(value is float3 @float3)
				return WriteConstantImpl(@float3, storage);
			else if(value is float4 @float4)
				return WriteConstantImpl(@float4, storage);
			else if(value is double @double)
				return WriteConstantImpl(@double, storage);
			else if(value is double2 @double2)
				return WriteConstantImpl(@double2, storage);
			else if(value is double3 @double3)
				return WriteConstantImpl(@double3, storage);
			else if(value is double4 @double4)
				return WriteConstantImpl(@double4, storage);
			throw new System.Exception($"unsupported constant type {value.GetType().Name}");
		}

		public static void WriteConstStorage(ref BlobBuilder builder, ref BTData data, NativeList<byte> constStorage)
		{
			unsafe
			{
				UnsafeUtility.MemCpy(
					builder.Allocate(ref data.constData, constStorage.Length).GetUnsafePtr(),
					constStorage.GetUnsafePtr(),
					constStorage.Length
					);
			}
		}

	}
}

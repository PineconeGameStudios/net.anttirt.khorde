using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
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

		public static ushort WriteConstantImpl<T>(T value, NativeList<byte> constStorage) where T : unmanaged
		{
			int align = UnsafeUtility.AlignOf<T>();
			int size = UnsafeUtility.SizeOf<T>();
			int rem = constStorage.Length % align;
			int offset = constStorage.Length;
			if(rem != 0)
				offset += align - rem;

			if(offset > ushort.MaxValue)
				throw new System.Exception("too many constants, max 65536 bytes storage");

			constStorage.ResizeUninitialized(offset + size);

			unsafe
			{
				byte* src = (byte*)&value;
				byte* dst = constStorage.GetUnsafePtr() + offset;
				UnsafeUtility.MemCpy(dst, src, size);
			}

			return (ushort)offset;
		}

		public static void BakeConstStorage(ref BlobBuilder builder, ref BTData data, NativeList<byte> constStorage)
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

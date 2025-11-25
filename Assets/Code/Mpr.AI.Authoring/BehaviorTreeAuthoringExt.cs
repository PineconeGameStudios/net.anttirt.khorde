using System.Collections.Generic;
using System.Reflection;
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

		delegate ushort WriteConstantDelegate(object objectValue, out byte length, NativeList<byte> constStorage);

		static Dictionary<System.Type, WriteConstantDelegate> writeConstantMethodCache = new();

		/// <summary>
		/// Write a boxed constant value to constant storage, returning the offset and length. The content of the boxed value must have an unmanaged type.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <returns></returns>
		/// <exception cref="System.InvalidOperationException"></exception>
		public static ushort WriteConstant(object value, out byte length, NativeList<byte> constStorage)
		{
			var type = value.GetType();

			if(!writeConstantMethodCache.TryGetValue(type, out var impl))
			{
				if(!UnsafeUtility.IsUnmanaged(type))
					throw new System.InvalidOperationException($"Attempt to write constant of managed type '{type}', only unmanaged types are allowed");

				impl = (WriteConstantDelegate)typeof(BehaviorTreeAuthoringExt)
					.GetMethod(nameof(WriteConstantTrampoline), BindingFlags.Static | BindingFlags.NonPublic)
					.MakeGenericMethod(type)
					.CreateDelegate(typeof(WriteConstantDelegate));
				writeConstantMethodCache[type] = impl;
			}

			return impl(value, out length, constStorage);
		}

		static ushort WriteConstantTrampoline<T>(object objectValue, out byte length, NativeList<byte> constStorage) where T : unmanaged
		{
			T value = (T)objectValue;
			return WriteConstant(value, out length, constStorage);
		}

		/// <summary>
		/// Write a value to constant storage, returning the offset and length.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ushort WriteConstant<T>(T value, out byte length, NativeList<byte> constStorage) where T : unmanaged
		{
			int align = UnsafeUtility.AlignOf<T>();
			int size = UnsafeUtility.SizeOf<T>();
			if(size > byte.MaxValue)
				throw new System.Exception("max constant size 255 bytes");

			length = (byte)size;

			int rem = constStorage.Length % align;
			int offset = constStorage.Length;
			if(rem != 0)
				offset += align - rem;

			if(offset > ushort.MaxValue)
				throw new System.Exception("too many constants, max 65535 bytes storage");

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

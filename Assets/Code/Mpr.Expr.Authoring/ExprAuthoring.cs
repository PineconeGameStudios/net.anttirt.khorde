using Mpr.Expr;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr.Authoring
{
	public static class ExprAuthoring
	{
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

				impl = (WriteConstantDelegate)typeof(ExprAuthoring)
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

		public static void BakeConstStorage(ref BlobBuilder builder, ref ExprData exprData, NativeList<byte> constStorage)
		{
			unsafe
			{
				UnsafeUtility.MemCpy(
					builder.Allocate(ref exprData.constData, constStorage.Length).GetUnsafePtr(),
					constStorage.GetUnsafePtr(),
					constStorage.Length
					);
			}
		}
	}
}

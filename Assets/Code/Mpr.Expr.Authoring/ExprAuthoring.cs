using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr.Authoring
{
	/// <summary>
	/// Reference to storage for a single expression. Pass this to <see cref="ExpressionBakingContext.Allocate"/>
	/// </summary>
	public unsafe ref struct ExpressionStorageRef
	{
		public ExpressionStorage* storage;
		public ulong* typeHash;

		public ExpressionStorageRef(ref ExpressionStorage storage, ref ulong typeHash)
		{
			fixed(ExpressionStorage* ptr = &storage)
				this.storage = ptr;
			fixed(ulong* ptr = &typeHash)
				this.typeHash = ptr;
		}
	}
	
	public static class ExprAuthoring
	{
		delegate ushort WriteConstantDelegate(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache);

		static Dictionary<System.Type, WriteConstantDelegate> writeConstantMethodCache = new();

		public const ushort MaxConstantSize = 0x7fff;

		public static ExprNodeRef WriteConstant(object value, NativeList<byte> constStorage,
			Dictionary<object, (ushort offset, ushort length)> cache = null)
		{
			ushort offset = WriteConstant(value, out var length, constStorage, cache);
			return ExprNodeRef.Const(offset, (byte)length);
		}
		
		public static ExpressionRef WriteConstant2(object value, NativeList<byte> constStorage,
			Dictionary<object, (ushort offset, ushort length)> cache = null)
		{
			ushort offset = WriteConstant(value, out var length, constStorage, cache);
			return ExpressionRef.Const(offset, length);
		}
		
		/// <summary>
		/// Write a boxed constant value to constant storage, returning the offset and length. The content of the boxed value must have an unmanaged type.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.InvalidOperationException"></exception>
		public static ushort WriteConstant(object value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null)
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

			return impl(value, out length, constStorage, cache);
		}

		static ushort WriteConstantTrampoline<T>(object objectValue, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			T value = (T)objectValue;
			return WriteConstant(value, out length, constStorage, cache);
		}

		/// <summary>
		/// Write a value to constant storage, returning an <see cref="ExprNodeRef"/> pointing to the constant.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ExprNodeRef WriteConstant<T>(T value, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			var offset = WriteConstant<T>(value, out var length, constStorage, cache);
			return ExprNodeRef.Const(offset, (byte)length);
		}

		/// <summary>
		/// Write a value to constant storage, returning an <see cref="ExpressionRef"/> pointing to the constant.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ExpressionRef WriteConstant2<T>(T value, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			var offset = WriteConstant<T>(value, out var length, constStorage, cache);
			return ExpressionRef.Const(offset, length);
		}

		/// <summary>
		/// Write a value to constant storage, returning the offset and length.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="length"></param>
		/// <param name="constStorage"></param>
		/// <param name="cache">Value cache for constant value deduplication</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public static ushort WriteConstant<T>(T value, out ushort length, NativeList<byte> constStorage, Dictionary<object, (ushort offset, ushort length)> cache = null) where T : unmanaged
		{
			if (cache != null)
			{
				if (cache.TryGetValue(value, out var result))
				{
					length = result.length;
					return result.offset;
				}
			}
			
			int align = UnsafeUtility.AlignOf<T>();
			int size = UnsafeUtility.SizeOf<T>();
			if(size > MaxConstantSize)
				throw new System.Exception("max constant size 32767 bytes");

			length = (ushort)size;

			int rem = constStorage.Length % align;
			int offset = constStorage.Length;
			if(rem != 0)
				offset += align - rem;

			if(offset + size > ushort.MaxValue)
				throw new System.Exception("too many constants, max 65535 bytes storage");

			constStorage.ResizeUninitialized(offset + size);

			unsafe
			{
				byte* src = (byte*)&value;
				byte* dst = constStorage.GetUnsafePtr() + offset;
				UnsafeUtility.MemCpy(dst, src, size);
			}

			if (cache != null)
			{
				cache[value] = ((ushort)offset, length);
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
		
		public static void BakeConstStorage(ref BlobBuilder builder, ref BlobExpressionData exprData, NativeList<byte> constStorage)
		{
			unsafe
			{
				UnsafeUtility.MemCpy(
					builder.Allocate(ref exprData.constants, constStorage.Length).GetUnsafePtr(),
					constStorage.GetUnsafePtr(),
					constStorage.Length
				);
			}
		}
		
		/// <summary>
		/// Allocate storage for an expression and record its type.
		/// </summary>
		/// <param name="builder">The blob builder being used for the current expression graph</param>
		/// <param name="storage">Reference to the storage slot for the expression being currently baked</param>
		/// <param name="hashCache">Cache of computed type hashes</param>
		/// <typeparam name="TExpression"></typeparam>
		/// <returns></returns>
		public static unsafe ref TExpression Allocate<TExpression>(ref BlobBuilder builder, ExpressionStorageRef storage, Dictionary<Type, ulong> hashCache) where TExpression : unmanaged, IExpressionBase
		{
			*storage.typeHash = ExpressionTypeManager.GetTypeHash<TExpression>(hashCache);
			if (UnsafeUtility.SizeOf<TExpression>() <= UnsafeUtility.SizeOf<ExpressionStorage>())
			{
				return ref *(TExpression*)storage.storage;
			}
			else
			{
				ref var blobPtr = ref storage.storage->GetDataReference<TExpression>();
				return ref builder.Allocate(ref blobPtr);
			}
		}
	}
}

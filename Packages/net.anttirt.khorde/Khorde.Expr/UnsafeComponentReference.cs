using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Khorde.Expr
{
	public struct UnsafeComponentReference
	{
		public IntPtr data;
		public int typeSize;
		public TypeIndex typeIndex;
		public ulong stableTypeHash;

		public static UnsafeComponentReference Make<T>(ref T component) where T : unmanaged
		{
			unsafe
			{
				fixed(T* p = &component)
					return new UnsafeComponentReference
					{
						data = (IntPtr)p,
						typeSize = UnsafeUtility.SizeOf<T>(),
						typeIndex = TypeManager.GetTypeIndex<T>(),
						stableTypeHash = TypeManager.GetTypeInfo<T>().StableTypeHash,
					};
			}
		}

		public Span<byte> AsSpan()
		{
			unsafe
			{
				return new Span<byte>(data.ToPointer(), typeSize);
			}
		}

		public NativeArray<byte> AsNativeArray()
		{
			unsafe
			{
				var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(data.ToPointer(), typeSize, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return result;
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		void CheckFieldBounds(int offset, int length)
		{
			unchecked
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));
            
				if(offset + length < offset)
					throw new ArgumentOutOfRangeException(nameof(length));
			}
        
			if (typeSize < offset + length)
				throw new IndexOutOfRangeException();
		}
		
		public NativeArray<byte> AsNativeArray(int offset, int length)
		{
			CheckFieldBounds(offset, length);
			
			unsafe
			{
				var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
					(byte*)data.ToPointer() + offset, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return result;
			}
		}
	}
}

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr
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

		public NativeSlice<byte> AsNativeSlice()
		{
			unsafe
			{
				return NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(data.ToPointer(), 1, typeSize);
			}
		}
	}
}

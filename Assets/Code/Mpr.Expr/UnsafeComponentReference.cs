using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr
{
	public struct UnsafeComponentReference
	{
		public IntPtr data;
		public int length;
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
						length = UnsafeUtility.SizeOf<T>(),
						typeIndex = TypeManager.GetTypeIndex<T>(),
						stableTypeHash = TypeManager.GetTypeInfo<T>().StableTypeHash,
					};
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
}

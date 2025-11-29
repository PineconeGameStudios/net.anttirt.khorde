using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Mpr.Burst
{
	/// <summary>
	/// Burst-compatible equivalents to <see cref="System.Runtime.InteropServices.MemoryMarshal"/>
	/// </summary>
	public static class SpanMarshal
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<byte> AsBytes<T>(this Span<T> span) where T : unmanaged => Cast<T, byte>(span);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<U> Cast<T, U>(this Span<T> span) where T : unmanaged where U : unmanaged
		{
			unsafe
			{
				// src size in bytes
				int srcSize = span.Length * UnsafeUtility.SizeOf<T>();

				// size per dst element
				int dstElemSize = UnsafeUtility.SizeOf<U>();

				// length of resulting span in dst elements
				int dstLength = srcSize / dstElemSize;

				// expected dst size in bytes
				int dstSize = dstLength * dstElemSize;

				if(dstSize != srcSize)
					throw new Exception("can't cast non-coprime sizes; would have leftover data");

				fixed(T* src = span)
				{
					return new Span<U>(src, dstLength);
				}
			}
		}
	}
}

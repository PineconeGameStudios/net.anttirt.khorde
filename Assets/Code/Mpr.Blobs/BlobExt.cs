using System;
using Unity.Entities;

namespace Mpr.Blobs
{
	public static class BlobExt
	{
		public static Span<T> AsSpan<T>(ref this BlobArray<T> array) where T : unmanaged
		{
			unsafe
			{
				if(array.Length == 0)
					return default;

				return new Span<T>(array.GetUnsafePtr(), array.Length);
			}
		}
	}
}

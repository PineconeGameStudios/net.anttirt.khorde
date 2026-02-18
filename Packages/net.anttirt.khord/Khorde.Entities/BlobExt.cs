using System;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Khorde.Blobs
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

		public static TextAsset CreateTextAsset<T>(ref this BlobBuilder builder) where T : unmanaged
		{
			var writer = new MemoryBinaryWriter();
			
			BlobAssetReference<BlobEntityQueryDesc>.Write(writer, builder, 0);
	
			ReadOnlySpan<byte> bytes;

			unsafe
			{
				bytes = new ReadOnlySpan<byte>(writer.Data, writer.Length);
			}
	
			return new TextAsset(bytes);
		}
	}
}

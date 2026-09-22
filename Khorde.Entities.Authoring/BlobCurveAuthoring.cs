using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Khorde.Blobs.Authoring
{
	public static class BlobCurveAuthoring
	{
		/// <summary>
		/// Bake an animation curve into a BlobAsset that can be referenced
		/// from components and evaluated in Burst code.
		/// </summary>
		/// <param name="curve"></param>
		/// <param name="allocator"></param>
		/// <returns></returns>
		public static BlobAssetReference<BlobCurve> Bake(this AnimationCurve curve, IBaker baker, Allocator allocator = Allocator.Persistent)
		{
			var builder = new BlobBuilder(Allocator.Temp);

			ref var curveBlob = ref builder.ConstructRoot<BlobCurve>();

			curve.ConstructBlob(ref builder, ref curveBlob);

			var reference = builder.CreateBlobAssetReference<BlobCurve>(allocator);

			baker.AddBlobAsset(ref reference, out _);

			builder.Dispose();

			return reference;
		}

	}
}
using Unity.Collections;
using Unity.Entities;

namespace Khorde.Blobs
{
	/// <summary>
	/// Describes an entity query in a format that can be serialized in a blob assets and loaded at runtime.
	/// </summary>
	public struct BlobEntityQueryDesc
	{
		public const int SchemaVersion = 1;

		public BlobArray<BlobComponentType> all;
		public BlobArray<BlobComponentType> any;
		public BlobArray<BlobComponentType> none;
		public BlobArray<BlobComponentType> disabled;
		public BlobArray<BlobComponentType> absent;
		public BlobArray<BlobComponentType> present;
		public EntityQueryOptions pendingOptions;

		NativeList<ComponentType> GetComponentArray(ref BlobArray<BlobComponentType> src, AllocatorManager.AllocatorHandle allocator)
		{
			var dst = new NativeList<ComponentType>(src.Length, allocator);

			for(int i = 0; i < src.Length; i++)
			{
				var ctype = src[i].ResolveComponentType();
				if(ctype.TypeIndex == TypeIndex.Null)
					UnityEngine.Debug.LogError($"failed to resolve stable type hash {src[i].stableTypeHash} to component type");
				dst.Add(ctype);
			}

			return dst;
		}

		public BlobAssetReference<BlobEntityQueryDesc> Instantiate(Allocator allocator)
		{
			var bb = new BlobBuilder(Allocator.Temp);
			ref var result = ref bb.ConstructRoot<BlobEntityQueryDesc>();

			Copy(bb, ref result.all, ref all);
			Copy(bb, ref result.any, ref any);
			Copy(bb, ref result.none, ref none);
			Copy(bb, ref result.disabled, ref disabled);
			Copy(bb, ref result.absent, ref absent);
			Copy(bb, ref result.present, ref present);

			result.pendingOptions = pendingOptions;

			return bb.CreateBlobAssetReference<BlobEntityQueryDesc>(allocator);

			static void Copy(BlobBuilder bb, ref BlobArray<BlobComponentType> dst, ref BlobArray<BlobComponentType> src)
			{
				var dstR = bb.Allocate(ref dst, src.Length);
				for(int i = 0; i < src.Length; i++)
					dstR[i] = src[i];
			}
		}

		public EntityQuery CreateQuery(EntityManager entityManager)
		{
			var eqb = new EntityQueryBuilder(Allocator.Temp);

			var allC = GetComponentArray(ref all, Allocator.Temp);
			eqb.WithAll(ref allC);
			allC.Dispose();

			var anyC = GetComponentArray(ref any, Allocator.Temp);
			eqb.WithAll(ref anyC);
			anyC.Dispose();

			var noneC = GetComponentArray(ref none, Allocator.Temp);
			eqb.WithAll(ref noneC);
			noneC.Dispose();

			var disabledC = GetComponentArray(ref disabled, Allocator.Temp);
			eqb.WithAll(ref disabledC);
			disabledC.Dispose();

			var absentC = GetComponentArray(ref absent, Allocator.Temp);
			eqb.WithAll(ref absentC);
			absentC.Dispose();

			var presentC = GetComponentArray(ref present, Allocator.Temp);
			eqb.WithAll(ref presentC);
			presentC.Dispose();

			eqb.WithOptions(pendingOptions);

			return eqb.Build(entityManager);
		}
	}
}
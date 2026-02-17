using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;

namespace Mpr.Blobs
{
    public class EntityQueryAsset : BlobAsset<BlobEntityQueryDesc>
    {
        /// <summary>
        /// Create an entity query from this asset
        /// </summary>
        /// <param name="entityManager"></param>
        /// <returns></returns>
        public EntityQuery CreateEntityQuery(EntityManager entityManager)
        {
            return GetValue(BlobEntityQueryDesc.SchemaVersion).CreateQuery(entityManager);
        }
    }
    
    public static class EntityQueryAssetExt
    {
        public static ref BlobEntityQueryDesc GetValue(ref this WeakObjectReference<EntityQueryAsset> asset)
            => ref asset.GetValue<BlobEntityQueryDesc, EntityQueryAsset>(BlobEntityQueryDesc.SchemaVersion);

        public static ref BlobEntityQueryDesc GetValue(this UnityObjectRef<EntityQueryAsset> asset)
            => ref asset.GetValue<BlobEntityQueryDesc, EntityQueryAsset>(BlobEntityQueryDesc.SchemaVersion);
    }
}
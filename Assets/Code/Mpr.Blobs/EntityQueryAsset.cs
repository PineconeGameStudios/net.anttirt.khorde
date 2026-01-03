using Unity.Entities;

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
}
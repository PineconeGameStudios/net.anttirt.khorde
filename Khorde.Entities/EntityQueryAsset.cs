using Unity.Entities;
using Unity.Entities.Content;

namespace Khorde.Blobs
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

#if UNITY_EDITOR
        private void OnEnable()
        {
            var icon = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Packages/net.anttirt.khorde/Icons/EntityQuery.psd");
            if(icon != null)
                UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
        }
#endif
    }
    
    public static class EntityQueryAssetExt
    {
        public static ref BlobEntityQueryDesc GetValue(ref this WeakObjectReference<EntityQueryAsset> asset)
            => ref asset.GetValue<BlobEntityQueryDesc, EntityQueryAsset>(BlobEntityQueryDesc.SchemaVersion);

        public static ref BlobEntityQueryDesc GetValue(this UnityObjectRef<EntityQueryAsset> asset)
            => ref asset.GetValue<BlobEntityQueryDesc, EntityQueryAsset>(BlobEntityQueryDesc.SchemaVersion);
    }
}
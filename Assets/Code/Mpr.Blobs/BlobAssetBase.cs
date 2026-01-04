using Unity.Collections;
using UnityEngine;

namespace Mpr.Blobs
{
    public abstract class BlobAssetBase : ScriptableObject
    {
        [SerializeField] protected TextAsset data;

        public bool TryGetData(out NativeArray<byte> result)
        {
            if (data != null)
            {
                result = data.GetData<byte>();
                return true;
            }

            result = default;
            return false;
        }
        
        // Serialized blobs have a 4-byte version and 32-byte header before the payload.
        // To ensure the in-memory asset data is aligned properly, we'll add padding
        // before the serialized blob. This assumes that loaded TextAsset data is aligned
        // to some reasonable value.
        protected const int BlobAlignment = 16;
        protected const int BlobVersionSize = 4;
        public const int PaddingSize = BlobAlignment - BlobVersionSize;
        protected const int BlobHeaderSize = 32;
        
        // Actual layout of the TextAsset byte array
        protected const int PayloadOffset =
            PaddingSize +
            BlobVersionSize +
            BlobHeaderSize;
    }
}
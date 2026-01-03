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
    }
}
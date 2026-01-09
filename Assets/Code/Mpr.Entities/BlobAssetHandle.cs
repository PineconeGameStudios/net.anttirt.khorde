using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Mpr.Entities
{
    public struct BlobAssetHandle<T> : IDisposable, IEquatable<BlobAssetHandle<T>>
        where T : unmanaged
    {
        private BlobAssetReference<T> m_Asset;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        public static bool TryRead<U>(U binaryReader, int version, out BlobAssetHandle<T> result)
            where U : BinaryReader
        {
            if (BlobAssetReference<T>.TryRead<U>(binaryReader, version, out var resultReference))
            {
                result = new BlobAssetHandle<T>
                {
                    m_Asset = resultReference,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = CollectionHelper.CreateSafetyHandle(Allocator.Persistent),
#endif
                };
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Interpret data in-place from an array into a blob asset handle.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="version"></param>
        /// <param name="result"></param>
        /// <param name="numBytesRead"></param>
        /// <returns></returns>
        /// <remarks>Inherits the safety handle of the array.</remarks>
        public static unsafe bool TryReadInplace(NativeArray<byte> data, int version,
            out BlobAssetHandle<T> result,
            out int numBytesRead)
        {
            result = default;
            if (BlobAssetReferenceExt.TryReadInplace((byte*)data.GetUnsafePtr(), data.Length, version, out result.m_Asset,
                    out numBytesRead))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                result.m_Safety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(data);
#endif
                return true;
            }

            return false;
        }

        public bool IsCreated => m_Asset.IsCreated;

        public ref T ValueRO
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return ref m_Asset.Value;
            }
        }

        public ref T ValueRW
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var s = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.UseSecondaryVersion(ref s);
                return ref m_Asset.Value;
            }
        }

        /// <summary>
        /// Dispose the blob asset. Call this if the handle was created through <see cref="TryRead"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The handle was created through <see cref="TryReadInplace"/></exception>
        public void Dispose()
        {
            m_Asset.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
        }

        public bool Equals(BlobAssetHandle<T> other) => m_Asset.Equals(other.m_Asset);
    }
}
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Mpr.Blobs
{
    /// <summary>
    /// Asset type wrapping a blob asset of type T. Derive with a concrete blob type to use.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BlobAsset<T> : BlobAssetBase where T : unmanaged
    {
#if UNITY_EDITOR
        /// <summary>
        /// Set asset data. Only available in the editor.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="version"></param>
        public TextAsset SetAssetData(BlobBuilder builder, int version)
        {
            data = new(SerializeTempBytes(builder, version));
            return data;
        }
#endif

        static unsafe ReadOnlySpan<byte> SerializeTempBytes(BlobBuilder builder, int version)
        {
            var writer = new MemoryBinaryWriter();
            writer.Write(new byte[PaddingSize]);
            BlobAssetReference<T>.Write(writer, builder, version);
            return new ReadOnlySpan<byte>(writer.Data, writer.Length);
        }

        public unsafe Hash128 GetHash128()
        {
            var rawBytes = data.GetData<byte>();

            Hash128 hash = default;
            if(rawBytes.Length > PayloadOffset)
                hash.Value.x = math.hash((byte*)rawBytes.GetUnsafeReadOnlyPtr() + PayloadOffset, rawBytes.Length - PayloadOffset);
            
            return hash;
        }

        /// <summary>
        /// Load data from the asset to a <see cref="BlobAssetReference{T}"/> backed by <see cref="Allocator.Persistent"/>. The asset reference must be disposed of later to release the memory
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public BlobAssetReference<T> LoadPersistent(int version)
        {
            var rawBytes = data.GetData<byte>();

            if (rawBytes.Length > PaddingSize)
            {
                unsafe
                {
                    var reader = new MemoryBinaryReader((byte*)rawBytes.GetUnsafePtr() + PaddingSize,
                        rawBytes.Length - PaddingSize);
                    if (BlobAssetReference<T>.TryRead(reader, version, out var result))
                        return result;
                }
            }

            throw new InvalidOperationException("invalid blob data");
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public unsafe ref T GetValue(int version)
        {
            NativeArray<byte> rawBytes = data.GetData<byte>();

            if (BlobAssetReferenceExt.TryReadInplace<T>((byte*)rawBytes.GetUnsafePtr(), rawBytes.Length, version,
                    out var loaded, out _))
            {
                return ref loaded.Value;
            }

            throw new InvalidOperationException("invalid blob data");
        }
    }

    public static class BlobAssetExt
    {
        unsafe delegate bool GetDataReferenceManagedDelegate(int instanceId, NativeArray<byte>* result);

        [AOT.MonoPInvokeCallback(typeof(GetDataReferenceManagedDelegate))]
        static unsafe bool GetDataReferenceManaged(int instanceId, NativeArray<byte>* result)
        {
            // NOTE: disable warning until UnityObjectRef<T> changes to EntityId instead of int instanceId
#pragma warning disable CS0618 // Type or member is obsolete
            var asset = Resources.InstanceIDToObject(instanceId) as BlobAssetBase;
#pragma warning restore CS0618 // Type or member is obsolete
            if (asset == null)
                return false;

            return asset.TryGetData(out *result);
        }

        unsafe delegate bool GetWeakDataReferenceManagedDelegate(UntypedWeakReferenceId* weakReferenceId,
            NativeArray<byte>* result);

        [AOT.MonoPInvokeCallback(typeof(GetWeakDataReferenceManagedDelegate))]
        static unsafe bool GetWeakDataReferenceManaged(UntypedWeakReferenceId* weakReferenceId,
            NativeArray<byte>* result)
        {
            var asset = RuntimeContentManager.GetObjectValue<BlobAssetBase>(*weakReferenceId);
            if (asset == null)
                return false;

            return asset.TryGetData(out *result);
        }

        struct Pointers
        {
            public IntPtr GetDataReference;
            public IntPtr GetWeakDataReference;
        }

        struct Delegates
        {
            public GetDataReferenceManagedDelegate GetDataReference;
            public GetWeakDataReferenceManagedDelegate GetWeakDataReference;
        }

        private static readonly SharedStatic<Pointers>
            FunctionPointers = SharedStatic<Pointers>.GetOrCreate<Pointers>();

        private static Delegates DelegateHolders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize()
        {
            unsafe
            {
                DelegateHolders.GetDataReference = GetDataReferenceManaged;
                DelegateHolders.GetWeakDataReference = GetWeakDataReferenceManaged;
                FunctionPointers.Data.GetDataReference =
                    Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetDataReference);
                FunctionPointers.Data.GetWeakDataReference =
                    Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetWeakDataReference);
            }
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static unsafe ref T GetValue<T>(ref this WeakObjectReference<BlobAsset<T>> asset, int version)
            where T : unmanaged
        {
            NativeArray<byte> rawBytes = default;

            fixed (UntypedWeakReferenceId* assetId = &asset.Id)
            {
                if (!new FunctionPointer<GetWeakDataReferenceManagedDelegate>(
                            FunctionPointers.Data.GetWeakDataReference)
                        .Invoke(assetId, &rawBytes))
                {
                    throw new InvalidOperationException("asset not loaded");
                }
            }

            if (BlobAssetReferenceExt.TryReadInplace<T>(
                    (byte*)rawBytes.GetUnsafePtr() + BlobAssetBase.PaddingSize,
                    rawBytes.Length - BlobAssetBase.PaddingSize,
                    version, out var loaded, out _))
            {
                return ref loaded.Value;
            }

            throw new InvalidOperationException("invalid blob data");
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static unsafe ref T GetValue<T>(this UnityObjectRef<BlobAsset<T>> asset, int version) where T : unmanaged
        {
            NativeArray<byte> rawBytes = default;

            if (!new FunctionPointer<GetDataReferenceManagedDelegate>(FunctionPointers.Data.GetDataReference).Invoke(
                    *(int*)&asset, &rawBytes))
            {
                throw new InvalidOperationException("asset not loaded");
            }

            if (BlobAssetReferenceExt.TryReadInplace<T>(
                    (byte*)rawBytes.GetUnsafePtr() + BlobAssetBase.PaddingSize,
                    rawBytes.Length - BlobAssetBase.PaddingSize,
                    version, out var loaded, out _))
            {
                return ref loaded.Value;
            }

            throw new InvalidOperationException("invalid blob data");
        }
    }
}
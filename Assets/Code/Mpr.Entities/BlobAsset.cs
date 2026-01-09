using System;
using System.Runtime.InteropServices;
using Mpr.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

#if UNITY_6000_4_OR_NEWER
#error TODO: fix when UnityObjectRef switches to EntityId
using UnityObjectRefId = UnityEngine.EntityId;
#else
using UnityObjectRefId = System.Int32;
#endif

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
            dataHash = ComputeDataHash();
            return data;
        }
#endif

        public void DestroyAsset()
        {
            Destroy(data);
            Destroy(this);
        }

        public void DestroyAssetImmediate()
        {
            DestroyImmediate(data);
            DestroyImmediate(this);
        }

        static unsafe ReadOnlySpan<byte> SerializeTempBytes(BlobBuilder builder, int version)
        {
            var writer = new MemoryBinaryWriter();
            writer.Write(new byte[PaddingSize]);
            BlobAssetReference<T>.Write(writer, builder, version);
            return new ReadOnlySpan<byte>(writer.Data, writer.Length);
        }

        /// <summary>
        /// Load data from the asset to a <see cref="BlobAssetHandle{T}"/> backed by <see cref="Allocator.Persistent"/>. The asset reference must be disposed of later to release the memory
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public BlobAssetHandle<T> LoadPersistent(int version)
        {
            var rawBytes = data.GetData<byte>();

            if (rawBytes.Length > PaddingSize)
            {
                unsafe
                {
                    var reader = new MemoryBinaryReader((byte*)rawBytes.GetUnsafePtr() + PaddingSize,
                        rawBytes.Length - PaddingSize);
                    if (BlobAssetHandle<T>.TryRead(reader, version, out var result))
                        return result;
                }
            }

            throw new InvalidOperationException("invalid blob data");
        }

        /// <summary>
        /// Read the binary data in-place into a handle that can be used to access blob data
        /// </summary>
        /// <param name="version"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryReadInPlace(int version, out BlobAssetHandle<T> result)
        {
            var rawBytes = data.GetData<byte>();
            var payload = rawBytes.GetSubArray(PaddingSize, rawBytes.Length - PaddingSize);

            return BlobAssetHandle<T>.TryReadInplace(payload, version, out result, out _);
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
        unsafe delegate bool GetDataStrongDelegate(UnityObjectRefId objectId, NativeArray<byte>* result);
        unsafe delegate bool GetDataWeakDelegate(UntypedWeakReferenceId* weakReferenceId, NativeArray<byte>* result);
        unsafe delegate bool GetHashStrongDelegate(UnityObjectRefId objectId, Hash128* result);
        unsafe delegate bool GetHashWeakDelegate(UntypedWeakReferenceId* weakReferenceId, Hash128* result);

        [AOT.MonoPInvokeCallback(typeof(GetDataStrongDelegate))]
        static unsafe bool GetDataStrong(UnityObjectRefId objectId, NativeArray<byte>* result)
        {
            // NOTE: disable warning until UnityObjectRef<T> changes to EntityId instead of int instanceId
#pragma warning disable CS0618 // Type or member is obsolete
            var asset = Resources.InstanceIDToObject(objectId) as BlobAssetBase;
#pragma warning restore CS0618 // Type or member is obsolete
            if (asset == null)
                return false;

            return asset.TryGetData(out *result);
        }

        [AOT.MonoPInvokeCallback(typeof(GetDataWeakDelegate))]
        static unsafe bool GetDataWeak(UntypedWeakReferenceId* weakReferenceId,
            NativeArray<byte>* result)
        {
            var asset = RuntimeContentManager.GetObjectValue<BlobAssetBase>(*weakReferenceId);
            if (asset == null)
                return false;

            return asset.TryGetData(out *result);
        }

        
        [AOT.MonoPInvokeCallback(typeof(GetHashStrongDelegate))]
        static unsafe bool GetHashStrong(UnityObjectRefId objectId, Hash128* result)
        {
            // NOTE: disable warning until UnityObjectRef<T> changes to EntityId instead of int instanceId
#pragma warning disable CS0618 // Type or member is obsolete
            var asset = Resources.InstanceIDToObject(objectId) as BlobAssetBase;
#pragma warning restore CS0618 // Type or member is obsolete
            if (asset == null)
                return false;

            *result = asset.DataHash;
            return true;
        }

        [AOT.MonoPInvokeCallback(typeof(GetHashWeakDelegate))]
        static unsafe bool GetHashWeak(UntypedWeakReferenceId* weakReferenceId,
            Hash128* result)
        {
            var asset = RuntimeContentManager.GetObjectValue<BlobAssetBase>(*weakReferenceId);
            if (asset == null)
                return false;

            *result = asset.DataHash;
            return true;
        }

        struct Pointers
        {
            public IntPtr GetDataStrong;
            public IntPtr GetDataWeak;
            public IntPtr GetHashStrong;
            public IntPtr GetHashWeak;
        }

        struct Delegates
        {
            public GetDataStrongDelegate GetDataStrong;
            public GetDataWeakDelegate GetDataWeak;
            public GetHashStrongDelegate GetHashStrong;
            public GetHashWeakDelegate GetHashWeak;
        }

        private struct Ctx1 {}
        private static readonly SharedStatic<Pointers>
            FunctionPointers = SharedStatic<Pointers>.GetOrCreate<Pointers, Ctx1>();

        private static Delegates DelegateHolders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize()
        {
            unsafe
            {
                DelegateHolders.GetDataStrong = GetDataStrong;
                DelegateHolders.GetDataWeak = GetDataWeak;
                DelegateHolders.GetHashStrong = GetHashStrong;
                DelegateHolders.GetHashWeak = GetHashWeak;
                FunctionPointers.Data.GetDataStrong = Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetDataStrong);
                FunctionPointers.Data.GetDataWeak = Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetDataWeak);
                FunctionPointers.Data.GetHashStrong = Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetHashStrong);
                FunctionPointers.Data.GetHashWeak = Marshal.GetFunctionPointerForDelegate(DelegateHolders.GetHashWeak);
            }
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="TBlob">The contained blob type</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static ref TBlob GetValue<TBlob>(ref this WeakObjectReference<BlobAsset<TBlob>> asset, int version)
            where TBlob : unmanaged
        {
            return ref GetValue<TBlob, BlobAsset<TBlob>>(ref asset, version);
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="TBlob">The contained blob type</typeparam>
        /// <typeparam name="TAsset">The actual asset type</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static unsafe ref TBlob GetValue<TBlob, TAsset>(ref this WeakObjectReference<TAsset> asset, int version)
            where TBlob : unmanaged
            where TAsset : BlobAsset<TBlob>
        {
            NativeArray<byte> rawBytes = default;

            fixed (UntypedWeakReferenceId* assetId = &asset.Id)
            {
                if (!new FunctionPointer<GetDataWeakDelegate>(
                            FunctionPointers.Data.GetDataWeak)
                        .Invoke(assetId, &rawBytes))
                {
                    throw new InvalidOperationException("asset not loaded");
                }
            }

            if (BlobAssetReferenceExt.TryReadInplace<TBlob>(
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
        /// <typeparam name="TBlob">The contained blob type</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static ref TBlob GetValue<TBlob>(this UnityObjectRef<BlobAsset<TBlob>> asset, int version) where TBlob : unmanaged
        {
            return ref GetValue<TBlob, BlobAsset<TBlob>>(asset, version);
        }

        /// <summary>
        /// Get a direct reference to data from the asset. The reference will be invalidated when the asset is unloaded / destroyed.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="TBlob">The contained blob type</typeparam>
        /// <typeparam name="TAsset">The actual asset type</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static unsafe ref TBlob GetValue<TBlob, TAsset>(this UnityObjectRef<TAsset> asset, int version)
            where TBlob : unmanaged where TAsset : BlobAsset<TBlob>
        {
            NativeArray<byte> rawBytes = default;

            if (!new FunctionPointer<GetDataStrongDelegate>(FunctionPointers.Data.GetDataStrong).Invoke(asset.GetObjectId(), &rawBytes))
            {
                throw new InvalidOperationException("asset not loaded");
            }

            if (BlobAssetReferenceExt.TryReadInplace<TBlob>(
                    (byte*)rawBytes.GetUnsafePtr() + BlobAssetBase.PaddingSize,
                    rawBytes.Length - BlobAssetBase.PaddingSize,
                    version, out var loaded, out _))
            {
                return ref loaded.Value;
            }

            throw new InvalidOperationException("invalid blob data");           
        }

        /// <summary>
        /// Get a handle to the data within the blob asset
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="version"></param>
        /// <typeparam name="TBlob"></typeparam>
        /// <typeparam name="TAsset"></typeparam>
        /// <returns></returns>
        public unsafe static BlobAssetHandle<TBlob> GetHandle<TBlob, TAsset>(this UnityObjectRef<TAsset> asset, int version)
            where TBlob : unmanaged where TAsset : BlobAsset<TBlob>
        {
            NativeArray<byte> rawBytes = default;

            if (!new FunctionPointer<GetDataStrongDelegate>(FunctionPointers.Data.GetDataStrong).Invoke(asset.GetObjectId(), &rawBytes))
            {
                return default;
            }

            BlobAssetHandle<TBlob>.TryReadInplace(rawBytes, version, out var result, out _);
            return result;
        }

        /// <summary>
        /// Get the data hash of this <see cref="BlobAsset{T}"/>
        /// </summary>
        /// <param name="asset"></param>
        /// <typeparam name="TAsset"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static unsafe Hash128 GetDataHash<TAsset>(this UnityObjectRef<TAsset> asset)
            where TAsset : BlobAssetBase
        {
            Hash128 result = default;

            if (!new FunctionPointer<GetHashStrongDelegate>(FunctionPointers.Data.GetHashStrong).Invoke(asset.GetObjectId(), &result))
            {
                throw new InvalidOperationException("asset not loaded");
            }

            return result;
        }
        
        /// <summary>
        /// Get the data hash of this <see cref="BlobAsset{T}"/>
        /// </summary>
        /// <param name="asset"></param>
        /// <typeparam name="TAsset"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <remarks>Can be called from Burst-compiled code</remarks>
        public static unsafe Hash128 GetDataHash<TAsset>(ref this WeakObjectReference<TAsset> asset)
            where TAsset : BlobAssetBase
        {
            Hash128 result = default;

            fixed (UntypedWeakReferenceId* assetId = &asset.Id)
            {
                if (!new FunctionPointer<GetHashWeakDelegate>(FunctionPointers.Data.GetHashWeak).Invoke(
                        assetId, &result))
                {
                    throw new InvalidOperationException("asset not loaded");
                }
            }

            return result;
        }
    }
}
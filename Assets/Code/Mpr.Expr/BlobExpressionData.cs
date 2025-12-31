using System;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace Mpr.Expr;

/// <summary>
/// Root structure for storing an expression graph in a blob.
/// </summary>
public struct BlobExpressionData
{
    /// <summary>
    /// Storage for constant-valued expression node references
    /// </summary>
    public BlobArray<byte> constants;

    /// <summary>
    /// Storage for expressions
    /// </summary>
    public BlobArray<ExpressionData> expressions;

    /// <summary>
    /// Used after loading an expression data blob to populate the function pointers in the <see cref="expressions"/> array
    /// </summary>
    public BlobArray<ulong> expressionTypeHashes;

    /// <summary>
    /// Internal pointers to patch strong asset references at runtime
    /// </summary>
    public BlobArray<BlobPtr<UntypedExpressionObjectId>> patchableObjectRefs;

    /// <summary>
    /// Component types (especially non-[ChunkSerializable] ones) might have a
    /// different layout on the target platform so we have to initialize layouts
    /// at runtime
    /// </summary>
    public BlobArray<BlobPtr<ExpressionComponentTypeInfo>> patchableTypeInfos;

    /// <summary>
    /// Used after loading an expression data blob to populate the field offsets
    /// in ExpressionComponentTypeInfo
    /// </summary>
    public BlobArray<ulong> typeInfoTypeHashes;

    /// <summary>
    /// Source graph node ids corresponding to nodes in the <see cref="expressions"/> array. Used for debugging.
    /// </summary>
    public BlobArray<UnityEngine.Hash128> sourceGraphNodeIds;
    
    /// <summary>
    /// Get constants buffer as a NativeSlice
    /// </summary>
    /// <returns></returns>
    public NativeSlice<byte> GetConstants()
    {
        unsafe
        {
            var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(
                constants.GetUnsafePtr(),
                1,
                constants.Length);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.GetTempMemoryHandle());
            #endif
            return slice;
        }
    }

    private bool isRuntimeInitialized;
    private ObjectLoadingStatus loadingStatus;

    /// <summary>
    /// Whether <see cref="RuntimeInitialize"/> has been called on this instance.
    /// </summary>
    public bool IsRuntimeInitialized => isRuntimeInitialized;
        
    /// <summary>
    /// Latest loading status for weakly referenced assets.
    /// </summary>
    public ObjectLoadingStatus LoadingStatus => loadingStatus;
    
    public static FieldInfo[] GetComponentFields<T>() where T : unmanaged, IComponentData
        => typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(UnsafeUtility.GetFieldOffset)
            .ToArray();

    /// <summary>
    /// Initialize expression function pointers, patch strong object refs, start loading weak object refs, etc.
    /// </summary>
    public void RuntimeInitialize(
        DynamicBuffer<BlobExpressionObjectReference> objectReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences
    )
    {
        if (isRuntimeInitialized)
            return;

        isRuntimeInitialized = true;

        for (int i = 0; i < patchableObjectRefs.Length; ++i)
        {
            patchableObjectRefs[i].Value = UntypedExpressionObjectId.FromUnityObjectRef(objectReferences[i].asset);
        }

        if (weakObjectReferences.IsCreated)
        {
            foreach (var weakObjectReference in weakObjectReferences)
            {
                weakObjectReference.asset.LoadAsync();
            }
        }

        UpdateLoadingStatus(objectReferences, weakObjectReferences);
            
        if (expressions.Length != expressionTypeHashes.Length)
        {
            throw new InvalidOperationException("corrupted data: must have the same amount of expressions and expression type hashes");
        }

        for (int i = 0; i < expressions.Length; ++i)
        {
            var typeHash = expressionTypeHashes[i];
            if (ExpressionTypeManager.TryGetEvaluateFunction(typeHash, out var function))
            {
                expressions[i].evaluateFuncPtr = (long)function.Value;
            }
            else
            {
                Debug.LogError($"couldn't find generated type info for hash {typeHash} at index {i}");
                throw new InvalidOperationException("couldn't find generated type info");
            }
        }
    }

    public void UpdateLoadingStatus(DynamicBuffer<BlobExpressionObjectReference> objectReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences)
    {
        if (loadingStatus == ObjectLoadingStatus.Completed)
            return;

        if (weakObjectReferences.IsCreated)
        {
            foreach (var weakObjectReference in weakObjectReferences)
            {
                if (weakObjectReference.asset.LoadingStatus != ObjectLoadingStatus.Completed)
                {
                    loadingStatus = weakObjectReference.asset.LoadingStatus;
                    return;
                }
            }
        }

        loadingStatus = ObjectLoadingStatus.Completed;
    }

    /// <summary>
    /// Release weak asset references, etc.
    /// </summary>
    /// <param name="objectReferences"></param>
    /// <param name="weakObjectReferences"></param>
    public void RuntimeDeinitialize(DynamicBuffer<BlobExpressionObjectReference> objectReferences, DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences)
    {
        if (!isRuntimeInitialized)
            return;

        isRuntimeInitialized = false;

        if (weakObjectReferences.IsCreated)
        {
            foreach (var weakObjectReference in weakObjectReferences)
            {
                weakObjectReference.asset.Release();
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Mpr.Blobs;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
    /// Output definitions for graph types that can be evaluated directly.
    /// </summary>
    public BlobArray<ExpressionOutput> outputs;

    /// <summary>
    /// List of local component types used by this expression graph.
    /// Indices in this array correspond to <see cref="ExpressionComponentTypeInfo.componentIndex"/>
    /// for local components.
    /// </summary>
    public BlobArray<BlobComponentType> localComponents;
    
    /// <summary>
    /// List of local component types used by this expression graph.
    /// Indices in this array correspond to <see cref="ExpressionComponentTypeInfo.componentIndex"/>
    /// for lookup components.
    /// </summary>
    public BlobArray<BlobComponentType> lookupComponents;
    
    /// <summary>
    /// Get constants buffer as a NativeArray
    /// </summary>
    /// <returns></returns>
    public NativeArray<byte> GetConstants()
    {
        unsafe
        {
            var slice = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
                constants.GetUnsafePtr(),
                constants.Length,
                Allocator.None);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.GetTempMemoryHandle());
            #endif
            return slice;
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private void CheckConstantRange(int start, int length)
    {
        unchecked
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            
            if(start + length < start)
                throw new ArgumentOutOfRangeException(nameof(length));
        }
        
        if (constants.Length < start + length)
            throw new IndexOutOfRangeException();
    }

    public unsafe ref readonly TConstant GetConstant<TConstant>(int byteOffset) where TConstant : unmanaged
    {
        CheckConstantRange(byteOffset, sizeof(TConstant));
        return ref *(TConstant*)(byteOffset + (byte*)constants.GetUnsafePtr());
    }

    private bool isRuntimeInitialized;
    private ObjectLoadingStatus loadingStatus;

    /// <summary>
    /// Whether <see cref="RuntimeInitialize"/> has been called on this instance.
    /// </summary>
    public bool IsRuntimeInitialized => isRuntimeInitialized;
        
    public static FieldInfo[] GetComponentFields<T>() where T : unmanaged, IComponentData
        => GetComponentFields(typeof(T));
    
    static FieldInfo[] GetComponentFields(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(UnsafeUtility.GetFieldOffset)
            .ToArray();

    struct ComponentReflectionCache
    {
        public NativeHashMap<ulong, UnsafeList<ExpressionComponentTypeInfo.Field>> Cache;
        public FunctionPointer<ComputeFieldsDelegate> ComputeFields;

        public delegate void ComputeFieldsDelegate(ulong typeHash,
            out UnsafeList<ExpressionComponentTypeInfo.Field> fields);

        [AOT.MonoPInvokeCallback(typeof(ComputeFieldsDelegate))]
        public static void GetFieldsImpl(ulong typeHash, out UnsafeList<ExpressionComponentTypeInfo.Field> fields)
        {
            var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(typeHash);
            if (typeIndex == default)
                throw new InvalidOperationException($"couldn't find type index for StableTypeHash {typeHash}");
            
            var info = TypeManager.GetTypeInfo(typeIndex);
            var reflFields = GetComponentFields(info.Type);
            fields = new UnsafeList<ExpressionComponentTypeInfo.Field>(reflFields.Length, Allocator.Domain);
            foreach (var field in reflFields)
                fields.Add(field);
        }
    }
    
    static readonly SharedStatic<ComponentReflectionCache> Cache
        = SharedStatic<ComponentReflectionCache>.GetOrCreate<ComponentReflectionCache>();

    static readonly ComponentReflectionCache.ComputeFieldsDelegate ComputeFieldsDelegate
        = ComponentReflectionCache.GetFieldsImpl;

    static BlobExpressionData()
    {
        Cache.Data.Cache = new(0, Allocator.Domain);
        Cache.Data.ComputeFields = new(Marshal.GetFunctionPointerForDelegate(ComputeFieldsDelegate));
    }

    static UnsafeList<ExpressionComponentTypeInfo.Field> GetFields(ulong typeHash)
    {
        if (Hint.Unlikely(!Cache.Data.Cache.TryGetValue(typeHash, out var fields)))
        {
            Cache.Data.ComputeFields.Invoke(typeHash, out fields);
            Cache.Data.Cache[typeHash] = fields;
        }
        
        return fields;
    }
    
    /// <summary>
    /// Initialize expression function pointers, patch strong object refs, start loading weak object refs, etc.
    /// </summary>
    public void RuntimeInitialize()
    {
        if (isRuntimeInitialized)
            return;

        isRuntimeInitialized = true;

        if (expressions.Length != expressionTypeHashes.Length)
        {
            throw new InvalidOperationException("corrupted data: must have the same amount of expressions and expression type hashes");
        }

        for (int i = 0; i < expressions.Length; ++i)
        {
            var expressionTypeHash = expressionTypeHashes[i];
            if(expressionTypeHash == 0)
                continue;
            
            if (ExpressionTypeManager.TryGetEvaluateFunction(expressionTypeHash, out var function))
            {
                expressions[i].evaluateFuncPtr = (long)function.Value;
            }
            else
            {
                Debug.LogError($"couldn't find generated type info for hash {expressionTypeHash} at index {i}");
                throw new InvalidOperationException("couldn't find generated type info");
            }
        }

        for (int i = 0; i < patchableTypeInfos.Length; ++i)
        {
            ref var typeInfo = ref patchableTypeInfos[i];
            var componentTypeHash = typeInfoTypeHashes[i];
            var fields = GetFields(componentTypeHash);
            ref var patchedFields = ref typeInfo.Value.fields;
            for(int j = 0; j < fields.Length; ++j)
                patchedFields[j] = fields[j];
        }
    }
}
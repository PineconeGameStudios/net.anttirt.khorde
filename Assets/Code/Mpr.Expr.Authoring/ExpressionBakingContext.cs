using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;

namespace Mpr.Expr.Authoring;

public unsafe class ExpressionBakingContext
{
    private DynamicBuffer<BlobExpressionObjectReference> strongReferences;
    private DynamicBuffer<BlobExpressionWeakObjectReference> weakReferences;
    private Dictionary<UnityEngine.Object, WeakObjectReference<UnityEngine.Object>> weakReferenceSet;
    private NativeList<IntPtr> patchableStrongObjectReferences;
    private NativeList<(ulong stableTypeHash, IntPtr typeReference)> patchableTypeInfos;
    private BlobBuilder builder;
    private BlobExpressionData* data;
    private NativeList<byte> constStorage;
    private Dictionary<Type, ulong> hashCache;
    
    // components accessed directly on the current entity (behavior trees / queries)
    private Dictionary<Type, ComponentType.AccessMode> localComponentsDict;
    private List<ComponentType> localComponents;

    // components looked up on other entities
    private Dictionary<Type, ComponentType.AccessMode> lookupComponentsDict;
    private List<ComponentType> lookupComponents;
    protected NativeArray<ExpressionData> builderExpressions;
    protected NativeArray<ulong> builderTypeHashes;
    protected NativeArray<UnityEngine.Hash128> builderSourceGraphNodeIds;
    private Allocator allocator;

    public enum ComponentLocation
    {
        Local,
        Lookup,
    }

    public ExpressionBakingContext(
        DynamicBuffer<BlobExpressionObjectReference> strongReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakReferences,
        Allocator allocator
    )
    {
        this.strongReferences = strongReferences;
        this.weakReferences = weakReferences;
        this.allocator = allocator;
        
        builder = new BlobBuilder(allocator);
        
        weakReferenceSet = new();
        patchableStrongObjectReferences = new(allocator);
        patchableTypeInfos = new(allocator);
        constStorage = new NativeList<byte>(allocator);
        hashCache = new();
        localComponents = new();
        lookupComponents = new();
        
        ref var root = ref ConstructRoot();
        fixed (BlobExpressionData* proot = &root)
            data = proot;
    }

    public ref BlobExpressionData GetData()
    {
        return ref *data;
    }

    NativeArray<T> AsArray<T>(BlobBuilderArray<T> blobBuilderArray) where T : unmanaged
    {
        var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
            blobBuilderArray.GetUnsafePtr(),
            blobBuilderArray.Length,
            Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
        return result;
    }

    /// <summary>
    /// Call this before baking individual expressions
    /// </summary>
    /// <param name="expressionCount"></param>
    public void InitializeBake(int expressionCount)
    {
        builderExpressions = AsArray(builder.Allocate(ref data->expressions, expressionCount));
        builderTypeHashes = AsArray(builder.Allocate(ref data->expressionTypeHashes, expressionCount));
        builderSourceGraphNodeIds = AsArray(builder.Allocate(ref data->sourceGraphNodeIds, expressionCount));
    }

    public int ExpressionCount
    {
        get
        {
            if (!builderExpressions.IsCreated)
                throw new InvalidOperationException("call InitializeBake first");
            
            return builderExpressions.Length;
        }
    }

    /// <summary>
    /// Get the storage slot for the expression at index <paramref name="index"/>
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public ExpressionStorageRef GetStorage(int index)
    {
        if(index < 0 || index >= builderExpressions.Length || index >= builderTypeHashes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        return new ExpressionStorageRef(
            ref ((ExpressionData*)builderExpressions.GetUnsafePtr())[index].storage,
            ref ((ulong*)builderTypeHashes.GetUnsafePtr())[index]
        );
    }

    /// <summary>
    /// Call this after all individual expressions have been baked
    /// </summary>
    public BlobAssetReference<TBlob> CreateAsset<TBlob>(Allocator assetAllocator) where TBlob : unmanaged
    {
        ExprAuthoring.BakeConstStorage(ref builder, ref *data, constStorage);
        
        // create an array of internal blob ptrs to strong object refs, so they
        // can be patched at runtime to point to loaded object instance ids
        var patchableObjectRefs = builder.Allocate(ref data->patchableObjectRefs, patchableStrongObjectReferences.Length);
        for (int i = 0; i < patchableStrongObjectReferences.Length; ++i)
        {
            ref UntypedExpressionObjectId objectId =
                ref *(UntypedExpressionObjectId*)patchableStrongObjectReferences[i];
            builder.SetPointer(ref patchableObjectRefs[i], ref objectId);
        }

        // patching info for component reflection
        
        // NOTE: component layout info must be patched at runtime because
        // there may be components with platform-dependent layouts
        var typeInfos = builder.Allocate(ref data->patchableTypeInfos, this.patchableTypeInfos.Length);
        var typeHashes = builder.Allocate(ref data->typeInfoTypeHashes, this.patchableTypeInfos.Length);
        for (int i = 0; i < patchableTypeInfos.Length; ++i)
        {
            ref ExpressionComponentTypeInfo typeInfo =
                ref *(ExpressionComponentTypeInfo*)this.patchableTypeInfos[i].typeReference;
            builder.SetPointer(ref typeInfos[i], ref typeInfo);
            typeHashes[i] = this.patchableTypeInfos[i].stableTypeHash;
        }
        
        return builder.CreateBlobAssetReference<TBlob>(assetAllocator);
    }

    /// <summary>
    /// Construct the root blob. For derived contexts, the actual
    /// root may be another type that contains <see cref="BlobExpressionData"/>
    /// directly or via <see cref="BlobPtr{BlobExpressionData}"/>.
    /// </summary>
    /// <returns></returns>
    protected virtual ref BlobExpressionData ConstructRoot()
    {
        return ref builder.ConstructRoot<BlobExpressionData>();
    }

    /// <summary>
    /// Allocate storage for an expression and record its type.
    /// </summary>
    /// <param name="storageRef">Reference to the storage slot for the expression being currently baked</param>
    /// <typeparam name="TExpression"></typeparam>
    /// <returns></returns>
    public ref TExpression Allocate<TExpression>(ExpressionStorageRef storageRef)
        where TExpression : unmanaged, IExpressionBase
    {
        return ref ExprAuthoring.Allocate<TExpression>(ref builder, storageRef, hashCache);
    }

    public void Bake<TAsset>(ref ExpressionObjectRef<TAsset> objectRef, TAsset asset) where TAsset : UnityEngine.Object
    {
        // NOTE: the dynamic buffer and patchable references have matching indices
        
        strongReferences.Add(new()
        {
            asset = asset,
        });
        
        fixed (void* p = &objectRef)
            patchableStrongObjectReferences.Add((IntPtr)p);
    }
        
    public void Bake<TAsset>(ref WeakExpressionObjectRef<TAsset> objectRef, TAsset asset) where TAsset : UnityEngine.Object
    {
        if (!weakReferenceSet.TryGetValue(asset, out var wor))
        {
            wor = weakReferenceSet[asset] = new WeakObjectReference<UnityEngine.Object>(asset);
            weakReferences.Add(new() { asset = wor });
        }

        objectRef.GlobalId = wor.Id.GlobalId;
        objectRef.GenerationType = wor.Id.GenerationType;
    }

    public void Bake<TComponentData>(ref ExpressionComponentTypeInfo typeInfo, ComponentLocation location) where TComponentData : unmanaged, IComponentData
    {
        int fieldCount = BlobExpressionData.GetComponentFields<TComponentData>().Length;
        builder.Allocate(ref typeInfo.fields, fieldCount);
        fixed(void* p = &typeInfo)
            patchableTypeInfos.Add((TypeManager.GetTypeInfo<TComponentData>().StableTypeHash, (IntPtr)p));

        switch (location)
        {
            case ComponentLocation.Local:
                typeInfo.componentIndex = localComponents.FindIndex(kv => kv.GetManagedType() == typeof(TComponentData));
                break;
            
            case ComponentLocation.Lookup:
                typeInfo.componentIndex = lookupComponents.FindIndex(kv => kv.GetManagedType() == typeof(TComponentData));
                break;
        }

        if(typeInfo.componentIndex <= 0)
            throw new System.Exception($"component type {typeof(TComponentData).Name} not found in type list");
    }

    public ExpressionRef Const<TConstant>(TConstant constant) where TConstant : unmanaged
        => ExprAuthoring.WriteConstant2(constant, constStorage);
}
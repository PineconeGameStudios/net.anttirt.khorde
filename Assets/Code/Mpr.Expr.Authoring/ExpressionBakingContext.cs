using System;
using System.Collections.Generic;
using System.Linq;
using Mpr.Blobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Expr.Authoring;

public enum ExpressionComponentLocation
{
    /// <summary>
    /// Components local to the expression's owning entity
    /// </summary>
    Local,
    
    /// <summary>
    /// Components on other entities accessed via ComponentLookup
    /// </summary>
    Lookup,
}

public unsafe class ExpressionBakingContext : IDisposable
{
    private NativeList<(ulong stableTypeHash, IntPtr typeReference)> patchableTypeInfos;
    protected BlobBuilder builder;
    public ref BlobBuilder Builder => ref builder;
    private BlobExpressionData* data;
    private NativeList<byte> constStorage;
    private Dictionary<Type, ulong> hashCache;
    private Dictionary<object, (ushort offset, ushort length)> constCache = new();
    
    // components accessed directly on the current entity (behavior trees / queries)
    private Dictionary<Type, ComponentType.AccessMode> localComponentsDict;
    private List<ComponentType> localComponents;
    public List<ComponentType> LocalComponents => localComponents;

    // components looked up on other entities
    private Dictionary<Type, ComponentType.AccessMode> lookupComponentsDict;
    private List<ComponentType> lookupComponents;
    public List<ComponentType> LookupComponents => lookupComponents;
    protected NativeArray<ExpressionData> builderExpressions;
    protected NativeArray<ulong> builderTypeHashes;
    protected NativeArray<UnityEngine.Hash128> builderSourceGraphNodeIds;
    protected NativeArray<ExpressionOutput> builderOutputs;
    private Allocator allocator;

    public ExpressionBakingContext(Allocator allocator)
    {
        this.allocator = allocator;
        
        builder = new BlobBuilder(allocator);
        
        patchableTypeInfos = new(allocator);
        constStorage = new NativeList<byte>(allocator);
        hashCache = new();
        
        localComponentsDict = new();
        lookupComponentsDict = new();
        
        ref var root = ref ConstructRoot();
        fixed (BlobExpressionData* proot = &root)
            data = proot;
    }
    
    public virtual void Dispose()
    {
        builder.Dispose();
        patchableTypeInfos.Dispose();
        constStorage.Dispose();
        data = null;
        builderExpressions = default;
        builderSourceGraphNodeIds = default;
        builderOutputs = default;
    }

    public ref BlobExpressionData GetData()
    {
        return ref *data;
    }

    protected NativeArray<T> AsArray<T>(BlobBuilderArray<T> blobBuilderArray) where T : unmanaged
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
    /// <param name="outputCount"></param>
    public virtual void InitializeBake(int expressionCount, int outputCount)
    {
        builderExpressions = AsArray(builder.Allocate(ref data->expressions, expressionCount));
        builderTypeHashes = AsArray(builder.Allocate(ref data->expressionTypeHashes, expressionCount));
        builderSourceGraphNodeIds = AsArray(builder.Allocate(ref data->sourceGraphNodeIds, expressionCount));
        
        builderOutputs = AsArray(builder.Allocate(ref data->outputs, outputCount));

        localComponents = localComponentsDict.Select(kv => new ComponentType(kv.Key, kv.Value)).ToList();
        lookupComponents = lookupComponentsDict.Select(kv => new ComponentType(kv.Key, kv.Value)).ToList();

        localComponentsDict = null;
        lookupComponentsDict = null;
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

    private static ComponentType.AccessMode Combine(ComponentType.AccessMode m0, ComponentType.AccessMode m1)
    {
        if(m0 == ComponentType.AccessMode.ReadWrite || m1 ==  ComponentType.AccessMode.ReadWrite)
            return ComponentType.AccessMode.ReadWrite;
        return ComponentType.AccessMode.ReadOnly;
    }

    public void RegisterComponentAccess<TComponent>(ExpressionComponentLocation location, ComponentType.AccessMode accessMode) where TComponent : IComponentData
        => RegisterComponentAccess(typeof(TComponent), location, accessMode);
    
    public void RegisterComponentAccess(Type type, ExpressionComponentLocation location, ComponentType.AccessMode accessMode)
    {
        if (localComponentsDict == null)
            throw new InvalidOperationException("Register component types before calling InitializeBake");
        
        var dict = location == ExpressionComponentLocation.Local ? localComponentsDict : lookupComponentsDict;
        if (dict.TryGetValue(type, out var value))
        {
            if (value != accessMode)
            {
                dict[type] = Combine(accessMode, value);
            }
        }
        else
        {
            dict[type] = accessMode;
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

    public void FinalizeBake()
    {
        ExprAuthoring.BakeConstStorage(ref builder, ref *data, constStorage);
        
        var localComponents = builder.Allocate(ref data->localComponents, this.localComponents.Count);
        for (int i = 0; i < localComponents.Length; ++i)
            localComponents[i] = new BlobComponentType(this.localComponents[i]);
        
        var lookupComponents = builder.Allocate(ref data->lookupComponents, this.lookupComponents.Count);
        for (int i = 0; i < lookupComponents.Length; ++i)
            lookupComponents[i] = new BlobComponentType(this.lookupComponents[i]);
        
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
    }

    /// <summary>
    /// Call this after all individual expressions have been baked
    /// </summary>
    public BlobAssetReference<TBlob> CreateAsset<TBlob>(Allocator assetAllocator) where TBlob : unmanaged
    {
        FinalizeBake();
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
    /// Allocate storage for an expression and record its type, returning a reference to the data that can be further initialized.
    /// </summary>
    /// <param name="storageRef">Reference to the storage slot for the expression being currently baked</param>
    /// <typeparam name="TExpression"></typeparam>
    /// <returns></returns>
    public ref TExpression CreateExpression<TExpression>(ExpressionStorageRef storageRef)
        where TExpression : unmanaged, IExpressionBase
    {
        return ref ExprAuthoring.Allocate<TExpression>(ref builder, storageRef, hashCache);
    }
    
    /// <summary>
    /// Allocate storage for an expression, initialize it and record its type.
    /// </summary>
    /// <param name="storageRef">Reference to the storage slot for the expression being currently baked</param>
    /// <param name="expression">The value to initialize the expression to</param>
    /// <typeparam name="TExpression"></typeparam>
    public void CreateExpression<TExpression>(ExpressionStorageRef storageRef, TExpression expression)
        where TExpression : unmanaged, IExpressionBase
        => CreateExpression<TExpression>(storageRef) = expression;

    public void Bake<TComponentData>(ref ExpressionComponentTypeInfo typeInfo, ExpressionComponentLocation location) where TComponentData : unmanaged, IComponentData
    {
        int fieldCount = BlobExpressionData.GetComponentFields<TComponentData>().Length;
        builder.Allocate(ref typeInfo.fields, fieldCount);
        fixed(void* p = &typeInfo)
            patchableTypeInfos.Add((TypeManager.GetTypeInfo<TComponentData>().StableTypeHash, (IntPtr)p));

        typeInfo.componentIndex = -1;

        switch (location)
        {
            case ExpressionComponentLocation.Local:
                typeInfo.componentIndex = localComponents.FindIndex(kv => kv.GetManagedType() == typeof(TComponentData));
                break;
            
            case ExpressionComponentLocation.Lookup:
                typeInfo.componentIndex = lookupComponents.FindIndex(kv => kv.GetManagedType() == typeof(TComponentData));
                break;
        }

        if(typeInfo.componentIndex == -1)
            throw new System.Exception($"component type {typeof(TComponentData).Name} not found in type list");
    }

    public ExpressionRef Const<TConstant>(TConstant constant) where TConstant : unmanaged
        => ExprAuthoring.WriteConstant2(constant, constStorage, constCache);
    
    public ExpressionRef Const(object constant)
        => ExprAuthoring.WriteConstant2(constant, constStorage, constCache);
}
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr;

public unsafe class ExpressionBakingContext
{
    private DynamicBuffer<BlobExpressionObjectReference> strongReferences;
    private DynamicBuffer<BlobExpressionWeakObjectReference> weakReferences;
    private Dictionary<UnityEngine.Object, WeakObjectReference<UnityEngine.Object>> weakReferenceSet;
    private NativeList<IntPtr> patchableStrongObjectReferences;
    private NativeList<(ulong stableTypeHash, IntPtr typeReference)> patchableTypeInfos;
    private BlobBuilder builder;
    private BlobExpressionData* data;
    
    // components accessed directly on the current entity (behavior trees / queries)
    private Dictionary<Type, ComponentType.AccessMode> localComponentsDict;
    private List<ComponentType> localComponents;

    // components looked up on other entities
    private Dictionary<Type, ComponentType.AccessMode> lookupComponentsDict;
    private List<ComponentType> lookupComponents;

    public enum ComponentLocation
    {
        Local,
        Lookup,
    }

    public ExpressionBakingContext(
        DynamicBuffer<BlobExpressionObjectReference> strongReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakReferences
    )
    {
        this.strongReferences = strongReferences;
        this.weakReferences = weakReferences;
    }

    public void Bake()
    {
        weakReferenceSet = new();
        builder = new BlobBuilder(Allocator.Temp);
        patchableStrongObjectReferences = new (Allocator.Temp);
        patchableTypeInfos = new(Allocator.Temp);
        ref var root = ref ConstructRoot();
        fixed (BlobExpressionData* proot = &root)
            data = proot;

        localComponents = new();
        lookupComponents = new();
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

    public ref TExpression Allocate<TExpression>(ref ExpressionData exprData) where TExpression : unmanaged, IExpression
    {
        if (UnsafeUtility.SizeOf<TExpression>() > UnsafeUtility.SizeOf<ExpressionStorage>())
        {
            ref var blobPtr = ref exprData.storage.AsDataReference<TExpression>();
            return ref builder.Allocate(ref blobPtr);
        }
        else
        {
            fixed (ExpressionStorage* pstorage = &exprData.storage)
                return ref *(TExpression*)pstorage;
        }
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

    public void FinalizeBake()
    {
        // create an array of internal blob ptrs to strong object refs, so they
        // can be patched at runtime to point to loaded object instance ids
        var patchableObjectRefs = builder.Allocate(ref data->patchableObjectRefs, patchableStrongObjectReferences.Length);
        for (int i = 0; i < patchableStrongObjectReferences.Length; ++i)
        {
            ref UntypedExpressionObjectRef objectRef =
                ref *(UntypedExpressionObjectRef*)patchableStrongObjectReferences[i];
            builder.SetPointer(ref patchableObjectRefs[i], ref objectRef);
        }

        // create patching info for type hashes
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

    public ExpressionRef GetExpressionRef(IPort inputPort)
    {
        throw new NotImplementedException();
    }
}
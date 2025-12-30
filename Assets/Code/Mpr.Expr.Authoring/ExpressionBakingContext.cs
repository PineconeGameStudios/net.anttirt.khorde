using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;

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

    public void Bake<TComponentData>(ref ExpressionComponentTypeInfo typeInfo) where TComponentData : IComponentData
    {
        fixed(void* p = &typeInfo)
            patchableTypeInfos.Add((TypeManager.GetTypeInfo<TComponentData>().StableTypeHash, (IntPtr)p));
    }

    public void FinalizeBake()
    {
        // create an array of internal blob ptrs to strong object refs, so they
        // can be patched at runtime to point to loaded object instance ids
        var patchableObjectRefs = builder.Allocate(ref data->patchableObjectRefs, patchableStrongObjectReferences.Length);
        for (int i = 0; i < patchableStrongObjectReferences.Length; ++i)
        {
            unsafe
            {
                ref UntypedExpressionObjectRef objectRef = ref *(UntypedExpressionObjectRef*)patchableStrongObjectReferences[i];
                builder.SetPointer(ref patchableObjectRefs[i], ref objectRef);
            }
        }
    }
}
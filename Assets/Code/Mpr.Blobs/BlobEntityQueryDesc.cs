using Unity.Collections;
using Unity.Entities;

namespace Mpr.Blobs;

/// <summary>
/// Describes an entity query in a format that can be serialized in a blob assets and loaded at runtime.
/// </summary>
public struct BlobEntityQueryDesc
{
    public BlobArray<BlobComponentType> all;
    public BlobArray<BlobComponentType> any;
    public BlobArray<BlobComponentType> none;
    public BlobArray<BlobComponentType> disabled;
    public BlobArray<BlobComponentType> absent;
    public BlobArray<BlobComponentType> present;
    public EntityQueryOptions pendingOptions;
    
    NativeList<ComponentType> GetComponentArray(ref BlobArray<BlobComponentType> src, AllocatorManager.AllocatorHandle allocator)
    {
        var dst = new NativeList<ComponentType>(src.Length, allocator);
        
        for (int i = 0; i < src.Length; i++)
            dst.Add(src[i].ResolveComponentType());
        
        return dst;
    }

    public EntityQuery CreateQuery(EntityManager entityManager)
    {
        var eqb = new  EntityQueryBuilder(Allocator.Temp);

        var allC = GetComponentArray(ref all, Allocator.Temp);
        eqb.WithAll(ref allC);
        allC.Dispose();

        var anyC = GetComponentArray(ref any, Allocator.Temp);
        eqb.WithAll(ref anyC);
        anyC.Dispose();

        var noneC = GetComponentArray(ref none, Allocator.Temp);
        eqb.WithAll(ref noneC);
        noneC.Dispose();

        var disabledC = GetComponentArray(ref disabled, Allocator.Temp);
        eqb.WithAll(ref disabledC);
        disabledC.Dispose();

        var absentC = GetComponentArray(ref absent, Allocator.Temp);
        eqb.WithAll(ref absentC);
        absentC.Dispose();

        var presentC = GetComponentArray(ref present, Allocator.Temp);
        eqb.WithAll(ref presentC);
        presentC.Dispose();
        
        eqb.WithOptions(pendingOptions);

        return eqb.Build(entityManager);
    }
}
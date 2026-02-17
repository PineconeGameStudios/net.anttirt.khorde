using System;
using Mpr.Query;
using Unity.Entities;
using Unity.Mathematics;

[assembly: QueryItemType(typeof(Entity))]
[assembly: QueryItemType(typeof(int2))]
[assembly: QueryItemType(typeof(float2))]

public struct QueryItemTypeInfo
{
    public Type type;
    
    public QueryItemTypeInfo(Type type)
    {
        this.type = type;
    }
}
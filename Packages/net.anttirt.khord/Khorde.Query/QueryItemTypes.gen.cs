using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Khorde.Query.Generated
{
	[Preserve]
	static class QueryTypeRegistry
	{
	    public static readonly QueryItemTypeInfo[] QueryItemTypes = new[]
	    {
	        new QueryItemTypeInfo(typeof(Entity)),
	        new QueryItemTypeInfo(typeof(int2)),
	        new QueryItemTypeInfo(typeof(float2)),
	    };
	}


	// TODO: generate query Execute<T> overloads that can be selected at runtime by type hash
}
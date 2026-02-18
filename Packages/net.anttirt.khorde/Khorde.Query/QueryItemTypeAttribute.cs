using System;

namespace Khorde.Query
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class QueryItemTypeAttribute : Attribute
	{
	    public Type ItemType { get; }
    
	    public QueryItemTypeAttribute(Type itemType)
	    {
	        this.ItemType = itemType;
	    }
	}
}
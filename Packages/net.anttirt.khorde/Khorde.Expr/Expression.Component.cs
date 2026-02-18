using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;

namespace Khorde.Expr
{
	public partial struct ReadComponentField : IExpression
	{
	    public ExpressionComponentTypeInfo typeInfo;

	    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
	    void CheckInitialized(int outputIndex)
	    {
	        if (typeInfo.fields[outputIndex].length == 0)
	            throw new InvalidOperationException("field info not initialized");
	    }
        
	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        CheckInitialized(outputIndex);
	        var field = typeInfo.fields[outputIndex];
	        untypedResult.CopyFrom(ctx.componentPtrs[typeInfo.componentIndex].AsNativeArray(field.offset, field.length));
	    }
	}

	public partial struct LookupComponentField : IExpression<Entity>
	{
	    public ExpressionRef Input0 { get; set; }
	    public ExpressionComponentTypeInfo typeInfo;

	    public void Evaluate(in ExpressionEvalContext ctx, in Entity entity, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        if (ctx.componentLookups[typeInfo.componentIndex].TryGetRefRO(entity, out var componentData))
	        {
	            if (outputIndex == 0)
	            {
	                untypedResult.AsSingle<bool>() = true;
	            }
	            else
	            {
	                var field = typeInfo.fields[outputIndex - 1];
	                if (componentData.IsCreated)
	                {
	                    untypedResult.CopyFrom(componentData.GetSubArray(field.offset, field.length));
	                }
	                else
	                {
	                    untypedResult.Clear();
	                }
	            }
	        }
	        else
	        {
	            untypedResult.Clear();
	        }
	    }
	}
}
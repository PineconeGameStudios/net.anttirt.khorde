using Unity.Collections;
using Unity.Entities;

namespace Mpr.Expr;

public partial struct ReadComponentField : IExpression
{
    public ExpressionComponentTypeInfo typeInfo;
        
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        var field = typeInfo.fields[outputIndex];
        untypedResult.CopyFrom(ctx.componentPtrs[typeInfo.componentIndex].AsNativeSlice().Slice(field.offset, field.length));
    }
}

public partial struct LookupComponentField : IExpression
{
    public ExpressionRef entity;
    public ExpressionComponentTypeInfo typeInfo;
        
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        if (ctx.componentLookups[typeInfo.componentIndex].TryGetRefRO(entity.Evaluate<Entity>(in ctx), out var componentData))
        {
            if (outputIndex == 0)
            {
                var result = untypedResult.SliceConvert<bool>();
                result[0] = true;
            }
            else
            {
                var field = typeInfo.fields[outputIndex - 1];
                if (componentData.IsCreated)
                {
                    untypedResult.CopyFrom(componentData.Slice(field.offset, field.length));
                }
                else
                {
                    for (int i = 0; i < untypedResult.Length; i++)
                        untypedResult[i] = default;
                }
            }
        }
        else
        {
            for (int i = 0; i < untypedResult.Length; i++)
                untypedResult[i] = default;
        }
    }
}
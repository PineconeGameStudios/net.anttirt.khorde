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

public partial struct LookupComponentField : IExpression<Entity>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionComponentTypeInfo typeInfo;

    public void Evaluate(in ExpressionEvalContext ctx, in Entity entity, int outputIndex, ref NativeSlice<byte> untypedResult)
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
                    untypedResult.CopyFrom(componentData.Slice(field.offset, field.length));
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
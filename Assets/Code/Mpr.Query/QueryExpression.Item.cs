using Mpr.Expr;
using Unity.Burst;
using Unity.Collections;

namespace Mpr.Query;

public partial struct CurrentQueryItem : IExpression
{
    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        // special: in query filter/scorer contexts, the final "component" is the current item
        ctx.componentPtrs[^1].AsNativeArray().CopyTo(untypedResult);
    }
}
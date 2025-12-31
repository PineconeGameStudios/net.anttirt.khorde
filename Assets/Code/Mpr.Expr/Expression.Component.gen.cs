using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Mpr.Expr;

[BurstCompile]
public partial struct ReadComponentField
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<ReadComponentField>(self, in ctx, outputIndex, ref untypedResult);
    }
}
    
[BurstCompile]
public partial struct LookupComponentField
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<LookupComponentField, Entity>(self, in ctx, outputIndex, ref untypedResult);
    }
}

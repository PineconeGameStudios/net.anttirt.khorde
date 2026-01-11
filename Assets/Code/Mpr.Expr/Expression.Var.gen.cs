using Unity.Burst;
using Unity.Collections;

namespace Mpr.Expr;

[BurstCompile]
public partial struct Variable
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<Variable>(self, in ctx, outputIndex, ref untypedResult);
    }
}

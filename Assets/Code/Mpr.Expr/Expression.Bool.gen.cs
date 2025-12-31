using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct BinaryBool
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryBool, bool, bool>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct UnaryBool
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<UnaryBool, bool>(self, in ctx, outputIndex, ref untypedResult);
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

[BurstCompile]
public partial struct Swizzle32x1
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<Swizzle32x1, int>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct Swizzle32x2
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<Swizzle32x2, int2>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct Swizzle32x3
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<Swizzle32x3, int3>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct Swizzle32x4
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<Swizzle32x4, int4>(self, in ctx, outputIndex, ref untypedResult);
    }
}

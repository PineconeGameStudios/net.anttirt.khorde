using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

[BurstCompile]
public partial struct BinaryFloat
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryFloat, float, float>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryFloat2
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryFloat2, float2, float2>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryFloat3
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryFloat3, float3, float3>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryFloat4
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryFloat4, float4, float4>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryInt
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryInt, int, int>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryInt2
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryInt2, int2, int2>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryInt3
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryInt3, int3, int3>(self, in ctx, outputIndex, ref untypedResult);
    }
}

[BurstCompile]
public partial struct BinaryInt4
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        EvalHelper.Evaluate<BinaryInt4, int4, int4>(self, in ctx, outputIndex, ref untypedResult);
    }
}
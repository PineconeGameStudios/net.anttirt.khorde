using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[BurstCompile]
public partial struct TestLargeExpression
{
    [BurstCompile]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<TestLargeExpression, float4>(self, in ctx, outputIndex, ref untypedResult);
    }
}

public partial struct TestManagedExpression
{
    [AOT.MonoPInvokeCallback(typeof(ExpressionEvalDelegate))]
    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeArray<byte> untypedResult)
    {
        EvalHelper.Evaluate<TestManagedExpression, int, int>(self, in ctx, outputIndex, ref untypedResult);
    }
}
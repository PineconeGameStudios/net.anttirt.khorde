using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct TestLargeExpression
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((TestLargeExpression*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((TestLargeExpression*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve]
public partial struct TestManagedExpression
{
    [AOT.MonoPInvokeCallback(typeof(EvaluateDelegate2))]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((TestManagedExpression*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [AOT.MonoPInvokeCallback(typeof(EvaluateDelegate2))]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((TestManagedExpression*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}
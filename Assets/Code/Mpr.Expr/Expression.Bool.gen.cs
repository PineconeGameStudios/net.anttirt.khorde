using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct BinaryBool
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryBool*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryBool*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct UnaryBool
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((UnaryBool*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((UnaryBool*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

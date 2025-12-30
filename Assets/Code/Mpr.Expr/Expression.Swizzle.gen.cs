using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct Swizzle32
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((Swizzle32*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((Swizzle32*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct Swizzle64
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((Swizzle64*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((Swizzle64*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct BinaryFloat
{
    [BurstCompile]
    public static unsafe void Evaluate(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct BinaryFloat2
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat2*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat2*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct BinaryFloat3
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat3*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat3*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

[Preserve, BurstCompile]
public partial struct BinaryFloat4
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat4*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((BinaryFloat4*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}
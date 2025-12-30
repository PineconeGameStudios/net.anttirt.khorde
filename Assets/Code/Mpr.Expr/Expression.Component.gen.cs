using Unity.Burst;
using Unity.Collections;
using UnityEngine.Scripting;

namespace Mpr.Expr;

[Preserve, BurstCompile]
public partial struct ReadComponentField
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((ReadComponentField*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((ReadComponentField*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}
    
[Preserve, BurstCompile]
public partial struct LookupComponentField
{
    [BurstCompile]
    public static unsafe void EvaluateDirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((LookupComponentField*)self)->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
        
    [BurstCompile]
    public static unsafe void EvaluateIndirect(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
        ref NativeSlice<byte> untypedResult)
    {
        ((LookupComponentField*)self->dataReference.GetUnsafePtr())->Evaluate(in ctx, outputIndex, ref untypedResult);
    }
}

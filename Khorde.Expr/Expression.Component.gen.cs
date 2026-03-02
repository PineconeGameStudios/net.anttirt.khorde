using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct ReadComponentField
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<ReadComponentField>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}
    
	[BurstCompile]
	public partial struct LookupComponentField
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<LookupComponentField, Entity>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct ReadBufferField
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<ReadBufferField, int>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct ReadBufferLength
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<ReadBufferLength>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}
}

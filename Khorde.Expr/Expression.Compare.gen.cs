using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct BinaryCompareFloat
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryCompareFloat, float, float>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryCompareInt
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryCompareInt, int, int>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}
}
using Khorde.Expr;
using Unity.Burst;
using Unity.Collections;

namespace Khorde.Query
{
	[BurstCompile]
	public partial struct CurrentQueryItem
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<CurrentQueryItem>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}
}

using Khorde.Expr;
using Unity.Burst;
using Unity.Collections;

namespace Khorde.Query
{
	public partial struct CurrentQueryItem : IExpression
	{
	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        // special: in query filter/scorer contexts, the final "component" is the current item
	        ctx.componentPtrs[^1].AsNativeArray().GetSubArray(0, untypedResult.Length).CopyTo(untypedResult);
	    }
	}
}
using Unity.Burst;
using Unity.Collections;

namespace Mpr.Expr
{
	public partial struct Variable : IExpression
	{
	    public int index;
    
	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        ctx.GetBlackboardVariable(index).CopyTo(untypedResult);
	    }
	}
}

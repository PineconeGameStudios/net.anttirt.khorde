using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	public partial struct Time : IExpression
	{
		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			if(outputIndex == 0)
				untypedResult.ReinterpretStore<float>(0, ctx.time);
			else
				untypedResult.ReinterpretStore<float>(0, ctx.deltaTime);
		}
	}
}

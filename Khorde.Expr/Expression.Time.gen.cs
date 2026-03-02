using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct Time : IExpression
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<Time>(self, in ctx, outputIndex, ref untypedResult);
		}
	}
}

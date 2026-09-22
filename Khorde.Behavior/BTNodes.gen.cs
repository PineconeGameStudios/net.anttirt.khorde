using Khorde.Expr;
using Unity.Burst;
using Unity.Collections;

namespace Khorde.Behavior
{
	[BurstCompile]
	public partial struct CurrentUtility
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<CurrentUtility>(self, in ctx, outputIndex, ref untypedResult);
		}
	}
}
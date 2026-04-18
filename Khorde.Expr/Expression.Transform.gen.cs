using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct ReadLocalToWorld
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<ReadLocalToWorld>(self, in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public partial struct LookupLocalToWorld
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<LookupLocalToWorld, Entity>(self, in ctx, outputIndex, ref untypedResult);
		}
	}
}

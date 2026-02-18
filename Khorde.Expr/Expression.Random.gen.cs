using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct RandomFloat
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<RandomFloat>(self, in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public partial struct RandomFloat2Direction
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<RandomFloat2Direction>(self, in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public partial struct RandomFloat3Direction
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<RandomFloat3Direction>(self, in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public partial struct RandomInt
	{
		[BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
			ref NativeArray<byte> untypedResult)
		{
			EvalHelper.Evaluate<RandomInt>(self, in ctx, outputIndex, ref untypedResult);
		}
	}
}

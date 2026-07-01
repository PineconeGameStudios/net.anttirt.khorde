using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	public partial struct Variable : IExpression
	{
		public VariableId index;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ctx.GetBlackboardVariable(index).CopyTo(untypedResult);
		}
	}

	public partial struct Ref : IExpression
	{
		public ExpressionRef @ref;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			@ref.Evaluate(in ctx, ref untypedResult);
		}
	}
}

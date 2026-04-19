using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Khorde.Expr
{
	[BurstCompile]
	public struct Switch : IExpressionBase
	{
		public ExpressionRef @switch;
		public BlobArray<Case> cases;
		public ExpressionRef @default;

		public struct Case
		{
			public int @case;
			public ExpressionRef value;
		}

	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
			int @case = @switch.Evaluate<int>(in ctx);
			for(int i = 0; i < cases.Length; ++i)
			{
				if(cases[i].@case == @case)
				{
					cases[i].value.Evaluate(in ctx, ref untypedResult);
					return;
				}
			}

			@default.Evaluate(in ctx, ref untypedResult);
	    }

	    [BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			self->GetUnsafePtr<Switch>()->Evaluate(in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public struct Select : IExpressionBase
	{
		public ExpressionRef @switch;
		public ExpressionRef @true;
		public ExpressionRef @false;

	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
			if(@switch.Evaluate<bool>(in ctx))
				@true.Evaluate(in ctx, ref untypedResult);
			else
				@false.Evaluate(in ctx, ref untypedResult);
	    }

	    [BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			self->GetUnsafePtr<Select>()->Evaluate(in ctx, outputIndex, ref untypedResult);
		}
	}

	[BurstCompile]
	public struct SwitchLinear : IExpressionBase
	{
		public ExpressionRef variable;
		public BlobArray<ExpressionRef> options;

	    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
			int key = variable.Evaluate<int>(in ctx);
			options[math.clamp(key, 0, options.Length - 1)].Evaluate(in ctx, ref untypedResult);
	    }

	    [BurstCompile]
		public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			self->GetUnsafePtr<SwitchLinear>()->Evaluate(in ctx, outputIndex, ref untypedResult);
		}
	}
}

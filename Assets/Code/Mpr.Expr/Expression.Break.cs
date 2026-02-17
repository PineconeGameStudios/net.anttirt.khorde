using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr
{
	public partial struct BreakInt2 : IExpression<int2>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct BreakInt3 : IExpression<int3>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct BreakInt4 : IExpression<int4>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct BreakFloat2 : IExpression<float2>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct BreakFloat3 : IExpression<float3>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct BreakFloat4 : IExpression<float4>
	{
		public ExpressionRef Input0 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, input0[outputIndex]);
		}
	}

	public partial struct MakeInt2 : IExpression<int, int>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int input0, in int input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new int2(input0, input1));
		}
	}

	public partial struct MakeInt3 : IExpression<int, int, int>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public ExpressionRef Input2 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int input0, in int input1, in int input2, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new int3(input0, input1, input2));
		}
	}

	public partial struct MakeInt4 : IExpression<int, int, int, int>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public ExpressionRef Input2 { get; set; }
		public ExpressionRef Input3 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in int input0, in int input1, in int input2, in int input3, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new int4(input0, input1, input2, input3));
		}
	}

	public partial struct MakeFloat2 : IExpression<float, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new float2(input0, input1));
		}
	}

	public partial struct MakeFloat3 : IExpression<float, float, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public ExpressionRef Input2 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float input0, in float input1, in float input2, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new float3(input0, input1, input2));
		}
	}

	public partial struct MakeFloat4 : IExpression<float, float, float, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public ExpressionRef Input2 { get; set; }
		public ExpressionRef Input3 { get; set; }

		public unsafe void Evaluate(in ExpressionEvalContext ctx, in float input0, in float input1, in float input2, in float input3, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.ReinterpretStore(0, new float4(input0, input1, input2, input3));
		}
	}
}

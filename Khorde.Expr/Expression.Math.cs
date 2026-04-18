using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Khorde.Expr
{
	public enum BinaryMathOp
	{
		Add,
		Subtract,
		Multiply,
		Divide,
	}

	public interface IBTBinaryOp
	{
		BinaryMathOp Op { get; }
	}

	public struct BTBinaryOp_Add : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Add; }
	}

	public struct BTBinaryOp_Sub : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Subtract; }
	}

	public struct BTBinaryOp_Mul : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Multiply; }
	}

	public struct BTBinaryOp_Div : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Divide; }
	}

	public partial struct BinaryFloat : IExpression<float, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float left, in float right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<float>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}

	}

	public partial struct BinaryFloat2 : IExpression<float2, float2>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 left, in float2 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<float2>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct BinaryFloat3 : IExpression<float3, float3>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 left, in float3 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<float3>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct BinaryFloat4 : IExpression<float4, float4>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 left, in float4 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<float4>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct BinaryInt : IExpression<int, int>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int left, in int right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<int>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}

	}

	public partial struct BinaryInt2 : IExpression<int2, int2>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int2 left, in int2 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<int2>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct BinaryInt3 : IExpression<int3, int3>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int3 left, in int3 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<int3>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct BinaryInt4 : IExpression<int4, int4>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }
		public BinaryMathOp @operator;

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int4 left, in int4 right, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			ref var result = ref untypedResult.AsSingle<int4>();
			switch(@operator)
			{
				case BinaryMathOp.Add: result = left + right; break;
				case BinaryMathOp.Subtract: result = left - right; break;
				case BinaryMathOp.Multiply: result = left * right; break;
				case BinaryMathOp.Divide: result = left / right; break;
			}
		}
	}

	public partial struct LengthFloat2 : IExpression<float2>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float>() = math.length(input0);
		}
	}

	public partial struct LengthFloat3 : IExpression<float3>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float>() = math.length(input0);
		}
	}

	public partial struct LengthFloat4 : IExpression<float4>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float>() = math.length(input0);
		}
	}

	public partial struct Normalize2 : IExpression<float2>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float2>() = math.normalizesafe(input0);
		}
	}

	public partial struct Normalize3 : IExpression<float3>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float3>() = math.normalizesafe(input0);
		}
	}

	public partial struct Normalize4 : IExpression<float4>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float4>() = math.normalizesafe(input0);
		}
	}

	public partial struct Floor1 : IExpression<float>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int>() = (int)math.floor(input0);
		}
	}

	public partial struct Floor2 : IExpression<float2>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int2>() = (int2)math.floor(input0);
		}
	}

	public partial struct Floor3 : IExpression<float3>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int3>() = (int3)math.floor(input0);
		}
	}

	public partial struct Floor4 : IExpression<float4>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int4>() = (int4)math.floor(input0);
		}
	}

	public partial struct Ceiling1 : IExpression<float>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int>() = (int)math.ceil(input0);
		}
	}

	public partial struct Ceiling2 : IExpression<float2>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int2>() = (int2)math.ceil(input0);
		}
	}

	public partial struct Ceiling3 : IExpression<float3>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int3>() = (int3)math.ceil(input0);
		}
	}

	public partial struct Ceiling4 : IExpression<float4>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<int4>() = (int4)math.ceil(input0);
		}
	}

	public partial struct ToFloat1 : IExpression<int>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float>() = input0;
		}
	}

	public partial struct ToFloat2 : IExpression<int2>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float2>() = input0;
		}
	}

	public partial struct ToFloat3 : IExpression<int3>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float3>() = input0;
		}
	}

	public partial struct ToFloat4 : IExpression<int4>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in int4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float4>() = input0;
		}
	}

	public partial struct Rescale2 : IExpression<float2, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float2>() = math.normalizesafe(input0) * input1;
		}
	}

	public partial struct Rescale3 : IExpression<float3, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float3>() = math.normalizesafe(input0) * input1;
		}
	}

	public partial struct Rescale4 : IExpression<float4, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float4 input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float4>() = math.normalizesafe(input0) * input1;
		}
	}

	public partial struct AngleToDirection : IExpression<float>
	{
		public ExpressionRef Input0 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float input0, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			math.sincos(input0, out var s, out var c);
			untypedResult.AsSingle<float2>() = new float2(c, s);
		}
	}

	public partial struct Rotate2D : IExpression<float2, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float2 input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float2>() = math.mul(float2x2.Rotate(input1), input0);
		}
	}

	public partial struct Rotate3D : IExpression<float3, quaternion>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, in quaternion input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<float3>() = math.mul(input1, input0);
		}
	}

	public partial struct AxisAngle : IExpression<float3, float>
	{
		public ExpressionRef Input0 { get; set; }
		public ExpressionRef Input1 { get; set; }

		[BurstCompile]
		public void Evaluate(in ExpressionEvalContext ctx, in float3 input0, in float input1, int outputIndex, ref NativeArray<byte> untypedResult)
		{
			untypedResult.AsSingle<quaternion>() = quaternion.AxisAngle(input0, input1);
		}
	}
}
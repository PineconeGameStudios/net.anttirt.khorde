using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

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
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

public struct SwizzleOp
{
	public byte outputCount;
	public ushort desc;
	const ushort Bits = 3;
	const ushort Mask = (1 << Bits) - 1;
	public const int ZeroOp = 4;

	public ushort this[int index]
	{
		get => (ushort)((desc >> (index * Bits)) & Mask);
		set => desc = (ushort)((desc & ~(Mask << (index * Bits))) | (value << (index * Bits)));
	}

	public static SwizzleOp Parse(string pattern)
	{
		var op = new SwizzleOp
		{
			outputCount = (byte)pattern.Length,
		};

		byte FieldToIndex(char field)
		{
			switch(char.ToLowerInvariant(field))
			{
				case 'x': case 'r': return 0;
				case 'y': case 'g': return 1;
				case 'z': case 'b': return 2;
				case 'w': case 'a': return 3;
				case '0': return ZeroOp;
				default: return 0;
			}
		}

		for(int i = 0; i < pattern.Length; ++i)
			op[i] = FieldToIndex(pattern[i]);

		return op;
	}

	public override string ToString()
	{
		char[] ret = new char[outputCount];
		for(int i = 0; i < outputCount; ++i)
			ret[i] = "xyzw0"[this[i]];
		return new string(ret);
	}
}

public partial struct Swizzle32x1 : IExpression<int>
{
	public ExpressionRef Input0 { get; set; }
	public SwizzleOp @operator;

	public unsafe void Evaluate(in ExpressionEvalContext ctx, in int input0, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		var result = untypedResult.Reinterpret<int>(1);
		for(int i = 0; i < @operator.outputCount; ++i)
		{
			var k = @operator[i];
			if(k == SwizzleOp.ZeroOp)
				result[i] = 0;
			else
				result[i] = input0;
		}
	}
}

public partial struct Swizzle32x2 : IExpression<int2>
{
	public ExpressionRef Input0 { get; set; }
	public SwizzleOp @operator;

	public unsafe void Evaluate(in ExpressionEvalContext ctx, in int2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		var result = untypedResult.Reinterpret<int>(1);
		for(int i = 0; i < @operator.outputCount; ++i)
		{
			var k = @operator[i];
			if(k == SwizzleOp.ZeroOp)
				result[i] = 0;
			else
				result[i] = input0[k];
		}
	}
}

public partial struct Swizzle32x3 : IExpression<int3>
{
	public ExpressionRef Input0 { get; set; }
	public SwizzleOp @operator;

	public unsafe void Evaluate(in ExpressionEvalContext ctx, in int3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		var result = untypedResult.Reinterpret<int>(1);
		for(int i = 0; i < @operator.outputCount; ++i)
		{
			var k = @operator[i];
			if(k == SwizzleOp.ZeroOp)
				result[i] = 0;
			else
				result[i] = input0[k];
		}
	}
}

public partial struct Swizzle32x4 : IExpression<int4>
{
	public ExpressionRef Input0 { get; set; }
	public SwizzleOp @operator;

	public unsafe void Evaluate(in ExpressionEvalContext ctx, in int4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
	{
		var result = untypedResult.Reinterpret<int>(1);
		for(int i = 0; i < @operator.outputCount; ++i)
		{
			var k = @operator[i];
			if(k == SwizzleOp.ZeroOp)
				result[i] = 0;
			else
				result[i] = input0[k];
		}
	}
}
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Mpr.Burst;

namespace Mpr.Expr
{
	public enum BinaryMathOp
	{
		Add,
		Sub,
		Mul,
		Div,
	}

	public interface IBTBinaryOp
	{
		BinaryMathOp Op { get; }
		int Apply(int a, int b);
		int2 Apply(int2 a, int2 b);
		int3 Apply(int3 a, int3 b);
		int4 Apply(int4 a, int4 b);
		float Apply(float a, float b);
		float2 Apply(float2 a, float2 b);
		float3 Apply(float3 a, float3 b);
		float4 Apply(float4 a, float4 b);
	}

	public struct BTBinaryOp_Add : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Add; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int Apply(int a, int b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int2 Apply(int2 a, int2 b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int3 Apply(int3 a, int3 b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int4 Apply(int4 a, int4 b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float Apply(float a, float b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float2 Apply(float2 a, float2 b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float3 Apply(float3 a, float3 b) => a + b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float4 Apply(float4 a, float4 b) => a + b;
	}

	public struct BTBinaryOp_Sub : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Sub; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int Apply(int a, int b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int2 Apply(int2 a, int2 b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int3 Apply(int3 a, int3 b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int4 Apply(int4 a, int4 b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float Apply(float a, float b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float2 Apply(float2 a, float2 b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float3 Apply(float3 a, float3 b) => a - b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float4 Apply(float4 a, float4 b) => a - b;
	}

	public struct BTBinaryOp_Mul : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Mul; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int Apply(int a, int b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int2 Apply(int2 a, int2 b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int3 Apply(int3 a, int3 b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int4 Apply(int4 a, int4 b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float Apply(float a, float b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float2 Apply(float2 a, float2 b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float3 Apply(float3 a, float3 b) => a * b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float4 Apply(float4 a, float4 b) => a * b;
	}

	public struct BTBinaryOp_Div : IBTBinaryOp
	{
		public BinaryMathOp Op { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BinaryMathOp.Div; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int Apply(int a, int b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int2 Apply(int2 a, int2 b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int3 Apply(int3 a, int3 b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public int4 Apply(int4 a, int4 b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float Apply(float a, float b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float2 Apply(float2 a, float2 b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float3 Apply(float3 a, float3 b) => a / b;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public float4 Apply(float4 a, float4 b) => a / b;
	}

	static partial class BTBinaryEval
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void Apply<TOp>(MathType type, Span<byte> left, Span<byte> right, Span<byte> result) where TOp : unmanaged, IBTBinaryOp
		{
			switch(type)
			{
				case MathType.Int: SpanMarshal.Cast<byte, int>(result)[0] = default(IntBinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, int>(left)[0], SpanMarshal.Cast<byte, int>(right)[0]); break;
				case MathType.Int2: SpanMarshal.Cast<byte, int2>(result)[0] = default(Int2BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, int2>(left)[0], SpanMarshal.Cast<byte, int2>(right)[0]); break;
				case MathType.Int3: SpanMarshal.Cast<byte, int3>(result)[0] = default(Int3BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, int3>(left)[0], SpanMarshal.Cast<byte, int3>(right)[0]); break;
				case MathType.Int4: SpanMarshal.Cast<byte, int4>(result)[0] = default(Int4BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, int4>(left)[0], SpanMarshal.Cast<byte, int4>(right)[0]); break;
				case MathType.Float: SpanMarshal.Cast<byte, float>(result)[0] = default(FloatBinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, float>(left)[0], SpanMarshal.Cast<byte, float>(right)[0]); break;
				case MathType.Float2: SpanMarshal.Cast<byte, float2>(result)[0] = default(Float2BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, float2>(left)[0], SpanMarshal.Cast<byte, float2>(right)[0]); break;
				case MathType.Float3: SpanMarshal.Cast<byte, float3>(result)[0] = default(Float3BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, float3>(left)[0], SpanMarshal.Cast<byte, float3>(right)[0]); break;
				case MathType.Float4: SpanMarshal.Cast<byte, float4>(result)[0] = default(Float4BinaryOp<TOp>).Apply(SpanMarshal.Cast<byte, float4>(left)[0], SpanMarshal.Cast<byte, float4>(right)[0]); break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Apply(MathType type, BinaryMathOp op, Span<byte> left, Span<byte> right, Span<byte> result)
		{
			switch(op)
			{
				case BinaryMathOp.Add: Apply<BTBinaryOp_Add>(type, left, right, result); break;
				case BinaryMathOp.Sub: Apply<BTBinaryOp_Sub>(type, left, right, result); break;
				case BinaryMathOp.Mul: Apply<BTBinaryOp_Mul>(type, left, right, result); break;
				case BinaryMathOp.Div: Apply<BTBinaryOp_Div>(type, left, right, result); break;
			}
		}

		struct IntBinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public int Apply(int a, int b) => default(TOp).Apply(a, b); }
		struct Int2BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public int2 Apply(int2 a, int2 b) => default(TOp).Apply(a, b); }
		struct Int3BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public int3 Apply(int3 a, int3 b) => default(TOp).Apply(a, b); }
		struct Int4BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public int4 Apply(int4 a, int4 b) => default(TOp).Apply(a, b); }

		struct FloatBinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public float Apply(float a, float b) => default(TOp).Apply(a, b); }
		struct Float2BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public float2 Apply(float2 a, float2 b) => default(TOp).Apply(a, b); }
		struct Float3BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public float3 Apply(float3 a, float3 b) => default(TOp).Apply(a, b); }
		struct Float4BinaryOp<TOp> where TOp : unmanaged, IBTBinaryOp { [MethodImpl(MethodImplOptions.AggressiveInlining)] public float4 Apply(float4 a, float4 b) => default(TOp).Apply(a, b); }
	}

}

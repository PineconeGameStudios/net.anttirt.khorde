using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Khorde.Expr
{
	[BurstCompile]
	public partial struct BinaryFloat
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryFloat, float, float>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryFloat2
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryFloat2, float2, float2>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryFloat3
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryFloat3, float3, float3>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryFloat4
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryFloat4, float4, float4>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryInt
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryInt, int, int>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryInt2
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryInt2, int2, int2>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryInt3
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryInt3, int3, int3>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct BinaryInt4
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<BinaryInt4, int4, int4>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct LengthFloat2
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<LengthFloat2, float2>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct LengthFloat3
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<LengthFloat3, float3>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile]
	public partial struct LengthFloat4
	{
	    [BurstCompile]
	    public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
	        ref NativeArray<byte> untypedResult)
	    {
	        EvalHelper.Evaluate<LengthFloat4, float4>(self, in ctx, outputIndex, ref untypedResult);
	    }
	}

	[BurstCompile] public partial struct Normalize2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Normalize2, float2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Normalize3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Normalize3, float3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Normalize4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Normalize4, float4>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Floor2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Floor2, float2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Floor3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Floor3, float3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Floor4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Floor4, float4>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Ceiling2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Ceiling2, float2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Ceiling3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Ceiling3, float3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Ceiling4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Ceiling4, float4>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct ToFloat2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<ToFloat2, int2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct ToFloat3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<ToFloat3, int3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct ToFloat4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<ToFloat4, int4>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Rescale2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Rescale2, float2, float>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Rescale3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Rescale3, float3, float>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct Rescale4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<Rescale4, float4, float>(self, in ctx, outputIndex, ref untypedResult); } }
}
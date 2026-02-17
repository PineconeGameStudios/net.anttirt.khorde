using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr
{
	[BurstCompile] public partial struct BreakInt2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt2, int2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct BreakInt3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt3, int3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct BreakInt4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt4, int4>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct BreakFloat2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt2, int2>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct BreakFloat3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt3, int3>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct BreakFloat4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<BreakInt4, int4>(self, in ctx, outputIndex, ref untypedResult); } }

	[BurstCompile] public partial struct MakeInt2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeInt2, int, int>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct MakeInt3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeInt3, int, int, int>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct MakeInt4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeInt4, int, int, int, int>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct MakeFloat2 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeFloat2, float, float>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct MakeFloat3 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeFloat3, float, float, float>(self, in ctx, outputIndex, ref untypedResult); } }
	[BurstCompile] public partial struct MakeFloat4 { [BurstCompile] public static unsafe void EvaluateFunc(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult) { EvalHelper.Evaluate<MakeFloat4, float, float, float, float>(self, in ctx, outputIndex, ref untypedResult); } }
}

using UnityEngine.Scripting;

namespace Khorde.Expr.Generated
{
	/// <summary>
	/// This is a generated class used for quickly loading type
	/// information at runtime. One is generated for each assembly
	/// that contains <see cref="IExpression"/> types.
	/// </summary>
	[Preserve]
	static unsafe class ExpressionTypeRegistry
	{
		public static readonly ExpressionTypeInfo[] ExpressionTypes = new[]
		{
			new ExpressionTypeInfo(typeof(BinaryFloat), BinaryFloat.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryFloat2), BinaryFloat2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryFloat3), BinaryFloat3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryFloat4), BinaryFloat4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryInt), BinaryInt.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryInt2), BinaryInt2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryInt3), BinaryInt3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryInt4), BinaryInt4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(Swizzle32x1), Swizzle32x1.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(Swizzle32x2), Swizzle32x2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(Swizzle32x3), Swizzle32x3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(Swizzle32x4), Swizzle32x4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(ReadComponentField), ReadComponentField.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(LookupComponentField), LookupComponentField.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryBool), BinaryBool.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(UnaryBool), UnaryBool.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(LengthFloat2), LengthFloat2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(LengthFloat3), LengthFloat3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(LengthFloat4), LengthFloat4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryCompareFloat), BinaryCompareFloat.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BinaryCompareInt), BinaryCompareInt.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(Variable), Variable.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(RandomFloat), RandomFloat.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(RandomFloat2Direction), RandomFloat2Direction.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(RandomFloat3Direction), RandomFloat3Direction.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(RandomInt), RandomInt.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakInt2), BreakInt2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakInt3), BreakInt3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakInt4), BreakInt4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakFloat2), BreakFloat2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakFloat3), BreakFloat3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(BreakFloat4), BreakFloat4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeInt2), MakeInt2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeInt3), MakeInt3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeInt4), MakeInt4.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeFloat2), MakeFloat2.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeFloat3), MakeFloat3.EvaluateFunc, true),
			new ExpressionTypeInfo(typeof(MakeFloat4), MakeFloat4.EvaluateFunc, true),
		};
	}
}
using UnityEngine.Scripting;

namespace Mpr.Expr.Generated;
    
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
        new ExpressionTypeInfo(typeof(TestLargeExpression), TestLargeExpression.EvaluateFunc, true),
        new ExpressionTypeInfo(typeof(TestManagedExpression), TestManagedExpression.EvaluateFunc, false),
        new ExpressionTypeInfo(typeof(ReadComponentField), ReadComponentField.EvaluateFunc, true),
        new ExpressionTypeInfo(typeof(LookupComponentField), LookupComponentField.EvaluateFunc, true),
        new ExpressionTypeInfo(typeof(BinaryBool), BinaryBool.EvaluateFunc, true),
        new ExpressionTypeInfo(typeof(UnaryBool), UnaryBool.EvaluateFunc, true),
    };
}
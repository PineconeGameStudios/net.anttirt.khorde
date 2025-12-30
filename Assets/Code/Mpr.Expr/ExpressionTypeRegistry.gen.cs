namespace Mpr.Expr.Generated;
    
/// <summary>
/// This is a generated class used for quickly loading type
/// information at runtime. One is generated for each assembly
/// that contains <see cref="IExpression"/> types.
/// </summary>
static class ExpressionTypeRegistry
{
    public static readonly ExpressionTypeInfo[] ExpressionTypes = new[]
    {
        ExpressionTypeInfo.Create<BinaryFloat>(true),
        ExpressionTypeInfo.Create<BinaryFloat2>(true),
        ExpressionTypeInfo.Create<BinaryFloat3>(true),
        ExpressionTypeInfo.Create<BinaryFloat4>(true),
        ExpressionTypeInfo.Create<BinaryInt>(true),
        ExpressionTypeInfo.Create<BinaryInt2>(true),
        ExpressionTypeInfo.Create<BinaryInt3>(true),
        ExpressionTypeInfo.Create<BinaryInt4>(true),
        ExpressionTypeInfo.Create<TestLargeExpression>(true),
        ExpressionTypeInfo.Create<TestManagedExpression>(false),
        ExpressionTypeInfo.Create<ReadComponentField>(true),
        ExpressionTypeInfo.Create<LookupComponentField>(true),
        ExpressionTypeInfo.Create<BinaryBool>(true),
        ExpressionTypeInfo.Create<UnaryBool>(true),
    };
}
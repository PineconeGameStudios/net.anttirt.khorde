namespace Mpr.Expr.Generated;
    
// one of these is generated separately for each assembly
static class ExpressionTypeRegistry
{
    public static readonly ExpressionTypeInfo[] ExpressionTypes = new[]
    {
        new ExpressionTypeInfo(typeof(BinaryFloat), true),
        new ExpressionTypeInfo(typeof(BinaryFloat2), true),
        new ExpressionTypeInfo(typeof(BinaryFloat3), true),
        new ExpressionTypeInfo(typeof(BinaryFloat4), true),
        new ExpressionTypeInfo(typeof(TestLargeExpression), true),
        new ExpressionTypeInfo(typeof(TestManagedExpression), false),
        new ExpressionTypeInfo(typeof(ReadComponentField), true),
        new ExpressionTypeInfo(typeof(LookupComponentField), true),
    };
}
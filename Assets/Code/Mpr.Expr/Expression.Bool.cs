using Unity.Burst;
using Unity.Collections;

namespace Mpr.Expr;

public enum BinaryBoolOp
{
    And,
    Or,
    Xor,
}

public partial struct BinaryBool : IExpression
{
    public ExpressionRef left, right;
    public BinaryBoolOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        bool left = this.left.Evaluate<bool>(in ctx);
        var result = untypedResult.SliceConvert<bool>();
        switch (@operator)
        {
            case BinaryBoolOp.And: result[0] = left && right.Evaluate<bool>(in ctx); break;
            case BinaryBoolOp.Or: result[0] = left || right.Evaluate<bool>(in ctx); break;
            case BinaryBoolOp.Xor: result[0] = left != right.Evaluate<bool>(in ctx); break;
        }
    }
}

public enum UnaryBoolOp
{
    Not,
}

public partial struct UnaryBool : IExpression
{
    public ExpressionRef operand;
    public UnaryBoolOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        var result = untypedResult.SliceConvert<bool>();
        switch (@operator)
        {
            case UnaryBoolOp.Not: result[0] = !operand.Evaluate<bool>(in ctx); break;
        }
    }
}

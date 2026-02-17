using Unity.Burst;
using Unity.Collections;

namespace Mpr.Expr
{
	public enum BinaryBoolOp
	{
	    And,
	    Or,
	    Xor,
	}

	public partial struct BinaryBool : IExpression<bool, bool>
	{
	    public ExpressionRef Input0 { get; set; }
	    public ExpressionRef Input1 { get; set; }
	    public BinaryBoolOp @operator;

	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, in bool left, in bool right, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        ref var result = ref untypedResult.AsSingle<bool>();
	        switch (@operator)
	        {
	            case BinaryBoolOp.And: result = left && right; break;
	            case BinaryBoolOp.Or: result = left || right; break;
	            case BinaryBoolOp.Xor: result = left != right; break;
	        }
	    }
	}

	public enum UnaryBoolOp
	{
	    Not,
	}

	public partial struct UnaryBool : IExpression<bool>
	{
	    public ExpressionRef Input0 { get; set; }
	    public UnaryBoolOp @operator;

	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, in bool operand, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        ref var result = ref untypedResult.AsSingle<bool>();
	        switch (@operator)
	        {
	            case UnaryBoolOp.Not: result = !operand; break;
	        }
	    }
	}
}

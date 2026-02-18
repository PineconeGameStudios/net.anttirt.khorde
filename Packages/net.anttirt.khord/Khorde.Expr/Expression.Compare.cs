using Unity.Burst;
using Unity.Collections;

namespace Khorde.Expr
{
	public enum BinaryCompareOp
	{
	    Less,
	    Greater,
	    LessOrEqual,
	    GreaterOrEqual,
	    Equal,
	    NotEqual,
	}

	public partial struct BinaryCompareFloat : IExpression<float, float>
	{
	    public ExpressionRef Input0 { get; set; }
	    public ExpressionRef Input1 { get; set; }
	    public BinaryCompareOp @operator;

	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, in float left, in float right, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        ref var result = ref untypedResult.AsSingle<bool>();
	        switch (@operator)
	        {
	            case BinaryCompareOp.Less: result = left < right; break;
	            case BinaryCompareOp.Greater: result = left > right; break;
	            case BinaryCompareOp.LessOrEqual: result = left <= right; break;
	            case BinaryCompareOp.GreaterOrEqual: result = left >= right; break;
	            case BinaryCompareOp.Equal: result = left == right; break;
	            case BinaryCompareOp.NotEqual: result = left != right; break;
	        }
	    }
	}

	public partial struct BinaryCompareInt : IExpression<int, int>
	{
	    public ExpressionRef Input0 { get; set; }
	    public ExpressionRef Input1 { get; set; }
	    public BinaryCompareOp @operator;

	    [BurstCompile]
	    public void Evaluate(in ExpressionEvalContext ctx, in int left, in int right, int outputIndex, ref NativeArray<byte> untypedResult)
	    {
	        ref var result = ref untypedResult.AsSingle<bool>();
	        switch (@operator)
	        {
	            case BinaryCompareOp.Less: result = left < right; break;
	            case BinaryCompareOp.Greater: result = left > right; break;
	            case BinaryCompareOp.LessOrEqual: result = left <= right; break;
	            case BinaryCompareOp.GreaterOrEqual: result = left >= right; break;
	            case BinaryCompareOp.Equal: result = left == right; break;
	            case BinaryCompareOp.NotEqual: result = left != right; break;
	        }
	    }
	}
}

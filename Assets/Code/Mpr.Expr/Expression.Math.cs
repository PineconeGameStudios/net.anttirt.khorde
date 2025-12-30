using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

public partial struct BinaryFloat : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        float left = this.left.Evaluate<float>(in ctx);
        float right = this.right.Evaluate<float>(in ctx);
        var result = untypedResult.SliceConvert<float>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
    
}

public partial struct BinaryFloat2 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        float2 left = this.left.Evaluate<float2>(in ctx);
        float2 right = this.right.Evaluate<float2>(in ctx);
        var result = untypedResult.SliceConvert<float2>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

public partial struct BinaryFloat3 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        float3 left = this.left.Evaluate<float3>(in ctx);
        float3 right = this.right.Evaluate<float3>(in ctx);
        var result = untypedResult.SliceConvert<float3>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

public partial struct BinaryFloat4 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        float4 left = this.left.Evaluate<float4>(in ctx);
        float4 right = this.right.Evaluate<float4>(in ctx);
        var result = untypedResult.SliceConvert<float4>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

public partial struct BinaryInt : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        int left = this.left.Evaluate<int>(in ctx);
        int right = this.right.Evaluate<int>(in ctx);
        var result = untypedResult.SliceConvert<int>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
    
}

public partial struct BinaryInt2 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        int2 left = this.left.Evaluate<int2>(in ctx);
        int2 right = this.right.Evaluate<int2>(in ctx);
        var result = untypedResult.SliceConvert<int2>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

public partial struct BinaryInt3 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        int3 left = this.left.Evaluate<int3>(in ctx);
        int3 right = this.right.Evaluate<int3>(in ctx);
        var result = untypedResult.SliceConvert<int3>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

public partial struct BinaryInt4 : IExpression
{
    public ExpressionRef left, right;
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        int4 left = this.left.Evaluate<int4>(in ctx);
        int4 right = this.right.Evaluate<int4>(in ctx);
        var result = untypedResult.SliceConvert<int4>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result[0] = left + right; break;
            case BinaryMathOp.Sub: result[0] = left - right; break;
            case BinaryMathOp.Mul: result[0] = left * right; break;
            case BinaryMathOp.Div: result[0] = left / right; break;
        }
    }
}

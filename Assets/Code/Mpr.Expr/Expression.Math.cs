using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

public partial struct BinaryFloat : IExpression<float, float>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in float left, in float right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<float>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
    
}

public partial struct BinaryFloat2 : IExpression<float2, float2>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in float2 left, in float2 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<float2>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}

public partial struct BinaryFloat3 : IExpression<float3, float3>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in float3 left, in float3 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<float3>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}

public partial struct BinaryFloat4 : IExpression<float4, float4>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in float4 left, in float4 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<float4>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}

public partial struct BinaryInt : IExpression<int, int>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in int left, in int right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<int>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
    
}

public partial struct BinaryInt2 : IExpression<int2, int2>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in int2 left, in int2 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<int2>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}

public partial struct BinaryInt3 : IExpression<int3, int3>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in int3 left, in int3 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<int3>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}

public partial struct BinaryInt4 : IExpression<int4, int4>
{
    public ExpressionRef Input0 { get; set; }
    public ExpressionRef Input1 { get; set; }
    public BinaryMathOp @operator;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in int4 left, in int4 right, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        ref var result = ref untypedResult.AsSingle<int4>();
        switch (@operator)
        {
            case BinaryMathOp.Add: result = left + right; break;
            case BinaryMathOp.Sub: result = left - right; break;
            case BinaryMathOp.Mul: result = left * right; break;
            case BinaryMathOp.Div: result = left / right; break;
        }
    }
}


using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Mpr.Expr;

public struct SwizzleOp
{
    public byte inputCount;
    public byte outputCount;
    public byte desc;

    public byte this[int index]
    {
        get => (byte)((desc >> (index * 2)) & 3);
        set => desc = (byte)((desc & ~(3 << (index * 2))) | (value << (index * 2)));
    }
}

/// <summary>
/// Swizzle 1-4 32-bit elements
/// </summary>
public partial struct Swizzle32 : IExpression
{
    public ExpressionRef input;
    public SwizzleOp @operator;

    [BurstCompile]
    public unsafe void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        int* inputData = stackalloc int[@operator.inputCount];
        
        {
            var inputSize = @operator.inputCount * 4;
            NativeSlice<byte> temp = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(inputData, 1, inputSize);
            input.Evaluate(in ctx, ref temp);
        }
        
        var result = untypedResult.SliceConvert<int>();

        for (int i = 0; i < @operator.outputCount; ++i)
            result[i] = inputData[@operator[i]];
    }
    
}

/// <summary>
/// Swizzle 1-4 64-bit elements
/// </summary>
public partial struct Swizzle64 : IExpression
{
    public ExpressionRef input;
    public SwizzleOp @operator;

    [BurstCompile]
    public unsafe void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        long* inputData = stackalloc long[@operator.inputCount];
        
        {
            var inputSize = @operator.inputCount * 8;
            NativeSlice<byte> temp = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(inputData, 1, inputSize);
            input.Evaluate(in ctx, ref temp);
        }
        
        var result = untypedResult.SliceConvert<long>();

        for (int i = 0; i < @operator.outputCount; ++i)
            result[i] = inputData[@operator[i]];
    }
    
}

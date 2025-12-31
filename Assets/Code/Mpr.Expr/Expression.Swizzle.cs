using Unity.Collections;
using Unity.Mathematics;

namespace Mpr.Expr;

public struct SwizzleOp
{
    public byte outputCount;
    public byte desc;

    public byte this[int index]
    {
        get => (byte)((desc >> (index * 2)) & 3);
        set => desc = (byte)((desc & ~(3 << (index * 2))) | (value << (index * 2)));
    }

    public static SwizzleOp Parse(string pattern)
    {
        var op = new SwizzleOp
        {
            outputCount = (byte)pattern.Length,
        };

        byte FieldToIndex(char field)
        {
            switch (char.ToLowerInvariant(field))
            {
                case 'x': case 'r': return 0;
                case 'y': case 'g': return 1;
                case 'z': case 'b': return 2;
                case 'w': case 'a': return 3;
                default: return 0;
            }
        }

        for (int i = 0; i < pattern.Length; ++i)
            op[i] = FieldToIndex(pattern[i]);

        return op;
    }
}

public partial struct Swizzle32x1 : IExpression<int>
{
    public ExpressionRef Input0 { get; set; }
    public SwizzleOp @operator;

    public unsafe void Evaluate(in ExpressionEvalContext ctx, in int input0, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        var result = untypedResult.Reinterpret<int>(1);
        fixed (int* inputData = &input0)
            for (int i = 0; i < @operator.outputCount; ++i)
                result[i] = inputData[@operator[i]];
    }
}

public partial struct Swizzle32x2 : IExpression<int2>
{
    public ExpressionRef Input0 { get; set; }
    public SwizzleOp @operator;

    public unsafe void Evaluate(in ExpressionEvalContext ctx, in int2 input0, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        var result = untypedResult.Reinterpret<int>(1);
        fixed (int* inputData = &input0.x)
            for (int i = 0; i < @operator.outputCount; ++i)
                result[i] = inputData[@operator[i]];
    }
}

public partial struct Swizzle32x3 : IExpression<int3>
{
    public ExpressionRef Input0 { get; set; }
    public SwizzleOp @operator;

    public unsafe void Evaluate(in ExpressionEvalContext ctx, in int3 input0, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        var result = untypedResult.Reinterpret<int>(1);
        fixed (int* inputData = &input0.x)
            for (int i = 0; i < @operator.outputCount; ++i)
                result[i] = inputData[@operator[i]];
    }
}

public partial struct Swizzle32x4 : IExpression<int4>
{
    public ExpressionRef Input0 { get; set; }
    public SwizzleOp @operator;

    public unsafe void Evaluate(in ExpressionEvalContext ctx, in int4 input0, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        var result = untypedResult.Reinterpret<int>(1);
        fixed (int* inputData = &input0.x)
            for (int i = 0; i < @operator.outputCount; ++i)
                result[i] = inputData[@operator[i]];
    }
}
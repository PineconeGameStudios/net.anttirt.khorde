using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Mpr.Expr;

public partial struct TestLargeExpression : IExpression
{
    public ExpressionRef input;
    public float4x4 matrix;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        float4 input = this.input.Evaluate<float4>(in ctx);
        var result = untypedResult.SliceConvert<float4>();
        result[0] = math.mul(matrix, input);
    }
}

public partial struct TestManagedExpression : IExpression
{
    public ExpressionRef x, y;
    public ExpressionObjectRef<Texture2D> texture;
    
    // TODO: could we somehow patch this to load the texture data
    // into a NativeArray so this could be accessed from burst and
    // job worker threads?
    // public NativeArray<Color32> textureData;

    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        // read a color from a texture
        var textureAsset = texture.Value;
        var result = untypedResult.SliceConvert<Color32>();
        result[0] = textureAsset.GetPixelData<Color32>(0)[textureAsset.width * y.Evaluate<int>(in ctx) + x.Evaluate<int>(in ctx)];
    }
}

// public partial struct TestBlobAssetReference : IExpression
// {
//     // TODO: this would need to be patched...
//     public BlobAssetReference<float4> asset;
//     
//     public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
//     {
//         var result = untypedResult.SliceConvert<float4>();
//         result[0] = asset.Value;
//     }
// }
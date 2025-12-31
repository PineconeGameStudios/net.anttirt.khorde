using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Mpr.Expr;

public partial struct TestLargeExpression : IExpression<float4>
{
    public ExpressionRef Input0 { get; }
    public float4x4 matrix;

    [BurstCompile]
    public void Evaluate(in ExpressionEvalContext ctx, in float4 input, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        untypedResult.AsSingle<float4>() = math.mul(matrix, input);
    }
}

public partial struct TestManagedExpression : IExpression<int, int>
{
    public ExpressionRef Input0 { get; }
    public ExpressionRef Input1 { get; }
    public ExpressionObjectRef<Texture2D> texture;
    
    // TODO: could we somehow patch this to load the texture data
    // into a NativeArray so this could be accessed from burst and
    // job worker threads?
    // public NativeArray<Color32> textureData;

    public void Evaluate(in ExpressionEvalContext ctx, in int x, in int y, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        // read a color from a texture
        var textureAsset = texture.Value;
        untypedResult.AsSingle<Color32>() = textureAsset.GetPixelData<Color32>(0)[textureAsset.width * y + x];
    }
}

// public partial struct TestBlobAssetReference : IExpression
// {
//     // TODO: this would need to be patched...
//     public BlobAssetReference<float4> asset;
//     
//     public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
//     {
//         var result = untypedResult.SliceConvert<float4>();
//         result[0] = asset.Value;
//     }
// }
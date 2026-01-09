using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mpr.Expr.Test;

[TestFixture]
public class BlobAssetTests
{
    [Test]
    public void Test_TextAsset_SafetyHandle()
    {
        var input = new byte[32];
        var asset = new TextAsset(input);
        try
        {
            var data = asset.GetData<byte>();

            Assert.DoesNotThrow(() => { data.ToArray(); });

            Object.DestroyImmediate(asset);

            Assert.Throws<ObjectDisposedException>(() => { data.ToArray(); });
        }
        finally
        {
            if(asset != null)
                Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void Test_BlobAsset_ValidationPtr()
    {
        var bb = new BlobBuilder(Allocator.Temp);
        bb.ConstructRoot<BlobTestContent>() = new()
        {
            position = new float3(1, 2, 3),
            rotation = quaternion.AxisAngle(new float3(1, 0, 0), 1.2f),
        };

        var blob = bb.CreateBlobAssetReference<BlobTestContent>(Allocator.Temp);
        Debug.Log(blob.Value);
        var blob2 = blob;
        blob.Dispose();
        Assert.Throws<InvalidOperationException>(() => { Debug.Log(blob2.Value); });
    }
    
    [Test]
    public void Test_BlobAsset_SafetyHandle()
    {
        var bb = new BlobBuilder(Allocator.Temp);
        bb.ConstructRoot<BlobTestContent>() = new()
        {
            position = new float3(1, 2, 3),
            rotation = quaternion.AxisAngle(new float3(1, 0, 0), 1.2f),
        };

        var asset = ScriptableObject.CreateInstance<TestBlobAsset>();
        
        try
        {
            asset.SetAssetData(bb, 1);
            
            Assert.IsTrue(asset.TryReadInPlace(1, out var handle));

            Assert.AreEqual(3, handle.ValueRO.position.z);
            
            asset.DestroyAssetImmediate();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                Debug.Log(handle.ValueRO.position.z);
            });
        }
        finally
        {
            if(asset != null)
                asset.DestroyAssetImmediate();
        }
    }
}
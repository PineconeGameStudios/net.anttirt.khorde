using System;
using Mpr.Blobs;
using Mpr.Expr.Authoring;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mpr.Expr.Test;

public unsafe class ExpressionTestBase
{
    protected World world;
    protected EntityManager em;
    protected ExpressionTestSystem testSystem;
    protected ExpressionBakingContext baker;
    protected ushort exprIndex;

    [SetUp]
    public virtual void SetUp()
    {
        ExpressionTypeManager.Initialize();
        
        world = new World("TestWorld");
        testSystem = world.GetOrCreateSystemManaged<ExpressionTestSystem>();
        em = world.EntityManager;
        baker ??= new ExpressionBakingContext(Allocator.Temp);
        exprIndex = 0;
    }

    [TearDown]
    public virtual void TearDown()
    {
        em = default;
        testSystem = null;
        world.Dispose();
        world = null;
        baker = null;
        exprIndex = 0;
    }
    
    protected ref TExpression Allocate<TExpression>(out ExpressionRef node)
        where TExpression : unmanaged, IExpressionBase
    {
        if (exprIndex >= baker.ExpressionCount)
            throw new InvalidOperationException("too many expressions; increase initial allocation size");

        node = ExpressionRef.Node(exprIndex, 0);
        return ref baker.CreateExpression<TExpression>(baker.GetStorage(exprIndex++));
    }

    protected ExpressionRef AddExpression<TExpression>(TExpression expression)
        where TExpression : unmanaged, IExpressionBase
    {
        Allocate<TExpression>(out var node) = expression;
        return node;
    }
}

[TestFixture]
public unsafe class ExpressionTests : ExpressionTestBase
{
    [Test]
    public void Test_Const()
    {
        baker.InitializeBake(0, 0);
        
        var True = baker.Const(true);
        var False = baker.Const(false);

        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        Assert.Greater(blob.Value.constants.Length, 0);
        Assert.That((IntPtr)blob.Value.constants.GetUnsafePtr(), Is.Not.Zero);
        var constants = blob.Value.GetConstants();
        Assert.Greater(constants.Length, 0);
        Assert.That((IntPtr)constants.GetUnsafePtr(), Is.Not.Zero);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(false, False.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, True.Evaluate<bool>(in ctx));
    }
    
    [Test]
    public void Test_Boolean()
    {
        baker.InitializeBake(6, 0);
        
        var True = baker.Const(true);
        var False = baker.Const(false);
        
        var n0 = AddExpression(new BinaryBool
        {
            Input0 = True,
            Input1 = False,
            @operator = BinaryBoolOp.And,
        });

        var n1 = AddExpression(new BinaryBool
        {
            Input0 = True,
            Input1 = False,
            @operator = BinaryBoolOp.Or,
        });

        var n2 = AddExpression(new BinaryBool
        {
            Input0 = True,
            Input1 = True,
            @operator = BinaryBoolOp.And,
        });
        
        var n3 = AddExpression(new UnaryBool
        {
            Input0 = True,
            @operator =  UnaryBoolOp.Not,
        });

        var n4 = AddExpression(new BinaryBool
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryBoolOp.And,
        });
        
        var n5 = AddExpression(new BinaryBool
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryBoolOp.Or,
        });

        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize();
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(false, n0.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n1.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n2.Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n3.Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n4.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n5.Evaluate<bool>(in ctx));
    }
    
    [Test]
    public void Test_Math()
    {
        baker.InitializeBake(6, 0);
        
        var n0 = AddExpression(new BinaryFloat2
        {
            Input0 = baker.Const(new float2(1, 2)),
            Input1 = baker.Const(new float2(2, 3)),
            @operator = BinaryMathOp.Add,
        });

        var n1 = AddExpression(new BinaryFloat2
        {
            Input0 = baker.Const(new float2(1, 2)),
            Input1 = baker.Const(new float2(2, 1)),
            @operator =  BinaryMathOp.Sub,
        });

        var n2 = AddExpression(new BinaryFloat2
        {
            Input0 = baker.Const(new float2(3, 3)),
            Input1 = baker.Const(new float2(2, 5)),
            @operator = BinaryMathOp.Mul,
        });
        
        var n3 = AddExpression(new BinaryFloat2
        {
            Input0 = baker.Const(new float2(0, 6)),
            Input1 = baker.Const(new float2(2, 2)),
            @operator = BinaryMathOp.Div,
        });

        var n4 = AddExpression(new BinaryFloat2
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryMathOp.Add,
        });
        
        var n5 = AddExpression(new BinaryFloat2
        {
            Input0 = n1,
            Input1 = n0,
            @operator = BinaryMathOp.Add,
        });

        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize();
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(new float2(3, 5), n0.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(-1, 1), n1.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(6, 15), n2.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(0, 3), n3.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(2, 6), n4.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(2, 6), n5.Evaluate<float2>(in ctx));
    }
    
    [Test]
    public void Test_Field()
    {
        baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Local, ComponentType.AccessMode.ReadOnly);
        baker.InitializeBake(1, 0);

        ref var rcf = ref Allocate<ReadComponentField>(out var n0);
        baker.Bake<TestComponent1>(ref rcf.typeInfo, ExpressionComponentLocation.Local);
        
        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize();

        Assert.Greater(
            blob.Value.expressions[0].storage.GetUnsafePtr<ReadComponentField>()->typeInfo.fields[0].length,
            0
            );
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);

        TestComponent1 tc1 = new TestComponent1
        {
            field0 = 42,
            field1 = true,
            field2 = false,
        };
        
        NativeArray<UnsafeComponentReference> componentPtrs = new  NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
        componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, componentPtrs, default);

        Assert.AreEqual(42, n0.WithOutputIndex(0).Evaluate<int>(in ctx));
        Assert.AreEqual(true, n0.WithOutputIndex(1).Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n0.WithOutputIndex(2).Evaluate<bool>(in ctx));
    }
    
    [Test]
    public void Test_Lookup()
    {
        baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Lookup, ComponentType.AccessMode.ReadOnly);
        baker.InitializeBake(1, 0);

        var otherEntity = em.CreateEntity();
        em.AddComponentData(otherEntity, new TestComponent1
        {
            field0 = 42,
            field1 = true,
            field2 = false,
        });
        
        ref var rcf = ref Allocate<LookupComponentField>(out var n0);
        baker.Bake<TestComponent1>(ref rcf.typeInfo, ExpressionComponentLocation.Lookup);
        rcf.Input0 = baker.Const(otherEntity);
        
        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize();

        Assert.Greater(
            blob.Value.expressions[0].storage.GetUnsafePtr<LookupComponentField>()->typeInfo.fields[0].length,
            0
        );
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);

        NativeArray<UntypedComponentLookup> componentLookups = new  NativeArray<UntypedComponentLookup>(1, Allocator.Temp);
        componentLookups[0] = testSystem.CheckedStateRef.GetUntypedComponentLookup<TestComponent1>(true);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, componentLookups);

        // HasComponent
        Assert.AreEqual(true, n0.WithOutputIndex(0).Evaluate<bool>(in ctx));
        
        const int FieldStartIndex = 1;
        Assert.AreEqual(42, n0.WithOutputIndex(FieldStartIndex + 0).Evaluate<int>(in ctx));
        Assert.AreEqual(true, n0.WithOutputIndex(FieldStartIndex + 1).Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n0.WithOutputIndex(FieldStartIndex + 2).Evaluate<bool>(in ctx));
    }
    
    [Test]
    public void Test_Swizzle()
    {
        baker.InitializeBake(18, 0);
        
        var n11 = AddExpression(new Swizzle32x1 { Input0 = baker.Const(1.0f), @operator = SwizzleOp.Parse("x") });
        var n12 = AddExpression(new Swizzle32x1 { Input0 = baker.Const(1.0f), @operator = SwizzleOp.Parse("xx") });
        var n13 = AddExpression(new Swizzle32x1 { Input0 = baker.Const(1.0f), @operator = SwizzleOp.Parse("xxx") });
        var n14 = AddExpression(new Swizzle32x1 { Input0 = baker.Const(1.0f), @operator = SwizzleOp.Parse("xxxx") });
        
        var n21 = AddExpression(new Swizzle32x2 { Input0 = baker.Const(new float2(1, 2)), @operator = SwizzleOp.Parse("y") });
        var n22 = AddExpression(new Swizzle32x2 { Input0 = baker.Const(new float2(1, 2)), @operator = SwizzleOp.Parse("yx") });
        var n23 = AddExpression(new Swizzle32x2 { Input0 = baker.Const(new float2(1, 2)), @operator = SwizzleOp.Parse("xyx") });
        var n24 = AddExpression(new Swizzle32x2 { Input0 = baker.Const(new float2(1, 2)), @operator = SwizzleOp.Parse("yxyx") });
        
        var n31 = AddExpression(new Swizzle32x3 { Input0 = baker.Const(new float3(1, 2, 3)), @operator = SwizzleOp.Parse("z") });
        var n32 = AddExpression(new Swizzle32x3 { Input0 = baker.Const(new float3(1, 2, 3)), @operator = SwizzleOp.Parse("zy") });
        var n33 = AddExpression(new Swizzle32x3 { Input0 = baker.Const(new float3(1, 2, 3)), @operator = SwizzleOp.Parse("zyx") });
        var n34 = AddExpression(new Swizzle32x3 { Input0 = baker.Const(new float3(1, 2, 3)), @operator = SwizzleOp.Parse("xzyx") });
        
        var n41 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("w") });
        var n42 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("wz") });
        var n43 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("wzy") });
        var n44 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("wzyx") });
        
        var n5 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("xyzw") });
        var n6 = AddExpression(new Swizzle32x4 { Input0 = baker.Const(new float4(1, 2, 3, 4)), @operator = SwizzleOp.Parse("xxyy") });

        var blob = baker.CreateAsset<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize();
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(1,                      n11.Evaluate<float>(in ctx));
        Assert.AreEqual(new float2(1, 1),       n12.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float3(1, 1, 1),    n13.Evaluate<float3>(in ctx));
        Assert.AreEqual(new float4(1, 1, 1, 1), n14.Evaluate<float4>(in ctx));

        Assert.AreEqual(2,                      n21.Evaluate<float>(in ctx));
        Assert.AreEqual(new float2(2, 1),       n22.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float3(1, 2, 1),    n23.Evaluate<float3>(in ctx));
        Assert.AreEqual(new float4(2, 1, 2, 1), n24.Evaluate<float4>(in ctx));

        Assert.AreEqual(3,                      n31.Evaluate<float>(in ctx));
        Assert.AreEqual(new float2(3, 2),       n32.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float3(3, 2, 1),    n33.Evaluate<float3>(in ctx));
        Assert.AreEqual(new float4(1, 3, 2, 1), n34.Evaluate<float4>(in ctx));

        Assert.AreEqual(4,                      n41.Evaluate<float>(in ctx));
        Assert.AreEqual(new float2(4, 3),       n42.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float3(4, 3, 2),    n43.Evaluate<float3>(in ctx));
        Assert.AreEqual(new float4(4, 3, 2, 1), n44.Evaluate<float4>(in ctx));

        Assert.AreEqual(new float4(1, 2, 3, 4),  n5.Evaluate<float4>(in ctx));
        Assert.AreEqual(new float4(1, 1, 2, 2),  n6.Evaluate<float4>(in ctx));
    }

    struct TestComponent1 : IComponentData
    {
        public int field0;
        public bool field1;
        public bool field2;
    }
}

[DisableAutoCreation]
public partial class ExpressionTestSystem : SystemBase
{
    protected override void OnUpdate()
    {
    }
}

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
using System;
using System.Collections.Generic;
using Mpr.Expr.Authoring;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;

namespace Mpr.Expr.Test;

[TestFixture]
public unsafe class ExpressionTests
{
    World world;
    EntityManager em;
    NativeList<byte> constStorage;
    Dictionary<Type, ulong> hashCache;
    ExpressionRef False;
    ExpressionRef True;
    ExpressionTestSystem testSystem;
    Entity bakedExpressionEntity;
    BlobBuilder blobBuilder;
    BlobExpressionData* root;
    ushort exprIndex;

    [SetUp]
    public void SetUp()
    {
        ExpressionTypeManager.Initialize();
        
        world = new World("TestWorld");

        testSystem = world.GetOrCreateSystemManaged<ExpressionTestSystem>();

        em = world.EntityManager;
        
        bakedExpressionEntity = em.CreateEntity();
        var strongRefs = em.AddBuffer<BlobExpressionObjectReference>(bakedExpressionEntity);
        var weakRefs = em.AddBuffer<BlobExpressionWeakObjectReference>(bakedExpressionEntity);

        constStorage = new NativeList<byte>(Allocator.Temp);
        hashCache = new Dictionary<Type, ulong>();
        exprIndex = 0;
        
        False = ExprAuthoring.WriteConstant2(false, constStorage);
        True = ExprAuthoring.WriteConstant2(true, constStorage);
        
        blobBuilder = new BlobBuilder(Allocator.Temp);
        ref var root = ref blobBuilder.ConstructRoot<BlobExpressionData>();
        fixed (BlobExpressionData* proot = &root)
            this.root = proot;
    }

    [TearDown]
    public void TearDown()
    {
        em = default;
        world.Dispose();
        world = null;
    }
    
    ref TExpression Allocate<TExpression>(BlobBuilderArray<ExpressionData> exprs, BlobBuilderArray<ulong> typeHashes, out ExpressionRef node)
        where TExpression : unmanaged, IExpressionBase
    {
        if (exprIndex >= exprs.Length)
            throw new InvalidOperationException("too many expressions; increase initial allocation size");
        
        ref var result = ref ExprAuthoring.Allocate<TExpression>(ref blobBuilder, new ExpressionStorageRef(ref exprs[exprIndex].storage, ref typeHashes[exprIndex]), hashCache);
        node = ExpressionRef.Node(exprIndex, 0);
        exprIndex++;
        return ref result;
    }

    ExpressionRef AddExpression<TExpression>(BlobBuilderArray<ExpressionData> exprs, BlobBuilderArray<ulong> typeHashes,
        TExpression expression)
        where TExpression : unmanaged, IExpressionBase
    {
        Allocate<TExpression>(exprs, typeHashes, out var node) = expression;
        return node;
    }

    [Test]
    public void Test_Const()
    {
        ExprAuthoring.BakeConstStorage(ref blobBuilder, ref *root, constStorage);
        var blob = blobBuilder.CreateBlobAssetReference<BlobExpressionData>(Allocator.Temp);

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
        var exprs = blobBuilder.Allocate(ref root->expressions, 6);
        var typeHashes = blobBuilder.Allocate(ref root->expressionTypeHashes, 6);

        var n0 = AddExpression(exprs, typeHashes, new BinaryBool
        {
            Input0 = True,
            Input1 = False,
            @operator = BinaryBoolOp.And,
        });

        var n1 = AddExpression(exprs, typeHashes, new BinaryBool
        {
            Input0 = True,
            Input1 = False,
            @operator = BinaryBoolOp.Or,
        });

        var n2 = AddExpression(exprs, typeHashes, new BinaryBool
        {
            Input0 = True,
            Input1 = True,
            @operator = BinaryBoolOp.And,
        });
        
        var n3 = AddExpression(exprs, typeHashes, new UnaryBool
        {
            Input0 = True,
            @operator =  UnaryBoolOp.Not,
        });

        var n4 = AddExpression(exprs, typeHashes, new BinaryBool
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryBoolOp.And,
        });
        
        var n5 = AddExpression(exprs, typeHashes, new BinaryBool
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryBoolOp.Or,
        });

        ExprAuthoring.BakeConstStorage(ref blobBuilder, ref *root, constStorage);
        var blob = blobBuilder.CreateBlobAssetReference<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize(default, default);
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);
        Assert.That(blob.Value.LoadingStatus, Is.EqualTo(ObjectLoadingStatus.Completed));
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(false, n0.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n1.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n2.Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n3.Evaluate<bool>(in ctx));
        Assert.AreEqual(false, n4.Evaluate<bool>(in ctx));
        Assert.AreEqual(true, n5.Evaluate<bool>(in ctx));
    }
    
    ExpressionRef Const<TConstant>(TConstant constant) where TConstant : unmanaged
        => ExprAuthoring.WriteConstant2(constant, constStorage);
    
    [Test]
    public void Test_Math()
    {
        var exprs = blobBuilder.Allocate(ref root->expressions, 6);
        var typeHashes = blobBuilder.Allocate(ref root->expressionTypeHashes, 6);
        
        var n0 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = Const(new float2(1, 2)),
            Input1 = Const(new float2(2, 3)),
            @operator = BinaryMathOp.Add,
        });

        var n1 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = Const(new float2(1, 2)),
            Input1 = Const(new float2(2, 1)),
            @operator =  BinaryMathOp.Sub,
        });

        var n2 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = Const(new float2(3, 3)),
            Input1 = Const(new float2(2, 5)),
            @operator = BinaryMathOp.Mul,
        });
        
        var n3 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = Const(new float2(0, 6)),
            Input1 = Const(new float2(2, 2)),
            @operator = BinaryMathOp.Div,
        });

        var n4 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = n0,
            Input1 = n1,
            @operator = BinaryMathOp.Add,
        });
        
        var n5 = AddExpression(exprs, typeHashes, new BinaryFloat2
        {
            Input0 = n1,
            Input1 = n0,
            @operator = BinaryMathOp.Add,
        });

        ExprAuthoring.BakeConstStorage(ref blobBuilder, ref *root, constStorage);
        var blob = blobBuilder.CreateBlobAssetReference<BlobExpressionData>(Allocator.Temp);

        blob.Value.RuntimeInitialize(default, default);
        
        Assert.IsTrue(blob.Value.IsRuntimeInitialized);
        Assert.That(blob.Value.LoadingStatus, Is.EqualTo(ObjectLoadingStatus.Completed));
        
        var ctx = new ExpressionEvalContext(ref blob.Value, default, default);

        Assert.AreEqual(new float2(3, 5), n0.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(-1, 1), n1.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(6, 15), n2.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(0, 3), n3.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(2, 6), n4.Evaluate<float2>(in ctx));
        Assert.AreEqual(new float2(2, 6), n5.Evaluate<float2>(in ctx));
    }

    struct TestComponent1 : IComponentData
    {
        public int field0;
        public bool field1;
        public bool field2;
    }
}

[DisableAutoCreation]
partial class ExpressionTestSystem : SystemBase
{
    protected override void OnUpdate()
    {
    }
}
using System;
using Mpr.Expr.Authoring;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using Unity.Transforms;

namespace Mpr.Expr.Test
{
	[TestFixture]
	public unsafe class GraphExpressionTests
	{
	    World world;
	    EntityManager em;
	    ExpressionTestSystem testSystem;
	    Entity bakedExpressionEntity;
	    GraphExpressionBakingContext baker;
	    ushort exprIndex;

	    [SetUp]
	    public void SetUp()
	    {
	        ExpressionTypeManager.Initialize();

	        world = new World("TestWorld");
	        testSystem = world.GetOrCreateSystemManaged<ExpressionTestSystem>();
	        em = world.EntityManager;
	        bakedExpressionEntity = em.CreateEntity();

	        var graph = GraphDatabase.LoadGraphForImporter<ExprSubgraph>("Assets/Prefabs/TestExpr.exprg");
        
	        baker = new GraphExpressionBakingContext(graph, Allocator.Temp);
	        exprIndex = 0;
	    }

	    [TearDown]
	    public void TearDown()
	    {
	        em = default;
	        testSystem = null;
	        world.Dispose();
	        world = null;
	        baker = null;
	        exprIndex = 0;
	    }

	    [Test]
	    public void Test_Graph()
	    {
	        var asset = baker.Build().CreateBlobAssetReference<BlobExpressionData>(Allocator.Temp);
	        Assert.IsFalse(asset.Value.IsRuntimeInitialized);
	        asset.Value.RuntimeInitialize();
	        Assert.IsTrue(asset.Value.IsRuntimeInitialized);
        
	        var lt = LocalTransform.FromPositionRotationScale(new float3(1, 2, 4), quaternion.identity, 1);
        
	        var componentPtrs = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
	        componentPtrs[0] = UnsafeComponentReference.Make(ref lt);
        
	        var ctx = new ExpressionEvalContext(ref asset.Value, componentPtrs, default, default, ref ExpressionBlackboardLayout.Empty);
        
	        Assert.That(asset.Value.outputs.Length, Is.EqualTo(1));
	        Assert.IsTrue(asset.Value.outputs[0].TryEvaluate<float>(in ctx, out var result));
	        Assert.AreEqual(3, result);
	    }
	}
}
using System;
using Mpr.Expr;
using Mpr.Query.Authoring;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Mpr.Query.Test;

[TestFixture]
public class QueryTests
{
    World world;
    EntityManager entityManager;
    QueryBakingContext baker;
    Entity resultsHolder;
    DynamicBuffer<QSResultItemStorage> untypedResults;

    [SetUp]
    public void Setup()
    {
        world = new World("Test");
        entityManager = world.EntityManager;
        var graph = GraphDatabase.LoadGraphForImporter<QueryGraph>("Assets/Prefabs/TestQuery.queryg");
        baker = new QueryBakingContext(graph, Allocator.Temp);
        resultsHolder = entityManager.CreateEntity(typeof(QSResultItemStorage));
        untypedResults = entityManager.GetBuffer<QSResultItemStorage>(resultsHolder);
    }

    [TearDown]
    public void TearDown()
    {
        world.Dispose();
    }

    [Test]
    public void Test_QueryExecute()
    {
        var asset = baker.Build().CreateBlobAssetReference<QSData>(Allocator.Temp);
        
        var components = new NativeArray<UnsafeComponentReference>(0,  Allocator.Temp);
        var entityQueries = new NativeArray<QSEntityQueryReference>(0, Allocator.Temp);
        var queryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(0,  Allocator.Temp);
        
        var qctx = new QueryExecutionContext(ref asset.Value, components, queryResultLookup);
        
        qctx.Execute<Entity>(untypedResults);

        var results = untypedResults.AsResultArray<Entity>();
    }
}
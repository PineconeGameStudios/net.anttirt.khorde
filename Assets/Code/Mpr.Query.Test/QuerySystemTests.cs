using System.Linq;
using Mpr.Expr;
using Mpr.Game;
using Mpr.Query.Authoring;
using Mpr.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Mpr.Query.Test
{
	public class QuerySystemTests : ECSTestsFixture
	{
	    [Test]
	    public void Test_QuerySystem()
	    {
	        ExpressionTypeManager.Initialize();

	        var graph = GraphDatabase.LoadGraphForImporter<QueryGraph>("Assets/Prefabs/TestQuery.queryg");
	        var baker = new QueryBakingContext(graph, Allocator.Temp);
	        var entityManager = World.EntityManager;
        
	        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, typeof(QuerySystem));
	        Assert.That(World.GetExistingSystem<QuerySystem>(), Is.Not.EqualTo(default(SystemHandle)));

	        World.Update();

	        var eqb = new EntityQueryBuilder(Allocator.Temp)
	            .WithAll<QuerySystemAssets>()
	            .WithOptions(EntityQueryOptions.IncludeSystems)
	            .Build(entityManager);
        
	        Assert.That(eqb.CalculateEntityCount(), Is.EqualTo(1));
        
	        var builder = baker.Build();
        
	        var asset = ScriptableObject.CreateInstance<QueryGraphAsset>();
	        asset.SetAssetData(builder, QSData.SchemaVersion);
	        asset.entityQueries = baker.EntityQueries.ToList();
	        asset.GetValue(QSData.SchemaVersion).exprData.RuntimeInitialize();
        
	        var querier = entityManager.CreateEntity(
	            typeof(QSResultItemStorage),
	            typeof(QueryAssetRegistration),
	            typeof(PendingQuery),
	            typeof(LocalTransform),
	            typeof(ExpressionBlackboardStorage),
	            typeof(ExpressionBlackboardLayouts)
	        );

	        var reg = new QueryAssetRegistration();
	        reg.Add(asset);
	        entityManager.SetSharedComponent(querier, reg);
	        entityManager.SetComponentData(querier, LocalTransform.FromPosition(new float3(-29, -31, 0)));
	        entityManager.SetComponentData(querier, new PendingQuery { query = asset });
	        entityManager.SetComponentEnabled<PendingQuery>(querier, true);
        
	        var player0 = entityManager.CreateEntity(typeof(LocalTransform), typeof(PlayerController));
	        entityManager.SetComponentData(player0, LocalTransform.FromPosition(new float3(30, 30, 0)));
        
	        var player1 = entityManager.CreateEntity(typeof(LocalTransform), typeof(PlayerController));
	        entityManager.SetComponentData(player1, LocalTransform.FromPosition(new float3(-30, -30, 0)));
        
	        World.Update();
        
	        var results = entityManager.GetBuffer<QSResultItemStorage>(querier).AsResultArray<Entity>();
	        Assert.That(results.Length, Is.GreaterThan(0));
	        Assert.That(results[0], Is.EqualTo(player1));

	        entityManager.GetBuffer<QSResultItemStorage>(querier).Clear();
        
	        results = entityManager.GetBuffer<QSResultItemStorage>(querier).AsResultArray<Entity>();
	        Assert.That(results.Length, Is.EqualTo(0));
        
	        // should not run query again
	        World.Update();
        
	        results = entityManager.GetBuffer<QSResultItemStorage>(querier).AsResultArray<Entity>();
	        Assert.That(results.Length, Is.EqualTo(0));
	    }
	}
}
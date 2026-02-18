using Khorde.Expr;
using Khorde.Query.Authoring;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using Unity.Transforms;
using Hash128 = Unity.Entities.Hash128;

namespace Khorde.Query.Test
{
	[TestFixture]
	public class QueryTests
	{
		World world;
		EntityManager entityManager;
		QueryBakingContext baker;
		Entity resultsHolder;
		DynamicBuffer<ExpressionBlackboardStorage> blackboard => entityManager.GetBuffer<ExpressionBlackboardStorage>(resultsHolder);
		DynamicBuffer<QSResultItemStorage> untypedResults => entityManager.GetBuffer<QSResultItemStorage>(resultsHolder);
		TestSystem testSystem;

		[SetUp]
		public void Setup()
		{
			world = new World("Test");
			entityManager = world.EntityManager;
			var graph = GraphDatabase.LoadGraphForImporter<QueryGraph>("Packages/net.anttirt.khord/Khorde.Query.Test/TestAssets/TestQuery.queryg");
			baker = new QueryBakingContext(graph, Allocator.Temp);
			resultsHolder = entityManager.CreateEntity(typeof(QSResultItemStorage), typeof(ExpressionBlackboardStorage));
			testSystem = world.GetOrCreateSystemManaged<TestSystem>();
		}

		[TearDown]
		public void TearDown()
		{
			world.Dispose();
		}

		[Test]
		public void Test_QueryExecute()
		{
			ExpressionTypeManager.Initialize();

			var asset = baker.Build().CreateBlobAssetReference<QSData>(Allocator.Temp);
			asset.Value.exprData.RuntimeInitialize();

			var player0 = entityManager.CreateEntity(typeof(LocalTransform), typeof(TestPlayerController));
			entityManager.SetComponentData(player0, LocalTransform.FromPosition(new float3(30, 30, 0)));

			var player1 = entityManager.CreateEntity(typeof(LocalTransform), typeof(TestPlayerController));
			entityManager.SetComponentData(player1, LocalTransform.FromPosition(new float3(-30, -30, 0)));

			var queryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(0, Allocator.Temp);
			foreach(var entityQuery in baker.EntityQueries)
			{
				var entityQueryResults = entityQuery.CreateEntityQuery(entityManager).ToEntityArray(Allocator.Temp);
				var list = queryResultLookup[entityQuery.DataHash] = new(entityQueryResults.Length, Allocator.Temp);
				list.CopyFrom(entityQueryResults);
			}

			LocalTransform lt = LocalTransform.FromPosition(new float3(-29, -31, 0));

			var components = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
			components[0] = UnsafeComponentReference.Make(ref lt);

			var lookups = new NativeArray<UntypedComponentLookup>(1, Allocator.Temp);
			lookups[0] = this.testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);

			var qctx = new QueryExecutionContext(ref asset.Value, components, lookups, queryResultLookup);

			qctx.Execute<Entity>(blackboard, ref ExpressionBlackboardLayout.Empty, untypedResults, default);

			var results = untypedResults.AsResultArray<Entity>();
			Assert.That(results.Length, Is.GreaterThan(0));
			Assert.That(results[0], Is.EqualTo(player1));
		}
	}

	[DisableAutoCreation]
	partial class TestSystem : SystemBase
	{
		protected override void OnUpdate()
		{
		}
	}
}
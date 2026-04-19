using System;
using System.Collections.Generic;
using System.Linq;
using Khorde.Behavior.Authoring;
using Khorde.Behavior.Systems;
using Khorde.Expr;
using Khorde.Query;
using Khorde.Query.Authoring;
using Khorde.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using static Khorde.Behavior.BTExec;
using Event = Khorde.Behavior.BTExecTrace.Event;

namespace Khorde.Behavior.Test
{
	public class BehaviorTreeSystemTests : ECSTestsFixture
	{
		List<UnityEngine.Object> created = new();

		[TearDown]
		public override void TearDown()
		{
			base.TearDown();

			foreach(var obj in created)
				if(obj != null)
					UnityEngine.Object.DestroyImmediate(obj);

			created.Clear();
		}

		[Test]
		public void Test_RunQuery()
		{
			ExpressionTypeManager.Initialize();

			var entityManager = World.EntityManager;

			var queryGraph = GraphDatabase.LoadGraphForImporter<QueryGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/QG_BTTest.queryg");
			var queryBaker = new QueryBakingContext(queryGraph, Allocator.Temp);

			var btGraph = GraphDatabase.LoadGraphForImporter<BehaviorTreeGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Query.btg");
			var btBaker = new BTBakingContext(btGraph, Allocator.Temp);

			DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World
				, typeof(QuerySystem)
				, typeof(BehaviorTreeUpdateSystem)
				, typeof(Unity.NetCode.PredictedSimulationSystemGroup)
				);
			Assert.That(World.GetExistingSystem<QuerySystem>(), Is.Not.EqualTo(default(SystemHandle)));
			Assert.That(World.GetExistingSystem<BehaviorTreeUpdateSystem>(), Is.Not.EqualTo(default(SystemHandle)));

			World.Update();

			var eqb = new EntityQueryBuilder(Allocator.Temp)
				.WithAll<QuerySystemAssets>()
				.WithOptions(EntityQueryOptions.IncludeSystems)
				.Build(entityManager);

			Assert.That(eqb.CalculateEntityCount(), Is.EqualTo(1));

			var queryBuilder = queryBaker.Build();
			var queryAsset = ScriptableObject.CreateInstance<QueryGraphAsset>();
			created.Add(queryAsset);
			queryAsset.SetAssetData(queryBuilder, QSData.SchemaVersion);
			queryAsset.entityQueries = queryBaker.EntityQueries.ToList();

			if(!queryAsset.TryReadInPlace(QSData.SchemaVersion, out var queryRef))
				throw new InvalidOperationException();

			queryRef.ValueRW.exprData.RuntimeInitialize(World.Unmanaged);

			var btBuilder = btBaker.Build();
			var btAsset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
			created.Add(btAsset);
			btAsset.SetAssetData(btBuilder, BTData.SchemaVersion);
			btAsset.Queries.Add(queryAsset);

			var querier = entityManager.CreateEntity(
				typeof(QSResultItemStorage),
				typeof(QueryAssetRegistration),
				typeof(PendingQuery),
				typeof(LocalTransform),
				typeof(ExpressionBlackboardStorage),
				typeof(ExpressionBlackboardLayouts),
				typeof(BehaviorTree),
				typeof(BTThread),
				typeof(BTStackFrame),
				typeof(BTExecTrace),
				typeof(BTState),
				typeof(BTInvokeQueue)
			);

			entityManager.SetComponentEnabled<BTInvokeQueue>(querier, false);

			var reg = new QueryAssetRegistration();
			reg.Add(queryRef.Reference);
			foreach(var eq in queryAsset.entityQueries)
			{
				if(!eq.TryReadInPlace(Blobs.BlobEntityQueryDesc.SchemaVersion, out var eqRef))
					throw new InvalidOperationException();
				reg.Add(eqRef.Reference);
			}

			entityManager.SetSharedComponent(querier, reg);
			entityManager.SetComponentData(querier, LocalTransform.FromPosition(new float3(-29, -31, 0)));
			entityManager.SetComponentData(querier, new PendingQuery { query = queryRef.Reference });
			entityManager.SetComponentEnabled<PendingQuery>(querier, true);

			if(!btAsset.TryReadInPlace(BTData.SchemaVersion, out var btData))
				throw new InvalidOperationException();

			entityManager.SetSharedComponent(querier, new BehaviorTree { tree = btData.Reference, });

			var bakedLayout = BehaviorTreeAuthoring.BakeLayout(btAsset, entityManager.GetBuffer<ExpressionBlackboardStorage>(querier), Allocator.Temp, dumpLayout: true);
			entityManager.SetSharedComponent(querier, new ExpressionBlackboardLayouts { asset = bakedLayout });

			var target = entityManager.CreateEntity(typeof(LocalToWorld));

			World.Update();

			var trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Nop, 0, 0, Event.Spawn)
				, Trace(BTExecType.Root, 1, 1, Event.Resume)
				, Trace(BTExecType.Root, 1, 1, Event.Call)
				, Trace(BTExecType.Query, 4, 2, Event.Wait)
				);

			World.Update();

			trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Query, 4, 2, Event.Resume)
				, Trace(BTExecType.Query, 4, 2, Event.Call)
				, Trace(BTExecType.WriteVar, 2, 3, Event.Return)
				, Trace(BTExecType.Query, 4, 2, Event.Return)
				, Trace(BTExecType.Root, 1, 1, Event.Call)
				, Trace(BTExecType.Query, 4, 2, Event.Wait)
				);
		}

		[Test]
		public void Test_RunQuery_Retry()
		{
			ExpressionTypeManager.Initialize();

			var entityManager = World.EntityManager;

			var queryGraph = GraphDatabase.LoadGraphForImporter<QueryGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/QG_BTTest.queryg");
			var queryBaker = new QueryBakingContext(queryGraph, Allocator.Temp);

			var btGraph = GraphDatabase.LoadGraphForImporter<BehaviorTreeGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_QueryRetry.btg");
			var btBaker = new BTBakingContext(btGraph, Allocator.Temp);

			DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World
				, typeof(QuerySystem)
				, typeof(BehaviorTreeUpdateSystem)
				, typeof(Unity.NetCode.PredictedSimulationSystemGroup)
				);
			Assert.That(World.GetExistingSystem<QuerySystem>(), Is.Not.EqualTo(default(SystemHandle)));
			Assert.That(World.GetExistingSystem<BehaviorTreeUpdateSystem>(), Is.Not.EqualTo(default(SystemHandle)));

			World.Update();

			var eqb = new EntityQueryBuilder(Allocator.Temp)
				.WithAll<QuerySystemAssets>()
				.WithOptions(EntityQueryOptions.IncludeSystems)
				.Build(entityManager);

			Assert.That(eqb.CalculateEntityCount(), Is.EqualTo(1));

			var queryBuilder = queryBaker.Build();
			var queryAsset = ScriptableObject.CreateInstance<QueryGraphAsset>();
			created.Add(queryAsset);
			queryAsset.SetAssetData(queryBuilder, QSData.SchemaVersion);
			queryAsset.entityQueries = queryBaker.EntityQueries.ToList();

			if(!queryAsset.TryReadInPlace(QSData.SchemaVersion, out var queryRef))
				throw new InvalidOperationException();

			queryRef.ValueRW.exprData.RuntimeInitialize(World.Unmanaged);

			var btBuilder = btBaker.Build();
			var btAsset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
			created.Add(btAsset);
			btAsset.SetAssetData(btBuilder, BTData.SchemaVersion);
			btAsset.Queries.Add(queryAsset);

			var querier = entityManager.CreateEntity(
				typeof(QSResultItemStorage),
				typeof(QueryAssetRegistration),
				typeof(PendingQuery),
				typeof(LocalTransform),
				typeof(ExpressionBlackboardStorage),
				typeof(ExpressionBlackboardLayouts),
				typeof(BehaviorTree),
				typeof(BTThread),
				typeof(BTStackFrame),
				typeof(BTExecTrace),
				typeof(BTState),
				typeof(BTInvokeQueue)
			);

			entityManager.SetComponentEnabled<BTInvokeQueue>(querier, false);

			var reg = new QueryAssetRegistration();
			reg.Add(queryRef.Reference);
			foreach(var eq in queryAsset.entityQueries)
			{
				if(!eq.TryReadInPlace(Blobs.BlobEntityQueryDesc.SchemaVersion, out var eqRef))
					throw new InvalidOperationException();
				reg.Add(eqRef.Reference);
			}

			entityManager.SetSharedComponent(querier, reg);
			entityManager.SetComponentData(querier, LocalTransform.FromPosition(new float3(-29, -31, 0)));
			entityManager.SetComponentData(querier, new PendingQuery { query = queryRef.Reference });
			entityManager.SetComponentEnabled<PendingQuery>(querier, true);

			if(!btAsset.TryReadInPlace(BTData.SchemaVersion, out var btData))
				throw new InvalidOperationException();

			entityManager.SetSharedComponent(querier, new BehaviorTree { tree = btData.Reference, });

			var bakedLayout = BehaviorTreeAuthoring.BakeLayout(btAsset, entityManager.GetBuffer<ExpressionBlackboardStorage>(querier), Allocator.Temp, dumpLayout: true);
			entityManager.SetSharedComponent(querier, new ExpressionBlackboardLayouts { asset = bakedLayout });

			World.Update();

			var trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Nop,      0, 0, Event.Spawn)
				, Trace(BTExecType.Root,     1,   1, Event.Resume)
				, Trace(BTExecType.Root,     1,   1, Event.Call)
				, Trace(BTExecType.Query,    3,     2, Event.Wait)
				);

			World.Update();

			trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Query,    3,     2, Event.Resume)
				, Trace(BTExecType.Query,    3,     2, Event.Yield) // free query lock
				);

			var target = entityManager.CreateEntity(typeof(LocalToWorld));

			World.Update();

			trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Query,    3,     2, Event.Resume)
				, Trace(BTExecType.Query,    3,     2, Event.Wait) // try again
				);

			World.Update();

			trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Query,    3,     2, Event.Resume)
				, Trace(BTExecType.Query,    3,     2, Event.Call)
				, Trace(BTExecType.WriteVar, 2,       3, Event.Return)
				, Trace(BTExecType.Query,    3,     2, Event.Return)
				, Trace(BTExecType.Root,     1,   1, Event.Call)
				, Trace(BTExecType.Query,    3,     2, Event.Wait)
				);

		}

		[Test]
		public void Test_Invoke()
		{
			ExpressionTypeManager.Initialize();

			var entityManager = World.EntityManager;

			var btGraph = GraphDatabase.LoadGraphForImporter<BehaviorTreeGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Invoke.btg");
			var btBaker = new BTBakingContext(btGraph, Allocator.Temp);

			DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World
				, typeof(BehaviorTreeUpdateSystem)
				, typeof(BehaviorTreeActionSystem)
				, typeof(Unity.NetCode.PredictedSimulationSystemGroup)
				);

			Assert.That(World.GetExistingSystem<BehaviorTreeUpdateSystem>(), Is.Not.EqualTo(default(SystemHandle)));
			Assert.That(World.GetExistingSystem<BehaviorTreeActionSystem>(), Is.Not.EqualTo(default(SystemHandle)));

			World.Update();

			var btBuilder = btBaker.Build();
			var btAsset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
			created.Add(btAsset);
			btAsset.SetAssetData(btBuilder, BTData.SchemaVersion);
			var action = AssetDatabase.LoadAssetAtPath<BTAction_TestCreateEntity>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BTAction_TestCreateEntity.asset");

			Assert.NotNull(action);

			btAsset.Actions.Add(action);

			var querier = entityManager.CreateEntity(
				typeof(QSResultItemStorage),
				typeof(QueryAssetRegistration),
				typeof(PendingQuery),
				typeof(LocalTransform),
				typeof(ExpressionBlackboardStorage),
				typeof(ExpressionBlackboardLayouts),
				typeof(BehaviorTree),
				typeof(BTThread),
				typeof(BTStackFrame),
				typeof(BTExecTrace),
				typeof(BTState),
				typeof(BTInvokeQueue),
				typeof(BehaviorTreeActionRef)
			);

			entityManager.SetComponentEnabled<BTInvokeQueue>(querier, false);

			entityManager.GetBuffer<BehaviorTreeActionRef>(querier).Add(new BehaviorTreeActionRef { value = btAsset.Actions[0] });

			if(!btAsset.TryReadInPlace(BTData.SchemaVersion, out var btData))
				throw new InvalidOperationException();

			entityManager.SetSharedComponent(querier, new BehaviorTree { tree = btData.Reference, });

			var bakedLayout = BehaviorTreeAuthoring.BakeLayout(btAsset, entityManager.GetBuffer<ExpressionBlackboardStorage>(querier), Allocator.Temp, dumpLayout: true);
			entityManager.SetSharedComponent(querier, new ExpressionBlackboardLayouts { asset = bakedLayout });

			var target = entityManager.CreateEntity(typeof(LocalToWorld));

			var query = World.EntityManager.CreateEntityQuery(typeof(ActionTestComponent));

			Assert.AreEqual(0, query.CalculateEntityCount());

			// first update places action in the queue
			World.Update();

			var trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Nop, 0, 0, Event.Spawn)
				, Trace(BTExecType.Root, 1, 1, Event.Resume)
				, Trace(BTExecType.Root, 1, 1, Event.Call)
				, Trace(BTExecType.Invoke, 2, 2, Event.Return)
				, Trace(BTExecType.Root, 1, 1, Event.Yield)
				);

			// second update actually performs the action (and places another action in the queue)
			World.Update();

			Assert.AreEqual(1, query.CalculateEntityCount());

			Assert.AreEqual(42, query.GetSingleton<ActionTestComponent>().value);

			// third update performs the action again (and places a third action in the queue)
			World.Update();

			Assert.AreEqual(2, query.CalculateEntityCount());
		}

		[Test]
		public void Test_LookupWrite()
		{
			ExpressionTypeManager.Initialize();

			var entityManager = World.EntityManager;

			var btGraph = GraphDatabase.LoadGraphForImporter<BehaviorTreeGraph>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_LookupWrite.btg");
			var btBaker = new BTBakingContext(btGraph, Allocator.Temp);

			DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World
				, typeof(BehaviorTreeUpdateSystem)
				, typeof(BehaviorTreeActionSystem)
				, typeof(Unity.NetCode.PredictedSimulationSystemGroup)
				);

			Assert.That(World.GetExistingSystem<BehaviorTreeUpdateSystem>(), Is.Not.EqualTo(default(SystemHandle)));
			Assert.That(World.GetExistingSystem<BehaviorTreeActionSystem>(), Is.Not.EqualTo(default(SystemHandle)));

			World.Update();

			var btBuilder = btBaker.Build();
			var btAsset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
			created.Add(btAsset);
			btAsset.SetAssetData(btBuilder, BTData.SchemaVersion);

			var querier = entityManager.CreateEntity(
				typeof(QSResultItemStorage),
				typeof(QueryAssetRegistration),
				typeof(PendingQuery),
				typeof(LocalTransform),
				typeof(ExpressionBlackboardStorage),
				typeof(ExpressionBlackboardLayouts),
				typeof(BehaviorTree),
				typeof(BTThread),
				typeof(BTStackFrame),
				typeof(BTExecTrace),
				typeof(BTState),
				typeof(BTInvokeQueue),
				typeof(BehaviorTreeActionRef)
			);

			entityManager.SetComponentEnabled<BTInvokeQueue>(querier, false);

			if(!btAsset.TryReadInPlace(BTData.SchemaVersion, out var btData))
				throw new InvalidOperationException();

			entityManager.SetSharedComponent(querier, new BehaviorTree { tree = btData.Reference, });

			var bakedLayout = BehaviorTreeAuthoring.BakeLayout(btAsset, entityManager.GetBuffer<ExpressionBlackboardStorage>(querier), Allocator.Temp, dumpLayout: true);
			entityManager.SetSharedComponent(querier, new ExpressionBlackboardLayouts { asset = bakedLayout });

			var otherEntity = entityManager.CreateEntity(typeof(TestMoveTarget));

			entityManager.GetBuffer<ExpressionBlackboardStorage>(querier).AsNativeArray().ReinterpretStore<Entity>(0, otherEntity);

			Assert.AreEqual(0.0f, entityManager.GetComponentData<TestMoveTarget>(otherEntity).tolerance);

			World.Update();

			Assert.AreEqual(5.0f, entityManager.GetComponentData<TestMoveTarget>(otherEntity).tolerance);
		}

		void AssertTrace(DynamicBuffer<BTExecTrace> trace, params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, 0, depth, 0);

		static BTExecTrace Trace(int threadId, BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, threadId, depth, 0);
	}
}

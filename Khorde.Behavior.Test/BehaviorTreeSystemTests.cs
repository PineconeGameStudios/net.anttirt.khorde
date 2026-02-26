using System;
using System.Collections.Generic;
using System.Linq;
using Khorde.Behavior.Authoring;
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
using UnityEngine;
using static Khorde.Behavior.BTExec;
using Event = Khorde.Behavior.BTExecTrace.Event;

namespace Khorde.Behavior.Test
{
	public class BehaviorTreeSystemTests : ECSTestsFixture
	{
		List<UnityEngine.Object> created = new();

		[Test]
		public void Test_RunQuery()
		{
			try
			{
				Test_RunQuery_Impl();
			}
			finally
			{
				foreach(var obj in created)
					if(obj != null)
						UnityEngine.Object.DestroyImmediate(obj);
			}
		}

		public void Test_RunQuery_Impl()
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
				typeof(BTState)
			);

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
				, Trace(BTExecType.Root, 1, 1, Event.Start)
				, Trace(BTExecType.Root, 1, 1, Event.Call)
				, Trace(BTExecType.Query, 4, 2, Event.Wait)
				);

			World.Update();

			trace = entityManager.GetBuffer<BTExecTrace>(querier);

			AssertTrace(trace
				, Trace(BTExecType.Query, 4, 2, Event.Start)
				, Trace(BTExecType.Query, 4, 2, Event.Call)
				, Trace(BTExecType.WriteVar, 2, 3, Event.Return)
				, Trace(BTExecType.Query, 4, 2, Event.Return)
				, Trace(BTExecType.Root, 1, 1, Event.Call)
				, Trace(BTExecType.Query, 4, 2, Event.Wait)
				);
		}

		void AssertTrace(DynamicBuffer<BTExecTrace> trace, params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, 0, depth, 0);

		static BTExecTrace Trace(int threadId, BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, threadId, depth, 0);
	}
}

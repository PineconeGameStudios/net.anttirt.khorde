using Mpr.Expr;
using Mpr.Query;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using static Mpr.Behavior.BTExec;
using static Mpr.Behavior.BTExecTrace;

namespace Mpr.Behavior.Test
{
	[TestFixture]
	public class BehaviorTreeGraphTests
	{
		World world;
		EntityManager em;
		Entity testEntity;
		DynamicBuffer<BTStackFrame> stack => em.GetBuffer<BTStackFrame>(testEntity);
		DynamicBuffer<BTExecTrace> trace => em.GetBuffer<BTExecTrace>(testEntity);
		DynamicBuffer<ExpressionBlackboardStorage> blackboard => em.GetBuffer<ExpressionBlackboardStorage>(testEntity);
		BehaviorTestSystem testSystem;
		PendingQuery defaultPendingQuery;

		[SetUp]
		public void SetUp()
		{
			ExpressionTypeManager.Initialize();

			world = new World("TestWorld");
			testSystem = world.GetOrCreateSystemManaged<BehaviorTestSystem>();
			em = world.EntityManager;
			testEntity = em.CreateEntity();
			em.AddBuffer<BTStackFrame>(testEntity);
			em.AddBuffer<BTExecTrace>(testEntity);
			em.AddBuffer<ExpressionBlackboardStorage>(testEntity);
		}

		[Test]
		public void Test_Graph1()
		{
			var btAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>("Assets/Prefabs/Npc_MoveAround.btg");
			BlobAssetReference<BTData> data = default;
			try
			{
				data = btAsset.LoadPersistent(BTData.SchemaVersion).Reference;
				data.Value.exprData.RuntimeInitialize();
				BTState state = default;
				Game.MoveTarget moveTarget = default;
				LocalTransform localTransform = LocalTransform.FromScale(1);
				Game.NpcTargetEntity targetEntity = default;

				var dump = new List<string>();
				BehaviorTreeExecution.DumpNodes(ref data.Value, dump);

				//foreach(var line in dump)
				//	UnityEngine.Debug.Log(line);

				ref var localComponents = ref data.Value.exprData.localComponents;

				NativeArray<UnsafeComponentReference> comps =
					new NativeArray<UnsafeComponentReference>(localComponents.Length, Allocator.Temp);

				for(int i = 0; i < localComponents.Length; ++i)
				{
					var type = localComponents[i].ResolveComponentType();
					var typeIndex = type.TypeIndex;
					if(typeIndex == TypeManager.GetTypeIndex<Game.MoveTarget>())
						comps[i] = UnsafeComponentReference.Make(ref moveTarget);
					else if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
						comps[i] = UnsafeComponentReference.Make(ref localTransform);
					else if(typeIndex == TypeManager.GetTypeIndex<Game.NpcTargetEntity>())
						comps[i] = UnsafeComponentReference.Make(ref targetEntity);
					else
						throw new Exception($"component {type.GetManagedType().FullName} not available in test");
				}

				NativeArray<UntypedComponentLookup> lookups = new NativeArray<UntypedComponentLookup>(1, Allocator.Temp);
				lookups[0] = testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Sequence, 5, 3, Event.Call),
					Trace(BTExecType.WriteField, 3, 4, Event.Return),
					Trace(BTExecType.Sequence, 5, 3, Event.Call),
					Trace(BTExecType.Wait, 4, 4, Event.Wait)
				);

				trace.Clear();

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 4, 4, Event.Start),
					Trace(BTExecType.Wait, 4, 4, Event.Wait)
				);

				trace.Clear();

				moveTarget.enabled = false;

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 4, 4, Event.Start),
					Trace(BTExecType.Wait, 4, 4, Event.Return),
					Trace(BTExecType.Sequence, 5, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Sequence, 8, 3, Event.Call),
					Trace(BTExecType.WriteField, 6, 4, Event.Return),
					Trace(BTExecType.Sequence, 8, 3, Event.Call),
					Trace(BTExecType.Wait, 7, 4, Event.Wait)
				);

			}
			finally
			{
				if(data.IsCreated)
					data.Dispose();
			}
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, depth, 0);

		[TearDown]
		public void TearDown()
		{
			world.Dispose();
		}
	}

	[DisableAutoCreation]
	partial class BehaviorTestSystem : SystemBase
	{
		protected override void OnUpdate()
		{
		}
	}
}

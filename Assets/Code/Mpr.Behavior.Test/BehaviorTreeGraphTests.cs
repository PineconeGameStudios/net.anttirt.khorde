using Mpr.Expr;
using NUnit.Framework;
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
		DynamicBuffer<BTStackFrame> stack;
		DynamicBuffer<BTExecTrace> trace;
		BehaviorTestSystem testSystem;

		[SetUp]
		public void SetUp()
		{
			world = new World("TestWorld");
			testSystem = world.GetOrCreateSystemManaged<BehaviorTestSystem>();
			em = world.EntityManager;
			testEntity = em.CreateEntity();
			em.AddBuffer<BTStackFrame>(testEntity);
			em.AddBuffer<BTExecTrace>(testEntity);
			stack = em.GetBuffer<BTStackFrame>(testEntity);
			trace = em.GetBuffer<BTExecTrace>(testEntity);
		}

		[Test]
		public void Test_Graph1()
		{
			var btAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>("Assets/Prefabs/Npc_MoveAround.btg");
			BlobAssetReference<BTData> data = default;
			try
			{
				data = btAsset.LoadPersistent();
				BTState state = default;
				Game.MoveTarget moveTarget = default;
				LocalTransform localTransform = LocalTransform.FromScale(1);
				Game.NpcTargetEntity targetEntity = default;

				var dump = new List<string>();
				BehaviorTreeExecution.DumpNodes(ref data.Value, dump);

				//foreach(var line in dump)
				//	UnityEngine.Debug.Log(line);

				NativeArray<UnsafeComponentReference> comps =
					new NativeArray<UnsafeComponentReference>(3, Allocator.Temp);
				comps[0] = UnsafeComponentReference.Make(ref moveTarget);
				comps[1] = UnsafeComponentReference.Make(ref targetEntity);
				comps[2] = UnsafeComponentReference.Make(ref localTransform);

				NativeArray<UntypedComponentLookup> lookups = new NativeArray<UntypedComponentLookup>(1,  Allocator.Temp);
				lookups[0] = testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, lookups, 0, trace);

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

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 4, 4, Event.Start),
					Trace(BTExecType.Wait, 4, 4, Event.Wait)
				);

				trace.Clear();

				moveTarget.enabled = false;

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, lookups, 0, trace);

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

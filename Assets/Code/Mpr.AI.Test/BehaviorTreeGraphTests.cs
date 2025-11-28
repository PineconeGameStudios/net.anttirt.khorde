using NUnit.Framework;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using static Mpr.AI.BT.BTExec;
using static Mpr.AI.BT.BTExecTrace;

namespace Mpr.AI.BT.Test
{
	[TestFixture]
	public class BehaviorTreeGraphTests
	{
		World world;
		EntityManager em;
		Entity testEntity;
		DynamicBuffer<BTStackFrame> stack;
		DynamicBuffer<BTExecTrace> trace;

		[SetUp]
		public void SetUp()
		{
			world = new World("TestWorld");
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

				var dump = new List<string>();
				BehaviorTreeExecution.DumpNodes(ref data.Value, dump);

				//foreach(var line in dump)
				//	UnityEngine.Debug.Log(line);

				System.Span<UnsafeComponentReference> comps = stackalloc UnsafeComponentReference[2];
				comps[0] = UnsafeComponentReference.Make(ref moveTarget);
				comps[1] = UnsafeComponentReference.Make(ref localTransform);

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, 0, trace);

				AssertTrace
				(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Sequence, 5, 3, Event.Call),
					Trace(Type.WriteField, 3, 4, Event.Return),
					Trace(Type.Sequence, 5, 3, Event.Call),
					Trace(Type.Wait, 4, 4, Event.Wait)
				);

				trace.Clear();

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, 0, trace);

				AssertTrace
				(
					Trace(Type.Wait, 4, 4, Event.Start),
					Trace(Type.Wait, 4, 4, Event.Wait)
				);

				trace.Clear();

				moveTarget.enabled = false;

				BehaviorTreeExecution.Execute(data, ref state, stack, comps, 0, trace);

				AssertTrace
				(
					Trace(Type.Wait, 4, 4, Event.Start),
					Trace(Type.Wait, 4, 4, Event.Return),
					Trace(Type.Sequence, 5, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Sequence, 8, 3, Event.Call),
					Trace(Type.WriteField, 6, 4, Event.Return),
					Trace(Type.Sequence, 8, 3, Event.Call),
					Trace(Type.Wait, 7, 4, Event.Wait)
				);

			}
			finally
			{
				if(data.IsCreated)
					data.Dispose();
			}
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(Type type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, depth, 0);

		[TearDown]
		public void TearDown()
		{
			world.Dispose();
		}
	}
}

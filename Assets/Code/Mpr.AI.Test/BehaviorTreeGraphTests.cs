using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;

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
				BehaviorTreeState state = default;
				Game.MoveTarget moveTarget = default;
				LocalTransform localTransform = LocalTransform.FromScale(1);

				var dump = new List<string>();
				BehaviorTreeExecution.DumpNodes(ref data.Value, dump);

				foreach(var line in dump)
					UnityEngine.Debug.Log(line);

				Span<UnsafeComponentReference> comps = stackalloc UnsafeComponentReference[2];
				comps[0] = UnsafeComponentReference.Make(ref moveTarget);
				comps[1] = UnsafeComponentReference.Make(ref localTransform);

				BehaviorTreeExecution.Execute(
					data,
					ref state,
					stack,
					comps,
					0,
					trace);
			}
			finally
			{
				if(data.IsCreated)
					data.Dispose();

				UnityEngine.Debug.Log(string.Join("\n", trace.AsNativeArray().ToArray()));
			}
		}

		[TearDown]
		public void TearDown()
		{
			world.Dispose();
		}
	}
}

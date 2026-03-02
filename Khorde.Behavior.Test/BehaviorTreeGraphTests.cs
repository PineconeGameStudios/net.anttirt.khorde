using Khorde.Behavior.Authoring;
using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using static Khorde.Behavior.BTExec;
using static Khorde.Behavior.BTExecTrace;
using Debug = UnityEngine.Debug;

namespace Khorde.Behavior.Test
{
	[Serializable]
	public struct TestMoveTarget : IComponentData
	{
		public float3 position;
		public float tolerance;
		public bool enabled;
	}

	public struct TestNpcTargetEntity : IComponentData
	{
		public Entity target;
	}

	[Serializable] class ReadTestMoveTarget : ComponentReaderNode<TestMoveTarget> { }
	[Serializable] class WriteTestMoveTarget : ComponentWriterNode<TestMoveTarget> { }

	[Serializable] class ReadTestNpcTargetEntity : ComponentReaderNode<TestNpcTargetEntity> { }
	[Serializable] class WriteTestNpcTargetEntity : ComponentWriterNode<TestNpcTargetEntity> { }

	[TestFixture]
	public class BehaviorTreeGraphTests
	{
		World world;
		EntityManager em;
		Entity testEntity;
		DynamicBuffer<BTThread> threads => em.GetBuffer<BTThread>(testEntity);
		DynamicBuffer<BTStackFrame> stack => em.GetBuffer<BTStackFrame>(testEntity);
		DynamicBuffer<BTExecTrace> trace => em.GetBuffer<BTExecTrace>(testEntity);
		DynamicBuffer<BTInvokeQueue> invoke => em.GetBuffer<BTInvokeQueue>(testEntity);
		DynamicBuffer<ExpressionBlackboardStorage> blackboard => em.GetBuffer<ExpressionBlackboardStorage>(testEntity);
		DynamicBuffer<TestBuffer> testBuffer => em.GetBuffer<TestBuffer>(testEntity);
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
			em.AddBuffer<BTThread>(testEntity);
			em.AddBuffer<BTStackFrame>(testEntity);
			em.AddBuffer<BTExecTrace>(testEntity);
			em.AddBuffer<BTInvokeQueue>(testEntity);
			em.SetComponentEnabled<BTInvokeQueue>(testEntity, false);
			em.AddBuffer<ExpressionBlackboardStorage>(testEntity);
			em.AddBuffer<TestBuffer>(testEntity);
		}

		[Test]
		public void Test_MoveAround()
		{
			var btAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_MoveAround.btg");
			BlobAssetReference<BTData> data = default;
			try
			{
				data = btAsset.LoadPersistent(BTData.SchemaVersion).Reference;
				data.Value.exprData.RuntimeInitialize(world.Unmanaged);
				BTState state = default;
				var components = TestComponents.Make();
				RegisterTestComponents(ref data.Value, ref components, out var comps, out var lookups);

				BehaviorTreeExecution.Execute(data, ref state, threads, stack, default, default, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Sequence, 4, 3, Event.Call),
					Trace(BTExecType.WriteField, 5, 4, Event.Return),
					Trace(BTExecType.Sequence, 4, 3, Event.Call),
					Trace(BTExecType.Wait, 3, 4, Event.Wait)
				);

				trace.Clear();

				BehaviorTreeExecution.Execute(data, ref state, threads, stack, default, default, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 3, 4, Event.Start),
					Trace(BTExecType.Wait, 3, 4, Event.Wait)
				);

				trace.Clear();

				components.moveTarget.enabled = false;

				BehaviorTreeExecution.Execute(data, ref state, threads, stack, default, default, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 3, 4, Event.Start),
					Trace(BTExecType.Wait, 3, 4, Event.Return),
					Trace(BTExecType.Sequence, 4, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Sequence, 7, 3, Event.Call),
					Trace(BTExecType.WriteField, 8, 4, Event.Return),
					Trace(BTExecType.Sequence, 7, 3, Event.Call),
					Trace(BTExecType.Wait, 6, 4, Event.Wait)
				);

			}
			finally
			{
				if(data.IsCreated)
					data.Dispose();
			}
		}

		[Test]
		public void Test_WriteVar()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_WriteVar.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			Assert.AreEqual(0, blackboardBytes.ReinterpretLoad<float>(0));

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(1.23f, blackboardBytes.ReinterpretLoad<float>(0));

			AssertTrace(
				Trace(BTExecType.Nop, 0, 0, Event.Spawn),
				Trace(BTExecType.Root, 1, 1, Event.Start),
				Trace(BTExecType.Root, 1, 1, Event.Call),
				Trace(BTExecType.WriteVar, 2, 2, Event.Return),
				Trace(BTExecType.Root, 1, 1, Event.Yield)
			);
		}

		[Test]
		public void Test_Parallel_Wait()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Parallel_Wait.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			Assert.AreEqual(0, blackboardBytes.ReinterpretLoad<float>(0));

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);
			Assert.AreEqual(1, blackboardBytes.ReinterpretLoad<float>(0));

			AssertTrace(
				Trace(0, BTExecType.Nop,        0, 0, Event.Spawn),
				Trace(0, BTExecType.Root,       1,   1, Event.Start),
				Trace(0, BTExecType.Root,       1,   1, Event.Call),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Spawn),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Call),
				Trace(0, BTExecType.Wait,       4,       3, Event.Wait),

				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Start),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Call),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.WriteVar,   6,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Optional,   7,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Nop,        0,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Return),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Yield)
			);

			trace.Clear();

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);
			Assert.AreEqual(2, blackboardBytes.ReinterpretLoad<float>(0));

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);
			Assert.AreEqual(3, blackboardBytes.ReinterpretLoad<float>(0));

			AssertTrace(
				Trace(0, BTExecType.Wait,       4,       3, Event.Start),
				Trace(0, BTExecType.Wait,       4,       3, Event.Wait),

				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Start),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Call),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.WriteVar,   6,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Optional,   7,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Nop,        0,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Return),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Yield),

				Trace(0, BTExecType.Wait,       4,       3, Event.Start),
				Trace(0, BTExecType.Wait,       4,       3, Event.Wait),

				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Start),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Call),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.WriteVar,   6,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Optional,   7,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Call),
				Trace(1, BTExecType.Nop,        0,       3, Event.Return),
				Trace(1, BTExecType.Sequence,   5,     2, Event.Return),
				Trace(1, BTExecType.ThreadRoot, 3,   1, Event.Yield)

			);

			components.moveTarget.enabled = true;

			trace.Clear();

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);
			Assert.AreEqual(3, blackboardBytes.ReinterpretLoad<float>(0));

			AssertTrace(
				Trace(0, BTExecType.Wait,       4,       3, Event.Start),
				Trace(0, BTExecType.Wait,       4,       3, Event.Return),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Abort),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Return),

				// start another cycle of the BT
				Trace(0, BTExecType.Root,       1,   1, Event.Call),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Spawn),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Call),

				// the wait condition completes immediately so the parallel
				// gets aborted before it has a chance to run
				Trace(0, BTExecType.Wait,       4,       3, Event.Return),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Abort),
				Trace(0, BTExecType.Parallel,   2,     2, Event.Return),
				Trace(0, BTExecType.Root,       1,   1, Event.Yield)
			);
		}

		[Test]
		public void Test_Subgraph()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_ExprSubgraph.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			var blackboardVars = blackboardBytes.Reinterpret<int>(1);

			Assert.AreEqual(0, blackboardVars[0]);
			Assert.AreEqual(0, blackboardVars[1]);

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(7, blackboardVars[0]);
			Assert.AreEqual(14, blackboardVars[1]);

			AssertTrace(
				Trace(0, BTExecType.Nop, 0, 0, Event.Spawn),
				Trace(0, BTExecType.Root, 1, 1, Event.Start),
				Trace(0, BTExecType.Root, 1, 1, Event.Call),
				Trace(0, BTExecType.Sequence, 3, 2, Event.Call),
				Trace(0, BTExecType.WriteVar, 2, 3, Event.Return),
				Trace(0, BTExecType.Sequence, 3, 2, Event.Call),
				Trace(0, BTExecType.WriteVar, 4, 3, Event.Return),
				Trace(0, BTExecType.Sequence, 3, 2, Event.Return),
				Trace(0, BTExecType.Root, 1, 1, Event.Yield)
			);

		}

		[Test]
		public void Test_Math()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Math.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			Assert.AreEqual(0.0f, blackboardBytes.GetSubArray(0, 4).ReinterpretLoad<float>(0));
			Assert.AreEqual(new float2(0), blackboardBytes.GetSubArray(4, 8).ReinterpretLoad<float2>(0));
			Assert.AreEqual(new int2(0), blackboardBytes.GetSubArray(12, 8).ReinterpretLoad<int2>(0));
			Assert.AreEqual(new int2(0), blackboardBytes.GetSubArray(20, 8).ReinterpretLoad<int2>(0));
			Assert.AreEqual(new float2(0), blackboardBytes.GetSubArray(28, 8).ReinterpretLoad<float2>(0));
			Assert.AreEqual(new float2(0), blackboardBytes.GetSubArray(36, 8).ReinterpretLoad<float2>(0));

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			// length(float2(3, 4)) == 5
			Assert.AreEqual(5.0f, blackboardBytes.GetSubArray(0, 4).ReinterpretLoad<float>(0));

			// normalize(float2(1, 1)) == float2(sqrt(2), sqrt(2))
			Assert.AreEqual(math.normalize(math.float2(1, 1)), blackboardBytes.GetSubArray(4, 8).ReinterpretLoad<float2>(0));

			// floor(float2(1.5, 1.5))
			Assert.AreEqual(new int2(1, 1), blackboardBytes.GetSubArray(12, 8).ReinterpretLoad<int2>(0));

			// ceil(float2(1.5, 1.5))
			Assert.AreEqual(new int2(2, 2), blackboardBytes.GetSubArray(20, 8).ReinterpretLoad<int2>(0));

			// tofloat(int2(1, 1))
			Assert.AreEqual(new float2(1, 1), blackboardBytes.GetSubArray(28, 8).ReinterpretLoad<float2>(0));

			// rescale(float2(3, 4), 10) == float2(6, 8)
			Assert.AreEqual(new float2(6, 8), blackboardBytes.GetSubArray(36, 8).ReinterpretLoad<float2>(0));
		}

		[Test]
		public void Test_DefaultValues()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_DefaultVar.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			Assert.AreEqual(Hex(0), Hex(blackboardBytes.ReinterpretLoad<int>(0)));
			Assert.AreEqual(Hex(42), Hex(blackboardBytes.ReinterpretLoad<int>(4)));

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(Hex(42), Hex(blackboardBytes.ReinterpretLoad<int>(0)));
			Assert.AreEqual(Hex(42), Hex(blackboardBytes.ReinterpretLoad<int>(4)));
		}

		[Test]
		public void Test_Append()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Append.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			Assert.AreEqual(0, testBuffer.Length);

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(5, testBuffer.Length);

			Assert.AreEqual(new TestBuffer[]
			{
				new TestBuffer{ field0 = true, field1 = 0 },
				new TestBuffer{ field0 = true, field1 = 1 },
				new TestBuffer{ field0 = true, field1 = 2 },
				new TestBuffer{ field0 = true, field1 = 3 },
				new TestBuffer{ field0 = true, field1 = 4 },
			}, testBuffer.AsNativeArray().ToArray());
		}

		[Test]
		public void Test_Buffer()
		{
			LoadBehaviorTree("Packages/net.anttirt.khorde/Khorde.Behavior.Test/TestAssets/BT_Test_Buffer.btg",
				out var data, out var blackboard, out var blackboardBytes, out var blackboardLayout);

			BTState state = default;
			var components = TestComponents.Make();
			RegisterTestComponents(ref data.ValueRW, ref components, out var comps, out var lookups);

			testBuffer.Add(new TestBuffer { field0 = true, field1 = 5 });
			testBuffer.Add(new TestBuffer { field0 = true, field1 = 6 });
			testBuffer.Add(new TestBuffer { field0 = true, field1 = 8 });
			testBuffer.Add(new TestBuffer { field0 = true, field1 = 12 });
			testBuffer.Add(new TestBuffer { field0 = true, field1 = 25 });

			int expected = testBuffer.AsNativeArray().ToArray().Sum(f => f.field1);

			Assert.AreEqual(5, testBuffer.Length);

			BehaviorTreeExecution.Execute(ref data.ValueRW, ref state, threads, stack, default, default, blackboard, ref blackboardLayout.ValueRW, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(5, testBuffer.Length);

			int result = blackboard.ReinterpretLoad<int>(0);

			Assert.AreEqual(expected, result);

			for(int i = 0; i < testBuffer.Length; i++)
			{
				Assert.AreEqual(false, testBuffer[i].field0);
				Assert.AreEqual(i, testBuffer[i].field1);
			}
		}

		static HexInt Hex(int value) => value;

		struct HexInt
		{
			public int Value;

			public static implicit operator HexInt(int value) => new HexInt { Value = value };
			public static implicit operator int(HexInt value) => value.Value;

			public override string ToString()
			{
				return Value.ToString("X");
			}
		}

		struct TestComponents
		{
			public static TestComponents Make()
			{
				return new TestComponents
				{
					moveTarget = default,
					localTransform = LocalTransform.FromScale(1),
					targetEntity = default,
				};
			}

			public TestMoveTarget moveTarget;
			public LocalTransform localTransform;
			public TestNpcTargetEntity targetEntity;
			public UntypedDynamicBuffer testBuffer;
		}

		private void RegisterTestComponents(
			ref BTData data,
			ref TestComponents testComponents,
			out NativeArray<UnsafeComponentReference> comps,
			out NativeArray<UntypedComponentLookup> lookups)
		{
			testComponents.testBuffer = testBuffer.AsUntyped();

			ref var localComponents = ref data.exprData.localComponents;
			comps = new NativeArray<UnsafeComponentReference>(localComponents.Length, Allocator.Temp);
			lookups = new NativeArray<UntypedComponentLookup>(data.exprData.lookupComponents.Length, Allocator.Temp);
			for(int i = 0; i < localComponents.Length; ++i)
			{
				var type = localComponents[i].ResolveComponentType();
				var typeIndex = type.TypeIndex;
				if(typeIndex == TypeManager.GetTypeIndex<TestMoveTarget>())
					comps[i] = UnsafeComponentReference.Make(ref testComponents.moveTarget);
				else if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
					comps[i] = UnsafeComponentReference.Make(ref testComponents.localTransform);
				else if(typeIndex == TypeManager.GetTypeIndex<TestNpcTargetEntity>())
					comps[i] = UnsafeComponentReference.Make(ref testComponents.targetEntity);
				else if(typeIndex == TypeManager.GetTypeIndex<TestBuffer>())
					comps[i] = UnsafeComponentReference.Make<TestBuffer>(ref testComponents.testBuffer);
				else
					throw new Exception($"component {type.GetManagedType().FullName} not available in test");
			}

			for(int i = 0; i < lookups.Length; ++i)
			{
				var type = data.exprData.lookupComponents[i].ResolveComponentType();
				var typeIndex = type.TypeIndex;
				if(typeIndex == TypeManager.GetTypeIndex<TestMoveTarget>())
					lookups[i] = testSystem.CheckedStateRef.GetUntypedComponentLookup<TestMoveTarget>(isReadOnly: true);
				else if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
					lookups[i] = testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);
				else if(typeIndex == TypeManager.GetTypeIndex<TestNpcTargetEntity>())
					lookups[i] = testSystem.CheckedStateRef.GetUntypedComponentLookup<TestNpcTargetEntity>(isReadOnly: true);
				else
					throw new Exception($"lookup {type.GetManagedType().FullName} not available in test");
			}
		}

		private void LoadBehaviorTree(string path, out Ptr<BTData> data, out NativeArray<ExpressionBlackboardStorage> blackboard, out NativeArray<byte> blackboardBytes, out Ptr<ExpressionBlackboardLayout> blackboardLayout)
		{
			var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>(path);
			data = Ptr.Make(ref asset.GetValue(BTData.SchemaVersion));
			data.ValueRW.exprData.RuntimeInitialize(world.Unmanaged);

			var layout = ExprAuthoring.ComputeLayout(new() { (asset.DataHash, new Ptr<BlobExpressionData>(ref data.ValueRW.exprData), "test") });
			var bakedLayout = ExprAuthoring.BakeLayout(layout, Allocator.Temp);

			ExprAuthoring.DumpLayout(layout, asset);

			blackboard = new NativeArray<ExpressionBlackboardStorage>(bakedLayout.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), Allocator.Temp);
			blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());

			ExprAuthoring.InitializeBlackboard(blackboard, layout);

			blackboardLayout = Ptr.Make(ref bakedLayout.Value.FindLayout(asset.DataHash));
		}

		private static void DumpNodes(ref BTData data)
		{
			var dump = new List<string>();
			BehaviorTreeExecution.DumpNodes(ref data, dump);
			foreach(var line in dump)
				UnityEngine.Debug.Log(line);
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, 0, depth, 0);

		static BTExecTrace Trace(int threadId, BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, threadId, depth, 0);

		[TearDown]
		public void TearDown()
		{
			TestContext.Out.WriteLine(string.Join("\n", trace.AsNativeArray().AsSpan().ToArray()));

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

using Khorde.Behavior.Authoring;
using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using static Khorde.Behavior.BTExec;
using static Khorde.Behavior.BTExecTrace;

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
			var btAsset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>("Packages/net.anttirt.khord/Khorde.Behavior.Test/TestAssets/BT_Test_MoveAround.btg");
			BlobAssetReference<BTData> data = default;
			try
			{
				data = btAsset.LoadPersistent(BTData.SchemaVersion).Reference;
				data.Value.exprData.RuntimeInitialize();
				BTState state = default;
				TestMoveTarget moveTarget = default;
				LocalTransform localTransform = LocalTransform.FromScale(1);
				TestNpcTargetEntity targetEntity = default;

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
					if(typeIndex == TypeManager.GetTypeIndex<TestMoveTarget>())
						comps[i] = UnsafeComponentReference.Make(ref moveTarget);
					else if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
						comps[i] = UnsafeComponentReference.Make(ref localTransform);
					else if(typeIndex == TypeManager.GetTypeIndex<TestNpcTargetEntity>())
						comps[i] = UnsafeComponentReference.Make(ref targetEntity);
					else
						throw new Exception($"component {type.GetManagedType().FullName} not available in test");
				}

				NativeArray<UntypedComponentLookup> lookups = new NativeArray<UntypedComponentLookup>(1, Allocator.Temp);
				lookups[0] = testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Sequence, 4, 3, Event.Call),
					Trace(BTExecType.WriteField, 5, 4, Event.Return),
					Trace(BTExecType.Sequence, 4, 3, Event.Call),
					Trace(BTExecType.Wait, 3, 4, Event.Wait)
				);

				trace.Clear();

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

				AssertTrace
				(
					Trace(BTExecType.Wait, 3, 4, Event.Start),
					Trace(BTExecType.Wait, 3, 4, Event.Wait)
				);

				trace.Clear();

				moveTarget.enabled = false;

				BehaviorTreeExecution.Execute(data, ref state, stack, blackboard.AsNativeArray(), ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

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
			var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>("Packages/net.anttirt.khord/Khorde.Behavior.Test/TestAssets/BT_Test_WriteVar.btg");
			ref var data = ref asset.GetValue(BTData.SchemaVersion);
			data.exprData.RuntimeInitialize();

			var layout = ExprAuthoring.ComputeLayout(new() { (asset.DataHash, new Ptr<BlobExpressionData>(ref data.exprData)) });
			var bakedLayout = ExprAuthoring.BakeLayout(layout, Allocator.Temp);

			var blackboard = new NativeArray<ExpressionBlackboardStorage>(bakedLayout.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), Allocator.Temp);
			ref var blackboardLayout = ref bakedLayout.Value.FindLayout(asset.DataHash);
			var blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());

			BTState state = default;
			TestMoveTarget moveTarget = default;
			LocalTransform localTransform = LocalTransform.FromScale(1);
			TestNpcTargetEntity targetEntity = default;

			var dump = new List<string>();
			BehaviorTreeExecution.DumpNodes(ref data, dump);

			//foreach(var line in dump)
			//	UnityEngine.Debug.Log(line);

			ref var localComponents = ref data.exprData.localComponents;

			var comps = new NativeArray<UnsafeComponentReference>(localComponents.Length, Allocator.Temp);

			for(int i = 0; i < localComponents.Length; ++i)
			{
				var type = localComponents[i].ResolveComponentType();
				var typeIndex = type.TypeIndex;
				if(typeIndex == TypeManager.GetTypeIndex<TestMoveTarget>())
					comps[i] = UnsafeComponentReference.Make(ref moveTarget);
				else if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
					comps[i] = UnsafeComponentReference.Make(ref localTransform);
				else if(typeIndex == TypeManager.GetTypeIndex<TestNpcTargetEntity>())
					comps[i] = UnsafeComponentReference.Make(ref targetEntity);
				else
					throw new Exception($"component {type.GetManagedType().FullName} not available in test");
			}

			var lookups = new NativeArray<UntypedComponentLookup>(data.exprData.lookupComponents.Length, Allocator.Temp);
			for(int i = 0; i < lookups.Length; ++i)
			{
				var type = data.exprData.lookupComponents[i].ResolveComponentType();
				var typeIndex = type.TypeIndex;
				if(typeIndex == TypeManager.GetTypeIndex<LocalTransform>())
					lookups[i] = testSystem.CheckedStateRef.GetUntypedComponentLookup<LocalTransform>(isReadOnly: true);
			}

			Assert.AreEqual(0, blackboardBytes.ReinterpretLoad<float>(0));

			BehaviorTreeExecution.Execute(ref data, ref state, stack, blackboard, ref blackboardLayout, default, default, ref defaultPendingQuery, comps, lookups, 0, trace);

			Assert.AreEqual(1.23f, blackboardBytes.ReinterpretLoad<float>(0));

			AssertTrace(
				Trace(BTExecType.Root, 1, 0, Event.Init),
				Trace(BTExecType.Root, 1, 1, Event.Start),
				Trace(BTExecType.Root, 1, 1, Event.Call),
				Trace(BTExecType.WriteVar, 2, 2, Event.Return),
				Trace(BTExecType.Root, 1, 1, Event.Yield)
			);

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

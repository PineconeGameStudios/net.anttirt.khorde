using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Expr.Test;
using Khorde.Query;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Khorde.Behavior.BTExec;
using static Khorde.Behavior.BTExecTrace;

namespace Khorde.Behavior.Test
{
	[TestFixture]
	public class BehaviorTreeTests : ExpressionTestBase
	{
		Entity testEntity;
		DynamicBuffer<BTStackFrame> stack => em.GetBuffer<BTStackFrame>(testEntity);
		DynamicBuffer<BTThread> threads => em.GetBuffer<BTThread>(testEntity);
		DynamicBuffer<BTExecTrace> trace => em.GetBuffer<BTExecTrace>(testEntity);
		Dictionary<Type, ulong> hashCache;
		new BTTestBakingContext baker;
		ref BTData data => ref baker.GetData();
		ref BlobBuilder builder => ref baker.Builder;
		PendingQuery defaultPendingQuery;

		unsafe class BTTestBakingContext : ExpressionBakingContext
		{
			private BTData* data;

			public new ref BTData GetData()
			{
				return ref *data;
			}

			public BTTestBakingContext() : base(Allocator.Temp) { }

			protected override ref BlobExpressionData ConstructRoot()
			{
				ref var data = ref builder.ConstructRoot<BTData>();
				fixed(BTData* ptr = &data)
					this.data = ptr;
				return ref data.exprData;
			}

			public BlobAssetReference<BTData> Bake()
			{
				FinalizeBake();
				return CreateAsset<BTData>(Allocator.Temp);
			}
		}

		[SetUp]
		public override void SetUp()
		{
			base.baker = baker = new BTTestBakingContext();

			base.SetUp();

			testEntity = em.CreateEntity();
			em.AddBuffer<BTThread>(testEntity);
			em.AddBuffer<BTStackFrame>(testEntity);
			em.AddBuffer<BTExecTrace>(testEntity);
			hashCache = new();
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, 0, depth, 0);

		static BTExecTrace Trace(int threadId, BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, threadId, depth, 0);

		struct ManagedStruct
		{
			public object obj;
		}

		[Test]
		public void Test_CreateBlob()
		{
			baker.InitializeBake(1, 0);

			AddExpression(new BinaryFloat()
			{
				Input0 = baker.Const(0.0f),
				Input1 = baker.Const(1.0f),
				@operator = BinaryMathOp.Add,
			});

			var execs = baker.Builder.Allocate(ref data.execs, 1);
			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);
			Assert.IsTrue(asset.IsCreated);
			Assert.IsTrue(asset.Value.execs.Length == 1);
			Assert.IsTrue(asset.Value.exprData.expressions.Length == 1);
		}

		[Test]
		public void Test_Execute()
		{
			baker.InitializeBake(0, 0);

			var execs = baker.Builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExec.BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Nop, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Fail()
		{
			baker.InitializeBake(0, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Fail;
			execs[2].data.fail = new Fail { };

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Fail, 2, 2, Event.Fail),
					Trace(BTExecType.Fail, 2, 2, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Catch()
		{
			baker.InitializeBake(0, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Catch;
			execs[2].data.@catch = new Catch { child = new BTExecNodeId(3) };

			execs[3].type = BTExecType.Fail;
			execs[3].data.fail = new Fail { };

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Catch, 2, 2, Event.Call),
					Trace(BTExecType.Fail, 3, 3, Event.Fail),
					Trace(BTExecType.Catch, 2, 2, Event.Catch),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Sequence()
		{
			baker.InitializeBake(0, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = BTExecType.Nop;
			execs[4].type = BTExecType.Nop;

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Nop, 3, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Nop, 4, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Selector()
		{
			baker.InitializeBake(0, 0);

			var False = baker.Const(false);
			var True = baker.Const(true);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Selector;
			execs[2].data.selector = new Selector { };
			var children2 = builder.Allocate(ref execs[2].data.selector.children, 3);
			children2[0] = new ConditionalBlock { condition = False, nodeId = new BTExecNodeId(3) };
			children2[1] = new ConditionalBlock { condition = True, nodeId = new BTExecNodeId(4) };
			children2[2] = new ConditionalBlock { condition = True, nodeId = new BTExecNodeId(5) };

			execs[3].type = BTExecType.Nop;
			execs[4].type = BTExecType.Nop;
			execs[5].type = BTExecType.Nop;

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Selector, 2, 2, Event.Call),
					Trace(BTExecType.Nop, 4, 3, Event.Return),
					Trace(BTExecType.Selector, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Sequence_Fail()
		{
			baker.InitializeBake(0, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = BTExecType.Fail;
			execs[4].type = BTExecType.Nop;

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Fail, 3, 3, Event.Fail),
					Trace(BTExecType.Fail, 3, 3, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Sequence_Catch()
		{
			baker.InitializeBake(0, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 4);
			execs[3].SetData(new Catch { child = new BTExecNodeId(5) });
			execs[4].type = BTExecType.Nop;
			execs[5].type = BTExecType.Fail;

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Catch, 3, 3, Event.Call),
					Trace(BTExecType.Fail, 5, 4, Event.Fail),
					Trace(BTExecType.Catch, 3, 3, Event.Catch),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Nop, 4, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		static WriteField.Field WriteField(ExpressionRef input, System.Reflection.FieldInfo fieldInfo)
		{
			return new WriteField.Field
			{
				input = input,
				offset = (ushort)UnsafeUtility.GetFieldOffset(fieldInfo),
				size = (ushort)UnsafeUtility.SizeOf(fieldInfo.FieldType),
			};
		}

		[Test]
		public void Test_Read()
		{
			baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Local, ComponentType.AccessMode.ReadOnly);
			baker.InitializeBake(1, 0);

			ref var rcf = ref Allocate<ReadComponentField>(out var n0);
			baker.Bake<TestComponent1>(ref rcf.typeInfo, ExpressionComponentLocation.Local);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 5);

			var TestComponent1_field1 = n0.WithOutputIndex(1);
			var TestComponent1_field2 = n0.WithOutputIndex(2);

			execs[3].SetData(new Optional { condition = TestComponent1_field1, child = new BTExecNodeId(4) });
			execs[4].type = BTExecType.Nop;
			execs[5].SetData(new Optional { condition = TestComponent1_field2, child = new BTExecNodeId(6) });
			execs[6].type = BTExecType.Nop;

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			NativeArray<UnsafeComponentReference> componentPtrs = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			NativeArray<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Optional, 3, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Optional, 5, 3, Event.Call),
					Trace(BTExecType.Nop, 6, 4, Event.Return),
					Trace(BTExecType.Optional, 5, 3, Event.Return),
					Trace(BTExecType.Sequence, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Write()
		{
			baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Local, ComponentType.AccessMode.ReadWrite);
			baker.InitializeBake(1, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetWriteField(ref builder, 0, WriteField(baker.Const(true), typeof(TestComponent1).GetField(nameof(TestComponent1.field1))));

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			NativeArray<UnsafeComponentReference> componentPtrs = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			NativeArray<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				Assert.IsFalse(tc1.field1);
				Assert.IsTrue(tc1.field2);

				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.WriteField, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);

				Assert.IsTrue(tc1.field1);
				Assert.IsTrue(tc1.field2);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Wait()
		{
			baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Local, ComponentType.AccessMode.ReadWrite);
			baker.InitializeBake(1, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			ref var rcf = ref Allocate<ReadComponentField>(out var n0);
			baker.Bake<TestComponent1>(ref rcf.typeInfo, ExpressionComponentLocation.Local);

			var TestComponent1_field1 = n0.WithOutputIndex(1);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetData(new Wait { until = TestComponent1_field1 });

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			NativeArray<UnsafeComponentReference> componentPtrs = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			NativeArray<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Wait, 2, 2, Event.Start),
					Trace(BTExecType.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				tc1.field1 = true;

				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Wait, 2, 2, Event.Start),
					Trace(BTExecType.Wait, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Wait, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_WriteVar()
		{
			baker.InitializeBake(0, 0);

			var execs = baker.Builder.Allocate(ref data.execs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].type = BTExec.BTExecType.WriteVar;
			execs[2].data.writeVar = new WriteVar
			{
				variableIndex = 0,
				input = baker.Const(3.523f),
			};

			baker.AddBlackboardVariable<float>("TestVar", isGlobal: true);

			world.EntityManager.AddBuffer<ExpressionBlackboardStorage>(testEntity);

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			var layout = ExprAuthoring.ComputeLayout(new() { (default, new Ptr<BlobExpressionData>(ref asset.Value.exprData)) });
			var bakedLayout = ExprAuthoring.BakeLayout(layout, Allocator.Temp);

			var blackboard = new NativeArray<ExpressionBlackboardStorage>(bakedLayout.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), Allocator.Temp);
			ref var blackboardLayout = ref bakedLayout.Value.FindLayout(default);

			var blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());

			Assert.GreaterOrEqual(blackboardBytes.Length, blackboardLayout.minByteLength);

			blackboardBytes.ReinterpretStore(0, 1.23f);

			BTState state = default;

			try
			{
				Assert.AreEqual(1.23f, blackboardBytes.ReinterpretLoad<float>(0));

				asset.Execute(ref state, threads, stack, blackboard, ref blackboardLayout, default, default, ref defaultPendingQuery, default, default, 0, trace);

				Assert.AreEqual(3.523f, blackboardBytes.ReinterpretLoad<float>(0));

				AssertTrace(
					Trace(BTExecType.Nop, 0, 0, Event.Spawn),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.WriteVar, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Fork()
		{
			baker.InitializeBake(0, 0);

			var execs = baker.Builder.Allocate(ref data.execs, 100);

			execs[1].type = BTExec.BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };
			execs[2].type = BTExecType.Parallel;
			execs[2].data.parallel = new Parallel { main = new BTExecNodeId(3), parallel = new BTExecNodeId(4) };
			execs[4].type = BTExecType.ThreadRoot;
			execs[4].data.root = new Root { child = new BTExecNodeId(5) };

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, default, default, 0, trace);

				AssertTrace(
					Trace(0, BTExecType.Nop,      0, 0, Event.Spawn),
					Trace(0, BTExecType.Root,     1,   1, Event.Start),
					Trace(0, BTExecType.Root,     1,   1, Event.Call),
					Trace(0, BTExecType.Parallel, 2,     2, Event.Spawn),
					Trace(0, BTExecType.Parallel, 2,     2, Event.Call),
					Trace(0, BTExecType.Nop,      3,       3, Event.Return),
					Trace(0, BTExecType.Parallel, 2,     2, Event.Abort),
					Trace(0, BTExecType.Parallel, 2,     2, Event.Return),
					Trace(0, BTExecType.Root,     1,   1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		[Test]
		public void Test_Fork_Wait()
		{
			baker.RegisterComponentAccess<TestComponent1>(ExpressionComponentLocation.Local, ComponentType.AccessMode.ReadWrite);
			baker.InitializeBake(1, 0);

			var execs = builder.Allocate(ref data.execs, 100);

			ref var rcf = ref Allocate<ReadComponentField>(out var n0);
			baker.Bake<TestComponent1>(ref rcf.typeInfo, ExpressionComponentLocation.Local);

			var TestComponent1_field1 = n0.WithOutputIndex(1);

			execs[1].type = BTExec.BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };
			execs[2].type = BTExecType.Parallel;
			execs[2].data.parallel = new Parallel { main = new BTExecNodeId(3), parallel = new BTExecNodeId(4) };
			execs[3].SetData(new Wait { until = TestComponent1_field1 });
			execs[4].type = BTExecType.ThreadRoot;
			execs[4].data.root = new Root { child = new BTExecNodeId(5) };

			var asset = baker.Bake();
			asset.Value.exprData.RuntimeInitialize(world.Unmanaged);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			NativeArray<UnsafeComponentReference> componentPtrs = new NativeArray<UnsafeComponentReference>(1, Allocator.Temp);
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			NativeArray<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(0, BTExecType.Nop,        0, 0, Event.Spawn),
					Trace(0, BTExecType.Root,       1,   1, Event.Start),
					Trace(0, BTExecType.Root,       1,   1, Event.Call),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Spawn),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Call),
					Trace(0, BTExecType.Wait,       3,       3, Event.Wait),

					Trace(1, BTExecType.ThreadRoot, 4,   1, Event.Start),
					Trace(1, BTExecType.ThreadRoot, 4,   1, Event.Call),
					Trace(1, BTExecType.Nop,        5,     2, Event.Return),
					Trace(1, BTExecType.ThreadRoot, 4,   1, Event.Abort)
				);

				trace.Clear();

				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(0, BTExecType.Wait,       3,       3, Event.Start),
					Trace(0, BTExecType.Wait,       3,       3, Event.Wait)
				);

				trace.Clear();

				tc1.field1 = true;

				asset.Execute(ref state, threads, stack, default, ref ExpressionBlackboardLayout.Empty, default, default, ref defaultPendingQuery, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(0, BTExecType.Wait,       3,       3, Event.Start),
					Trace(0, BTExecType.Wait,       3,       3, Event.Return),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Return),

					Trace(0, BTExecType.Root,       1,   1, Event.Call),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Spawn),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Call),
					Trace(0, BTExecType.Wait,       3,       3, Event.Return),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Abort),
					Trace(0, BTExecType.Parallel,   2,     2, Event.Return),

					Trace(0, BTExecType.Root,       1,   1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		static string GetShortName(System.Type type)
		{
			if(type == typeof(int)) return "int";
			if(type == typeof(float)) return "float";
			return type.Name;
		}
	}

	struct TestComponent1 : IComponentData
	{
		public int field0;
		public bool field1;
		public bool field2;
	}

	struct TestComponent2 : IComponentData
	{
		public int @int;
		public int2 @int2;
		public int3 @int3;
		public int4 @int4;
		public float @float;
		public float2 @float2;
		public float3 @float3;
		public float4 @float4;
	}
}

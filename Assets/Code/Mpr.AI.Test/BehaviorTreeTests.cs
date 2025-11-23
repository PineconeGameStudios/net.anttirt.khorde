using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using static Mpr.AI.BT.BTExec;
using static Mpr.AI.BT.BTExecTrace;

namespace Mpr.AI.BT.Test
{
	[TestFixture]
	public class BehaviorTreeTests
	{
		World world;
		EntityManager em;
		Entity testEntity;
		DynamicBuffer<BTStackFrame> stack;
		DynamicBuffer<BTExecTrace> trace;
		ushort exprCount;
		NativeList<byte> constStorage;
		BTExprNodeRef False;
		BTExprNodeRef True;

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
			exprCount = 0;
			constStorage = new NativeList<byte>(Allocator.Temp);
			False = BTExprNodeRef.Const(BehaviorTreeAuthoringExt.WriteConstantImpl(false, constStorage));
			True = BTExprNodeRef.Const(BehaviorTreeAuthoringExt.WriteConstantImpl(true, constStorage));
		}

		[TearDown]
		public void TearDown()
		{
			em = default;
			world.Dispose();
			world = null;
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(Type type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, depth, 0);

		[Test]
		public void Test_CreateBlob()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 1);
			var exprs = builder.Allocate(ref data.exprs, 1);
			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);
			Assert.IsTrue(asset.IsCreated);
			Assert.IsTrue(asset.Value.execs.Length == 1);
			Assert.IsTrue(asset.Value.exprs.Length == 1);
		}

		[Test]
		public void Test_Execute()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = BTExec.Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Nop, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = Type.Fail;
			execs[2].data.fail = new Fail { };

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Fail, 2, 2, Event.Fail),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = Type.Catch;
			execs[2].data.@catch = new Catch { child = new BTExecNodeId(3) };

			execs[3].type = Type.Fail;
			execs[3].data.fail = new Fail { };

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Catch, 2, 2, Event.Call),
					Trace(Type.Fail, 3, 3, Event.Fail),
					Trace(Type.Catch, 2, 2, Event.Catch),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = Type.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = Type.Nop;
			execs[4].type = Type.Nop;

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Nop, 3, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Nop, 4, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = Type.Selector;
			execs[2].data.selector = new Selector { };
			var children2 = builder.Allocate(ref execs[2].data.selector.children, 3);
			children2[0] = new ConditionalBlock { condition = False, nodeId = new BTExecNodeId(3) };
			children2[1] = new ConditionalBlock { condition = True, nodeId = new BTExecNodeId(4) };
			children2[2] = new ConditionalBlock { condition = True, nodeId = new BTExecNodeId(5) };

			execs[3].type = Type.Nop;
			execs[4].type = Type.Nop;
			execs[5].type = Type.Nop;

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Selector, 2, 2, Event.Call),
					Trace(Type.Nop, 4, 3, Event.Return),
					Trace(Type.Selector, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].type = Type.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = Type.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = Type.Fail;
			execs[4].type = Type.Nop;

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Fail, 3, 3, Event.Fail),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 4);
			execs[3].SetData(new Catch { child = new BTExecNodeId(5) });
			execs[4].type = Type.Nop;
			execs[5].type = Type.Fail;

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, default, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Catch, 3, 3, Event.Call),
					Trace(Type.Fail, 5, 4, Event.Fail),
					Trace(Type.Catch, 3, 3, Event.Catch),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Nop, 4, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
					);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}

		struct TestComponent1
		{
			public int field0;
			public bool field1;
			public bool field2;
		}

		BTExprNodeRef ReadExpr(ref BlobBuilder builder, BlobBuilderArray<BTExpr> exprs, byte componentIndex, System.Reflection.FieldInfo fieldInfo)
		{
			exprs[exprCount] = new BTExpr
			{
				type = BTExpr.ExprType.ReadField,
				data = new BTExpr.Data
				{
					readField = new BTExpr.ReadField
					{
						componentIndex = 0,
					}
				}
			};

			var read1Fields = builder.Allocate(ref exprs[exprCount].data.readField.fields, 1);
			read1Fields[0] = fieldInfo;

			++exprCount;

			return BTExprNodeRef.Node((ushort)(exprCount - 1), 0);
		}

		static WriteField.Field WriteField(BTExprNodeRef input, System.Reflection.FieldInfo fieldInfo)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 5);

			var TestComponent1_field1 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field1)));
			var TestComponent1_field2 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field2)));

			execs[3].SetData(new Optional { condition = TestComponent1_field1, child = new BTExecNodeId(4) });
			execs[4].type = Type.Nop;
			execs[5].SetData(new Optional { condition = TestComponent1_field2, child = new BTExecNodeId(6) });
			execs[6].type = Type.Nop;

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, componentPtrs, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Optional, 3, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Call),
					Trace(Type.Optional, 5, 3, Event.Call),
					Trace(Type.Nop, 6, 4, Event.Return),
					Trace(Type.Optional, 5, 3, Event.Return),
					Trace(Type.Sequence, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetWriteField(ref builder, 0, WriteField(True, typeof(TestComponent1).GetField(nameof(TestComponent1.field1))));

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			BehaviorTreeState state = default;

			try
			{
				Assert.IsFalse(tc1.field1);
				Assert.IsTrue(tc1.field2);

				asset.Execute(ref state, stack, componentPtrs, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.WriteField, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			var TestComponent1_field1 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field1)));

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetData(new Wait { until = TestComponent1_field1 });

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			BehaviorTreeState state = default;

			try
			{
				asset.Execute(ref state, stack, componentPtrs, 0, trace);

				AssertTrace(
					Trace(Type.Root, 1, 0, Event.Init),
					Trace(Type.Root, 1, 1, Event.Start),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				asset.Execute(ref state, stack, componentPtrs, 0, trace);

				AssertTrace(
					Trace(Type.Wait, 2, 2, Event.Start),
					Trace(Type.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				tc1.field1 = true;

				asset.Execute(ref state, stack, componentPtrs, 0, trace);

				AssertTrace(
					Trace(Type.Wait, 2, 2, Event.Start),
					Trace(Type.Wait, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Call),
					Trace(Type.Wait, 2, 2, Event.Return),
					Trace(Type.Root, 1, 1, Event.Yield)
				);
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
		}
	}
}

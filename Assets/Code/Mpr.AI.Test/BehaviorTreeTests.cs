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

		BTExprNodeRef ConstBool(bool value, BlobBuilderArray<BTExpr> exprs)
		{
			exprs[exprCount] = new BTExpr { index = 1, data = new BTExpr.Data { @bool = new BTExpr.Bool { data = new BTExpr.Bool.Data { @const = new BTExpr.Bool.Const(value) } } } };
			return new BTExprNodeRef(exprCount++, 0);
		}

		BTExprNodeRef Read<T>(BlobBuilderArray<BTExpr> exprs, byte componentIndex, string fieldName) where T : unmanaged
		{
			exprs[exprCount] = new BTExpr
			{
				index = 0,
				data = new BTExpr.Data
				{
					readField = new BTExpr.ReadField
					{
						componentIndex = componentIndex,
						fieldOffset = (ushort)UnsafeUtility.GetFieldOffset(typeof(T).GetField(fieldName))
					}
				}
			};
			return new BTExprNodeRef(exprCount++, 0);
		}


		[Test]
		public void Test_CreateBlob()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 1);
			var exprs = builder.Allocate(ref data.exprs, 1);
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
			execs[3].childIndex = 0;
			execs[4].type = Type.Nop;
			execs[4].childIndex = 1;

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
			children2[0] = new ConditionalBlock { condition = ConstBool(false, exprs), nodeId = new BTExecNodeId(3) };
			children2[1] = new ConditionalBlock { condition = ConstBool(true, exprs), nodeId = new BTExecNodeId(4) };
			children2[2] = new ConditionalBlock { condition = ConstBool(true, exprs), nodeId = new BTExecNodeId(5) };

			execs[3].type = Type.Nop;
			execs[3].childIndex = 0;
			execs[4].type = Type.Nop;
			execs[4].childIndex = 1;
			execs[5].type = Type.Nop;
			execs[5].childIndex = 2;

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
			execs[3].childIndex = 0;
			execs[4].type = Type.Nop;
			execs[4].childIndex = 1;

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

		[Test]
		public void Test_Read()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 5);
			execs[3].SetData(new Optional { condition = Read<TestComponent1>(exprs, 0, nameof(TestComponent1.field1)), child = new BTExecNodeId(4) });
			execs[4].type = Type.Nop;
			execs[5].SetData(new Optional { condition = Read<TestComponent1>(exprs, 0, nameof(TestComponent1.field2)), child = new BTExecNodeId(6) });
			execs[6].type = Type.Nop;

			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<System.IntPtr> componentPtrs = stackalloc System.IntPtr[1];

			unsafe
			{
				componentPtrs[0] = (System.IntPtr)(&tc1);
			}

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
	}

	static class BehaviorTreeTestExt
	{
		public static void SetData(ref this BTExec self, in Root value) { self.type = Type.Root; self.data.root = value; }
		public static void SetSequence(ref this BTExec self, ref BlobBuilder builder, BlobBuilderArray<BTExec> execs, params ushort[] childNodeIds)
		{
			self.type = Type.Sequence;
			var array = builder.Allocate(ref self.data.sequence.children, childNodeIds.Length);
			for(int i = 0; i < childNodeIds.Length; i++)
			{
				array[i] = new BTExecNodeId(childNodeIds[i]);
				execs[childNodeIds[i]].childIndex = (byte)i;
			}
		}
		public static void SetSelector(ref this BTExec self, ref BlobBuilder builder, BlobBuilderArray<BTExec> execs, params (ushort, BTExprNodeRef)[] childNodeIds)
		{
			self.type = Type.Selector;
			var array = builder.Allocate(ref self.data.selector.children, childNodeIds.Length);
			for(int i = 0; i < childNodeIds.Length; i++)
			{
				array[i] = new ConditionalBlock { nodeId = new BTExecNodeId(childNodeIds[i].Item1), condition = childNodeIds[i].Item2 };
				execs[childNodeIds[i].Item1].childIndex = (byte)i;
			}
		}
		public static void SetData(ref this BTExec self, in WriteField value) { self.type = Type.WriteField; self.data.writeField = value; }
		public static void SetData(ref this BTExec self, in Wait value) { self.type = Type.Wait; self.data.wait = value; }
		public static void SetData(ref this BTExec self, in Fail value) { self.type = Type.Fail; self.data.fail = value; }
		public static void SetData(ref this BTExec self, in Optional value) { self.type = Type.Optional; self.data.optional = value; }
		public static void SetData(ref this BTExec self, in Catch value) { self.type = Type.Catch; self.data.@catch = value; }
	}
}

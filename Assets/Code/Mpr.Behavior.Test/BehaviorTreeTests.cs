using Mpr.Expr;
using Mpr.Expr.Authoring;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Mpr.Behavior.BTExec;
using static Mpr.Behavior.BTExecTrace;

namespace Mpr.Behavior.Test
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
		ExprNodeRef False;
		ExprNodeRef True;

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
			var offset = ExprAuthoring.WriteConstant(false, out var length, constStorage);
			False = ExprNodeRef.Const(offset, length);
			offset = ExprAuthoring.WriteConstant(true, out length, constStorage);
			True = ExprNodeRef.Const(offset, length);
		}

		[TearDown]
		public void TearDown()
		{
			em = default;
			world.Dispose();
			world = null;
		}

		void AssertTrace(params BTExecTrace[] expected) => Assert.AreEqual(expected, trace.AsNativeArray().AsSpan().ToArray());

		static BTExecTrace Trace(BTExecType type, ushort nodeId, int depth, Event @event)
			=> new BTExecTrace(new BTExecNodeId(nodeId), type, @event, depth, 0);

		struct ManagedStruct
		{
			public object obj;
		}

		[Test]
		public void Test_FailWriteConstantManaged()
		{
			Assert.That(
				() => ExprAuthoring.WriteConstant(new ManagedStruct(), out var length, constStorage),
				Throws.Exception
				);
		}

		[Test]
		public void Test_CreateBlob()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 1);
			var exprs = builder.Allocate(ref data.exprData.exprs, 1);
			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);
			Assert.IsTrue(asset.IsCreated);
			Assert.IsTrue(asset.Value.execs.Length == 1);
			Assert.IsTrue(asset.Value.exprData.exprs.Length == 1);
		}

		[Test]
		public void Test_Execute()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].type = BTExec.BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Fail;
			execs[2].data.fail = new Fail { };

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Fail, 2, 2, Event.Fail),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Catch;
			execs[2].data.@catch = new Catch { child = new BTExecNodeId(3) };

			execs[3].type = BTExecType.Fail;
			execs[3].data.fail = new Fail { };

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = BTExecType.Nop;
			execs[4].type = BTExecType.Nop;

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

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

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].type = BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			execs[2].type = BTExecType.Sequence;
			execs[2].data.sequence = new Sequence { };
			var children2 = builder.Allocate(ref execs[2].data.sequence.children, 2);
			children2[0] = new BTExecNodeId(3);
			children2[1] = new BTExecNodeId(4);

			execs[3].type = BTExecType.Fail;
			execs[4].type = BTExecType.Nop;

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Sequence, 2, 2, Event.Call),
					Trace(BTExecType.Fail, 3, 3, Event.Fail),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 4);
			execs[3].SetData(new Catch { child = new BTExecNodeId(5) });
			execs[4].type = BTExecType.Nop;
			execs[5].type = BTExecType.Fail;

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, default, default, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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

		ExprNodeRef ReadExpr(ref BlobBuilder builder, BlobBuilderArray<BTExpr> exprs, byte componentIndex, System.Reflection.FieldInfo fieldInfo)
		{
			exprs[exprCount] = new BTExpr
			{
				type = BTExpr.BTExprType.ReadField,
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

			return ExprNodeRef.Node((ushort)(exprCount - 1), 0);
		}

		static WriteField.Field WriteField(ExprNodeRef input, System.Reflection.FieldInfo fieldInfo)
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
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);
			var types = builder.Allocate(ref data.exprData.componentTypes, 1);
			types[0] = new Blobs.BlobComponentType(TypeManager.GetTypeInfo<TestComponent1>().StableTypeHash, ComponentType.AccessMode.ReadWrite);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetSequence(ref builder, execs, 3, 5);

			var TestComponent1_field1 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field1)));
			var TestComponent1_field2 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field2)));

			execs[3].SetData(new Optional { condition = TestComponent1_field1, child = new BTExecNodeId(4) });
			execs[4].type = BTExecType.Nop;
			execs[5].SetData(new Optional { condition = TestComponent1_field2, child = new BTExecNodeId(6) });
			execs[6].type = BTExecType.Nop;

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			System.Span<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);
			var types = builder.Allocate(ref data.exprData.componentTypes, 1);
			types[0] = new Blobs.BlobComponentType(TypeManager.GetTypeInfo<TestComponent1>().StableTypeHash, ComponentType.AccessMode.ReadWrite);

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetWriteField(ref builder, 0, WriteField(True, typeof(TestComponent1).GetField(nameof(TestComponent1.field1))));

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			System.Span<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				Assert.IsFalse(tc1.field1);
				Assert.IsTrue(tc1.field2);

				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
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
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);
			var types = builder.Allocate(ref data.exprData.componentTypes, 1);
			types[0] = new Blobs.BlobComponentType(TypeManager.GetTypeInfo<TestComponent1>().StableTypeHash, ComponentType.AccessMode.ReadWrite);

			var TestComponent1_field1 = ReadExpr(ref builder, exprs, 0, typeof(TestComponent1).GetField(nameof(TestComponent1.field1)));

			execs[1].SetData(new Root { child = new BTExecNodeId(2) });
			execs[2].SetData(new Wait { until = TestComponent1_field1 });

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			TestComponent1 tc1 = new TestComponent1 { field0 = 42, field1 = false, field2 = true };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc1);

			System.Span<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Wait, 2, 2, Event.Start),
					Trace(BTExecType.Wait, 2, 2, Event.Wait)
				);

				trace.Clear();

				tc1.field1 = true;

				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

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

		static string GetShortName(System.Type type)
		{
			if(type == typeof(int)) return "int";
			if(type == typeof(float)) return "float";
			return type.Name;
		}

		private static MathType[] mathTypes = System.Enum.GetValues(typeof(MathType)).Cast<MathType>().ToArray();
		private static BinaryMathOp[] binaryOps = System.Enum.GetValues(typeof(BinaryMathOp)).Cast<BinaryMathOp>().ToArray();
		private static Dictionary<MathType, System.Type> realTypes = mathTypes.ToDictionary(t => t, t => t switch
		{
			MathType.Int => typeof(int),
			MathType.Int2 => typeof(int2),
			MathType.Int3 => typeof(int3),
			MathType.Int4 => typeof(int4),
			MathType.Float => typeof(float),
			MathType.Float2 => typeof(float2),
			MathType.Float3 => typeof(float3),
			MathType.Float4 => typeof(float4),
			_ => throw new System.NotImplementedException(),
		});
		private static object Replicate(MathType type, int value) => type switch
		{
			MathType.Int => value,
			MathType.Int2 => new int2(value),
			MathType.Int3 => new int3(value),
			MathType.Int4 => new int4(value),
			MathType.Float => (float)value,
			MathType.Float2 => new float2(value),
			MathType.Float3 => new float3(value),
			MathType.Float4 => new float4(value),
			_ => throw new System.NotImplementedException(),
		};
		private static int Compute(int a, int b, BinaryMathOp op) => op switch
		{
			BinaryMathOp.Add => a + b,
			BinaryMathOp.Sub => a - b,
			BinaryMathOp.Mul => a * b,
			BinaryMathOp.Div => a / b,
			_ => throw new System.NotImplementedException(),
		};

		public static object[] TestCases =
			binaryOps
			.SelectMany(op => mathTypes.Select(type => (op, type)))
			.Select(pair => new object[] { pair.op, pair.type, Replicate(pair.type, 6), Replicate(pair.type, 2), Replicate(pair.type, Compute(6, 2, pair.op)) })
			.ToArray()
			;

		[TestCaseSource(nameof(TestCases))]
		public void Test_Math(BinaryMathOp op, MathType mathType, object left, object right, object result)
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, 100);
			var exprs = builder.Allocate(ref data.exprData.exprs, 100);
			var types = builder.Allocate(ref data.exprData.componentTypes, 1);
			types[0] = new Blobs.BlobComponentType(TypeManager.GetTypeInfo<TestComponent2>().StableTypeHash, ComponentType.AccessMode.ReadWrite);

			execs[1].type = BTExec.BTExecType.Root;
			execs[1].data.root = new Root { child = new BTExecNodeId(2) };

			//BTBinaryOp op = BTBinaryOp.Add;
			//BTMathType mathType = BTMathType.Int;
			//int left = 1;
			//int right = 2;
			//int result = 3;

			System.Reflection.FieldInfo field = typeof(TestComponent2)
				.GetField(GetShortName(left.GetType()), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

			var offset0 = ExprAuthoring.WriteConstant(left, out var length0, constStorage);
			var const0 = ExprNodeRef.Const(offset0, length0);
			var offset1 = ExprAuthoring.WriteConstant(right, out var length1, constStorage);
			var const1 = ExprNodeRef.Const(offset1, length1);

			exprs[0].type = BTExpr.BTExprType.BinaryMath;
			exprs[0].data.binaryMath = new BTExpr.BinaryMath
			{
				left = const0,
				right = const1,
				op = op,
				type = mathType,
			};

			var expr = ExprNodeRef.Node(0, 0);

			execs[2].SetWriteField(ref builder, 0, WriteField(expr, field));

			TestComponent2 tc2 = new TestComponent2 { };

			System.Span<UnsafeComponentReference> componentPtrs = stackalloc UnsafeComponentReference[1];
			componentPtrs[0] = UnsafeComponentReference.Make(ref tc2);

			ExprAuthoring.BakeConstStorage(ref builder, ref data.exprData, constStorage);
			var asset = builder.CreateBlobAssetReference<BTData>(Allocator.Temp);

			System.Span<UntypedComponentLookup> lookups = default;

			BTState state = default;

			try
			{
				asset.Execute(ref state, stack, componentPtrs, lookups, 0, trace);

				AssertTrace(
					Trace(BTExecType.Root, 1, 0, Event.Init),
					Trace(BTExecType.Root, 1, 1, Event.Start),
					Trace(BTExecType.Root, 1, 1, Event.Call),
					Trace(BTExecType.WriteField, 2, 2, Event.Return),
					Trace(BTExecType.Root, 1, 1, Event.Yield)
				);

				Assert.AreEqual(result, field.GetValue(tc2));
			}
			finally
			{
				foreach(var item in trace)
					TestContext.WriteLine(item);
			}
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

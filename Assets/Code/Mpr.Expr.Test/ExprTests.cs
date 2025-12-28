using Mpr.Expr.Authoring;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Entities;

namespace Mpr.Expr.Test
{
	[TestFixture]
	public class ExprTests
	{
		World world;
		EntityManager em;
		Entity testEntity;
		ushort exprCount;
		NativeList<byte> constStorage;
		ExprNodeRef False;
		ExprNodeRef True;
		BlobBuilder builder;
		ExprTestSystem testSystem;

		[SetUp]
		public void SetUp()
		{
			world = new World("TestWorld");

			testSystem = world.GetOrCreateSystemManaged<ExprTestSystem>();

			em = world.EntityManager;
			testEntity = em.CreateEntity();
			exprCount = 0;
			constStorage = new NativeList<byte>(Allocator.Temp);
			var offset = ExprAuthoring.WriteConstant(false, out var length, constStorage);
			False = ExprNodeRef.Const(offset, length);
			offset = ExprAuthoring.WriteConstant(true, out length, constStorage);
			True = ExprNodeRef.Const(offset, length);
			builder = new BlobBuilder(Allocator.Temp);
		}

		[TearDown]
		public void TearDown()
		{
			em = default;
			world.Dispose();
			world = null;
		}

		ExprNodeRef ReadExpr(BlobBuilderArray<BTExpr> exprs, byte componentIndex, System.Reflection.FieldInfo fieldInfo)
		{
			exprs[exprCount] = new BTExpr
			{
				type = BTExpr.BTExprType.ReadField,
				data = new BTExpr.Data
				{
					readField = new BTExpr.ReadField
					{
						componentIndex = componentIndex,
					}
				}
			};

			var read1Fields = builder.Allocate(ref exprs[exprCount].data.readField.fields, 1);
			read1Fields[0] = fieldInfo;

			++exprCount;

			return ExprNodeRef.Node((ushort)(exprCount - 1), 0);
		}

		ExprNodeRef LookupExpr(BlobBuilderArray<BTExpr> exprs, byte componentIndex, ExprNodeRef entity, System.Reflection.FieldInfo fieldInfo)
		{
			exprs[exprCount] = new BTExpr
			{
				type = BTExpr.BTExprType.LookupField,
				data = new BTExpr.Data
				{
					lookupField = new BTExpr.LookupField
					{
						entity = entity,
						componentIndex = componentIndex,
					}
				}
			};

			var fields = builder.Allocate(ref exprs[exprCount].data.lookupField.fields, 1);
			fields[0] = fieldInfo;

			const int FieldIndex = 0;

			++exprCount;

			return ExprNodeRef.Node((ushort)(exprCount - 1), FieldIndex + 1);
		}

		[Test]
		public void Test_Lookup()
		{
			ref var data = ref builder.ConstructRoot<ExprData>();

			var testComp = new TestComponent1
			{
				field0 = 9876,
				field1 = true,
				field2 = false,
			};

			var testEntity = em.CreateEntity();
			em.AddComponentData(testEntity, testComp);
			var testEntityRef = ExprAuthoring.WriteConstant(testEntity, constStorage);

			var exprs = builder.Allocate(ref data.exprs, 100);
			var types = builder.Allocate(ref data.lookupComponents, 1);
			types[0] = Blobs.BlobComponentType.Make<TestComponent1>(ComponentType.AccessMode.ReadOnly);

			var lookup = LookupExpr(exprs, 0, testEntityRef, typeof(TestComponent1).GetField(nameof(TestComponent1.field0)));

			ExprAuthoring.BakeConstStorage(ref builder, ref data, constStorage);
			var asset = builder.CreateBlobAssetReference<ExprData>(Allocator.Temp);

			Span<UntypedComponentLookup> lookups = stackalloc UntypedComponentLookup[1];
			lookups[0] = testSystem.CheckedStateRef.GetUntypedComponentLookup<TestComponent1>(isReadOnly: true);
			lookups[0].Update(testSystem);

			var ctx = new ExprEvalContext(
				ref asset.Value,
				default,
				lookups
				);

			int result = lookup.Evaluate<int>(in ctx);

			Assert.AreEqual(testComp.field0, result);
		}

		struct TestComponent1 : IComponentData
		{
			public int field0;
			public bool field1;
			public bool field2;
		}
	}

	[DisableAutoCreation]
	partial class ExprTestSystem : SystemBase
	{
		protected override void OnUpdate()
		{
		}
	}
}

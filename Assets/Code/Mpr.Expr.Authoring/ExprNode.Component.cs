using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring
{
	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentReaderNode<T> : ExprBase, IComponentAccess where T : unmanaged, Unity.Entities.IComponentData
	{
		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public override string Title => $"Read {typeof(T).Name}";

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			var index = context.localComponents.FindIndex(kv => kv.GetManagedType() == typeof(T));
			if(index == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			expr.type = BTExpr.BTExprType.ReadField;
			expr.data.readField = new BTExpr.ReadField
			{
				componentIndex = (byte)index,
			};

			var fields = BlobExpressionData.GetComponentFields<T>();

			var bakedFields = builder.Allocate(ref expr.data.readField.fields, fields.Length);
			for(int i = 0; i < fields.Length; i++)
			{
				int offset = UnsafeUtility.GetFieldOffset(fields[i]);
				if(offset > ushort.MaxValue)
					throw new Exception("component too large; field offset over 65k");

				bakedFields[i] = fields[i];
			}
		}

		public override void Bake(ExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.Allocate<ReadComponentField>(storage);
			context.Bake<T>(ref data.typeInfo, ExpressionBakingContext.ComponentLocation.Local);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			foreach(var field in BlobExpressionData.GetComponentFields<T>())
			{
				context.AddOutputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentLookupNode<T> : ExprBase, IComponentLookup where T : unmanaged, Unity.Entities.IComponentData
	{
		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public override string Title => $"Lookup {typeof(T).Name}";

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			var index = context.lookupComponents.FindIndex(kv => kv.GetManagedType() == typeof(T));
			if(index == -1)
				throw new System.Exception($"component type {typeof(T).Name} not found in type list");

			expr.type = BTExpr.BTExprType.LookupField;
			expr.data.lookupField = new BTExpr.LookupField
			{
				entity = context.GetExprNodeRef(GetInputPort(0)),
				componentIndex = (byte)index,
			};

			var fields = BlobExpressionData.GetComponentFields<T>();

			var bakedFields = builder.Allocate(ref expr.data.lookupField.fields, fields.Length);
			for(int i = 0; i < fields.Length; i++)
			{
				int offset = UnsafeUtility.GetFieldOffset(fields[i]);
				if(offset > ushort.MaxValue)
					throw new Exception("component too large; field offset over 65k");

				bakedFields[i] = fields[i];
			}
		}

		public override void Bake(ExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.Allocate<LookupComponentField>(storage);
			context.Bake<T>(ref data.typeInfo, ExpressionBakingContext.ComponentLocation.Lookup);
			data.Input0 = context.GetExpressionRef(GetInputPort(0));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<Entity>("Entity")
				.WithDisplayName("Entity")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			/// NOTE: this offsets output indices for fields by 1 (see <see cref="Mpr.Expr.BTExpr.LookupField.Evaluate"/>
			context.AddOutputPort<bool>("HasComponent")
				.WithDisplayName("[Has Component]")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			foreach(var field in BlobExpressionData.GetComponentFields<T>())
			{
				context.AddOutputPort(field.Name)
					.WithDisplayName(field.Name)
					.WithDataType(field.FieldType)
					.Build();
			}
		}
	}

	[Serializable] internal class ReadLocalTransform : ComponentReaderNode<Unity.Transforms.LocalTransform> { }

	[Serializable] internal class LookupLocalToWorld : ComponentLookupNode<Unity.Transforms.LocalToWorld> { }
	[Serializable] internal class LookupLocalTransform : ComponentLookupNode<Unity.Transforms.LocalTransform> { }
}
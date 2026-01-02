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

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<ReadComponentField>(storage);
			context.Bake<T>(ref data.typeInfo, ExpressionComponentLocation.Local);
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

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<LookupComponentField>(storage);
			context.Bake<T>(ref data.typeInfo, ExpressionComponentLocation.Lookup);
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
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	[NodeCategory("Component")]
	public abstract class ComponentReaderNode<T> : ExprBase, IComponentAccess where T : unmanaged, Unity.Entities.IComponentData
	{
		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public ComponentReaderNode() { Title = $"Read {typeof(T).Name}"; }

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

		public ComponentLookupNode() { Title = $"Lookup {typeof(T).Name}"; }

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
				.WithCapacity(PortCapacity.Single)
				.Build();

			/// NOTE: this offsets output indices for fields by 1 (see <see cref="Khorde.Expr.BTExpr.LookupField.Evaluate"/>
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

	[Serializable]
	[NodeCategory("Component")]
	public abstract class BufferReaderNode<T> : ExprBase, IComponentAccess where T : unmanaged, Unity.Entities.IBufferElementData
	{
		private IPort index;

		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public BufferReaderNode() { Title = $"Read {typeof(T).Name}"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<ReadBufferField>(storage);
			data.Input0 = context.GetExpressionRef(index);
			context.BakeBuffer<T>(ref data.typeInfo, ExpressionComponentLocation.Local);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			index = context.AddInputPort<int>("Index")
				.WithDisplayName("Index")
				.WithCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			foreach(var field in BlobExpressionData.GetBufferFields<T>())
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
	public abstract class BufferLengthNode<T> : ExprBase, IComponentAccess where T : unmanaged, Unity.Entities.IBufferElementData
	{
		public ComponentType ComponentType => new ComponentType(typeof(T), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public BufferLengthNode() { Title = $"Read {typeof(T).Name}"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<ReadBufferLength>(storage);
			context.BakeBuffer<T>(ref data.typeInfo, ExpressionComponentLocation.Local);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<int>("Length")
				.WithDisplayName("Length")
				.Build();
		}
	}
}
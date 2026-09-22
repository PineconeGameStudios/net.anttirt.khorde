using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using Unity.Transforms;

namespace Khorde.Expr.Authoring
{
	[Serializable] internal class ReadLocalToWorld : ComponentReaderNode<Unity.Transforms.LocalToWorld>
	{
		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<Expr.ReadLocalToWorld>(storage);
			context.Bake<Unity.Transforms.LocalToWorld>(ref data.typeInfo, ExpressionComponentLocation.Local);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			base.OnDefinePorts(context);

			context.AddOutputPort<float3>("Position")
				.Build();

			context.AddOutputPort<quaternion>("Rotation")
				.Build();

			context.AddOutputPort<float3>("Scale")
				.Build();
		}
	}

	[Serializable] internal class ReadLocalTransform : ComponentReaderNode<Unity.Transforms.LocalTransform> { }

	[Serializable] internal class LookupLocalToWorld : ComponentLookupNode<Unity.Transforms.LocalToWorld>
	{
		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<Expr.LookupLocalToWorld>(storage);
			context.Bake<Unity.Transforms.LocalToWorld>(ref data.typeInfo, ExpressionComponentLocation.Lookup);
			data.Input0 = context.GetExpressionRef(GetInputPort(0));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			base.OnDefinePorts(context);

			context.AddOutputPort<float3>("Position")
				.Build();

			context.AddOutputPort<quaternion>("Rotation")
				.Build();

			context.AddOutputPort<float3>("Scale")
				.Build();
		}
	}

	[Serializable] internal class LookupLocalTransform : ComponentLookupNode<Unity.Transforms.LocalTransform> { }

	[Serializable]
	[Node("Component", iconPath: null, title: "World Distance")]
	internal class WorldDistance : ExprBase, IComponentAccess, IComponentLookup
	{
		private IPort targetPort;

		public ComponentType ComponentType => new ComponentType(typeof(LocalToWorld), ComponentType.AccessMode.ReadOnly);
		public bool IsReadOnly => true;

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<Expr.WorldDistance>(storage);
			context.Bake<LocalToWorld>(ref data.localTypeInfo, ExpressionComponentLocation.Local);
			context.Bake<LocalToWorld>(ref data.lookupTypeInfo, ExpressionComponentLocation.Lookup);
			data.Input0 = context.GetExpressionRef(targetPort);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			targetPort = context.AddInputPort<Entity>("Target")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<bool>("HasComponent")
				.WithDisplayName("[Has Component]")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort<float3>("VectorToTarget")
				.WithDisplayName(string.Empty)
				.WithTooltip("Vector from expression owner to target entity")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort<float>("DistanceToTarget")
				.WithDisplayName(string.Empty)
				.WithTooltip("Distance from expression owner to target entity")
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}

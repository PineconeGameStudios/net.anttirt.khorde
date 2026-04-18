using System;
using Unity.Mathematics;

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
}

using System;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	class Time : ExprBase
	{
		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			context.CreateExpression(storage, new Khorde.Expr.Time { });
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<float>("Time")
				.WithDisplayName("Time")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();

			context.AddOutputPort<float>("DeltaTime")
				.WithDisplayName("DeltaTime")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}
}

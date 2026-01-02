using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring
{
	[Serializable]
	[NodeCategory("Boolean")]
	internal class AndBool : ExprBase
	{
		public override string Title => "And (bool)";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<BinaryBool>(storage);
			data.@operator = BinaryBoolOp.And;
			data.Input0 = context.GetExpressionRef(GetInputPort(0));
			data.Input1 = context.GetExpressionRef(GetInputPort(1));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("a").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddInputPort<bool>("b").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddOutputPort<bool>("out").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).Build();
		}
	}

	[Serializable]
	[NodeCategory("Boolean")]
	internal class OrBool : ExprBase
	{
		public override string Title => "Or (bool)";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<BinaryBool>(storage);
			data.@operator = BinaryBoolOp.Or;
			data.Input0 = context.GetExpressionRef(GetInputPort(0));
			data.Input1 = context.GetExpressionRef(GetInputPort(1));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("a").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddInputPort<bool>("b").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddOutputPort<bool>("out").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).Build();
		}
	}

	[Serializable]
	[NodeCategory("Boolean")]
	internal class NotBool : ExprBase
	{
		public override string Title => "Not (bool)";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.CreateExpression<UnaryBool>(storage);
			data.@operator = UnaryBoolOp.Not;
			data.Input0 = context.GetExpressionRef(GetInputPort(0));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("in").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddOutputPort<bool>("out").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).Build();
		}
	}

}

using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	[NodeCategory("Boolean")]
	internal class AndBool : Base, IExprNode
	{
		public override string Title => "And (bool)";

		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			expr.type = BTExpr.ExprType.Bool;
			expr.data.@bool = new BTExpr.Bool
			{
				index = BTExpr.Bool.BoolType.And,
				data = new BTExpr.Bool.Data
				{
					and = new BTExpr.Bool.And(
						context.GetExprNodeRef(GetInputPort(0)),
						context.GetExprNodeRef(GetInputPort(1))
						),
				}
			};
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
	internal class OrBool : Base, IExprNode
	{
		public override string Title => "Or (bool)";

		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			expr.type = BTExpr.ExprType.Bool;
			expr.data.@bool = new BTExpr.Bool
			{
				index = BTExpr.Bool.BoolType.Or,
				data = new BTExpr.Bool.Data
				{
					or = new BTExpr.Bool.Or(
						context.GetExprNodeRef(GetInputPort(0)),
						context.GetExprNodeRef(GetInputPort(1))
						),
				}
			};
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
	internal class NotBool : Base, IExprNode
	{
		public override string Title => "Not (bool)";

		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			expr.type = BTExpr.ExprType.Bool;
			expr.data.@bool = new BTExpr.Bool
			{
				index = BTExpr.Bool.BoolType.Not,
				data = new BTExpr.Bool.Data
				{
					not = new BTExpr.Bool.Not(
						context.GetExprNodeRef(GetInputPort(0))
						),
				}
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("in").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
			context.AddOutputPort<bool>("out").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).Build();
		}
	}

}

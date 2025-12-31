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

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			expr.type = BTExpr.BTExprType.Bool;
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

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.Allocate<BinaryBool>(storage);
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

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			expr.type = BTExpr.BTExprType.Bool;
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
		
		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.Allocate<BinaryBool>(storage);
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

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			expr.type = BTExpr.BTExprType.Bool;
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

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			ref var data = ref context.Allocate<UnaryBool>(storage);
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

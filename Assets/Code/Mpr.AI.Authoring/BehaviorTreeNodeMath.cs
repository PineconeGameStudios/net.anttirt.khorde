using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.AI.BT.Nodes
{
	[Serializable]
	internal class AddFloat3 : Base, IExprNode
	{
		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context)
		{
			expr.type = BTExpr.ExprType.Float3;
			expr.data.float3 = new BTExpr.Float3
			{
				index = BTExpr.Float3.Float3Type.Add,
				data = new BTExpr.Float3.Data
				{
					add = new BTExpr.Float3.Add(
						context.GetExprNodeRef(GetInputPort(0)),
						context.GetExprNodeRef(GetInputPort(1))
						),
				}
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float3>("a")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<float3>("b")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<float3>("out")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable]
	internal class AndBool : Base, IExprNode
	{
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
	internal class OrBool : Base, IExprNode
	{
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
	internal class NotBool : Base, IExprNode
	{
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

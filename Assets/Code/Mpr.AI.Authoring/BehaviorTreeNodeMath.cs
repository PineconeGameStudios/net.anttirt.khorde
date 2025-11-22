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
						GetInputPort(0).GetExprNodeRef(context),
						GetInputPort(1).GetExprNodeRef(context)
						),
				}
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float3>("a")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float3>("b")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddOutputPort<float3>("out")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}

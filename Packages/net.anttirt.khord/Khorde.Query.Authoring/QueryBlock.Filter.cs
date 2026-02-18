using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Query.Authoring
{
	public interface IFilter : INode
	{
		void Bake(ref QSFilter qsFilter, QueryBakingContext queryBakingContext);
	}

	[UseWithContext(typeof(IPass))]
	[Serializable]
	class ExpressionFilter : QueryGraphBlockBase, IFilter
	{
		public override string Title => $"Expression Filter";

		public void Bake(ref QSFilter qsFilter, QueryBakingContext queryBakingContext)
		{
			qsFilter.type = QSFilter.FilterType.Expression;
			qsFilter.expr = queryBakingContext.GetExpressionRef(GetInputPort(0));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("in_pass")
				.WithDisplayName("Accept Item")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}
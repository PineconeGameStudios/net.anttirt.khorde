using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	public interface IScorer : INode
	{
		void Bake(ref QSScorer qSScorer, QueryBakingContext queryBakingContext);
	}

	[UseWithContext(typeof(IPass))]
	[Serializable]
	class ExpressionScorer : QueryGraphBlockBase, IScorer
	{
		public override string Title => $"Expression Scorer";

		public void Bake(ref QSScorer scorer, QueryBakingContext queryBakingContext)
		{
			scorer.type = QSScorer.ScorerType.Expression;
			scorer.expr = queryBakingContext.GetExprNodeRef(GetInputPort(0));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float>("in_score")
				.WithDisplayName(string.Empty)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}

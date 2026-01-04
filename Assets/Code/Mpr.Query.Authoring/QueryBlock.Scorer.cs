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
		private INodeOption normalizerOption;
		private INodeOption negateOption;

		public void Bake(ref QSScorer scorer, QueryBakingContext queryBakingContext)
		{
			scorer.type = QSScorer.ScorerType.Expression;
			scorer.expr = queryBakingContext.GetExpressionRef(GetInputPort(0));
			normalizerOption.TryGetValue(out scorer.normalizer);
			negateOption.TryGetValue(out scorer.negate);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			normalizerOption = context.AddOption<QSScorer.Normalizer>("normalizer")
				.WithDisplayName("Normalizer")
				.Build();

			negateOption = context.AddOption<bool>("negate")
				.WithDisplayName("Negate")
				.Build();
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

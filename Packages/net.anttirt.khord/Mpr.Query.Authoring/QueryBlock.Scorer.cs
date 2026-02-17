using System;
using Unity.GraphToolkit.Editor;

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
		private INodeOption noiseOption;

		public void Bake(ref QSScorer scorer, QueryBakingContext queryBakingContext)
		{
			scorer.type = QSScorer.ScorerType.Expression;
			scorer.expr = queryBakingContext.GetExpressionRef(GetInputPort(0));
			normalizerOption.TryGetValue(out scorer.normalizer);
			negateOption.TryGetValue(out scorer.negate);
			noiseOption.TryGetValue(out scorer.noise);
		}

		public override void Validate(GraphLogger logger)
		{
			noiseOption.TryGetValue<float>(out var noise);
			if(!(noise >= 0 && noise <= 1.0f))
				logger.LogError("noise must be between 0 (no noise) and 1 (completely random)", this);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			normalizerOption = context.AddOption<QSScorer.Normalizer>("normalizer")
				.WithDisplayName("Normalizer")
				.Build();

			negateOption = context.AddOption<bool>("negate")
				.WithDisplayName("Negate")
				.Build();

			noiseOption = context.AddOption<float>("noise")
				.WithDisplayName("Noise")
				.WithTooltip("Add noise to scores (0.0 = no noise, 1.0 = completely random)")
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

using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	[UseWithContext(typeof(PassInt2), typeof(PassFloat2), typeof(PassEntity))]
	[Serializable]
	class ExpressionScorer : QueryBlockBase
	{
		public override string Title => $"Expression Scorer";

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

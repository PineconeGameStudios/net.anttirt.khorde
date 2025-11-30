using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	[UseWithContext(typeof(PassInt2), typeof(PassFloat2), typeof(PassEntity))]
	[Serializable]
	class ExpressionFilter : QueryBlockBase
	{
		public override string Title => $"Expression Filter";

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<bool>("in_pass")
				.WithDisplayName(string.Empty)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}
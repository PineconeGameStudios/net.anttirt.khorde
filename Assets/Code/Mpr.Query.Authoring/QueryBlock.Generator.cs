
using System;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	[Serializable]
	[UseWithContext(typeof(PassFloat2))]
	class GeneratorFloat2_Rectangle : QueryBlockBase
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float2>("center")
				.WithDisplayName("Center")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float>("density")
				.WithDisplayName("Density")
				.WithDefaultValue(1.0f)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float2>("dimensions")
				.WithDisplayName("Dimensions")
				.WithDefaultValue(new float2(4, 4))
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

		}
	}

	[Serializable]
	[UseWithContext(typeof(PassFloat2))]
	class GeneratorFloat2_Circle : QueryBlockBase
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float2>("center")
				.WithDisplayName("Center")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float>("density")
				.WithDisplayName("Density")
				.WithDefaultValue(1.0f)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float>("radius")
				.WithDisplayName("Radius")
				.WithDefaultValue(10.0f)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}
}
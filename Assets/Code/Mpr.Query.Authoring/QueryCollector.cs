using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	/// <summary>
	/// Marker type for passes
	/// </summary>
	class PassRef { }

	abstract class PassContext<T> : QueryContextBase
	{
		public override string Title => $"Pass ({typeof(T).Name})";

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<PassRef>("pass")
				.WithDisplayName("Pass")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable] class PassInt2 : PassContext<int2> { }
	[Serializable] class PassFloat2 : PassContext<float2> { }
	[Serializable] class PassEntity : PassContext<Entity> { }

	class QueryNode : QueryNodeBase
	{
		public override string Title => "Query";

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<int>("pass_count")
				.WithDisplayName("Pass Count")
				.WithTooltip("Passes are evaluated in order until there are enough results or all passes have been evaluated.")
				.WithDefaultValue(1)
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<int>("result_count")
				.WithDisplayName("Result Count")
				.WithDefaultValue(1)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			GetNodeOptionByName("pass_count").TryGetValue<int>(out var passCount);

			for(int i = 0; i < passCount; i++)
			{
				context.AddInputPort<PassRef>($"pass_{i}")
					.WithDisplayName($"Pass #{i + 1}")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Single)
					.Build();
			}
		}
	}
}

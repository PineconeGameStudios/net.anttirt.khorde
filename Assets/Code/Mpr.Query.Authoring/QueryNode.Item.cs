
using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	abstract class ItemNodeBase<T> : QueryGraphNodeBase
	{
		public override string Title => $"Current Item ({typeof(T).Name})";

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<T>("item")
				.WithDisplayName(string.Empty)
				.WithPortCapacity(PortCapacity.Multi)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable] class CurrentItemInt2 : ItemNodeBase<int2> { }
	[Serializable] class CurrentItemFloat2 : ItemNodeBase<float2> { }
	[Serializable] class CurrentItemEntity : ItemNodeBase<Entity> { }
}
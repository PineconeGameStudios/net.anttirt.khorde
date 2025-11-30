using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	/// <summary>
	/// Marker type for passes
	/// </summary>
	class PassRef<T> { }

	public interface IPass : INode { }
	public interface IPass<T> : IPass where T : unmanaged { }

	public abstract class Pass<T> : QueryGraphContextBase, IPass<T> where T : unmanaged
	{
		public override string Title => $"Pass ({typeof(T).Name})";

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<PassRef<T>>("pass")
				.WithDisplayName("Pass")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable] [NodeCategory("Query")] class PassInt2 : Pass<int2> { }
	[Serializable] [NodeCategory("Query")] class PassFloat2 : Pass<float2> { }
	[Serializable] [NodeCategory("Query")] class PassEntity : Pass<Entity> { }

	public interface IQuery : INode
	{
		List<IPort> GetPassPorts();
		IPort GetResultCountPort();
		Type ItemType { get; }
	}

	public abstract class Query<T> : QueryGraphNodeBase, IQuery
	{
		public override string Title => $"Query ({typeof(T).Name})";

		public Type ItemType => typeof(T);
		public Type PassRefType => typeof(PassRef<T>);

		public List<IPort> GetPassPorts() => GetInputPorts().Where(p => p.dataType == PassRefType).ToList();
		public IPort GetResultCountPort() => GetInputPort(0);

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
				context.AddInputPort<PassRef<T>>($"pass_{i}")
					.WithDisplayName($"Pass #{i + 1}")
					.WithConnectorUI(PortConnectorUI.Circle)
					.WithPortCapacity(PortCapacity.Single)
					.Build();
			}
		}
	}

	[Serializable] [NodeCategory("Query")] class QueryInt2 : Query<int2> { }
	[Serializable] [NodeCategory("Query")] class QueryFloat2 : Query<float2> { }
	[Serializable] [NodeCategory("Query")] class QueryEntity : Query<Entity> { }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Query.Authoring
{
	/// <summary>
	/// Marker type for passes
	/// </summary>
	class PassRef<T> { }

	public interface IPass : INode { }
	public interface IPass<T> : IPass where T : unmanaged { }

	public abstract class Pass<T> : QueryGraphContextBase, IPass<T> where T : unmanaged
	{
		public override string Title => $"Query Pass";

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<PassRef<T>>("pass")
				.WithDisplayName("Pass")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Arrowhead)
				.Build();
		}

		public override void Validate(GraphLogger logger)
		{
			bool haveGenerators = blockNodes.Any(b => b is IGenerator);
			if(!haveGenerators)
				logger.LogError("must have at least one generator", this);
		}
	}

	[Serializable][NodeCategory("Query")] class QueryPassInt2 : Pass<int2> { }
	[Serializable][NodeCategory("Query")] class QueryPassInt3 : Pass<int3> { }
	[Serializable][NodeCategory("Query")] class QueryPassFloat2 : Pass<float2> { }
	[Serializable][NodeCategory("Query")] class QueryPassFloat3 : Pass<float3> { }
	[Serializable][NodeCategory("Query")] class QueryPassEntity : Pass<Entity> { }

	public interface IQuery : INode
	{
		List<IPort> GetPassPorts();
		IPort GetResultCountPort();
		Type ItemType { get; }
		QueryScoringDirection ScoringDirection { get; }
	}

	public abstract class Query<T> : QueryGraphNodeBase, IQuery
	{
		private INodeOption scoringDirection;
		private INodeOption passCountOption;

		const int MinPassCount = 1;
		const int MaxPassCount = 10;

		public override string Title => $"Query (Result Item: {typeof(T).Name})";

		public Type ItemType => typeof(T);
		public Type PassRefType => typeof(PassRef<T>);

		public QueryScoringDirection ScoringDirection
		{
			get
			{
				scoringDirection.TryGetValue<QueryScoringDirection>(out var value);
				return value;
			}
		}

		public List<IPort> GetPassPorts() => GetInputPorts().Where(p => p.dataType == PassRefType).ToList();
		public IPort GetResultCountPort() => GetInputPort(0);

		public override void Validate(GraphLogger logger)
		{
			passCountOption.TryGetValue<int>(out var passCount);

			if(passCount < MinPassCount)
			{
				logger.LogError("must have at least 1 pass", this);
				return;
			}

			foreach(var port in GetInputPorts().Skip(1))
			{
				if(port.isConnected)
					return;
			}

			logger.LogError("at least one pass must be connected", this);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			scoringDirection = context.AddOption<QueryScoringDirection>("scoring_direction")
				.WithDisplayName("Scoring Direction")
				.Build();

			passCountOption = context.AddOption<int>("pass_count")
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

			passCountOption.TryGetValue<int>(out var passCount);

			if(passCount < MinPassCount || passCount > MaxPassCount)
				return;

			for(int i = 0; i < passCount; i++)
			{
				context.AddInputPort<PassRef<T>>($"pass_{i}")
					.WithDisplayName($"Pass #{i + 1}")
					.WithConnectorUI(PortConnectorUI.Arrowhead)
					.WithPortCapacity(PortCapacity.Single)
					.Build();
			}
		}
	}

	[Serializable][NodeCategory("Query")] class QueryInt2 : Query<int2> { }
	[Serializable][NodeCategory("Query")] class QueryInt3 : Query<int3> { }
	[Serializable][NodeCategory("Query")] class QueryFloat2 : Query<float2> { }
	[Serializable][NodeCategory("Query")] class QueryFloat3 : Query<float3> { }
	[Serializable][NodeCategory("Query")] class QueryEntity : Query<Entity> { }
}

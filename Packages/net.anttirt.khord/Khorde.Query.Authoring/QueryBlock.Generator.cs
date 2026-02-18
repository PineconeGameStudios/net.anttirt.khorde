
using System;
using Khorde.Blobs;
using Khorde.Blobs.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEngine;

namespace Khorde.Query.Authoring
{
	public interface IGenerator : INode
	{
		void Bake(ref QSGenerator generator, QueryBakingContext queryBakingContext);
	}

	[Serializable]
	[UseWithContext(typeof(IPass<float2>))]
	class GeneratorFloat2_Rectangle : QueryGraphBlockBase, IGenerator
	{
		public void Bake(ref QSGenerator generator, QueryBakingContext queryBakingContext)
		{
			generator.generatorType = QSGenerator.GeneratorType.Float2Rectangle;
			generator.data.float2Rectangle = new QSGenerator.Float2Rectangle
			{
				center = queryBakingContext.GetExpressionRef(GetInputPortByName("center")),
				orientation = queryBakingContext.GetExpressionRef(GetInputPortByName("orientation")),
				size = queryBakingContext.GetExpressionRef(GetInputPortByName("size")),
				spacing = queryBakingContext.GetExpressionRef(GetInputPortByName("spacing")),
			};
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<float2>("center")
				.WithDisplayName("Center")
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float>("orientation")
				.WithDisplayName("Orientation")
				.WithDefaultValue(1.0f)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float2>("size")
				.WithDisplayName("Size")
				.WithDefaultValue(new float2(4, 4))
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();

			context.AddInputPort<float>("spacing")
				.WithDisplayName("Spacing")
				.WithDefaultValue(1.0f)
				.WithPortCapacity(PortCapacity.Single)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable]
	[UseWithContext(typeof(IPass<float2>))]
	class GeneratorFloat2_Circle : QueryGraphBlockBase, IGenerator
	{
		public void Bake(ref QSGenerator generator, QueryBakingContext queryBakingContext)
		{
			throw new NotImplementedException();
		}

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
	
	[Serializable]
	[UseWithContext(typeof(IPass<Entity>))]
	class GeneratorEntityQuery : QueryGraphBlockBase, IGenerator
	{
		private INodeOption query;
		
		public void Bake(ref QSGenerator generator, QueryBakingContext queryBakingContext)
		{
			if (!query.TryGetValue<EntityQueryAsset>(out var entityQuery))
			{
				return;
			}

			generator.generatorType = QSGenerator.GeneratorType.Entities;
			generator.data.entities = new QSGenerator.Entities
			{
				queryHash = entityQuery.DataHash,
			};
			
			queryBakingContext.AddQuery(entityQuery);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			query = context.AddOption<EntityQueryAsset>("entityQuery")
				.WithDisplayName(string.Empty)
				.WithTooltip("Entity query asset to use for base entity generation")
				.Build();
		}
	}
}
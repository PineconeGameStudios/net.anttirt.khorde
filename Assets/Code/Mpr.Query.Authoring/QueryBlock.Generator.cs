
using System;
using Mpr.Blobs;
using Mpr.Blobs.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
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
			generator.generatorType = QSGenerator.GeneratorType.Entities;
			generator.data.entities = new QSGenerator.Entities
			{
				queryIndex = 0,
			};
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			query = context.AddOption<string>("query")
				.WithDefaultValue("all: LocalTransform; none: Disabled")
				.WithDisplayName("Query")
				.WithTooltip("Entity query description to be parsed")
				.Build();
		}

		public override void Validate(GraphLogger logger)
		{
			var bb = new BlobBuilder(Allocator.Temp);
			ref var desc = ref bb.ConstructRoot<BlobEntityQueryDesc>();
			query.TryGetValue(out string pattern);
			BlobEntityQueryDescAuthoring.Bake(ref desc, pattern, ref bb, err => logger.LogError(err, this));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{

		}
	}
}
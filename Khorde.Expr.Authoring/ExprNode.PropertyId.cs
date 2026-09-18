using System;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	class ShaderPropertyId : ExprBase
	{
		private INodeOption propertyName;

		public override void OnEnable() { Title = "Shader Prop"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			this.propertyName.TryGetValue<string>(out var propertyName);
			context.CreateExpression(storage, new Khorde.Expr.Ref { @ref = context.ShaderPropertyId(propertyName) });
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			propertyName = context.AddOption<string>("PropertyName")
				.WithDisplayName(string.Empty)
				.WithTooltip("Property Name")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<int>("Id")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable]
	class AnimatorPropertyId : ExprBase
	{
		private INodeOption propertyName;

		public override void OnEnable() { Title = "Animator Prop"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			this.propertyName.TryGetValue<string>(out var propertyName);
			context.CreateExpression(storage, new Khorde.Expr.Ref { @ref = context.AnimatorPropertyId(propertyName) });
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			propertyName = context.AddOption<string>("PropertyName")
				.WithDisplayName(string.Empty)
				.WithTooltip("Property Name")
				.Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<int>("Id")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}
}

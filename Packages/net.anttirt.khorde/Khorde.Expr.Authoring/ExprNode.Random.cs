using System;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	class RandomInt : ExprBase
	{
		private INodeOption minOption;
		private INodeOption maxOption;

		public override string Title => "Random";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			var expr = new Khorde.Expr.RandomInt { };
			minOption.TryGetValue(out expr.min);
			maxOption.TryGetValue(out expr.max);
			context.CreateExpression(storage, expr);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			minOption = context.AddOption<int>("Min").Build();
			maxOption = context.AddOption<int>("Max").Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<int>("Result")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable]
	class RandomFloat : ExprBase
	{
		private INodeOption minOption;
		private INodeOption maxOption;

		public override string Title => "Random";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			var expr = new Khorde.Expr.RandomInt { };
			minOption.TryGetValue(out expr.min);
			maxOption.TryGetValue(out expr.max);
			context.CreateExpression(storage, expr);
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			minOption = context.AddOption<float>("Min").Build();
			maxOption = context.AddOption<float>("Max").Build();
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<float>("Result")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable]
	class RandomFloat2Direction : ExprBase
	{
		public override string Title => "Random Direction";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			context.CreateExpression(storage, new Khorde.Expr.RandomFloat2Direction());
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<float2>("Result")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable]
	class RandomFloat3Direction : ExprBase
	{
		public override string Title => "Random Direction";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			context.CreateExpression(storage, new Khorde.Expr.RandomFloat3Direction());
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<float3>("Result")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}
}

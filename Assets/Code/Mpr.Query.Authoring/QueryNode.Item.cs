
using System;
using Mpr.Expr.Authoring;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Query.Authoring
{
	interface IQueryCurrentItemNode
	{
		Type ItemType { get; }
	}
	
	abstract class ItemNodeBase<T> : QueryGraphNodeBase, IExprNode, IQueryCurrentItemNode
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

		public void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			context.CreateExpression(storage, new CurrentQueryItem());
		}

		public Type ItemType => typeof(T);
	}

	[Serializable] class CurrentItemInt2 : ItemNodeBase<int2> { }
	[Serializable] class CurrentItemFloat2 : ItemNodeBase<float2> { }
	[Serializable] class CurrentItemEntity : ItemNodeBase<Entity> { }
}
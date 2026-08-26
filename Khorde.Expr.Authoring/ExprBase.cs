using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	[UseWithGraph(typeof(IExprGraph))]
	public abstract class ExprBase : Node, IExprNode
	{
		public abstract void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage);
	}
}

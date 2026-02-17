using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring
{
	public abstract class ExprBase : Node, IExprNode
	{
		public abstract void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage);
	}
}

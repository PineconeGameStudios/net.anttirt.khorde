using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	public interface IExprNode : INode
	{
		public void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage);
	}

}
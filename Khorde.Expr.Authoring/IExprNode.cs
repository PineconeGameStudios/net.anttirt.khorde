using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	public interface IExprNode : INode
	{
		public void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage);
	}

	public interface ICustomExprNode : INode
	{
		public ExpressionRef GetExpressionRef(GraphExpressionBakingContext context, IPort port);
	}
}
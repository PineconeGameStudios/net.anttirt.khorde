using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr
{
	public abstract class ExprNode : Node, IExprNode
	{
		public abstract void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context);
	}
}

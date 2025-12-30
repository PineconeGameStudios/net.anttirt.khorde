using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring
{
	public interface IExprNode : INode
	{
		public void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context);
		public void Bake(ExpressionBakingContext context, ref ExpressionData data);
	}

}
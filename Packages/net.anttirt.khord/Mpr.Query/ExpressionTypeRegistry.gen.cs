using Mpr.Query;
using UnityEngine.Scripting;

namespace Mpr.Expr.Generated
{
	/// <summary>
	/// This is a generated class used for quickly loading type
	/// information at runtime. One is generated for each assembly
	/// that contains <see cref="IExpression"/> types.
	/// </summary>
	[Preserve]
	static unsafe class ExpressionTypeRegistry
	{
	    public static readonly ExpressionTypeInfo[] ExpressionTypes = new[]
	    {
	        new ExpressionTypeInfo(typeof(CurrentQueryItem), CurrentQueryItem.EvaluateFunc, true),
	    };
	}
}
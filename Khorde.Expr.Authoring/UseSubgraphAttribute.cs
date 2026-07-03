using System;

namespace Khorde.Expr.Authoring
{
	[System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public sealed class UseSubgraphAttribute : System.Attribute
	{
		readonly Type subGraphType;

		public UseSubgraphAttribute(Type subGraphType)
		{
			this.subGraphType = subGraphType;
		}

		public Type SubGraphType
		{
			get { return subGraphType; }
		}
	}
}
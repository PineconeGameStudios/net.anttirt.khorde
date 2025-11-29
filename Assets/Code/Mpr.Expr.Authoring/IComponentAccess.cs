using System;

namespace Mpr.Expr.Authoring
{
	public interface IComponentAccess
	{
		public Type ComponentType { get; }
		public bool IsReadOnly { get; }
	}

}
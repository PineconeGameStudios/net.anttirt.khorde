using System;

namespace Mpr.Expr
{
	public interface IComponentAccess
	{
		public Type ComponentType { get; }
		public bool IsReadOnly { get; }
	}

}
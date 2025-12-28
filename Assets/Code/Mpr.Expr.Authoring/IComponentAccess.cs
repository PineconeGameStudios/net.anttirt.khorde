using Unity.Entities;

namespace Mpr.Expr.Authoring
{
	public interface IComponentAccess
	{
		public ComponentType ComponentType { get; }
		public bool IsReadOnly { get; }
	}

	public interface IComponentLookup
	{
		public ComponentType ComponentType { get; }
		public bool IsReadOnly { get; }
	}
}
using Unity.Entities;

namespace Khorde.Expr.Authoring
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
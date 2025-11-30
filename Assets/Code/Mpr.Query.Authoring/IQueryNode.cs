using Unity.GraphToolkit.Editor;

namespace Mpr.Query.Authoring
{
	public interface IQueryNode
	{

	}

	abstract class QueryNodeBase : Node, IQueryNode
	{

	}

	abstract class QueryContextBase : ContextNode, IQueryNode
	{

	}

	abstract class QueryBlockBase : BlockNode, IQueryNode
	{

	}
}
using Unity.GraphToolkit.Editor;

namespace Mpr.Query.Authoring
{
	public interface IQueryGraphNode : INode { }

	public abstract class QueryGraphNodeBase : Node, IQueryGraphNode { }

	public abstract class QueryGraphContextBase : ContextNode, IQueryGraphNode { }

	public abstract class QueryGraphBlockBase : BlockNode, IQueryGraphNode { }
}
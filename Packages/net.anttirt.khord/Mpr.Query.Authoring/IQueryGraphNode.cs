using Unity.GraphToolkit.Editor;

namespace Mpr.Query.Authoring
{
	public interface IQueryGraphNode : INode
	{
		void Validate(GraphLogger logger);
	}

	public abstract class QueryGraphNodeBase : Node, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}

	public abstract class QueryGraphContextBase : ContextNode, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}

	public abstract class QueryGraphBlockBase : BlockNode, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}
}
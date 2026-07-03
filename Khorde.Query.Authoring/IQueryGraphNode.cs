using System;
using Unity.GraphToolkit.Editor;

namespace Khorde.Query.Authoring
{
	public interface IQueryGraphNode : INode
	{
		void Validate(GraphLogger logger);
	}

	[Serializable]
	public abstract class QueryGraphNodeBase : Node, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}

	[Serializable]
	public abstract class QueryGraphContextBase : ContextNode, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}

	[Serializable]
	public abstract class QueryGraphBlockBase : BlockNode, IQueryGraphNode
	{
		public virtual void Validate(GraphLogger logger) { }
	}
}
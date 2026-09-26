using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	public abstract class ExecBase : Node, IExecNode
	{
		public const string EXEC_PORT_DEFAULT_NAME = "Execution";

		public abstract void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId);

		public virtual int NodeCount => 1;
	}

	/// <summary>
	/// Marker type for ports of type Execution
	/// </summary>
	// NOTE: Graphtoolkit hardcodes this specific type name to get a nice
	// visual style. It also defaults to an internal fake "execution flow" type
	// if no data type for a port is specified, but refactoring to subgraphs
	// does not work correctly if that type is used, so we use a real type for
	// the ports instead as a workaround.
	[Serializable]
	public class ExecutionFlow { }

	public interface IExecNode : INode
	{
		public void Register(BTBakingContext context, BTExecNodeId nodeId) { }
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex, BTExecNodeId nodeId);
		public int NodeCount { get; }

		/// <summary>
		/// Get an ordered list of all output ports of type <see cref="ExecutionFlow"/>. The index in this list corresponds to <see cref="BTStackFrame.execOutputIndex"/>.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<IPort> GetOutputExecPorts()
		{
			if(this is Node node)
			{
				foreach(var port in EnumPorts(node))
					yield return port;
			}

			if(this is ContextNode context)
			{
				foreach(var block in context.BlockNodes)
				{
					foreach(var port in EnumPorts(block))
						yield return port;
				}
			}
		}

		protected static IEnumerable<IPort> EnumPorts(Node node)
		{
			foreach(var port in node.GetOutputPorts())
				if(port.DataType == typeof(ExecutionFlow))
					yield return port;
		}
	}

	public interface IUtilityNode : INode
	{
		public IPort GetUtilityDebugPort();
	}
}
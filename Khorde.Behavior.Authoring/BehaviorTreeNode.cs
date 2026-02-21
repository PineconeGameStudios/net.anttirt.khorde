using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Behavior.Authoring
{
	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	public abstract class ExecBase : Node, IExecNode
	{
		public const string EXEC_PORT_DEFAULT_NAME = "Execution";

		public abstract void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex);

		public virtual int NodeCount => 1;
	}

	/// <summary>
	/// Marker type for ports of type Execution
	/// </summary>
	[Serializable]
	public class Exec { }

	public interface IExecNode : INode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BTBakingContext context, int nodeIndex);
		public int NodeCount { get; }
	}

}
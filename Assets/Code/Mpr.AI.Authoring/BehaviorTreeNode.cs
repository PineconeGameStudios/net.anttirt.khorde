using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	public interface IComponentAccess
	{
		public Type ComponentType { get; }
		public bool IsReadOnly { get; }
	}

	[Serializable]
	[UseWithGraph(typeof(BehaviorTreeGraph))]
	public abstract class Base : Node
	{
		public const string EXEC_PORT_DEFAULT_NAME = "Execution";
	}

	/// <summary>
	/// Marker type for ports of type Execution
	/// </summary>
	[Serializable]
	public class Exec { }

	public interface IExecNode : INode
	{
		public void Bake(ref BlobBuilder builder, ref BTExec exec, BakingContext context);
	}

	public interface IExprNode : INode
	{
		public void Bake(ref BlobBuilder builder, ref BTExpr expr, BakingContext context);
	}

}
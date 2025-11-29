using Codice.Client.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.AI.BT.Nodes
{
	public class BakingContext : IDisposable
	{
		public Graph rootGraph;
		public Dictionary<NodeKey<IExecNode>, BTExecNodeId> execNodeMap;
		public Dictionary<NodeKey<IExprNode>, BTExprNodeRef> exprNodeMap;
		public NativeList<byte> constStorage;
		public HashSet<Type> componentTypeSet;
		public List<Type> componentTypes;
		public List<string> errors;
		public List<string> warnings;
		public SubgraphStack subgraphStack;

		public BakingContext(Graph rootGraph)
		{
			this.rootGraph = rootGraph ?? throw new ArgumentNullException(nameof(rootGraph));

			constStorage = new NativeList<byte>(Allocator.Persistent);
			componentTypeSet = new();
			componentTypes = new();
			execNodeMap = new();
			exprNodeMap = new();
			errors = new();
			warnings = new();
			subgraphStack = new();
		}

		public BlobBuilder Bake(AllocatorManager.AllocatorHandle allocator)
		{
			var roots = rootGraph.GetNodes().OfType<Root>().ToList();
			if(roots.Count == 0)
			{
				errors.Add($"no Root node found");
				return default;
			}

			if(roots.Count > 1)
			{
				errors.Add($"graph must have exactly one Root node, {roots.Count} found");
				return default;
			}

			RegisterExecNode(null); // 0: NOP node for default exec stubs
			RegisterExecNode(roots[0]); // 1: Root
			RegisterNodes(rootGraph);

			componentTypes = componentTypeSet.OrderBy(t => t.FullName).ToList();

			var builder = new BlobBuilder(allocator);

			ref var data = ref builder.ConstructRoot<BTData>();
			var execs = builder.Allocate(ref data.execs, execNodeMap.Count);
			var exprs = builder.Allocate(ref data.exprData.exprs, exprNodeMap.Count);
			var types = builder.Allocate(ref data.exprData.componentTypes, componentTypes.Count);
			var exprNodeIds = builder.Allocate(ref data.exprData.exprNodeIds, exprNodeMap.Count);
			var execNodeIds = builder.Allocate(ref data.execNodeIds, execNodeMap.Count);
			var execNodeSubgraphStacks = builder.Allocate(ref data.execNodeSubgraphStacks, execNodeMap.Count);

			for(int i = 0; i < componentTypes.Count; ++i)
				types[i] = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(componentTypes[i])).StableTypeHash;

			BakeNodes(rootGraph, ref builder, ref execs, ref exprs, ref execNodeIds, ref exprNodeIds, ref execNodeSubgraphStacks);

			BehaviorTreeAuthoringExt.BakeConstStorage(ref builder, ref data, constStorage);

			return builder;
		}

		void BakeNodes(Graph graph,
			ref BlobBuilder builder,
			ref BlobBuilderArray<BTExec> execs,
			ref BlobBuilderArray<BTExpr> exprs,
			ref BlobBuilderArray<UnityEngine.Hash128> execNodeIds,
			ref BlobBuilderArray<UnityEngine.Hash128> exprNodeIds,
			ref BlobBuilderArray<BlobArray<UnityEngine.Hash128>> execNodeSubgraphStack
			)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					BakeNodes(subgraphNode.GetSubgraph(), ref builder, ref execs, ref exprs, ref execNodeIds, ref exprNodeIds, ref execNodeSubgraphStack);
					PopSubgraph();
				}
				else if(node is IExecNode execNode)
				{
					var index = GetNodeId(execNode).index;
					execNodeIds[index] = execNode.Guid;
					var subgraphStackIds = builder.Allocate(ref execNodeSubgraphStack[index], subgraphStack.Depth);
					int i = 0;
					foreach(var hash in subgraphStack.Hashes)
						subgraphStackIds[i++] = hash;

					execNode.Bake(ref builder, ref execs[index], this);
				}
				else if(node is IExprNode exprNode)
				{
					var index = GetNodeId(exprNode).index;
					exprNodeIds[index] = exprNode.Guid;
					exprNode.Bake(ref builder, ref exprs[index], this);
				}
			}
		}

		public void Dispose()
		{
			constStorage.Dispose();
		}

		public struct SubgraphStackStackSave : IDisposable
		{
			BakingContext context;
			SubgraphStack saved;

			public SubgraphStackStackSave(BakingContext context)
			{
				this.context = context;
				this.saved = context.subgraphStack.Clone();
			}

			public void Dispose()
			{
				context.subgraphStack = saved;
			}
		}

		public SubgraphStackStackSave SaveSubgraph() { return new SubgraphStackStackSave(this); }
		public void PushSubgraph(ISubgraphNode subgraphNode) => subgraphStack.Push(subgraphNode);
		public void PopSubgraph() => subgraphStack.Pop();

		public NodeKey<IExecNode> GetNodeKey(IExecNode execNode) => new(subgraphStack.GetKey(), execNode);
		public NodeKey<IExprNode> GetNodeKey(IExprNode exprNode) => new(subgraphStack.GetKey(), exprNode);

		public void RegisterExecNode(IExecNode execNode)
		{
			var index = execNodeMap.Count;
			if(index > ushort.MaxValue)
				throw new Exception("max exec node capacity exceeded");
			if(!execNodeMap.TryAdd(GetNodeKey(execNode), new BTExecNodeId((ushort)index)))
				throw new Exception("duplicate node key");
		}

		public void RegisterExprNode(IExprNode exprNode)
		{
			var index = exprNodeMap.Count;
			if(index > ushort.MaxValue)
				throw new Exception("max expr node capacity exceeded");
			if(!exprNodeMap.TryAdd(GetNodeKey(exprNode), new BTExprNodeRef((ushort)index, 0, false)))
				throw new Exception("duplicate node key");
		}

		public BTExecNodeId GetNodeId(IExecNode execNode)
		{
			return execNodeMap[GetNodeKey(execNode)];
		}

		public BTExprNodeRef GetNodeId(IExprNode exprNode)
		{
			return exprNodeMap[GetNodeKey(exprNode)];
		}

		public Graph CurrentGraph
		{
			get
			{
				if(subgraphStack.Depth > 0)
					return subgraphStack.Current.GetSubgraph();

				return rootGraph;
			}
		}

		void RegisterNodes(Graph graph)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					RegisterNodes(subgraphNode.GetSubgraph());
					PopSubgraph();
				}
				else if(node is IExecNode execNode)
				{
					if(node is not Root) // Root is registered separately
						RegisterExecNode(execNode);
				}
				else if(node is IExprNode exprNode)
				{
					RegisterExprNode(exprNode);
				}

				if(node is IComponentAccess componentAccess)
				{
					componentTypeSet.Add(componentAccess.ComponentType);
				}
			}
		}

		public BTExecNodeId GetTargetNodeId(IPort srcPort)
		{
			var dstPorts = new List<IPort>();

			using var _ = SaveSubgraph();

			INode srcNode = srcPort.GetNode();
			srcPort.GetConnectedPorts(dstPorts);

			if(dstPorts.Count == 0)
				return default;

			if(dstPorts.Count > 1)
				errors.Add($"node {srcPort.GetNode()} port {srcPort} is connected to multiple exec ports");

			var dstPort = dstPorts[0];
			var dstNode = dstPort.GetNode();

			while(true)
			{
				if(dstNode is ISubgraphNode subgraphNode)
				{
					subgraphStack.Push(subgraphNode);

					var dstVariable = subgraphNode.GetVariableForInputPort(dstPort);
					var subgraph = subgraphNode.GetSubgraph();
					var dstVariableNodes = subgraph.GetNodes().OfType<IVariableNode>().Where(vn => vn.variable == dstVariable).ToList();

					if(dstVariableNodes.Count == 0)
					{
						warnings.Add($"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(dstVariableNodes.Count > 1)
						errors.Add($"subgraph {subgraph} exec variable {dstVariable} has multiple instances");

					srcNode = dstVariableNodes[0];

					if(srcNode.outputPortCount == 0)
					{
						warnings.Add($"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(srcNode.outputPortCount > 1)
						errors.Add($"subgraph {subgraph} node {srcNode} has multiple exec output ports");

					dstPorts.Clear();
					srcNode.GetOutputPort(0).GetConnectedPorts(dstPorts);

					if(dstPorts.Count == 0)
					{
						warnings.Add($"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(dstPorts.Count > 1)
						errors.Add($"subgraph {subgraph} node {srcNode} is connected to multiple exec ports");

					dstPort = dstPorts[0];
					dstNode = dstPort.GetNode();
				}

				// else if(dstNode is IVariableNode varNode)
				// {
				// 	errors.Add($"subgraph exec outputs not implemented");

				// 	var currentSubgraph = subgraphStack.Current;

				// 	subgraphStack.Pop();

				// 	// TODO: exit subgraph and follow in parent subgraph
				// 	// var srcPort = currentSubgraph.GetOutputPortForVariable(varNode);

				// 	return default;
				// }

				else if(dstNode is IExecNode execNode)
				{
					return GetNodeId(execNode);
				}

				else
				{
					errors.Add($"unhandled exec node type {dstNode.GetType().Name}");
					return default;
				}
			}
		}

		public BTExprNodeRef GetExprNodeRef(IPort dstPort)
		{
			if(!dstPort.isConnected)
				return HandleDisconnectedPort(dstPort);

			using var _ = SaveSubgraph();

			var srcPorts = new List<IPort>();
			dstPort.GetConnectedPorts(srcPorts);

			if(srcPorts.Count > 1)
				errors.Add($"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

			var srcPort = srcPorts[0];
			var srcNode = srcPort.GetNode();

			while(true)
			{
				if(srcNode is IVariableNode varNode)
				{
					dstPort = subgraphStack.Current.GetInputPortForVariable(varNode.variable);
					if(!dstPort.isConnected)
						return HandleDisconnectedPort(dstPort);

					subgraphStack.Pop();

					srcPorts.Clear();
					dstPort.GetConnectedPorts(srcPorts);

					if(srcPorts.Count > 1)
						errors.Add($"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

					srcPort = srcPorts[0];
					srcNode = srcPort.GetNode();
				}
				// else if(srcNode is ISubgraphNode subgraphNode)
				// {
				// 	errors.Add("subgraph expr sources not implemented");
				// 	// TODO: descend into subgraph
				// 	return default;
				// }
				else if(srcNode is IExprNode exprNode)
				{
					var result = GetNodeId(exprNode);
					int i = 0;
					bool found = false;

					foreach(var outputPort in srcNode.GetOutputPorts())
					{
						if(outputPort == srcPort)
						{
							found = true;
							break;
						}
						else
						{
							i++;
						}
					}

					if(!found)
						errors.Add($"couldn't find src port index");

					result = new BTExprNodeRef(result.index, (byte)i, result.constant);
					return result;
				}
				else
				{
					errors.Add($"unhandled expr source node type {srcNode.GetType().Name}");
					return default;
				}
			}

			BTExprNodeRef HandleDisconnectedPort(IPort dstPort)
			{
				if(dstPort.TryGetValue(out var value))
				{
					// TODO: deduplicate constants
					ushort offset = BehaviorTreeAuthoringExt.WriteConstant(value, out var length, constStorage);
					return BTExprNodeRef.Const(offset, length);
				}
				else
				{
					errors.Add($"port {dstPort} is not conneted to a source and couldn't get inlined value");
					return default;
				}
			}
		}
	}

	/// <summary>
	/// Every execution path through unique subgraph nodes produces a copy of
	/// the subgraph because they can have different input expressions. We key
	/// them via the subgraph stack. Expression resolution works in reverse.
	/// </summary>
	public class SubgraphStack : IEquatable<SubgraphStack>
	{
		private List<ISubgraphNode> path;
		private List<UnityEngine.Hash128> pathHashes;

		public SubgraphStack()
		{
			path = new();
			pathHashes = new();
		}

		public SubgraphStack(SubgraphStack src)
		{
			path = new(src.path);
			pathHashes = new(src.pathHashes);
		}

		public SubgraphStack Clone() => new SubgraphStack(this);

		public IEnumerable<UnityEngine.Hash128> Hashes => pathHashes;

		public int Depth => pathHashes.Count;
		public void Push(ISubgraphNode node) { path.Add(node); pathHashes.Add(node.Guid); }
		public void Pop() { path.RemoveAt(path.Count - 1); pathHashes.RemoveAt(pathHashes.Count - 1); }
		public UnityEngine.Hash128 GetKey() => UnityEngine.Hash128.Compute(pathHashes);
		public override bool Equals(object obj) => Equals(obj as SubgraphStack);
		public override int GetHashCode() => GetKey().GetHashCode();
		public ISubgraphNode Current => path[path.Count - 1];

		public bool Equals(SubgraphStack other)
		{
			if(other is null)
				return false;

			if(GetHashCode() != other.GetHashCode())
				return false;

			return pathHashes.SequenceEqual(other.pathHashes);
		}

		public void Clear()
		{
			path.Clear();
			pathHashes.Clear();
		}

		public static bool operator ==(SubgraphStack left, SubgraphStack right)
		{
			if(left is null)
				return right is null;

			return left.Equals(right);
		}

		public static bool operator !=(SubgraphStack left, SubgraphStack right) => !(left == right);
	}

	/// <summary>
	/// <see cref="INode"/> instances can be "instantiated" via subgraphs. Each
	/// instance gets a unique key by a hash of the subgraph stack leading to
	/// the node and the node object itself.
	/// </summary>
	/// <param name="subgraphStackKey"></param>
	/// <param name="node"></param>
	public readonly record struct NodeKey<TNode>(UnityEngine.Hash128 subgraphStackKey, TNode node) where TNode : INode;

}
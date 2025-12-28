using Mpr.Expr.Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Behavior.Authoring
{
	public class BTBakingContext : ExprBakingContext
	{
		public Dictionary<NodeKey<IExecNode>, BTExecNodeId> execNodeMap;

		public BTBakingContext(Graph rootGraph)
			: base(rootGraph)
		{
			execNodeMap = new();
		}

		protected override bool RegisterNodes()
		{
			var roots = rootGraph.GetNodes().OfType<Root>().ToList();
			if(roots.Count == 0)
			{
				errors.Add($"no Root node found");
				return false;
			}

			if(roots.Count > 1)
			{
				errors.Add($"graph must have exactly one Root node, {roots.Count} found");
				return false;
			}

			RegisterExecNode(null); // 0: NOP node for default exec stubs
			RegisterExecNode(roots[0]); // 1: Root
			RegisterExecNodes(rootGraph);

			return base.RegisterNodes();
		}

		public NodeKey<IExecNode> GetNodeKey(IExecNode execNode) => new(subgraphStack.GetKey(), execNode);
		public void RegisterExecNode(IExecNode execNode)
		{
			var index = execNodeMap.Count;
			if(index > ushort.MaxValue)
				throw new Exception("max exec node capacity exceeded");
			if(!execNodeMap.TryAdd(GetNodeKey(execNode), new BTExecNodeId((ushort)index)))
				throw new Exception("duplicate node key");
		}

		public BTExecNodeId GetNodeId(IExecNode execNode)
		{
			return execNodeMap[GetNodeKey(execNode)];
		}

		protected override void Bake()
		{
			ref var data = ref builder.ConstructRoot<BTData>();
			BakeExprData(ref data.exprData);
			var execs = builder.Allocate(ref data.execs, execNodeMap.Count);
			var execNodeIds = builder.Allocate(ref data.execNodeIds, execNodeMap.Count);
			var execNodeSubgraphStacks = builder.Allocate(ref data.execNodeSubgraphStacks, execNodeMap.Count);

			BakeExecNodes(rootGraph, ref builder, ref execs, ref execNodeIds, ref execNodeSubgraphStacks);

			BakeConstData(ref data.exprData);
		}

		void BakeExecNodes(Graph graph,
			ref BlobBuilder builder,
			ref BlobBuilderArray<BTExec> execs,
			ref BlobBuilderArray<UnityEngine.Hash128> execNodeIds,
			ref BlobBuilderArray<BlobArray<UnityEngine.Hash128>> execNodeSubgraphStack
			)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					BakeExecNodes(subgraphNode.GetSubgraph(), ref builder, ref execs, ref execNodeIds, ref execNodeSubgraphStack);
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
			}
		}

		void RegisterExecNodes(Graph graph)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					RegisterExecNodes(subgraphNode.GetSubgraph());
					PopSubgraph();
				}
				else if(node is IExecNode execNode)
				{
					if(node is not Root) // Root is registered separately
						RegisterExecNode(execNode);
				}

				if(node is IComponentAccess componentAccess)
				{
					var managedType = componentAccess.ComponentType.GetManagedType();
					componentTypeSet.TryGetValue(managedType, out var access);
					access |= componentAccess.ComponentType.AccessModeType;
					componentTypeSet[managedType] = access;
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

	}

}
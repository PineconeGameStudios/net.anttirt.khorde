using Mpr.Expr;
using Mpr.Expr.Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Hash128 = UnityEngine.Hash128;

namespace Mpr.Behavior.Authoring
{
	public unsafe class BTBakingContext : GraphExpressionBakingContext
	{
		public Dictionary<NodeKey<IExecNode>, BTExecNodeId> execNodeMap;
		private BTData* data;
		private NativeArray<BTExec> builderExecs;
		private NativeArray<Hash128> builderExecNodeIds;
		private NativeArray<BlobArray<Hash128>> builderExecNodeSubgraphStacks;

		public BTBakingContext(Graph rootGraph, Allocator allocator)
			: base(rootGraph, allocator)
		{
			execNodeMap = new();
		}

		public override void Dispose()
		{
			base.Dispose();

			builderExecs = default;
			builderExecNodeIds = default;
			builderExecNodeSubgraphStacks = default;
		}

		protected override ref BlobExpressionData ConstructRoot()
		{
			ref var data = ref builder.ConstructRoot<BTData>();
			fixed(BTData* dataPtr = &data)
				this.data = dataPtr;
			return ref data.exprData;
		}

		protected override bool RegisterGraphNodes()
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

			return true;
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

		public override void InitializeBake(int expressionCount, int outputCount)
		{
			base.InitializeBake(expressionCount, outputCount);

			builderExecs = AsArray(builder.Allocate(ref data->execs, execNodeMap.Count));
			builderExecNodeIds = AsArray(builder.Allocate(ref data->execNodeIds, execNodeMap.Count));
			builderExecNodeSubgraphStacks = AsArray(builder.Allocate(ref data->execNodeSubgraphStacks, execNodeMap.Count));
		}

		protected override bool BakeGraphNodes()
		{
			BakeExecNodes(rootGraph);
			return true;
		}

		void BakeExecNodes(Graph graph)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					BakeExecNodes(subgraphNode.GetSubgraph());
					PopSubgraph();
				}
				else if(node is IExecNode execNode)
				{
					var index = GetNodeId(execNode).index;
					builderExecNodeIds[index] = execNode.Guid;
					var subgraphStackIds = builder.Allocate(ref builderExecNodeSubgraphStacks.UnsafeElementAt(index), subgraphStack.Depth);
					int i = 0;
					foreach(var hash in subgraphStack.Hashes)
						subgraphStackIds[i++] = hash;

					execNode.Bake(ref builder, ref builderExecs.UnsafeElementAt(index), this);
				}
			}
		}

		static readonly UnityEngine.Hash128 globalKey = new UnityEngine.Hash128(0xddddddddddddddddul, 0xddddddddddddddddul);
		record struct VariableKey(UnityEngine.Hash128 subgraphStackKey, string name);
		Dictionary<VariableKey, int> variables = new();

		VariableKey GetVariableKey(IVariable variable)
		{
			bool isGlobal = IsGlobal(variable);
			return new VariableKey(isGlobal ? globalKey : subgraphStack.GetKey(), variable.name);
		}

		public static bool IsGlobal(IVariable variable)
		{
			return !variable.name.StartsWith("_");
		}

		public int GetVariableIndex(IVariable variable)
		{
			return variables[GetVariableKey(variable)];
		}

		void RegisterExecNodes(Graph graph)
		{
			foreach(var variable in graph.GetVariables())
			{
				if(variable.variableKind == VariableKind.Local)
				{
					var key = GetVariableKey(variable);
					if(!variables.ContainsKey(key))
					{
						variables[key] = AddBlackboardVariable(
							variable.name,
							IsGlobal(variable),
							variable.dataType
						);
					}
				}
			}

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
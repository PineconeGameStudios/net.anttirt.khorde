using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Hash128 = UnityEngine.Hash128;

namespace Khorde.Behavior.Authoring
{
	public unsafe class BTBakingContext : GraphExpressionBakingContext
	{
		public Dictionary<NodeKey<IExecNode>, BTExecNodeId> execNodeMap;
		private int execNodeIdCounter;
		private BTData* data;
		private NativeArray<BTExec> builderExecs;
		private NativeArray<Hash128> builderExecNodeIds;
		private NativeArray<Hash128> utilityPreviewPortIds;
		private NativeArray<BTData.ExecPorts> builderExecNodeExecPorts;
		private NativeArray<BlobArray<Hash128>> builderExecNodeSubgraphStacks;
		private List<QueryGraphAsset> queries = new();
		private List<BehaviorTreeAction> actions = new();

		public BTBakingContext(Graph rootGraph, Allocator allocator)
			: base(rootGraph, allocator)
		{
			execNodeMap = new();
		}

		public List<QueryGraphAsset> Queries => queries;

		public List<BehaviorTreeAction> Actions => actions;

		public override void Dispose()
		{
			base.Dispose();

			builderExecs = default;
			builderExecNodeIds = default;
			utilityPreviewPortIds = default;
			builderExecNodeExecPorts = default;
			builderExecNodeSubgraphStacks = default;
			execNodeIdCounter = 0;
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
			bool isSubgraph = rootGraph.GetVariables().Any(v => v.VariableKind == VariableKind.Input && v.DataType == typeof(ExecutionFlow));

			if(roots.Count == 0)
			{
				if(!isSubgraph)
					AddError(rootGraph, $"no Root node found");

				return false;
			}

			if(isSubgraph)
			{
				AddError(rootGraph, "Cannot have both a Root node and Exec input variables on the same graph");
			}

			if(roots.Count > 1)
			{
				foreach(var root in roots)
					AddError(root, $"graph must have exactly one Root node, {roots.Count} found");
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
			var index = execNodeIdCounter;
			if(index > ushort.MaxValue)
				throw new Exception("max exec node capacity exceeded");
			var nodeId = new BTExecNodeId((ushort)index);
			if(!execNodeMap.TryAdd(GetNodeKey(execNode), nodeId))
				throw new Exception("duplicate node key");
			execNodeIdCounter += execNode?.NodeCount ?? 1;

			execNode?.Register(this, nodeId);
		}

		public BTExecNodeId GetNodeId(IExecNode execNode)
		{
			return execNodeMap[GetNodeKey(execNode)];
		}

		public override void InitializeBake(int expressionCount, int outputCount)
		{
			base.InitializeBake(expressionCount, outputCount);

			var execCount = execNodeMap.Sum(nk => nk.Key.node?.NodeCount ?? 1);

			builderExecs = AsArray(builder.Allocate(ref data->execs, execCount));
			builderExecNodeIds = AsArray(builder.Allocate(ref data->execNodeIds, execCount));
			utilityPreviewPortIds = AsArray(builder.Allocate(ref data->utilityPreviewPortIds, execCount));
			builderExecNodeExecPorts = AsArray(builder.Allocate(ref data->execNodeExecPorts, execCount));
			builderExecNodeSubgraphStacks = AsArray(builder.Allocate(ref data->execNodeSubgraphStacks, execCount));
			data->graphId = rootGraph?.ID ?? default;
		}

		protected override bool BakeGraphNodes()
		{
			BakeExecNodes(rootGraph);
			return true;
		}

		void BakeExecNodes(Graph graph)
		{
			using var _ = TraceScope(graph.Name);

			bool hasUtilitySelectors = false;

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
					var nodeId = GetNodeId(execNode);
					var baseIndex = nodeId.index;
					var execPorts = execNode.GetOutputExecPorts().ToArray();
					Hash128 utilityPort = default;
					if(node is IUtilityNode utilityNode)
						utilityPort = utilityNode.GetUtilityDebugPort()?.ID ?? default;

					for(int j = 0; j < execNode.NodeCount; j++)
					{
						var index = baseIndex + j;

						execNode.Bake(ref builder, ref builderExecs.UnsafeElementAt(index), this, j, nodeId);

						//UnityEngine.Debug.Log($"baked {execNode} into {builderExecs.UnsafeElementAt(index + j).DumpString()} at node {nodeId} (pass {j})");

						builderExecNodeIds[index] = execNode.ID;
						utilityPreviewPortIds[index] = utilityPort;

						var subgraphStackIds = builder.Allocate(ref builderExecNodeSubgraphStacks.UnsafeElementAt(index), subgraphStack.Depth);
						int i = 0;
						foreach(var hash in subgraphStack.Hashes)
							subgraphStackIds[i++] = hash;

						var portPairs = builder.Allocate(ref builderExecNodeExecPorts.ElementAt(index).pairs, execPorts.Length);
						for(int k = 0;  k < execPorts.Length; k++)
						{
							portPairs[k] = new()
							{
								output = execPorts[k].ID,
								input = execPorts[k].IsConnected ? execPorts[k].FirstConnectedPort.ID : default,
							};
						}

						nodeId.index++;
					}
				}

				if(!hasUtilitySelectors && node is UtilitySelector)
					hasUtilitySelectors = true;
			}

			if(queries.Count > 0)
				data->flags |= BTData.Flags.HasQueries;

			if(hasUtilitySelectors)
				data->flags |= BTData.Flags.HasUtilitySelectors;
		}

		public int GetQueryIndex(INodeOption queryOption)
		{
			queryOption.TryGetValue<QueryGraphAsset>(out var queryGraphAsset);
			if(queryGraphAsset == null)
				throw new InvalidOperationException("query graph not assigned");

			int index = queries.IndexOf(queryGraphAsset);
			if(index != -1)
				return index;

			queries.Add(queryGraphAsset);
			return queries.Count - 1;
		}

		void RegisterExecNodes(Graph graph)
		{
			using var _ = TraceScope(graph.Name);

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
				AddError(srcPort.GetNode(), $"node {srcPort.GetNode()} port {srcPort} is connected to multiple exec ports");

			var dstPort = dstPorts[0];
			var dstNode = dstPort.GetNode();

			while(true)
			{
				if(dstNode is ISubgraphNode subgraphNode)
				{
					subgraphStack.Push(subgraphNode);

					var dstVariable = subgraphNode.GetVariableForInputPort(dstPort);
					var subgraph = subgraphNode.GetSubgraph();
					var dstVariableNodes = subgraph.GetNodes().OfType<IVariableNode>().Where(vn => vn.Variable == dstVariable).ToList();

					if(dstVariableNodes.Count == 0)
					{
						AddWarning(subgraphNode, $"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(dstVariableNodes.Count > 1)
						foreach(var dstVariableNode in dstVariableNodes)
							AddError(dstVariableNode, $"subgraph {subgraph} exec variable {dstVariable} has multiple instances");

					srcNode = dstVariableNodes[0];

					if(srcNode.OutputPortCount == 0)
					{
						AddWarning(subgraphNode, $"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(srcNode.OutputPortCount > 1)
						AddError(srcNode, $"subgraph {subgraph} node {srcNode} has multiple exec output ports");

					dstPorts.Clear();
					srcNode.GetOutputPort(0).GetConnectedPorts(dstPorts);

					if(dstPorts.Count == 0)
					{
						AddWarning(subgraphNode, $"execution reaches subgraph {subgraph} variable {dstVariable} but it is not connected to anything within the subgraph");
						return default;
					}

					if(dstPorts.Count > 1)
						AddError(srcNode, $"subgraph {subgraph} node {srcNode} is connected to multiple exec ports");

					dstPort = dstPorts[0];
					dstNode = dstPort.GetNode();
				}

				else if(dstNode is IVariableNode varNode)
				{
					var currentSubgraph = subgraphStack.Current;

					subgraphStack.Pop();

					srcPort = currentSubgraph.GetOutputPortForVariable(varNode.Variable);
					srcNode = (ISubgraphNode)srcPort.GetNode();

					dstPorts.Clear();
					srcPort.GetConnectedPorts(dstPorts);

					if(dstPorts.Count == 0)
						return default;

					if(dstPorts.Count > 1)
						AddError(srcNode, $"subgraph node {srcNode} output {srcPort} is connected to multiple exec ports");

					dstPort = dstPorts[0];
					dstNode = dstPort.GetNode();
				}

				else if(dstNode is IExecNode execNode)
				{
					return GetNodeId(execNode);
				}

				else
				{
					AddError(dstNode, $"unhandled exec node type {dstNode.GetType().Name}");
					return default;
				}
			}
		}
	}

}
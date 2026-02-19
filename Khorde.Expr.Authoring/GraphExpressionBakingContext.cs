using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Khorde.Expr.Authoring
{
	public class GraphExpressionBakingContext : ExpressionBakingContext
	{
		protected Graph rootGraph;
		protected SubgraphStack subgraphStack = new();
		private Dictionary<NodeKey<IExprNode>, ushort> exprNodeMap = new();
		private Dictionary<NodeKey<IVariable>, ushort> varNodeMap = new();
		private Dictionary<NodeKey<IVariableNode>, ushort> outputNodeMap = new();
		private ushort exprNodeCounter;
		protected static readonly UnityEngine.Hash128 globalKey = new UnityEngine.Hash128(0xddddddddddddddddul, 0xddddddddddddddddul);

		protected readonly struct VariableKey : IEquatable<VariableKey>
		{
			public readonly UnityEngine.Hash128 subgraphStackKey;
			public readonly string name;

			public VariableKey(Hash128 subgraphStackKey, string name)
			{
				this.subgraphStackKey = subgraphStackKey;
				this.name = name;
			}

			public override bool Equals(object obj)
			{
				return obj is VariableKey key && Equals(key);
			}

			public bool Equals(VariableKey other)
			{
				return subgraphStackKey.Equals(other.subgraphStackKey) &&
					EqualityComparer<string>.Default.Equals(name, other.name);
			}

			public override int GetHashCode()
			{
				int hash = 23;
				hash = hash * 17 + subgraphStackKey.GetHashCode();
				if(name != null)
					hash = hash * 17 + name.GetHashCode();
				return hash;
			}

			public static bool operator ==(VariableKey left, VariableKey right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(VariableKey left, VariableKey right)
			{
				return !(left == right);
			}
		}

		protected Dictionary<VariableKey, int> variables = new();
		protected List<(object context, string message)> errors = new();
		protected List<(object context, string message)> warnings = new();

		public void AddError(object context, string message) => errors.Add((context, message));
		public void AddWarning(object context, string message) => warnings.Add((context, message));

		public List<(object context, string message)> Errors => errors;
		public List<(object context, string message)> Warnings => warnings;

		public GraphExpressionBakingContext(Graph rootGraph, Allocator allocator)
			: base(allocator)
		{
			if(rootGraph == null)
				throw new ArgumentNullException(nameof(rootGraph));

			this.rootGraph = rootGraph;
		}

		List<IPort> portsTemp = new();

		protected VariableKey GetVariableKey(IVariable variable)
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

		public int GetVariableIndex(IPort resultVarPort)
		{
			portsTemp.Clear();
			resultVarPort.GetConnectedPorts(portsTemp);
			if(portsTemp.Count != 1)
			{
				AddError(resultVarPort.GetNode(), $"port {resultVarPort.name} must be connected to a single variable");
				return -1;
			}

			var varNode = portsTemp[0].GetNode() as IVariableNode;
			if(varNode == null)
			{
				AddError(resultVarPort.GetNode(), $"port {resultVarPort.name} must be connected to a blackboard variable");
				return -1;
			}

			return GetVariableIndex(varNode.variable);
		}

		public ExpressionRef GetExpressionRef(IPort dstPort)
		{
			if(!dstPort.isConnected)
				return HandleDisconnectedPort(dstPort);

			using var _ = SaveSubgraph();

			var srcPorts = new List<IPort>();
			dstPort.GetConnectedPorts(srcPorts);

			if(srcPorts.Count > 1)
				AddError(dstPort.GetNode(), $"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

			var srcPort = srcPorts[0];
			var srcNode = srcPort.GetNode();

			while(true)
			{
				if(srcNode is IVariableNode varNode)
				{
					if(varNode.variable.variableKind == VariableKind.Local)
					{
						return ExpressionRef.Node(varNodeMap[GetNodeKey(varNode.variable)], 0);
					}
					else
					{
						if(subgraphStack.Depth == 0)
						{
							AddError(varNode, $"node {varNode} but the subgraph stack is empty; the root graph appears to be a subgraph");
							return default;
						}

						dstPort = subgraphStack.Current.GetInputPortForVariable(varNode.variable);
						if(!dstPort.isConnected)
							return HandleDisconnectedPort(dstPort);

						subgraphStack.Pop();

						srcPorts.Clear();
						dstPort.GetConnectedPorts(srcPorts);

						if(srcPorts.Count > 1)
							AddError(dstPort.GetNode(), $"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

						srcPort = srcPorts[0];
						srcNode = srcPort.GetNode();
					}
				}
				// else if(srcNode is ISubgraphNode subgraphNode)
				// {
				// 	errors.Add("subgraph expr sources not implemented");
				// 	// TODO: descend into subgraph
				// 	return default;
				// }
				else if(srcNode is IExprNode exprNode)
				{
					ushort outputIndex = 0;
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
							outputIndex++;
						}
					}

					if(!found)
						AddError(exprNode, $"couldn't find src port index");

					return ExpressionRef.Node(exprNodeMap[GetNodeKey(exprNode)], outputIndex);
				}
				else
				{
					AddError(srcNode, $"unhandled expr source node type {srcNode.GetType().Name}");
					return default;
				}
			}

			ExpressionRef HandleDisconnectedPort(IPort dstPort)
			{
				if(dstPort.TryGetValue(out var value))
				{
					return Const(value);
				}
				else
				{
					AddError(dstPort.GetNode(), $"port {dstPort} is not connected to a source and couldn't get inlined value");
					return default;
				}
			}
		}

		public BlobBuilder Build()
		{
			RegisterExprNodes(this.rootGraph);
			if(!RegisterGraphNodes())
				return default;
			InitializeBake(exprNodeCounter, outputNodeMap.Count);
			BakeExprNodes(this.rootGraph);
			if(!BakeGraphNodes())
				return default;
			FinalizeBake();
			return builder;
		}

		void RegisterExprNodes(Graph graph)
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
					RegisterExprNodes(subgraphNode.GetSubgraph());
					PopSubgraph();
				}
				else if(node is IExprNode exprNode)
				{
					RegisterExprNode(exprNode);
				}
				else if(node is IVariableNode varNode)
				{
					if(varNode.variable.variableKind == VariableKind.Output)
					{
						RegisterOutput(varNode);
					}
					else if(varNode.variable.variableKind == VariableKind.Local)
					{
						RegisterVariableRead(varNode.variable);
					}
				}

				if(node is IComponentAccess componentAccess)
				{
					RegisterComponentAccess(
						componentAccess.ComponentType.GetManagedType(),
						ExpressionComponentLocation.Local,
						componentAccess.ComponentType.AccessModeType);
				}

				if(node is IComponentLookup componentLookup)
				{
					RegisterComponentAccess(
						componentLookup.ComponentType.GetManagedType(),
						ExpressionComponentLocation.Lookup,
						componentLookup.ComponentType.AccessModeType);
				}
			}
		}

		private void RegisterVariableRead(IVariable variable)
		{
			var index = exprNodeCounter;
			if(index > ushort.MaxValue)
				throw new InvalidOperationException("max expr node capacity exceeded");
			if(!varNodeMap.TryAdd(GetNodeKey(variable), (ushort)index))
				return;
			exprNodeCounter++;
		}

		protected virtual bool RegisterGraphNodes()
		{
			return true;
		}

		private void RegisterExprNode(IExprNode exprNode)
		{
			var index = exprNodeCounter;
			if(index > ushort.MaxValue)
				throw new InvalidOperationException("max expr node capacity exceeded");
			if(!exprNodeMap.TryAdd(GetNodeKey(exprNode), (ushort)index))
				throw new InvalidOperationException("duplicate node key");
			exprNodeCounter++;
		}

		private void RegisterOutput(IVariableNode outputNode)
		{
			var index = outputNodeMap.Count;
			if(!outputNodeMap.TryAdd(GetNodeKey(outputNode), (ushort)index))
				throw new InvalidOperationException("duplicate node key");
		}

		void BakeExprNodes(Graph graph)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					BakeExprNodes(subgraphNode.GetSubgraph());
					PopSubgraph();
				}
				else if(node is IExprNode exprNode)
				{
					var nodeIndex = exprNodeMap[GetNodeKey(exprNode)];
					builderSourceGraphNodeIds[nodeIndex] = exprNode.Guid;
					exprNode.Bake(this, GetStorage(nodeIndex));
				}
				else if(node is IVariableNode varNode)
				{
					if(varNode.variable.variableKind == VariableKind.Output)
					{
						var outputIndex = outputNodeMap[GetNodeKey(varNode)];
						var input = varNode.GetInputPort(0);
						builderOutputs[outputIndex] = new ExpressionOutput
						{
							expression = GetExpressionRef(input),
							valueType = input.dataType.GetExpressionValueType(),
							valueSize = (ushort)UnsafeUtility.SizeOf(input.dataType),
						};
					}
					else if(varNode.variable.variableKind == VariableKind.Local)
					{
						var nodeIndex = varNodeMap[GetNodeKey(varNode.variable)];
						// NOTE: this means variable nodes for the same variable are folded into one in the baked data
						builderSourceGraphNodeIds[nodeIndex] = varNode.Guid;
						CreateExpression(GetStorage(nodeIndex), new Variable
						{
							index = GetVariableIndex(varNode.variable),
						});
					}
				}
			}
		}

		protected virtual bool BakeGraphNodes()
		{
			return true;
		}

		public NodeKey<IVariable> GetNodeKey(IVariable variable) => new(subgraphStack.GetKey(), variable);
		public NodeKey<IExprNode> GetNodeKey(IExprNode exprNode) => new(subgraphStack.GetKey(), exprNode);
		public NodeKey<IVariableNode> GetNodeKey(IVariableNode varNode) => new(subgraphStack.GetKey(), varNode);

		public struct SubgraphStackStackSave : IDisposable
		{
			GraphExpressionBakingContext context;
			SubgraphStack saved;

			public SubgraphStackStackSave(GraphExpressionBakingContext context)
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
	}
}
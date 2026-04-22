using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Debug = UnityEngine.Debug;

namespace Khorde.Expr.Authoring
{
	public class GraphExpressionBakingContext : ExpressionBakingContext
	{
		protected Graph rootGraph;
		protected SubgraphStack subgraphStack = new();
		private Dictionary<NodeKey<IExprNode>, ushort> exprNodeMap = new();
		private Dictionary<NodeKey<IVariable>, ushort> varNodeMap = new();
		private Dictionary<NodeKey<(ICustomExprNode, int outputIndex)>, ushort> generatedVarNodeMap = new();
		private Dictionary<NodeKey<IVariableNode>, ushort> outputNodeMap = new();
		private Dictionary<NodeKey<IVariableNode>, ushort> inputNodeMap = new();
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

		protected Dictionary<VariableKey, VariableId> variables = new();
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

		public override void InitializeBake(int expressionCount, int outputCount)
		{
			base.InitializeBake(expressionCount, outputCount);

			ref var data = ref GetData();
			builder.AllocateString(ref data.assetName, rootGraph.name);
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

		public VariableId GetVariableIndex(IVariable variable)
		{
			return variables[GetVariableKey(variable)];
		}

		public VariableId GetVariableIndex(IPort resultVarPort)
		{
			portsTemp.Clear();
			resultVarPort.GetConnectedPorts(portsTemp);
			if(portsTemp.Count != 1)
			{
				AddError(resultVarPort.GetNode(), $"port {resultVarPort.name} must be connected to a single variable");
				return VariableId.Invalid;
			}

			var varNode = portsTemp[0].GetNode() as IVariableNode;
			if(varNode == null)
			{
				AddError(resultVarPort.GetNode(), $"port {resultVarPort.name} must be connected to a blackboard variable");
				return VariableId.Invalid;
			}

			return GetVariableIndex(varNode.variable);
		}

		public ExpressionRef GetExpressionRef(IPort dstPort)
		{
			using var _l = TraceScope(dstPort.name);

			if(!dstPort.isConnected)
				return HandleDisconnectedPort(dstPort);

			using var _ = SaveSubgraph();

			var srcPorts = new List<IPort>();
			dstPort.GetConnectedPorts(srcPorts);

			if(srcPorts.Count == 0)
				return HandleDisconnectedPort(dstPort);

			if(srcPorts.Count > 1)
				AddError(dstPort.GetNode(), $"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

			var srcPort = srcPorts[0];
			var srcNode = srcPort.GetNode();

			while(true)
			{
				Trace($"srcNode={NodeName(srcNode)} port={srcPort.name}");

				if(srcNode is IVariableNode varNode)
				{
					if(varNode.variable.variableKind == VariableKind.Local)
					{
						return ExpressionRef.Node(varNodeMap[GetNodeKey(varNode.variable)], 0);
					}
					else if(varNode.variable.variableKind == VariableKind.Input)
					{
						// exit subgraph
						if(subgraphStack.Depth == 0)
						{
							// TODO: Define a call interface and bake input
							// variables for standalone expression graphs.
							AddError(varNode, $"top level input variables not supported");
							return default;
						}

						dstPort = subgraphStack.Current.GetInputPortForVariable(varNode.variable);

						if(dstPort == null)
						{
							AddError(varNode, $"node {varNode} returns null for subgraph node {subgraphStack.Current} input port; try resaving the graph '{subgraphStack.Current.GetSubgraph()?.name}'");
							return default;
						}

						if(!dstPort.isConnected)
							return HandleDisconnectedPort(dstPort);

						PopSubgraph();

						srcPorts.Clear();
						dstPort.GetConnectedPorts(srcPorts);

						if(srcPorts.Count > 1)
							AddError(dstPort.GetNode(), $"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

						srcPort = srcPorts[0];
						srcNode = srcPort.GetNode();
					}
					else if(varNode.variable.variableKind == VariableKind.Output)
					{
						// output variable node within a subgraph; just follow normally
						dstPort = varNode.GetInputPort(0);

						srcPorts.Clear();
						dstPort.GetConnectedPorts(srcPorts);

						if(srcPorts.Count > 1)
							AddError(dstPort.GetNode(), $"node {dstPort.GetNode()} port {dstPort} is connected to multiple sources");

						srcPort = srcPorts[0];
						srcNode = srcPort.GetNode();
					}
					else
					{
						AddError(srcNode, $"unsupported variable kind {varNode.variable.variableKind}");
					}
				}
				else if(srcNode is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);

					var subgraphVariable = subgraphNode.GetVariableForOutputPort(srcPort);
					var nodes = subgraphNode.GetSubgraph().GetNodes().OfType<IVariableNode>().Where(v => v.variable == subgraphVariable).ToArray();
					if(nodes.Length != 1)
					{
						if(nodes.Length > 1)
							AddError(subgraphNode, "output variable within subgraph has multiple connections");
						else
							AddError(subgraphNode, "output variable within subgraph has no connections");

						return default;
					}

					srcNode = nodes[0];
					srcPort = srcNode.GetInputPort(0);
				}
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
				else if(srcNode is IConstantNode constNode)
				{
					if(!constNode.TryGetValue(out var value))
					{
						AddError(constNode, $"couldn't retrieve constant value from constant node");
						return default;
					}

					return Const(value);
				}
				else if(srcNode is ICustomExprNode customNode)
				{
					return customNode.GetExpressionRef(this, srcPort);
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

			var varNodesByIndex = varNodeMap.ToDictionary(kv => kv.Value, kv => kv.Key);
			var exprNodesByIndex = exprNodeMap.ToDictionary(kv => kv.Value, kv => kv.Key);
			var genVarNodesByIndex = generatedVarNodeMap.ToDictionary(kv => kv.Value, kv => kv.Key);

			for(ushort nodeIndex = 0; nodeIndex < exprNodeCounter; ++nodeIndex)
			{
				if(builderTypeHashes[nodeIndex] == 0)
				{
					if(varNodesByIndex.TryGetValue(nodeIndex, out var variableNode))
					{
						AddError(variableNode.node, $"variable {variableNode.node.name} at index {nodeIndex} failed to add an expression type hash");
					}
					else if(exprNodesByIndex.TryGetValue(nodeIndex, out var exprNode))
					{
						AddError(variableNode.node, $"expression {exprNode.node.GetType().FullName} at index {nodeIndex} failed to add an expression type hash");
					}
					else if(genVarNodesByIndex.TryGetValue(nodeIndex, out var genVar))
					{
						AddError(variableNode.node, $"genvar {genVar.node.Item1.GetType().FullName}/{genVar.node.outputIndex} at index {nodeIndex} failed to add an expression type hash");
					}
					else
					{
						AddError(this, $"node index {nodeIndex} could not be identified");
					}
				}
			}

			return builder;
		}

		void RegisterExprNodes(Graph graph)
		{
			using var _ = TraceScope(graph.name);

			foreach(var variable in graph.GetVariables())
			{
				if(variable.variableKind == VariableKind.Local)
				{
					var key = GetVariableKey(variable);
					if(!variables.ContainsKey(key))
					{
						variable.TryGetDefaultValue(out object defaultValue);
						variables[key] = AddBlackboardVariable(
							GetVariableName(variable),
							IsGlobal(variable),
							variable.dataType,
							defaultValue
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
						RegisterVariableRead(varNode);
					}
					else if(varNode.variable.variableKind == VariableKind.Input)
					{
						RegisterInput(varNode);
					}
					else
					{
						AddError(this, $"unsupported variable kind {varNode.variable.variableKind}");
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

		protected virtual string GetVariableName(IVariable variable)
		{
			return variable.name;
		}

		private void RegisterVariableRead(IVariableNode varNode)
		{
			var nodeIndex = exprNodeCounter;
			if(nodeIndex > ushort.MaxValue)
				throw new InvalidOperationException("max expr node capacity exceeded");
			if(!varNodeMap.TryAdd(GetNodeKey(varNode.variable), nodeIndex))
				return;
			exprNodeCounter++;
		}

		/// <summary>
		/// Register a generated variable (i.e. not from the current graph's explicit blackboard) and return the blackboard variable index
		/// </summary>
		/// <param name="node"></param>
		/// <param name="outputIndex"></param>
		/// <param name="name"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public VariableId RegisterGeneratedVariable(ICustomExprNode node, int outputIndex, string name, bool isGlobal, Type type)
		{
			var nodeIndex = exprNodeCounter;
			if(nodeIndex > ushort.MaxValue)
				throw new InvalidOperationException("max expr node capacity exceeded");

			if(!generatedVarNodeMap.TryAdd(GetNodeKey(node, outputIndex), nodeIndex))
				throw new InvalidOperationException("generated variable already registered");

			exprNodeCounter++;

			return AddBlackboardVariable(name, isGlobal: isGlobal, type: type, defaultValue: null);
		}

		protected virtual bool RegisterGraphNodes()
		{
			return true;
		}

		private void RegisterExprNode(IExprNode exprNode)
		{
			var nodeIndex = exprNodeCounter;
			if(nodeIndex > ushort.MaxValue)
				throw new InvalidOperationException("max expr node capacity exceeded");
			if(!exprNodeMap.TryAdd(GetNodeKey(exprNode), nodeIndex))
				throw new InvalidOperationException("duplicate node key");
			exprNodeCounter++;
		}

		private void RegisterOutput(IVariableNode outputNode)
		{
			var index = outputNodeMap.Count;
			if(!outputNodeMap.TryAdd(GetNodeKey(outputNode), (ushort)index))
				throw new InvalidOperationException("duplicate node key");
		}

		private void RegisterInput(IVariableNode outputNode)
		{
			var index = inputNodeMap.Count;
			if(!inputNodeMap.TryAdd(GetNodeKey(outputNode), (ushort)index))
				throw new InvalidOperationException("duplicate node key");
		}

		void BakeExprNodes(Graph graph)
		{
			using var _ = TraceScope(graph.name);

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
						// NOTE: Baking these is only relevant for standalone
						// expressions, not for subgraphs.

						if(graph == rootGraph)
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
					else if(varNode.variable.variableKind == VariableKind.Input)
					{
						// NOTE: Baking these is only relevant for standalone
						// expressions, not for subgraphs.

						if(graph == rootGraph)
						{
							// TODO: Define a call interface and bake input
							// variables for standalone expression graphs.
							AddError(varNode, "top-level input variables not supported");
						}
					}
					else
					{
						AddError(varNode, $"unsupported var kind {varNode.variable.variableKind}");
					}
				}
			}
		}

		public void BakeGeneratedVariable(ICustomExprNode node, int outputIndex, VariableId variableIndex)
		{
			var nodeIndex = generatedVarNodeMap[GetNodeKey(node, outputIndex)];
			builderSourceGraphNodeIds[nodeIndex] = node.Guid;
			CreateExpression(GetStorage(nodeIndex), new Variable
			{
				index = variableIndex,
			});
		}

		public ExpressionRef GetGeneratedVariableNodeRef(ICustomExprNode node, int outputIndex)
		{
			return ExpressionRef.Node(generatedVarNodeMap[GetNodeKey(node, outputIndex)], 0);
		}

		protected virtual bool BakeGraphNodes()
		{
			return true;
		}

		public NodeKey<IVariable> GetNodeKey(IVariable variable) => new(subgraphStack.GetKey(), variable);
		public NodeKey<(ICustomExprNode, int outputIndex)> GetNodeKey(ICustomExprNode node, int outputIndex) => new(subgraphStack.GetKey(), (node, outputIndex));
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
				context.Trace($"restore subgr; stack = [{string.Join(", ", context.subgraphStack.Hashes.Select(h => h.ToString().Substring(0, 8)))}]");
			}
		}

		public SubgraphStackStackSave SaveSubgraph() { return new SubgraphStackStackSave(this); }

		public void PushSubgraph(ISubgraphNode subgraphNode)
		{
			subgraphStack.Push(subgraphNode);
			Trace($"push subgraph {subgraphNode.Guid}; stack = [{string.Join(", ", subgraphStack.Hashes.Select(h => h.ToString().Substring(0, 8)))}]");
		}
		public void PopSubgraph()
		{
			var cur = subgraphStack.Current;
			subgraphStack.Pop();
			Trace($"pop subgraph  {cur.Guid}; stack = [{string.Join(", ", subgraphStack.Hashes.Select(h => h.ToString().Substring(0, 8)))}]");
		}

		protected bool traceBaking = false;
		protected int logDepth;
		protected string logPad => new string(' ', 4 * logDepth);

		protected struct TracingScope : IDisposable
		{
			public GraphExpressionBakingContext parent;
			public string name;
			public string msg;

			public TracingScope(GraphExpressionBakingContext parent, string name, string msg)
			{
				this.parent = parent;
				this.name = name;
				this.msg = msg;

				if(parent.traceBaking)
					Debug.Log($"{parent.logPad}{name}({msg}) {{");

				++parent.logDepth;
			}

			public void Dispose()
			{
				--parent.logDepth;

				if(parent.traceBaking)
					Debug.Log($"{parent.logPad}}}");
			}
		}

		protected TracingScope TraceScope(string msg, [CallerMemberName] string name = "") => new TracingScope(this, name, msg);
		protected void Trace(string msg)
		{
			if(traceBaking)
				Debug.Log(logPad + msg);
		}

		static string NodeName(INode node) => node switch
		{
			IVariableNode varNode => varNode.variable.name,
			ISubgraphNode subgraphNode => subgraphNode.GetSubgraph().name,
			IConstantNode constNode => $"({constNode.dataType.Name}) {(constNode.TryGetValue(out var value) ? value.ToString() : "")}",
			_ => $"{node.GetType().Name}({node.Guid.ToString().Substring(0, 8)})",
		};
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr
{
	public class ExprBakingContext : IDisposable
	{
		public Graph rootGraph;
		public Dictionary<NodeKey<IExprNode>, ExprNodeRef> exprNodeMap;
		public NativeList<byte> constStorage;
		public HashSet<Type> componentTypeSet;
		public List<Type> componentTypes;
		public List<string> errors;
		public List<string> warnings;
		public SubgraphStack subgraphStack;
		protected BlobBuilder builder;

		public ExprBakingContext(Graph rootGraph)
		{
			this.rootGraph = rootGraph ?? throw new ArgumentNullException(nameof(rootGraph));

			constStorage = new NativeList<byte>(Allocator.Persistent);
			componentTypeSet = new();
			componentTypes = new();
			exprNodeMap = new();
			errors = new();
			warnings = new();
			subgraphStack = new();
		}

		public void Dispose()
		{
			constStorage.Dispose();
		}

		public BlobBuilder Bake(AllocatorManager.AllocatorHandle allocator)
		{
			if(!RegisterNodes())
				return default;

			componentTypes = componentTypeSet.OrderBy(t => t.FullName).ToList();

			builder = new BlobBuilder(allocator);

			Bake();

			//ref var data = ref builder.ConstructRoot<BTData>();

			// ref var exprData = ref BakeImpl();
			// var exprs = builder.Allocate(ref exprData.exprs, exprNodeMap.Count);
			// var types = builder.Allocate(ref exprData.componentTypes, componentTypes.Count);
			// var exprNodeIds = builder.Allocate(ref exprData.exprNodeIds, exprNodeMap.Count);

			// var execs = builder.Allocate(ref data.execs, execNodeMap.Count);
			// var execNodeIds = builder.Allocate(ref data.execNodeIds, execNodeMap.Count);
			// var execNodeSubgraphStacks = builder.Allocate(ref data.execNodeSubgraphStacks, execNodeMap.Count);

			// for(int i = 0; i < componentTypes.Count; ++i)
			// 	types[i] = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(componentTypes[i])).StableTypeHash;

			// BakeNodes(rootGraph, ref builder, ref execs, ref exprs, ref execNodeIds, ref exprNodeIds, ref execNodeSubgraphStacks);

			return builder;
		}

		protected void BakeExprData(ref ExprData exprData)
		{
			var exprs = builder.Allocate(ref exprData.exprs, exprNodeMap.Count);
			var types = builder.Allocate(ref exprData.componentTypes, componentTypes.Count);
			var exprNodeIds = builder.Allocate(ref exprData.exprNodeIds, exprNodeMap.Count);

			for(int i = 0; i < componentTypes.Count; ++i)
				types[i] = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(componentTypes[i])).StableTypeHash;

			BakeExprNodes(rootGraph, ref builder, ref exprs, ref exprNodeIds);
		}

		protected void BakeConstData(ref ExprData exprData)
		{
			ExprAuthoring.BakeConstStorage(ref builder, ref exprData, constStorage);
		}

		protected virtual void Bake()
		{
			ref var exprData = ref builder.ConstructRoot<ExprData>();
			BakeExprData(ref exprData);
			BakeConstData(ref exprData);
		}

		protected virtual bool RegisterNodes()
		{
			RegisterExprNodes(rootGraph);
			return true;
		}

		void BakeExprNodes(Graph graph,
			ref BlobBuilder builder,
			ref BlobBuilderArray<BTExpr> exprs,
			ref BlobBuilderArray<UnityEngine.Hash128> exprNodeIds
			)
		{
			foreach(var node in graph.GetNodes())
			{
				if(node is ISubgraphNode subgraphNode)
				{
					PushSubgraph(subgraphNode);
					BakeExprNodes(subgraphNode.GetSubgraph(), ref builder, ref exprs, ref exprNodeIds);
					PopSubgraph();
				}
				else if(node is IExprNode exprNode)
				{
					var index = GetNodeId(exprNode).index;
					exprNodeIds[index] = exprNode.Guid;
					exprNode.Bake(ref builder, ref exprs[index], this);
				}
			}
		}

		public struct SubgraphStackStackSave : IDisposable
		{
			ExprBakingContext context;
			SubgraphStack saved;

			public SubgraphStackStackSave(ExprBakingContext context)
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

		public NodeKey<IExprNode> GetNodeKey(IExprNode exprNode) => new(subgraphStack.GetKey(), exprNode);

		public void RegisterExprNode(IExprNode exprNode)
		{
			var index = exprNodeMap.Count;
			if(index > ushort.MaxValue)
				throw new Exception("max expr node capacity exceeded");
			if(!exprNodeMap.TryAdd(GetNodeKey(exprNode), new ExprNodeRef((ushort)index, 0, false)))
				throw new Exception("duplicate node key");
		}

		public ExprNodeRef GetNodeId(IExprNode exprNode)
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

		void RegisterExprNodes(Graph graph)
		{
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

				if(node is IComponentAccess componentAccess)
				{
					componentTypeSet.Add(componentAccess.ComponentType);
				}
			}
		}

		public ExprNodeRef GetExprNodeRef(IPort dstPort)
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

					result = new ExprNodeRef(result.index, (byte)i, result.constant);
					return result;
				}
				else
				{
					errors.Add($"unhandled expr source node type {srcNode.GetType().Name}");
					return default;
				}
			}

			ExprNodeRef HandleDisconnectedPort(IPort dstPort)
			{
				if(dstPort.TryGetValue(out var value))
				{
					// TODO: deduplicate constants
					ushort offset = ExprAuthoring.WriteConstant(value, out var length, constStorage);
					return ExprNodeRef.Const(offset, length);
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
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEditorInternal;

namespace Mpr.Expr.Authoring;

public class GraphExpressionBakingContext : ExpressionBakingContext
{
    protected Graph rootGraph;
    protected SubgraphStack subgraphStack = new();
    private Dictionary<NodeKey<IExprNode>, ushort> exprNodeMap = new();
    private Dictionary<NodeKey<IVariableNode>, ushort> outputNodeMap = new();
    protected List<string> errors = new();
    protected List<string> warnings = new();
    
    public List<string> Warnings => warnings;
    public List<string> Errors => errors;
    
    public GraphExpressionBakingContext(Graph rootGraph, Allocator allocator)
        : base(allocator)
    {
        this.rootGraph = rootGraph;
    }

    public ExpressionRef GetExpressionRef(IPort dstPort)
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
			    if (subgraphStack.Depth == 0)
			    {
				    errors.Add($"node {varNode} but the subgraph stack is empty; the root graph appears to be a subgraph");
				    return default;
			    }
			    
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
				    errors.Add($"couldn't find src port index");

			    return ExpressionRef.Node(exprNodeMap[GetNodeKey(exprNode)], outputIndex);
		    }
		    else
		    {
			    errors.Add($"unhandled expr source node type {srcNode.GetType().Name}");
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
			    errors.Add($"port {dstPort} is not conneted to a source and couldn't get inlined value");
			    return default;
		    }
	    }
    }

    public BlobBuilder Build()
    {
	    RegisterExprNodes(this.rootGraph);
	    if (!RegisterGraphNodes())
		    return default;
        InitializeBake(exprNodeMap.Count, outputNodeMap.Count);
        BakeExprNodes(this.rootGraph);
        if (!BakeGraphNodes())
	        return default;
        FinalizeBake();
        return builder;
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
            else if (node is IVariableNode varNode && varNode.variable.variableKind == VariableKind.Output)
            {
	            RegisterOutput(varNode);
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

    protected virtual bool RegisterGraphNodes()
    {
	    return true;
    }

    private void RegisterExprNode(IExprNode exprNode)
    {
        var index = exprNodeMap.Count;
        if(index > ushort.MaxValue)
            throw new InvalidOperationException("max expr node capacity exceeded");
        if(!exprNodeMap.TryAdd(GetNodeKey(exprNode), (ushort)index))
            throw new InvalidOperationException("duplicate node key");
    }

    private void RegisterOutput(IVariableNode outputNode)
    {
	    var index = outputNodeMap.Count;
	    if (!outputNodeMap.TryAdd(GetNodeKey(outputNode), (ushort)index))
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
		    else if (node is IVariableNode varNode && varNode.variable.variableKind == VariableKind.Output)
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
    }

    protected virtual bool BakeGraphNodes()
    {
	    return true;
    }

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
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring;

public class GraphExpressionBakingContext : ExpressionBakingContext
{
    private Graph rootGraph;
    
    public GraphExpressionBakingContext(Graph rootGraph, DynamicBuffer<BlobExpressionObjectReference> strongReferences, DynamicBuffer<BlobExpressionWeakObjectReference> weakReferences, Allocator allocator)
        : base(strongReferences, weakReferences, allocator)
    {
        this.rootGraph = rootGraph;
    }

    public ExpressionRef GetExpressionRef(IPort inputPort)
    {
        throw new NotImplementedException();
    }

    private int GetExpressionCount()
    {
        throw new NotImplementedException();
    }

    public void InitializeBake()
    {
        base.InitializeBake(GetExpressionCount());
    }
}
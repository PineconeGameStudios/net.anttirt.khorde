using System;
using Unity.GraphToolkit.Editor;

namespace Mpr.Expr.Authoring;

abstract internal class CompareBase<TArg> : ExprBase where TArg : unmanaged
{
    private INodeOption @operator;

    public override string Title
    {
        get
        {
            if (@operator != null && @operator.TryGetValue(out BinaryCompareOp op))
                return op.ToString();

            return "(compare)";
        }
    }

    public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
    {
        if (typeof(TArg) == typeof(float))
        {
            ref var data = ref context.CreateExpression<BinaryCompareFloat>(storage);
            @operator.TryGetValue(out data.@operator);
            data.Input0 = context.GetExpressionRef(GetInputPort(0));
            data.Input1 = context.GetExpressionRef(GetInputPort(1));
        }
        
        if (typeof(TArg) == typeof(int))
        {
            ref var data = ref context.CreateExpression<BinaryCompareInt>(storage);
            @operator.TryGetValue(out data.@operator);
            data.Input0 = context.GetExpressionRef(GetInputPort(0));
            data.Input1 = context.GetExpressionRef(GetInputPort(1));
        }
    }

    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        @operator = context.AddOption<BinaryCompareOp>("op")
            .WithDisplayName("")
            .Build();
    }

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddInputPort<TArg>("a").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
        context.AddInputPort<TArg>("b").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).WithPortCapacity(PortCapacity.Single).Build();
        context.AddOutputPort<bool>("out").WithDisplayName(string.Empty).WithConnectorUI(PortConnectorUI.Circle).Build();
    }
}

[Serializable] [NodeCategory("Compare")] internal class CompareFloatNode : CompareBase<float> { }
[Serializable] [NodeCategory("Compare")] internal class CompareIntNode : CompareBase<int> { }
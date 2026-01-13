using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Mpr.Expr.Authoring;

enum VectorType
{
	Int2,
	Int3,
	Int4,
	Float2,
	Float3,
	Float4
}

static class VectorTypeExt
{
	public static int Dimension(this VectorType type) => type switch
	{
		VectorType.Int2 => 2,
		VectorType.Int3 => 3,
		VectorType.Int4 => 4,
		VectorType.Float2 => 2,
		VectorType.Float3 => 3,
		VectorType.Float4 => 4,
		_ => throw new InvalidOperationException()
	};

	public static Type Type(this VectorType type) => type switch
	{
		VectorType.Int2 => typeof(int2),
		VectorType.Int3 => typeof(int3),
		VectorType.Int4 => typeof(int4),
		VectorType.Float2 => typeof(float2),
		VectorType.Float3 => typeof(float3),
		VectorType.Float4 => typeof(float4),
		_ => throw new InvalidOperationException()
	};

	public static Type ScalarType(this VectorType type) => type switch
	{
		VectorType.Int2 => typeof(int),
		VectorType.Int3 => typeof(int),
		VectorType.Int4 => typeof(int),
		VectorType.Float2 => typeof(float),
		VectorType.Float3 => typeof(float),
		VectorType.Float4 => typeof(float),
		_ => throw new InvalidOperationException()
	};
}

abstract class BreakBase : ExprBase
{
	protected abstract VectorType inputType { get; }
	private IPort inputPort;
	private List<IPort> outputPorts;

	public override string Title => "Break";

	public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
	{
		switch(inputType)
		{
			case VectorType.Int2: context.CreateExpression(storage, new BreakInt2 { Input0 = context.GetExpressionRef(inputPort) }); break;
			case VectorType.Int3: context.CreateExpression(storage, new BreakInt3 { Input0 = context.GetExpressionRef(inputPort) }); break;
			case VectorType.Int4: context.CreateExpression(storage, new BreakInt4 { Input0 = context.GetExpressionRef(inputPort) }); break;
			case VectorType.Float2: context.CreateExpression(storage, new BreakFloat2 { Input0 = context.GetExpressionRef(inputPort) }); break;
			case VectorType.Float3: context.CreateExpression(storage, new BreakFloat3 { Input0 = context.GetExpressionRef(inputPort) }); break;
			case VectorType.Float4: context.CreateExpression(storage, new BreakFloat4 { Input0 = context.GetExpressionRef(inputPort) }); break;
			default:
				break;
		}
	}

	protected override void OnDefinePorts(IPortDefinitionContext context)
	{
		inputPort = context.AddInputPort("Input")
			.WithDisplayName(string.Empty)
			.WithDataType(inputType.Type())
			.Build();

		var names = "XYZW";

		outputPorts = Enumerable.Range(0, inputType.Dimension()).Select(i =>
			context
				.AddOutputPort($"Output{i}")
				.WithDisplayName(new string(names[i], 1))
				.WithDataType(inputType.ScalarType())
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build()
				).ToList();
	}
}

[Serializable] class BreakInt2Node : BreakBase { protected override VectorType inputType => VectorType.Int2; }
[Serializable] class BreakInt3Node : BreakBase { protected override VectorType inputType => VectorType.Int3; }
[Serializable] class BreakInt4Node : BreakBase { protected override VectorType inputType => VectorType.Int4; }
[Serializable] class BreakFloat2Node : BreakBase { protected override VectorType inputType => VectorType.Float2; }
[Serializable] class BreakFloat3Node : BreakBase { protected override VectorType inputType => VectorType.Float3; }
[Serializable] class BreakFloat4Node : BreakBase { protected override VectorType inputType => VectorType.Float4; }

abstract class MakeBase : ExprBase
{
	protected abstract VectorType outputType { get; }
	private IPort outputPort;
	private List<IPort> inputs;

	public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
	{
		switch(outputType)
		{
			case VectorType.Int2: context.CreateExpression(storage, new MakeInt2 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]) }); break;
			case VectorType.Int3: context.CreateExpression(storage, new MakeInt3 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]), Input2 = context.GetExpressionRef(inputs[2]) }); break;
			case VectorType.Int4: context.CreateExpression(storage, new MakeInt4 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]), Input2 = context.GetExpressionRef(inputs[2]), Input3 = context.GetExpressionRef(inputs[3]) }); break;
			case VectorType.Float2: context.CreateExpression(storage, new MakeFloat2 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]) }); break;
			case VectorType.Float3: context.CreateExpression(storage, new MakeFloat3 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]), Input2 = context.GetExpressionRef(inputs[2]) }); break;
			case VectorType.Float4: context.CreateExpression(storage, new MakeFloat4 { Input0 = context.GetExpressionRef(inputs[0]), Input1 = context.GetExpressionRef(inputs[1]), Input2 = context.GetExpressionRef(inputs[2]), Input3 = context.GetExpressionRef(inputs[3]) }); break;
			default:
				break;
		}
	}

	protected override void OnDefinePorts(IPortDefinitionContext context)
	{
		outputPort = context.AddOutputPort("Output")
			.WithDisplayName(string.Empty)
			.WithDataType(outputType.Type())
			.Build();

		var names = "XYZW";

		inputs = Enumerable.Range(0, outputType.Dimension())
			.Select(i =>
			context.AddInputPort($"Input{i}")
			.WithDisplayName(new string(names[i], 1))
			.WithDataType(outputType.ScalarType())
			.WithConnectorUI(PortConnectorUI.Circle)
			.WithPortCapacity(PortCapacity.Multi)
			.Build()
			).ToList();
	}
}

[Serializable] class MakeInt2Node : MakeBase { protected override VectorType outputType => VectorType.Int2; }
[Serializable] class MakeInt3Node : MakeBase { protected override VectorType outputType => VectorType.Int3; }
[Serializable] class MakeInt4Node : MakeBase { protected override VectorType outputType => VectorType.Int4; }
[Serializable] class MakeFloat2Node : MakeBase { protected override VectorType outputType => VectorType.Float2; }
[Serializable] class MakeFloat3Node : MakeBase { protected override VectorType outputType => VectorType.Float3; }
[Serializable] class MakeFloat4Node : MakeBase { protected override VectorType outputType => VectorType.Float4; }

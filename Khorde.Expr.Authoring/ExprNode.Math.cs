using System;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;

namespace Khorde.Expr.Authoring
{
	[Serializable]
	internal abstract class OpBase<T, OpT> : ExprBase where T : unmanaged where OpT : unmanaged, IBTBinaryOp
	{
		public override void OnEnable() { Title = $"{default(OpT).Op}"; }

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			var left = context.GetExpressionRef(GetInputPort(0));
			var right = context.GetExpressionRef(GetInputPort(1));

			if(typeof(T) == typeof(float))
			{
				ref var data = ref context.CreateExpression<BinaryFloat>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(float2))
			{
				ref var data = ref context.CreateExpression<BinaryFloat2>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(float3))
			{
				ref var data = ref context.CreateExpression<BinaryFloat3>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(float4))
			{
				ref var data = ref context.CreateExpression<BinaryFloat4>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(int))
			{
				ref var data = ref context.CreateExpression<BinaryInt>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(int2))
			{
				ref var data = ref context.CreateExpression<BinaryInt2>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(int3))
			{
				ref var data = ref context.CreateExpression<BinaryInt3>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
			else if(typeof(T) == typeof(int4))
			{
				ref var data = ref context.CreateExpression<BinaryInt4>(storage);
				data.@operator = default(OpT).Op;
				data.Input0 = left;
				data.Input1 = right;
			}
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<T>("a")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<T>("b")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<T>("out")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.Build();
		}
	}

	[Serializable][NodeCategory("Math/Add")] internal class AddInt : OpBase<int, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt : OpBase<int, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt : OpBase<int, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt : OpBase<int, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinInt : OpBase<int, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxInt : OpBase<int, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModInt : OpBase<int, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerInt : OpBase<int, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddInt2 : OpBase<int2, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt2 : OpBase<int2, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt2 : OpBase<int2, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt2 : OpBase<int2, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinInt2 : OpBase<int2, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxInt2 : OpBase<int2, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModInt2 : OpBase<int2, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerInt2 : OpBase<int2, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddInt3 : OpBase<int3, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt3 : OpBase<int3, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt3 : OpBase<int3, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt3 : OpBase<int3, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinInt3 : OpBase<int3, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxInt3 : OpBase<int3, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModInt3 : OpBase<int3, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerInt3 : OpBase<int3, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddInt4 : OpBase<int4, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt4 : OpBase<int4, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt4 : OpBase<int4, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt4 : OpBase<int4, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinInt4 : OpBase<int4, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxInt4 : OpBase<int4, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModInt4 : OpBase<int4, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerInt4 : OpBase<int4, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddFloat : OpBase<float, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat : OpBase<float, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat : OpBase<float, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat : OpBase<float, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinFloat : OpBase<float, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxFloat : OpBase<float, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModFloat : OpBase<float, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerFloat : OpBase<float, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddFloat2 : OpBase<float2, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat2 : OpBase<float2, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat2 : OpBase<float2, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat2 : OpBase<float2, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinFloat2 : OpBase<float2, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxFloat2 : OpBase<float2, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModFloat2 : OpBase<float2, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerFloat2 : OpBase<float2, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddFloat3 : OpBase<float3, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat3 : OpBase<float3, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat3 : OpBase<float3, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat3 : OpBase<float3, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinFloat3 : OpBase<float3, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxFloat3 : OpBase<float3, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModFloat3 : OpBase<float3, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerFloat3 : OpBase<float3, BTBinaryOp_Power> { }

	[Serializable][NodeCategory("Math/Add")] internal class AddFloat4 : OpBase<float4, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat4 : OpBase<float4, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat4 : OpBase<float4, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat4 : OpBase<float4, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Min")] internal class MinFloat4 : OpBase<float4, BTBinaryOp_Min> { }
	[Serializable][NodeCategory("Math/Max")] internal class MaxFloat4 : OpBase<float4, BTBinaryOp_Max> { }
	[Serializable][NodeCategory("Math/Mod")] internal class ModFloat4 : OpBase<float4, BTBinaryOp_Mod> { }
	[Serializable][NodeCategory("Math/Power")] internal class PowerFloat4 : OpBase<float4, BTBinaryOp_Power> { }

	[Serializable]
	internal abstract class UnaryBase<TExpr, TIn, TOut> : ExprBase
		where TExpr : unmanaged, IExpression<TIn>
		where TIn : unmanaged
		where TOut : unmanaged
	{
		private IPort input;
		private IPort output;

		public override void OnEnable()
		{
			var name = typeof(TExpr).Name;
			Title = name.Substring(0, name.Length - 1);
		}

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			TExpr expr = default;
			expr.Input0 = context.GetExpressionRef(input);
			context.CreateExpression(storage, expr);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			input = context.AddInputPort<TIn>("input")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			output = context.AddOutputPort<TOut>("output")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat2Node : UnaryBase<LengthFloat2, float2, float> { }
	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat3Node : UnaryBase<LengthFloat3, float3, float> { }
	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat4Node : UnaryBase<LengthFloat4, float4, float> { }
	[Serializable][NodeCategory("Math/Normalize")] internal class Normalize2Node : UnaryBase<Normalize2, float2, float2> { }
	[Serializable][NodeCategory("Math/Normalize")] internal class Normalize3Node : UnaryBase<Normalize3, float3, float3> { }
	[Serializable][NodeCategory("Math/Normalize")] internal class Normalize4Node : UnaryBase<Normalize4, float4, float4> { }
	[Serializable][NodeCategory("Math/Floor")] internal class Floor1Node : UnaryBase<Floor1, float, int> { }
	[Serializable][NodeCategory("Math/Floor")] internal class Floor2Node : UnaryBase<Floor2, float2, int2> { }
	[Serializable][NodeCategory("Math/Floor")] internal class Floor3Node : UnaryBase<Floor3, float3, int3> { }
	[Serializable][NodeCategory("Math/Floor")] internal class Floor4Node : UnaryBase<Floor4, float4, int4> { }
	[Serializable][NodeCategory("Math/Ceiling")] internal class Ceiling1Node : UnaryBase<Ceiling1, float, int> { }
	[Serializable][NodeCategory("Math/Ceiling")] internal class Ceiling2Node : UnaryBase<Ceiling2, float2, int2> { }
	[Serializable][NodeCategory("Math/Ceiling")] internal class Ceiling3Node : UnaryBase<Ceiling3, float3, int3> { }
	[Serializable][NodeCategory("Math/Ceiling")] internal class Ceiling4Node : UnaryBase<Ceiling4, float4, int4> { }
	[Serializable][NodeCategory("Math/ToFloat")] internal class ToFloat1Node : UnaryBase<ToFloat1, int, float> { }
	[Serializable][NodeCategory("Math/ToFloat")] internal class ToFloat2Node : UnaryBase<ToFloat2, int2, float2> { }
	[Serializable][NodeCategory("Math/ToFloat")] internal class ToFloat3Node : UnaryBase<ToFloat3, int3, float3> { }
	[Serializable][NodeCategory("Math/ToFloat")] internal class ToFloat4Node : UnaryBase<ToFloat4, int4, float4> { }
	[Serializable][NodeCategory("Math/Rotation")] internal class AngleToDirectionNode : UnaryBase<AngleToDirection, float, float2> { }
	[Serializable][NodeCategory("Math/Transform")] internal class GetTranslationNode : UnaryBase<GetTranslation, float4x4, float3> { }
	[Serializable][NodeCategory("Math/Transform")] internal class GetRotationNode : UnaryBase<GetRotation, float4x4, quaternion> { }
	[Serializable][NodeCategory("Math/Transform")] internal class GetScaleNode : UnaryBase<GetScale, float4x4, float3> { }

	[Serializable]
	internal abstract class BinaryBase<TExpr, TIn0, TIn1, TOut> : ExprBase
		where TExpr : unmanaged, IExpression<TIn0, TIn1>
		where TIn0 : unmanaged
		where TIn1 : unmanaged
		where TOut : unmanaged
	{
		private IPort input0;
		private IPort input1;
		private IPort output;

		public override void OnEnable()
		{
			var name = typeof(TExpr).Name;
			Title = name.Substring(0, name.Length - 1);
		}

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			TExpr expr = default;
			expr.Input0 = context.GetExpressionRef(input0);
			expr.Input1 = context.GetExpressionRef(input1);
			context.CreateExpression(storage, expr);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			input0 = context.AddInputPort<TIn0>("input0")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			input1 = context.AddInputPort<TIn1>("input1")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Single)
				.Build();

			output = context.AddOutputPort<TOut>("output")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable][NodeCategory("Math/Rescale")] internal class Rescale2Node : BinaryBase<Rescale2, float2, float, float2> { }
	[Serializable][NodeCategory("Math/Rescale")] internal class Rescale3Node : BinaryBase<Rescale3, float3, float, float3> { }
	[Serializable][NodeCategory("Math/Rescale")] internal class Rescale4Node : BinaryBase<Rescale4, float4, float, float4> { }
	[Serializable][NodeCategory("Math/Rotation")] internal class Rotate2DNode : BinaryBase<Rotate2D, float2, float, float2> { }
	[Serializable][NodeCategory("Math/Rotation")] internal class Rotate3DNode : BinaryBase<Rotate3D, float3, quaternion, float3> { }
	[Serializable][NodeCategory("Math/Rotation")] internal class AxisAngleNode : BinaryBase<AxisAngle, float3, float, quaternion> { }
	[Serializable]
	[NodeCategory("Math/Transform")]
	internal class WithTranslationNode : BinaryBase<WithTranslation, float4x4, float3, float4x4>
	{
		public override void OnEnable()
		{
			Title = "With Translation";
		}
	}
	[Serializable]
	[NodeCategory("Math/Transform")]
	internal class WithRotationNode : BinaryBase<WithRotation, float4x4, quaternion, float4x4>
	{
		public override void OnEnable()
		{
			Title = "With Rotation";
		}
	}
	[Serializable]
	[NodeCategory("Math/Transform")]
	internal class WithScaleNode : BinaryBase<WithScale, float4x4, float3, float4x4>
	{
		public override void OnEnable()
		{
			Title = "With Scale";
		}
	}
}
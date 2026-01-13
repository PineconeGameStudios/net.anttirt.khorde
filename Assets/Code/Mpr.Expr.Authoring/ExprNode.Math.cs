using System;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Mpr.Expr.Authoring
{
	internal abstract class OpBase<T, OpT> : ExprBase where T : unmanaged where OpT : unmanaged, IBTBinaryOp
	{
		static StyleSheet s_styleSheet;

		public override StyleSheet CustomStyleSheet
		{
			get
			{
				if(s_styleSheet == null)
					s_styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"Assets/Settings/BTStyleSheets/Nodes/{default(OpT).Op}.uss");

				return s_styleSheet;
			}
		}

		public override string Title => $"{default(OpT).Op}";

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
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddInputPort<T>("b")
				.WithDisplayName(string.Empty)
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
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
	[Serializable][NodeCategory("Math/Add")] internal class AddInt2 : OpBase<int2, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt2 : OpBase<int2, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt2 : OpBase<int2, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt2 : OpBase<int2, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddInt3 : OpBase<int3, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt3 : OpBase<int3, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt3 : OpBase<int3, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt3 : OpBase<int3, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddInt4 : OpBase<int4, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubInt4 : OpBase<int4, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulInt4 : OpBase<int4, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivInt4 : OpBase<int4, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddFloat : OpBase<float, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat : OpBase<float, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat : OpBase<float, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat : OpBase<float, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddFloat2 : OpBase<float2, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat2 : OpBase<float2, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat2 : OpBase<float2, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat2 : OpBase<float2, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddFloat3 : OpBase<float3, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat3 : OpBase<float3, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat3 : OpBase<float3, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat3 : OpBase<float3, BTBinaryOp_Div> { }
	[Serializable][NodeCategory("Math/Add")] internal class AddFloat4 : OpBase<float4, BTBinaryOp_Add> { }
	[Serializable][NodeCategory("Math/Sub")] internal class SubFloat4 : OpBase<float4, BTBinaryOp_Sub> { }
	[Serializable][NodeCategory("Math/Mul")] internal class MulFloat4 : OpBase<float4, BTBinaryOp_Mul> { }
	[Serializable][NodeCategory("Math/Div")] internal class DivFloat4 : OpBase<float4, BTBinaryOp_Div> { }

	internal abstract class LengthBase<TExpr, TArg> : ExprBase where TExpr : unmanaged, IExpression<TArg> where TArg : unmanaged
	{
		public override string Title => "Length";

		public override void Bake(GraphExpressionBakingContext context, ExpressionStorageRef storage)
		{
			TExpr expr = default;
			expr.Input0 = context.GetExpressionRef(GetInputPort(0));
			context.CreateExpression(storage, expr);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<TArg>("input")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Single)
				.Build();

			context.AddOutputPort<float>("output")
				.WithDisplayName("")
				.WithConnectorUI(PortConnectorUI.Circle)
				.WithPortCapacity(PortCapacity.Multi)
				.Build();
		}
	}

	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat2Node : LengthBase<LengthFloat2, float2> { }
	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat3Node : LengthBase<LengthFloat3, float3> { }
	[Serializable][NodeCategory("Math/Length")] internal class LengthFloat4Node : LengthBase<LengthFloat4, float4> { }
}
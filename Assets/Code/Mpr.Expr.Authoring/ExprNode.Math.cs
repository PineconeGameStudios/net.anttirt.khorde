using System;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Mpr.Expr
{
	internal abstract class OpBase<T, OpT> : ExprNode where T : unmanaged where OpT : unmanaged, IBTBinaryOp
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

		public override string Title => $"{default(OpT).Op} ({typeof(T).Name})";

		public override void Bake(ref BlobBuilder builder, ref BTExpr expr, ExprBakingContext context)
		{
			expr.type = BTExpr.BTExprType.BinaryOp;

			BTMathType type;

			if(typeof(T) == typeof(int))
				type = BTMathType.Int;
			else if(typeof(T) == typeof(int2))
				type = BTMathType.Int2;
			else if(typeof(T) == typeof(int3))
				type = BTMathType.Int3;
			else if(typeof(T) == typeof(int4))
				type = BTMathType.Int4;
			else if(typeof(T) == typeof(float))
				type = BTMathType.Float;
			else if(typeof(T) == typeof(float2))
				type = BTMathType.Float2;
			else if(typeof(T) == typeof(float3))
				type = BTMathType.Float3;
			else if(typeof(T) == typeof(float4))
				type = BTMathType.Float4;
			else
				throw new NotImplementedException();

			expr.data.binaryOp = new BTExpr.BinaryOp
			{
				left = context.GetExprNodeRef(GetInputPort(0)),
				right = context.GetExprNodeRef(GetInputPort(1)),
				op = default(OpT).Op,
				type = type,
			};
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

	[Serializable] [NodeCategory("Math/Add")] internal class AddInt : OpBase<int, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubInt : OpBase<int, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulInt : OpBase<int, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivInt : OpBase<int, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddInt2 : OpBase<int2, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubInt2 : OpBase<int2, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulInt2 : OpBase<int2, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivInt2 : OpBase<int2, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddInt3 : OpBase<int3, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubInt3 : OpBase<int3, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulInt3 : OpBase<int3, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivInt3 : OpBase<int3, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddInt4 : OpBase<int4, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubInt4 : OpBase<int4, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulInt4 : OpBase<int4, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivInt4 : OpBase<int4, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddFloat : OpBase<float, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubFloat : OpBase<float, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulFloat : OpBase<float, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivFloat : OpBase<float, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddFloat2 : OpBase<float2, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubFloat2 : OpBase<float2, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulFloat2 : OpBase<float2, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivFloat2 : OpBase<float2, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddFloat3 : OpBase<float3, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubFloat3 : OpBase<float3, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulFloat3 : OpBase<float3, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivFloat3 : OpBase<float3, BTBinaryOp_Div> { }
	[Serializable] [NodeCategory("Math/Add")] internal class AddFloat4 : OpBase<float4, BTBinaryOp_Add> { }
	[Serializable] [NodeCategory("Math/Sub")] internal class SubFloat4 : OpBase<float4, BTBinaryOp_Sub> { }
	[Serializable] [NodeCategory("Math/Mul")] internal class MulFloat4 : OpBase<float4, BTBinaryOp_Mul> { }
	[Serializable] [NodeCategory("Math/Div")] internal class DivFloat4 : OpBase<float4, BTBinaryOp_Div> { }
}

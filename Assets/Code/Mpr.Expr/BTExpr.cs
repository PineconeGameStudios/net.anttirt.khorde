using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Mpr.Blobs;
using Mpr.Burst;

namespace Mpr.Expr
{
	public interface IBTExprEval
	{
		void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result);
	}

	public readonly record struct BTExprNodeRef(ushort index, byte outputIndex, bool constant)
	{
		public static BTExprNodeRef Node(ushort index, byte outputIndex) => new BTExprNodeRef(index, outputIndex, false);
		public static BTExprNodeRef Const(ushort offset, byte length) => new BTExprNodeRef(offset, length, true);

		public T Evaluate<T>(ref BTExprData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
		{
			if(constant)
			{
				var constData = data.constData.AsSpan();
				constData = constData.Slice(index, outputIndex);
				var castData = SpanMarshal.Cast<byte, T>(constData);
				return castData[0];
			}

			return data.GetNode(this).Evaluate<T>(ref data, outputIndex, componentPtrs);
		}

		public void Evaluate(ref BTExprData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
		{
			if(constant)
			{
				data.constData.AsSpan().Slice(index, outputIndex).CopyTo(result);
				return;
			}

			data.GetNode(this).Evaluate(ref data, outputIndex, componentPtrs, result);
		}

		public override string ToString()
		{
			return constant ? $"const(off={index}, sz={outputIndex}) " : $"ref(expr={index} out={outputIndex})";
		}
	}


	/// <summary>
	/// Data and evaluation for pure expression nodes.
	/// </summary>
	public struct BTExpr
	{
		public Data data;
		public ExprType type;

		public enum ExprType : byte
		{
			ReadField,
			Bool,
			Float3,
			BinaryOp,
		}

		public T Evaluate<T>(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
		{
			Span<T> result = stackalloc T[1];
			Evaluate(ref data, outputIndex, componentPtrs, SpanMarshal.AsBytes(result));
			return result[0];
		}

		public void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
		{
			switch(type)
			{
				case ExprType.ReadField: this.data.readField.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case ExprType.Bool: this.data.@bool.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case ExprType.Float3: this.data.@float3.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case ExprType.BinaryOp: this.data.binaryOp.Evaluate(ref data, outputIndex, componentPtrs, result); return;
			}
#if DEBUG
			throw new Exception();
#endif
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct Data
		{
			[FieldOffset(0)] public ReadField readField;
			[FieldOffset(0)] public Bool @bool;
			[FieldOffset(0)] public Float3 @float3;
			[FieldOffset(0)] public BinaryOp binaryOp;
		}

		public string DumpString()
		{
			string result = type.ToString() + ":";

			switch(type)
			{
				case ExprType.ReadField: result += data.readField.DumpString(); break;
				case ExprType.Bool: result += data.@bool.DumpString(); break;
				case ExprType.Float3: result += data.@float3.DumpString(); break;
			}

			return result;
		}

		public struct BinaryOp : IBTExprEval
		{
			public BTExprNodeRef left;
			public BTExprNodeRef right;
			public BTMathType type;
			public BTBinaryOp op;

			public void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				Span<byte> leftData = stackalloc byte[result.Length];
				Span<byte> rightData = stackalloc byte[result.Length];
				left.Evaluate(ref data, componentPtrs, leftData);
				right.Evaluate(ref data, componentPtrs, rightData);
				BTBinaryEval.Apply(type, op, leftData, rightData, result);
			}
		}

		public struct ReadField : IBTExprEval
		{
			public byte componentIndex;
			public BlobArray<Field> fields;

			public struct Field
			{
				public ushort offset;
				public ushort length;

				public static implicit operator Field(System.Reflection.FieldInfo fieldInfo)
				{
					return new Field
					{
						offset = (ushort)UnsafeUtility.GetFieldOffset(fieldInfo),
						length = (ushort)UnsafeUtility.SizeOf(fieldInfo.FieldType),
					};
				}

				public override string ToString()
				{
					return $"{{ offset={offset}, length={length} }}";
				}
			}

			public void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				ref var field = ref fields[outputIndex];
				var componentData = componentPtrs[componentIndex].AsSpan();
				var fieldData = componentData.Slice(field.offset, field.length);
				fieldData.CopyTo(result);
			}

			public string DumpString()
			{
				return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
			}
		}

		public struct Bool : IBTExprEval
		{
			public readonly record struct Not(BTExprNodeRef inner);
			public readonly record struct And(BTExprNodeRef left, BTExprNodeRef right);
			public readonly record struct Or(BTExprNodeRef left, BTExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Not not;
				[FieldOffset(0)] public And and;
				[FieldOffset(0)] public Or or;
			}

			public enum BoolType
			{
				Not,
				And,
				Or,
			}

			public Data data;
			public BoolType index;

			public bool Evaluate(ref BTExprData btData, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
			{
				switch(index)
				{
					case BoolType.Not: return !data.not.inner.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.And: return data.and.left.Evaluate<bool>(ref btData, componentPtrs) && data.and.right.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.Or: return data.or.left.Evaluate<bool>(ref btData, componentPtrs) || data.or.right.Evaluate<bool>(ref btData, componentPtrs);
				}
#if DEBUG
				Debug.Log($"invalid BTBoolExpr type index {index}");
				throw new Exception();
#else
			return false;
#endif
			}

			public void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				SpanMarshal.Cast<byte, bool>(result)[0] = Evaluate(ref data, componentPtrs);
			}

			public string DumpString()
			{
				switch(index)
				{
					case BoolType.Not: return data.not.ToString();
					case BoolType.And: return data.not.ToString();
					case BoolType.Or: return data.not.ToString();
				}
				return "";
			}
		}

		public struct Float3 : IBTExprEval
		{
			public readonly record struct Add(BTExprNodeRef left, BTExprNodeRef right);
			public readonly record struct Sub(BTExprNodeRef left, BTExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Add add;
				[FieldOffset(0)] public Sub sub;
			}

			public Data data;
			public Float3Type index;

			public enum Float3Type
			{
				Add,
				Sub,
			}

			public float3 Evaluate(ref BTExprData btData, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
			{
				switch(index)
				{
					case Float3Type.Add: return data.add.left.Evaluate<float3>(ref btData, componentPtrs) + data.add.right.Evaluate<float3>(ref btData, componentPtrs);
					case Float3Type.Sub: return data.sub.left.Evaluate<float3>(ref btData, componentPtrs) + data.sub.right.Evaluate<float3>(ref btData, componentPtrs);
				}
#if DEBUG
				Debug.Log($"invalid BTBoolExpr type index {index}");
				throw new Exception();
#else
			return false;
#endif
			}

			public void Evaluate(ref BTExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				SpanMarshal.Cast<byte, float3>(result)[0] = Evaluate(ref data, componentPtrs);
			}

			public string DumpString()
			{
				switch(index)
				{
					case Float3Type.Add: return data.add.ToString();
					case Float3Type.Sub: return data.sub.ToString();
				}
				return "";
			}
		}
	}
}

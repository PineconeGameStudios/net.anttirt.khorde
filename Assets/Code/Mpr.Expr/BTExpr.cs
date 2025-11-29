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
	public interface IExprEval
	{
		void Evaluate(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result);
	}

	public readonly record struct ExprNodeRef(ushort index, byte outputIndex, bool constant)
	{
		public static ExprNodeRef Node(ushort index, byte outputIndex) => new ExprNodeRef(index, outputIndex, false);
		public static ExprNodeRef Const(ushort offset, byte length) => new ExprNodeRef(offset, length, true);

		public T Evaluate<T>(ref ExprData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
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

		public void Evaluate(ref ExprData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
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
		public BTExprType type;

		public enum BTExprType : byte
		{
			ReadField,
			Bool,
			BinaryMath,
		}

		public T Evaluate<T>(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
		{
			Span<T> result = stackalloc T[1];
			Evaluate(ref data, outputIndex, componentPtrs, SpanMarshal.AsBytes(result));
			return result[0];
		}

		public void Evaluate(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
		{
			switch(type)
			{
				case BTExprType.ReadField: this.data.readField.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case BTExprType.Bool: this.data.@bool.Evaluate(ref data, outputIndex, componentPtrs, result); return;
				case BTExprType.BinaryMath: this.data.binaryMath.Evaluate(ref data, outputIndex, componentPtrs, result); return;
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
			[FieldOffset(0)] public BinaryMath binaryMath;
		}

		public string DumpString()
		{
			string result = type.ToString() + ":";

			switch(type)
			{
				case BTExprType.ReadField: result += data.readField.DumpString(); break;
				case BTExprType.Bool: result += data.@bool.DumpString(); break;
				case BTExprType.BinaryMath: result += data.binaryMath.DumpString(); break;
			}

			return result;
		}

		public struct BinaryMath : IExprEval
		{
			public ExprNodeRef left;
			public ExprNodeRef right;
			public MathType type;
			public BinaryMathOp op;

			public void Evaluate(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				Span<byte> leftData = stackalloc byte[result.Length];
				Span<byte> rightData = stackalloc byte[result.Length];
				left.Evaluate(ref data, componentPtrs, leftData);
				right.Evaluate(ref data, componentPtrs, rightData);
				BTBinaryEval.Apply(type, op, leftData, rightData, result);
			}

			public string DumpString()
			{
				return $"({left} {op} {right}):{type}";
			}
		}

		public struct ReadField : IExprEval
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

			public void Evaluate(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
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

		public struct Bool : IExprEval
		{
			public readonly record struct Not(ExprNodeRef inner);
			public readonly record struct And(ExprNodeRef left, ExprNodeRef right);
			public readonly record struct Or(ExprNodeRef left, ExprNodeRef right);
			public readonly record struct Xor(ExprNodeRef left, ExprNodeRef right);

			[StructLayout(LayoutKind.Explicit)]
			public struct Data
			{
				[FieldOffset(0)] public Not not;
				[FieldOffset(0)] public And and;
				[FieldOffset(0)] public Or or;
				[FieldOffset(0)] public Xor xor;
			}

			public enum BoolType
			{
				Not,
				And,
				Or,
				Xor,
			}

			public Data data;
			public BoolType index;

			public bool Evaluate(ref ExprData btData, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
			{
				switch(index)
				{
					case BoolType.Not: return !data.not.inner.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.And: return data.and.left.Evaluate<bool>(ref btData, componentPtrs) && data.and.right.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.Or: return data.or.left.Evaluate<bool>(ref btData, componentPtrs) || data.or.right.Evaluate<bool>(ref btData, componentPtrs);
					case BoolType.Xor: return data.xor.left.Evaluate<bool>(ref btData, componentPtrs) != data.xor.right.Evaluate<bool>(ref btData, componentPtrs);
				}
				Debug.Log($"invalid BTBoolExpr type index {index}");
#if DEBUG
				throw new Exception();
#else
				return false;
#endif
			}

			public void Evaluate(ref ExprData data, byte outputIndex, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
			{
				SpanMarshal.Cast<byte, bool>(result)[0] = Evaluate(ref data, componentPtrs);
			}

			public string DumpString()
			{
				switch(index)
				{
					case BoolType.Not: return data.not.ToString();
					case BoolType.And: return data.and.ToString();
					case BoolType.Or: return data.or.ToString();
				}
				return "";
			}
		}
	}
}

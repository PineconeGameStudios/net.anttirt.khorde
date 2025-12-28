using Mpr.Blobs;
using Mpr.Burst;
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Expr
{
	public unsafe ref struct ExprEvalContext
	{
		public ExprEvalContext(
			ref ExprData data,
			ReadOnlySpan<UnsafeComponentReference> componentPtrs,
			ReadOnlySpan<UntypedComponentLookup> componentLookups
			)
		{
			fixed(ExprData* pData = &data)
				this.dataPtr = pData;
			this.componentPtrs = componentPtrs;
			this.componentLookups = componentLookups;
		}

		public ExprData* dataPtr;
		public ReadOnlySpan<UnsafeComponentReference> componentPtrs;
		public ReadOnlySpan<UntypedComponentLookup> componentLookups;

		public ref ExprData data
		{
			get
			{
				return ref *dataPtr;
			}
		}
	}

	public interface IExprEval
	{
		void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result);
	}

	public readonly record struct ExprNodeRef(ushort index, byte outputIndex, bool constant)
	{
		public static ExprNodeRef Node(ushort index, byte outputIndex) => new ExprNodeRef(index, outputIndex, false);
		public static ExprNodeRef Const(ushort offset, byte length) => new ExprNodeRef(offset, length, true);

		public T Evaluate<T>(in ExprEvalContext ctx) where T : unmanaged
		{
			if(constant)
			{
				var constData = ctx.data.constData.AsSpan();
				constData = constData.Slice(index, outputIndex);
				var castData = SpanMarshal.Cast<byte, T>(constData);
				return castData[0];
			}

			return ctx.data.GetNode(this).Evaluate<T>(in ctx, outputIndex);
		}

		public void Evaluate(in ExprEvalContext ctx, Span<byte> result)
		{
			if(constant)
			{
				ctx.data.constData.AsSpan().Slice(index, outputIndex).CopyTo(result);
				return;
			}

			ctx.data.GetNode(this).Evaluate(in ctx, outputIndex, result);
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
			LookupField,
		}

		public T Evaluate<T>(in ExprEvalContext ctx, byte outputIndex) where T : unmanaged
		{
			Span<T> result = stackalloc T[1];
			var resultBytes = SpanMarshal.AsBytes(result);
			Evaluate(in ctx, outputIndex, resultBytes);
			return result[0];
		}

		public void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result)
		{
			switch(type)
			{
				case BTExprType.ReadField: this.data.readField.Evaluate(in ctx, outputIndex, result); return;
				case BTExprType.Bool: this.data.@bool.Evaluate(in ctx, outputIndex, result); return;
				case BTExprType.BinaryMath: this.data.binaryMath.Evaluate(in ctx, outputIndex, result); return;
				case BTExprType.LookupField: this.data.lookupField.Evaluate(in ctx, outputIndex, result); return;
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
			[FieldOffset(0)] public LookupField lookupField;
		}

		public string DumpString()
		{
			string result = type.ToString() + ":";

			switch(type)
			{
				case BTExprType.ReadField: result += data.readField.DumpString(); break;
				case BTExprType.Bool: result += data.@bool.DumpString(); break;
				case BTExprType.BinaryMath: result += data.binaryMath.DumpString(); break;
				case BTExprType.LookupField: result += data.lookupField.DumpString(); break;
			}

			return result;
		}

		public struct BinaryMath : IExprEval
		{
			public ExprNodeRef left;
			public ExprNodeRef right;
			public MathType type;
			public BinaryMathOp op;

			public void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result)
			{
				Span<byte> leftData = stackalloc byte[result.Length];
				Span<byte> rightData = stackalloc byte[result.Length];
				left.Evaluate(in ctx, leftData);
				right.Evaluate(in ctx, rightData);
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

			public void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result)
			{
				ref var field = ref fields[outputIndex];
				var componentData = ctx.componentPtrs[componentIndex].AsSpan();
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

			public bool Evaluate(in ExprEvalContext ctx)
			{
				switch(index)
				{
					case BoolType.Not: return !data.not.inner.Evaluate<bool>(in ctx);
					case BoolType.And: return data.and.left.Evaluate<bool>(in ctx) && data.and.right.Evaluate<bool>(in ctx);
					case BoolType.Or: return data.or.left.Evaluate<bool>(in ctx) || data.or.right.Evaluate<bool>(in ctx);
					case BoolType.Xor: return data.xor.left.Evaluate<bool>(in ctx) != data.xor.right.Evaluate<bool>(in ctx);
				}
				Debug.Log($"invalid BTBoolExpr type index {index}");
#if DEBUG
				throw new Exception();
#else
				return false;
#endif
			}

			public void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result)
			{
				SpanMarshal.Cast<byte, bool>(result)[0] = Evaluate(in ctx);
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

		public struct LookupField : IExprEval
		{
			public ExprNodeRef entity;
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

			public void Evaluate(in ExprEvalContext ctx, byte outputIndex, Span<byte> result)
			{
				var entity = this.entity.Evaluate<Entity>(in ctx);

				if (componentIndex >= ctx.componentLookups.Length)
				{
					Debug.LogError($"componentIndex {componentIndex} is out of range (length:{ctx.componentLookups.Length})");
					throw new Exception("invalid componentIndex");
				}

				if (!ctx.componentLookups[componentIndex].IsCreated)
				{
					Debug.LogError($"componentLookup at index {componentIndex} was not created");
					throw new Exception("componentLookup for index was not created");
				}
				
				if(ctx.componentLookups[componentIndex].TryGetRefRO(entity, out var componentDataArray))
				{
					if(outputIndex == 0)
					{
						SpanMarshal.Cast<byte, bool>(result)[0] = true;
					}
					else
					{
						if(!componentDataArray.IsCreated)
							throw new Exception("attempting to access a field on a zero-sized component");

						/// NOTE: index 0 is always the "has component" boolean, so field outputs are offset by 1
						/// see <see cref="Mpr.Expr.Authoring.ComponentLookupNode{T}.OnDefinePorts"/>
						ref var field = ref fields[outputIndex - 1];
						var componentData = componentDataArray.AsReadOnlySpan();
						var fieldData = componentData.Slice(field.offset, field.length);
						fieldData.CopyTo(result);
					}
				}
				else
				{
					result.Clear();
				}
			}

			public string DumpString()
			{
				return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
			}
		}
	}
}

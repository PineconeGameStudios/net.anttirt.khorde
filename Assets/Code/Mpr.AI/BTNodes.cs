using System;
using Unity.Entities;

namespace Mpr.AI.BT
{
	public readonly record struct BTExecNodeId(ushort index);
	public readonly record struct BTExprNodeRef(ushort index, byte outputIndex, bool constant)
	{
		public static BTExprNodeRef Node(ushort index, byte outputIndex) => new BTExprNodeRef(index, outputIndex, false);
		public static BTExprNodeRef Const(ushort offset, byte length) => new BTExprNodeRef(offset, length, true);

		public T Evaluate<T>(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs) where T : unmanaged
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

		public void Evaluate(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs, Span<byte> result)
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

	public struct ConditionalBlock
	{
		public BTExprNodeRef condition;
		public BTExecNodeId nodeId;

		public override string ToString()
		{
			return $"{{condition={condition}, nodeId={nodeId}}}";
		}
	}

	// TODO
	// public struct UtilityBlock
	// {
	// 	public UtilityData utility;
	// 	public BTExecNodeId nodeId;
	// }

	public struct Root
	{
		public BTExecNodeId child;

		public string DumpString()
		{
			return $"{{ child={child} }}";
		}
	}

	public struct Sequence
	{
		public BlobArray<BTExecNodeId> children;

		public string DumpString()
		{
			return $"{{ children=[{string.Join(", ", children.ToArray())}] }}";
		}
	}

	public struct Selector
	{
		public BlobArray<ConditionalBlock> children;

		public string DumpString()
		{
			return $"{{ children=[{string.Join(", ", children.ToArray())}] }}";
		}
	}

	public struct WriteField
	{
		public byte componentIndex;

		public struct Field
		{
			public BTExprNodeRef input;
			public ushort offset;
			public ushort size;

			public override string ToString()
			{
				return $"{{ input={input}, offset={offset}, size={size} }}";
			}
		}

		public BlobArray<Field> fields;

		public void Evaluate(ref BTData data, ReadOnlySpan<UnsafeComponentReference> componentPtrs)
		{
			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = componentPtrs[componentIndex].AsSpan().Slice(field.offset, field.size);
				field.input.Evaluate(ref data, componentPtrs, fieldSpan);
			}
		}

		public string DumpString()
		{
			return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
		}
	}

	public struct Wait
	{
		public BTExprNodeRef until;

		public string DumpString()
		{
			return $"{{ until={until} }}";
		}
	}

	public struct Fail
	{
		public string DumpString()
		{
			return "{}";
		}
	}

	public struct Optional
	{
		public BTExprNodeRef condition;
		public BTExecNodeId child;

		public string DumpString()
		{
			return $"{{ condition={condition}, child={child} }}";
		}
	}

	public struct Catch
	{
		public BTExecNodeId child;

		public string DumpString()
		{
			return $"{{ child={child} }}";
		}
	}
}
using System;
using Unity.Entities;
using Mpr.Blobs;
using Mpr.Expr;

namespace Mpr.AI.BT
{
	public readonly record struct BTExecNodeId(ushort index);
	public struct ConditionalBlock
	{
		public ExprNodeRef condition;
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
			public ExprNodeRef input;
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
				field.input.Evaluate(ref data.exprData, componentPtrs, fieldSpan);
			}
		}

		public string DumpString()
		{
			return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
		}
	}

	public struct Wait
	{
		public ExprNodeRef until;

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
		public ExprNodeRef condition;
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
using Khorde.Expr;
using System;
using Unity.Entities;

namespace Khorde.Behavior
{
	public struct BTExecNodeId : IEquatable<BTExecNodeId>
	{
		public ushort index;

		public BTExecNodeId(ushort index)
		{
			this.index = index;
		}

		public bool Equals(BTExecNodeId other)
		{
			return index == other.index;
		}

		public override bool Equals(object obj)
		{
			return obj is BTExecNodeId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return index.GetHashCode();
		}

		public static bool operator ==(BTExecNodeId left, BTExecNodeId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(BTExecNodeId left, BTExecNodeId right)
		{
			return !left.Equals(right);
		}
	}

	public struct ConditionalBlock
	{
		public ExpressionRef condition;
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
			public ExpressionRef input;
			public ushort offset;
			public ushort size;

			public override string ToString()
			{
				return $"{{ input={input}, offset={offset}, size={size} }}";
			}
		}

		public BlobArray<Field> fields;

		public void Evaluate(in ExpressionEvalContext ctx)
		{
			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = ctx.componentPtrs[componentIndex].AsNativeArray(field.offset, field.size);
				field.input.Evaluate(in ctx, ref fieldSpan);
			}
		}

		public string DumpString()
		{
			return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
		}
	}

	public struct Wait
	{
		public ExpressionRef until;
		public ExpressionRef duration;

		public string DumpString()
		{
			return $"{{ until={until} for={duration} }}";
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
		public ExpressionRef condition;
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

	public struct WriteVar
	{
		public ExpressionRef input;
		public int variableIndex;

		public string DumpString()
		{
			return $"{{ input={input} varIndex={variableIndex} }}";
		}
	}

	public struct Query
	{
		/// <summary>
		/// Query index to use, the same index as what gets baked into <see cref="BehaviorTreeAsset.Queries"/>
		/// </summary>
		public int queryIndex;

		/// <summary>
		/// Blackboard variable storing the result items (currently scalar, same as query result item type)
		/// </summary>
		public int variableIndex;

		/// <summary>
		/// Int blackboard variable storing the result count (currently 0 or 1)
		/// </summary>
		public int resultCountVariableIndex;

		public string DumpString()
		{
			return $"{{ query={queryIndex} var={variableIndex} nRes={resultCountVariableIndex} }}";
		}
	}
}
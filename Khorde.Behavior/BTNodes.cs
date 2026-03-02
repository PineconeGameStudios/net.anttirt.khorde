using Khorde.Expr;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Khorde.Behavior
{
	/// <summary>
	/// Type-safe wrapper for a Behavior Tree execution node index
	/// </summary>
	public struct BTExecNodeId : IEquatable<BTExecNodeId>
	{
		public ushort index;

		public BTExecNodeId(ushort index)
		{
			this.index = index;
		}

		public override string ToString() => $"Node({index})";

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
			var component = ctx.componentPtrs[componentIndex];
			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = component.AsNativeArray(field.offset, field.size);
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
			return $"{{ until={until}, for={duration} }}";
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
		public VariableId variable;

		public string DumpString()
		{
			return $"{{ input={input}, varIndex={variable} }}";
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
		public VariableId result;

		/// <summary>
		/// Int blackboard variable storing the result count (currently 0 or 1)
		/// </summary>
		public VariableId resultCount;

		/// <summary>
		/// Branch to execute on query success (results found)
		/// </summary>
		public BTExecNodeId success;

		/// <summary>
		/// Branch to execute on query failure (no results)
		/// </summary>
		public BTExecNodeId failure;

		/// <summary>
		/// Blackboard inputs to the query graph
		/// </summary>
		public BlobArray<WriteVar> inputs;

		public string DumpString()
		{
			return $"{{ query={queryIndex}, var={result}, nRes={resultCount}, success={success}, failure={failure} }}";
		}
	}

	public struct Parallel
	{
		public BTExecNodeId main;
		public BTExecNodeId parallel;

		public string DumpString()
		{
			return $"{{ main={main}, parallel={parallel} }}";
		}
	}

	public struct ThreadRoot
	{
		public BTExecNodeId child;
		public bool loop;

		public string DumpString()
		{
			return $"{{ child={child}, loop={loop} }}";
		}
	}

	public enum RepeatMode : byte
	{
		Count,
		Infinite,
		Condition,
	}

	public struct Repeat
	{
		public BTExecNodeId child;
		public ExpressionRef param;
		public VariableId counter;
		public RepeatMode mode;

		public string DumpString()
		{
			return $"{{ child={child}, param={param} }}";
		}
	}

	public struct Append
	{
		public byte componentIndex;
		public BlobArray<WriteField.Field> fields;

		public void Evaluate(in ExpressionEvalContext ctx)
		{
			var buffer = ctx.componentPtrs[componentIndex].AsBuffer();

			var elementBase = buffer.ElementSize * buffer.Length;

			buffer.Resize(buffer.Length + 1, NativeArrayOptions.ClearMemory);

			var data = buffer.AsNativeArray();

			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = data.GetSubArray(elementBase + field.offset, field.size);
				field.input.Evaluate(in ctx, ref fieldSpan);
			}
		}

		public string DumpString()
		{
			return $"{{ componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
		}
	}

	public struct Invoke
	{
		public int actionIndex;
		public bool blocking;

		public string DumpString()
		{
			return $"{{ actionIndex={actionIndex} blocking={blocking} }}";
		}
	}

	public struct WriteBufferField
	{
		public ExpressionRef bufferIndex;

		public byte componentIndex;

		public BlobArray<WriteField.Field> fields;

		public bool Evaluate(in ExpressionEvalContext ctx)
		{
			var component = ctx.componentPtrs[componentIndex];
			var buffer = component.AsBuffer();
			var index = bufferIndex.Evaluate<int>(ctx);

			if(index < 0 || index >= buffer.Length)
				return false;

			var data = buffer.AsNativeArray();
			var elemBase = index * buffer.ElementSize;

			for(int i = 0; i < fields.Length; ++i)
			{
				ref var field = ref fields[i];
				var fieldSpan = data.GetSubArray(elemBase + field.offset, field.size);
				field.input.Evaluate(in ctx, ref fieldSpan);
			}

			return true;
		}

		public string DumpString()
		{
			return $"{{ bufferIndex={bufferIndex} componentIndex={componentIndex}, fields=[{string.Join(", ", fields.ToArray())}] }}";
		}
	}
}
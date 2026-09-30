using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Khorde.Behavior
{
	/// <summary>
	/// Data for execution nodes.
	/// </summary>
	public struct BTExec
	{
		public BTExecType type;
		public Flags flags;
		[Tooltip("Index of this node within its parent Sequence")]
		public Data data;

		public enum BTExecType : byte
		{
			Nop,
			Root,
			Sequence,
			Selector,
			WriteField,
			Wait,
			Fail,
			If,
			Catch, // TODO: catch failures from child threads
			WriteVar,
			Query,
			Parallel,
			ThreadRoot,
			Repeat,
			Append,
			Invoke,
			WriteBufferField,
			WriteLookupField,
			UtilitySelector,
			Utility,
			UtilityCooldown,
			UtilityCurve,
			UtilityRange,
			Once,
			Log,
		}

		public enum Flags : byte
		{
			None = 0,
			ZeroUtility = 1 << 0,
		}

		[StructLayout(LayoutKind.Explicit, Pack = 8)]
		public struct Data
		{
			[FieldOffset(0)] public Root root;
			[FieldOffset(0)] public Sequence sequence;
			[FieldOffset(0)] public Selector selector;
			[FieldOffset(0)] public WriteField writeField;
			[FieldOffset(0)] public Wait wait;
			[FieldOffset(0)] public Fail fail;
			[FieldOffset(0)] public If @if;
			[FieldOffset(0)] public Catch @catch;
			[FieldOffset(0)] public WriteVar writeVar;
			[FieldOffset(0)] public Query query;
			[FieldOffset(0)] public Parallel parallel;
			[FieldOffset(0)] public ThreadRoot threadRoot;
			[FieldOffset(0)] public Repeat repeat;
			[FieldOffset(0)] public Append append;
			[FieldOffset(0)] public Invoke invoke;
			[FieldOffset(0)] public WriteBufferField writeBufferField;
			[FieldOffset(0)] public WriteLookupField writeLookupField;
			[FieldOffset(0)] public UtilitySelector utilitySelector;
			[FieldOffset(0)] public Utility utility;
			[FieldOffset(0)] public UtilityCooldown utilityCooldown;
			[FieldOffset(0)] public UtilityCurve utilityCurve;
			[FieldOffset(0)] public UtilityRange utilityRange;
			[FieldOffset(0)] public Once once;
			[FieldOffset(0)] public Log log;
		}

		public string DumpString()
		{
			string result = type.ToString() + ":";

			switch(type)
			{
				case BTExecType.Nop: break;
				case BTExecType.Root: result += data.root.DumpString(); break;
				case BTExecType.Sequence: result += data.sequence.DumpString(); break;
				case BTExecType.Selector: result += data.selector.DumpString(); break;
				case BTExecType.WriteField: result += data.writeField.DumpString(); break;
				case BTExecType.Wait: result += data.wait.DumpString(); break;
				case BTExecType.Fail: result += data.fail.DumpString(); break;
				case BTExecType.If: result += data.@if.DumpString(); break;
				case BTExecType.Catch: result += data.@catch.DumpString(); break;
				case BTExecType.WriteVar: result += data.writeVar.DumpString(); break;
				case BTExecType.Query: result += data.query.DumpString(); break;
				case BTExecType.Parallel: result += data.parallel.DumpString(); break;
				case BTExecType.ThreadRoot: result += data.threadRoot.DumpString(); break;
				case BTExecType.Repeat: result += data.repeat.DumpString(); break;
				case BTExecType.Append: result += data.append.DumpString(); break;
				case BTExecType.Invoke: result += data.invoke.DumpString(); break;
				case BTExecType.WriteBufferField: result += data.writeBufferField.DumpString(); break;
				case BTExecType.WriteLookupField: result += data.writeLookupField.DumpString(); break;
				case BTExecType.UtilitySelector: result += data.utilitySelector.DumpString(); break;
				case BTExecType.Utility: result += data.utility.DumpString(); break;
				case BTExecType.UtilityCooldown: result += data.utilityCooldown.DumpString(); break;
				case BTExecType.UtilityCurve: result += data.utilityCurve.DumpString(); break;
				case BTExecType.UtilityRange: result += data.utilityRange.DumpString(); break;
				case BTExecType.Once: result += data.once.DumpString(); break;
				case BTExecType.Log: result += data.log.DumpString(); break;
				default: break;
			}

			return result;
		}
	}

	public static class BTExecExt
	{
		public static FixedString32Bytes ToFixedString(this BTExec.BTExecType type)
		{
			switch(type)
			{
			case BTExec.BTExecType.Nop: return "Nop";
			case BTExec.BTExecType.Root: return "Root";
			case BTExec.BTExecType.Sequence: return "Sequence";
			case BTExec.BTExecType.Selector: return "Selector";
			case BTExec.BTExecType.WriteField: return "WriteField";
			case BTExec.BTExecType.Wait: return "Wait";
			case BTExec.BTExecType.Fail: return "Fail";
			case BTExec.BTExecType.If: return "If";
			case BTExec.BTExecType.Catch: return "Catch";
			case BTExec.BTExecType.WriteVar: return "WriteVar";
			case BTExec.BTExecType.Query: return "Query";
			case BTExec.BTExecType.Parallel: return "Parallel";
			case BTExec.BTExecType.ThreadRoot: return "ThreadRoot";
			case BTExec.BTExecType.Repeat: return "Repeat";
			case BTExec.BTExecType.Append: return "Append";
			case BTExec.BTExecType.Invoke: return "Invoke";
			case BTExec.BTExecType.WriteBufferField: return "WriteBufferField";
			case BTExec.BTExecType.WriteLookupField: return "WriteLookupField";
			case BTExec.BTExecType.UtilitySelector: return "UtilitySelector";
			case BTExec.BTExecType.Utility: return "Utility";
			case BTExec.BTExecType.UtilityCooldown: return "UtilityCooldown";
			case BTExec.BTExecType.UtilityCurve: return "UtilityCurve";
			case BTExec.BTExecType.UtilityRange: return "UtilityRange";
			case BTExec.BTExecType.Once: return "Once";
			case BTExec.BTExecType.Log: return "Log";
			default: return "[unknown]";
			}
		}
	}
}

using System.Runtime.InteropServices;
using UnityEngine;

namespace Khorde.Behavior
{
	/// <summary>
	/// Data for execution nodes.
	/// </summary>
	public struct BTExec
	{
		public BTExecType type;
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
			Optional,
			Catch, // TODO: catch failures from child threads
			WriteVar,
			Query,
			Parallel,
			ThreadRoot,
			// Repeat, // TODO: finite count, condition
			// Invoke, // TODO: execute callback on main thread (option: execute directly in job)
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
			[FieldOffset(0)] public Optional optional;
			[FieldOffset(0)] public Catch @catch;
			[FieldOffset(0)] public WriteVar writeVar;
			[FieldOffset(0)] public Query query;
			[FieldOffset(0)] public Parallel parallel;
			[FieldOffset(0)] public ThreadRoot threadRoot;
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
				case BTExecType.Optional: result += data.optional.DumpString(); break;
				case BTExecType.Catch: result += data.@catch.DumpString(); break;
				case BTExecType.WriteVar: result += data.writeVar.DumpString(); break;
				case BTExecType.Query: result += data.query.DumpString(); break;
				case BTExecType.Parallel: result += data.parallel.DumpString(); break;
				case BTExecType.ThreadRoot: result += data.threadRoot.DumpString(); break;
				default: break;
			}

			return result;
		}
	}
}

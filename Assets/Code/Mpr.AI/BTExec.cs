using System.Runtime.InteropServices;
using UnityEngine;

namespace Mpr.AI.BT
{
	/// <summary>
	/// Data for execution nodes.
	/// </summary>
	public struct BTExec
	{
		public Type type;
		[Tooltip("Index of this node within its parent Sequence")]
		public Data data;

		public enum Type : byte
		{
			Nop,
			Root,
			Sequence,
			Selector,
			WriteField,
			Wait,
			Fail,
			Optional,
			Catch,
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
		}

		public string DumpString()
		{
			string result = type.ToString() + ":";

			switch(type)
			{
				case Type.Nop: break;
				case Type.Root: result += data.root.DumpString(); break;
				case Type.Sequence: result += data.sequence.DumpString(); break;
				case Type.Selector: result += data.selector.DumpString(); break;
				case Type.WriteField: result += data.writeField.DumpString(); break;
				case Type.Wait: result += data.wait.DumpString(); break;
				case Type.Fail: result += data.fail.DumpString(); break;
				case Type.Optional: result += data.optional.DumpString(); break;
				case Type.Catch: result += data.@catch.DumpString(); break;
				default: break;
			}

			return result;
		}
	}
}

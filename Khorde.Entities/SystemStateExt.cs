using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using BF = System.Reflection.BindingFlags;

namespace Khorde.Entities
{
	public static class SystemStateExt
	{
		/// <summary>
		/// Entities warns about creating queries during ISystem.OnUpdate due
		/// to performance considerations, but in certain data-driven scenarios
		/// we have no choice.
		/// <para/>
		/// Call this in ISystem.OnCreate to silence the
		/// warning for the system.
		/// </summary>
		/// <param name="state"></param>
		/// <exception cref="InvalidOperationException"></exception>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
		public static void IgnoreCreateQueryInOnUpdateWarning(ref this SystemState state)
		{
			var flagsField = typeof(SystemState).GetField("m_Flags", BF.NonPublic | BF.Instance);

			if(flagsField.FieldType != typeof(uint))
				throw new InvalidOperationException("SystemState layout has changed");

			var warnFlagField = typeof(SystemState).GetField("kDidWarnIsExecutingISystemOnUpdate", BF.NonPublic | BF.Static);
			var warnFlag = (uint)warnFlagField.GetValue(null);
			var flagsFieldOffset = UnsafeUtility.GetFieldOffset(flagsField);
			unsafe
			{
				fixed(SystemState* pState = &state)
				{
					uint* pFlags = (uint*)((byte*)pState + flagsFieldOffset);
					*pFlags |= warnFlag;
				}
			}
		}
	}
}

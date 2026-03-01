using System;
using System.Runtime.CompilerServices;

namespace Khorde.Expr
{
	/// <summary>
	/// Type-safe wrapper for a blackboard variable index.
	/// </summary>
	public struct VariableId : IEquatable<VariableId>
	{
		public sbyte index;

		public static readonly VariableId Invalid = new VariableId(-1);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VariableId(int index) { if(index < -1 || index > sbyte.MaxValue) throw new ArgumentOutOfRangeException(); this.index = (sbyte)index; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj) { return obj is VariableId index && Equals(index); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(VariableId other) { return index == other.index; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() { return index; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator==(VariableId lhs,  VariableId rhs) => lhs.index == rhs.index;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator!=(VariableId lhs,  VariableId rhs) => lhs.index != rhs.index;

		public override string ToString() => $"Var({index})";
	}
}

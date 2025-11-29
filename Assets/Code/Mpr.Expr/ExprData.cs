using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Mpr.Expr
{
	public struct ExprData
	{
		public BlobArray<BTExpr> exprs;
		public BlobArray<byte> constData;
		public BlobArray<ulong> componentTypes;
		public BlobArray<UnityEngine.Hash128> exprNodeIds;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExpr GetNode(ExprNodeRef nodeRef) => ref exprs[nodeRef.index];
	}
}

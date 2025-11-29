using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Mpr.Expr
{
	public struct BTExprData
	{
		public BlobArray<BTExpr> exprs;
		public BlobArray<byte> constData;
		public BlobArray<ulong> componentTypes;
		public BlobArray<UnityEngine.Hash128> exprNodeIds;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExpr GetNode(BTExprNodeRef nodeRef) => ref exprs[nodeRef.index];
	}
}

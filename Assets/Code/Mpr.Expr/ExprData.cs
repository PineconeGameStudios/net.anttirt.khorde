using Mpr.Blobs;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Mpr.Expr
{
	public struct ExprData
	{
		public BlobArray<BTExpr> exprs;
		public BlobArray<byte> constData;

		/// <summary>
		/// Components for direct access on the expression owner entity as part of BT/EQ chunk iteration
		/// </summary>
		public BlobArray<BlobComponentType> localComponents;

		/// <summary>
		/// Components for indirect access on other entities
		/// </summary>
		public BlobArray<BlobComponentType> lookupComponents;

		public BlobArray<UnityEngine.Hash128> exprNodeIds;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExpr GetNode(ExprNodeRef nodeRef) => ref exprs[nodeRef.index];
	}
}

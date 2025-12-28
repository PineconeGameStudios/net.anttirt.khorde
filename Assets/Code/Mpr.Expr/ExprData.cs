using Mpr.Blobs;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Mpr.Expr
{
	public struct ExprData
	{
		/// <summary>
		/// Storage for expression nodes.
		/// </summary>
		public BlobArray<BTExpr> exprs;

		/// <summary>
		/// Storage for baking-time constant values. Indexed by <see
		/// cref="ExprNodeRef"/> when <c><see cref="ExprNodeRef.constant"/> ==
		/// true</c>
		/// </summary>
		/// <remarks>
		/// This is a mixed-type buffer where each constant value is simply
		/// stored sequentially after the previous value. Alignment padding is
		/// applied between subsequent values.
		/// </remarks>
		public BlobArray<byte> constData;

		/// <summary>
		/// Components for direct access on the expression owner entity as part of BT/EQ chunk iteration
		/// </summary>
		public BlobArray<BlobComponentType> localComponents;

		/// <summary>
		/// Components for indirect access on other entities
		/// </summary>
		public BlobArray<BlobComponentType> lookupComponents;

		/// <summary>
		/// Source graph node ids corresponding to nodes in the <see cref="exprs"/> array. Used for debugging.
		/// </summary>
		public BlobArray<UnityEngine.Hash128> exprNodeIds;

		/// <summary>
		/// Resolve a node reference to a node.
		/// </summary>
		/// <param name="nodeRef"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref BTExpr GetNode(ExprNodeRef nodeRef) => ref exprs[nodeRef.index];
	}
}

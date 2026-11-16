using Khorde.Expr.Authoring;
using UnityEditor.Search;

namespace Khorde.Query.Authoring
{
	public static class QueryGraphIndexer
	{
		private const int Version = 1;

		[CustomObjectIndexer(typeof(QueryGraphAsset), version = Version)]
		private static void IndexQueryGraphAsset(CustomObjectIndexerTarget context, ObjectIndexer indexer) => ExprAuthoring.IndexGraphSourceAsset(context, indexer, context.target);

		// TODO: add placeholder query subgraph asset for indexing
		// [CustomObjectIndexer(typeof(QuerySubgraphAsset), version = Version)]
		// private static void IndexQuerySubgraphAsset(CustomObjectIndexerTarget context, ObjectIndexer indexer) => ExprAuthoring.IndexGraphSourceAsset(context, indexer, context.target);
	}
}

using Khorde.Expr.Authoring;
using UnityEditor.Search;

namespace Khorde.Behavior.Authoring
{
	public static class BehaviorGraphIndexer
	{
		private const int Version = 1;

		[CustomObjectIndexer(typeof(BehaviorTreeAsset), version = Version)]
		private static void IndexBehaviorTreeAsset(CustomObjectIndexerTarget context, ObjectIndexer indexer) => ExprAuthoring.IndexGraphSourceAsset(context, indexer, context.target);

		[CustomObjectIndexer(typeof(BehaviorTreeSubgraphAsset), version = Version)]
		private static void IndexBehaviorTreeSubgraphAsset(CustomObjectIndexerTarget context, ObjectIndexer indexer) => ExprAuthoring.IndexGraphSourceAsset(context, indexer, context.target);
	}
}

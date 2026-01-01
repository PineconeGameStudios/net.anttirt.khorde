using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mpr.Expr.Authoring
{
	[ScriptedImporter(2, ExprSubgraph.AssetExtension)]
	internal class ExprSubgraphImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			var asset = ScriptableObject.CreateInstance<ExprSubgraphAsset>();
			ctx.AddObjectToAsset("ExprSubgraph", asset);
			ctx.SetMainObject(asset);
		}
	}
}

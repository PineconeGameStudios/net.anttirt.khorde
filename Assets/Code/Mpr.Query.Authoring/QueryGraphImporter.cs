using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	[ScriptedImporter(1, QueryGraph.AssetExtension)]
	internal class QueryGraphImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			var asset = ScriptableObject.CreateInstance<QueryGraphAsset>();
			ctx.AddObjectToAsset("QueryGraph", asset);
			ctx.SetMainObject(asset);
		}
	}
}

using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mpr.AI.BT
{
	[ScriptedImporter(1, BehaviorTreeGraph.AssetExtension)]
	internal class BehaviorTreeImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			var graph = GraphDatabase.LoadGraphForImporter<BehaviorTreeGraph>(ctx.assetPath);

			if(graph == null)
			{
				ctx.LogImportError($"Failed to load graph of type '{nameof(BehaviorTreeGraph)}' from path '{ctx.assetPath}'");
				return;
			}

			var asset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
			ctx.AddObjectToAsset("BehaviorTree", asset);
			ctx.SetMainObject(asset);
		}
	}
}
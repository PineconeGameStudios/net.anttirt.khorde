using System;
using System.Linq;
using Unity.Entities.Serialization;
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

			bool isSubgraph = graph.GetNodes().OfType<IVariableNode>().Any(v => v.variable.variableKind == VariableKind.Input || v.variable.variableKind == VariableKind.Output);

			if(isSubgraph)
			{
				// not importing subgraphs
				return;
			}

			var asset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();

			var writer = new MemoryBinaryWriter();
			graph.Bake(writer);
			ReadOnlySpan<byte> data;

			unsafe
			{
				data = new ReadOnlySpan<byte>(writer.Data, writer.Length);
			}

			asset.bakedGraph = new TextAsset(data);
			asset.bakedGraph.name = "Data";

			ctx.AddObjectToAsset("Data", asset.bakedGraph);
			ctx.AddObjectToAsset("BehaviorTree", asset);
			ctx.SetMainObject(asset);
		}
	}
}
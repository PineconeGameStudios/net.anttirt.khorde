using System;
using System.Linq;
using Unity.Entities.Serialization;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	[ScriptedImporter(1, QueryGraph.AssetExtension)]
	internal class QueryGraphImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			var graph = GraphDatabase.LoadGraphForImporter<QueryGraph>(ctx.assetPath);

			if(graph == null)
			{
				ctx.LogImportError($"Failed to load graph of type '{nameof(QueryGraph)}' from path '{ctx.assetPath}'");
				return;
			}

			bool isSubgraph = graph.GetNodes().OfType<IVariableNode>().Any(v => v.variable.variableKind == VariableKind.Input || v.variable.variableKind == VariableKind.Output);

			if(isSubgraph)
			{
				// not importing subgraphs
			}
			else
			{
				var asset = ScriptableObject.CreateInstance<QueryGraphAsset>();

				var writer = new MemoryBinaryWriter();
				graph.Bake(writer);
				ReadOnlySpan<byte> data;

				unsafe
				{
					data = new ReadOnlySpan<byte>(writer.Data, writer.Length);
				}

				asset.bakedQuery = new TextAsset(data);
				asset.bakedQuery.name = "Data";

				ctx.AddObjectToAsset("Data", asset.bakedQuery);
				ctx.AddObjectToAsset("BehaviorTree", asset);
				ctx.SetMainObject(asset);
			}
		}
	}
}

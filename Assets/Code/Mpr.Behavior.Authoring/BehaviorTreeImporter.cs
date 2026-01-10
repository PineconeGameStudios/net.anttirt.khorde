using System;
using System.IO;
using System.Linq;
using Mpr.Behavior.Authoring;
using Unity.Collections;
using Unity.Entities.Serialization;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mpr.Behavior
{
	[ScriptedImporter(3, BehaviorTreeGraph.AssetExtension)]
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
				var asset = ScriptableObject.CreateInstance<BehaviorTreeSubgraphAsset>();
				ctx.AddObjectToAsset("BehaviorTreeSubgraph", asset);
				ctx.SetMainObject(asset);
			}
			else
			{
				using (var context = new BTBakingContext(graph, Allocator.Temp))
				{
					var builder = context.Build();
					if (!builder.IsCreated)
					{
						ctx.LogImportError("Build failed");
						return;
					}

					if (context.Errors.Count > 0)
					{
						foreach (var error in context.Errors)
							ctx.LogImportError(error);

						return;
					}
					
					var obj = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
					var data = obj.SetAssetData(builder, BTData.SchemaVersion);
					ctx.AddObjectToAsset(Path.GetFileNameWithoutExtension(ctx.assetPath), obj);
					ctx.AddObjectToAsset("data", data);
					ctx.SetMainObject(obj);
				}

				//var writer = new MemoryBinaryWriter();
				//graph.Bake(writer);
				//ReadOnlySpan<byte> data;

				//unsafe
				//{
				//	data = new ReadOnlySpan<byte>(writer.Data, writer.Length);
				//}

				//asset.bakedGraph = new TextAsset(data);
				//asset.bakedGraph.name = "Data";

				//ctx.AddObjectToAsset("Data", asset.bakedGraph);
				//ctx.AddObjectToAsset("BehaviorTree", asset);
				//ctx.SetMainObject(asset);
			}
		}
	}
}
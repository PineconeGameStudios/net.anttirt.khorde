using Khorde.Behavior.Authoring;
using Khorde.Expr.Authoring;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Khorde.Behavior
{
	[ScriptedImporter(BTData.SchemaVersion, BehaviorTreeGraph.AssetExtension, importQueueOffset: 3)]
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

			if(graph.nodeCount == 0)
			{
				// create a blank placeholder so creating a fresh asset doesn't result in a user-visible error
				var obj = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
				ctx.AddObjectToAsset("asset", obj);
				ctx.SetMainObject(obj);
			}
			else if(isSubgraph)
			{
				// not importing subgraphs, so just add a placeholder for the icon
				var asset = ScriptableObject.CreateInstance<BehaviorTreeSubgraphAsset>();
				ctx.AddObjectToAsset("BehaviorTreeSubgraph", asset);
				ctx.SetMainObject(asset);
			}
			else
			{
				using(var context = new BTBakingContext(graph, Allocator.Temp))
				{
					var builder = context.Build();

					if(!builder.IsCreated)
					{
						ctx.LogImportError($"importing asset '{ctx.assetPath}' failed");
					}

					if(context.Errors.Count > 0)
					{
						foreach(var (obj_, msg) in context.Errors)
							ctx.LogImportError(msg);

						return;
					}

					foreach(var log in context.Warnings)
						ctx.LogImportWarning(log.message);

					var obj = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
					obj.Queries.AddRange(context.Queries);
					obj.Actions.AddRange(context.Actions);
					foreach(var q in obj.Queries)
						ctx.DependsOnArtifact(AssetDatabase.GetAssetPath(q));
					foreach(var guid in graph.GetSubgraphs())
						ctx.DependsOnSourceAsset(AssetDatabase.GUIDToAssetPath(guid));
					var data = obj.SetAssetData(builder, BTData.SchemaVersion);
					ctx.AddObjectToAsset("asset", obj);
					ctx.AddObjectToAsset("data", data);
					ctx.SetMainObject(obj);
				}
			}
		}
	}
}

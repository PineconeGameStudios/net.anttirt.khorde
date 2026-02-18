using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Khorde.Query.Authoring
{
	[ScriptedImporter(1, QueryGraph.AssetExtension, importQueueOffset: 2)]
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

			if(graph.nodeCount == 0)
			{
				// create a blank placeholder so creating a fresh asset doesn't result in a user-visible error
				var obj = ScriptableObject.CreateInstance<QueryGraphAsset>();
				ctx.AddObjectToAsset(Path.GetFileNameWithoutExtension(ctx.assetPath), obj);
				ctx.SetMainObject(obj);
			}
			else if(isSubgraph)
			{
				// not importing subgraphs
			}
			else
			{
				using(var context = new QueryBakingContext(graph, Allocator.Temp))
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

					var obj = ScriptableObject.CreateInstance<QueryGraphAsset>();
					var data = obj.SetAssetData(builder, QSData.SchemaVersion);
					obj.entityQueries = context.EntityQueries.ToList();
					foreach(var eq in obj.entityQueries)
						ctx.DependsOnArtifact(AssetDatabase.GetAssetPath(eq));
					ctx.AddObjectToAsset(Path.GetFileNameWithoutExtension(ctx.assetPath), obj);
					ctx.AddObjectToAsset("data", data);
					ctx.SetMainObject(obj);
				}
			}
		}
	}
}

using Khorde.Expr.Authoring;
using System.Linq;
using Unity.Collections;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Khorde.Query.Authoring
{
	[ScriptedImporter(QSData.SchemaVersion | (ImporterVersion << 24), QueryGraph.AssetExtension, importQueueOffset: 2)]
	internal class QueryGraphImporter : ScriptedImporter
	{
		public const int ImporterVersion = 1;

		public static string[] GatherDependenciesFromSourceFile(string path) => ExprAuthoring.GatherDependenciesFromSourceFile(path);

		public override void OnImportAsset(AssetImportContext ctx)
		{
			try
			{
				//GraphDatabase.StartEphemeralLoad();

				var graph = GraphDatabase.LoadGraphForImporter<QueryGraph>(ctx.assetPath);

				if(graph == null)
				{
					ctx.LogImportError($"Failed to load graph of type '{nameof(QueryGraph)}' from path '{ctx.assetPath}'");
					return;
				}

				bool isSubgraph = graph.GetNodes().OfType<IVariableNode>().Any(v => v.Variable.VariableKind == VariableKind.Input || v.Variable.VariableKind == VariableKind.Output);

				if(graph.NodeCount == 0)
				{
					// create a blank placeholder so creating a fresh asset doesn't result in a user-visible error
					var obj = ScriptableObject.CreateInstance<QueryGraphAsset>();
					ctx.AddObjectToAsset("asset", obj);
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
						foreach(var guid in graph.GetSubgraphs())
							ctx.DependsOnSourceAsset(AssetDatabase.GUIDToAssetPath(guid));
						ctx.AddObjectToAsset("asset", obj);
						ctx.AddObjectToAsset("data", data);
						ctx.SetMainObject(obj);
					}
				}
			}
			finally
			{
				//GraphDatabase.EndEphemeralLoad();
			}
		}
	}
}

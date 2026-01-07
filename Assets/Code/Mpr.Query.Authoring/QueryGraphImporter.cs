using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
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
				var obj = ScriptableObject.CreateInstance<QueryGraphAsset>();

				using (var context = new QueryBakingContext(graph, Allocator.Temp))
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

					var data = obj.SetAssetData(builder, QSData.SchemaVersion);
					obj.entityQueries = context.EntityQueries.ToList();
					ctx.AddObjectToAsset(Path.GetFileNameWithoutExtension(ctx.assetPath), obj);
					ctx.AddObjectToAsset("data", data);
					ctx.SetMainObject(obj);
				}
			}
		}
	}
}

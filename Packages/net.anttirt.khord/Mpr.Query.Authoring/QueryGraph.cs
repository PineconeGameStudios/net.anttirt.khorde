
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	[Serializable]
	[Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
	[UseNodes(typeof(IQueryGraphNode))]
	[UseNodes(typeof(Expr.Authoring.IExprNode))]
	[UseSubgraph(typeof(Expr.Authoring.ExprSubgraph))]
	public class QueryGraph : Graph
	{
		internal const string AssetExtension = "queryg";

		const string k_graphName = "Query Graph";

		/// <summary>
		/// Creates a new Expression Subgraph asset file in the project window.
		/// </summary>
		[MenuItem("Assets/Create/Query Graph")]
		static void CreateAssetFile()
		{
			GraphDatabase.PromptInProjectBrowserToCreateNewAsset<QueryGraph>(k_graphName);
		}

		/// <summary>
		/// Called when the graph changes.
		/// </summary>
		/// <param name="infos">The GraphLogger object to which errors and warnings are added.</param>
		/// <remarks>
		/// This method is triggered whenever the graph is modified. It calls `CheckGraphErrors` to validate the graph
		/// and report any issues.
		/// </remarks>
		public override void OnGraphChanged(GraphLogger infos)
		{
			base.OnGraphChanged(infos);

			CheckGraphErrors(infos);
		}

		public override void OnEnable()
		{
			base.OnEnable();
		}

		/// <summary>
		/// Checks the graph for errors and warnings and adds them to the result object.
		/// </summary>
		/// <param name="infos">Object implementing <see cref="GraphLogger"/> interface and containing
		/// collected errors and warnings</param>
		/// <remarks>Errors and warnings are reported by adding them to the GraphLogger object,
		/// which is the default reporting mechanism for a Graph Toolkit tool. </remarks>
		void CheckGraphErrors(GraphLogger infos)
		{
			List<IQuery> queryNodes = new();

			foreach(var node in GetNodes())
			{
				if(node is IQuery query)
				{
					queryNodes.Add(query);
				}
			}

			if(queryNodes.Count == 0)
			{
				infos.LogError("The graph needs to have a Query node", this);
			}

			if(queryNodes.Count > 1)
			{
				foreach(var node in queryNodes)
					infos.LogError("More than one Query node found", node);
			}

			Type itemType = queryNodes[0].ItemType;

			foreach(var node in GetNodes())
			{
				if(node is IQueryCurrentItemNode currentItemNode)
				{
					if(itemType != currentItemNode.ItemType)
					{
						infos.LogError(
							$"Wrong item type. Expected '{itemType.FullName}'",
							node);
					}
				}

				if(node is QueryGraphContextBase context)
				{
					foreach(var blockNode in context.blockNodes)
					{
						if(blockNode is IQueryGraphNode iqn)
						{
							try
							{
								iqn.Validate(infos);
							}
							catch(Exception e)
							{
								infos.LogError(e.Message, blockNode);
								Debug.LogException(e);
							}
						}
					}
				}

				{
					if(node is IQueryGraphNode iqn)
					{
						try
						{
							iqn.Validate(infos);
						}
						catch(Exception e)
						{
							infos.LogError(e.Message, node);
							Debug.LogException(e);
						}
					}
				}
			}

			using(var context = new QueryBakingContext(this, Allocator.Temp))
			{
				var builder = context.Build();

				foreach(var (obj, msg) in context.Errors)
					infos.LogError(msg, obj);

				foreach(var (obj, msg) in context.Warnings)
					infos.LogWarning(msg, obj);
			}
		}

		public void Bake(BinaryWriter writer)
		{
			using(var context = new QueryBakingContext(this, Allocator.Temp))
			{
				var builder = context.Build();

				if(context.Errors.Count > 0)
				{
					throw new System.Exception($"Errors while baking {this}:\n\t" + string.Join("\n\t", context.Errors));
				}

				BlobAssetReference<QSData>.Write(writer, builder, 0);
			}
		}
	}
}

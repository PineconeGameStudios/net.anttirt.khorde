
using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Mpr.Query.Authoring
{
	[Serializable]
	[Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
	[UseNodes(typeof(IQueryNode))]
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
			// List<StartNode> startNodes = GetNodes().OfType<StartNode>().ToList();

			// switch (startNodes.Count)
			// {
			//     case 0:
			//         infos.LogError("Add a StartNode in your Visual Novel graph.", this);
			//         break;
			//     case >= 1:
			//         {
			//             foreach (var startNode in startNodes.Skip(1))
			//             {
			//                 infos.LogWarning($"VisualNovelDirector only supports one StartNode per graph. Only the first created one will be used.", startNode);
			//             }
			//             break;
			//         }
			// }
		}
	}
}

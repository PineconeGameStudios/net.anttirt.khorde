using Khorde.Behavior.Authoring;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Khorde.Behavior
{
	[Serializable]
	[Graph(AssetExtension, GraphOptions.SupportsSubgraphs, typeof(BehaviorTreeGraphViewController))]
	[UseNodes(typeof(Khorde.Expr.Authoring.IExprNode))]
	[UseSubgraph(typeof(Expr.Authoring.ExprSubgraph))]
	[UseSubgraph(typeof(BehaviorTreeGraph))]
	public class BehaviorTreeGraph : Graph
	{
		internal const string AssetExtension = "btg";

		const string k_graphName = "Behavior Tree Graph";

		/// <summary>
		/// Creates a new Visual Novel Director graph asset file in the project window.
		/// </summary>
		/// <remarks>This is also where we add the shortcut to create a new graph from the editor Asset menu.</remarks>
		[MenuItem("Assets/Create/Behavior Tree Graph")]
		static void CreateAssetFile()
		{
			GraphDatabase.PromptInProjectBrowserToCreateNewAsset<BehaviorTreeGraph>(k_graphName);
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
			using(var context = new BTBakingContext(this, Allocator.Temp))
			{
				var builder = context.Build();
				foreach(var (obj, msg) in context.Errors)
				{
					infos.LogError(msg, obj);
				}
			}
		}

		public void Bake(BinaryWriter writer)
		{
			using(var context = new BTBakingContext(this, Allocator.Temp))
			{
				var builder = context.Build();

				if(context.Errors.Count > 0)
				{
					throw new System.Exception($"Errors while baking {this}:\n\t" + string.Join("\n\t", context.Errors));
				}

				BlobAssetReference<BTData>.Write(writer, builder, 0);
			}
		}
	}
}

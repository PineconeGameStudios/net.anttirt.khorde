using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mpr.Behavior
{
	public class BehaviorTreeGraphViewController : EditorUpdatableObject, IGraphViewController
	{
		BTGraphAssets cachedAssets;
		IGraphView rootView;
		Graph graph;

		BTGraphAssets assets
		{
			get
			{
				if(cachedAssets == null)
					cachedAssets = AssetDatabase.LoadAssetByGUID<BTGraphAssets>(AssetDatabase.FindAssetGUIDs($"t:{nameof(BTGraphAssets)}")[0]);

				return cachedAssets;
			}
		}

		public void Initialize(IGraphView rootView)
		{
			this.rootView = rootView;
			this.graph = rootView.Graph;
		}

		protected override void OnEnable()
		{
		}

		protected override void OnUpdate()
		{
			if(Application.isPlaying)
			{
				var world = World.DefaultGameObjectInjectionWorld;

				if(world == null)
					return;

				var highlights = new NativeHashSet<UnityEngine.Hash128>(10, world.UpdateAllocator.ToAllocator);

				var entities = Unity.Entities.Editor.EntitySelection.GetActiveEntities(world, world.UpdateAllocator.ToAllocator);

				if(entities.Length == 1)
				{
					var entity = entities[0];
					var em = world.EntityManager;
					if(em.HasComponent<BehaviorTree>(entity)
						&& em.HasBuffer<BTStackFrame>(entity))
					{
						ref var btData = ref em.GetSharedComponent<BehaviorTree>(entity).tree.Value;
						var stack = em.GetBuffer<BTStackFrame>(entity);

						foreach(var frame in stack)
						{
							var index = frame.nodeId.index;
							highlights.Add(btData.execNodeIds[index]);
							ref var sgstack = ref btData.execNodeSubgraphStacks[index];
							for(int j = 0; j < sgstack.Length; ++j)
								highlights.Add(sgstack[j]);
						}
					}
				}

				foreach(var (node, nodeView) in graph.GetNodes().Select(n => (n, n.GetView(rootView))).Where(n => n.Item2 != null))
				{
					bool highlight = highlights.Contains(node.Guid);

					if(nodeView is VisualElement ve)
					{
						if(highlight)
							ve.styleSheets.Add(assets.executionHighlightStyle);
						else
							ve.styleSheets.Remove(assets.executionHighlightStyle);
					}
				}
			}
		}

		protected override void OnDisable()
		{
			foreach(var (node, nodeView) in graph.GetNodes().Select(n => (n, n.GetView(rootView))).Where(n => n.Item2 != null))
			{
				if(nodeView is VisualElement ve)
				{
					ve.styleSheets.Remove(assets.executionHighlightStyle);
				}
			}
		}
	}
}
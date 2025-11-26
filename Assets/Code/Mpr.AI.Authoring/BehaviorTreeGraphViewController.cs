using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Mpr.AI.BT
{
	public class BehaviorTreeGraphViewController : IGraphViewController
	{
		IGraphView rootView;
		Graph graph;
		World world;

		public void OnEnable()
		{
			this.graph = rootView.Graph;

			UnityEngine.Debug.Log($"BTGVC.Enable() with Graph={graph}");

			// var views = graph.GetNodes().Select(node => node.GetView(rootView)).ToList();

			EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
		}

		private void EditorApplication_playModeStateChanged(PlayModeStateChange stateChange)
		{
			if(stateChange == PlayModeStateChange.EnteredPlayMode)
			{
				EditorApplication.update += OnUpdate;
			}
			else
			{
				EditorApplication.update -= OnUpdate;
				ClearHighlights();
			}
		}

		private void OnUpdate()
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
					nodeView.OverrideHighlighted = highlights.Contains(node.Guid);
				}
			}
		}

		private void ClearHighlights()
		{
			foreach(var (node, nodeView) in graph.GetNodes().Select(n => (n, n.GetView(rootView))).Where(n => n.Item2 != null))
			{
				nodeView.OverrideHighlighted = false;
			}
		}

		public void Dispose()
		{
			EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
			UnityEngine.Debug.Log($"BTGVC.Dispose()");
		}

		public void SetRootView(IGraphView rootView)
		{
			UnityEngine.Debug.Log($"BTGVC.SetRootView({rootView})");
			this.rootView = rootView;
		}
	}
}
using Khorde.Behavior;
using Khorde.Entities;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.GraphToolkit.Editor.GraphVisualization;
using Unity.Mathematics;

namespace Khorde.Behaviour.Authoring
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	partial class BehaviorTreeDebugGraphSystem : SystemBase
	{
		class GraphContext
		{
			public HashSet<NodeReference> playing = new();
			public HashSet<NodeReference> stillPlaying = new();
			public Context context;

			public GraphContext(Context context)
			{
				this.context = context;
			}
		}

		Dictionary<Hash128, GraphContext> contexts = new();

		protected override void OnDestroy()
		{
			foreach(var (_, context) in contexts)
			{
				context.context.ClearAllVisualization();
				context.context.Dispose();
			}

			contexts.Clear();
		}

		protected override void OnUpdate()
		{
			var selected = SelectedEntity.Value;

			foreach(var (_, context) in contexts)
				context.stillPlaying.Clear();

			if(EntityManager.HasComponent<BehaviorTree>(selected))
			{
				ref var tree = ref EntityManager.GetSharedComponent<BehaviorTree>(selected).tree.Value;

				if(tree.graphId == default)
				{
					UnityEngine.Debug.LogError($"tree was not baked with a graph id");
					return;
				}

				if(!contexts.TryGetValue(tree.graphId, out var context))
					context = contexts[tree.graphId] = new(Registry.CreateVisualizationContext(tree.graphId));

				var stack = SystemAPI.GetBuffer<BTStackFrame>(selected);
				var state = SystemAPI.GetComponent<BTState>(selected);
				var threads = SystemAPI.GetBuffer<BTThread>(selected);
				var utility = SystemAPI.GetBuffer<BTUtilityDebug>(selected);
				var now = (float)SystemAPI.Time.ElapsedTime;

				// preview utility values
				if(utility.Length > 0)
				{
					for(int i = 0; i < tree.execs.Length; ++i)
					{
						ref var node = ref tree.execs[i];

						if(tree.utilityPreviewPortIds[i] != default)
						{
							var port = context.context.GetPortReference(tree.utilityPreviewPortIds[i]);
							// TODO: get the last computed utility value; separate buffer?
							port.SetPreview(utility[i].value.ToString("N2"));
						}
					}
				}

				// active nodes on the thread's current stack

				for(int tid = 0; tid < threads.Length; ++tid)
				{
					var threadStack = stack.AsNativeArray().GetSubArray(threads[tid].frameOffset, threads[tid].frameCount);
					for(int frameId = 0; frameId < threadStack.Length; ++frameId)
					{
						var frame = threadStack[frameId];
						var nodeId = tree.execNodeIds[frame.nodeId.index];

						// generate nodes (Nop etc) don't have a matching graph node
						if(!nodeId.isValid)
							continue;

						var nodeRef = context.context.GetNodeReference(nodeId);

						ref var nodeData = ref tree.GetNode(frame.nodeId);
						switch(nodeData.type)
						{
							case BTExec.BTExecType.Wait:
								if(threads[tid].waitStartTime != BehaviorTreeExecution.ThreadWaitStartTime_Invalid)
								{
									nodeRef.FillAmount = math.saturate((now - threads[tid].waitStartTime) / threads[tid].waitDuration);
								}
								else
								{
									PlayNode(context, nodeRef, nodeData.type);
								}
								break;

							case BTExec.BTExecType.UtilityCooldown:
								// TODO: get cooldown progress
								PlayNode(context, nodeRef, nodeData.type);
								break;

							default:
								PlayNode(context, nodeRef, nodeData.type);
								break;
						}
					}
				}
			}

			foreach(var (_, context) in contexts)
			{
				if(context.playing.Count != context.stillPlaying.Count)
				{
					foreach(var nodeRef in context.playing.ToArray())
					{
						if(!context.stillPlaying.Contains(nodeRef))
						{
							context.playing.Remove(nodeRef);
							try
							{
								nodeRef.Context.Motion.Stop(nodeRef);
							}
							catch(System.Exception) { }
						}
					}
				}
			}
		}

		private void PlayNode(GraphContext context, NodeReference nodeRef, BTExec.BTExecType nodeType)
		{
			if(context.playing.Add(nodeRef))
			{
				try
				{
					nodeRef.Context.Motion.Play(nodeRef);
				}
				catch(System.Exception e)
				{
					UnityEngine.Debug.Log($"{e.GetType().Name}: {e.Message.Replace("\r\n", " ").Replace('\n', ' ')}. ID={nodeRef.NodeID} Type={nodeType}");
					try
					{
						nodeRef.Context.Motion.Stop(nodeRef);
					}
					catch(System.Exception) { }
				}
			}

			context.stillPlaying.Add(nodeRef);
		}
	}
}
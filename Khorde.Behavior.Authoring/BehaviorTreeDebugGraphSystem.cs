using Khorde.Behavior;
using Khorde.Entities;
using System.Collections.Generic;
using Unity.Entities;
using Unity.GraphToolkit.Editor.GraphVisualization;
using Unity.Mathematics;

namespace Khorde.Behaviour.Authoring
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	partial class BehaviorTreeDebugGraphSystem : SystemBase
	{
		HashSet<NodeReference> playing = new();
		HashSet<NodeReference> stillPlaying = new();
		Dictionary<Hash128, Context> contexts = new();

		protected override void OnDestroy()
		{
			foreach(var (_, context) in contexts)
				context.Dispose();

			contexts.Clear();
		}

		protected override void OnUpdate()
		{
			var selected = SelectedEntity.Value;
			if(EntityManager.HasComponent<BehaviorTree>(selected))
			{
				ref var tree = ref EntityManager.GetSharedComponent<BehaviorTree>(selected).tree.Value;

				if(tree.graphId == default)
				{
					UnityEngine.Debug.LogError($"tree was not baked with a graph id");
					return;
				}

				if(!contexts.TryGetValue(tree.graphId, out var context))
					context = Registry.CreateVisualizationContext(tree.graphId);

				var stack = EntityManager.GetBuffer<BTStackFrame>(selected, isReadOnly: true);
				var state = EntityManager.GetComponentData<BTState>(selected);
				var threads = EntityManager.GetBuffer<BTThread>(selected);
				var now = (float)SystemAPI.Time.ElapsedTime;

				// preview utility values
				for(int i = 0; i < tree.execs.Length; ++i)
				{
					ref var node = ref tree.execs[i];

					if(tree.utilityPreviewPortIds[i] != default)
					{
						var port = context.GetPortReference(tree.utilityPreviewPortIds[i]);
						// TODO: get the last computed utility value; separate buffer?
						port.SetPreview("0.34");
					}
				}

				// active nodes on the thread's current stack
				stillPlaying.Clear();

				for(int tid = 0; tid < threads.Length; ++tid)
				{
					var threadStack = stack.AsNativeArray().GetSubArray(threads[tid].frameOffset, threads[tid].frameCount);
					for(int frameId = 0; frameId < threadStack.Length; ++frameId)
					{
						var frame = threadStack[frameId];
						var nodeRef = context.GetNodeReference(tree.execNodeIds[frame.nodeId.index]);

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
									PlayNode(context, nodeRef);
								}
								break;

							case BTExec.BTExecType.UtilityCooldown:
								// TODO: get cooldown progress
								PlayNode(context, nodeRef);
								break;

							default:
								PlayNode(context, nodeRef);
								break;
						}
					}
				}

				foreach(var nodeRef in playing)
				{
					if(!stillPlaying.Contains(nodeRef))
					{
						playing.Remove(nodeRef);
						context.Motion.Stop(nodeRef);
					}
				}
			}
		}

		private void PlayNode(Context context, NodeReference nodeRef)
		{
			if(playing.Add(nodeRef))
				context.Motion.Play(nodeRef);

			stillPlaying.Add(nodeRef);
		}
	}
}
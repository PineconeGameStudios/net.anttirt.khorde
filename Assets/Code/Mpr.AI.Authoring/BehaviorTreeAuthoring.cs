using Mpr.AI.BT.Nodes;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Transforms;
using UnityEngine;

namespace Mpr.AI.BT
{
	public class BehaviorTreeAuthoring : MonoBehaviour
	{
		public BehaviorTreeAsset behaviorTree;

		class Baker : Baker<BehaviorTreeAuthoring>
		{
			public override void Bake(BehaviorTreeAuthoring authoring)
			{
				if(authoring.behaviorTree == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				var src = authoring.behaviorTree;

				var allNodes = src.graph.GetNodes().ToList();

				var execNodes = allNodes.OfType<IExecNode>().ToList();
				var exprNodes = allNodes.OfType<IExprNode>().ToList();
				var constStorage = new NativeList<byte>(1024, Allocator.Temp);

				var builder = new BlobBuilder(Allocator.Temp);
				ref var data = ref builder.ConstructRoot<BTData>();
				var execs = builder.Allocate(ref data.execs, execNodes.Count + 1); // leave room for default Nop node at start
				var exprs = builder.Allocate(ref data.exprs, exprNodes.Count);

				// Nop is used as a default fallback for unconnected exec ports
				execs[0].type = BTExec.Type.Nop;
				const int ExecNodeOffset = 1;

				var exprNodeMap = exprNodes.Select((e, index) => (e, index)).ToDictionary(p => (INode)p.e, p => new BTExprNodeRef((ushort)p.index, default, false));
				var execNodeMap = execNodes.Select((e, index) => (e, index)).ToDictionary(p => (INode)p.e, p => new BTExecNodeId((ushort)(p.index + ExecNodeOffset)));

				var destinationPorts = new List<IPort>();

				var errors = new List<string>();

				var componentTypes = new List<System.Type> { typeof(LocalTransform) };

				var context = new BakingContext(execNodeMap, exprNodeMap, constStorage, componentTypes, errors);

				for(int i = 0; i < execNodes.Count; ++i)
					execNodes[i].Bake(ref builder, ref execs[i + ExecNodeOffset], context);

				for(int i = 0; i < exprNodes.Count; ++i)
					exprNodes[i].Bake(ref builder, ref exprs[i], context);

				if(errors.Count > 0)
				{
					throw new System.Exception($"Errors while baking {authoring}:\n\t" + string.Join("\n\t", errors));
				}

				BehaviorTreeAuthoringExt.WriteConstStorage(ref builder, ref data, constStorage);

				var tree = builder.CreateBlobAssetReference<BTData>(Allocator.Persistent);
				AddBlobAsset(ref tree, out _);

				AddComponent(entity, new BehaviorTree
				{
					tree = tree,
				});
			}
		}
	}
}

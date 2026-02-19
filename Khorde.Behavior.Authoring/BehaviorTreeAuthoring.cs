using Khorde.Blobs;
using Khorde.Expr;
using Khorde.Expr.Authoring;
using Khorde.Query;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Khorde.Behavior
{
	[Icon("Packages/net.anttirt.khorde/Icons/BehaviorGraph.psd")]
	public class BehaviorTreeAuthoring : MonoBehaviour
	{
		public BehaviorTreeAsset behaviorTree;

		class Baker : Baker<BehaviorTreeAuthoring>
		{
			public override void Bake(BehaviorTreeAuthoring authoring)
			{
				DependsOn(authoring.behaviorTree);

				if(authoring.behaviorTree == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				AddSharedComponent(entity, new BehaviorTree
				{
					tree = authoring.behaviorTree,
				});

				AddBuffer<BTStackFrame>(entity);
				var blackboard = AddBuffer<ExpressionBlackboardStorage>(entity);

				ref var exprData = ref authoring.behaviorTree.GetValue(BTData.SchemaVersion).exprData;

				{
					var exprDatas = new List<(Hash128, Ptr<BlobExpressionData>)>();
					var assetLookup = new Dictionary<Hash128, BlobAssetBase>();
					exprDatas.Add((authoring.behaviorTree.DataHash, new Ptr<BlobExpressionData>(ref exprData)));
					assetLookup[authoring.behaviorTree.DataHash] = authoring.behaviorTree;

					foreach(var query in authoring.behaviorTree.Queries)
					{
						exprDatas.Add((query.DataHash, new Ptr<BlobExpressionData>(ref query.GetValue(QSData.SchemaVersion).exprData)));
						assetLookup[query.DataHash] = query;
					}

					var layout = ExprAuthoring.ComputeLayout(exprDatas);

					// foreach(var (asset, layoutVariables) in layout)
					// {
					// 	Debug.Log($"{assetLookup[asset]} blackboard layout:\n" + string.Join('\n', layoutVariables.Select(lv => $"{lv.name}: {lv.offset}+{lv.length} (global:{lv.isGlobal})")));
					// }

					var baked = ExprAuthoring.BakeLayout(layout, Allocator.Persistent);
					AddBlobAsset(ref baked, out var _);
					AddSharedComponent(entity, new ExpressionBlackboardLayouts() { asset = baked, });

					blackboard.Resize(baked.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), NativeArrayOptions.ClearMemory);
				}

				AddComponent(entity, new BTState { });

				if(authoring.behaviorTree.Queries.Count > 0)
				{
					var reg = new QueryAssetRegistration();
					foreach(var query in authoring.behaviorTree.Queries)
						reg.Add(query);
					AddSharedComponent(entity, reg);
					AddComponent(entity, new PendingQuery());
					SetComponentEnabled<PendingQuery>(entity, false);
					AddBuffer<QSResultItemStorage>(entity);
				}
			}
		}
	}
}

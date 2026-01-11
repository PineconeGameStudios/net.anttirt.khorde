using System.Collections.Generic;
using System.Linq;
using Mpr.Blobs;
using Mpr.Expr;
using Mpr.Expr.Authoring;
using Mpr.Query;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Mpr.Behavior
{
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
					var exprDatas = new List<(BlobAssetBase, Ptr<BlobExpressionData>)>();
					exprDatas.Add((authoring.behaviorTree, new Ptr<BlobExpressionData>(ref exprData)));

					foreach (var query in authoring.behaviorTree.Queries)
						exprDatas.Add((query, new Ptr<BlobExpressionData>(ref query.GetValue(QSData.SchemaVersion).exprData)));

					var layout = ExprAuthoring.ComputeLayout(exprDatas);

					foreach (var (asset, layoutVariables) in layout)
					{
						Debug.Log($"{asset} blackboard layout:\n" + string.Join('\n', layoutVariables.Select(lv => $"{lv.name}: {lv.offset}+{lv.length} (global:{lv.isGlobal})")));
					}
					
					var baked = ExprAuthoring.BakeLayout(layout, Allocator.Persistent);
					AddBlobAsset(ref baked, out var _);
					AddSharedComponent(entity, new ExpressionBlackboardLayouts() { asset = baked, });

					blackboard.Resize(baked.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), NativeArrayOptions.ClearMemory);
				}

				AddComponent(entity, new BTState { });

				if (authoring.behaviorTree.Queries.Count > 0)
				{
					var reg = new QueryAssetRegistration();
					foreach (var query in authoring.behaviorTree.Queries)
						reg.Add(query);
					AddSharedComponent(entity, reg);
				}
			}
		}
	}
}

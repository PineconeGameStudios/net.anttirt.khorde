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
		public bool trace;

		class Baker : Baker<BehaviorTreeAuthoring>
		{
			public override void Bake(BehaviorTreeAuthoring authoring)
			{
				DependsOn(authoring.behaviorTree);

				if(authoring.behaviorTree == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				var dataReference = authoring.behaviorTree.LoadPersistent(BTData.SchemaVersion).Reference;
				AddBlobAsset(ref dataReference, out _);

				AddSharedComponent(entity, new BehaviorTree
				{
					tree = dataReference,
				});

				AddBuffer<BTThread>(entity);
				AddBuffer<BTStackFrame>(entity);

				if(authoring.trace)
					AddBuffer<BTExecTrace>(entity);

				var blackboard = AddBuffer<ExpressionBlackboardStorage>(entity);

				ref var exprData = ref authoring.behaviorTree.GetValue(BTData.SchemaVersion).exprData;

				{
					var exprDatas = new List<(Hash128, Ptr<BlobExpressionData>, string)>();
					var assetLookup = new Dictionary<Hash128, BlobAssetBase>();
					exprDatas.Add((authoring.behaviorTree.DataHash, new Ptr<BlobExpressionData>(ref exprData), authoring.behaviorTree.name));
					assetLookup[authoring.behaviorTree.DataHash] = authoring.behaviorTree;

					foreach(var query in authoring.behaviorTree.Queries)
					{
						exprDatas.Add((query.DataHash, new Ptr<BlobExpressionData>(ref query.GetValue(QSData.SchemaVersion).exprData), query.name));
						assetLookup[query.DataHash] = query;
					}

					var layout = ExprAuthoring.ComputeLayout(exprDatas);

					//foreach(var (asset, layoutVariables) in layout)
					//{
					//	Debug.Log($"{assetLookup[asset]} blackboard layout:\n" + string.Join('\n', layoutVariables.Select(lv => $"{lv.name}: {lv.offset}+{lv.length} (global:{lv.isGlobal})")));
					//}

					var baked = ExprAuthoring.BakeLayout(layout, Allocator.Persistent);
					AddBlobAsset(ref baked, out var _);
					AddSharedComponent(entity, new ExpressionBlackboardLayouts() { asset = baked, });

					blackboard.Resize(baked.Value.ComputeStorageLength<ExpressionBlackboardStorage>(), NativeArrayOptions.ClearMemory);

					ExprAuthoring.InitializeBlackboard(blackboard.AsNativeArray(), layout);
				}

				AddComponent(entity, new BTState { });

				if(authoring.behaviorTree.Queries.Count > 0)
				{
					var reg = new QueryAssetRegistration();
					foreach(var query in authoring.behaviorTree.Queries)
					{
						var queryDataReference = query.LoadPersistent(QSData.SchemaVersion).Reference;
						AddBlobAsset(ref queryDataReference, out _);
						reg.Add(queryDataReference);

						foreach(var eq in query.entityQueries)
						{
							var eqDataReference = eq.LoadPersistent(BlobEntityQueryDesc.SchemaVersion).Reference;
							AddBlobAsset(ref eqDataReference, out _);
							reg.Add(eqDataReference);
						}
					}
					AddSharedComponent(entity, reg);
					AddComponent(entity, new PendingQuery());
					SetComponentEnabled<PendingQuery>(entity, false);
					AddBuffer<QSResultItemStorage>(entity);
				}
			}
		}
	}
}

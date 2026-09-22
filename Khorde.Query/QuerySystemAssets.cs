using Khorde.Blobs;
using Khorde.Entities;
using Khorde.Expr;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Khorde.Query
{
	/// <summary>
	/// Singleton for preparing assets used by the query system. All assets must be registered before they can be used.
	/// </summary>
	public struct QuerySystemAssets : IComponentData, IDisposable
	{
		public NativeHashMap<BlobAssetReference<BlobEntityQueryDesc>, EntityQueryMetaData> entityQueries;
		public NativeHashMap<BlobAssetReference<QSData>, QueryMetaData> queryGraphs;
		public int epoch;

		public struct EntityQueryMetaData
		{
			public EntityQuery query;
			public Hash128 hash;
			public int epoch;

			public void Dispose()
			{
			}
		}

		public struct QueryMetaData : IDisposable
		{
			public NativeList<ExprSystemTypeHandleHolder> typeHandles;
			public NativeList<ExprSystemComponentLookupHolder> lookups;
			public EntityQuery jobQuery;
			public int epoch;

			public void Dispose()
			{
				typeHandles.Dispose();
				lookups.Dispose();
			}
		}

		public QuerySystemAssets(Allocator allocator)
		{
			entityQueries = new(0, allocator);
			queryGraphs = new(0, allocator);
			epoch = 1;
		}

		public void Update(ref SystemState state, NativeList<QueryAssetRegistration> regs)
		{
			++epoch;

			foreach(var queryAssetRegistration in regs)
			{
				foreach(var asset in queryAssetRegistration.Assets)
				{
					if(!queryGraphs.TryAdd(asset, new() { epoch = epoch }))
					{
						var temp = queryGraphs[asset];
						temp.epoch = epoch;
						queryGraphs[asset] = temp;
					}
				}

				foreach(var asset in queryAssetRegistration.EntityQueryAssets)
				{
					if(!entityQueries.TryAdd(asset, new() { epoch = epoch }))
					{
						var temp = entityQueries[asset];
						temp.epoch = epoch;
						entityQueries[asset] = temp;
					}
				}
			}

			NativeList<BlobAssetReference<QSData>> staleQueryGraphs = default;

			foreach(var kv in queryGraphs)
			{
				if(kv.Value.epoch != epoch)
				{
					if(!staleQueryGraphs.IsCreated)
						staleQueryGraphs = new(1, Allocator.Temp);

					staleQueryGraphs.Add(kv.Key);
				}
			}

			if(staleQueryGraphs.IsCreated)
			{
				foreach(var key in staleQueryGraphs)
				{
					queryGraphs[key].Dispose();
					queryGraphs.Remove(key);
				}
			}

			NativeList<BlobAssetReference<BlobEntityQueryDesc>> staleEntityQueries = default;

			foreach(var kv in entityQueries)
			{
				if(kv.Value.epoch != epoch)
				{
					if(!staleEntityQueries.IsCreated)
						staleEntityQueries = new(1, Allocator.Temp);

					staleEntityQueries.Add(kv.Key);
				}
			}

			if(staleEntityQueries.IsCreated)
			{
				foreach(var key in staleEntityQueries)
				{
					entityQueries[key].Dispose();
					entityQueries.Remove(key);
				}
			}

			foreach(var query in entityQueries)
			{
				if(query.Value.query == default)
				{
					query.Value.query = query.Key.Value.CreateQuery(state.EntityManager);
					query.Value.hash = query.Key.GetHash();
				}
			}

			NativeList<BlobAssetReference<QSData>> failures = default;

			foreach(var pair in queryGraphs)
			{
				ref var holder = ref pair.Value;

				if(holder.typeHandles.IsCreated)
				{
					foreach(ref var typeHandle in holder.typeHandles.AsArray().AsSpan())
						typeHandle.typeHandle.Update(ref state);

					foreach(ref var lookup in holder.lookups.AsArray().AsSpan())
						lookup.componentLookup.Update(ref state);
				}
				else
				{
					holder.typeHandles = new(Allocator.Persistent);
					holder.lookups = new(Allocator.Persistent);
					ref var data = ref pair.Key.Value;

					var builder = new EntityQueryBuilder(Allocator.Temp);

					var instanceComponents = new NativeList<ComponentType>(Allocator.Temp)
					{
						ComponentType.ReadWrite<QSResultItemStorage>(),
						ComponentType.ReadWrite<PendingQuery>(),
					};

					if(!ExpressionSystemUtility.TryAddQueriesAndComponents(ref state, ref data.exprData,
							ref holder.typeHandles, ref holder.lookups, instanceComponents))
					{
						if(!failures.IsCreated)
							failures = new(1, Allocator.Temp);

						UnityEngine.Debug.LogError($"Failed to register Query Graph asset {pair.Key.GetHash().ToStringBurst()}");
						failures.Add(pair.Key);

						continue;
					}

					builder.WithAll(ref instanceComponents);

					holder.jobQuery = state.GetEntityQuery(builder);
					//holder.jobQuery = builder.Build(state.EntityManager);

					FixedString128Bytes name = default;
					if(pair.Key.Value.exprData.assetName.Length > 0)
						pair.Key.Value.exprData.assetName.CopyTo(ref name);
					UnityEngine.Debug.Log($"Created queries / components for Query Graph asset {name} ({pair.Key.GetHash().ToStringBurst()})");
				}
			}

			if(failures.IsCreated)
			{
				foreach(var failure in failures)
				{
					queryGraphs.Remove(failure);
				}
			}
		}

		public void Dispose()
		{
			foreach(var pair in entityQueries)
				pair.Value.Dispose();
			entityQueries.Dispose();
			foreach(var pair in queryGraphs)
				pair.Value.Dispose();
			queryGraphs.Dispose();
		}
	}

	/// <summary>
	/// All assets referenced by <see cref="QueryAssetRegistration"/> are
	/// automatically registered and usable with <see cref="QuerySystem"/>.
	/// Alternatively, assets must be registered using
	/// <see cref="SystemAPI.GetSingleton{QuerySystemAssets}"/> before use.
	/// </summary>
	public struct QueryAssetRegistration : ISharedComponentData
	{
		BlobAssetReference<QSData> asset0;
		BlobAssetReference<QSData> asset1;
		BlobAssetReference<QSData> asset2;
		BlobAssetReference<QSData> asset3;
		BlobAssetReference<QSData> asset4;
		BlobAssetReference<QSData> asset5;
		BlobAssetReference<QSData> asset6;
		BlobAssetReference<QSData> asset7;

		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset0;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset1;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset2;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset3;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset4;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset5;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset6;
		BlobAssetReference<BlobEntityQueryDesc> entityQueryAsset7;

		private int queryCount;
		private int entityQueryCount;

		public const int Capacity = 8;

		unsafe BlobAssetReference<QSData>* GetQueryData()
		{
			fixed(BlobAssetReference<QSData>* ptr = &asset0)
				return ptr;
		}

		unsafe BlobAssetReference<BlobEntityQueryDesc>* GetEntityQueryData()
		{
			fixed(BlobAssetReference<BlobEntityQueryDesc>* ptr = &entityQueryAsset0)
				return ptr;
		}

		public int QueryCount => queryCount;
		public int EntityQueryCount => entityQueryCount;

		public unsafe void Add(BlobAssetReference<QSData> asset)
		{
			var data = GetQueryData();

			if(queryCount < Capacity)
				data[queryCount++] = asset;
			else
				throw new InvalidOperationException("max supported queries reached");
		}

		public unsafe void Add(BlobAssetReference<BlobEntityQueryDesc> asset)
		{
			var data = GetEntityQueryData();

			if(entityQueryCount < Capacity)
				data[entityQueryCount++] = asset;
			else
				throw new InvalidOperationException("max supported queries reached");
		}

		public unsafe NativeArray<BlobAssetReference<QSData>> Assets
		{
			get
			{
				var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BlobAssetReference<QSData>>(
					GetQueryData(), QueryCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return result;
			}
		}

		public unsafe NativeArray<BlobAssetReference<BlobEntityQueryDesc>> EntityQueryAssets
		{
			get
			{
				var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<BlobAssetReference<BlobEntityQueryDesc>>(
					GetEntityQueryData(), EntityQueryCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return result;
			}
		}
	}

	/// <summary>
	/// Create an entity with this tag component in the world to allow the <see
	/// cref="QueryDebugSystem"/> to run. The system creates the tag
	/// automatically when running in the editor.
	/// </summary>
	public struct QueryDebugEnable : IComponentData { }
}
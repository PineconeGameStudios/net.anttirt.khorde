using Khorde.Blobs;
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

		public struct EntityQueryMetaData
		{
			public EntityQuery query;
			public Hash128 hash;
		}

		public struct QueryMetaData : IDisposable
		{
			public NativeList<ExprSystemTypeHandleHolder> typeHandles;
			public NativeList<ExprSystemComponentLookupHolder> lookups;
			public EntityQuery jobQuery;

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
		}

		/// <summary>
		/// Register a query graph asset.
		/// </summary>
		/// <param name="queryGraph"></param>
		public void Register(BlobAssetReference<QSData> queryGraph)
		{
			queryGraphs.TryAdd(queryGraph, default);
		}

		/// <summary>
		/// Register an entity query asset.
		/// </summary>
		/// <param name="queryGraph"></param>
		public void Register(BlobAssetReference<BlobEntityQueryDesc> entityQuery)
		{
			entityQueries.TryAdd(entityQuery, default);
		}

		public void Update(ref SystemState state, NativeList<QueryAssetRegistration> regs)
		{
			foreach(var queryAssetRegistration in regs)
			{
				foreach(var asset in queryAssetRegistration.Assets)
				{
					if(!queryGraphs.ContainsKey(asset))
					{
						Register(asset);
					}
				}

				foreach(var asset in queryAssetRegistration.EntityQueryAssets)
				{
					if(!entityQueries.ContainsKey(asset))
					{
						Register(asset);
					}
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

						failures.Add(pair.Key);

						continue;
					}

					builder.WithAll(ref instanceComponents);

					holder.jobQuery = state.GetEntityQuery(builder);
					//holder.jobQuery = builder.Build(state.EntityManager);
				}
			}

			if(failures.IsCreated)
			{
				foreach(var failure in failures)
					queryGraphs.Remove(failure);
			}
		}

		public void Dispose()
		{
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

		public unsafe int QueryCount
		{
			get
			{
				var data = GetQueryData();

				for(int i = 0; i < Capacity; ++i)
					if(data[i] == default)
						return i;

				return Capacity;
			}
		}

		public unsafe int EntityQueryCount
		{
			get
			{
				var data = GetEntityQueryData();

				for(int i = 0; i < Capacity; ++i)
					if(data[i] == default)
						return i;

				return Capacity;
			}
		}

		public unsafe void Add(BlobAssetReference<QSData> asset)
		{
			var data = GetQueryData();

			int length = QueryCount;
			if(length < Capacity)
				data[length] = asset;
			else
				throw new InvalidOperationException("max supported queries reached");
		}

		public unsafe void Add(BlobAssetReference<BlobEntityQueryDesc> asset)
		{
			var data = GetEntityQueryData();

			int length = EntityQueryCount;
			if(length < Capacity)
				data[length] = asset;
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

}
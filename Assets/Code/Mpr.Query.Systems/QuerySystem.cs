using Mpr.Expr;
using System;
using Mpr.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Mpr.Query
{
	public partial struct QuerySystem : ISystem
	{
		// 1. query per EntityQueryAsset, keyed by Hash128
		// 2. query per QSData, keyed by ???
		//	-> new asset type for QSData that can also be keyed
		// 3. 
		
		/// <summary>
		/// Results for entity queries used in query generators
		/// </summary>
		private NativeHashMap<Hash128, NativeList<Entity>> entityQueryResultLookup;
		
		/// <summary>
		/// Query requests queue
		/// </summary>
		private NativeQueue<QueryQueueEntry> incoming;

		void ISystem.OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Query>();
			entityQueryResultLookup = new NativeHashMap<Hash128, NativeList<Entity>>(1, Allocator.Persistent);
			state.EntityManager.AddComponentData(state.SystemHandle, new QueryQueue
			{
				queue = this.incoming = new NativeQueue<QueryQueueEntry>(Allocator.Persistent),
			});
			
			state.AddDependency<QueryQueue>(isReadOnly: false);
		}

		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			state.EntityManager.GetAllUniqueSharedComponents<QSEntityQuery>(out var components, Allocator.Temp);

			entityQueryResultLookup.Clear();

			foreach(ref var component in components.AsArray().AsSpan())
			{
				if(component.runtimeEntityQuery == default && component.queryDesc.IsValid())
				{
					component.runtimeEntityQuery = component.queryDesc
						.GetValue(BlobEntityQueryDesc.SchemaVersion)
						.CreateQuery(state.EntityManager);
				}

				if(component.runtimeEntityQuery != default)
				{
					component.results = component.runtimeEntityQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var dep);
					state.Dependency = dep;
					entityQueryResultLookup[component.hash] = component.results;
				}
			}

			// TODO: jobify this somehow
			state.EntityManager.CompleteDependencyBeforeRW<QueryQueue>();
			var entries = incoming.ToArray(state.WorldUpdateAllocator);
			entries.Sort(default(QueryQueueEntry.Comparer));
			int start = 0;
			for (int i = 0; i < entries.Length; i++)
			{
				int end = i + 1;
				if (i == entries.Length - 1 || entries[end].query != entries[i].query)
				{
					// run queries for [start..end)
					var slice = entries.GetSubArray(start, end - start);
					var query = slice[0].query;
					
					// update start
					start = end;
				}
			}
		}

		void ISystem.OnDestroy(ref SystemState state)
		{
			entityQueryResultLookup.Dispose();
			incoming.Dispose();
		}
	}
}

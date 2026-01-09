using System;
using System.Collections.Generic;
using Mpr.Blobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Query
{
	public struct Query : ISharedComponentData
	{
		public BlobAssetReference<QSData> query;
	}

	public struct QueryState : IComponentData, IEnableableComponent
	{
		public State state;
		
		public enum State
		{
			None,
			Running,
			Finished,
		}
	}

	public struct QueryQueueEntry
	{
		public UnityObjectRef<QueryGraphAsset> query;
		
		// results are stored as a DynamicBuffer<QSResultItemStorage> on the querier
		public Entity querier;

		public struct Comparer : IComparer<QueryQueueEntry>
		{
			public int Compare(QueryQueueEntry x, QueryQueueEntry y)
			{
				// TODO: fix this when UnityObjectRef switches to EntityId
				var xid = UnsafeUtility.As<UnityObjectRef<QueryGraphAsset>, int>(ref x.query);
				var yid = UnsafeUtility.As<UnityObjectRef<QueryGraphAsset>, int>(ref y.query);
				
				int c = xid.CompareTo(yid);
				if(c != 0)
					return c;
				return x.querier.CompareTo(y.querier);
			}
		}
	}

	/// <summary>
	/// Query queue singleton. Use a parallel writer on the queue
	/// to enqueue queries. Results are stored on a
	/// <see cref="DynamicBuffer{QSResultItemStorage}"/> on the
	/// querier entity
	/// </summary>
	public struct QueryQueue : IComponentData
	{
		public NativeQueue<QueryQueueEntry> queue;
	}
}
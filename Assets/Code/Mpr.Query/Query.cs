using System;
using System.Collections.Generic;
using Mpr.Blobs;
using Unity.Collections;
using Unity.Entities;

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
		public BlobAssetReference<QSData> query;
		
		// results are stored as a DynamicBuffer<QSResultItemStorage> on the querier
		public Entity querier;

		public struct Comparer : IComparer<QueryQueueEntry>
		{
			public int Compare(QueryQueueEntry x, QueryQueueEntry y)
			{
				unsafe
				{
					if (x.query.GetUnsafePtr() < y.query.GetUnsafePtr())
						return -1;
					if (x.query.GetUnsafePtr() > y.query.GetUnsafePtr())
						return 1;
					return x.querier.CompareTo(y.querier);
				}
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
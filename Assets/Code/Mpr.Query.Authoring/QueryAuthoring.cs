using Mpr.Query.Authoring;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Query
{
	public class QueryAuthoring : MonoBehaviour
	{
		public QueryGraphAsset queryGraph;

		class Baker : Baker<QueryAuthoring>
		{
			public override void Bake(QueryAuthoring authoring)
			{
				DependsOn(authoring.queryGraph);

				if(authoring.queryGraph == null)
					return;

				var entity = GetEntity(authoring, TransformUsageFlags.None);

				var query = authoring.queryGraph.LoadPersistent();
				AddBlobAsset(ref query, out _);

				AddSharedComponent(entity, new Query
				{
					query = query,
				});
			}
		}
	}
}

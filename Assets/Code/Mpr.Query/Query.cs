using Unity.Entities;

namespace Mpr.Query
{
	public struct Query : ISharedComponentData
	{
		public BlobAssetReference<QSData> query;
	}
}
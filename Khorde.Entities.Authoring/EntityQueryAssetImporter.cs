using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Khorde.Blobs.Authoring
{
	[ScriptedImporter(BlobEntityQueryDesc.SchemaVersion, "entityquery", importQueueOffset: 1)]
	public class EntityQueryAssetImporter : ScriptedImporter
	{
		public override void OnImportAsset(AssetImportContext ctx)
		{
			var lines = File.ReadAllText(ctx.assetPath);
			var builder = new BlobBuilder(Allocator.Temp);
			ref var query = ref builder.ConstructRoot<BlobEntityQueryDesc>();
			query.Bake(Path.GetFileName(ctx.assetPath), lines, ref builder, err => ctx.LogImportError(err));
			var obj = ScriptableObject.CreateInstance<EntityQueryAsset>();
			var data = obj.SetAssetData(builder, BlobEntityQueryDesc.SchemaVersion);
			ctx.AddObjectToAsset("asset", obj);
			ctx.AddObjectToAsset("data", data);
			ctx.SetMainObject(obj);
		}
	}
}

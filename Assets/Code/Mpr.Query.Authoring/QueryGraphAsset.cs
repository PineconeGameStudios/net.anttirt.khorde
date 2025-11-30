using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	public class QueryGraphAsset : ScriptableObject
	{
		public TextAsset bakedQuery;

		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/QueryGraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}

		public BlobAssetReference<QSData> LoadPersistent()
		{
			var data = bakedQuery.GetData<byte>();
			MemoryBinaryReader reader;
			unsafe
			{
				reader = new MemoryBinaryReader((byte*)data.GetUnsafePtr(), data.Length);
			}

			if(!BlobAssetReference<QSData>.TryRead(reader, 0, out var result))
				throw new System.Exception("failed to load query graph blob from imported binary asset");

			return result;
		}
	}
}

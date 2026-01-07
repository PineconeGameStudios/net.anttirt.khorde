using System.Collections.Generic;
using Mpr.Blobs;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	public class QueryGraphAsset : BlobAsset<QSData>
	{
		public List<EntityQueryAsset> entityQueries;
		
#if UNITY_EDITOR
		private void OnEnable()
		{
			var icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/QueryGraph.psd");
			if(icon != null)
				UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
		}
#endif
	}
}

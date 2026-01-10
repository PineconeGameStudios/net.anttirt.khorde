using System.Collections.Generic;
using Mpr.Blobs;
using Mpr.Query;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeAsset : BlobAsset<BTData>
	{
		public List<QueryGraphAsset> Queries { get; private set; } = new();
		
		private void OnEnable()
		{
			#if UNITY_EDITOR
			var icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/BehaviorGraph.psd");
			if(icon != null)
				UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
			#endif
		}
	}
}
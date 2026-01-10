using Mpr.Blobs;
using UnityEditor;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeAsset : BlobAsset<BTData>
	{
		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/BehaviorGraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}
	}
}
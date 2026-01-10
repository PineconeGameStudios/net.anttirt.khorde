using Mpr.Blobs;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeAsset : BlobAsset<BTData>
	{
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
using UnityEditor;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeSubgraphAsset : ScriptableObject
	{
		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/BehaviorSubgraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}
	}
}

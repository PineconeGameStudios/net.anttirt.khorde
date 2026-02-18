using UnityEditor;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeSubgraphAsset : ScriptableObject
	{
		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/net.anttirt.khord/Icons/BehaviorSubgraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}
	}
}

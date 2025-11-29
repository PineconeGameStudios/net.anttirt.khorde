using UnityEditor;
using UnityEngine;

namespace Mpr.Query.Authoring
{
	public class QueryGraphAsset : ScriptableObject
	{
		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/QueryGraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}
	}
}

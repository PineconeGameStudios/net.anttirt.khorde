using UnityEditor;
using UnityEngine;

namespace Mpr.Expr.Authoring
{
	public class ExprSubgraphAsset : ScriptableObject
	{
		private void OnEnable()
		{
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/net.anttirt.khord/Icons/ExprSubgraph.psd");
			if(icon != null)
				EditorGUIUtility.SetIconForObject(this, icon);
		}
	}
}

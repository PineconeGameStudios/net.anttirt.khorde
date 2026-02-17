using UnityEngine;
using UnityEngine.UIElements;

namespace Mpr.Behavior
{
	[CreateAssetMenu(menuName = "Behavior Tree/Internal/Graph Assets", fileName = "BTGraphAssets")]
	public class BTGraphAssets : ScriptableObject
	{
		public StyleSheet executionHighlightStyle;
	}
}
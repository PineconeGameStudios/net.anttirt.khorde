using Unity.Entities;
using Unity.GraphToolkit.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Khorde.Expr.Authoring
{
	[DataTypeStyleMapper(typeof(ExprSubgraph))]
	public class EntitiesDataStyleMapper : DataTypeStyleMapper
	{
		public EntitiesDataStyleMapper()
		{
			Register(typeof(Entity), EditorGUIUtility.IconContent("Packages/net.anttirt.khorde/Icons/Entity@4x.png").image as Texture2D, new Color(0.7686275f, 0.7686275f, 0.7686275f));
			Register(typeof(int2), EditorGUIUtility.IconContent("Packages/net.anttirt.khorde/Icons/IntVector2@4x.png").image as Texture2D, new Color(0.07843138f, 0.827451f, 0.4078431f));
			Register(typeof(int3), EditorGUIUtility.IconContent("Packages/net.anttirt.khorde/Icons/IntVector3@4x.png").image as Texture2D, new Color(1, 0.9176471f, 0.02745098f));
			Register(typeof(int4), EditorGUIUtility.IconContent("Packages/net.anttirt.khorde/Icons/IntVector4@4x.png").image as Texture2D, new Color(0.9058824f, 0.5529412f, 0.8627451f));
		}
	}
}
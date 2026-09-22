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
			RegisterAll(this);
		}

		public static Texture2D GetIcon(string filename)
		{
			return EditorGUIUtility.IconContent($"Packages/net.anttirt.khorde/Icons/{filename}").image as Texture2D;
		}

		public static void RegisterAll(DataTypeStyleMapper mapper)
		{
			mapper.Register(typeof(Entity), GetIcon("Entity@4x.png"), new Color(0.7686275f, 0.7686275f, 0.7686275f));
			mapper.Register(typeof(int2), GetIcon("IntVector2@4x.png"), new Color(0.07843138f, 0.827451f, 0.4078431f));
			mapper.Register(typeof(int3), GetIcon("IntVector3@4x.png"), new Color(1, 0.9176471f, 0.02745098f));
			mapper.Register(typeof(int4), GetIcon("IntVector4@4x.png"), new Color(0.9058824f, 0.5529412f, 0.8627451f));
		}
	}
}
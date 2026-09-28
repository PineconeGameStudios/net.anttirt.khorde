using Unity.Entities;
using UnityEditor.AssetImporters;

namespace Khorde.Entities.Editor
{
	public class TypeDependencyCacheExt
	{
		public static void AddComponentTypeDependency(AssetImportContext ctx, ComponentType type)
		{
			Unity.Scenes.Editor.TypeDependencyCache.AddComponentTypeDependency(ctx, type);
		}
	}
}
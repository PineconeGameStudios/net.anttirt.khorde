using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
	public static class EntitySelection
	{
		public static bool TryGetActiveEntity(out Entity entity, out World world)
		{
			var proxy = (Selection.activeObject as EntitySelectionProxy) ?? (Selection.activeContext as EntitySelectionProxy);
			if(proxy != null)
			{
				entity = proxy.Entity;
				world = proxy.World;
				return true;
			}

			entity = default;
			world = default;

			return false;
		}

		public static NativeList<Entity> GetActiveEntities(World world, AllocatorManager.AllocatorHandle allocator)
		{
			var result = new NativeList<Entity>(allocator);

			foreach(var obj in Selection.objects)
				if(obj is EntitySelectionProxy proxy && proxy.World == world)
					result.Add(proxy.Entity);

			return result;
		}
	}
}

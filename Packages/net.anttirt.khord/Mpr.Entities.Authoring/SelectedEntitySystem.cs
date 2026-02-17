using Unity.Entities;

namespace Mpr.Entities.Authoring
{
	[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
	partial class SelectedEntitySystem : SystemBase
	{
		protected override void OnUpdate()
		{
			if(Unity.Entities.Editor.EntitySelection.TryGetActiveEntity(out var entity, out var world))
			{
				SelectedEntity.Value = entity;
			}
			else
			{
				SelectedEntity.Value = default;
			}
		}
	}
}
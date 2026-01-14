using Unity.Entities;

namespace Mpr.Entities.Authoring
{
	[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
	partial class CurrentEntitySystem : SystemBase
	{
		protected override void OnUpdate()
		{
			if(Unity.Entities.Editor.EntitySelection.TryGetActiveEntity(out var entity, out var world))
			{
				CurrentEntity.Value = entity;
			}
			else
			{
				CurrentEntity.Value = default;
			}
		}
	}
}
using Unity.Burst;
using Unity.Entities;

namespace Mpr.Entities
{
	/// <summary>
	/// Patches entity and asset references in rich blobs after loading. See <see cref="RichBlob{TBlob}"/> and <see cref="RichBlobBakingContext"/>.
	/// </summary>
	/// <remarks>
	/// Updated right after other systems in <see
	/// cref="Unity.Scenes.SceneSystemGroup"/> so we can patch blobs as soon as
	/// they're loaded before other systems can access them.
	/// <para/>
	/// Patching is world-specific, and built-in entity remapping can't touch
	/// the insides of blobs, so the blobs have to be re-patched when moved to
	/// a new world.
	/// </remarks>
	[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
	[UpdateInGroup(typeof(Unity.Scenes.SceneSystemGroup), OrderLast = true)]
	public partial struct PatchRichBlobSystem : ISystem
	{
		[BurstCompile]
		void ISystem.OnUpdate(ref SystemState state)
		{
			// TODO: this could possibly be optimized with a shared component
			// storing the current world sequence

			// TODO: can multiple active worlds reference the same blob asset simultaneously?

			foreach(var (data, entities, objRefs, entity) in SystemAPI.Query<
				PatchableRichBlob,
				DynamicBuffer<RichBlobEntityHolder>,
				DynamicBuffer<RichBlobReferenceHolder>
				>().WithEntityAccess())
			{
				data.Asset.Reinterpret<UntypedRichBlobPatchData>().Value
					.Patch(entities, objRefs, state.WorldUnmanaged.SequenceNumber);
			}
		}
	}
}

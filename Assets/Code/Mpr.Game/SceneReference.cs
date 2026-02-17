using Unity.Entities;
using Unity.Entities.Content;

namespace Mpr.Game;

public struct SceneReference : IBufferElementData
{
	public WeakObjectSceneReference reference;
}
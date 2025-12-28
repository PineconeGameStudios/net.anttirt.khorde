using Unity.Entities;

namespace Mpr.Net
{
	public static class WorldSystemFilterExt
	{
		/// <summary>
		/// Worlds with authority over game state. Combines <see cref="WorldSystemFilterFlags.ServerSimulation"/> and <see cref="WorldSystemFilterFlags.LocalSimulation"/>.
		/// </summary>
		public const WorldSystemFilterFlags Authority = WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation;
	}
}
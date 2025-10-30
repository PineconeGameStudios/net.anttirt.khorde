using Unity.NetCode;
using UnityEngine.Scripting;

namespace Mpr.Net
{
	[Preserve]
	public class GameBootstrap : ClientServerBootstrap
	{
		public override bool Initialize(string defaultWorldName)
		{
			AutoConnectPort = 7979;
			return base.Initialize(defaultWorldName);
		}
	}
}

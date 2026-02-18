using UnityEditor;

namespace Khorde.Editor
{
	static class ReloadDomainMenuItem
	{
		const string kReloadDomain = "Khorde/Reload Domain";

		[MenuItem(kReloadDomain)]
		public static void ReloadDomain()
		{
			EditorUtility.RequestScriptReload();
		}
	}
}
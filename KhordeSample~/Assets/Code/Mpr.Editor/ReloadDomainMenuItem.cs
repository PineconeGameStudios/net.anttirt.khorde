using UnityEditor;

namespace Mpr.Editor
{
	static class ReloadDomainMenuItem
	{
		const string kReloadDomain = "Mpr/Reload Domain";

		[MenuItem(kReloadDomain)]
		public static void ReloadDomain()
		{
			EditorUtility.RequestScriptReload();
		}
	}
}
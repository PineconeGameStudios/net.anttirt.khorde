using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Khorde.Editor
{
	public class EditorTestAutoRunner
	{
		const string kAutorunOnCompile = "Khorde/Autorun Tests on Compile";

		[MenuItem(kAutorunOnCompile)]
		public static void ToggleAutorunOnCompile()
		{
			bool value = EditorPrefs.GetBool(kAutorunOnCompile);
			value = !value;
			EditorPrefs.SetBool(kAutorunOnCompile, value);
			Menu.SetChecked(kAutorunOnCompile, value);
		}

		[InitializeOnLoadMethod]
		static void ListenForDomainReload()
		{
			AssemblyReloadEvents.afterAssemblyReload -= AssemblyReloadEvents_afterAssemblyReload;
			AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
		}

		private static void AssemblyReloadEvents_afterAssemblyReload()
		{
			if(!Application.isPlaying && EditorPrefs.GetBool(kAutorunOnCompile))
				ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings { filters = new Filter[] { new Filter { testMode = TestMode.EditMode } } });
		}
	}
}

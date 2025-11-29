using UnityEditor;

namespace Mpr.Editor
{
	class CSProject : AssetPostprocessor
	{
		private static string OnGeneratedCSProject(string path, string content)
		{
			return content.Replace("<LangVersion>9.0</LangVersion>", "<LangVersion>10.0</LangVersion>");
		}
	}
}
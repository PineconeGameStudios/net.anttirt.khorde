using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mpr.Game.UI
{
	class SceneButton : MonoBehaviour
	{
		public string sceneName;

		public void OnClick()
		{
			SceneManager.LoadScene(sceneName);
		}
	}
}
using Unity.Entities.Content;
using UnityEngine;

namespace Mpr.Game.UI
{
	class SceneButton : MonoBehaviour
	{
		public WeakObjectSceneReference scene;

		public void OnClick()
		{
			scene.LoadAsync(new Unity.Loading.ContentSceneParameters { loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single, autoIntegrate = true });
		}
	}
}
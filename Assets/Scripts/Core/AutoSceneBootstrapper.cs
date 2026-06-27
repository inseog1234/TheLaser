using UI.InGame;
using UI.Title;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public static class AutoSceneBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Bootstrap(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Bootstrap(scene);
        }

        private static void Bootstrap(Scene scene)
        {
            string sceneName = scene.name.ToLowerInvariant();

            if (sceneName.Contains("title") && Object.FindFirstObjectByType<TitleMenuController>() == null)
                new GameObject("TitleMenuController").AddComponent<TitleMenuController>();

            if ((sceneName.Contains("main") || sceneName.Contains("game")) && Object.FindFirstObjectByType<InGameStageFlowController>() == null)
                new GameObject("InGameStageFlowController").AddComponent<InGameStageFlowController>();
        }
    }
}

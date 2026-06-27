using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    public class SceneFadeController : MonoBehaviour
    {
        private static SceneFadeController instance;
        private CanvasGroup canvasGroup;

        public static SceneFadeController Instance
        {
            get
            {
                if (instance == null)
                    CreateInstance();
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (instance != null)
                return;

            GameObject root = new GameObject("SceneFadeController");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SceneFadeController>();
            instance.BuildCanvas();
        }

        private void BuildCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject imageObject = new GameObject("FadeImage");
            imageObject.transform.SetParent(transform, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            canvasGroup = imageObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        public void LoadScene(string sceneName, float fadeTime = 0.45f)
        {
            StartCoroutine(LoadSceneRoutine(sceneName, fadeTime));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, float fadeTime)
        {
            yield return Fade(1f, fadeTime);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone)
                yield return null;
            yield return Fade(0f, fadeTime);
        }


        public void LoadSceneFromCurrentFade(string sceneName, float fadeInTime = 0.45f)
        {
            StartCoroutine(LoadSceneFromCurrentFadeRoutine(sceneName, fadeInTime));
        }

        private IEnumerator LoadSceneFromCurrentFadeRoutine(string sceneName, float fadeInTime)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone)
                yield return null;

            yield return Fade(0f, fadeInTime);
        }

        public IEnumerator Fade(float targetAlpha, float duration)
        {
            if (canvasGroup == null)
                yield break;

            canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
        }
    }
}

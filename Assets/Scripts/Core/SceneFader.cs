using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>Fade-to-black scene transitions.</summary>
    public class SceneFader : MonoBehaviour
    {
        static SceneFader instance;
        Image overlay;
        bool busy;

        static SceneFader Instance
        {
            get
            {
                if (instance != null) return instance;
                var canvas = UIFactory.Canvas("SceneFader", 1000);
                DontDestroyOnLoad(canvas.gameObject);
                instance = canvas.gameObject.AddComponent<SceneFader>();
                instance.overlay = UIFactory.Image(UIFactory.Stretch(canvas.transform, "Overlay"), Color.black);
                instance.overlay.raycastTarget = false;
                return instance;
            }
        }

        public static void FadeIn() => Instance.StartCoroutine(Instance.Fade(1f, 0f, 0.6f));

        public static void LoadScene(string scene)
        {
            if (Instance.busy) return;
            Instance.StartCoroutine(Instance.LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            busy = true;
            overlay.raycastTarget = true;
            yield return Fade(overlay.color.a, 1f, 0.35f);
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(scene);
            overlay.raycastTarget = false;
            busy = false;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            overlay.color = new Color(0f, 0f, 0f, to);
        }
    }
}

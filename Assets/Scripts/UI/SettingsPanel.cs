using UnityEngine;
using UnityEngine.SceneManagement;

namespace MutantPlants
{
    /// <summary>Settings menu shared by the main menu and the pause menu.</summary>
    public static class SettingsPanel
    {
        public static GameObject Build(Transform canvas, System.Action onBack)
        {
            GameSettings.EnsureLoaded();
            var panel = UIFactory.MenuPanel(canvas, "Settings", 680f);
            UIFactory.Label(panel, L.T("settings").ToUpper(), 48, UIFactory.Accent, 80f);

            var startLanguage = L.Current;

            UIFactory.Slider(panel, L.T("sensitivity"), 0.2f, 6f, GameSettings.MouseSensitivity, v => v.ToString("0.0"),
                v => GameSettings.MouseSensitivity = v);
            UIFactory.Slider(panel, L.T("volume"), 0f, 1f, GameSettings.EffectsVolume, v => Mathf.RoundToInt(v * 100) + "%",
                v => { GameSettings.EffectsVolume = v; SoundFX.ApplyVolumes(); });
            UIFactory.Slider(panel, L.T("music"), 0f, 1f, GameSettings.MusicVolume, v => Mathf.RoundToInt(v * 100) + "%",
                v => { GameSettings.MusicVolume = v; SoundFX.ApplyVolumes(); });
            UIFactory.Slider(panel, L.T("fov"), 60f, 100f, GameSettings.FieldOfView, v => Mathf.RoundToInt(v).ToString(),
                v => { GameSettings.FieldOfView = v; if (Camera.main != null) Camera.main.fieldOfView = v; });
            UIFactory.Toggle(panel, L.T("invertY"), GameSettings.InvertY, v => GameSettings.InvertY = v);
            UIFactory.Toggle(panel, L.T("shake"), GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
            UIFactory.Toggle(panel, L.T("fullscreen"), GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);

            UnityEngine.UI.Text langLabel = null;
            var langButton = UIFactory.Button(panel, "", () =>
            {
                L.Current = L.Current == L.Language.English ? L.Language.Hungarian : L.Language.English;
                langLabel.text = $"{L.T("language")}: {L.T("langName")}";
            }, 54f);
            langLabel = langButton.GetComponentInChildren<UnityEngine.UI.Text>();
            langLabel.text = $"{L.T("language")}: {L.T("langName")}";

            UIFactory.Button(panel, L.T("saveBack"), () =>
            {
                GameSettings.Save();
                // UI text is built once, so rebuild the menu when the language changed.
                if (L.Current != startLanguage && GameManager.Instance == null)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                else
                    onBack();
            });

            var root = panel.parent.gameObject;
            root.SetActive(false);
            return root;
        }
    }
}

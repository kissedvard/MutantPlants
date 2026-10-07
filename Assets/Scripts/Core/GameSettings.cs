using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Player-adjustable settings, persisted with PlayerPrefs (GDD: "Beállítások mentése").
    /// </summary>
    public static class GameSettings
    {
        const string KeySensitivity = "settings.sensitivity";
        const string KeyVolume = "settings.volume";
        const string KeyMusic = "settings.music";
        const string KeyFov = "settings.fov";
        const string KeyInvertY = "settings.invertY";
        const string KeyFullscreen = "settings.fullscreen";
        const string KeyShake = "settings.shake";
        const string KeyLanguage = "settings.language";

        public static float MouseSensitivity = 2f;
        public static float EffectsVolume = 0.8f;
        public static float MusicVolume = 0.45f;
        public static float FieldOfView = 75f;
        public static bool InvertY;
        public static bool Fullscreen = true;
        public static bool ScreenShake = true;

        static bool loaded;

        public static void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(KeySensitivity, 2f);
            EffectsVolume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.45f);
            FieldOfView = PlayerPrefs.GetFloat(KeyFov, 75f);
            InvertY = PlayerPrefs.GetInt(KeyInvertY, 0) == 1;
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            ScreenShake = PlayerPrefs.GetInt(KeyShake, 1) == 1;
            L.Current = (L.Language)PlayerPrefs.GetInt(KeyLanguage, 0);
            loaded = true;
            Apply();
        }

        public static void EnsureLoaded()
        {
            if (!loaded) Load();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
            PlayerPrefs.SetFloat(KeyVolume, EffectsVolume);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
            PlayerPrefs.SetFloat(KeyFov, FieldOfView);
            PlayerPrefs.SetInt(KeyInvertY, InvertY ? 1 : 0);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyShake, ScreenShake ? 1 : 0);
            PlayerPrefs.SetInt(KeyLanguage, (int)L.Current);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            SoundFX.ApplyVolumes();
            if (!Application.isEditor) Screen.fullScreen = Fullscreen;
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = FieldOfView;
        }
    }
}

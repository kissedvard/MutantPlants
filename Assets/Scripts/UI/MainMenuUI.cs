using UnityEngine;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>Main menu (GDD: Menü): new game, continue from checkpoint, settings, leaderboard, quit.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        public string gameScene = "Farm";

        GameObject mainPanel, settingsPanel;
        RectTransform title, sun;

        void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.Load();

            var canvas = UIFactory.Canvas("MenuCanvas");
            canvas.transform.SetParent(transform, false);

            // Title: big chunky green letters with a sun peeking out behind.
            sun = UIFactory.Icon(canvas.transform, "Sun", UISkin.SunIcon, UISkin.Sun, new Vector2(0.5f, 1f), new Vector2(560f, -40f), 170f).rectTransform;
            title = UIFactory.At(canvas.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1500f, 180f));
            var titleText = UIFactory.Text(title, L.T("title"), 140, TextAnchor.MiddleCenter, UISkin.Grass, 7f);
            var hl = titleText.gameObject.AddComponent<Outline>();
            hl.effectColor = new Color(0.75f, 1f, 0.45f);
            hl.effectDistance = new Vector2(0f, 2f);
            title.gameObject.AddComponent<PopIn>().duration = 0.9f;

            // Subtitle on a cream ribbon
            var ribbon = UIFactory.At(canvas.transform, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1000f, 64f));
            UIFactory.Sliced(ribbon, UISkin.Rounded, UISkin.Cream, 1.4f);
            UIFactory.Text(ribbon, L.T("subtitle"), 28, TextAnchor.MiddleCenter, UISkin.Outline, 0f);
            ribbon.gameObject.AddComponent<PopIn>().delay = 0.2f;

            // Root that holds the buttons + leaderboard (hidden while settings are open).
            mainPanel = UIFactory.Stretch(canvas.transform, "Main").gameObject;

            // Left: wooden sign with buttons
            var sign = UIFactory.Rect(mainPanel.transform, "ButtonSign", new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(520f, 0f));
            UIFactory.Sliced(sign, UISkin.WoodPanel, Color.white, 1f, true);
            var layout = sign.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 40, 44);
            layout.spacing = 16f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            sign.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sign.gameObject.AddComponent<PopIn>().delay = 0.3f;

            UIFactory.Button(sign, L.T("newGame"), NewGame, 84f);
            var cont = UIFactory.Button(sign, L.T("continue"), Continue, 68f, UISkin.Sky);
            cont.interactable = SaveSystem.HasCheckpoint;
            UIFactory.Button(sign, L.T("settings"), () => { mainPanel.SetActive(false); settingsPanel.SetActive(true); }, 68f, UISkin.Orange);
            UIFactory.Button(sign, L.T("quit"), Quit, 68f, UISkin.Red);

            // Right: leaderboard sign
            var board = UIFactory.Rect(mainPanel.transform, "Leaderboard", new Vector2(0.7f, 0.5f), new Vector2(0.7f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(580f, 360f));
            UIFactory.Sliced(board, UISkin.WoodPanel, Color.white, 1f, true);
            board.gameObject.AddComponent<PopIn>().delay = 0.45f;
            var boardTitle = UIFactory.Rect(board, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(560f, 56f));
            UIFactory.Text(boardTitle, L.T("leaderboard"), 40, TextAnchor.MiddleCenter, UISkin.Sun, 3.5f);
            var boardBody = UIFactory.Rect(board, "Body", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-60f, -110f));
            var boardText = UIFactory.Text(boardBody, GameUI.LeaderboardText(), 30, TextAnchor.MiddleCenter, Color.white, 2.5f);
            boardText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boardText.lineSpacing = 1.15f;

            settingsPanel = SettingsPanel.Build(canvas.transform, () => { settingsPanel.SetActive(false); mainPanel.SetActive(true); });

            var helpRt = UIFactory.At(canvas.transform, "Controls", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1800f, 40f));
            UIFactory.Text(helpRt, L.T("controls"), 21, TextAnchor.MiddleCenter, Color.white, 2f);

            var creditRt = UIFactory.At(canvas.transform, "Credit", new Vector2(1f, 0f), new Vector2(-24f, 70f), new Vector2(600f, 30f));
            UIFactory.Text(creditRt, "Kiss Edvárd • X080OP", 18, TextAnchor.LowerRight, UISkin.Cream, 1.5f);
        }

        void Start()
        {
            SoundFX.PlayMusic(SoundFX.Track.Menu);
            SceneFader.FadeIn();
        }

        void Update()
        {
            // Gentle bob + tilt, like a hand-painted sign swaying.
            float t = Time.time;
            title.anchoredPosition = new Vector2(0f, -70f + Mathf.Sin(t * 1.6f) * 6f);
            title.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 2f);
            sun.localRotation = Quaternion.Euler(0f, 0f, -t * 20f);
        }

        void NewGame()
        {
            SaveSystem.ClearCheckpoint();
            SaveSystem.ContinueFromCheckpoint = false;
            SceneFader.LoadScene(gameScene);
        }

        void Continue()
        {
            SaveSystem.ContinueFromCheckpoint = true;
            SceneFader.LoadScene(gameScene);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

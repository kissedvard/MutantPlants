using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>
    /// Builds the cartoon-styled uGUI from code (see UISkin for the look),
    /// so the UI needs no prefabs or imported fonts.
    /// </summary>
    public static class UIFactory
    {
        public static Color Accent => UISkin.Sun;
        public static Color TextColor => Color.white;
        public static Color PanelColor => UISkin.Wood;

        public static Font Font => UISkin.Font;

        public static Canvas Canvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name) =>
            Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        /// <summary>Anchored at a point (anchor = pivot), handy for HUD elements.</summary>
        public static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) =>
            Rect(parent, name, anchor, anchor, anchor, position, size);

        public static Image Image(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>9-sliced skin sprite. Higher ppu multiplier = thinner border.</summary>
        public static Image Sliced(RectTransform rt, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f, bool tiled = false)
        {
            var img = Image(rt, color);
            img.sprite = sprite;
            img.type = tiled ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            return img;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 position, float size)
        {
            var img = Image(At(parent, name, anchor, position, new Vector2(size, size)), color);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Rounded progress bar with dark outline. Returns the fill image (Filled, horizontal).</summary>
        public static Image Bar(RectTransform rt, Color fill, Color track)
        {
            Sliced(rt, UISkin.RoundedSmall, track, 1.6f).raycastTarget = false;
            var fillRt = Rect(rt, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            var img = Image(fillRt, fill);
            img.sprite = UISkin.RoundedFlat;
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Horizontal fill image (health trail etc.).</summary>
        public static Image FilledImage(RectTransform rt, Color color)
        {
            var img = Image(rt, color);
            img.sprite = UISkin.RoundedFlat;
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Chunky cartoon text: bold, thick dark outline and a drop shadow.</summary>
        public static Text Text(Transform parent, string content, int size, TextAnchor align, Color? color = null, float outline = 3f)
        {
            var rt = Stretch(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontStyle = UISkin.HasCustomFont ? FontStyle.Normal : FontStyle.Bold;
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Chunky(t.gameObject, outline);
            return t;
        }

        public static void Chunky(GameObject go, float outline)
        {
            if (outline <= 0f) return;
            var o1 = go.AddComponent<Outline>();
            o1.effectColor = UISkin.Outline;
            o1.effectDistance = new Vector2(outline, -outline);
            var o2 = go.AddComponent<Outline>();
            o2.effectColor = UISkin.Outline;
            o2.effectDistance = new Vector2(-outline, outline);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -outline * 2f);
        }

        /// <summary>Dimmed backdrop with a centered wooden sign holding a vertical layout.</summary>
        public static RectTransform MenuPanel(Transform parent, string name, float width = 620f)
        {
            var dim = Stretch(parent, name);
            Image(dim, new Color(0.05f, 0.08f, 0.02f, 0.55f));
            var panel = Rect(dim, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0f));
            Sliced(panel, UISkin.WoodPanel, Color.white, 1f, true);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 40, 44);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.gameObject.AddComponent<PopIn>();
            return panel;
        }

        public static Text Label(Transform layoutParent, string content, int size, Color? color = null, float height = 0f)
        {
            var t = Text(layoutParent, content, size, TextAnchor.MiddleCenter, color, size >= 40 ? 4f : 2.5f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height > 0f ? height : size * 1.5f;
            return t;
        }

        /// <summary>Big glossy green cartoon button with bounce.</summary>
        public static Button Button(Transform layoutParent, string label, UnityAction onClick, float height = 68f, Color? color = null)
        {
            var rt = Rect(layoutParent, label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = Sliced(rt, UISkin.Rounded, color ?? UISkin.Grass, 1.3f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.05f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            button.onClick.AddListener(() => SoundFX.Play(SoundFX.Sfx.Click));
            button.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            rt.gameObject.AddComponent<UIBounce>();
            var t = Text(rt, label, 32, TextAnchor.MiddleCenter, Color.white, 3f);
            t.rectTransform.offsetMin = new Vector2(0f, 4f);
            return button;
        }

        public static Slider Slider(Transform layoutParent, string label, float min, float max, float value, System.Func<float, string> format, UnityAction<float> onChange)
        {
            var row = Rect(layoutParent, label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            var text = Text(row, "", 26, TextAnchor.MiddleLeft, UISkin.Cream, 2.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1f);

            var sliderRt = Rect(row, "Slider", new Vector2(0.53f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 30f));
            var bg = Sliced(sliderRt, UISkin.RoundedSmall, new Color(0.25f, 0.16f, 0.08f), 2f);

            var fillArea = Rect(sliderRt, "Fill Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Sliced(fill, UISkin.RoundedFlat, UISkin.Grass, 3f);

            var handleArea = Rect(sliderRt, "Handle Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-30f, 0f));
            var handle = Rect(handleArea, "Handle", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 14f));
            var handleImg = Image(handle, UISkin.Sun);
            handleImg.sprite = UISkin.Circle;
            handleImg.preserveAspect = true;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            bg.raycastTarget = true;

            text.text = $"{label}: {format(value)}";
            slider.onValueChanged.AddListener(v => text.text = $"{label}: {format(v)}");
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        public static Toggle Toggle(Transform layoutParent, string label, bool value, UnityAction<bool> onChange)
        {
            var row = Rect(layoutParent, label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

            var box = Rect(row, "Box", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            var boxImg = Sliced(box, UISkin.RoundedSmall, UISkin.Cream, 1.5f);
            var check = Rect(box, "Check", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), new Vector2(4f, 4f));
            var checkImg = Image(check, UISkin.Grass);
            checkImg.sprite = UISkin.Check;
            checkImg.preserveAspect = true;

            var text = Text(row, label, 26, TextAnchor.MiddleLeft, UISkin.Cream, 2.5f);
            text.rectTransform.offsetMin = new Vector2(60f, 0f);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => SoundFX.Play(SoundFX.Sfx.Click));
            toggle.onValueChanged.AddListener(onChange);
            return toggle;
        }
    }
}

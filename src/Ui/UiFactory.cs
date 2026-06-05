using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Atoms: each method creates exactly one styled uGUI element. Higher-level components compose these;
    /// nothing else news up raw UI objects, so styling stays in one place (the Theme + this factory).
    /// </summary>
    public static class UiFactory
    {
        private static TMP_FontAsset _font;

        /// <summary>A native game button sprite (captured at runtime) so our buttons match the game's look.</summary>
        public static Sprite ButtonSprite;

        /// <summary>
        /// The TMP font used by all labels. Resolved lazily; composition may set it explicitly from a
        /// cloned in-game label for guaranteed-correct material.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                {
                    _font = ResolveFont();
                }
                return _font;
            }
            set => _font = value;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            return all != null && all.Length > 0 ? all[0] : null;
        }

        public static RectTransform Panel(string name, Transform parent, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = background;
            return rt;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (Font != null)
            {
                tmp.font = Font;
            }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        public static Button Button(string name, Transform parent, string text, Theme theme, UnityAction onClick)
        {
            var rt = Panel(name, parent, theme.ButtonIdle);
            var img = rt.gameObject.GetComponent<Image>();
            if (ButtonSprite != null) { img.sprite = ButtonSprite; img.type = Image.Type.Sliced; }
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            var label = Label(name + "_label", rt, text, 14f, theme.Text, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.margin = new Vector4(4f, 0f, 4f, 0f);
            label.enableWordWrapping = false;          // buttons are single-line — never reflow their height
            label.overflowMode = TextOverflowModes.Ellipsis;
            return btn;
        }

        /// <summary>Pin a control to an exact size (atomic sizing) so changing its text never resizes it.</summary>
        public static LayoutElement Fixed(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width; le.flexibleWidth = 0f;
            le.preferredHeight = height; le.minHeight = height; le.flexibleHeight = 0f;
            return le;
        }

        public static VerticalLayoutGroup VerticalLayout(string name, Transform parent, float spacing, RectOffset padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static Image LineImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            ((RectTransform)go.transform).pivot = new Vector2(0f, 0.5f); // pivot at line start
            return img;
        }

        public static LayoutElement PreferredHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return le;
        }

        /// <summary>Anchor a RectTransform to fill its parent.</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchor a fixed-size box to a corner of its parent.</summary>
        public static void AnchorTopLeft(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(offset.x, -offset.y);
        }

        public static HorizontalLayoutGroup HorizontalLayout(string name, Transform parent, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true; // fill the row height, else children collapse to 0px and vanish
            return h;
        }

        private static Sprite _ringSprite;

        /// <summary>A reusable anti-aliased ring sprite (white; tint via Image.color) for range circles.</summary>
        public static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 128;
            float c = (n - 1) / 2f, outer = c, inner = c - 3f; // 3px ring band
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = (d <= outer && d >= inner) ? 1f : 0f;
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return _ringSprite;
        }

        public static Image Ring(string name, Transform parent, Color color, bool dashed = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = dashed ? DashedRingSprite() : RingSprite();
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            ((RectTransform)go.transform).pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        private static Sprite _dashedRingSprite;

        /// <summary>A dashed anti-aliased ring (white; tint via Image.color) — reads as a "range" cue.</summary>
        public static Sprite DashedRingSprite()
        {
            if (_dashedRingSprite != null) return _dashedRingSprite;
            const int n = 128, dashes = 32;
            float c = (n - 1) / 2f, outer = c, inner = c - 4f; // 4px band
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    bool band = d <= outer && d >= inner;
                    float ang = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg + 180f; // dash gaps around circumference
                    bool on = (int)(ang / (360f / dashes)) % 2 == 0;
                    px[y * n + x] = new Color(1f, 1f, 1f, band && on ? 1f : 0f);
                }
            tex.SetPixels(px);
            tex.Apply();
            _dashedRingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return _dashedRingSprite;
        }
    }
}

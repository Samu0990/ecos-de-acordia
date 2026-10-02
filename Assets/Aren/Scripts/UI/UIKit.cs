using UnityEngine;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// Paleta e fábrica da UI (uGUI montada em código — sem prefabs de UI para manter
    /// tudo versionável e ajustável). Direção visual do doc §29: escura, hierarquia
    /// forte, detalhes dourados e identidade musical (ondas, anéis).
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.05f, 0.04f, 0.07f, 0.86f);
        public static readonly Color Gold = new Color(0.93f, 0.77f, 0.46f, 1f);
        public static readonly Color GoldDim = new Color(0.62f, 0.5f, 0.32f, 1f);
        public static readonly Color Bone = new Color(0.95f, 0.92f, 0.86f, 1f);
        public static readonly Color Muted = new Color(0.66f, 0.62f, 0.6f, 1f);
        public static readonly Color Cyan = new Color(0.55f, 0.95f, 1f, 1f);
        public static readonly Color Crimson = new Color(0.86f, 0.18f, 0.28f, 1f);
        public static readonly Color Violet = new Color(0.72f, 0.48f, 1f, 1f);

        static Font serif, serifBold, display, sans, sansBold;
        public static Font Serif => serif ??= Resources.Load<Font>("UI/Fonts/NotoSerif-Regular");
        public static Font SerifBold => serifBold ??= Resources.Load<Font>("UI/Fonts/NotoSerif-Bold");
        public static Font Display => display ??= Resources.Load<Font>("UI/Fonts/NotoSerifDisplay-Regular");
        public static Font Sans => sans ??= Resources.Load<Font>("UI/Fonts/NotoSans-Regular");
        public static Font SansBold => sansBold ??= Resources.Load<Font>("UI/Fonts/NotoSans-Bold");

        static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new System.Collections.Generic.Dictionary<string, Sprite>();
        public static Sprite S(string name)
        {
            if (!sprites.TryGetValue(name, out var s) || s == null)
            {
                s = Resources.Load<Sprite>("UI/Sprites/" + name);
                sprites[name] = s;
            }
            return s;
        }

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Img(string name, Transform parent, string sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size, Image.Type type = Image.Type.Simple)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite != null ? S(sprite) : null;
            im.color = color;
            im.type = type;
            im.raycastTarget = false;
            return im;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 box, bool shadow = true)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.75f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        /// <summary>Espaçamento entre letras simulado (fonte legada não tem tracking).</summary>
        public static string Spaced(string s, int spaces = 1)
        {
            var sb = new System.Text.StringBuilder();
            string pad = new string(' ', spaces);   // espaço fino
            for (int i = 0; i < s.Length; i++) { sb.Append(s[i]); if (i < s.Length - 1) sb.Append(pad); }
            return sb.ToString();
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Runtime uGUI construction helpers for world-space VR panels.
    /// </summary>
    public static class PresentableUi
    {
        public static readonly Color Green = new Color(0.25f, 1f, 0.45f, 1f);
        public static readonly Color GreenDim = new Color(0.12f, 0.55f, 0.28f, 1f);
        public static readonly Color Red = new Color(1f, 0.25f, 0.22f, 1f);
        public static readonly Color RedDim = new Color(0.55f, 0.12f, 0.1f, 1f);
        public static readonly Color Cyan = new Color(0.35f, 0.85f, 1f, 1f);
        public static readonly Color PanelFill = new Color(0.02f, 0.08f, 0.12f, 0.84f);
        public static readonly Color PanelFillRed = new Color(0.18f, 0.02f, 0.02f, 0.88f);

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizePx, float metersPerPixel,
            bool interactive, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            canvas.worldCamera = Camera.main;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * metersPerPixel;

            if (interactive)
            {
                go.AddComponent<GraphicRaycaster>();
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
            }

            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Centered(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced = false,
            bool raycast = false, bool preserveAspect = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            img.preserveAspect = preserveAspect;
            if (sliced && sprite != null && sprite.border.sqrMagnitude > 0f)
                img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Sprite sprite, Color color, bool preserveAspect = false)
        {
            return Img(Rect(name, parent, anchorMin, anchorMax), sprite, color, preserveAspect: preserveAspect);
        }

        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, TMP_FontAsset font = null)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
            bool bold = false, TMP_FontAsset font = null)
        {
            return Text(Rect(name, parent, anchorMin, anchorMax), text, size, color, align, bold, font);
        }

        /// <summary>Image sized from sprite aspect at a given pixel height.</summary>
        public static Image BanglaLine(string name, Transform parent, Sprite sprite, Vector2 anchoredPos,
            float heightPx, Color color)
        {
            var aspect = sprite != null ? sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : 6f;
            var rt = Centered(name, parent, anchoredPos, new Vector2(heightPx * aspect, heightPx));
            return Img(rt, sprite, color, preserveAspect: true);
        }

        public static void SetAlpha(Graphic g, float a)
        {
            if (g == null)
                return;
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}

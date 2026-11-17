using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>Loads the client's HUD / scorecard pictures (Resources/Ui/Hud, Resources/Ui/Score) as sprites, cached.</summary>
    internal static class HudArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <param name="path">Resources path without extension, e.g. "Ui/Hud/02_back_button_frame".</param>
        /// <param name="border">9-slice border in texture pixels (left, bottom, right, top), zero = plain sprite.</param>
        public static Sprite Get(string path, Vector4 border = default)
        {
            if (Cache.TryGetValue(path, out var s) && s != null)
                return s;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError("[HudArt] missing Resources/" + path + ".png");
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            Cache[path] = s;
            return s;
        }

        private static Sprite _pill;

        /// <summary>White capsule, 9-sliced, for bar fills.</summary>
        public static Sprite Pill()
        {
            if (_pill != null)
                return _pill;
            const int n = 64;
            const float r = 31f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                var dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f)));
            }

            tex.Apply();
            _pill = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _pill;
        }

        private static Sprite _glow;

        /// <summary>Soft round glow (alpha falls off smoothly to the edge).</summary>
        public static Sprite Glow()
        {
            if (_glow != null)
                return _glow;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                var a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }

            tex.Apply();
            _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _glow;
        }

        private static readonly Dictionary<string, Sprite> Rings = new Dictionary<string, Sprite>();

        /// <summary>Thin ring made of <paramref name="segments"/> arcs (fill = share of each segment that is lit), soft edges.</summary>
        public static Sprite Ring(int segments, float fill, float thickness01)
        {
            var key = segments + "_" + fill + "_" + thickness01;
            if (Rings.TryGetValue(key, out var s) && s != null)
                return s;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f);
                var d = p.magnitude / (n * 0.5f);
                var band = Mathf.Clamp01(1f - Mathf.Abs(d - (1f - thickness01 * 0.5f - 0.01f)) / (thickness01 * 0.5f));
                var seg = 1f;
                if (segments > 0)
                {
                    var ang = (Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI) + 1f) % 1f * segments;
                    var f = ang - Mathf.Floor(ang);
                    seg = Mathf.Clamp01((fill - f) * 12f + 0.5f) * Mathf.Clamp01(f * 12f + 0.5f);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, band * seg));
            }

            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            Rings[key] = s;
            return s;
        }

        private static Sprite _solid;

        /// <summary>1 px white sprite (tinted by the Image colour) for fills.</summary>
        public static Sprite Solid()
        {
            if (_solid != null)
                return _solid;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (var i = 0; i < px.Length; i++)
                px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return _solid;
        }
    }
}

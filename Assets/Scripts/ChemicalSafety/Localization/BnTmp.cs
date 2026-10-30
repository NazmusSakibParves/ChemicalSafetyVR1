using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Localization
{
    /// <summary>
    /// TextMeshProUGUI that shows Bangla correctly. TextMeshPro cannot shape Bengali (conjuncts, vowel signs), so in Bangla mode
    /// the text is drawn as pre-rendered images (Tools/render_bn_auto.py, Assets/Resources/BnAuto). English text, and every
    /// text when the language is English, goes through the normal TMP path, so English looks exactly as before.
    ///
    /// Code keeps assigning plain strings (`label.text = Sc10Content.QuestionPrompt`). In Bangla mode the string is either already
    /// Bangla (content JSON) or an English UI literal that is looked up in the content JSON (Loc.Translate). A text with several
    /// lines that has no image as a whole (e.g. a label plus a smaller sub line) is drawn line by line.
    /// </summary>
    public class BnTmp : TextMeshProUGUI
    {
        // keep equal to RENDER_PX in Tools/render_bn_auto.py
        public const float RenderPx = 80f;
        private const int DefaultBucket = 1200;
        private const float BnScale = 1.18f;

        private static Dictionary<string, int[]> _index;
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Requested = new HashSet<string>();
        private static readonly Regex SizeTag = new Regex(@"^\s*<size=(\d+(?:\.\d+)?)%>(.*?)(</size>)?\s*$", RegexOptions.Compiled);

        private string _src = string.Empty;
        private readonly List<Image> _imgs = new List<Image>();
        private bool _showing;
        private Vector2 _lastRect;
        private string _lastShown;
        private bool _lastBangla;
        private float _lastFont;
        private TextAlignmentOptions _lastAlign;
        private bool _lastBold;

        public override string text
        {
            get => _src;
            set
            {
                _src = value ?? string.Empty;
                Apply();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (string.IsNullOrEmpty(_src))
                _src = m_text ?? string.Empty;
            Loc.LanguageChanged += Apply;
            Apply();
        }

        protected override void OnDisable()
        {
            Loc.LanguageChanged -= Apply;
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (_showing && rectTransform.rect.size != _lastRect)
                Apply();
        }

        private void LateUpdate()
        {
            if (!_showing)
                return;
            foreach (var i in _imgs)
                i.color = color;
            if (!Mathf.Approximately(_lastFont, EffectiveFont) || _lastAlign != alignment || _lastBold != IsBold)
                Apply();
        }

        private float EffectiveFont => enableAutoSizing ? Mathf.Max(fontSizeMax, 1f) : Mathf.Max(fontSize, 1f);

        private bool IsBold => (fontStyle & FontStyles.Bold) != 0;

        private Image GetImage(int i)
        {
            while (_imgs.Count <= i)
            {
                var go = new GameObject("BnImage" + _imgs.Count, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                _imgs.Add(img);
            }

            return _imgs[i];
        }

        private void HideImages()
        {
            _showing = false;
            foreach (var i in _imgs)
                i.gameObject.SetActive(false);
        }

        private void Apply()
        {
            var shown = _src;
            var bangla = Loc.IsBangla;
            if (bangla && !Loc.HasBengali(shown))
            {
                var bn = Loc.Translate(shown);
                if (bn != null)
                    shown = bn;
            }

            // nothing changed since the last time: no work (dynamic labels are often re-assigned every frame)
            if (shown == _lastShown && bangla == _lastBangla && rectTransform.rect.size == _lastRect && Mathf.Approximately(_lastFont, EffectiveFont) &&
                _lastAlign == alignment && _lastBold == IsBold)
                return;
            _lastShown = shown;
            _lastBangla = bangla;
            _lastRect = rectTransform.rect.size;
            _lastFont = EffectiveFont;
            _lastAlign = alignment;
            _lastBold = IsBold;

            if (!bangla || !Loc.HasBengali(shown))
            {
                HideImages();
                base.text = shown;
                return;
            }

            var bucket = WantedPx();
            var sprites = new List<Sprite>();
            var scales = new List<float>();
            var whole = FindSprite(shown, bucket);
            if (whole != null)
            {
                sprites.Add(whole);
                scales.Add(1f);
            }
            else if (shown.IndexOf('\n') >= 0)
            {
                foreach (var raw in shown.Split('\n'))
                {
                    var pct = 1f;
                    var line = raw;
                    var m = SizeTag.Match(raw);
                    if (m.Success)
                    {
                        pct = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100f;
                        line = m.Groups[2].Value;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var sp = FindSprite(line, bucket);
                    if (sp == null)
                    {
                        sprites.Clear();
                        break;
                    }

                    sprites.Add(sp);
                    scales.Add(pct);
                }
            }

            if (sprites.Count == 0)
            {
                // never hand Bengali to TextMeshPro (it draws boxes and floods the log): show nothing and report it once
                HideImages();
                base.text = string.Empty;
                Request(shown, true);
                return;
            }

            base.text = string.Empty;
            Fit(sprites, scales);
        }

        // the width of the box expressed in the pixels the images were rendered at
        private float WantedPx()
        {
            var w = rectTransform.rect.width;
            return w < 8f ? 1500f : w * RenderPx / EffectiveFont;
        }

        /// <summary>
        /// Every text is rendered at a ladder of wrap widths (Tools/render_bn_auto.py), widest = one line. The widest variant that
        /// still fits the box is used, so a text that fits on one line is never wrapped; a box narrower than every variant gets the
        /// narrowest and the image is scaled down to fit.
        /// </summary>
        private Sprite FindSprite(string bn, float wantedPx)
        {
            if (_index == null)
                LoadIndex();
            var key = Hash(bn, IsBold);
            if (!_index.TryGetValue(key, out var buckets))
            {
                key = Hash(bn, !IsBold);
                if (!_index.TryGetValue(key, out buckets))
                    return null;
            }

            var best = buckets[0];
            foreach (var b in buckets)
            {
                if (b <= wantedPx)
                    best = b;
            }

            var name = key + "_" + best;
            if (!SpriteCache.TryGetValue(name, out var sp) || sp == null)
            {
                sp = Resources.Load<Sprite>("BnAuto/" + name);
                SpriteCache[name] = sp;
            }

            return sp;
        }

        private void Fit(List<Sprite> sprites, List<float> pcts)
        {
            var box = rectTransform.rect.size;
            var baseScale = EffectiveFont / RenderPx * BnScale; // Bengali looks smaller than Latin at the same size
            var w = new float[sprites.Count];
            var h = new float[sprites.Count];
            float totalH = 0f, maxW = 0f;
            for (var i = 0; i < sprites.Count; i++)
            {
                w[i] = sprites[i].rect.width * baseScale * pcts[i];
                h[i] = sprites[i].rect.height * baseScale * pcts[i];
                totalH += h[i];
                maxW = Mathf.Max(maxW, w[i]);
            }

            var shrink = Mathf.Min(1f, box.x / Mathf.Max(1f, maxW), box.y / Mathf.Max(1f, totalH));
            totalH *= shrink;

            var a = alignment;
            var left = a == TextAlignmentOptions.Left || a == TextAlignmentOptions.TopLeft || a == TextAlignmentOptions.BottomLeft ||
                       a == TextAlignmentOptions.MidlineLeft || a == TextAlignmentOptions.BaselineLeft || a == TextAlignmentOptions.CaplineLeft;
            var right = a == TextAlignmentOptions.Right || a == TextAlignmentOptions.TopRight || a == TextAlignmentOptions.BottomRight ||
                        a == TextAlignmentOptions.MidlineRight || a == TextAlignmentOptions.BaselineRight || a == TextAlignmentOptions.CaplineRight;
            var top = a == TextAlignmentOptions.Top || a == TextAlignmentOptions.TopLeft || a == TextAlignmentOptions.TopRight ||
                      a == TextAlignmentOptions.TopJustified;
            var bottom = a == TextAlignmentOptions.Bottom || a == TextAlignmentOptions.BottomLeft || a == TextAlignmentOptions.BottomRight;
            var x = left ? 0f : right ? 1f : 0.5f;
            var y = top ? 1f : bottom ? 0f : 0.5f;

            // the block of lines sits at the vertical alignment of the box, lines stacked top to bottom
            var blockTopFromBoxTop = top ? 0f : bottom ? box.y - totalH : (box.y - totalH) * 0.5f;
            var cursor = blockTopFromBoxTop;
            for (var i = 0; i < sprites.Count; i++)
            {
                var img = GetImage(i);
                img.gameObject.SetActive(true);
                img.sprite = sprites[i];
                var irt = (RectTransform)img.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(x, 1f);
                irt.pivot = new Vector2(x, 1f);
                irt.sizeDelta = new Vector2(w[i] * shrink, h[i] * shrink);
                irt.anchoredPosition = new Vector2(0f, -cursor);
                cursor += h[i] * shrink;
                img.color = color;
            }

            for (var i = sprites.Count; i < _imgs.Count; i++)
                _imgs[i].gameObject.SetActive(false);
            _showing = true;
        }

        public static string Hash(string text, bool bold)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text + "|" + (bold ? "b" : "r")));
                var sb = new StringBuilder(16);
                for (var i = 0; i < 8; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static void LoadIndex()
        {
            _index = new Dictionary<string, int[]>();
            var asset = Resources.Load<TextAsset>("BnAuto/index");
            if (asset == null)
                return;
            // {"hash":[1200,800],...}: tiny hand parser, no dependency
            var s = asset.text;
            var i = 0;
            while (true)
            {
                var q = s.IndexOf('"', i);
                if (q < 0)
                    break;
                var q2 = s.IndexOf('"', q + 1);
                var key = s.Substring(q + 1, q2 - q - 1);
                var l = s.IndexOf('[', q2);
                var r = s.IndexOf(']', l);
                var parts = s.Substring(l + 1, r - l - 1).Split(',');
                var list = new List<int>();
                foreach (var p in parts)
                {
                    if (int.TryParse(p.Trim(), out var n))
                        list.Add(n);
                }

                if (list.Count > 0)
                    _index[key] = list.ToArray();
                i = r + 1;
            }
        }

        private void Request(string bn, bool missing)
        {
#if UNITY_EDITOR
            var id = Hash(bn, IsBold);
            if (!Requested.Add(id))
                return;
            try
            {
                // Tools/render_bn_auto.py reads this file and renders every (text, bold, width) asked for
                var path = System.IO.Path.Combine(Application.dataPath, "../Temp/bn_requests.jsonl");
                var json = JsonUtility.ToJson(new Wrap { t = bn });
                var quoted = json.Substring(json.IndexOf(':') + 1).TrimEnd('}');
                System.IO.File.AppendAllText(path, "{\"text\":" + quoted + ",\"bold\":" + (IsBold ? "true" : "false") + "}\n",
                    new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }

            if (missing)
                Debug.LogError("[BnTmp] no Bangla image for: " + bn.Replace('\n', '|').Substring(0, Mathf.Min(48, bn.Length)) + " (run Tools/render_bn_auto.py)");
#endif
        }

        [Serializable]
        private sealed class Wrap
        {
            public string t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _index = null;
            SpriteCache.Clear();
            Requested.Clear();
        }
    }
}

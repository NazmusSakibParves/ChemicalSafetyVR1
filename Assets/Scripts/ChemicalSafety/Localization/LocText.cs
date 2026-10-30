using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Localization
{
    /// <summary>
    /// One label that is English TMP text or a rendered Bangla image, following Loc.Current. The box (RectTransform) and
    /// the font size stay the same in both languages, so layouts do not change.
    /// </summary>
    public sealed class LocText : MonoBehaviour
    {
        // Tools/render_content_bn.py renders every Bangla image at this font size
        public const float BnRenderPx = 96f;

        private TextMeshProUGUI _tmp;
        private Image _img;
        private RectTransform _rt;
        private string _scenario;
        private string _bnKey;
        private string _en;
        private float _fontPx;
        private TextAlignmentOptions _align;
        private Color _color;
        private bool _bound;

        public static LocText Create(RectTransform host, TMP_FontAsset enFont, float fontPx, Color color,
            TextAlignmentOptions align, bool bold)
        {
            var lt = host.gameObject.AddComponent<LocText>();
            lt._rt = host;
            lt._fontPx = fontPx;
            lt._color = color;
            lt._align = align;

            var tmp = host.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = enFont != null ? enFont : tmp.font;
            tmp.fontSize = fontPx;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = fontPx;
            tmp.fontSizeMin = Mathf.Max(10f, fontPx * 0.5f);
            lt._tmp = tmp;

            var imgGo = new GameObject("Bn", typeof(RectTransform));
            imgGo.transform.SetParent(host, false);
            var img = imgGo.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = color;
            lt._img = img;
            return lt;
        }

        /// <summary>Static key from Resources/Content/&lt;scenario&gt;.json.</summary>
        public void Bind(string scenario, string key)
        {
            _scenario = scenario;
            _bnKey = key;
            _en = Loc.T(scenario, key, Lang.En);
            _bound = true;
            Refresh();
        }

        private void OnEnable() => Loc.LanguageChanged += Refresh;
        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        public void SetColor(Color c)
        {
            _color = c;
            if (_tmp != null) _tmp.color = c;
            if (_img != null) _img.color = c;
        }

        public void Refresh()
        {
            if (!_bound || _tmp == null)
                return;
            if (Loc.IsBangla)
            {
                var sprite = Resources.Load<Sprite>("BnText/" + _scenario + "/" + _bnKey);
                if (sprite == null)
                    Debug.LogError($"[LocText] Missing Bangla image BnText/{_scenario}/{_bnKey} (run Tools/render_content_bn.py)");
                _tmp.enabled = false;
                _img.gameObject.SetActive(sprite != null);
                if (sprite != null)
                {
                    _img.sprite = sprite;
                    FitImage(sprite);
                }
            }
            else
            {
                _img.gameObject.SetActive(false);
                _tmp.enabled = true;
                _tmp.text = _en;
            }
        }

        private void FitImage(Sprite sprite)
        {
            var box = _rt.rect.size;
            var scale = _fontPx / BnRenderPx;
            var w = sprite.rect.width * scale;
            var h = sprite.rect.height * scale;
            var shrink = Mathf.Min(1f, box.x / Mathf.Max(1f, w), box.y / Mathf.Max(1f, h));
            w *= shrink;
            h *= shrink;
            var irt = (RectTransform)_img.transform;
            var left = _align == TextAlignmentOptions.Left || _align == TextAlignmentOptions.TopLeft || _align == TextAlignmentOptions.BottomLeft;
            irt.anchorMin = irt.anchorMax = new Vector2(left ? 0f : 0.5f, 0.5f);
            irt.pivot = new Vector2(left ? 0f : 0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(w, h);
        }
    }
}

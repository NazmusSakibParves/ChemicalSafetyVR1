using System.Collections;
using ChemicalSafety.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The caution popup of Scenario 22, drawn after the client's workflow picture: a frosted-glass card (almost clear, a thin rose-to-white border with a soft red
    /// glow on the left), the title "CAUTION, DO NOT CARRY SOLVENT IN A DRINKING WATER BOTTLE." in light grey, two faint glass tiles with the client's icons (a bottle in
    /// a red prohibition ring; a head drinking from a bottle with the yellow warning triangle over its corner) and a faint "Close (x)" in the top right corner.
    /// It opens when the supervisor stops the worker (the film pauses), the voice-over plays and it hides by itself when the voice is done (the director calls
    /// <see cref="HideFade"/>). The pictures are Resources/Sc22/Sc22_*.png (Tools/generate_sc22_popup_assets.py), the two frames are 9-sliced sprites.
    /// </summary>
    public sealed class Sc22CautionPopup : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;

        private Transform _card;
        private CanvasGroup _group;
        private Coroutine _fade;
        private Vector3 _baseScale = Vector3.one;

        public bool Visible => _card != null && _card.gameObject.activeSelf;
        public Transform Card => _card;

        private static Sprite S(string n) => Resources.Load<Sprite>("Sc22/" + n);

        private void Build()
        {
            if (_card != null || art == null)
                return;
            // the picture's frame is 710 x 507 (1.4 : 1)
            var canvas = PresentableUi.WorldCanvas("Sc22Caution", transform, new Vector2(1100f, 786f), 0.00056f, false, 32);
            _card = canvas.transform;
            _baseScale = _card.localScale;
            _group = canvas.gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = canvas.gameObject.AddComponent<CanvasGroup>();

            var frameSprite = S("Sc22_PopupFrame");
            var tileSprite = S("Sc22_PopupTile");
            if (frameSprite != null)
            {
                var frame = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, frameSprite, Color.white);
                frame.type = Image.Type.Sliced;
            }

            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.08f, 0.755f), new Vector2(0.92f, 0.915f), Sc22Content.CautionTitle, 56f,
                new Color(0.93f, 0.93f, 0.92f), TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f;
            title.fontSizeMax = 56f;

            // the two glass tiles (picture: x 0.07-0.455 and 0.50-0.945 of the frame, y 0.22-0.76 from the bottom)
            for (var i = 0; i < 2; i++)
            {
                var x0 = i == 0 ? 0.07f : 0.505f;
                var x1 = i == 0 ? 0.495f : 0.93f;
                if (tileSprite != null)
                {
                    var tile = PresentableUi.Img("Tile" + i, canvas.transform, new Vector2(x0, 0.10f), new Vector2(x1, 0.745f), tileSprite, Color.white);
                    tile.type = Image.Type.Sliced;
                }

                var icon = S(i == 0 ? "Sc22_IconNoBottle" : "Sc22_IconDrink");
                if (icon != null)
                {
                    var w = x1 - x0;
                    var rt = PresentableUi.Rect("Icon" + i, canvas.transform, new Vector2(x0 + w * 0.08f, 0.135f), new Vector2(x1 - w * 0.08f, 0.71f));
                    PresentableUi.Img(rt, icon, Color.white, preserveAspect: true);
                }
            }

            // the yellow warning triangle sits over the lower right corner of the second icon
            var warn = S("Sc22_IconWarn");
            if (warn != null)
            {
                var rt = PresentableUi.Rect("Warn", canvas.transform, new Vector2(0.735f, 0.115f), new Vector2(0.91f, 0.34f));
                PresentableUi.Img(rt, warn, Color.white, preserveAspect: true);
            }

            var close = PresentableUi.Text("Close", canvas.transform, new Vector2(0.73f, 0.895f), new Vector2(0.90f, 0.935f), Sc22Content.CautionClose, 22f,
                new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.MidlineRight, false, art.englishFont);
            close.raycastTarget = false;
            var closeIcon = S("Sc22_CloseIcon");
            if (closeIcon != null)
            {
                var rt = PresentableUi.Rect("CloseIcon", canvas.transform, new Vector2(0.905f, 0.893f), new Vector2(0.945f, 0.936f));
                PresentableUi.Img(rt, closeIcon, Color.white, preserveAspect: true);
            }

            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        /// <summary>Opens the card at a pose and scale (in front of the head).</summary>
        public void Show(Vector3 position, Quaternion rotation, float worldScale)
        {
            if (_card == null)
                Build();
            if (_card == null)
                return;
            _card.localScale = _baseScale * Mathf.Max(0.1f, worldScale);
            _card.SetPositionAndRotation(position, rotation);
            _card.gameObject.SetActive(true);
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(PopIn());
        }

        public void Hide()
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = null;
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        /// <summary>Fades the card out (0.3 s), then hides it.</summary>
        public IEnumerator HideFade()
        {
            if (_card == null || !_card.gameObject.activeSelf)
                yield break;
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = null;
            for (var t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                if (_group != null)
                    _group.alpha = 1f - t / 0.3f;
                yield return null;
            }

            _card.gameObject.SetActive(false);
            if (_group != null)
                _group.alpha = 1f;
        }

        private IEnumerator PopIn()
        {
            var baseScale = _card.localScale;
            _card.localScale = baseScale * 0.92f;
            if (_group != null)
                _group.alpha = 0f;
            for (var t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                var k = PresentableFx.EaseOutBack(t / 0.35f);
                _card.localScale = baseScale * Mathf.Lerp(0.92f, 1f, k);
                if (_group != null)
                    _group.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }

            _card.localScale = baseScale;
            if (_group != null)
                _group.alpha = 1f;
            _fade = null;
        }
    }
}

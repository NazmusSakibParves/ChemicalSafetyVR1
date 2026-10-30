using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Lazy-follow subtitle bar below the user's gaze: Bangla line (image) + English line.
    /// </summary>
    public sealed class WorldSubtitle : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private float distance = 1.0f;
        [SerializeField] private float heightOffset = 0.34f; // top safe zone, above the panels, buttons and props
        [SerializeField] private float followSpeed = 3f;

        private Transform _cam;
        private CanvasGroup _group;
        private Image _bangla;
        private TextMeshProUGUI _english;
        private Coroutine _fade;

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;

            var canvas = PresentableUi.WorldCanvas("SubtitleCanvas", transform, new Vector2(1500f, 260f), 0.0005f,
                false, 500);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var bg = PresentableUi.Rect("Background", canvas.transform, Vector2.zero, Vector2.one);
            PresentableUi.Img(bg, art != null ? art.softGlow : null, new Color(0f, 0.02f, 0.05f, 0.85f));

            _bangla = PresentableUi.BanglaLine("Bangla", canvas.transform, null, new Vector2(0f, 40f), 92f,
                Color.white);
            _english = PresentableUi.Text("English", canvas.transform, new Vector2(0.04f, 0.03f),
                new Vector2(0.96f, 0.33f), string.Empty, 42f, new Color(0.8f, 0.93f, 1f), font: art != null ? art.englishFont : null);
            _english.enableAutoSizing = true;
            _english.fontSizeMin = 26f;
            _english.fontSizeMax = 42f;

            // Always drawn over props, even when the user stands right next to a drum.
            PresentableUi.AlwaysOnTop(canvas);
            SnapToView();
        }

        private void LateUpdate()
        {
            if (_cam == null)
                return;

            Target(out var pos, out var rot);
            var k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
        }

        public void Show(Sprite bangla, string english, float seconds = 0f)
        {
            // English mode: English line only. Bangla mode: the Bangla image only; a line that has no image falls back to the
            // text (BnTmp shows the Bangla translation of an English line).
            if (!ChemicalSafety.Localization.Loc.IsBangla)
                bangla = null;
            else if (bangla != null)
                english = string.Empty;

            if (_bangla != null)
            {
                _bangla.sprite = bangla;
                _bangla.enabled = bangla != null;
                if (bangla != null)
                {
                    var rt = _bangla.rectTransform;
                    var h = bangla.rect.height > 140f ? 140f : 84f;
                    rt.sizeDelta = new Vector2(Mathf.Min(1420f, h * bangla.rect.width / bangla.rect.height), h);
                    rt.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(english) ? 0f : 42f);
                }
            }

            if (_english != null)
            {
                _english.text = english ?? string.Empty;
                var alone = bangla == null;
                _english.rectTransform.anchorMin = new Vector2(0.04f, alone ? 0.12f : 0.03f);
                _english.rectTransform.anchorMax = new Vector2(0.96f, alone ? 0.88f : 0.33f);
                _english.fontSizeMax = alone ? 72f : 42f;
            }

            StartFade(1f, 0.3f, seconds);
        }

        /// <summary>Lowers / raises the subtitle bar (kept clear of the Instructions panel while it is open).</summary>
        public void SetHeightOffset(float offset) => heightOffset = offset;

        public bool IsVisible => _group != null && _group.alpha > 0.05f;

        public void Hide()
        {
            StartFade(0f, 0.3f, 0f);
        }

        private void StartFade(float target, float duration, float holdThenHide)
        {
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(target, duration, holdThenHide));
        }

        private IEnumerator FadeRoutine(float target, float duration, float holdThenHide)
        {
            var from = _group.alpha;
            yield return PresentableFx.Tween(duration, t => _group.alpha = Mathf.Lerp(from, target, t));
            if (holdThenHide > 0f)
            {
                yield return PresentableFx.Wait(holdThenHide);
                yield return PresentableFx.Tween(0.4f, t => _group.alpha = Mathf.Lerp(1f, 0f, t));
            }

            _fade = null;
        }

        private void SnapToView()
        {
            if (_cam == null)
                return;
            Target(out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        private void Target(out Vector3 pos, out Quaternion rot)
        {
            var fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            pos = _cam.position + fwd * distance + Vector3.up * heightOffset;
            rot = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }
}

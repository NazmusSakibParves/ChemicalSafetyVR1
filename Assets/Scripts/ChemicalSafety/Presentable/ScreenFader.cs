using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Head-locked black fade for comfortable VR transitions.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private float startAlpha = 1f;

        private Image _image;
        private Coroutine _running;
        private float _fade;
        private float _extra;

        public float Alpha => _fade;

        /// <summary>Second, independent darkening (comfort fade when the head pokes into a solid prop).</summary>
        public void SetExtra(float alpha)
        {
            _extra = alpha;
            Apply();
        }

        private void Apply() => PresentableUi.SetAlpha(_image, Mathf.Max(_fade, _extra));

        private void Awake()
        {
            var cam = Camera.main != null ? Camera.main.transform : transform;
            var canvas = PresentableUi.WorldCanvas("FadeCanvas", cam, new Vector2(400f, 400f), 0.01f, false, 32000);
            canvas.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            canvas.transform.localRotation = Quaternion.identity;
            var rt = PresentableUi.Rect("Fade", canvas.transform, Vector2.zero, Vector2.one);
            _image = PresentableUi.Img(rt, null, new Color(0f, 0f, 0f, startAlpha));
            _fade = startAlpha;
        }

        public void SetImmediate(float alpha)
        {
            if (_running != null)
                StopCoroutine(_running);
            _fade = alpha;
            Apply();
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            if (_running != null)
                StopCoroutine(_running);

            var from = Alpha;
            yield return PresentableFx.Tween(duration, t =>
            {
                _fade = Mathf.Lerp(from, alpha, t);
                Apply();
            });
        }
    }
}

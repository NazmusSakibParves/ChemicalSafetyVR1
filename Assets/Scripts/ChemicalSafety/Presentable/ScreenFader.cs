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

        public float Alpha => _image != null ? _image.color.a : 0f;

        private void Awake()
        {
            var cam = Camera.main != null ? Camera.main.transform : transform;
            var canvas = PresentableUi.WorldCanvas("FadeCanvas", cam, new Vector2(400f, 400f), 0.01f, false, 32000);
            canvas.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            canvas.transform.localRotation = Quaternion.identity;
            var rt = PresentableUi.Rect("Fade", canvas.transform, Vector2.zero, Vector2.one);
            _image = PresentableUi.Img(rt, null, new Color(0f, 0f, 0f, startAlpha));
        }

        public void SetImmediate(float alpha)
        {
            if (_running != null)
                StopCoroutine(_running);
            PresentableUi.SetAlpha(_image, alpha);
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            if (_running != null)
                StopCoroutine(_running);

            var from = Alpha;
            yield return PresentableFx.Tween(duration, t => PresentableUi.SetAlpha(_image, Mathf.Lerp(from, alpha, t)));
        }
    }
}

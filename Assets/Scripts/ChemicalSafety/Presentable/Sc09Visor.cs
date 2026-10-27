using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9: chemical drops on the face shield. A quad rides on the head camera (about 0.3 m in front of the eyes, drawn on
    /// top of everything) with a droplet mask made at runtime. <see cref="Splash"/> makes the drops appear (flash, then the drops
    /// settle), <see cref="Wash"/> runs the clean-up: the water film runs down, the drops lose their colour, shrink away and the
    /// shield is clear. No grab pass or blur, so it is cheap on the Quest.
    /// </summary>
    public sealed class Sc09Visor : MonoBehaviour
    {
        [SerializeField] private Shader shader;
        [SerializeField] private float distance = 0.32f;
        [SerializeField] private Vector2 size = new Vector2(0.95f, 0.8f);

        private static readonly int CleanId = Shader.PropertyToID("_Clean");
        private static readonly int WetId = Shader.PropertyToID("_Wet");
        private static readonly int SlideId = Shader.PropertyToID("_Slide");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private Transform _quad;
        private Material _mat;
        private Texture2D _mask;
        private float _clean;
        private float _wet;
        private float _slide;
        private float _flash;
        private float _strength;
        private bool _active;

        /// <summary>0 = fully dirty, 1 = clear.</summary>
        public float Clean => _clean;

        /// <summary>True while drops are visible on the shield.</summary>
        public bool Dirty => _active && _clean < 0.98f && _strength > 0.02f;

        public float Strength => _strength;
        public float Wet => _wet;

        private void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
            if (_mask != null)
                Destroy(_mask);
        }

        private void Build()
        {
            if (_quad != null)
                return;
            var cam = Camera.main;
            if (cam == null || shader == null)
                return;
            _mask = MakeMask(512, 9);
            _mat = new Material(shader) { name = "Sc09VisorRuntime", renderQueue = (int)RenderQueue.Overlay + 10 };
            _mat.SetTexture("_MainTex", _mask);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Sc09VisorQuad";
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _quad = go.transform;
            _quad.SetParent(cam.transform, false);
            _quad.localPosition = new Vector3(0f, -0.01f, distance);
            _quad.localRotation = Quaternion.identity;
            _quad.localScale = new Vector3(size.x, size.y, 1f);
            go.SetActive(false);
        }

        private void Apply()
        {
            if (_mat == null)
                return;
            _mat.SetFloat(CleanId, _clean);
            _mat.SetFloat(WetId, _wet);
            _mat.SetFloat(SlideId, _slide);
            _mat.SetFloat(FlashId, _flash);
            _mat.SetFloat(StrengthId, _strength);
        }

        public void Hide()
        {
            StopAllCoroutines();
            _active = false;
            _clean = 0f;
            _wet = 0f;
            _slide = 0f;
            _flash = 0f;
            _strength = 0f;
            if (_quad != null)
                _quad.gameObject.SetActive(false);
        }

        /// <summary>Liquid hits the shield: a purple flash, then the drops.</summary>
        public IEnumerator Splash(float seconds = 0.9f)
        {
            Build();
            if (_quad == null)
                yield break;
            _active = true;
            _clean = 0f;
            _wet = 0f;
            _slide = 0f;
            _strength = 0f;
            _quad.gameObject.SetActive(true);
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / seconds);
                _strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));
                _flash = (1f - k) * Mathf.Clamp01(k * 8f);
                Apply();
                yield return null;
            }

            _strength = 1f;
            _flash = 0f;
            Apply();
        }

        /// <summary>Shows the dirty shield at once (editor tests).</summary>
        public void ShowDirty()
        {
            Build();
            if (_quad == null)
                return;
            _active = true;
            _clean = 0f;
            _wet = 0f;
            _slide = 0f;
            _flash = 0f;
            _strength = 1f;
            _quad.gameObject.SetActive(true);
            Apply();
        }

        /// <summary>Running water: the film appears, the drops creep and wash off over <paramref name="seconds"/>.</summary>
        public IEnumerator Wash(float seconds)
        {
            if (_quad == null)
                yield break;
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / seconds);
                _wet = Mathf.Clamp01(k * 5f) * (1f - 0.35f * Mathf.SmoothStep(0.7f, 1f, k));
                _slide = k * 1.6f;
                _clean = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.12f) / 0.88f));
                Apply();
                yield return null;
            }

            _clean = 1f;
            _wet = 0.25f;
            Apply();
            yield return PresentableFx.Wait(0.8f);
            for (var i = 0; i < 20; i++)
            {
                _wet = Mathf.Lerp(0.25f, 0f, i / 19f);
                _strength = Mathf.Lerp(1f, 0f, i / 19f);
                Apply();
                yield return null;
            }

            _active = false;
            _quad.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the droplet mask
        private static Texture2D MakeMask(int n, int seed)
        {
            var rnd = new System.Random(seed);
            var px = new Color32[n * n];
            for (var i = 0; i < px.Length; i++)
                px[i] = new Color32(0, 0, 255, 0);

            void Drop(float cx, float cy, float rx, float ry, float stick)
            {
                var x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 2));
                var x1 = Mathf.Min(n - 1, Mathf.CeilToInt(cx + rx + 2));
                var y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 2));
                var y1 = Mathf.Min(n - 1, Mathf.CeilToInt(cy + ry + 2));
                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var dx = (x - cx) / rx;
                        var dy = (y - cy) / ry;
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 1f)
                            continue;
                        var body = Mathf.Clamp01((1f - d) * 3.2f);
                        // a bright crescent and a darker rim: reads as a lens
                        var hx = (x - (cx + rx * 0.35f)) / (rx * 0.38f);
                        var hy = (y - (cy - ry * 0.35f)) / (ry * 0.30f);
                        var hl = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy));
                        var idx = y * n + x;
                        if (body * 255f > px[idx].r)
                        {
                            var g = (byte)Mathf.Clamp(128f + hl * 127f - Mathf.Clamp01((d - 0.7f) / 0.3f) * 60f, 0f, 255f);
                            px[idx] = new Color32((byte)(body * 255f), g, (byte)(stick * 255f), 255);
                        }
                    }
                }
            }

            float R() => (float)rnd.NextDouble();

            // many fine drops, a middle size, and a few big blobs; the small ones wash off first (low "stick")
            for (var i = 0; i < 1100; i++)
            {
                var r = 1.1f + R() * 2.4f;
                Drop(R() * n, R() * n, r, r * (0.9f + R() * 0.3f), R() * 0.45f);
            }

            for (var i = 0; i < 150; i++)
            {
                var r = 2.8f + R() * 4.2f;
                Drop(R() * n, R() * n, r, r * (1f + R() * 0.5f), 0.25f + R() * 0.45f);
            }

            for (var i = 0; i < 24; i++)
            {
                var r = 7f + R() * 8f;
                Drop(R() * n, R() * n, r, r * (1.1f + R() * 0.7f), 0.55f + R() * 0.4f);
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                name = "Sc09VisorMask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}

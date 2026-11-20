using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemicalSafety.Splash
{
    /// <summary>
    /// Startup intro: "Immersive Experience by" appears letter by letter (drop + pop + gold shimmer),
    /// then the BMQAS logo pops in with a glow ring, then everything fades out and the app scene loads.
    /// Standalone: touches no gameplay object. The world-space canvas does not move with the head, so
    /// head tracking is the normal XR camera.
    /// </summary>
    public class SplashSequence : MonoBehaviour
    {
        public TMP_Text title;
        public RectTransform titleRule;
        public RectTransform logo;
        public Image logoImage;
        public Image glow;
        public Image ring;
        public CanvasGroup root;
        public RectTransform[] sparks;
        public int nextBuildIndex = 1;

        const float LetterStep = 0.055f, LetterDur = 0.55f;
        const float LogoStart = 2.1f, LogoDur = 0.9f, HoldUntil = 5.0f, FadeDur = 0.8f;

        IEnumerator Start()
        {
            root.alpha = 1f;
            logo.localScale = Vector3.zero; logoImage.color = new Color(1, 1, 1, 0);
            glow.color = new Color(1, 1, 1, 0); ring.color = new Color(1, 1, 1, 0);
            titleRule.localScale = new Vector3(0, 1, 1);
            title.ForceMeshUpdate();

            AsyncOperation load = null;
            if (SceneManager.sceneCountInBuildSettings > nextBuildIndex)
            {
                load = SceneManager.LoadSceneAsync(nextBuildIndex);
                load.allowSceneActivation = false;
            }

            var sparkSeed = new float[sparks.Length];
            for (int i = 0; i < sparks.Length; i++) sparkSeed[i] = Random.value;

            // Own clock: each frame advances at most 1/20 s, so a long frame (big scene loading in the background)
            // can never skip the intro; every part of it is drawn.
            float t = 0f;
            yield return null;
            while (true)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                AnimateTitle(t);
                AnimateRule(t);
                AnimateLogo(t);
                AnimateSparks(t, sparkSeed);

                if (t > HoldUntil)
                {
                    float f = Mathf.Clamp01((t - HoldUntil) / FadeDur);
                    root.alpha = 1f - f * f;
                    if (f >= 1f) break;
                }
                yield return null;
            }

            if (load == null) { Debug.LogWarning("[SPLASH] No next scene in the build."); yield break; }
            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;
        }

        void AnimateTitle(float t)
        {
            title.ForceMeshUpdate();
            var info = title.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                int mi = ch.materialReferenceIndex, vi = ch.vertexIndex;
                var verts = info.meshInfo[mi].vertices;
                var cols = info.meshInfo[mi].colors32;
                float k = Mathf.Clamp01((t - 0.2f - i * LetterStep) / LetterDur);
                float pop = EaseOutBack(k);
                float a = Mathf.Clamp01(k * 2f);
                Vector3 centre = (verts[vi] + verts[vi + 2]) * 0.5f;
                float drop = (1f - EaseOutCubic(k)) * 70f;
                float scale = Mathf.Lerp(1.8f, 1f, pop);
                // shimmer: a bright gold band that sweeps across after the last letter has landed
                float sweepT = (t - (0.2f + info.characterCount * LetterStep + 0.3f)) / 1.2f;
                float pos = (float)i / Mathf.Max(1, info.characterCount - 1);
                float band = sweepT < 0f || sweepT > 1.4f ? 0f : Mathf.Clamp01(1f - Mathf.Abs(pos * 1.2f - (sweepT * 1.3f - 0.1f)) * 5f);
                var baseCol = new Color32(250, 247, 238, 255);
                var gold = new Color32(255, 205, 60, 255);
                Color32 c = Color32.Lerp(baseCol, gold, band);
                c.a = (byte)(255 * a);
                for (int j = 0; j < 4; j++)
                {
                    var v = verts[vi + j];
                    v = centre + (v - centre) * scale + new Vector3(0, drop, 0);
                    info.meshInfo[mi].vertices[vi + j] = v;
                    cols[vi + j] = c;
                }
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                info.meshInfo[m].mesh.colors32 = info.meshInfo[m].colors32;
                title.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        void AnimateRule(float t)
        {
            float k = Mathf.Clamp01((t - 1.5f) / 0.7f);
            titleRule.localScale = new Vector3(EaseOutCubic(k), 1, 1);
        }

        void AnimateLogo(float t)
        {
            float k = Mathf.Clamp01((t - LogoStart) / LogoDur);
            float s = Mathf.Lerp(0.55f, 1f, EaseOutBack(k));
            logo.localScale = Vector3.one * s * (1f + 0.012f * Mathf.Sin(t * 2.2f) * k);
            logoImage.color = new Color(1, 1, 1, Mathf.Clamp01(k * 2.2f));

            // soft golden halo behind the logo
            float g = Mathf.Clamp01((t - LogoStart) / 0.9f);
            glow.color = new Color(1f, 0.82f, 0.25f, 0.28f * g * (0.8f + 0.2f * Mathf.Sin(t * 2.6f)));
            glow.rectTransform.localScale = Vector3.one * (1.1f + 0.05f * Mathf.Sin(t * 2.6f));

            // one shock ring when the logo lands
            float r = Mathf.Clamp01((t - (LogoStart + 0.35f)) / 1.1f);
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 2.2f, EaseOutCubic(r));
            ring.color = new Color(1f, 0.85f, 0.35f, (1f - r) * 0.55f * (r > 0f ? 1f : 0f));
        }

        void AnimateSparks(float t, float[] seed)
        {
            for (int i = 0; i < sparks.Length; i++)
            {
                var rt = sparks[i];
                float life = (t * (0.12f + seed[i] * 0.1f) + seed[i] * 7f) % 1f;
                float x = (seed[i] * 2f - 1f) * 900f + Mathf.Sin(t * 0.8f + i) * 25f;
                float y = Mathf.Lerp(-520f, 520f, life);
                rt.anchoredPosition = new Vector2(x, y);
                var img = rt.GetComponent<Image>();
                var c = img.color; c.a = Mathf.Sin(life * Mathf.PI) * 0.45f * Mathf.Clamp01(t / 1.0f);
                img.color = c;
            }
        }

        static float EaseOutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }
    }
}

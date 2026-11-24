using System.Collections;
using ChemicalSafety.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// client 2026-11-24: after the scorecard (5 s, the Back to Home click or the 40-minute time-out) a white credits screen (text, then the logos),
    /// then every cache is dropped and the app leaves for the Quest home. One-shot coroutine; nothing is subscribed, nothing runs per frame except
    /// the two fades. While it plays only its own layer is rendered (white background), so the scene behind costs nothing and never shows.
    /// </summary>
    public static class CreditsSplash
    {
        // false = the old end (black fade, upload wait, quit) without the credits
        public const bool Enabled = true;

        private const float TextSeconds = 3f;
        private const float LogoSeconds = 3f;
        private const float FadeSeconds = 0.4f;
        private const int SplashLayer = 5; // the built-in UI layer: only this layer is drawn while the splash plays
        private const float CanvasW = 1600f;
        private const float CanvasH = 800f;
        private const float Distance = 1.6f;
        private const float MetersPerPixel = 0.0016f;

        private static readonly Color Ink = new Color(0.08f, 0.08f, 0.09f, 1f);

        private const string Credits =
            "<b><size=46>A production of the Deutsche Gesellschaft\nfür Internationale Zusammenarbeit (GIZ) GmbH.</size></b>\n\n" +
            "<size=38>Script : MD. MEHEDI HASAN</size>\n" +
            "<size=35>Production : Bengal Mobile QA Solution (BMQAS)</size>\n" +
            "<size=32>A production for: National Occupational Safety and Health Training and Research Center (NOSHTRI)</size>\n" +
            "<size=29>Publication:2026</size>";

        /// <param name="head">the player's camera</param>
        /// <param name="uploadStillWaiting">true while the result upload is running: the white page stays up to <paramref name="maxHold"/> seconds in total</param>
        public static IEnumerator Play(Transform head, System.Func<bool> uploadStillWaiting, float maxHold)
        {
            var cam = head != null ? head.GetComponent<Camera>() : null;
            if (cam == null)
                yield break;

            // everything of the old scene stops rendering: only the white splash layer is drawn
            var oldMask = cam.cullingMask;
            var oldFlags = cam.clearFlags;
            var oldBg = cam.backgroundColor;
            cam.cullingMask = 1 << SplashLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;

            // a fixed panel in front of the head (not head-locked: comfortable in VR)
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            fwd.Normalize();
            var canvas = PresentableUi.WorldCanvas("CreditsCanvas", null, new Vector2(CanvasW, CanvasH), MetersPerPixel, false, 32001);
            canvas.gameObject.layer = SplashLayer;
            canvas.transform.SetPositionAndRotation(head.position + fwd * Distance, Quaternion.LookRotation(fwd, Vector3.up));

            var white = PresentableUi.Img(PresentableUi.Rect("White", canvas.transform, Vector2.zero, Vector2.one), null, Color.white);
            var page1 = Page("Page1", canvas.transform);
            var page2 = Page("Page2", canvas.transform);
            page2.alpha = 0f;
            BuildTextPage(page1.transform);
            var tex1 = Logo(page2.transform, "Ui/Splash/Logo_GermanCoopGiz", -330f, 10f, 900f);
            var tex2 = Logo(page2.transform, "Ui/Splash/Logo_GobSeal", 470f, 10f, 640f);
            var veil = PresentableUi.Img(PresentableUi.Rect("Veil", canvas.transform, Vector2.zero, Vector2.one), null, Color.black);
            SetLayerDeep(canvas.transform, SplashLayer);

            // the black of the last fade lifts into the white credits
            yield return PresentableFx.Tween(FadeSeconds, t => PresentableUi.SetAlpha(veil, 1f - t));
            var held = FadeSeconds;
            yield return Hold(TextSeconds);
            held += TextSeconds;
            yield return PresentableFx.Tween(FadeSeconds, t => { page1.alpha = 1f - t; page2.alpha = t; });
            held += FadeSeconds;
            yield return Hold(LogoSeconds);
            held += LogoSeconds;

            // the result must reach the disk / server: stay on the white page for the upload (never more than maxHold in total)
            while (uploadStillWaiting != null && uploadStillWaiting() && held < maxHold)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            // the cleanup: splash gone, textures released, caches dropped, memory returned (the process ends right after)
            Object.Destroy(canvas.gameObject);
            if (tex1 != null)
                Resources.UnloadAsset(tex1);
            if (tex2 != null)
                Resources.UnloadAsset(tex2);
            HudArt.ClearCache();
            BnTmp.ClearSpriteCache();
            cam.cullingMask = oldMask;
            cam.clearFlags = oldFlags;
            cam.backgroundColor = oldBg;
            yield return null;
            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone)
                yield return null;
            System.GC.Collect();
        }

        private static IEnumerator Hold(float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime; // the clock is stopped and audio may be paused: real time
                yield return null;
            }
        }

        private static CanvasGroup Page(string name, Transform parent)
        {
            var rt = PresentableUi.Rect(name, parent, Vector2.zero, Vector2.one);
            return rt.gameObject.AddComponent<CanvasGroup>();
        }

        // Liberation Sans (the Arial-metric font TextMesh Pro already ships), squeezed to ~88 % width for the condensed look of the client's reference
        private static void BuildTextPage(Transform parent)
        {
            var rt = PresentableUi.Rect("Credits", parent, Vector2.zero, Vector2.one);
            rt.localScale = new Vector3(0.88f, 1f, 1f);
            rt.offsetMin = new Vector2(-100f, 0f);
            rt.offsetMax = new Vector2(100f, 0f);
            var go = rt.gameObject;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            tmp.text = Credits;
            tmp.color = Ink;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.lineSpacing = 6f;
        }

        // a logo drawn at a fixed width (aspect kept); the texture is returned so it can be unloaded
        private static Texture2D Logo(Transform parent, string resource, float x, float y, float width)
        {
            var tex = Resources.Load<Texture2D>(resource);
            if (tex == null)
            {
                Debug.LogWarning("[CreditsSplash] missing Resources/" + resource);
                return null;
            }

            var rt = PresentableUi.Centered("Logo", parent, new Vector2(x, y), new Vector2(width, width * tex.height / tex.width));
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            return tex;
        }

        private static void SetLayerDeep(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++)
                SetLayerDeep(t.GetChild(i), layer);
        }
    }
}

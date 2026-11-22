#if UNITY_EDITOR
using ChemicalSafety.Splash;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Startup intro scene ("Immersive Experience by" + BMQAS logo, animated) and the Android app icon.
    /// Creates ONLY new assets (Assets/Art/Splash, Assets/Scenes/ChemicalSafety_Splash.unity) and sets
    /// Player Settings splash/icon fields. Never touches the Presentable scene or any gameplay code.
    /// </summary>
    public static class SplashSetup
    {
        public const string Dir = "Assets/Art/Splash/";
        public const string SplashScene = "Assets/Scenes/ChemicalSafety_Splash.unity";
        private const string LogoPng = Dir + "Splash2_Logo.png";
        private const string FontTtf = "Assets/Art/Splash/Georgia-BoldItalic.ttf";
        private const string FontAsset = Dir + "Georgia-BoldItalic SDF.asset";

        [MenuItem("Chemical Safety/Splash/Apply Splash Scene + Icon")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[SPLASH] Stop Play mode first."); return; }

            ImportSprite(LogoPng);
            ImportTex(Dir + "AppIcon.png");
            ImportTex(Dir + "AppIcon_Adaptive_Fg.png");
            ImportTex(Dir + "AppIcon_Adaptive_Bg.png");

            // Unity's own "Made with Unity" splash is replaced by our scene, which is the first scene of the Presentable APK.
            PlayerSettings.SplashScreen.show = false;

            ApplyIcons();
            BuildSplashScene();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Called by the Presentable APK build (ChemicalSafetyApkBuilder) so that EVERY build, also one made from a fresh clone, has the same two
        /// Player Settings as the delivered APK: Unity's own "Made with Unity" splash off and the Android app icon set. The ProjectSettings file
        /// itself is not committed (Unity rewrites it), the intro scene and the icon pictures are. Never throws into the build: a missing picture
        /// only skips the icon.
        /// </summary>
        public static void ApplyBuildBranding()
        {
            PlayerSettings.SplashScreen.show = false;
            var ok = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon.png") != null
                     && AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon_Adaptive_Fg.png") != null
                     && AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon_Adaptive_Bg.png") != null;
            if (ok)
                ApplyIcons();
            else
                Debug.LogWarning("[SPLASH] App icon pictures not found in " + Dir + ": the icon was left as it is.");
        }

        private static void ApplyIcons()
        {
            var app = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon_Adaptive_Fg.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon_Adaptive_Bg.png");
            var target = NamedBuildTarget.Android;
            PlayerSettings.SetIcons(target, new[] { app }, IconKind.Any);
            // Legacy/Round kinds are obsolete in Unity 6: they are generated from the Adaptive layers.
            var icons = PlayerSettings.GetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            foreach (var ic in icons) { ic.SetTexture(bg, 0); ic.SetTexture(fg, 1); }
            PlayerSettings.SetPlatformIcons(target, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, icons);
            Debug.Log("[SPLASH] Android icons set.");
        }

        private static TMP_FontAsset GetFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
            if (font == null) { Debug.LogError("[SPLASH] Font missing: " + FontTtf); return null; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            fa.name = "Georgia-BoldItalic SDF";
            AssetDatabase.CreateAsset(fa, FontAsset);
            fa.material.name = "Georgia-BoldItalic SDF Material";
            fa.atlasTextures[0].name = "Georgia-BoldItalic SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            // bake the letters we use so the build needs no dynamic generation
            fa.TryAddCharacters("Immersive Experience by");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        private static void BuildSplashScene()
        {
            var tfont = GetFont();
            var logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPng);
            if (tfont == null || logoSprite == null) { Debug.LogError("[SPLASH] Missing font or logo sprite."); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: plain black, nothing else in the scene (head tracking is the normal XR camera pose).
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 1f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
            camGo.AddComponent<AudioListener>();

            // World-space canvas 2.4 m ahead, 2000 x 1200 units at 0.0012 m/unit.
            var cgo = new GameObject("SplashCanvas");
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            cgo.AddComponent<CanvasScaler>();
            var rootGroup = cgo.AddComponent<CanvasGroup>();
            var crt = cgo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(2000, 1200);
            crt.position = new Vector3(0, 0, 2.4f);
            crt.localScale = Vector3.one * 0.0012f;

            // soft vignette-like backdrop panel (very dark blue)

            var sparks = new RectTransform[26];
            var spark = MakeCircleSprite(16);
            for (int i = 0; i < sparks.Length; i++)
            {
                var s = MakeImage(crt, "Spark" + i, Vector2.one * Random.Range(6f, 14f), Vector2.zero, new Color(1f, 0.85f, 0.4f, 0f), spark);
                sparks[i] = s.rectTransform;
            }

            var glowSprite = MakeGlowSprite(256);
            var glow = MakeImage(crt, "Glow", new Vector2(900, 900), new Vector2(0, -170), new Color(1, 0.82f, 0.25f, 0), glowSprite);
            var ringSprite = MakeRingSprite(256);
            var ring = MakeImage(crt, "Ring", new Vector2(640, 640), new Vector2(0, -170), new Color(1, 1, 1, 0), ringSprite);

            var logo = MakeImage(crt, "Logo", new Vector2(589, 540) * 0.95f, new Vector2(0, -170), Color.white, logoSprite);

            var tgo = new GameObject("Title");
            tgo.transform.SetParent(crt, false);
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            tmp.font = tfont;
            tmp.text = "Immersive Experience by";
            tmp.fontSize = 120;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = new Color(0.98f, 0.97f, 0.93f, 1f);
            tmp.raycastTarget = false;
            var trt = tmp.rectTransform;
            trt.sizeDelta = new Vector2(1900, 200);
            trt.anchoredPosition = new Vector2(0, 340);

            var rule = MakeImage(crt, "TitleRule", new Vector2(520, 6), new Vector2(0, 225), new Color(1f, 0.8f, 0.25f, 0.95f), null);

            var seq = cgo.AddComponent<SplashSequence>();
            seq.title = tmp; seq.titleRule = rule.rectTransform;
            seq.logo = logo.rectTransform; seq.logoImage = logo;
            seq.glow = glow; seq.ring = ring; seq.root = rootGroup; seq.sparks = sparks;
            seq.nextBuildIndex = 1;

            EditorSceneManager.SaveScene(scene, SplashScene);
            Debug.Log("[SPLASH] Splash scene built: " + SplashScene);
        }

        private static Image MakeImage(Transform parent, string name, Vector2 size, Vector2 pos, Color col, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = col; img.raycastTarget = false;
            img.preserveAspect = sprite != null;
            img.rectTransform.sizeDelta = size;
            img.rectTransform.anchoredPosition = pos;
            return img;
        }

        // Tiny generated sprites (kept as sub-assets of nothing: saved as PNG files under Assets/Art/Splash).
        private static Sprite MakeCircleSprite(int n) => SaveSprite("Gen_Dot.png", n, (x, y, r) => Mathf.Clamp01((1f - r) * 6f));
        private static Sprite MakeGlowSprite(int n) => SaveSprite("Gen_Glow.png", n, (x, y, r) => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
        private static Sprite MakeRingSprite(int n) => SaveSprite("Gen_Ring.png", n, (x, y, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) * 22f));

        private static Sprite SaveSprite(string file, int n, System.Func<float, float, float, float> alphaOf)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alphaOf(fx, fy, r)));
                }
            tex.Apply();
            var path = Dir + file;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            ImportSprite(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ImportSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        private static void ImportTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.isReadable = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
    }
}
#endif

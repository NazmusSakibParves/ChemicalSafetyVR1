#if UNITY_EDITOR
using ChemicalSafety.Flow;
using ChemicalSafety.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Imports Bangla TMP font + welcome VO, wires intro texts and app-start greeting.
    /// </summary>
    public static class BanglaFontAndVoSetup
    {
        private const string ScenePath = "Assets/Scenes/ChemicalSafety_Demo.unity";
        private const string FontTtfPath = "Assets/Art/Fonts/NotoSansBengali-Regular.ttf";
        private const string FontAssetPath = "Assets/Art/Fonts/NotoSansBengali SDF.asset";
        private const string VoPath = "Assets/Art/Audio/VO/VO_AssalamuAlaikum_bn.mp3";

        [MenuItem("Chemical Safety/Setup Bangla Font And Welcome VO")]
        public static void Setup()
        {
            AssetDatabase.ImportAsset(FontTtfPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(VoPath, ImportAssetOptions.ForceUpdate);

            var fontAsset = EnsureBanglaTmpFont();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoPath);
            if (clip == null)
            {
                Debug.LogError("[ChemicalSafety] Missing VO at " + VoPath);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyBanglaFontToIntroAndWorld(fontAsset);
            WireWelcomePlayer(clip, fontAsset);
            ClearScenarioIntroVoiceClips();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChemicalSafety] Bangla font + welcome VO wired. Play mode: hear আসসালামু আলাইকুম at start.");
        }

        public static TMP_FontAsset EnsureBanglaTmpFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (source == null)
            {
                Debug.LogError("[ChemicalSafety] Missing font TTF at " + FontTtfPath);
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(source);
            if (fontAsset == null)
            {
                Debug.LogError("[ChemicalSafety] TMP_FontAsset.CreateFontAsset failed.");
                return null;
            }

            fontAsset.name = "NotoSansBengali SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Persist atlas / material sub-assets created by TMP.
            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            if (fontAsset.atlasTexture != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void ApplyBanglaFontToIntroAndWorld(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            var presenter = Object.FindAnyObjectByType<IntroInstructionPresenter>(FindObjectsInactive.Include);
            if (presenter != null)
            {
                AssignIfBanglaField(presenter, "greetingText", fontAsset);
                AssignIfBanglaField(presenter, "banglaBodyText", fontAsset);
            }

            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
            {
                if (tmp == null)
                    continue;
                var n = tmp.gameObject.name;
                if (n == "Greeting" || n == "Bangla" || n.Contains("Bn") || n.Contains("Bangla"))
                    tmp.font = fontAsset;
            }

            foreach (var tmp in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include))
            {
                if (tmp == null)
                    continue;
                var text = tmp.text ?? string.Empty;
                if (ContainsBangla(text) || tmp.gameObject.name.Contains("Bangla"))
                    tmp.font = fontAsset;
            }
        }

        private static void AssignIfBanglaField(object target, string fieldName, TMP_FontAsset fontAsset)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
                return;
            var tmp = field.GetValue(target) as TMP_Text;
            if (tmp != null)
                tmp.font = fontAsset;
        }

        private static void WireWelcomePlayer(AudioClip clip, TMP_FontAsset fontAsset)
        {
            var host = GameObject.Find("WelcomeVoice");
            if (host == null)
                host = new GameObject("WelcomeVoice");

            var source = host.GetComponent<AudioSource>();
            if (source == null)
                source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var canvasGo = GameObject.Find("WelcomeSubtitleCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("WelcomeSubtitleCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var group = canvasGo.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var subtitleGo = GameObject.Find("WelcomeSubtitle");
            if (subtitleGo == null)
            {
                subtitleGo = new GameObject("WelcomeSubtitle", typeof(RectTransform));
                subtitleGo.transform.SetParent(canvasGo.transform, false);
            }

            var rt = subtitleGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.08f);
            rt.anchorMax = new Vector2(0.9f, 0.22f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = subtitleGo.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
                tmp = subtitleGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "আসসালামু আলাইকুম";
            tmp.fontSize = 48;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (fontAsset != null)
                tmp.font = fontAsset;

            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.85f);

            var player = host.GetComponent<WelcomeVoicePlayer>();
            if (player == null)
                player = host.AddComponent<WelcomeVoicePlayer>();
            player.Configure(clip, source, tmp, group);

            SetPrivate(player, "welcomeClip", clip);
            SetPrivate(player, "voiceSource", source);
            SetPrivate(player, "subtitleText", tmp);
            SetPrivate(player, "subtitleGroup", group);
            SetPrivate(player, "subtitleBn", "আসসালামু আলাইকুম");
        }

        private static void ClearScenarioIntroVoiceClips()
        {
            var director = Object.FindAnyObjectByType<ScenarioIntroDirector>(FindObjectsInactive.Include);
            if (director == null)
                return;

            // App-start WelcomeVoicePlayer owns the salam VO; keep scenario intro clips null
            // so hotspot intro does not replay the same greeting audio.
            var field = typeof(ScenarioIntroDirector).GetField("contents",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
                return;

            var contents = field.GetValue(director) as ScenarioIntroDirector.IntroContent[];
            if (contents == null)
                return;

            foreach (var c in contents)
            {
                if (c != null)
                    c.voiceClip = null;
            }

            EditorUtility.SetDirty(director);
        }

        private static bool ContainsBangla(string text)
        {
            foreach (var c in text)
            {
                if (c >= '\u0980' && c <= '\u09FF')
                    return true;
            }

            return false;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
#endif

#if UNITY_EDITOR
using ChemicalSafety.Data;
using ChemicalSafety.Flow;
using ChemicalSafety.Interaction;
using ChemicalSafety.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Builds a reusable demo scene: floor, camera, drums, hotspot, MCQ UI.
    /// </summary>
    public static class ChemicalSafetyDemoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ChemicalSafety_Demo.unity";

        [MenuItem("Chemical Safety/Build Demo Scene (SC01)")]
        public static void BuildDemoScene()
        {
            var scenario = Sc01ContentFactory.CreateOrUpdateSc01();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CleanupDefaultCamera();

            var systems = CreateSystemsRoot();
            var flow = systems.GetComponent<TrainingFlowController>();
            var unlockBanner = systems.GetComponentInChildren<UnlockBannerView>(true);
            var mcq = BuildMcqPanel(systems.transform);
            SetPrivate(flow, "mcqPresenter", mcq);

            var bridge = systems.AddComponent<UnlockEventBridge>();
            bridge.Configure(flow, unlockBanner);

            CreateFloor();
            CreateXrRig();
            CreateDrumRack(scenario, flow);
            EnsureEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ChemicalSafety] Demo scene saved: {ScenePath}. Press Play, look at drums, press E to open MCQ.");
        }

        private static void CleanupDefaultCamera()
        {
            var main = GameObject.Find("Main Camera");
            if (main != null)
                Object.DestroyImmediate(main);
        }

        private static GameObject CreateSystemsRoot()
        {
            var root = new GameObject("ChemicalSafety_Systems");
            root.AddComponent<TrainingFlowController>();

            var bannerRoot = new GameObject("UnlockBanner");
            bannerRoot.transform.SetParent(root.transform);
            var canvas = bannerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bannerRoot.AddComponent<CanvasScaler>();
            bannerRoot.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("Message", typeof(RectTransform));
            textGo.transform.SetParent(bannerRoot.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0.8f);
            rt.anchorMax = new Vector2(0.9f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var banner = bannerRoot.AddComponent<UnlockBannerView>();
            SetPrivate(banner, "root", bannerRoot);
            SetPrivate(banner, "messageText", tmp);
            return root;
        }

        private static void CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TeleportFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.AddComponent<TeleportationArea>();
        }

        private static void CreateXrRig()
        {
            var rig = new GameObject("XR_Origin_Demo");
            rig.transform.position = new Vector3(0f, 0f, -2f);

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(rig.transform, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(cameraOffset.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            camGo.AddComponent<AudioListener>();

            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
            {
                var manager = new GameObject("XR Interaction Manager");
                manager.AddComponent<XRInteractionManager>();
            }
        }

        private static void CreateDrumRack(ScenarioDefinition scenario, TrainingFlowController flow)
        {
            var rack = new GameObject("DrumRack_SC01");
            rack.transform.position = new Vector3(0f, 0f, 3f);

            var labels = new[] { "Faded Label", "No Label", "GHS!=SDS", "GHS=SDS (Correct)" };
            var colors = new[]
            {
                new Color(0.55f, 0.45f, 0.2f),
                new Color(0.35f, 0.35f, 0.35f),
                new Color(0.75f, 0.25f, 0.2f),
                new Color(0.2f, 0.65f, 0.35f)
            };

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            for (var i = 0; i < 4; i++)
            {
                var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                drum.name = $"Drum_{i + 1}_{labels[i]}";
                drum.transform.SetParent(rack.transform, false);
                drum.transform.localPosition = new Vector3(-1.5f + i * 1f, 0.6f, 0f);
                drum.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                var renderer = drum.GetComponent<Renderer>();
                if (renderer != null && lit != null)
                    renderer.sharedMaterial = new Material(lit) { color = colors[i] };
            }

            var clipboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clipboard.name = "SDS_Clipboard";
            clipboard.transform.SetParent(rack.transform, false);
            clipboard.transform.localPosition = new Vector3(2.2f, 1f, 0f);
            clipboard.transform.localScale = new Vector3(0.4f, 0.55f, 0.05f);

            var hotspot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hotspot.name = "Hotspot_SC01";
            hotspot.transform.SetParent(rack.transform, false);
            hotspot.transform.localPosition = new Vector3(0f, 1.4f, -0.8f);
            hotspot.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var interactable = hotspot.AddComponent<XRSimpleInteractable>();
            interactable.selectMode = InteractableSelectMode.Single;

            var hotspotComp = hotspot.AddComponent<HotspotInteractable>();
            SetPrivate(hotspotComp, "scenario", scenario);
            SetPrivate(hotspotComp, "flowController", flow);

            var hotspotRenderer = hotspot.GetComponent<Renderer>();
            if (hotspotRenderer != null && lit != null)
                hotspotRenderer.sharedMaterial = new Material(lit) { color = new Color(0.1f, 0.7f, 0.95f) };

            var clickProxy = new GameObject("EditorActivateHotspot");
            clickProxy.transform.SetParent(rack.transform, false);
            clickProxy.AddComponent<EditorHotspotKeyActivator>().Configure(hotspotComp);
        }

        private static McqPresenter BuildMcqPanel(Transform parent)
        {
            var root = new GameObject("MCQ_WorldCanvas");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 1.6f, 1.5f);
            root.transform.localScale = Vector3.one * 0.0025f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 700);
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            var panel = CreateUiPanel(root.transform, "Panel", new Vector2(860, 640), Vector2.zero);
            var title = CreateTmp(panel.transform, "Title", new Vector2(0, 260), new Vector2(800, 50), 34, "Scenario");
            var qid = CreateTmp(panel.transform, "QuestionId", new Vector2(0, 210), new Vector2(800, 36), 24, "MCS");
            var prompt = CreateTmp(panel.transform, "Prompt", new Vector2(0, 140), new Vector2(800, 90), 28, "Question");
            var feedback = CreateTmp(panel.transform, "Feedback", new Vector2(0, -250), new Vector2(800, 70), 24, string.Empty);

            var buttons = new Button[4];
            var labels = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
            {
                var btnGo = CreateUiPanel(panel.transform, $"Option{i}", new Vector2(760, 56), new Vector2(0, 60 - i * 70));
                btnGo.GetComponent<Image>().color = new Color(0.92f, 0.95f, 0.98f);
                buttons[i] = btnGo.AddComponent<Button>();
                labels[i] = CreateTmp(btnGo.transform, "Label", Vector2.zero, new Vector2(720, 50), 24, $"Option {i}");
            }

            var closeGo = CreateUiPanel(panel.transform, "Close", new Vector2(180, 48), new Vector2(320, -280));
            closeGo.GetComponent<Image>().color = new Color(0.85f, 0.9f, 0.95f);
            var closeBtn = closeGo.AddComponent<Button>();
            CreateTmp(closeGo.transform, "Label", Vector2.zero, new Vector2(160, 40), 22, "Close");

            var presenter = root.AddComponent<McqPresenter>();
            SetPrivate(presenter, "panelRoot", root);
            SetPrivate(presenter, "titleText", title);
            SetPrivate(presenter, "questionIdText", qid);
            SetPrivate(presenter, "promptText", prompt);
            SetPrivate(presenter, "feedbackText", feedback);
            SetPrivate(presenter, "optionButtons", buttons);
            SetPrivate(presenter, "optionLabels", labels);
            SetPrivate(presenter, "closeButton", closeBtn);
            root.SetActive(false);
            return presenter;
        }

        private static GameObject CreateUiPanel(Transform parent, string name, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.96f);
            return go;
        }

        private static TextMeshProUGUI CreateTmp(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            float fontSize,
            string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = tmp.rectTransform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return tmp;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == path)
                    return;
            }

            var list = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(list, 0);
            list[list.Length - 1] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = list;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
#endif

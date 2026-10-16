#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Flow;
using ChemicalSafety.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.EventSystems;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Builds a presentable warehouse shell, reference murals, intro VO panel, and wires 10s intro → MCQ.
    /// AI 3D providers are optional; polished primitives are the reliable fallback.
    /// </summary>
    public static class DemoPolishBuilder
    {
        private const string ScenePath = "Assets/Scenes/ChemicalSafety_Demo.unity";
        private const string RefWarehouse = "Assets/Art/Reference/Ref_WarehouseAisle.jpg";
        private const string RefInstructions = "Assets/Art/Reference/Ref_InstructionsPanel.jpg";
        private const string RefDrums = "Assets/Art/Reference/Ref_FourDrums.jpg";

        [MenuItem("Chemical Safety/Polish Demo (Warehouse + 10s Intro + Murals)")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var flow = Object.FindAnyObjectByType<TrainingFlowController>();
            var systems = flow != null ? flow.gameObject : GameObject.Find("ChemicalSafety_Systems");
            if (systems == null)
            {
                systems = new GameObject("ChemicalSafety_Systems");
                flow = systems.AddComponent<TrainingFlowController>();
            }

            DestroyNamed("WarehouseShell");
            DestroyNamed("ZoneHeader_SC01");
            DestroyNamed("ZoneHeader_SC02");
            DestroyNamed("Intro_InstructionPanel");
            DestroyInactiveOrActive("MCQ_FullscreenCanvas");
            DestroyNamed("MCQ_WorldCanvas");

            BuildWarehouseShell();
            PlaceMurals();
            PolishDrumRack();
            SeparateSc02Zone();
            var intro = BuildIntroPanel(systems.transform);
            var mcq = BuildWorldMcq(systems.transform);
            WireFlow(systems, flow, intro, mcq);
            EnsureEventSystem();
            SoftenLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChemicalSafety] Polish demo applied: warehouse + murals + 10s intro (আসসালামু আলাইকুম) → MCQ. Play → E.");
        }

        private static void BuildWarehouseShell()
        {
            var root = new GameObject("WarehouseShell");
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            CreateBox(root.transform, "Floor", new Vector3(0f, -0.02f, 4f), new Vector3(18f, 0.04f, 16f),
                new Color(0.45f, 0.47f, 0.5f), lit);
            CreateBox(root.transform, "SafetyLineL", new Vector3(-2.2f, 0.01f, 3f), new Vector3(0.12f, 0.02f, 10f),
                new Color(0.95f, 0.85f, 0.15f), lit);
            CreateBox(root.transform, "SafetyLineR", new Vector3(2.2f, 0.01f, 3f), new Vector3(0.12f, 0.02f, 10f),
                new Color(0.95f, 0.85f, 0.15f), lit);

            CreateBox(root.transform, "WallBack", new Vector3(0f, 2.5f, 11f), new Vector3(18f, 5f, 0.2f),
                new Color(0.72f, 0.74f, 0.78f), lit);
            CreateBox(root.transform, "WallLeft", new Vector3(-9f, 2.5f, 4f), new Vector3(0.2f, 5f, 14f),
                new Color(0.7f, 0.72f, 0.76f), lit);
            CreateBox(root.transform, "WallRight", new Vector3(9f, 2.5f, 4f), new Vector3(0.2f, 5f, 14f),
                new Color(0.7f, 0.72f, 0.76f), lit);
            CreateBox(root.transform, "Ceiling", new Vector3(0f, 5.1f, 4f), new Vector3(18f, 0.15f, 14f),
                new Color(0.55f, 0.58f, 0.62f), lit);

            // Orange rack frame around SC01
            CreateBox(root.transform, "RackFrame", new Vector3(-1.5f, 1.2f, 5.2f), new Vector3(5.2f, 2.4f, 0.15f),
                new Color(0.85f, 0.45f, 0.12f), lit);
            CreateBox(root.transform, "RackPostL", new Vector3(-4f, 1.4f, 5.2f), new Vector3(0.15f, 2.8f, 0.15f),
                new Color(0.85f, 0.45f, 0.12f), lit);
            CreateBox(root.transform, "RackPostR", new Vector3(1f, 1.4f, 5.2f), new Vector3(0.15f, 2.8f, 0.15f),
                new Color(0.85f, 0.45f, 0.12f), lit);

            var sign = CreateWorldTmp(root.transform, "Sign_Caution",
                new Vector3(-6.5f, 2.4f, 10.6f), "সাবধান: বিপদজনক উপকরণ\nWarning: Hazardous Materials",
                4f, new Color(0.1f, 0.1f, 0.1f));
            sign.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var sign2 = CreateWorldTmp(root.transform, "Sign_Safety",
                new Vector3(6.5f, 2.4f, 10.6f), "নিরাপত্তা প্রথম\nSafety First",
                4f, new Color(0.05f, 0.35f, 0.15f));
            sign2.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void PlaceMurals()
        {
            PlaceMural("Mural_Warehouse", RefWarehouse, new Vector3(0f, 2.6f, 10.85f), new Vector3(6.5f, 3.2f, 1f));
            PlaceMural("Mural_FourDrums", RefDrums, new Vector3(5.8f, 2.2f, 8.5f), new Vector3(3.2f, 2.2f, 1f),
                Quaternion.Euler(0f, -25f, 0f));
        }

        private static void PlaceMural(string name, string assetPath, Vector3 pos, Vector3 scale, Quaternion? rot = null)
        {
            DestroyNamed(name);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogWarning("[ChemicalSafety] Missing mural texture: " + assetPath);
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot ?? Quaternion.Euler(0f, 180f, 0f);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit != null ? lit : Shader.Find("Standard"));
            mat.mainTexture = tex;
            mat.color = Color.white;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void PolishDrumRack()
        {
            var rack = GameObject.Find("DrumRack_SC01");
            if (rack == null)
                return;

            rack.transform.position = new Vector3(-1.5f, 0f, 4.6f);

            var colors = new[]
            {
                new Color(0.15f, 0.35f, 0.75f),
                new Color(0.12f, 0.32f, 0.7f),
                new Color(0.18f, 0.38f, 0.78f),
                new Color(0.1f, 0.45f, 0.35f)
            };

            var i = 0;
            foreach (Transform child in rack.transform)
            {
                if (!child.name.StartsWith("Drum_"))
                    continue;

                child.localPosition = new Vector3(-1.8f + i * 1.15f, 0.55f, 0f);
                child.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                var r = child.GetComponent<Renderer>();
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (r != null && lit != null)
                    r.sharedMaterial = new Material(lit) { color = colors[Mathf.Min(i, colors.Length - 1)] };

                // Remove old boards
                for (var guard = 0; guard < 8; guard++)
                {
                    var lb = child.Find("LabelBoard");
                    if (lb == null)
                        break;
                    Object.DestroyImmediate(lb.gameObject);
                }

                string label;
                if (child.name.Contains("Faded"))
                    label = "1\nFaded Label";
                else if (child.name.Contains("No Label"))
                    label = "2\nNo Label";
                else if (child.name.Contains("GHS!="))
                    label = "3\nGHS ≠ SDS";
                else
                    label = "4\nCorrect";

                CreateDrumLabel(child, label);
                i++;
            }
        }

        private static void CreateDrumLabel(Transform drum, string text)
        {
            var root = new GameObject("LabelBoard");
            root.transform.SetParent(drum, false);
            root.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var cs = drum.localScale;
            root.transform.localScale = new Vector3(
                1f / Mathf.Max(0.01f, cs.x),
                1f / Mathf.Max(0.01f, cs.y),
                1f / Mathf.Max(0.01f, cs.z));

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(0.95f, 0.55f, 0.04f);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var br = board.GetComponent<Renderer>();
            if (br != null && lit != null)
                br.sharedMaterial = new Material(lit) { color = new Color(0.08f, 0.1f, 0.16f) };

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            labelGo.transform.localScale = Vector3.one * 0.09f;
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 7;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void SeparateSc02Zone()
        {
            var zone = GameObject.Find("StorageZone_SC02");
            if (zone != null)
                zone.transform.position = new Vector3(4.5f, 0f, 3.5f);

            var header = CreateWorldTmp(null, "ZoneTitle_SC01",
                new Vector3(-1.5f, 3.1f, 4.2f), "SC01 — সঠিক ড্রাম নির্বাচন (A–D)", 5f, Color.white);
            header.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var header2 = CreateWorldTmp(null, "ZoneTitle_SC02",
                new Vector3(4.5f, 2.8f, 3.2f), "SC02 — Chemical Storage", 4.5f, Color.white);
            header2.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static IntroInstructionPresenter BuildIntroPanel(Transform parent)
        {
            var root = new GameObject("Intro_InstructionPanel");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 1.7f, 2.4f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            root.AddComponent<FaceCameraBillboard>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1100f, 700f);
            root.transform.localScale = Vector3.one * 0.0022f;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var cg = root.AddComponent<CanvasGroup>();

            var bg = CreateUiPanel(root.transform, "Bg", stretch: true);
            bg.GetComponent<Image>().color = new Color(0.05f, 0.12f, 0.28f, 0.92f);

            // Soft use of instructions reference as decorative top strip if available
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RefInstructions);
            if (tex != null)
            {
                var strip = CreateUiPanel(root.transform, "ArtStrip", stretch: false);
                var srt = strip.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.05f, 0.72f);
                srt.anchorMax = new Vector2(0.95f, 0.96f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var img = strip.GetComponent<Image>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RefInstructions);
                if (sprite == null)
                {
                    // Ensure sprite import
                    var importer = AssetImporter.GetAtPath(RefInstructions) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.SaveAndReimport();
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RefInstructions);
                    }
                }

                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                }
                else
                {
                    img.color = new Color(0.2f, 0.45f, 0.85f, 0.35f);
                }
            }

            var greeting = CreateUiTmp(root.transform, "Greeting", new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.7f),
                42, "আসসালামু আলাইকুম", FontStyles.Bold, TextAlignmentOptions.Center);
            greeting.color = new Color(0.75f, 0.9f, 1f);

            var title = CreateUiTmp(root.transform, "Title", new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.58f),
                40, "INSTRUCTIONS", FontStyles.Bold, TextAlignmentOptions.Center);
            title.color = Color.white;

            var en = CreateUiTmp(root.transform, "English", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.48f),
                28, "Select the correctly labeled drum based on the SDS.", FontStyles.Normal,
                TextAlignmentOptions.Center);
            en.color = new Color(0.9f, 0.93f, 1f);

            var bn = CreateUiTmp(root.transform, "Bangla", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.28f),
                26, "সেফটি ডাটা শিট (SDS) অনুসারে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।", FontStyles.Normal,
                TextAlignmentOptions.Center);
            bn.color = new Color(0.7f, 0.85f, 1f);

            var cd = CreateUiTmp(root.transform, "Countdown", new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.12f),
                36, "10", FontStyles.Bold, TextAlignmentOptions.Center);
            cd.color = new Color(0.5f, 0.85f, 1f);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var presenter = root.AddComponent<IntroInstructionPresenter>();
            SetPrivate(presenter, "panelRoot", root);
            SetPrivate(presenter, "greetingText", greeting);
            SetPrivate(presenter, "titleText", title);
            SetPrivate(presenter, "englishBodyText", en);
            SetPrivate(presenter, "banglaBodyText", bn);
            SetPrivate(presenter, "countdownText", cd);
            SetPrivate(presenter, "canvasGroup", cg);
            SetPrivate(presenter, "voiceSource", audio);

            root.SetActive(false);
            return presenter;
        }

        private static McqPresenter BuildWorldMcq(Transform parent)
        {
            var root = new GameObject("MCQ_WorldCanvas");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 1.65f, 2.2f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            root.AddComponent<FaceCameraBillboard>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 80;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1200f, 900f);
            root.transform.localScale = Vector3.one * 0.002f;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            var dim = CreateUiPanel(root.transform, "Card", stretch: true);
            dim.GetComponent<Image>().color = new Color(0.97f, 0.98f, 1f, 0.97f);

            var title = CreateUiTmp(root.transform, "Title", new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.96f),
                36, "Scenario", FontStyles.Bold, TextAlignmentOptions.Left);
            title.color = Color.black;
            var qid = CreateUiTmp(root.transform, "QuestionId", new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.88f),
                26, "MCS", FontStyles.Normal, TextAlignmentOptions.Left);
            qid.color = new Color(0.15f, 0.35f, 0.55f);
            var prompt = CreateUiTmp(root.transform, "Prompt", new Vector2(0.06f, 0.64f), new Vector2(0.94f, 0.80f),
                30, "Question", FontStyles.Normal, TextAlignmentOptions.Left);
            prompt.color = Color.black;

            var buttons = new Button[4];
            var labels = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
            {
                float yMax = 0.58f - i * 0.11f;
                float yMin = yMax - 0.095f;
                var btnGo = CreateUiPanel(root.transform, "Option" + i, stretch: false);
                var brt = btnGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.06f, yMin);
                brt.anchorMax = new Vector2(0.94f, yMax);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                btnGo.GetComponent<Image>().color = new Color(0.88f, 0.92f, 0.98f, 1f);
                buttons[i] = btnGo.AddComponent<Button>();
                labels[i] = CreateUiTmp(btnGo.transform, "Label", Vector2.zero, Vector2.one, 26, "Option",
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                labels[i].color = Color.black;
                Stretch(labels[i].rectTransform, 0.03f, 0.1f, 0.97f, 0.9f);
            }

            var feedback = CreateUiTmp(root.transform, "Feedback", new Vector2(0.06f, 0.08f), new Vector2(0.7f, 0.18f),
                24, string.Empty, FontStyles.Italic, TextAlignmentOptions.Left);
            feedback.color = new Color(0.1f, 0.4f, 0.2f);

            var closeGo = CreateUiPanel(root.transform, "Close", stretch: false);
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.76f, 0.05f);
            crt.anchorMax = new Vector2(0.94f, 0.15f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            closeGo.GetComponent<Image>().color = new Color(0.82f, 0.86f, 0.92f, 1f);
            var closeBtn = closeGo.AddComponent<Button>();
            var closeLabel = CreateUiTmp(closeGo.transform, "Label", Vector2.zero, Vector2.one, 28, "Close",
                FontStyles.Bold, TextAlignmentOptions.Center);
            closeLabel.color = Color.black;

            var hide = new List<GameObject>();
            AddIfExists(hide, "DemoIntroPlaque");
            AddIfExists(hide, "Mural_Warehouse");
            AddIfExists(hide, "Mural_FourDrums");
            AddIfExists(hide, "ZoneTitle_SC01");
            AddIfExists(hide, "ZoneTitle_SC02");
            AddIfExists(hide, "Intro_InstructionPanel");
            var rack = GameObject.Find("DrumRack_SC01");
            if (rack != null)
            {
                foreach (Transform c in rack.transform)
                {
                    if (!c.name.StartsWith("Drum_"))
                        continue;
                    var board = c.Find("LabelBoard");
                    if (board != null)
                        hide.Add(board.gameObject);
                }
            }

            var presenter = root.AddComponent<McqPresenter>();
            SetPrivate(presenter, "panelRoot", root);
            SetPrivate(presenter, "titleText", title);
            SetPrivate(presenter, "questionIdText", qid);
            SetPrivate(presenter, "promptText", prompt);
            SetPrivate(presenter, "feedbackText", feedback);
            SetPrivate(presenter, "optionButtons", buttons);
            SetPrivate(presenter, "optionLabels", labels);
            SetPrivate(presenter, "closeButton", closeBtn);
            SetPrivate(presenter, "closeButtonLabel", closeLabel);
            SetPrivate(presenter, "hideWhileOpen", hide.ToArray());

            root.SetActive(false);
            return presenter;
        }

        private static void WireFlow(
            GameObject systems,
            TrainingFlowController flow,
            IntroInstructionPresenter introUi,
            McqPresenter mcq)
        {
            var director = systems.GetComponent<ScenarioIntroDirector>();
            if (director == null)
                director = systems.AddComponent<ScenarioIntroDirector>();

            var rack = GameObject.Find("DrumRack_SC01");
            var highlights = new List<GameObject>();
            if (rack != null)
            {
                foreach (Transform c in rack.transform)
                {
                    if (c.name.StartsWith("Drum_"))
                        highlights.Add(c.gameObject);
                }
            }

            var contents = new[]
            {
                new ScenarioIntroDirector.IntroContent
                {
                    scenarioId = "SC01",
                    greetingBn = "আসসালামু আলাইকুম",
                    title = "INSTRUCTIONS",
                    englishBody =
                        "VOICE-OVER: Select the correctly labeled drum based on the Safety Data Sheet (SDS).",
                    banglaBody =
                        "ভয়েস-ওভার: সেফটি ডাটা শিট (SDS) অনুসারে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।",
                    durationSeconds = 10f,
                    highlightTargets = highlights.ToArray()
                },
                new ScenarioIntroDirector.IntroContent
                {
                    scenarioId = "SC02",
                    greetingBn = string.Empty,
                    title = "INSTRUCTIONS",
                    englishBody =
                        "Choose the correct storage compartment for the flammable solvent (compatibility segregation).",
                    banglaBody =
                        "দাহ্য দ্রাবকের জন্য সঠিক স্টোরেজ কম্পার্টমেন্ট নির্বাচন করুন (সামঞ্জস্য পৃথকীকরণ)।",
                    durationSeconds = 10f,
                    highlightTargets = System.Array.Empty<GameObject>()
                }
            };

            director.Configure(introUi, contents);
            SetPrivate(director, "includeGreetingOnlyOnFirstScenario", true);

            if (flow == null)
                flow = systems.GetComponent<TrainingFlowController>();
            SetPrivate(flow, "mcqPresenter", mcq);
            SetPrivate(flow, "introDirector", director);
        }

        private static void SoftenLighting()
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.98f, 0.94f);
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            if (RenderSettings.skybox == null)
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.28f);
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            // Prefer XR UI module for Quest ray; otherwise Input System for desk.
            var xrType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrType != null)
            {
                if (es.GetComponent(xrType) == null)
                    es.gameObject.AddComponent(xrType);
#if ENABLE_INPUT_SYSTEM
                var inputModule = es.GetComponent<InputSystemUIInputModule>();
                if (inputModule != null)
                    Object.DestroyImmediate(inputModule);
#endif
            }
#if ENABLE_INPUT_SYSTEM
            else if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }

        private static GameObject CreateBox(
            Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Shader lit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (lit != null)
                go.GetComponent<Renderer>().sharedMaterial = new Material(lit) { color = color };
            return go;
        }

        private static TextMeshPro CreateWorldTmp(
            Transform parent, string name, Vector3 pos, string text, float fontSize, Color color)
        {
            DestroyNamed(name);
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.15f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static GameObject CreateUiPanel(Transform parent, string name, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            if (stretch)
                Stretch(go.GetComponent<RectTransform>(), 0, 0, 1, 1);
            return go;
        }

        private static TextMeshProUGUI CreateUiTmp(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            string text,
            FontStyles style,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = align;
            if (name == "Greeting" || name == "Bangla" || ContainsBanglaChars(text))
            {
                var bangla = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/Art/Fonts/NotoSansBengali SDF.asset");
                if (bangla != null)
                    tmp.font = bangla;
            }

            Stretch(tmp.rectTransform, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
            return tmp;
        }

        private static bool ContainsBanglaChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (var c in text)
            {
                if (c >= '\u0980' && c <= '\u09FF')
                    return true;
            }

            return false;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AddIfExists(List<GameObject> list, string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                list.Add(go);
        }

        private static void DestroyNamed(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static void DestroyInactiveOrActive(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == name && go.scene.IsValid())
                    Object.DestroyImmediate(go);
            }
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

#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Flow;
using ChemicalSafety.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ChemicalSafety.Editor
{
    public static class McqUiClarityFixer
    {
        [MenuItem("Chemical Safety/Fix MCQ UI Clarity (Fullscreen + Drum Labels)")]
        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/ChemicalSafety_Demo.unity",
                OpenSceneMode.Single);

            var flow = Object.FindAnyObjectByType<TrainingFlowController>();
            var systems = flow != null ? flow.gameObject : GameObject.Find("ChemicalSafety_Systems");

            DestroyIfExists("MCQ_WorldCanvas");
            DestroyInactiveOrActive("MCQ_FullscreenCanvas");

            EnsureZoneHeaders();
            var hideList = CollectHideWhileOpenTargets();
            var presenter = BuildFullscreenMcq(
                systems != null ? systems.transform : null,
                hideList.ToArray());

            if (flow != null)
                SetPrivate(flow, "mcqPresenter", presenter);

            RebuildDrumLabels();
            // Re-collect after labels/zone headers exist
            hideList = CollectHideWhileOpenTargets();
            SetPrivate(presenter, "hideWhileOpen", hideList.ToArray());

            EnsureEventSystemForOverlayUi();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChemicalSafety] Fullscreen MCQ + upright labels + zone headers. Play → E → D → Next (or 2).");
        }

        private static McqPresenter BuildFullscreenMcq(Transform parent, GameObject[] hideWhileOpen)
        {
            var root = new GameObject("MCQ_FullscreenCanvas");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var dim = CreatePanel(root.transform, "Dim", stretch: true);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            dim.GetComponent<Image>().raycastTarget = true;

            var card = CreatePanel(root.transform, "Card", stretch: false);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.12f, 0.12f);
            cardRt.anchorMax = new Vector2(0.88f, 0.88f);
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0.98f, 0.99f, 1f, 0.98f);

            var title = CreateTmp(card.transform, "Title", new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), 40, "Scenario", FontStyles.Bold);
            var qid = CreateTmp(card.transform, "QuestionId", new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.88f), 28, "MCS", FontStyles.Normal);
            qid.color = new Color(0.15f, 0.35f, 0.55f);
            var prompt = CreateTmp(card.transform, "Prompt", new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.80f), 34, "Question", FontStyles.Normal);

            var buttons = new Button[4];
            var labels = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
            {
                float yMax = 0.60f - i * 0.11f;
                float yMin = yMax - 0.09f;
                var btnGo = CreatePanel(card.transform, "Option" + i, stretch: false);
                var brt = btnGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.06f, yMin);
                brt.anchorMax = new Vector2(0.94f, yMax);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                btnGo.GetComponent<Image>().color = new Color(0.90f, 0.94f, 0.98f, 1f);
                buttons[i] = btnGo.AddComponent<Button>();
                var colors = buttons[i].colors;
                colors.highlightedColor = new Color(0.75f, 0.88f, 1f);
                colors.pressedColor = new Color(0.65f, 0.82f, 0.95f);
                buttons[i].colors = colors;
                labels[i] = CreateTmp(btnGo.transform, "Label", Vector2.zero, Vector2.one, 30, "Option", FontStyles.Normal);
                Stretch(labels[i].rectTransform, 0.03f, 0.05f, 0.97f, 0.95f);
            }

            var feedback = CreateTmp(card.transform, "Feedback", new Vector2(0.05f, 0.08f), new Vector2(0.72f, 0.18f), 26, string.Empty, FontStyles.Italic);
            feedback.color = new Color(0.1f, 0.35f, 0.15f);

            var closeGo = CreatePanel(card.transform, "Close", stretch: false);
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.78f, 0.06f);
            crt.anchorMax = new Vector2(0.95f, 0.16f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            closeGo.GetComponent<Image>().color = new Color(0.85f, 0.88f, 0.92f, 1f);
            var closeBtn = closeGo.AddComponent<Button>();
            var closeLabel = CreateTmp(closeGo.transform, "Label", Vector2.zero, Vector2.one, 28, "Close", FontStyles.Bold);
            closeLabel.alignment = TextAlignmentOptions.Center;

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
            SetPrivate(presenter, "hideWhileOpen", hideWhileOpen ?? new GameObject[0]);

            root.SetActive(false);
            return presenter;
        }

        private static void EnsureZoneHeaders()
        {
            var rack = GameObject.Find("DrumRack_SC01");
            if (rack != null)
            {
                EnsureZoneHeader(
                    "ZoneHeader_SC01",
                    rack.transform.position + new Vector3(0f, 2.4f, 0f),
                    "SC01 — Choose correct drum (A–D)");
            }

            var zone = GameObject.Find("StorageZone_SC02");
            if (zone != null)
            {
                EnsureZoneHeader(
                    "ZoneHeader_SC02",
                    zone.transform.position + new Vector3(0f, 2.2f, 0f),
                    "SC02 — Chemical Storage");
            }
        }

        private static void EnsureZoneHeader(string name, Vector3 worldPos, string text)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(name);
            root.transform.position = worldPos;
            // Face typical XR Origin / camera looking toward +Z content from -Z
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = Vector3.zero;
            board.transform.localScale = new Vector3(2.4f, 0.45f, 0.04f);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var br = board.GetComponent<Renderer>();
            if (br != null && lit != null)
                br.sharedMaterial = new Material(lit) { color = new Color(0.08f, 0.12f, 0.2f) };

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            labelGo.transform.localScale = Vector3.one * 0.12f;
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }

        private static List<GameObject> CollectHideWhileOpenTargets()
        {
            var list = new List<GameObject>();
            AddIfFound(list, "DemoIntroPlaque");
            AddIfFound(list, "ZoneHeader_SC01");
            AddIfFound(list, "ZoneHeader_SC02");

            var rack = GameObject.Find("DrumRack_SC01");
            if (rack != null)
            {
                foreach (Transform child in rack.transform)
                {
                    if (!child.name.StartsWith("Drum_"))
                        continue;
                    var board = child.Find("LabelBoard");
                    if (board != null)
                        list.Add(board.gameObject);
                }
            }

            return list;
        }

        private static void AddIfFound(List<GameObject> list, string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                list.Add(go);
        }

        private static void RebuildDrumLabels()
        {
            var rack = GameObject.Find("DrumRack_SC01");
            if (rack == null)
                return;

            foreach (Transform child in rack.transform)
            {
                if (!child.name.StartsWith("Drum_"))
                    continue;

                DestroyChildNamed(child, "Label");
                DestroyChildNamed(child, "LabelBoard");

                string text;
                if (child.name.Contains("Faded"))
                    text = "1\nFaded Label\nWRONG";
                else if (child.name.Contains("No Label"))
                    text = "2\nNo Label\nWRONG";
                else if (child.name.Contains("GHS!="))
                    text = "3\nGHS != SDS\nWRONG";
                else if (child.name.Contains("Correct") || child.name.Contains("GHS=SDS"))
                    text = "4\nGHS = SDS\nCORRECT";
                else
                    text = child.name;

                // Root cancels drum non-uniform scale so TMP stays upright/readable.
                var root = new GameObject("LabelBoard");
                root.transform.SetParent(child, false);
                root.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var cs = child.localScale;
                root.transform.localScale = new Vector3(
                    1f / Mathf.Max(0.01f, cs.x),
                    1f / Mathf.Max(0.01f, cs.y),
                    1f / Mathf.Max(0.01f, cs.z));

                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Board";
                board.transform.SetParent(root.transform, false);
                board.transform.localPosition = Vector3.zero;
                board.transform.localScale = new Vector3(1.15f, 0.7f, 0.04f);
                Object.DestroyImmediate(board.GetComponent<Collider>());
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var r = board.GetComponent<Renderer>();
                if (r != null && lit != null)
                    r.sharedMaterial = new Material(lit) { color = new Color(0.05f, 0.05f, 0.08f) };

                // Sibling of thin board — uniform scale, not crushed by board Z.
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(root.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                labelGo.transform.localScale = Vector3.one * 0.1f;
                var tmp = labelGo.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = 7;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.textWrappingMode = TextWrappingModes.Normal;
            }

            var hotspot = GameObject.Find("Hotspot_SC01");
            if (hotspot != null)
            {
                DestroyChildNamed(hotspot.transform, "Label");
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(hotspot.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                labelGo.transform.localScale = Vector3.one * 0.2f;
                var tmp = labelGo.AddComponent<TextMeshPro>();
                tmp.text = "SC01\nSELECT";
                tmp.fontSize = 8;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.cyan;
            }
        }

        private static void DestroyChildNamed(Transform parent, string childName)
        {
            // Destroy all matches — leftover LabelBoards from prior fixer runs can stack.
            while (true)
            {
                var old = parent.Find(childName);
                if (old == null)
                    break;
                Object.DestroyImmediate(old.gameObject);
            }
        }

        private static void EnsureEventSystemForOverlayUi()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            var xrType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrType != null)
            {
                var xr = es.GetComponent(xrType);
                if (xr != null)
                    Object.DestroyImmediate(xr);
            }

#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
                es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void DestroyIfExists(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go != null && go.name == name && go.scene.IsValid())
                    Object.DestroyImmediate(go);
            }
        }

        private static void DestroyInactiveOrActive(string name)
        {
            DestroyIfExists(name);
            var found = GameObject.Find(name);
            if (found != null)
                Object.DestroyImmediate(found);
        }

        private static GameObject CreatePanel(Transform parent, string name, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            if (stretch)
                Stretch(go.GetComponent<RectTransform>(), 0, 0, 1, 1);
            return go;
        }

        private static TextMeshProUGUI CreateTmp(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            string text,
            FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(tmp.rectTransform, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
            return tmp;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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

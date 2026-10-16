#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Ensures world-space MCQ/Intro canvases receive Quest controller rays.
    /// </summary>
    public static class QuestUiRayFixer
    {
        [MenuItem("Chemical Safety/Fix Quest UI Ray Select")]
        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/ChemicalSafety_Demo.unity",
                OpenSceneMode.Single);

            EnsureTrackedRaycaster("MCQ_WorldCanvas");
            EnsureTrackedRaycaster("Intro_InstructionPanel");
            EnsureXrEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ChemicalSafety] Quest UI ray fix applied (TrackedDeviceGraphicRaycaster + XRUIInputModule). Rebuild APK.");
        }

        private static void EnsureTrackedRaycaster(string objectName)
        {
            GameObject go = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate != null
                    && candidate.name == objectName
                    && candidate.scene.IsValid())
                {
                    go = candidate;
                    break;
                }
            }

            if (go == null)
            {
                Debug.LogWarning("[ChemicalSafety] Missing UI root: " + objectName);
                return;
            }

            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();

            if (go.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        private static void EnsureXrEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            if (es.GetComponent<XRUIInputModule>() == null)
                es.gameObject.AddComponent<XRUIInputModule>();

#if ENABLE_INPUT_SYSTEM
            var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule != null)
                Object.DestroyImmediate(inputModule);
#endif
        }
    }
}
#endif

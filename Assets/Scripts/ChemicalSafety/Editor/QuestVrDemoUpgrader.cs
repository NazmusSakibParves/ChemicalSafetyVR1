#if UNITY_EDITOR
using System.Linq;
using ChemicalSafety.Data;
using ChemicalSafety.Flow;
using ChemicalSafety.Interaction;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Upgrades ChemicalSafety_Demo for real VR + richer boss demo content.
    /// </summary>
    public static class QuestVrDemoUpgrader
    {
        private const string ScenePath = "Assets/Scenes/ChemicalSafety_Demo.unity";
        private const string XrOriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        [MenuItem("Chemical Safety/Upgrade Demo For Quest VR")]
        public static void Upgrade()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sc01 = Sc01ContentFactory.CreateOrUpdateSc01();
            var sc02 = Sc02ContentFactory.CreateOrUpdateSc02();
            var flow = Object.FindAnyObjectByType<TrainingFlowController>();
            if (flow == null)
            {
                Debug.LogError("[ChemicalSafety] TrainingFlowController missing. Build demo scene first.");
                return;
            }

            ReplaceWithXrOriginPrefab();
            EnsureTeleportationProvider();
            LabelDrumsAndClipboard();
            EnsureIntroPlaque();
            CreateOrUpdateSc02Hotspot(sc02, flow);
            EnsurePlayerSpawnSc02(flow);
            EnsureSc01HotspotWired(sc01, flow);
            EnableOpenXrQuestProfiles();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChemicalSafety] Quest VR upgrade complete. Connect Quest Link, Play, teleport, ray-select hotspots.");
        }

        private static void ReplaceWithXrOriginPrefab()
        {
            var old = GameObject.Find("XR_Origin_Demo");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[ChemicalSafety] XR Origin prefab not found at " + XrOriginPrefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "XR Origin (XR Rig)";
            instance.transform.position = old != null
                ? old.transform.position
                : new Vector3(0f, 0f, -2f);

            if (old != null)
                Object.DestroyImmediate(old);

        }

        private static void EnsureTeleportationProvider()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;

            if (origin.GetComponentInChildren<TeleportationProvider>(true) != null)
                return;

            var loco = origin.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>(true);
            var host = loco != null ? loco.gameObject : origin.gameObject;
            host.AddComponent<TeleportationProvider>();
        }

        private static void LabelDrumsAndClipboard()
        {
            var rack = GameObject.Find("DrumRack_SC01");
            if (rack != null)
            {
                foreach (Transform child in rack.transform)
                {
                    if (!child.name.StartsWith("Drum_"))
                        continue;

                    string text;
                    if (child.name.Contains("Faded"))
                        text = "1 Faded Label\n(WRONG)";
                    else if (child.name.Contains("No Label"))
                        text = "2 No Label\n(WRONG)";
                    else if (child.name.Contains("GHS!="))
                        text = "3 GHS != SDS\n(WRONG)";
                    else if (child.name.Contains("Correct") || child.name.Contains("GHS=SDS"))
                        text = "4 GHS = SDS\n(CORRECT)";
                    else
                        text = child.name;

                    EnsureWorldLabel(child, "Label", text, new Vector3(0f, 1.1f, 0f));
                }
            }

            var clipboard = GameObject.Find("SDS_Clipboard");
            if (clipboard != null)
                EnsureWorldLabel(clipboard.transform, "Label", "SDS Clipboard", new Vector3(0f, 0.8f, 0f));

            var hotspot = GameObject.Find("Hotspot_SC01");
            if (hotspot != null)
                EnsureWorldLabel(hotspot.transform, "Label", "SC01 Hotspot\n(Ray select)", new Vector3(0f, 0.6f, 0f));
        }

        private static void EnsureIntroPlaque()
        {
            var existing = GameObject.Find("DemoIntroPlaque");
            if (existing != null)
                return;

            var plaque = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plaque.name = "DemoIntroPlaque";
            plaque.transform.position = new Vector3(0f, 1.7f, 1.2f);
            plaque.transform.localScale = new Vector3(1.6f, 0.7f, 1f);
            Object.DestroyImmediate(plaque.GetComponent<MeshCollider>());

            EnsureWorldLabel(plaque.transform, "Title", "Chemical Safety VR\nDemo — SC01 + SC02", Vector3.zero, 4f);
        }

        private static void CreateOrUpdateSc02Hotspot(ScenarioDefinition sc02, TrainingFlowController flow)
        {
            var rack = GameObject.Find("DrumRack_SC01");
            var parent = rack != null ? rack.transform.parent : null;

            var zone = GameObject.Find("StorageZone_SC02");
            if (zone == null)
            {
                zone = new GameObject("StorageZone_SC02");
                if (parent != null)
                    zone.transform.SetParent(parent, false);
                zone.transform.position = new Vector3(3.5f, 0f, 3f);

                // Simple visual: 4 compartment boxes
                var colors = new[]
                {
                    new Color(0.9f, 0.4f, 0.1f),
                    new Color(0.2f, 0.5f, 0.9f),
                    new Color(0.8f, 0.2f, 0.2f),
                    new Color(0.3f, 0.7f, 0.3f)
                };
                var names = new[] { "Oxidizer", "Flammable", "Acid", "Base" };
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                for (var i = 0; i < 4; i++)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Compartment_" + names[i];
                    box.transform.SetParent(zone.transform, false);
                    box.transform.localPosition = new Vector3(-1.2f + i * 0.8f, 0.4f, 0f);
                    box.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
                    var r = box.GetComponent<Renderer>();
                    if (r != null && lit != null)
                        r.sharedMaterial = new Material(lit) { color = colors[i] };
                    EnsureWorldLabel(box.transform, "Label", names[i], new Vector3(0f, 0.7f, 0f), 2.5f);
                }
            }

            var hotspot = GameObject.Find("Hotspot_SC02");
            if (hotspot == null)
            {
                hotspot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hotspot.name = "Hotspot_SC02";
                hotspot.transform.SetParent(zone.transform, false);
                hotspot.transform.localPosition = new Vector3(0f, 1.3f, -0.9f);
                hotspot.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var r = hotspot.GetComponent<Renderer>();
                if (r != null && lit != null)
                    r.sharedMaterial = new Material(lit) { color = new Color(0.95f, 0.55f, 0.1f) };
            }

            if (hotspot.GetComponent<XRSimpleInteractable>() == null)
                hotspot.AddComponent<XRSimpleInteractable>().selectMode = InteractableSelectMode.Single;

            var hi = hotspot.GetComponent<HotspotInteractable>();
            if (hi == null)
                hi = hotspot.AddComponent<HotspotInteractable>();
            SetPrivate(hi, "scenario", sc02);
            SetPrivate(hi, "flowController", flow);
            SetPrivate(hi, "requireUnlocked", true);

            EnsureWorldLabel(hotspot.transform, "Label", "SC02 Hotspot\n(Unlock after SC01)", new Vector3(0f, 0.6f, 0f));

            EnsurePlayerSpawnSc02(flow);

            // Extra E-key activator for SC02 (key 2)
            var keyHost = GameObject.Find("EditorHotspotKeys");
            if (keyHost == null)
            {
                keyHost = new GameObject("EditorHotspotKeys");
            }

            var existing = keyHost.GetComponent<EditorHotspotDualKeyActivator>();
            if (existing == null)
                existing = keyHost.AddComponent<EditorHotspotDualKeyActivator>();

            var sc01 = GameObject.Find("Hotspot_SC01")?.GetComponent<HotspotInteractable>();
            existing.Configure(sc01, hi);
        }

        /// <summary>
        /// Standing point facing Hotspot_SC02 — used when Next teleports after SC01.
        /// </summary>
        private static void EnsurePlayerSpawnSc02(TrainingFlowController flow)
        {
            var zone = GameObject.Find("StorageZone_SC02");
            var hotspot = GameObject.Find("Hotspot_SC02");

            var spawnGo = GameObject.Find("PlayerSpawn_SC02");
            if (spawnGo == null)
            {
                spawnGo = new GameObject("PlayerSpawn_SC02");
                if (zone != null)
                    spawnGo.transform.SetParent(zone.transform, true);
            }

            Vector3 pos;
            Quaternion rot;
            if (hotspot != null)
            {
                // Stand ~1.5m in front of the hotspot (toward -Z local of zone / camera approach).
                pos = hotspot.transform.position + new Vector3(0f, 0f, -1.5f);
                pos.y = 0f;
                rot = Quaternion.LookRotation(
                    (hotspot.transform.position - pos).normalized,
                    Vector3.up);
            }
            else if (zone != null)
            {
                pos = zone.transform.position + new Vector3(0f, 0f, -1.5f);
                rot = Quaternion.LookRotation(Vector3.forward);
            }
            else
            {
                pos = new Vector3(3.5f, 0f, 1.5f);
                rot = Quaternion.identity;
            }

            spawnGo.transform.SetPositionAndRotation(pos, rot);

            var origin = GameObject.Find("XR Origin (XR Rig)")
                ?? GameObject.Find("XR Origin");
            if (origin != null)
                SetPrivate(flow, "playerRoot", origin.transform);
            SetPrivate(flow, "playerSpawnSc02", spawnGo.transform);
        }

        private static void EnsureSc01HotspotWired(ScenarioDefinition sc01, TrainingFlowController flow)
        {
            var hotspot = GameObject.Find("Hotspot_SC01");
            if (hotspot == null)
                return;

            var hi = hotspot.GetComponent<HotspotInteractable>();
            if (hi == null)
                hi = hotspot.AddComponent<HotspotInteractable>();
            SetPrivate(hi, "scenario", sc01);
            SetPrivate(hi, "flowController", flow);
        }

        private static void EnableOpenXrQuestProfiles()
        {
            try
            {
                var featureAssets = AssetDatabase.FindAssets("t:OpenXRInteractionFeature");
                var enabledCount = 0;
                foreach (var guid in featureAssets)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var feature = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (feature == null)
                        continue;

                    var name = feature.GetType().Name;
                    if (name.IndexOf("Touch", System.StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("MetaQuest", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var enabledProp = feature.GetType().GetProperty("enabled");
                    if (enabledProp == null || !enabledProp.CanWrite)
                        continue;

                    enabledProp.SetValue(feature, true, null);
                    EditorUtility.SetDirty(feature);
                    enabledCount++;
                }

                Debug.Log("[ChemicalSafety] OpenXR touch-related features enabled: " + enabledCount
                          + ". Also check Project Settings > XR Plug-in Management > OpenXR.");
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ChemicalSafety] Could not auto-enable OpenXR profiles: " + ex.Message);
            }
        }

        private static void EnsureWorldLabel(Transform parent, string childName, string text, Vector3 localPos, float scale = 3f)
        {
            var existing = parent.Find(childName);
            GameObject labelGo;
            TextMeshPro tmp;
            if (existing == null)
            {
                labelGo = new GameObject(childName);
                labelGo.transform.SetParent(parent, false);
                tmp = labelGo.AddComponent<TextMeshPro>();
            }
            else
            {
                labelGo = existing.gameObject;
                tmp = labelGo.GetComponent<TextMeshPro>();
                if (tmp == null)
                    tmp = labelGo.AddComponent<TextMeshPro>();
            }

            labelGo.transform.localPosition = localPos;
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.transform.localScale = Vector3.one * 0.08f * (scale / 3f);
            tmp.text = text;
            tmp.fontSize = 8;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
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

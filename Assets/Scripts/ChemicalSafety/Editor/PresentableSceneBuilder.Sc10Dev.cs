#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 10 developer menus: start Play directly in SC10, and an editor preview that renders screenshots without Play mode.</summary>
    public static partial class PresentableSceneBuilder
    {
        private const string Sc10OnlyKey = "ChemSafetySc10Only";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 10")]
        private static void ToggleSc10Only()
        {
            EditorPrefs.SetBool(Sc10OnlyKey, !EditorPrefs.GetBool(Sc10OnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 10", true)]
        private static bool ToggleSc10OnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 10", EditorPrefs.GetBool(Sc10OnlyKey, false));
            return true;
        }

        // Reads Temp/sc10_views.txt: one view per line "name x y z yawDeg pitchDeg fov" (x and z relative to the SC10 origin, y absolute
        // eye height, game yaw: the preview turns it itself). Writes Temp/Smoke/sc10_<name>.png.
        [MenuItem("Chemical Safety/Presentable/Preview SC10 (screenshots)")]
        public static void PreviewSc10()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Presentable] Exit Play mode first.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var states = new Dictionary<GameObject, bool>();
            GameObject env10 = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (!r.name.StartsWith("Environment_SC"))
                    continue;
                states[r] = r.activeSelf;
                if (r.name == "Environment_SC10")
                    env10 = r;
            }

            if (env10 == null)
            {
                Debug.LogError("[Presentable] Environment_SC10 missing; build the scene first.");
                return;
            }

            foreach (var kv in states)
                kv.Key.SetActive(kv.Key == env10);

            var oldMode = RenderSettings.ambientMode;
            var oldSky = RenderSettings.ambientSkyColor;
            var oldEq = RenderSettings.ambientEquatorColor;
            var oldGr = RenderSettings.ambientGroundColor;
            var oldFog = RenderSettings.fogColor;
            var oldFs = RenderSettings.fogStartDistance;
            var oldFe = RenderSettings.fogEndDistance;
            var oldRef = RenderSettings.reflectionIntensity;
            Light sun = null;
            var oldSun = 0f;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    oldSun = l.intensity;
                    break;
                }
            }

            var mso = new SerializedObject(env10.GetComponent<ChemicalSafety.Presentable.Sc03Mood>());
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = mso.FindProperty("ambientSky").colorValue;
            RenderSettings.ambientEquatorColor = mso.FindProperty("ambientEquator").colorValue;
            RenderSettings.ambientGroundColor = mso.FindProperty("ambientGround").colorValue;
            RenderSettings.fogColor = mso.FindProperty("fogColor").colorValue;
            RenderSettings.fogStartDistance = mso.FindProperty("fogStart").floatValue;
            RenderSettings.fogEndDistance = mso.FindProperty("fogEnd").floatValue;
            RenderSettings.reflectionIntensity = mso.FindProperty("reflectionIntensity").floatValue;
            if (sun != null)
                sun.intensity = mso.FindProperty("sunIntensity").floatValue;

            var views = new List<string>();
            var viewFile = "Temp/sc10_views.txt";
            if (File.Exists(viewFile))
                views.AddRange(File.ReadAllLines(viewFile));
            else
            {
                views.Add("spill 0.4 1.6 1.3 90 14 72");
                views.Add("kit 2.75 1.55 0.45 90 4 62");
                views.Add("hood -2.62 1.55 -0.9 0 4 70");
                views.Add("ppe -3.35 1.55 0.35 -90 4 70");
                views.Add("wide 0.4 2.2 3.1 -40 14 90");
            }

            foreach (var ps in env10.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Directory.CreateDirectory("Temp/Smoke");
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            foreach (var line in views)
            {
                var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 7)
                    continue;
                float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
                cam.transform.position = new Vector3(Sc10Ox + F(1), F(2), F(3));
                cam.transform.rotation = Quaternion.Euler(F(5), 180f - F(4), 0f);
                cam.fieldOfView = F(6);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/Smoke/sc10_" + p[0] + ".png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            foreach (var kv in states)
                kv.Key.SetActive(kv.Value);
            RenderSettings.ambientMode = oldMode;
            RenderSettings.ambientSkyColor = oldSky;
            RenderSettings.ambientEquatorColor = oldEq;
            RenderSettings.ambientGroundColor = oldGr;
            RenderSettings.fogColor = oldFog;
            RenderSettings.fogStartDistance = oldFs;
            RenderSettings.fogEndDistance = oldFe;
            RenderSettings.reflectionIntensity = oldRef;
            if (sun != null)
                sun.intensity = oldSun;
            Debug.Log("[SC10] preview written to Temp/Smoke/sc10_*.png");
        }
    }
}
#endif

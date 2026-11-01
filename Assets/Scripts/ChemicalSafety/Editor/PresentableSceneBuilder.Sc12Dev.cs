#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 12 developer menu: an editor preview that renders screenshots without Play mode.</summary>
    public static partial class PresentableSceneBuilder
    {
        private const string Sc12OnlyKey = "ChemSafetySc12Only";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 12")]
        private static void ToggleSc12Only()
        {
            EditorPrefs.SetBool(Sc12OnlyKey, !EditorPrefs.GetBool(Sc12OnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 12", true)]
        private static bool ToggleSc12OnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 12", EditorPrefs.GetBool(Sc12OnlyKey, false));
            return true;
        }

        // Reads Temp/sc12_views.txt: one view per line "name x y z yawDeg pitchDeg fov" (x and z relative to the SC12 origin, y absolute
        // eye height, game yaw: the preview turns it itself). Writes Temp/Smoke/sc12_<name>.png.
        [MenuItem("Chemical Safety/Presentable/Preview SC12 (screenshots)")]
        public static void PreviewSc12()
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
            GameObject env12 = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (!r.name.StartsWith("Environment_SC"))
                    continue;
                states[r] = r.activeSelf;
                if (r.name == "Environment_SC12")
                    env12 = r;
            }

            if (env12 == null)
            {
                Debug.LogError("[Presentable] Environment_SC12 missing; build the scene first.");
                return;
            }

            foreach (var kv in states)
                kv.Key.SetActive(kv.Key == env12);

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

            var mso = new SerializedObject(env12.GetComponent<ChemicalSafety.Presentable.Sc03Mood>());
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
            var viewFile = "Temp/sc12_views.txt";
            if (File.Exists(viewFile))
                views.AddRange(File.ReadAllLines(viewFile));
            else
            {
                views.Add("start 0.1 1.6 3.0 0 3 75");
                views.Add("lockers 0.0 1.5 -1.4 0 4 60");
                views.Add("goggles -1.17 1.2 -2.3 0 8 45");
                views.Add("gloves -0.39 1.2 -2.3 0 8 45");
                views.Add("resp 0.39 1.2 -2.3 0 8 45");
                views.Add("aprons 1.17 1.3 -2.3 0 8 45");
                views.Add("left -0.5 1.6 0.8 -80 2 75");
                views.Add("window 0.5 1.6 0.2 85 2 80");
            }

            foreach (var ps in env12.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Directory.CreateDirectory("Temp/Smoke");
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
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
                var outside = env12.transform.Find("Room/Outside");
                if (outside != null)
                    outside.gameObject.SetActive(false);
                cam.transform.position = new Vector3(Sc12Ox + F(1), F(2), F(3));
                cam.transform.rotation = Quaternion.Euler(F(5), 180f - F(4), 0f);
                cam.fieldOfView = F(6);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/Smoke/sc12_" + p[0] + ".png", tex.EncodeToPNG());
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
            Debug.Log("[SC12] preview written to Temp/Smoke/sc12_*.png");
        }
    }
}
#endif

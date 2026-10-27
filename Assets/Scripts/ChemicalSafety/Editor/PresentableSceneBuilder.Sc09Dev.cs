#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 9 developer menus: start Play directly in SC09, and an editor preview that renders screenshots without Play mode.</summary>
    public static partial class PresentableSceneBuilder
    {
        private const string Sc09OnlyKey = "ChemSafetySc09Only";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 9")]
        private static void ToggleSc09Only()
        {
            EditorPrefs.SetBool(Sc09OnlyKey, !EditorPrefs.GetBool(Sc09OnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 9", true)]
        private static bool ToggleSc09OnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 9", EditorPrefs.GetBool(Sc09OnlyKey, false));
            return true;
        }

        // Reads Temp/sc09_views.txt: one view per line "name x y z yawDeg pitchDeg fov" (x and z relative to the SC09 origin, y absolute
        // eye height, game yaw: the preview turns it itself). Writes Temp/Smoke/sc09_<name>.png.
        [MenuItem("Chemical Safety/Presentable/Preview SC09 (screenshots)")]
        public static void PreviewSc09()
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
            GameObject env9 = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (!r.name.StartsWith("Environment_SC"))
                    continue;
                states[r] = r.activeSelf;
                if (r.name == "Environment_SC09")
                    env9 = r;
            }

            if (env9 == null)
            {
                Debug.LogError("[Presentable] Environment_SC09 missing; build the scene first.");
                return;
            }

            foreach (var kv in states)
                kv.Key.SetActive(kv.Key == env9);

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

            var mso = new SerializedObject(env9.GetComponent<ChemicalSafety.Presentable.Sc03Mood>());
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
            var viewFile = "Temp/sc09_views.txt";
            if (File.Exists(viewFile))
                views.AddRange(File.ReadAllLines(viewFile));
            else
            {
                views.Add("main 0 1.55 1.6 0 6 80");
                views.Add("bench 0 1.5 0.9 0 30 70");
                views.Add("shower -2.5 1.55 1.0 90 5 70");
                views.Add("wide 0 1.6 3.0 0 5 90");
            }

            var withWater = File.Exists("Temp/sc09_water.txt");
            foreach (var ps in env9.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (withWater)
                    ps.Simulate(4.5f, true, true);
            }

            // Temp/sc09_broken.txt: show the broken flask pose (shards scattered, puddle and splatter out), the scene is reloaded afterwards
            var brokenPose = File.Exists("Temp/sc09_broken.txt");
            if (brokenPose)
            {
                var broken = FindDeep(env9.transform, "Sc09Broken");
                var f2 = FindDeep(env9.transform, "Sc09Flask2");
                if (broken != null && f2 != null)
                {
                    f2.gameObject.SetActive(false);
                    broken.gameObject.SetActive(true);
                    broken.position = f2.position + new Vector3(-0.22f, 0f, 0.0f);
                    broken.rotation = Quaternion.Euler(0f, 25f, 0f);
                    var rnd = new System.Random(5);
                    foreach (var t in broken.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Sc09Shard"))
                        {
                            var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                            var r = 0.06f + (float)rnd.NextDouble() * 0.28f;
                            t.position = broken.position + new Vector3(Mathf.Cos(a) * r, 0.004f, Mathf.Sin(a) * r);
                            t.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                        }
                        else if (t.name.StartsWith("Sc09Splat"))
                        {
                            t.gameObject.SetActive(true);
                        }
                        else if (t.name == "Sc09Puddle")
                        {
                            t.gameObject.SetActive(true);
                            t.localScale = new Vector3(0.46f, 0.34f, 1f);
                        }
                    }
                }
            }

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
                cam.transform.position = new Vector3(Sc09Ox + F(1), F(2), F(3));
                cam.transform.rotation = Quaternion.Euler(F(5), 180f - F(4), 0f);
                cam.fieldOfView = F(6);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/Smoke/sc09_" + p[0] + ".png", tex.EncodeToPNG());
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
            if (brokenPose)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[SC09] preview written to Temp/Smoke/sc09_*.png");
        }
    }
}
#endif

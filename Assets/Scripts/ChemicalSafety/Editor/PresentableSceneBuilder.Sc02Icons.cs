using System.IO;
using UnityEditor;
using UnityEngine;
using ChemicalSafety.Presentable;

namespace ChemicalSafety.Editor
{
    public static partial class PresentableSceneBuilder
    {
        /// <summary>
        /// Scenario 2 callouts show each chemical as a transparent picture. There is no such art, so every container model is
        /// rendered once (isolated, transparent background) into Assets/Art/Presentable/2D/Sc02/Generated/Chem_N.png at scene build.
        /// </summary>
        private static Sprite[] BakeSc02ChemIcons(ContainerInspect inspect, PresentableArt art)
        {
            const int size = 512;
            var dir = ArtRoot + "/2D/Sc02/Generated";
            Directory.CreateDirectory(dir);
            var count = inspect.EntryCount;
            var icons = new Sprite[count];
            var far = new Vector3(0f, -900f, 0f);

            var camGo = new GameObject("Sc02IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            cam.fieldOfView = 24f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var lightGo = new GameObject("Sc02IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            for (var i = 0; i < count; i++)
            {
                var model = inspect.ModelOf(i);
                if (model == null)
                    continue;
                var clone = Object.Instantiate(model.gameObject);
                clone.SetActive(true);
                clone.transform.SetParent(null, false);
                clone.transform.rotation = model.rotation;
                clone.transform.localScale = model.lossyScale;
                clone.transform.position = far;
                foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true);
                foreach (var c in clone.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);

                var rs = clone.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0)
                {
                    Object.DestroyImmediate(clone);
                    continue;
                }

                var b = rs[0].bounds;
                foreach (var r in rs)
                    b.Encapsulate(r.bounds);
                var radius = b.extents.magnitude;
                var dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                // from the learner's side (+Z): that is where the labels and stickers are
                var dir3 = new Vector3(0.28f, 0.16f, 1f).normalized;
                cam.transform.position = b.center + dir3 * dist;
                cam.transform.LookAt(b.center);
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                var path = dir + "/Chem_" + i + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
                Object.DestroyImmediate(clone);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.maxTextureSize = 512;
                    imp.SaveAndReimport();
                }

                icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);

            art.sc02ChemIcons = icons;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return icons;
        }
    }
}

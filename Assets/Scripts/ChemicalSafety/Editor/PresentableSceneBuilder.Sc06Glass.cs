#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 6 glassware: real vessels (lathe meshes with wall thickness), a fresnel glass shader, tube-swept handles, liquids.</summary>
    public static partial class PresentableSceneBuilder
    {
        private static Material Sc06GlassMat(string name, Color tint, float clear = 0.07f, float edge = 0.75f)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;
            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("ChemicalSafety/Sc06Glass");
            if (shader == null)
            {
                Debug.LogWarning("[SC06] glass shader missing, using the plain transparent glass");
                return LabGlass();
            }

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }

            m.SetColor("_Tint", tint);
            m.SetFloat("_Clear", clear);
            m.SetFloat("_Edge", edge);
            m.SetFloat("_Reflect", 1.5f);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        private static Material Sc06Liquid(string name, Color c)
        {
            var m = Mat(name, c, 0.92f, 0f);
            MakeTransparent(m);
            return m;
        }

        // Surface of revolution. 'prof' runs from the bottom centre outwards and up to the rim, (radius, y). The inner wall is the same
        // profile pushed inwards by 'thick' so the vessel is a real hollow glass shell.
        private static Mesh Sc06Lathe(string assetName, Vector2[] prof, float thick, int seg = 32, bool hollow = true)
        {
            var path = GenDir + "/Mesh_" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();

            void Surface(List<Vector2> pts, bool flip)
            {
                var start = verts.Count;
                foreach (var p in pts)
                {
                    for (var j = 0; j <= seg; j++)
                    {
                        var a = j / (float)seg * Mathf.PI * 2f;
                        verts.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                    }
                }

                for (var i = 0; i < pts.Count - 1; i++)
                {
                    for (var j = 0; j < seg; j++)
                    {
                        var a = start + i * (seg + 1) + j;
                        var b = a + seg + 1;
                        if (flip)
                            tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                        else
                            tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            }

            var outer = new List<Vector2>(prof);
            Surface(outer, false);
            if (hollow)
            {
                var inner = new List<Vector2>();
                for (var i = prof.Length - 1; i >= 0; i--)
                {
                    var p = prof[i];
                    inner.Add(new Vector2(Mathf.Max(0f, p.x - thick), i == prof.Length - 1 ? p.y : p.y + thick));
                }

                // rim: outer last ring -> inner first ring
                var rim = new List<Vector2> { prof[prof.Length - 1], inner[0] };
                Surface(rim, true);
                // inner wall faces the axis
                Surface(inner, true);
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A tube swept along a planar path in the XY plane (handles, hooks).
        private static Mesh Sc06Tube(string assetName, List<Vector3> pts, float radius, int sides = 10)
        {
            var path = GenDir + "/Mesh_" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (var i = 0; i < pts.Count; i++)
            {
                var t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var n = new Vector3(-t.y, t.x, 0f);
                var b = Vector3.forward;
                for (var j = 0; j <= sides; j++)
                {
                    var a = j / (float)sides * Mathf.PI * 2f;
                    verts.Add(pts[i] + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * radius);
                }
            }

            for (var i = 0; i < pts.Count - 1; i++)
            {
                for (var j = 0; j < sides; j++)
                {
                    var a = i * (sides + 1) + j;
                    var c = a + sides + 1;
                    tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject Sc06Mesh(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // Beaker with a pouring lip and a thick rim bead.
        private static Mesh Sc06BeakerMesh() => Sc06Lathe("Sc06Beaker2", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.030f, 0f), new Vector2(0.036f, 0.004f), new Vector2(0.0385f, 0.02f), new Vector2(0.0395f, 0.10f),
            new Vector2(0.0405f, 0.108f), new Vector2(0.0425f, 0.1105f), new Vector2(0.0425f, 0.1135f), new Vector2(0.0405f, 0.1145f)
        }, 0.0022f, 36);

        // Measuring cup: tapered, with a spout lip; the handle is added by the caller.
        private static Mesh Sc06CupMesh() => Sc06Lathe("Sc06Cup", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.036f, 0f), new Vector2(0.040f, 0.004f), new Vector2(0.0455f, 0.05f), new Vector2(0.050f, 0.115f),
            new Vector2(0.0515f, 0.121f), new Vector2(0.0525f, 0.1245f), new Vector2(0.0505f, 0.1255f)
        }, 0.0024f, 36);

        private static Mesh Sc06ConicalMesh() => Sc06Lathe("Sc06Conical", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.050f, 0f), new Vector2(0.058f, 0.006f), new Vector2(0.050f, 0.06f), new Vector2(0.032f, 0.115f),
            new Vector2(0.0145f, 0.150f), new Vector2(0.0145f, 0.178f), new Vector2(0.0185f, 0.1815f), new Vector2(0.0185f, 0.185f), new Vector2(0.0150f, 0.186f)
        }, 0.0022f, 32);

        // Test tube: rounded bottom, rim bead (about 15 cm).
        private static Mesh Sc06TestTubeMesh()
        {
            var pts = new List<Vector2>();
            const float r = 0.0085f;
            for (var i = 0; i <= 6; i++)
            {
                var a = i / 6f * Mathf.PI * 0.5f;
                pts.Add(new Vector2(Mathf.Sin(a) * r, r - Mathf.Cos(a) * r));
            }

            pts.Add(new Vector2(r, 0.14f));
            pts.Add(new Vector2(r + 0.0009f, 0.1425f));
            pts.Add(new Vector2(r + 0.0009f, 0.1445f));
            pts.Add(new Vector2(r - 0.0003f, 0.1450f));
            return Sc06Lathe("Sc06TestTube", pts.ToArray(), 0.0009f, 18);
        }

        // Wide-mouth glass jar for the pour (about 0.27 m tall, opening radius 0.062).
        private static Mesh Sc06JarMesh() => Sc06Lathe("Sc06PourJar", new[]
        {
            new Vector2(0f, 0f), new Vector2(0.070f, 0f), new Vector2(0.083f, 0.006f), new Vector2(0.0865f, 0.03f), new Vector2(0.0865f, 0.185f),
            new Vector2(0.080f, 0.215f), new Vector2(0.066f, 0.235f), new Vector2(0.063f, 0.245f), new Vector2(0.063f, 0.262f),
            new Vector2(0.0675f, 0.2665f), new Vector2(0.0675f, 0.2735f), new Vector2(0.0645f, 0.2745f)
        }, 0.0035f, 40);

        private static Mesh Sc06RoundFlaskMesh()
        {
            var pts = new List<Vector2>();
            const float R = 0.075f;
            for (var i = 0; i <= 16; i++)
            {
                var a = i / 16f * Mathf.PI * 0.86f;
                pts.Add(new Vector2(Mathf.Sin(a) * R, R - Mathf.Cos(a) * R));
            }

            var lastY = pts[pts.Count - 1].y;
            pts.Add(new Vector2(0.024f, lastY + 0.03f));
            pts.Add(new Vector2(0.0225f, lastY + 0.08f));
            pts.Add(new Vector2(0.026f, lastY + 0.086f));
            pts.Add(new Vector2(0.0245f, lastY + 0.088f));
            return Sc06Lathe("Sc06RoundFlask", pts.ToArray(), 0.002f, 32);
        }

        // Liquid inside a vessel: the profile of the inner wall up to 'fill' (0..1 of the inner height).
        private static Mesh Sc06LiquidDisc(string assetName, float radius, float height)
        {
            return Sc06Lathe(assetName, new[] { new Vector2(0f, 0f), new Vector2(radius * 0.96f, 0f), new Vector2(radius, 0.003f), new Vector2(radius, height), new Vector2(0f, height) },
                0.001f, 28, false);
        }

        private static void Sc06GlassVessel(Transform parent, string name, Mesh mesh, Vector3 baseAt, float scale, Material glass,
            Material liquid = null, float liquidRadius = 0f, float liquidHeight = 0f)
        {
            Sc06Mesh(name, parent, mesh, glass, baseAt, null, Vector3.one * scale);
            if (liquid != null)
            {
                var disc = Sc06LiquidDisc("Sc06Liq_" + name, liquidRadius, liquidHeight);
                Sc06Mesh(name + "Liquid", parent, disc, liquid, baseAt + Vector3.up * (0.003f * scale), null, Vector3.one * scale);
            }
        }

        // A measuring cup with a real handle (tube swept through a D-shaped path).
        private static void Sc06MeasuringCup(Transform parent, string name, Vector3 baseAt, float yaw, Material glass)
        {
            var cup = Sc06Mesh(name, parent, Sc06CupMesh(), glass, baseAt, Quaternion.Euler(0f, yaw, 0f));
            var path = new List<Vector3>();
            for (var i = 0; i <= 14; i++)
            {
                var a = Mathf.Lerp(-1.25f, 1.25f, i / 14f);
                path.Add(new Vector3(0.050f + Mathf.Cos(a) * 0.035f, 0.062f + Mathf.Sin(a) * 0.04f, 0f));
            }

            var handle = Sc06Mesh(name + "Handle", cup.transform, Sc06Tube("Sc06CupHandle", path, 0.0042f, 8), glass, baseAt, Quaternion.Euler(0f, yaw, 0f));
            handle.transform.SetParent(cup.transform, true);
        }

        // Rack with six test tubes (some with coloured liquid), as in the lab pictures.
        private static void Sc06TestTubeRack(Transform parent, Vector3 baseAt, Material glass)
        {
            var wood = Mat("Sc06RackWood", new Color(0.78f, 0.74f, 0.66f), 0.3f, 0f);
            var tubeMesh = Sc06TestTubeMesh();
            Box("RackBase", parent, baseAt + new Vector3(0f, 0.008f, 0f), new Vector3(0.20f, 0.016f, 0.075f), wood, false);
            Box("RackShelfLow", parent, baseAt + new Vector3(0f, 0.045f, 0f), new Vector3(0.20f, 0.010f, 0.075f), wood, false);
            Box("RackShelfHigh", parent, baseAt + new Vector3(0f, 0.095f, 0f), new Vector3(0.20f, 0.010f, 0.075f), wood, false);
            Box("RackPostL", parent, baseAt + new Vector3(0.095f, 0.055f, 0f), new Vector3(0.010f, 0.11f, 0.075f), wood, false);
            Box("RackPostR", parent, baseAt + new Vector3(-0.095f, 0.055f, 0f), new Vector3(0.010f, 0.11f, 0.075f), wood, false);
            var colours = new[]
            {
                new Color(0.95f, 0.78f, 0.2f, 0.55f), new Color(0.3f, 0.7f, 0.95f, 0.5f), new Color(0.9f, 0.3f, 0.3f, 0.5f),
                new Color(0.4f, 0.8f, 0.45f, 0.5f), new Color(0.78f, 0.88f, 0.92f, 0.4f), new Color(0.75f, 0.45f, 0.85f, 0.5f)
            };
            for (var i = 0; i < 6; i++)
            {
                var x = (i - 2.5f) * 0.032f;
                var at = baseAt + new Vector3(x, 0.016f, 0f);
                Sc06Mesh("TestTube" + i, parent, tubeMesh, glass, at);
                var lq = Sc06Liquid("Sc06TubeLiq" + i, colours[i]);
                var h = i == 4 ? 0.02f : 0.05f + (i % 3) * 0.015f;
                var disc = Sc06LiquidDisc("Sc06TubeLiqMesh" + i, 0.0076f, h);
                Sc06Mesh("TestTubeLiquid" + i, parent, disc, lq, at + Vector3.up * 0.0012f);
            }
        }
    }
}
#endif

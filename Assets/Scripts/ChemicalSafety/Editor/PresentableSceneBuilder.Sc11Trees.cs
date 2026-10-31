#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 11 trees: four Bangladeshi species built as real meshes (not spheres): coconut palm (curved trunk, arching fronds),
    /// betel-nut / supari palm (very tall thin straight trunk, small crown), mango (short trunk, dense dark dome of leaf cards) and
    /// mahogany (tall straight trunk, wide light crown). Three random variants per species are saved as mesh assets and reused.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc11Mb
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> bark = new List<int>();
            public readonly List<int> leaf = new List<int>();
        }

        private static void Sc11Tube(Sc11Mb m, Vector3[] path, float[] radius, int sides)
        {
            var start = m.v.Count;
            var vlen = 0f;
            for (var i = 0; i < path.Length; i++)
            {
                if (i > 0)
                    vlen += Vector3.Distance(path[i], path[i - 1]);
                var dir = (i < path.Length - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                var side = Vector3.Cross(dir, Vector3.forward);
                if (side.sqrMagnitude < 0.01f)
                    side = Vector3.Cross(dir, Vector3.right);
                side.Normalize();
                var up = Vector3.Cross(side, dir).normalized;
                for (var j = 0; j <= sides; j++)
                {
                    var a = j / (float)sides * Mathf.PI * 2f;
                    var nrm = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    m.v.Add(path[i] + nrm * radius[i]);
                    m.n.Add(nrm);
                    m.uv.Add(new Vector2(j / (float)sides * 2f, vlen * 0.5f));
                }
            }

            var row = sides + 1;
            for (var i = 0; i < path.Length - 1; i++)
            {
                for (var j = 0; j < sides; j++)
                {
                    var a = start + i * row + j;
                    m.bark.Add(a); m.bark.Add(a + row); m.bark.Add(a + 1);
                    m.bark.Add(a + 1); m.bark.Add(a + row); m.bark.Add(a + row + 1);
                }
            }
        }

        private static void Sc11Card(Sc11Mb m, Vector3 c, Vector3 right, Vector3 up, float sx, float sy, Vector3 normal)
        {
            var i = m.v.Count;
            m.v.Add(c - right * sx * 0.5f - up * sy * 0.5f);
            m.v.Add(c + right * sx * 0.5f - up * sy * 0.5f);
            m.v.Add(c + right * sx * 0.5f + up * sy * 0.5f);
            m.v.Add(c - right * sx * 0.5f + up * sy * 0.5f);
            for (var k = 0; k < 4; k++)
                m.n.Add(normal);
            m.uv.Add(new Vector2(0, 0)); m.uv.Add(new Vector2(1, 0)); m.uv.Add(new Vector2(1, 1)); m.uv.Add(new Vector2(0, 1));
            m.leaf.Add(i); m.leaf.Add(i + 2); m.leaf.Add(i + 1);
            m.leaf.Add(i); m.leaf.Add(i + 3); m.leaf.Add(i + 2);
        }

        /// <summary>One palm frond: a strip that rises from the crown and then droops under its own weight.</summary>
        private static void Sc11Frond(Sc11Mb m, Vector3 origin, float yawDeg, float len, float width, float rise, float droop)
        {
            var yaw = yawDeg * Mathf.Deg2Rad;
            var dirH = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var side = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            const int segs = 8;
            var start = m.v.Count;
            for (var k = 0; k <= segs; k++)
            {
                var t = k / (float)segs;
                var p = origin + dirH * (len * t) + Vector3.up * (len * (rise * t - droop * t * t));
                var w = width * 0.5f;
                m.v.Add(p - side * w);
                m.v.Add(p + side * w);
                m.n.Add(Vector3.up);
                m.n.Add(Vector3.up);
                m.uv.Add(new Vector2(0f, t));
                m.uv.Add(new Vector2(1f, t));
            }

            for (var k = 0; k < segs; k++)
            {
                var a = start + k * 2;
                m.leaf.Add(a); m.leaf.Add(a + 2); m.leaf.Add(a + 1);
                m.leaf.Add(a + 1); m.leaf.Add(a + 2); m.leaf.Add(a + 3);
            }
        }

        private static void Sc11Dome(Sc11Mb m, System.Random r, Vector3 c, float rx, float ry, int cards, float fMin, float sizeMin, float sizeMax)
        {
            for (var i = 0; i < cards; i++)
            {
                Vector3 d;
                do
                {
                    d = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 2f - 1f);
                } while (d.sqrMagnitude > 1f || d.sqrMagnitude < 0.05f || d.y < -0.35f);

                d.Normalize();
                var f = fMin + (float)r.NextDouble() * (1f - fMin);
                var pos = c + new Vector3(d.x * rx * f, d.y * ry * f, d.z * rx * f);
                var right = new Vector3((float)r.NextDouble() * 2f - 1f, ((float)r.NextDouble() - 0.5f) * 0.6f, (float)r.NextDouble() * 2f - 1f).normalized;
                var up = Vector3.Cross(right, d).normalized;
                if (up.sqrMagnitude < 0.01f)
                    up = Vector3.up;
                var size = sizeMin + (float)r.NextDouble() * (sizeMax - sizeMin);
                Sc11Card(m, pos, right, up, size, size, (d + Vector3.up * 0.3f).normalized);
            }
        }

        private static Mesh Sc11MakeTree(string kind, int seed)
        {
            var r = new System.Random(seed);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var m = new Sc11Mb();
            switch (kind)
            {
                case "coconut":
                {
                    var h = R(9f, 13f);
                    var a = R(0f, 6.28f);
                    var lean = R(0.8f, 2.2f);
                    const int N = 12;
                    var path = new Vector3[N + 1];
                    var rad = new float[N + 1];
                    for (var i = 0; i <= N; i++)
                    {
                        var t = i / (float)N;
                        path[i] = new Vector3(Mathf.Cos(a) * lean * t * t, h * t, Mathf.Sin(a) * lean * t * t);
                        rad[i] = Mathf.Lerp(0.27f, 0.15f, t) + (t < 0.1f ? 0.09f * (1f - t / 0.1f) : 0f);
                    }

                    Sc11Tube(m, path, rad, 9);
                    var top = path[N];
                    for (var i = 0; i < 15; i++)
                        Sc11Frond(m, top, i * 24f + R(-9f, 9f), R(3.3f, 4.5f), R(1.0f, 1.35f), R(0.5f, 0.95f), R(1.0f, 1.6f));
                    for (var i = 0; i < 5; i++)
                        Sc11Frond(m, top, R(0f, 360f), R(2.0f, 2.8f), 0.9f, R(1.3f, 1.7f), R(0.25f, 0.5f));
                    break;
                }
                case "betel":
                {
                    var h = R(11f, 15f);
                    var a = R(0f, 6.28f);
                    var lean = R(0.1f, 0.8f);
                    const int N = 12;
                    var path = new Vector3[N + 1];
                    var rad = new float[N + 1];
                    for (var i = 0; i <= N; i++)
                    {
                        var t = i / (float)N;
                        path[i] = new Vector3(Mathf.Cos(a) * lean * t * t, h * t, Mathf.Sin(a) * lean * t * t);
                        rad[i] = Mathf.Lerp(0.115f, 0.075f, t);
                    }

                    Sc11Tube(m, path, rad, 7);
                    var top = path[N];
                    for (var i = 0; i < 9; i++)
                        Sc11Frond(m, top, i * 40f + R(-10f, 10f), R(1.9f, 2.7f), R(0.7f, 0.9f), R(0.6f, 1.1f), R(0.9f, 1.35f));
                    for (var i = 0; i < 3; i++)
                        Sc11Frond(m, top, R(0f, 360f), 1.6f, 0.6f, 1.6f, 0.3f);
                    break;
                }
                case "mango":
                {
                    var h = R(1.8f, 2.6f);
                    var path = new[] { Vector3.zero, new Vector3(0.05f, h * 0.5f, 0f), new Vector3(0f, h, 0.05f) };
                    Sc11Tube(m, path, new[] { 0.44f, 0.36f, 0.30f }, 10);
                    for (var b = 0; b < 3; b++)
                    {
                        var ang = b * 2.1f + R(0f, 1f);
                        var o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        Sc11Tube(m, new[] { path[2], path[2] + o * 0.7f + Vector3.up * 0.8f, path[2] + o * 1.9f + Vector3.up * 1.7f },
                            new[] { 0.22f, 0.15f, 0.09f }, 6);
                    }

                    Sc11Dome(m, r, new Vector3(0f, h + R(1.6f, 2.1f), 0f), R(3.4f, 4.4f), R(2.3f, 2.9f), 150, 0.55f, 1.6f, 2.4f);
                    break;
                }
                default: // mahogany
                {
                    var h = R(8.5f, 11.5f);
                    const int N = 9;
                    var path = new Vector3[N + 1];
                    var rad = new float[N + 1];
                    for (var i = 0; i <= N; i++)
                    {
                        var t = i / (float)N;
                        path[i] = new Vector3(0.2f * Mathf.Sin(t * 3f), h * t, 0.15f * Mathf.Cos(t * 2.4f));
                        rad[i] = Mathf.Lerp(0.34f, 0.19f, t) + (t < 0.12f ? 0.2f * (1f - t / 0.12f) : 0f);
                    }

                    Sc11Tube(m, path, rad, 10);
                    var top = path[N];
                    for (var b = 0; b < 5; b++)
                    {
                        var ang = b * 1.26f + R(0f, 0.6f);
                        var o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        var baseAt = path[N - 2];
                        Sc11Tube(m, new[] { baseAt, baseAt + o * 1.2f + Vector3.up * 0.7f, baseAt + o * 2.8f + Vector3.up * 1.5f },
                            new[] { 0.16f, 0.1f, 0.06f }, 6);
                    }

                    Sc11Dome(m, r, top + new Vector3(0f, 1.3f, 0f), R(4.2f, 5.4f), R(2.4f, 3.2f), 170, 0.45f, 1.7f, 2.5f);
                    break;
                }
            }

            var mesh = new Mesh { name = "Sc11Tree_" + kind + "_" + seed, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(m.v);
            mesh.SetNormals(m.n);
            mesh.SetUVs(0, m.uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(m.bark, 0);
            mesh.SetTriangles(m.leaf, 1);
            mesh.RecalculateBounds();
            var path2 = GenDir + "/Mesh_Sc11Tree_" + kind + "_" + seed + ".asset";
            AssetDatabase.DeleteAsset(path2);
            AssetDatabase.CreateAsset(mesh, path2);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path2);
        }

        /// <summary>The tree line behind the fence: a natural mix of coconut, betel-nut, mango and mahogany trees.</summary>
        private static void Sc11PlantTrees(Transform parent, float ox)
        {
            var bark = Mat("Sc11BarkMat", Color.white, 0.12f, 0f, Sc06Tex("Label_Sc11Bark"));
            Material Foliage(string name, string tex, Color tint, float smooth)
            {
                var mt = Mat(name, tint, smooth, 0f, Sc06Tex(tex), null, false, true);
                mt.SetFloat("_Cull", 0f);
                return mt;
            }

            var mats = new Dictionary<string, Material>
            {
                { "coconut", Foliage("Sc11FrondCoconut", "Label_Sc11Frond", new Color(0.95f, 1.05f, 0.8f), 0.3f) },
                { "betel", Foliage("Sc11FrondBetel", "Label_Sc11Frond", new Color(0.85f, 1.0f, 0.75f), 0.25f) },
                { "mango", Foliage("Sc11LeafMango", "Label_Sc11LeafCluster", new Color(0.62f, 0.8f, 0.55f), 0.35f) },
                { "mahogany", Foliage("Sc11LeafMahogany", "Label_Sc11LeafCluster", new Color(0.92f, 1.1f, 0.72f), 0.3f) },
            };
            var kinds = new[] { "coconut", "betel", "mango", "mahogany" };
            var variants = new Dictionary<string, Mesh[]>();
            foreach (var k in kinds)
            {
                var arr = new Mesh[3];
                for (var i = 0; i < 3; i++)
                    arr[i] = Sc11MakeTree(k, 11 * (i + 1) + k.Length);
                variants[k] = arr;
            }

            var rng = new System.Random(77);
            var holder = new GameObject("Trees").transform;
            holder.SetParent(parent, false);

            void Plant(string kind, Vector3 at, float scale)
            {
                var go = new GameObject("Tree_" + kind);
                go.transform.SetParent(holder, true);
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = variants[kind][rng.Next(3)];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { bark, mats[kind] };
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // the near, recognisable trees at the sides of the yard (the first three of the picture's horizon)
            Plant("coconut", new Vector3(ox + 9.5f, 0f, -9f), 1.0f);
            Plant("mango", new Vector3(ox - 14f, 0f, -12f), 1.0f);
            Plant("betel", new Vector3(ox + 12.5f, 0f, -12f), 1.0f);
            Plant("mahogany", new Vector3(ox - 9f, 0f, -20f), 1.0f);
            for (var i = 0; i < 30; i++)
            {
                var x = ox - 42f + (float)rng.NextDouble() * 84f;
                var z = -16f - (float)rng.NextDouble() * 34f;
                if (Mathf.Abs(x - (ox + Sc11GateX)) < 5f && z > -34f)
                    continue; // keep the road open
                Plant(kinds[rng.Next(kinds.Length)], new Vector3(x, 0f, z), 0.85f + (float)rng.NextDouble() * 0.4f);
            }
        }
    }
}
#endif

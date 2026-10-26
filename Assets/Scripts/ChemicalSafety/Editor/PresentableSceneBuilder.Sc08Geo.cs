#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 8 mesh helpers: curved shells (goggle lenses, face shield), swept tubes (straps, rims, tap), hanging cloth (apron).</summary>
    public static partial class PresentableSceneBuilder
    {
        private static Mesh Sc08SaveMesh(string assetName, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var path = GenDir + "/Mesh_" + assetName + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (uvs != null && uvs.Count == verts.Count)
                mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A thin curved shell: a cylinder strip around the Y axis, arc centred on +Z (front), radius r (outer), height h centred on y = 0.
        // Both faces are built so it is visible from both sides with a plain lit material.
        private static Mesh Sc08ArcShell(string assetName, float r, float h, float arcDeg, float thick, int seg = 28, float cylTilt = 0f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Surface(float radius, bool flip)
            {
                var start = verts.Count;
                for (var j = 0; j <= 1; j++)
                {
                    for (var i = 0; i <= seg; i++)
                    {
                        var a = Mathf.Lerp(-arcDeg * 0.5f, arcDeg * 0.5f, i / (float)seg) * Mathf.Deg2Rad;
                        var y = (j == 0 ? -0.5f : 0.5f) * h;
                        // a slight conical tilt makes the shield flare toward the bottom
                        var rr = radius + (j == 0 ? cylTilt : 0f);
                        verts.Add(new Vector3(Mathf.Sin(a) * rr, y, Mathf.Cos(a) * rr - r));
                        uvs.Add(new Vector2(i / (float)seg, j));
                    }
                }

                for (var i = 0; i < seg; i++)
                {
                    var a0 = start + i;
                    var b0 = start + seg + 1 + i;
                    if (flip)
                        tris.AddRange(new[] { a0, a0 + 1, b0, a0 + 1, b0 + 1, b0 });
                    else
                        tris.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 });
                }
            }

            Surface(r, true);
            Surface(r - thick, false);
            // top and bottom edges
            for (var j = 0; j <= 1; j++)
            {
                var start = verts.Count;
                for (var i = 0; i <= seg; i++)
                {
                    var a = Mathf.Lerp(-arcDeg * 0.5f, arcDeg * 0.5f, i / (float)seg) * Mathf.Deg2Rad;
                    var y = (j == 0 ? -0.5f : 0.5f) * h;
                    var rr0 = r + (j == 0 ? cylTilt : 0f);
                    var rr1 = r - thick + (j == 0 ? cylTilt : 0f);
                    verts.Add(new Vector3(Mathf.Sin(a) * rr0, y, Mathf.Cos(a) * rr0 - r));
                    verts.Add(new Vector3(Mathf.Sin(a) * rr1, y, Mathf.Cos(a) * rr1 - r));
                    uvs.Add(Vector2.zero);
                    uvs.Add(Vector2.zero);
                }

                for (var i = 0; i < seg; i++)
                {
                    var a0 = start + i * 2;
                    if (j == 0)
                        tris.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 1, a0 + 3, a0 + 2 });
                    else
                        tris.AddRange(new[] { a0, a0 + 2, a0 + 1, a0 + 1, a0 + 2, a0 + 3 });
                }
            }

            return Sc08SaveMesh(assetName, verts, uvs, tris);
        }

        // A tube swept along any 3D path (parallel-transport frames).
        private static Mesh Sc08SweepTube(string assetName, List<Vector3> pts, float radius, int sides = 8, float flatten = 1f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var up = Vector3.up;
            var t0 = (pts[1] - pts[0]).normalized;
            if (Mathf.Abs(Vector3.Dot(t0, up)) > 0.95f)
                up = Vector3.forward;
            var n = Vector3.Cross(up, t0).normalized;
            var b = Vector3.Cross(t0, n).normalized;
            for (var i = 0; i < pts.Count; i++)
            {
                var t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                // transport the frame
                var nn = n - Vector3.Dot(n, t) * t;
                if (nn.sqrMagnitude < 1e-6f)
                    nn = Vector3.Cross(t, b);
                n = nn.normalized;
                b = Vector3.Cross(t, n).normalized;
                for (var j = 0; j <= sides; j++)
                {
                    var a = j / (float)sides * Mathf.PI * 2f;
                    verts.Add(pts[i] + (n * Mathf.Cos(a) * radius + b * Mathf.Sin(a) * radius * flatten));
                    uvs.Add(new Vector2(j / (float)sides, i / (float)(pts.Count - 1)));
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

            return Sc08SaveMesh(assetName, verts, uvs, tris);
        }

        // Hanging cloth in the XY plane: top at y = 0, hanging to yBottom (negative). 'halfWidth(y)' gives the outline; 'fold' adds
        // gentle vertical folds and a soft sag. Front = +Z.
        private static Mesh Sc08Cloth(string assetName, System.Func<float, float> halfWidth, float yBottom, int nx, int ny, float fold, float uvScale)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var j = 0; j <= ny; j++)
            {
                var v = j / (float)ny;
                var y = Mathf.Lerp(0f, yBottom, v);
                var hw = halfWidth(y);
                for (var i = 0; i <= nx; i++)
                {
                    var u = i / (float)nx;
                    var x = Mathf.Lerp(-hw, hw, u);
                    var z = fold * (Mathf.Sin(x * 31f + y * 2.3f) * 0.55f + Mathf.Sin(x * 13f - y * 1.1f) * 0.45f) * Mathf.Clamp01(-y * 4f)
                            + 0.018f * Mathf.Sin(v * Mathf.PI * 0.9f) * (1f - Mathf.Abs(x) / Mathf.Max(0.05f, hw) * 0.35f);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(x * uvScale, y * uvScale));
                }
            }

            for (var j = 0; j < ny; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var a = j * (nx + 1) + i;
                    var c = a + nx + 1;
                    tris.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            }

            return Sc08SaveMesh(assetName, verts, uvs, tris);
        }

        private static GameObject Sc08Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion? localRot = null,
            Vector3? localScale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static GameObject Sc08LocalBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // two-sided lit material (cloth, thin shells)
        private static Material Sc08TwoSided(Material m)
        {
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif

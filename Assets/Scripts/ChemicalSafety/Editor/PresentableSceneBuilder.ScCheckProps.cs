#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Real 3D props of the Supervisor Check hall (no flat stand-ins): rounded boxes (jug, tablet, carton, control box, table), lathe
    /// meshes (flasks, beaker, drums, caps, pipes flanges) and tube meshes (hoses), plus contact-shadow blobs and light shafts.
    /// All meshes are saved under Generated/ScCheck and reused on every rebuild (same asset, so scene references stay valid).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        // ------------------------------------------------------------------ mesh library

        private static Mesh ScSave(Mesh m, string name)
        {
            System.IO.Directory.CreateDirectory(ScCheckGenDir);
            var path = ScCheckGenDir + "/Mesh_ScProp_" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(m, old);
                Object.DestroyImmediate(m);
                return old;
            }

            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static float[] ScEdgeCoords(float half, float r)
        {
            return new[] { -half, -half + r * 0.25f, -half + r * 0.6f, -half + r, half - r, half - r * 0.6f, half - r * 0.25f, half };
        }

        private static Mesh ScRoundedBox(string name, Vector3 size, float radius)
        {
            var half = size * 0.5f;
            var r = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z))) * 0.98f;
            var inner = half - Vector3.one * r;
            var coords = new[] { ScEdgeCoords(half.x, r), ScEdgeCoords(half.y, r), ScEdgeCoords(half.z, r) };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var axis = 0; axis < 3; axis++)
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var b = (axis + 1) % 3;
                    var c = (axis + 2) % 3;
                    var start = verts.Count;
                    var nb = coords[b].Length;
                    var nc = coords[c].Length;
                    for (var i = 0; i < nb; i++)
                    {
                        for (var j = 0; j < nc; j++)
                        {
                            var p = Vector3.zero;
                            p[axis] = sign * half[axis];
                            p[b] = coords[b][i];
                            p[c] = coords[c][j];
                            var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var d = p - q;
                            var n = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.zero;
                            if (n == Vector3.zero)
                                n[axis] = sign;
                            verts.Add(q + n * r);
                            norms.Add(n);
                            uvs.Add(new Vector2((p[b] / size[b]) + 0.5f, (p[c] / size[c]) + 0.5f));
                        }
                    }

                    for (var i = 0; i < nb - 1; i++)
                    {
                        for (var j = 0; j < nc - 1; j++)
                        {
                            var a0 = start + i * nc + j;
                            var a1 = a0 + 1;
                            var a2 = a0 + nc;
                            var a3 = a2 + 1;
                            var faceN = Vector3.Cross(verts[a2] - verts[a0], verts[a1] - verts[a0]);
                            var flip = Vector3.Dot(faceN, norms[a0]) < 0f;
                            if (!flip)
                                tris.AddRange(new[] { a0, a2, a1, a1, a2, a3 });
                            else
                                tris.AddRange(new[] { a0, a1, a2, a1, a3, a2 });
                        }
                    }
                }
            }

            var m = new Mesh { name = "ScProp_" + name };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return ScSave(m, name);
        }

        // profile: x = radius, y = height, ordered bottom to top (outside surface); closed at the axis by the first / last point
        private static Mesh ScLathe(string name, Vector2[] profile, int seg = 28)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var n = profile.Length;
            for (var k = 0; k < n; k++)
            {
                var a = profile[Mathf.Max(0, k - 1)];
                var b = profile[Mathf.Min(n - 1, k + 1)];
                var t = (b - a).normalized;
                var n2 = new Vector2(t.y, -t.x);
                for (var s = 0; s <= seg; s++)
                {
                    var ang = s / (float)seg * Mathf.PI * 2f;
                    var cs = Mathf.Cos(ang);
                    var sn = Mathf.Sin(ang);
                    verts.Add(new Vector3(profile[k].x * cs, profile[k].y, profile[k].x * sn));
                    norms.Add(new Vector3(n2.x * cs, n2.y, n2.x * sn).normalized);
                    uvs.Add(new Vector2(s / (float)seg, k / (float)(n - 1)));
                }
            }

            for (var k = 0; k < n - 1; k++)
            {
                for (var s = 0; s < seg; s++)
                {
                    var a0 = k * (seg + 1) + s;
                    var a1 = a0 + 1;
                    var a2 = a0 + seg + 1;
                    var a3 = a2 + 1;
                    tris.AddRange(new[] { a0, a1, a2, a1, a3, a2 });
                }
            }

            var m = new Mesh { name = "ScProp_" + name };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            // the winding above faces inwards on some profiles: make the outside visible from the camera side
            var test = Vector3.Cross(verts[seg + 1] - verts[0], verts[1] - verts[0]);
            if (Vector3.Dot(test, norms[0]) < 0f)
            {
                var flipped = new List<int>();
                for (var i = 0; i < tris.Count; i += 3)
                    flipped.AddRange(new[] { tris[i], tris[i + 2], tris[i + 1] });
                m.SetTriangles(flipped, 0);
            }

            return ScSave(m, name);
        }

        private static Mesh ScTube(string name, Vector3[] pts, float radius, int seg = 10)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var k = 0; k < pts.Length; k++)
            {
                var t = (pts[Mathf.Min(pts.Length - 1, k + 1)] - pts[Mathf.Max(0, k - 1)]).normalized;
                var side = Vector3.Cross(t, Mathf.Abs(t.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var up = Vector3.Cross(side, t).normalized;
                for (var s = 0; s <= seg; s++)
                {
                    var ang = s / (float)seg * Mathf.PI * 2f;
                    var n = side * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                    verts.Add(pts[k] + n * radius);
                    norms.Add(n);
                    uvs.Add(new Vector2(s / (float)seg, k / (float)(pts.Length - 1)));
                }
            }

            for (var k = 0; k < pts.Length - 1; k++)
            {
                for (var s = 0; s < seg; s++)
                {
                    var a0 = k * (seg + 1) + s;
                    var a1 = a0 + 1;
                    var a2 = a0 + seg + 1;
                    var a3 = a2 + 1;
                    tris.AddRange(new[] { a0, a2, a1, a1, a2, a3 });
                }
            }

            var m = new Mesh { name = "ScProp_" + name };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return ScSave(m, name);
        }

        private static GameObject ScMeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 worldPos, Vector3 euler, bool shadow = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(euler));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private static void ScBlob(Transform parent, Vector3 worldFloorPos, Vector2 size, float alpha = 0.8f)
        {
            var tex = ScTex("ScCheck_Blob");
            var m = MatAlpha("ScCheckBlob", tex, new Color(0f, 0f, 0f, alpha), false);
            var q = QuadObj("Blob", parent, worldFloorPos + Vector3.up * 0.004f, new Vector3(90f, 0f, 0f), size, m);
            q.transform.SetParent(parent, true);
        }

        // ------------------------------------------------------------------ props

        private static void ScBuildProps(Transform room, float fFar, Material white, Material steel, Material darkSteel, Material yellow, Material blue,
            out MeshRenderer labelRenderer, out MeshRenderer posterRenderer, out Sc04BenchHeight benchHeight, out Transform posterT)
        {
            // ---- wall: poster with a frame that has depth, control box, tablet, pipes
            var posterMat = Mat("ScCheckPoster", Color.white, 0.2f, 0f, ScResTex("poster"));
            var poster = ScPicture("SdsPoster", room, ScP(2.9f, 2.65f, fFar - 0.05f), new Vector2(1.18f, 1.77f), posterMat);
            posterT = poster.transform;
            posterRenderer = poster.GetComponent<MeshRenderer>();
            var frameMat = Mat("ScCheckPosterFrame", new Color(0.82f, 0.84f, 0.86f), 0.5f, 0.6f);
            var rb = ScRoundedBox("PosterFrameBar", new Vector3(1.26f, 0.03f, 0.035f), 0.008f);
            var rbv = ScRoundedBox("PosterFrameBarV", new Vector3(0.03f, 1.89f, 0.035f), 0.008f);
            ScMeshObj("PosterFrameT", room, rb, frameMat, ScP(2.9f, 2.65f + 0.93f, fFar - 0.04f), Vector3.zero, false);
            ScMeshObj("PosterFrameB", room, rb, frameMat, ScP(2.9f, 2.65f - 0.93f, fFar - 0.04f), Vector3.zero, false);
            ScMeshObj("PosterFrameL", room, rbv, frameMat, ScP(2.9f - 0.615f, 2.65f, fFar - 0.04f), Vector3.zero, false);
            ScMeshObj("PosterFrameR", room, rbv, frameMat, ScP(2.9f + 0.615f, 2.65f, fFar - 0.04f), Vector3.zero, false);
            ScBox("PosterBacking", room, 2.9f, 2.65f, fFar - 0.015f, 1.24f, 1.83f, 0.03f, white);

            var ctrlMat = Mat("ScCheckCtrlBox", new Color(0.70f, 0.72f, 0.72f), 0.55f, 0.35f);
            var ctrlMesh = ScRoundedBox("ControlBox", new Vector3(0.46f, 0.52f, 0.2f), 0.02f);
            ScMeshObj("ControlBox", room, ctrlMesh, ctrlMat, ScP(1.2f, 1.45f, fFar - 0.11f), Vector3.zero);
            var scr = ScRoundedBox("ControlScreen", new Vector3(0.26f, 0.13f, 0.012f), 0.004f);
            ScMeshObj("ControlScreen", room, scr, Mat("ScCheckCtrlScreen", new Color(0.35f, 0.5f, 0.3f), 0.5f, 0f, null, new Color(0.2f, 0.45f, 0.18f)), ScP(1.2f, 1.58f, fFar - 0.22f), Vector3.zero, false);
            var btn = ScLathe("Button", new[] { new Vector2(0.0f, 0f), new Vector2(0.022f, 0f), new Vector2(0.022f, 0.012f), new Vector2(0.016f, 0.018f), new Vector2(0f, 0.018f) }, 18);
            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++)
                    ScMeshObj("Btn", room, btn, Mat("ScCheckBtn" + ((i + j) % 2), (i + j) % 2 == 0 ? new Color(0.78f, 0.78f, 0.8f) : new Color(0.25f, 0.27f, 0.3f), 0.5f, 0.3f),
                        ScP(1.30f - i * 0.1f, 1.37f - j * 0.07f, fFar - 0.215f), new Vector3(-90f, 0f, 0f), false);
            var hood = ScRoundedBox("CtrlHood", new Vector3(0.5f, 0.04f, 0.26f), 0.012f);
            ScMeshObj("CtrlHood", room, hood, ctrlMat, ScP(1.2f, 1.74f, fFar - 0.12f), Vector3.zero);
            var gland = ScLathe("Gland", new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.018f, 0.05f), new Vector2(0.012f, 0.06f), new Vector2(0f, 0.06f) }, 14);
            for (var i = 0; i < 3; i++)
                ScMeshObj("Gland", room, gland, darkSteel, ScP(1.32f - i * 0.12f, 1.19f, fFar - 0.11f), new Vector3(180f, 0f, 0f), false);

            var pipeMat = Mat("ScCheckPipe", new Color(0.66f, 0.68f, 0.70f), 0.5f, 0.7f);
            var pipe = ScLathe("Pipe", new[] { new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.034f, 4.8f), new Vector2(0f, 4.8f) }, 16);
            var flange = ScLathe("Flange", new[] { new Vector2(0.034f, 0f), new Vector2(0.065f, 0f), new Vector2(0.065f, 0.025f), new Vector2(0.034f, 0.025f) }, 16);
            foreach (var pr in new[] { 1.2f, 4.5f })
            {
                ScMeshObj("Pipe", room, pipe, pipeMat, ScP(pr, 0.2f, fFar - 0.14f), Vector3.zero);
                for (var k = 0; k < 4; k++)
                    ScMeshObj("PipeFlange", room, flange, pipeMat, ScP(pr, 0.9f + k * 1.1f, fFar - 0.14f), Vector3.zero, false);
                for (var k = 0; k < 3; k++)
                    ScMeshObj("PipeClamp", room, ScRoundedBox("PipeClamp", new Vector3(0.1f, 0.04f, 0.06f), 0.012f), darkSteel, ScP(pr, 0.6f + k * 1.5f, fFar - 0.1f), Vector3.zero, false);
            }

            var cable = ScTube("OrangeCable", new[] { ScP(0.98f, 1.2f, fFar - 0.2f), ScP(0.98f, 0.9f, fFar - 0.19f), ScP(0.92f, 0.5f, fFar - 0.22f), ScP(0.86f, 0.14f, fFar - 0.3f) }, 0.016f, 10);
            ScMeshObj("OrangeCable", room, cable, Mat("ScCheckCable", new Color(0.92f, 0.42f, 0.1f), 0.45f, 0f), Vector3.zero, Vector3.zero, false);

            var tablet = ScRoundedBox("Tablet", new Vector3(0.42f, 0.30f, 0.024f), 0.014f);
            ScMeshObj("Tablet", room, tablet, Mat("ScCheckTablet", new Color(0.04f, 0.04f, 0.05f), 0.65f, 0.4f), ScP(4.0f, 1.55f, fFar - 0.05f), Vector3.zero);
            var tscr = ScRoundedBox("TabletScreen", new Vector3(0.385f, 0.265f, 0.004f), 0.002f);
            ScMeshObj("TabletScreen", room, tscr, Mat("ScCheckTabletScreen", Color.white, 0.55f, 0f, ScTex("ScCheck_TabletScreen"), new Color(0.35f, 0.38f, 0.45f)),
                ScP(4.0f, 1.55f, fFar - 0.0665f), Vector3.zero, false);

            // ---- the steel table, everything on it (group follows the eye height)
            var bench = new GameObject("Bench").transform;
            bench.SetParent(room, false);
            var topMat = Sc12Tiled("ScCheckTableTop", null, new Color(0.90f, 0.92f, 0.95f), 0.82f, 0.35f, new Vector2(3f, 1f), ScTex("ScCheck_Steel_Normal"));
            topMat.SetFloat("_BumpScale", 0.2f);
            const float tr = 1.5f, tf = 4.05f;
            var top = ScRoundedBox("TableTop", new Vector3(2.4f, 0.04f, 0.95f), 0.012f);
            ScMeshObj("TableTop", bench, top, topMat, ScP(tr, 0.89f, tf), Vector3.zero);
            var lip = ScRoundedBox("TableLip", new Vector3(2.4f, 0.06f, 0.95f), 0.02f);
            ScMeshObj("TableLip", bench, lip, steel, ScP(tr, 0.845f, tf), Vector3.zero);
            var leg = ScRoundedBox("TableLeg", new Vector3(0.06f, 2.0f, 0.06f), 0.014f);
            foreach (var dr in new[] { -1.1f, 1.1f })
                foreach (var df in new[] { -0.4f, 0.4f })
                    ScMeshObj("TableLeg", bench, leg, steel, ScP(tr + dr, -0.15f, tf + df), Vector3.zero);
            var brace = ScRoundedBox("TableBrace", new Vector3(2.2f, 0.04f, 0.04f), 0.01f);
            ScMeshObj("TableBraceF", bench, brace, steel, ScP(tr, 0.5f, tf - 0.4f), Vector3.zero, false);
            ScMeshObj("TableBraceB", bench, brace, steel, ScP(tr, 0.5f, tf + 0.4f), Vector3.zero, false);
            var apron = ScRoundedBox("TableApron", new Vector3(2.3f, 0.09f, 0.03f), 0.008f);
            ScMeshObj("TableApronF", bench, apron, steel, ScP(tr, 0.79f, tf - 0.4f), Vector3.zero, false);
            ScMeshObj("TableApronB", bench, apron, steel, ScP(tr, 0.79f, tf + 0.4f), Vector3.zero, false);
            var apronSide = ScRoundedBox("TableApronSide", new Vector3(0.03f, 0.09f, 0.8f), 0.008f);
            ScMeshObj("TableApronL", bench, apronSide, steel, ScP(tr - 1.1f, 0.79f, tf), Vector3.zero, false);
            ScMeshObj("TableApronR", bench, apronSide, steel, ScP(tr + 1.1f, 0.79f, tf), Vector3.zero, false);
            var edge = ScRoundedBox("TableEdgeRoll", new Vector3(2.42f, 0.014f, 0.97f), 0.006f);
            ScMeshObj("TableEdgeRoll", bench, edge, Mat("ScCheckEdgeSteel", new Color(0.93f, 0.95f, 0.97f), 0.9f, 0.6f), ScP(tr, 0.912f, tf), Vector3.zero, false);
            var shelf = ScRoundedBox("TableShelf", new Vector3(2.3f, 0.03f, 0.85f), 0.01f);
            ScMeshObj("TableShelf", bench, shelf, steel, ScP(tr, 0.25f, tf), Vector3.zero, false);
            var crate = ScRoundedBox("Crate", new Vector3(0.6f, 0.2f, 0.4f), 0.015f);
            ScMeshObj("Crate", bench, crate, Mat("ScCheckCrate", new Color(0.35f, 0.3f, 0.22f), 0.2f, 0f), ScP(tr + 0.5f, 0.375f, tf), Vector3.zero);

            const float topY = 0.91f;
            // the chemical jug, centered on the table (single prop per round)
            var plastic = Mat("ScCheckJugPlastic", new Color(0.93f, 0.95f, 0.97f), 0.72f, 0f);
            var jugR = tr;
            var jugRoot = Sc06BuildJug(bench, ScP(jugR, topY, tf + 0.05f), null, out _, out var jugCap);
            jugRoot.transform.rotation = Quaternion.identity;
            jugRoot.transform.localScale = Vector3.one * 1.45f; // a little larger than life so the label reads from the player spot, like the picture
            jugCap.transform.SetParent(jugRoot.transform, true);
            jugCap.transform.position = jugRoot.transform.position + new Vector3(JugMouthX, JugCapBottom, 0f);
            var jugLabel = jugRoot.transform.Find("JugLabel");
            jugLabel.GetComponent<MeshRenderer>().sharedMaterial = Mat("ScCheckJugLabel", Color.white, 0.3f, 0f, ScResTex("label"));
            jugLabel.localScale = new Vector3(0.18f, 0.12f, 1f);
            labelRenderer = jugLabel.GetComponent<MeshRenderer>();
            ScBlob(room, ScP(jugR, 0f, tf + 0.05f) + Vector3.up * (topY - 0.004f), new Vector2(0.48f, 0.36f), 0.5f);

            // work order on a small stand, a folder, clipboard with pen, glassware, hose
            var paperMat = Mat("ScCheckWorkOrder", Color.white, 0.2f, 0f, ScTex("ScCheck_WorkOrder"));
            var sheetMesh = ScRoundedBox("WorkOrderSheet", new Vector3(0.26f, 0.36f, 0.004f), 0.001f);
            ScMeshObj("WorkOrder", bench, sheetMesh, paperMat, ScP(tr - 0.22f, topY + 0.19f, tf - 0.08f), new Vector3(-12f, 180f, 2f));
            var stand = ScRoundedBox("Stand", new Vector3(0.28f, 0.03f, 0.1f), 0.012f);
            ScMeshObj("Stand", bench, stand, Mat("ScCheckStand", new Color(0.2f, 0.22f, 0.26f), 0.4f, 0.2f), ScP(tr - 0.22f, topY + 0.015f, tf - 0.04f), Vector3.zero);
            var flat = ScRoundedBox("ClipFlat", new Vector3(0.23f, 0.012f, 0.32f), 0.004f);
            ScMeshObj("ClipboardFlat", bench, flat, Mat("ScCheckClipboard", new Color(0.62f, 0.44f, 0.26f), 0.4f, 0f), ScP(tr - 0.05f, topY + 0.008f, tf - 0.2f), new Vector3(0f, 188f, 0f));
            var flatPaper = ScRoundedBox("ClipFlatPaper", new Vector3(0.205f, 0.004f, 0.28f), 0.001f);
            ScMeshObj("ClipboardPaper", bench, flatPaper, Mat("ScCheckFormPaper", Color.white, 0.15f, 0f, ScTex("ScCheck_FormPaper")), ScP(tr - 0.05f, topY + 0.0155f, tf - 0.2f), new Vector3(0f, 188f, 0f), false);
            var flatClip = ScRoundedBox("ClipFlatClip", new Vector3(0.08f, 0.014f, 0.028f), 0.005f);
            ScMeshObj("ClipboardClip", bench, flatClip, Mat("ScCheckClip", new Color(0.62f, 0.64f, 0.68f), 0.6f, 0.9f), ScP(tr - 0.05f, topY + 0.022f, tf - 0.2f - 0.15f), new Vector3(0f, 188f, 0f), false);
            var penLathe = ScLathe("PenTable", new[] { new Vector2(0f, 0f), new Vector2(0.0025f, 0f), new Vector2(0.0058f, 0.012f), new Vector2(0.0058f, 0.15f), new Vector2(0.0045f, 0.155f), new Vector2(0f, 0.155f) }, 14);
            ScMeshObj("PenOnTable", bench, penLathe, Mat("ScCheckPen", new Color(0.08f, 0.16f, 0.5f), 0.6f, 0.3f), ScP(tr + 0.2f, topY + 0.0075f, tf - 0.15f), new Vector3(0f, 0f, 90f) + new Vector3(0f, 25f, 0f), false);

            // Glassware, flasks, beaker, and hoses removed (client request: single chemical prop per round, no extra lab containers)

            benchHeight = bench.gameObject.AddComponent<Sc04BenchHeight>();
            var bso = new SerializedObject(benchHeight);
            bso.FindProperty("builtTopY").floatValue = 0.91f;
            bso.FindProperty("minTop").floatValue = 0.72f;
            bso.FindProperty("belowHead").floatValue = 0.62f;
            bso.FindProperty("maxTop").floatValue = 0.95f;
            bso.ApplyModifiedPropertiesWithoutUndo();
            ScBlob(room, ScP(tr, 0f, tf), new Vector2(3.0f, 1.6f), 0.55f);

            // ---- left foreground: blue shelf with white jugs (like the picture), danger carton, drums
            var shelfPost = ScRoundedBox("ShelfPost", new Vector3(0.05f, 1.5f, 0.05f), 0.01f);
            var shelfBoard = ScRoundedBox("ShelfBoard", new Vector3(1.4f, 0.04f, 0.5f), 0.01f);
            const float sr = -6.4f, sf = 1.8f;
            foreach (var dr in new[] { -0.68f, 0.68f })
                foreach (var df in new[] { -0.22f, 0.22f })
                    ScMeshObj("ShelfPost", room, shelfPost, blue, ScP(sr + dr, 0.75f, sf + df), Vector3.zero);
            var smallJug = ScRoundedBox("SmallJug", new Vector3(0.2f, 0.26f, 0.14f), 0.03f);
            var smallCap = ScLathe("SmallCap", CapProfile(), 16);
            for (var lv = 0; lv < 3; lv++)
            {
                ScMeshObj("ShelfBoard", room, shelfBoard, blue, ScP(sr, 0.12f + lv * 0.62f, sf), Vector3.zero);
                for (var k = 0; k < 3; k++)
                {
                    var jr = sr - 0.42f + k * 0.42f;
                    var jy = 0.14f + lv * 0.62f;
                    ScMeshObj("ShelfJug", room, smallJug, plastic, ScP(jr, jy + 0.13f, sf), new Vector3(0f, (k * 17 + lv * 31) % 20 - 10f, 0f));
                    ScMeshObj("ShelfJugCap", room, smallCap, Mat("ScCheckCap", new Color(0.96f, 0.97f, 0.98f), 0.6f, 0f), ScP(jr + 0.03f, jy + 0.26f, sf), Vector3.zero, false);
                }
            }

            ScBlob(room, ScP(sr, 0f, sf), new Vector2(1.9f, 1.0f), 0.55f);
            var carton = ScRoundedBox("Carton", new Vector3(0.7f, 0.6f, 0.55f), 0.012f);
            ScMeshObj("Carton", room, carton, Mat("ScCheckCarton", new Color(0.66f, 0.52f, 0.34f), 0.15f, 0f), ScP(-4.9f, 0.3f, 5.8f), new Vector3(0f, 12f, 0f));
            var dangerFace = ScRoundedBox("DangerFace", new Vector3(0.52f, 0.5f, 0.004f), 0.001f);
            ScMeshObj("DangerFace", room, dangerFace, Mat("ScCheckDanger", Color.white, 0.3f, 0f, ScTex("ScCheck_DangerCarton")), ScP(-4.9f, 0.3f, 5.8f - 0.2765f) , new Vector3(0f, 12f, 0f), false);
            ScBlob(room, ScP(-4.9f, 0f, 5.8f), new Vector2(1.1f, 0.95f), 0.5f);

            var drum = ScLathe("Drum", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.285f, 0.015f), new Vector2(0.28f, 0.03f), new Vector2(0.28f, 0.2f), new Vector2(0.292f, 0.21f), new Vector2(0.28f, 0.22f),
                new Vector2(0.28f, 0.4f), new Vector2(0.292f, 0.41f), new Vector2(0.28f, 0.42f), new Vector2(0.28f, 0.58f), new Vector2(0.285f, 0.6f), new Vector2(0.27f, 0.6f), new Vector2(0f, 0.6f)
            }, 32);
            for (var i = 0; i < 4; i++)
            {
                var dr = -8.4f + (i % 2) * 0.62f;
                var df = 6.4f - (i / 2) * 0.62f;
                ScMeshObj("Drum" + i, room, drum, i % 2 == 0 ? blue : white, ScP(dr, 0f, df), Vector3.zero);
                ScBlob(room, ScP(dr, 0f, df), new Vector2(0.9f, 0.9f), 0.5f);
            }

            // ---- light shafts through the high windows of the far wall
            var shaftTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Env/FX_Shaft.png");
            if (shaftTex != null)
            {
                var shaftMat = MatAlpha("ScCheckShaft", shaftTex, new Color(1f, 0.93f, 0.78f, 0.13f), true);
                for (var i = 0; i < 3; i++)
                {
                    var q = QuadObj("Shaft" + i, room, ScP(-3.8f + i * 1.7f, 2.6f, fFar - 1.2f), new Vector3(-24f, 180f, 0f), new Vector2(1.5f, 4.6f), shaftMat);
                    q.transform.SetParent(room, true);
                }
            }
        }

        private static Vector2[] CapProfile()
        {
            var pts = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.046f, 0f) };
            for (var i = 0; i < 10; i++)
            {
                var y = 0.003f + i * 0.0033f;
                pts.Add(new Vector2(0.046f, y));
                pts.Add(new Vector2(i % 2 == 0 ? 0.0485f : 0.0455f, y + 0.0016f));
            }

            pts.Add(new Vector2(0.046f, 0.04f));
            pts.Add(new Vector2(0.04f, 0.044f));
            pts.Add(new Vector2(0f, 0.044f));
            return pts.ToArray();
        }
    }
}
#endif

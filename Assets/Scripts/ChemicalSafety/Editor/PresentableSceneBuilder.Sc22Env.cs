#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 22 (the last one): a worker decants acetone from a drum into an empty drinking-water bottle, the supervisor stops him. The film is the client
    /// animator's own work (Sc_22_v3.blend, 290 frames at 24 fps, exported to Assets/Art/Presentable/3D/Sc22/Sc22Film.glb, baked to Resources/Sc22/Sc22FilmBake.bytes).
    /// The blend only has a white box as a room, so the garment-factory hall of the client's workflow pictures (scene01-07.jpg) is built here: concrete pillars with
    /// blue / yellow signs and the chemical-guide poster, beams with pipes and tube lights, sewing tables with dark fabric on the left, fabric bales on the right,
    /// steel table behind the blue drums, worn grey floor with a yellow line. The labels are the client's own pictures (WhatsApp 13.03.20 / 13.03.26): they are
    /// wrapped on the drum, the water bottle and the can as real arc meshes 3 mm off the surface (so they sit tight, never loose). The supervisor, a pink dummy in
    /// the draft, gets a blue boiler suit painted from the mesh. Nothing of the other scenarios is touched. <c>Sc22Enabled = false</c> leaves the scenario out.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc22Enabled = true;
        private const float Sc22Ox = 820f;       // the room stands far from every other scenario (x = 820)
        private const float Sc22Scale = 0.5f;    // the animator's characters are ~3.5 units tall in Blender
        private const string Sc22Dir = ArtRoot + "/3D/Sc22";

        // hall box (metres, relative to the room origin)
        private const float Sc22X0 = -10f, Sc22X1 = 6.5f, Sc22Zb = -7.5f, Sc22Zf = 8.5f, Sc22H = 3.9f;

        private sealed class Sc22Refs
        {
            public Transform room;
            public Sc06Gate gate;
            public Sc22Cinematic cine;
            public Transform viewing;
            public Sc22CautionPopup caution;
        }

        private static Texture2D Sc22Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc22Dir + "/Tex/" + n + ".png");

        private static Vector3 Sc22P(float x, float y, float z) => new Vector3(Sc22Ox + x, y, z);

        private static Material Sc22Glass(string name, Color c, float smooth)
        {
            var had = Mats.ContainsKey(name);
            var m = Mat(name, c, smooth, 0f);
            if (had)
                return m;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Sc22LabelMat(string name, Texture2D tex, float smooth = 0.28f)
        {
            var had = Mats.ContainsKey(name);
            var m = Mat(name, Color.white, smooth, 0f, tex, null, false, true);
            if (!had)
            {
                m.SetFloat("_Cull", 0f);       // two-sided: the arc can never be culled by a winding mistake
                m.SetFloat("_Cutoff", 0.3f);
                EditorUtility.SetDirty(m);
            }

            return m;
        }

        /// <summary>A strip of a cylinder around the local Z axis (radius r, from z0 to z1, angle a0 to a1 in degrees), uv 0..1. flipU / flipV for the picture direction.</summary>
        private static Mesh Sc22Arc(string name, float r, float z0, float z1, float a0, float a1, int seg, bool flipU, bool flipV)
        {
            var path = GenDir + "/Sc22_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create)
                mesh = new Mesh();
            else
                mesh.Clear();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var t = i / (float)seg;
                var a = Mathf.Lerp(a0, a1, t) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var u = flipU ? 1f - t : t;
                v.Add(dir * r + new Vector3(0f, 0f, z0));
                v.Add(dir * r + new Vector3(0f, 0f, z1));
                n.Add(dir);
                n.Add(dir);
                uv.Add(new Vector2(u, flipV ? 1f : 0f));
                uv.Add(new Vector2(u, flipV ? 0f : 1f));
                if (i < seg)
                {
                    var b = i * 2;
                    tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                }
            }

            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (create)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>The label patch of the glb (a loose sheet next to the surface) is cut out of a copy of the mesh: the sub-mesh stays but has no triangles.</summary>
        private static Mesh Sc22DropSubmesh(Mesh src, int sub, string name)
        {
            var path = GenDir + "/Sc22_" + name + ".asset";
            var m = Object.Instantiate(src);
            m.name = name;
            m.SetTriangles(new int[0], sub);
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>The can's label patch (sub-mesh 1, a flat sheet on the can's -Y face) gets planar uv 0..1 over its own bounds (the glb uv showed a cut-out of
        /// the picture) and is lifted 3 mm off the face so it sits tight and never z-fights.</summary>
        private static Mesh Sc22CanLabelUv(Mesh src, string name)
        {
            var path = GenDir + "/Sc22_" + name + ".asset";
            var m = Object.Instantiate(src);
            m.name = name;
            var idx = new List<int>(m.GetIndices(1));
            var verts = new List<Vector3>(m.vertices);
            var uv = new List<Vector2>(m.uv);
            while (uv.Count < verts.Count)
                uv.Add(Vector2.zero);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var i in idx)
            {
                x0 = Mathf.Min(x0, verts[i].x);
                x1 = Mathf.Max(x1, verts[i].x);
                z0 = Mathf.Min(z0, verts[i].z);
                z1 = Mathf.Max(z1, verts[i].z);
            }

            var done = new HashSet<int>();
            foreach (var i in idx)
            {
                if (!done.Add(i))
                    continue;
                var v = verts[i];
                uv[i] = new Vector2((v.x - x0) / (x1 - x0), (z1 - v.z) / (z1 - z0));   // top of the picture = world up (the can's local -Z)
                v.y -= 0.003f;
                verts[i] = v;
            }

            // the glb jerrycan is faceted (hard normals per face): soft normals over the body (sub-mesh 0) so it reads as moulded plastic
            var nrm = new List<Vector3>(m.normals);
            if (nrm.Count == verts.Count)
            {
                var body = new HashSet<int>(m.GetIndices(0));
                var groups = new Dictionary<Vector3Int, List<int>>();
                foreach (var i in body)
                {
                    var key = Vector3Int.RoundToInt(verts[i] * 400f);
                    if (!groups.TryGetValue(key, out var gl))
                        groups[key] = gl = new List<int>();
                    gl.Add(i);
                }

                var soft = new Vector3[nrm.Count];
                foreach (var kv in groups)
                    foreach (var i in kv.Value)
                    {
                        var acc = Vector3.zero;
                        foreach (var j in kv.Value)
                            if (Vector3.Dot(nrm[i], nrm[j]) > 0.35f)
                                acc += nrm[j];
                        soft[i] = acc.sqrMagnitude > 1e-6f ? acc.normalized : nrm[i];
                    }

                foreach (var i in body)
                    nrm[i] = soft[i];
                m.SetNormals(nrm);
            }

            m.SetVertices(verts);
            m.SetUVs(0, uv);
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static GameObject Sc22AddArc(Transform parent, string name, Mesh mesh, Material mat)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        // ------------------------------------------------------------------ the room
        private static Sc22Refs BuildSc22Room(Transform env, PresentableArt art)
        {
            var refs = new Sc22Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            refs.room = room;
            if (!Sc22Enabled)
                return refs;
            Sc22TextureSettings();

            // bright factory hall (tube lights, daylight from the windows)
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.86f, 0.85f, 0.81f);   // the workflow picture averages about 100 / 255: not as dark as the last build (58)
            mso.FindProperty("ambientEquator").colorValue = new Color(0.72f, 0.71f, 0.67f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.40f, 0.39f, 0.36f);
            mso.FindProperty("fogColor").colorValue = new Color(0.55f, 0.55f, 0.52f);
            mso.FindProperty("fogStart").floatValue = 14f;
            mso.FindProperty("fogEnd").floatValue = 80f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.15f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ---- materials
            var wallM = Mat("Sc22Wall", new Color(1.46f, 1.52f, 1.62f), 0.04f, 0f, Sc22Tex("Sc22_Concrete"));   // painted white (client)
            wallM.SetTextureScale("_BaseMap", new Vector2(4f, 1f));
            var pillarM = Mat("Sc22Pillar", new Color(1.0f, 0.98f, 0.94f), 0.10f, 0f, Sc22Tex("Sc22_Pillar"));
            var floorM = Mat("Sc22Floor", Color.white, 0.06f, 0f, Sc22Tex("Sc22_Floor"));
            floorM.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
            var ceilM = Mat("Sc22Ceiling", new Color(1.0f, 1.0f, 0.97f), 0.08f, 0f, Sc22Tex("Sc22_Ceiling"));
            ceilM.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
            var beamM = Mat("Sc22Beam", new Color(0.92f, 0.91f, 0.88f), 0.10f, 0f, Sc22Tex("Sc22_Concrete"));
            var tubeM = Mat("Sc22Tube", new Color(0.95f, 0.97f, 1f), 0.5f, 0f, null, new Color(4.4f, 4.6f, 4.9f));
            var winM = Mat("Sc22Window", new Color(0.95f, 0.92f, 0.84f), 0.2f, 0f, null, new Color(2.2f, 2.1f, 1.8f));
            var wallBackM = Mat("Sc22WallBack", new Color(1.52f, 1.58f, 1.68f), 0.04f, 0f, Sc22Tex("Sc22_Concrete"));   // painted white (client)
            wallBackM.SetTextureScale("_BaseMap", new Vector2(4f, 1f));
            var frameM = Mat("Sc22WinFrame", new Color(0.25f, 0.26f, 0.27f), 0.35f, 0.6f);
            var pipeRed = Mat("Sc22PipeRed", new Color(0.52f, 0.11f, 0.08f), 0.35f, 0.45f);
            var conduitM = Mat("Sc22Conduit", new Color(0.50f, 0.51f, 0.51f), 0.35f, 0.5f);
            var baleM = Mat("Sc22Bale", new Color(1.1f, 1.1f, 1.06f), 0.05f, 0f, Sc22Tex("Sc22_FabricLight"));
            var fabricDarkM = Mat("Sc22FabricDark", Color.white, 0.05f, 0f, Sc22Tex("Sc22_FabricDark"));
            var steelM = Mat("Sc22Steel", new Color(1.1f, 1.12f, 1.14f), 0.40f, 0.55f, Sc22Tex("Sc22_Steel"));
            var woodM = Mat("Sc22Wood", new Color(0.30f, 0.17f, 0.09f), 0.30f, 0f);
            var grayBox = Mat("Sc22GrayBox", new Color(0.55f, 0.56f, 0.55f), 0.4f, 0.5f);
            var blueDrumM = Mat("Sc22BlueDrum", new Color(0.04f, 0.15f, 0.55f), 0.55f, 0f);
            var lineM = Mat("Sc22YellowLine", Color.white, 0.2f, 0f, Sc22Tex("Sc22_YellowLine"), null, false, true);
            var signBlueM = Mat("Sc22SignBlue", Color.white, 0.25f, 0f, Sc22Tex("Sc22_SignBlue"));
            var signYellowM = Mat("Sc22SignYellow", Color.white, 0.25f, 0f, Sc22Tex("Sc22_SignYellow"));
            var posterM = Mat("Sc22Poster", Color.white, 0.25f, 0f, Sc22Tex("Sc22_PosterChem"));

            GameObject B(string n, Vector3 centre, Vector3 size, Material m, bool collider = false)
            {
                var g = Box(n, room, centre, size, m);
                var c = g.GetComponent<Collider>();
                if (c != null && !collider)
                    c.enabled = false;
                return g;
            }

            GameObject Quad(string n, Vector3 pos, Vector3 euler, Vector2 size, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                g.name = n;
                g.transform.SetParent(room, false);
                g.transform.position = pos;
                g.transform.rotation = Quaternion.Euler(euler);
                g.transform.localScale = new Vector3(size.x, size.y, 1f);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                return g;
            }

            GameObject Cyl(string n, Vector3 pos, Vector3 euler, float dia, float len, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = n;
                g.transform.SetParent(room, false);
                g.transform.position = pos;
                g.transform.rotation = Quaternion.Euler(euler);
                g.transform.localScale = new Vector3(dia, len * 0.5f, dia);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }

            var cx = (Sc22X0 + Sc22X1) * 0.5f;
            var cz = (Sc22Zb + Sc22Zf) * 0.5f;
            var sx = Sc22X1 - Sc22X0;
            var sz = Sc22Zf - Sc22Zb;

            // ---- shell
            B("Sc22Floor", Sc22P(cx, -0.1f, cz), new Vector3(sx, 0.2f, sz), floorM, true);
            B("Sc22Ceiling", Sc22P(cx, Sc22H + 0.1f, cz), new Vector3(sx + 0.4f, 0.2f, sz + 0.4f), ceilM);
            B("Sc22WallBack", Sc22P(cx, Sc22H * 0.5f, Sc22Zb - 0.1f), new Vector3(sx + 0.4f, Sc22H, 0.2f), wallBackM);
            B("Sc22WallFront", Sc22P(cx, Sc22H * 0.5f, Sc22Zf + 0.1f), new Vector3(sx + 0.4f, Sc22H, 0.2f), wallM);
            B("Sc22WallLeft", Sc22P(Sc22X0 - 0.1f, Sc22H * 0.5f, cz), new Vector3(0.2f, Sc22H, sz + 0.4f), wallM);
            B("Sc22WallRight", Sc22P(Sc22X1 + 0.1f, Sc22H * 0.5f, cz), new Vector3(0.2f, Sc22H, sz + 0.4f), wallM);

            // windows (daylight) on the back wall and the side walls, frames and mullions
            for (var i = 0; i < 6; i++)
            {
                var wx = -8f + i * 2.8f;
                Quad("Sc22WinBack" + i, Sc22P(wx, 2.35f, Sc22Zb + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(1.8f, 1.2f), winM);
                B("Sc22WinFrameT" + i, Sc22P(wx, 2.97f, Sc22Zb + 0.03f), new Vector3(1.9f, 0.06f, 0.05f), frameM);
                B("Sc22WinFrameB" + i, Sc22P(wx, 1.73f, Sc22Zb + 0.03f), new Vector3(1.9f, 0.06f, 0.05f), frameM);
                B("Sc22WinMull" + i, Sc22P(wx, 2.35f, Sc22Zb + 0.03f), new Vector3(0.05f, 1.2f, 0.05f), frameM);
            }

            for (var i = 0; i < 4; i++)
            {
                var wz = -5.5f + i * 3.6f;
                Quad("Sc22WinLeft" + i, Sc22P(Sc22X0 + 0.012f, 2.35f, wz), new Vector3(0f, 90f, 0f), new Vector2(1.8f, 1.2f), winM);
                Quad("Sc22WinRight" + i, Sc22P(Sc22X1 - 0.012f, 2.35f, wz), new Vector3(0f, -90f, 0f), new Vector2(1.8f, 1.2f), winM);
            }

            // black skirting, 12 inches (0.305 m) high, along the four walls (client 2026-11-17: white walls, black skirting)
            {
                var skirtM = Mat("Sc22Skirting", new Color(0.05f, 0.05f, 0.055f), 0.20f, 0f);
                const float skH = 0.305f, skT = 0.02f;
                B("Sc22SkirtBack", Sc22P(cx, skH * 0.5f, Sc22Zb + skT * 0.5f), new Vector3(sx, skH, skT), skirtM);
                B("Sc22SkirtFront", Sc22P(cx, skH * 0.5f, Sc22Zf - skT * 0.5f), new Vector3(sx, skH, skT), skirtM);
                B("Sc22SkirtLeft", Sc22P(Sc22X0 + skT * 0.5f, skH * 0.5f, cz), new Vector3(skT, skH, sz), skirtM);
                B("Sc22SkirtRight", Sc22P(Sc22X1 - skT * 0.5f, skH * 0.5f, cz), new Vector3(skT, skH, sz), skirtM);
            }

            // ---- pillars with the client's signs and poster
            float[,] pil = { { 2.8f, -3.0f }, { -4.6f, -3.4f }, { 2.8f, 3.6f }, { -6.2f, 4.2f }, { 5.0f, -6.0f }, { -9.0f, -2.0f } };
            for (var i = 0; i < pil.GetLength(0); i++)
                B("Sc22Pillar" + i, Sc22P(pil[i, 0], Sc22H * 0.5f, pil[i, 1]), new Vector3(0.55f, Sc22H, 0.55f), pillarM, true);
            // pillar 0 (ahead): blue sign, yellow warning sign, two grey switch boxes
            Quad("Sc22SignBlue", Sc22P(2.8f - 0.05f, 2.45f, -3.0f + 0.2812f), new Vector3(0f, 180f, 0f), new Vector2(0.46f, 0.46f), signBlueM);
            Quad("Sc22SignYellow", Sc22P(2.8f + 0.2f, 1.75f, -3.0f + 0.2812f), new Vector3(0f, 180f, 0f), new Vector2(0.20f, 0.34f), signYellowM);
            B("Sc22SwitchBox0", Sc22P(2.8f - 0.28f, 1.7f, -3.0f + 0.31f), new Vector3(0.34f, 0.40f, 0.12f), grayBox);
            B("Sc22SwitchBox1", Sc22P(2.8f - 0.62f, 1.55f, -3.0f + 0.31f), new Vector3(0.26f, 0.30f, 0.12f), grayBox);
            // pillar 1 (right): the chemical-guide poster
            Quad("Sc22PosterChem", Sc22P(-4.6f + 0.2f, 1.85f, -3.4f + 0.2812f), new Vector3(0f, 180f, 0f), new Vector2(0.66f, 1.15f), posterM);

            // ---- ceiling: beams, pipes, conduit, tube lights, fans
            for (var i = 0; i < 4; i++)
                B("Sc22BeamX" + i, Sc22P(cx, Sc22H - 0.28f, -5.2f + i * 4.2f), new Vector3(sx, 0.55f, 0.45f), beamM);
            for (var i = 0; i < 5; i++)
                B("Sc22BeamZ" + i, Sc22P(-8.5f + i * 4.3f, Sc22H - 0.28f, cz), new Vector3(0.45f, 0.55f, sz), beamM);
            Cyl("Sc22PipeRedA", Sc22P(cx, Sc22H - 0.72f, -4.2f), new Vector3(0f, 0f, 90f), 0.11f, sx, pipeRed);
            Cyl("Sc22PipeRedB", Sc22P(cx, Sc22H - 0.72f, 0.4f), new Vector3(0f, 0f, 90f), 0.09f, sx, pipeRed);
            Cyl("Sc22ConduitA", Sc22P(cx, Sc22H - 0.55f, -4.6f), new Vector3(0f, 0f, 90f), 0.045f, sx, conduitM);
            Cyl("Sc22ConduitB", Sc22P(cx, Sc22H - 0.58f, 1.4f), new Vector3(0f, 0f, 90f), 0.04f, sx, conduitM);
            Cyl("Sc22ConduitC", Sc22P(-3.9f, Sc22H - 0.9f, cz), new Vector3(90f, 0f, 0f), 0.05f, sz, conduitM);
            for (var r = 0; r < 4; r++)
                for (var i = 0; i < 6; i++)
                {
                    var tx = -8.5f + i * 3.0f + (r % 2) * 1.5f;
                    var tz = -5.5f + r * 4.0f;
                    B("Sc22TubeHousing" + r + "_" + i, Sc22P(tx, Sc22H - 0.50f, tz), new Vector3(1.3f, 0.05f, 0.18f), frameM);
                    B("Sc22Tube" + r + "_" + i, Sc22P(tx, Sc22H - 0.54f, tz), new Vector3(1.2f, 0.04f, 0.12f), tubeM);
                }

            foreach (var fp in new[] { new Vector2(-2.5f, -0.5f), new Vector2(3.5f, 2.0f), new Vector2(-6.5f, 1.0f) })
            {
                var fan = new GameObject("Sc22Fan").transform;
                fan.SetParent(room, false);
                fan.position = Sc22P(fp.x, Sc22H - 0.95f, fp.y);
                var rod = Cyl("Rod", fan.position + Vector3.up * 0.45f, Vector3.zero, 0.03f, 0.9f, frameM);
                rod.transform.SetParent(fan, true);
                var hub = Cyl("Hub", fan.position, Vector3.zero, 0.22f, 0.08f, frameM);
                hub.transform.SetParent(fan, true);
                for (var b = 0; b < 3; b++)
                {
                    var blade = Box("Blade" + b, fan, Vector3.zero, new Vector3(0.65f, 0.012f, 0.14f), frameM);
                    Object.DestroyImmediate(blade.GetComponent<Collider>());
                    blade.transform.position = fan.position;
                    blade.transform.rotation = Quaternion.Euler(0f, b * 120f, 0f);
                    blade.transform.position += blade.transform.right * 0.5f;
                }

                var spin = fan.gameObject.AddComponent<Sc22Spin>();
                var sso = new SerializedObject(spin);
                sso.FindProperty("degPerSecond").floatValue = 90f;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- floor markings (worn yellow safety lines)
            Quad("Sc22LineA", Sc22P(cx, 0.004f, -2.4f), new Vector3(90f, 0f, 0f), new Vector2(sx, 0.11f), lineM);
            Quad("Sc22LineB", Sc22P(-3.8f, 0.004f, cz), new Vector3(90f, 90f, 0f), new Vector2(sz, 0.11f), lineM);

            // ---- the back and the left: sewing machines on their tables with stools (Scenario 6 SewingMachine.glb, the same model as Scenarios 6 / 7), dark fabric,
            // a carton and a jerrycan on the floor (client picture: the far left of the hall is the sewing section)
            var sewSpots = new[]
            {
                new Vector3(0.6f, 0f, -5.0f), new Vector3(2.3f, 0f, -5.0f), new Vector3(4.0f, 0f, -5.0f),
                new Vector3(4.6f, 0f, -2.8f), new Vector3(4.6f, 0f, -1.0f), new Vector3(4.6f, 0f, 0.8f), new Vector3(4.6f, 0f, 2.6f),
                new Vector3(-2.0f, 0f, -5.6f), new Vector3(-3.8f, 0f, -5.6f)
            };
            for (var si = 0; si < sewSpots.Length; si++)
                Sc22SewingUnit(room, Sc22P(sewSpots[si].x, 0f, sewSpots[si].z), si < 3 || si > 6 ? 180f : 270f, si, fabricDarkM);
            B("Sc22Carton", Sc22P(5.4f, 0.22f, 3.8f), new Vector3(0.55f, 0.44f, 0.45f), Mat("Sc22Carton", new Color(0.55f, 0.42f, 0.28f), 0.15f, 0f));
            B("Sc22FloorCan", Sc22P(3.4f, 0.22f, 4.2f), new Vector3(0.30f, 0.44f, 0.24f), Mat("Sc22FloorCan", new Color(0.92f, 0.93f, 0.94f), 0.45f, 0f));

            // ---- the right: fabric bales
            for (var s = 0; s < 3; s++)
                for (var layer = 0; layer < 3 - s % 2; layer++)
                    B("Sc22Bale" + s + "_" + layer, Sc22P(-7.2f - (s % 2) * 0.1f, 0.36f + layer * 0.72f, -5.8f + s * 1.5f), new Vector3(1.1f, 0.70f, 1.3f), baleM);

            // ---- steel table behind the drum, a blue drum under it
            B("Sc22TableTop", Sc22P(-0.9f, 0.93f, -1.15f), new Vector3(3.6f, 0.05f, 0.85f), steelM, true);
            B("Sc22TableShelf", Sc22P(-0.9f, 0.28f, -1.15f), new Vector3(3.5f, 0.03f, 0.78f), steelM);
            B("Sc22TableBack", Sc22P(-0.9f, 1.15f, -1.55f), new Vector3(3.6f, 0.4f, 0.04f), steelM);
            foreach (var lx in new[] { -2.6f, 0.8f })
                foreach (var lz in new[] { -1.5f, -0.8f })
                    B("Sc22TableLeg", Sc22P(lx, 0.46f, lz), new Vector3(0.05f, 0.92f, 0.05f), steelM);
            // (the blue drum under the table that floated on the shelf was removed: client review 2026-11-17)

            // ---- the animator's film scene
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Sc22Dir + "/Sc22Film.glb");
            if (asset == null)
            {
                Debug.LogError("[Sc22] Sc22Film.glb missing in " + Sc22Dir);
                return refs;
            }

            var film = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(film, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            film.name = "Sc22Film";
            film.transform.SetParent(room, false);
            film.transform.position = new Vector3(Sc22Ox, 0f, 0f);
            film.transform.rotation = Quaternion.identity;
            film.transform.localScale = Vector3.one * Sc22Scale;
            foreach (var a in film.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
            Sc04NoColliders(film);
            foreach (var sk in film.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sk.updateWhenOffscreen = true;

            // the draft's white box (floor / walls), rig control shapes and helper markers are not part of the picture: the hall above is
            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("cs_") || r.name == "BG" || r.name == "BG02" || r.name == "BG02.001" || r.name == "BG03" || r.name == "Pen" || r.name.StartsWith("empty"))
                    r.enabled = false;

            var props = Sc22Props(film);
            Sc22Recolour(film);

            // ---- the cinematic (player of the film)
            var cineGo = new GameObject("Sc22Cinematic");
            cineGo.transform.SetParent(room, false);
            var cine = cineGo.AddComponent<Sc22Cinematic>();
            var cso = new SerializedObject(cine);
            cso.FindProperty("filmRoot").objectReferenceValue = film.transform;

            AudioSource Src(string n, bool loop, float vol)
            {
                var g = new GameObject(n);
                g.transform.SetParent(cineGo.transform, false);
                var s = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = loop;
                s.volume = vol;
                s.spatialBlend = 0.85f;
                s.minDistance = 2.5f;
                s.maxDistance = 40f;
                return s;
            }

            cso.FindProperty("fxSource").objectReferenceValue = Src("FxSource", false, 1f);
            cso.FindProperty("loopSource").objectReferenceValue = Src("LoopSource", true, 0.4f);
            cso.FindProperty("pour").objectReferenceValue = Sc22BuildPour(room, props);
            cso.ApplyModifiedPropertiesWithoutUndo();
            refs.cine = cine;

            // lights: a few warm-white points around the action, the rest of the hall comes from the ambient light and the emissive tubes
            foreach (var lp in new[] { new Vector3(-1.2f, 3.0f, 0.8f), new Vector3(1.8f, 3.0f, -0.8f), new Vector3(-3.5f, 3.0f, 3.2f), new Vector3(-0.5f, 3.0f, 3.6f), new Vector3(3.0f, 3.0f, 3.0f) })
            {
                var lg = new GameObject("Sc22Light");
                lg.transform.SetParent(room, false);
                lg.transform.position = Sc22P(lp.x, lp.y, lp.z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 8f;
                l.intensity = 1.0f;
                l.color = new Color(1f, 0.97f, 0.92f);
                l.shadows = LightShadows.None;
            }

            var amb = new GameObject("HallAmbience");
            amb.transform.SetParent(room, false);
            var asrc = amb.AddComponent<AudioSource>();
            asrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc20/Hall_Loop.wav");
            asrc.loop = true;
            asrc.playOnAwake = true;
            asrc.spatialBlend = 0f;
            asrc.volume = 0.22f;

            // where the learner stands before / after the film: where the animator's camera is, looking at the drums
            refs.viewing = Marker("Sc22Viewing", room, Sc22P(-1.9f, 0f, 3.9f), 157f);

            // ---- gate card after the film
            var gateGo = new GameObject("Sc22GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc22Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc22Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc22Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // ---- the caution popup (the film pauses when the supervisor stops the worker)
            var cautionGo = new GameObject("Sc22CautionHost");
            cautionGo.transform.SetParent(room, false);
            refs.caution = cautionGo.AddComponent<Sc22CautionPopup>();
            SetRef(refs.caution, "art", art);
            return refs;
        }

        // ------------------------------------------------------------------ liquid, stream, tap, glass, labels
        private sealed class Sc22PropRefs
        {
            public Transform tapTip, bottleMouth, bottleLiquid, bottle2Liquid, canMouth, canLiquid, bottleCap, canCap;
        }

        /// <summary>Unit liquid body along local +Z (z 0..1): a cylinder of radius 1 or a box of 1 x 1; side walls and a top face (the bottom is never seen).</summary>
        private static Mesh Sc22LiquidMesh(string name, bool box)
        {
            var path = GenDir + "/Sc22_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create)
                mesh = new Mesh();
            else
                mesh.Clear();
            var v = new List<Vector3>();
            var nm = new List<Vector3>();
            var tr = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                var k = v.Count;
                v.AddRange(new[] { a, b, c, d });
                nm.AddRange(new[] { n, n, n, n });
                tr.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            if (box)
            {
                var c = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
                for (var i = 0; i < 4; i++)
                {
                    var a = c[i];
                    var b = c[(i + 1) % 4];
                    var mid = (a + b) * 0.5f;
                    Quad(a, b, b + Vector3.forward, a + Vector3.forward, new Vector3(mid.x, mid.y, 0f).normalized);
                }

                Quad(c[0] + Vector3.forward, c[1] + Vector3.forward, c[2] + Vector3.forward, c[3] + Vector3.forward, Vector3.forward);
            }
            else
            {
                const int seg = 28;
                for (var i = 0; i < seg; i++)
                {
                    var a0 = i / (float)seg * Mathf.PI * 2f;
                    var a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                    var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    var k = v.Count;
                    v.AddRange(new[] { p0, p1, p1 + Vector3.forward, p0 + Vector3.forward });
                    nm.AddRange(new[] { p0, p1, p1, p0 });
                    tr.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                    // top cap
                    var kc = v.Count;
                    v.AddRange(new[] { Vector3.forward, p0 + Vector3.forward, p1 + Vector3.forward });
                    nm.AddRange(new[] { Vector3.forward, Vector3.forward, Vector3.forward });
                    tr.AddRange(new[] { kc, kc + 1, kc + 2 });
                }
            }

            mesh.SetVertices(v);
            mesh.SetNormals(nm);
            mesh.SetTriangles(tr, 0);
            mesh.RecalculateBounds();
            if (create)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Unit open tube along local +Z (radius 1, z 0..1): the stream from the tap.</summary>
        private static Mesh Sc22TubeMesh()
        {
            var path = GenDir + "/Sc22_StreamTube.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var create = mesh == null;
            if (create)
                mesh = new Mesh();
            else
                mesh.Clear();
            const int seg = 10;
            var v = new List<Vector3>();
            var nm = new List<Vector3>();
            var tr = new List<int>();
            for (var i = 0; i < seg; i++)
            {
                var a0 = i / (float)seg * Mathf.PI * 2f;
                var a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                var k = v.Count;
                v.AddRange(new[] { p0, p1, p1 + Vector3.forward, p0 + Vector3.forward });
                nm.AddRange(new[] { p0, p1, p1, p0 });
                tr.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            mesh.SetVertices(v);
            mesh.SetNormals(nm);
            mesh.SetTriangles(tr, 0);
            mesh.RecalculateBounds();
            if (create)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material Sc22TwoSided(Material m)
        {
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Sc22PropRefs Sc22Props(GameObject film)
        {
            var refs = new Sc22PropRefs();
            var ghs = Sc22LabelMat("Sc22LabelDrumGhs", Sc22Tex("Sc22_LabelDrumGhs"));
            var plate = Sc22LabelMat("Sc22LabelDrumPlate", Sc22Tex("Sc22_LabelDrumPlate"));
            var water = Sc22LabelMat("Sc22LabelWater", Sc22Tex("Sc22_LabelWater"), 0.45f);
            var canLab = Sc22LabelMat("Sc22LabelCan", Sc22Tex("Sc22_LabelCan"));
            // clear PET bottle: the fresnel glass of Scenario 6 (visible rim = thickness), the can a translucent white HDPE so the liquid shows through
            var glass = Sc06GlassMat("Sc22BottleGlassFx", new Color(0.80f, 0.91f, 1f, 1f), 0.10f, 0.95f);
            var canGlass = Sc06GlassMat("Sc22CanGlassFx", new Color(0.97f, 0.98f, 1f, 1f), 0.26f, 0.85f);
            var drumBlue = Mat("Sc22DrumBlue", new Color(0.03f, 0.16f, 0.62f), 0.55f, 0f);
            var capBlue = Mat("Sc22CapBlue", new Color(0.04f, 0.22f, 0.80f), 0.66f, 0f);   // blue screw caps (client)
            var chrome = Mat("Sc22TapChrome", new Color(1.0f, 0.76f, 0.30f), 0.80f, 0.85f);   // golden tap (client 2026-11-17)
            // acetone: colourless and clear like water (very faint cool tint), seen by its surface and highlights
            var acetone = Sc22TwoSided(Sc06Liquid("Sc22Acetone", new Color(0.80f, 0.92f, 1f, 0.42f)));
            var cylMesh = Sc22LiquidMesh("LiquidCyl", false);
            var boxMesh = Sc22LiquidMesh("LiquidBox", true);

            Transform Liquid(Transform parent, string nm, Mesh m, Vector3 pos, Vector2 xy)
            {
                var g = new GameObject(nm);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // local +Z of the mesh = towards the top of the container (-Z of the node)
                g.transform.localScale = new Vector3(xy.x, xy.y, 0.0001f);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = acetone;
                r.shadowCastingMode = ShadowCastingMode.Off;
                g.SetActive(false);
                return g.transform;
            }

            // a screw cap: a short cylinder along the container axis, local +Z of the mesh = towards the top (-Z of the node)
            Transform Cap(Transform parent, string nm, Vector3 pos, Vector3 scale)
            {
                var g = new GameObject(nm);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                g.transform.localScale = scale;
                g.AddComponent<MeshFilter>().sharedMesh = cylMesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = capBlue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return g.transform;
            }

            Transform Empty(Transform parent, string nm, Vector3 pos)
            {
                var g = new GameObject(nm);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                return g.transform;
            }

            foreach (var mr in film.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    continue;
                var mesh = mf.sharedMesh;
                var n = mr.name;
                var mats = mr.sharedMaterials;
                if (n == "Tank" || n == "Tank.001")
                {
                    // drum: the animator's loose label patch (sub-mesh 1) is dropped; real arcs on the surface instead (axis = local Z, 'up' = -Z, the patch looked to local -Y)
                    if (mats.Length > 0)
                        mats[0] = drumBlue;
                    if (mats.Length > 2)
                        mats[2] = chrome;   // the tap (brass in the glb) and the bung rings: chrome like the client's picture
                    mr.sharedMaterials = mats;
                    mf.sharedMesh = Sc22DropSubmesh(mesh, 1, "DrumNoLabel_" + n.Replace('.', '_'));
                    if (n != "Tank")
                        continue;
                    refs.tapTip = Empty(mr.transform, "Sc22TapTip", new Vector3(-7.10f, 0f, -16.88f));
                    const float r = 5.038f + 0.03f;
                    var arcDeg = 66f;
                    var wArc = r * arcDeg * Mathf.Deg2Rad;
                    var hGhs = wArc / (847f / 567f);
                    var hPlate = wArc / (863f / 290f);
                    var zTop = -11.8f - hGhs * 0.5f;      // upper edge of the label (towards the drum top)
                    var zGhs0 = zTop;
                    var zGhs1 = zTop + hGhs;
                    var zP0 = zGhs1 + 0.35f;
                    var zP1 = zP0 + hPlate;
                    Sc22AddArc(mr.transform, "Sc22LabelGhs", Sc22Arc("ArcGhs", r, zGhs0, zGhs1, -90f - arcDeg * 0.5f, -90f + arcDeg * 0.5f, 20, false, true), ghs);
                    Sc22AddArc(mr.transform, "Sc22LabelPlate", Sc22Arc("ArcPlate", r, zP0, zP1, -90f - arcDeg * 0.5f, -90f + arcDeg * 0.5f, 20, false, true), plate);
                }
                else if (n == "Bottole" || n == "Bottole.001")
                {
                    if (mats.Length > 0)
                        mats[0] = glass;
                    mr.sharedMaterials = mats;
                    mf.sharedMesh = Sc22DropSubmesh(mesh, 1, "BottleNoLabel_" + n.Replace('.', '_'));
                    // two copies of the DRINKING WATER sticker on opposite sides of the bottle (reads from either side), 3 mm off the wall
                    const float r = 1.0f + 0.02f;
                    const float h = 1.567f;
                    var wArc = h * (1162f / 627f);
                    var arcDeg = wArc / r * Mathf.Rad2Deg;
                    var z0 = -3.372f - h * 0.5f;
                    var z1 = z0 + h;
                    for (var k = 0; k < 2; k++)
                    {
                        var c = k * 180f;
                        Sc22AddArc(mr.transform, "Sc22LabelWater" + k, Sc22Arc("ArcWater" + k, r, z0, z1, c - arcDeg * 0.5f, c + arcDeg * 0.5f, 20, false, true), water);
                    }

                    // the acetone inside: bottom at local z -0.04, grows towards the neck (-Z)
                    var liq = Liquid(mr.transform, "Sc22Liquid", cylMesh, new Vector3(0f, 0f, -0.06f), new Vector2(0.93f, 0.93f));
                    // blue cap on the neck: off while the bottle is filled (Bottole, see Sc22Pour), on in the bin (Bottole.001)
                    var bcap = Cap(mr.transform, "Sc22Cap", new Vector3(0f, 0f, -5.45f), new Vector3(0.40f, 0.40f, 0.56f));
                    if (n == "Bottole")
                    {
                        refs.bottleCap = bcap;
                        refs.bottleLiquid = liq;
                        refs.bottleMouth = Empty(mr.transform, "Sc22Mouth", new Vector3(0f, 0f, -5.97f));
                    }
                    else
                    {
                        refs.bottle2Liquid = liq;
                    }
                }
                else if (n == "Can" || n == "Can.001")
                {
                    if (mats.Length > 0)
                        mats[0] = canGlass;
                    if (mats.Length > 1)
                        mats[1] = canLab;
                    mr.sharedMaterials = mats;
                    if (mesh.subMeshCount > 1 && mesh.isReadable)
                        mf.sharedMesh = Sc22CanLabelUv(mesh, "CanLabelUv_" + n.Replace('.', '_'));
                    var ccap = Cap(mr.transform, "Sc22Cap", new Vector3(0.28f, 0f, -1.17f), new Vector3(0.125f, 0.125f, 0.17f));
                    if (n == "Can.001")
                    {
                        refs.canCap = ccap;   // unscrewed and lifted away before the pour (Sc22Pour)
                        refs.canLiquid = Liquid(mr.transform, "Sc22Liquid", boxMesh, new Vector3(0f, 0f, 0f), new Vector2(0.88f, 0.40f));
                        refs.canMouth = Empty(mr.transform, "Sc22Mouth", new Vector3(0.28f, 0f, -1.27f));
                    }
                }
            }

            return refs;
        }

        /// <summary>The stream from the tap tip, its drops and the liquid levels (Sc22Pour), wired to the props.</summary>
        private static Sc22Pour Sc22BuildPour(Transform room, Sc22PropRefs pr)
        {
            var go = new GameObject("Sc22Pour");
            go.transform.SetParent(room, false);
            var pour = go.AddComponent<Sc22Pour>();

            var stream = new GameObject("Sc22Stream");
            stream.transform.SetParent(room, false);
            stream.AddComponent<MeshFilter>().sharedMesh = Sc22TubeMesh();
            var sr = stream.AddComponent<MeshRenderer>();
            sr.sharedMaterial = Sc22TwoSided(Sc06Liquid("Sc22Stream", new Color(0.94f, 0.98f, 1f, 0.72f)));
            sr.shadowCastingMode = ShadowCastingMode.Off;
            stream.SetActive(false);

            var sp = new GameObject("Sc22Splash");
            sp.transform.SetParent(room, false);
            var ps = sp.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.38f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.95f, 0.98f, 1f, 0.75f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.012f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // upwards: the drops splash back out of the mouth
            var pr2 = sp.GetComponent<ParticleSystemRenderer>();
            pr2.sharedMaterial = ParticleMat("Sc22Drop", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Presentable/3D/Sc20/Tex/Sc20_Drop.png"), false);
            pr2.shadowCastingMode = ShadowCastingMode.Off;

            var so = new SerializedObject(pour);
            so.FindProperty("tapTip").objectReferenceValue = pr.tapTip;
            so.FindProperty("bottleMouth").objectReferenceValue = pr.bottleMouth;
            so.FindProperty("bottleLiquid").objectReferenceValue = pr.bottleLiquid;
            so.FindProperty("bottle2Liquid").objectReferenceValue = pr.bottle2Liquid;
            so.FindProperty("canMouth").objectReferenceValue = pr.canMouth;
            so.FindProperty("canLiquid").objectReferenceValue = pr.canLiquid;
            so.FindProperty("bottleCap").objectReferenceValue = pr.bottleCap;
            so.FindProperty("canCap").objectReferenceValue = pr.canCap;
            so.FindProperty("stream").objectReferenceValue = stream.transform;
            so.FindProperty("splash").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pour;
        }

        /// <summary>A sewing machine on its table with the operator's stool (Scenario 6 glb, scaled so the table top is 0.80 m, lit URP materials like Scenario 7),
        /// the needle bar moving, a pile of dark cloth on the table. at = floor point of the centre of the unit, yaw in degrees.</summary>
        private static void Sc22SewingUnit(Transform parent, Vector3 at, float yaw, int idx, Material cloth)
        {
            var model = SpawnSc06("SewingMachine.glb", parent);
            if (model == null)
                return;
            model.name = "Sc22Sewing" + idx;
            foreach (var mr in model.GetComponentsInChildren<Renderer>(true))
            {
                var mms = mr.sharedMaterials;
                for (var mi = 0; mi < mms.Length; mi++)
                {
                    var gm = mms[mi];
                    if (gm == null || gm.shader == null || !gm.shader.name.Contains("glTF"))
                        continue;
                    var gt = gm.HasProperty("baseColorTexture") ? gm.GetTexture("baseColorTexture") : null;
                    var gc = gm.HasProperty("baseColorFactor") ? gm.GetColor("baseColorFactor") : Color.white;
                    var metalish = gm.name.Contains("makinesi") || gm.name.Contains("default");
                    mms[mi] = Mat("Sc07Gl_" + gm.name.Replace(" ", "_"), gc, metalish ? 0.45f : 0.3f, metalish ? 0.25f : 0f, gt, null, false);
                }

                mr.sharedMaterials = mms;
            }

            var b = BoundsOfTree(model.transform);
            var topY0 = b.min.y + b.size.y * 0.6f;
            foreach (var tr in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var tmf = tr.GetComponent<MeshFilter>();
                if (tmf == null || tmf.sharedMesh == null)
                    continue;
                var tms = tr.sharedMaterials;
                for (var si = 0; si < tms.Length && si < tmf.sharedMesh.subMeshCount; si++)
                {
                    if (tms[si] == null || !tms[si].name.Contains("Oak"))
                        continue;
                    var vv = tmf.sharedMesh.vertices;
                    var l2w = tr.transform.localToWorldMatrix;
                    var mx = float.MinValue;
                    foreach (var vi in tmf.sharedMesh.GetIndices(si))
                        mx = Mathf.Max(mx, l2w.MultiplyPoint3x4(vv[vi]).y);
                    topY0 = mx;
                }
            }

            var k = topY0 - b.min.y > 0.05f ? 0.80f / (topY0 - b.min.y) : 1f;
            model.transform.localScale = Vector3.one * k;
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.rotation;
            b = BoundsOfTree(model.transform);
            model.transform.position += new Vector3(at.x - b.center.x, -b.min.y, at.z - b.center.z);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
            Sc04NoColliders(model);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "dikis_makinesi_ignesi")
                {
                    var bob = t.gameObject.AddComponent<Sc06Bobber>();
                    var bso = new SerializedObject(bob);
                    bso.FindProperty("amplitude").floatValue = 0.012f;
                    bso.FindProperty("hertz").floatValue = 3f + idx * 0.27f;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }

            b = BoundsOfTree(model.transform);
            var pile = Box("Sc22Cloth" + idx, parent, new Vector3(b.center.x, 0.86f, b.center.z), new Vector3(0.30f, 0.07f, 0.24f), cloth);
            var pc = pile.GetComponent<Collider>();
            if (pc != null)
                pc.enabled = false;
        }

        /// <summary>The supervisor (rig.001, 'body low', a pink dummy in the draft) gets the blue boiler suit of the workflow picture, his hard hat is yellow.
        /// The other worker keeps the animator's colours.</summary>
        private static void Sc22Recolour(GameObject film)
        {
            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "body low" && r is SkinnedMeshRenderer sk && sk.sharedMesh != null && sk.sharedMesh.isReadable)
                {
                    var tex = Sc22SuitTexture(sk);
                    r.sharedMaterial = Mat("Sc22SupBody", Color.white, 0.22f, 0f, tex);
                }
            }
        }

        private static void Sc22TextureSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc22Dir + "/Tex" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null)
                    continue;
                var isLabel = p.Contains("Sc22_Label");
                if (imp.mipmapEnabled && imp.anisoLevel == 8 && imp.filterMode == FilterMode.Trilinear && imp.alphaIsTransparency == isLabel)
                    continue;
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.maxTextureSize = 2048;
                imp.alphaIsTransparency = isLabel || p.Contains("YellowLine");
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }

            // the sliced frames of the caution popup (border in source pixels)
            foreach (var fr in new[] { new KeyValuePair<string, int>("Assets/Resources/Sc22/Sc22_PopupFrame.png", 56), new KeyValuePair<string, int>("Assets/Resources/Sc22/Sc22_PopupTile.png", 40) })
            {
                var fi = AssetImporter.GetAtPath(fr.Key) as TextureImporter;
                if (fi == null)
                    continue;
                var want = new Vector4(fr.Value, fr.Value, fr.Value, fr.Value);
                if (fi.textureType == TextureImporterType.Sprite && fi.spriteBorder == want && fi.alphaIsTransparency)
                    continue;
                fi.textureType = TextureImporterType.Sprite;
                fi.spriteImportMode = SpriteImportMode.Single;
                fi.spriteBorder = want;
                fi.alphaIsTransparency = true;
                fi.mipmapEnabled = false;
                fi.filterMode = FilterMode.Bilinear;
                fi.textureCompression = TextureImporterCompression.Uncompressed;
                fi.SaveAndReimport();
            }

            foreach (var icon in new[] { "Assets/Resources/Sc22/Sc22_IconNoBottle.png", "Assets/Resources/Sc22/Sc22_IconDrink.png", "Assets/Resources/Sc22/Sc22_IconWarn.png", "Assets/Resources/Sc22/Sc22_CloseIcon.png" })
            {
                var ii = AssetImporter.GetAtPath(icon) as TextureImporter;
                if (ii == null || (ii.textureType == TextureImporterType.Sprite && ii.mipmapEnabled && ii.maxTextureSize == 1024))
                    continue;
                ii.textureType = TextureImporterType.Sprite;
                ii.spriteImportMode = SpriteImportMode.Single;
                ii.alphaIsTransparency = true;
                ii.mipmapEnabled = true;
                ii.filterMode = FilterMode.Trilinear;
                ii.maxTextureSize = 1024;
                ii.textureCompression = TextureImporterCompression.CompressedHQ;
                ii.SaveAndReimport();
            }

            var sds = AssetImporter.GetAtPath("Assets/Resources/Sc22/Sc22_Sds.png") as TextureImporter;
            if (sds != null && (sds.textureType != TextureImporterType.Sprite || !sds.mipmapEnabled || sds.anisoLevel != 8 || sds.maxTextureSize != 4096))
            {
                sds.textureType = TextureImporterType.Sprite;
                sds.spriteImportMode = SpriteImportMode.Single;
                sds.mipmapEnabled = true;
                sds.filterMode = FilterMode.Trilinear;
                sds.anisoLevel = 8;
                sds.maxTextureSize = 4096;
                sds.textureCompression = TextureImporterCompression.CompressedHQ;
                sds.SaveAndReimport();
            }
        }
        private static Texture2D Sc22SuitTexture(SkinnedMeshRenderer sk)
        {
            const int N = 1024;
            var path = Sc22Dir + "/Tex/Sc22_SupervisorBody.png";
            var mesh = sk.sharedMesh;
            var uv = mesh.uv;
            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var bw = mesh.boneWeights;
            var bones = sk.bones;
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var v in verts)
            {
                y0 = Mathf.Min(y0, v.y);
                y1 = Mathf.Max(y1, v.y);
            }

            var skin = new Color(0.60f, 0.40f, 0.31f);
            var orange = new Color(0.030f, 0.055f, 0.170f);   // boiler suit NAVY blue (top; client 2026-11-17)
            var navy = new Color(0.025f, 0.045f, 0.140f);     // boiler suit NAVY blue (legs, forearms)
            var glove = new Color(0.07f, 0.46f, 0.38f);    // green chemical gloves
            var boot = new Color(0.05f, 0.05f, 0.05f);
            var teal = new Color(0.035f, 0.06f, 0.18f);
            var hardHat = new Color(1.0f, 0.80f, 0.03f);
            // 1 = colour fixed by the bone, 0 = by height (torso, legs)
            int KindOf(int i, out Color fixedCol)
            {
                var n = bones[bw[i].boneIndex0].name;
                fixedCol = orange;
                if (n.StartsWith("head"))
                {
                    fixedCol = hardHat;   // the cap of this mesh follows the head bone: yellow (client)
                    return 1;
                }

                if (n.StartsWith("neck"))
                {
                    fixedCol = skin;
                    return 1;
                }

                if (n.StartsWith("hand") || n.StartsWith("c_index") || n.StartsWith("c_middle") || n.StartsWith("c_ring") || n.StartsWith("c_pinky") || n.StartsWith("c_thumb")
                    || n.StartsWith("index") || n.StartsWith("middle") || n.StartsWith("ring") || n.StartsWith("pinky") || n.StartsWith("thumb"))
                {
                    fixedCol = glove;
                    return 1;
                }

                if (n.StartsWith("forearm"))
                {
                    fixedCol = navy;
                    return 1;
                }

                if (n.StartsWith("c_arm"))
                {
                    fixedCol = orange;   // upper sleeve
                    return 1;
                }

                if (n.StartsWith("arm_stretch"))
                {
                    fixedCol = orange;
                    return 1;
                }

                return 0;
            }

            Color ByHeight(float h)
            {
                if (h < 0.075f)
                    return boot;
                if (h < 0.535f)
                    return navy;
                if (h < 0.565f)
                    return teal;
                if (h < 0.585f)
                    return navy;
                if (h < 0.800f)
                    return orange;
                return orange;        // collar and yoke (one piece)
            }

            var cols = new Color[N * N];
            var set = new bool[N * N];
            for (var t = 0; t + 2 < tris.Length; t += 3)
            {
                int a2 = tris[t], b2 = tris[t + 1], c2 = tris[t + 2];
                var ka = KindOf(a2, out var fa);
                var kb = KindOf(b2, out var fb);
                var kc = KindOf(c2, out var fc);
                var useFixed = ka + kb + kc >= 2;      // a triangle is bone-coloured when two of its vertices are
                var fixedCol = ka == 1 ? fa : kb == 1 ? fb : fc;
                float ha = (verts[a2].y - y0) / (y1 - y0), hb = (verts[b2].y - y0) / (y1 - y0), hc = (verts[c2].y - y0) / (y1 - y0);
                Vector2 pa = uv[a2] * N, pb = uv[b2] * N, pc = uv[c2] * N;
                var minx = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
                var maxx = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
                var miny = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
                var maxy = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
                var den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (Mathf.Abs(den) < 1e-6f)
                    continue;
                for (var y = miny; y <= maxy; y++)
                    for (var x = minx; x <= maxx; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        var l1 = ((pb.y - pc.y) * (px - pc.x) + (pc.x - pb.x) * (py - pc.y)) / den;
                        var l2 = ((pc.y - pa.y) * (px - pc.x) + (pa.x - pc.x) * (py - pc.y)) / den;
                        var l3 = 1f - l1 - l2;
                        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f)
                            continue;
                        cols[y * N + x] = useFixed ? fixedCol : ByHeight(l1 * ha + l2 * hb + l3 * hc);
                        set[y * N + x] = true;
                    }
            }

            // grow the painted islands a few pixels (no seams at the island borders), then a little fabric grain
            for (var pass = 0; pass < 6; pass++)
            {
                var nc = (Color[])cols.Clone();
                var ns = (bool[])set.Clone();
                for (var y = 1; y < N - 1; y++)
                    for (var x = 1; x < N - 1; x++)
                    {
                        var i = y * N + x;
                        if (set[i])
                            continue;
                        foreach (var d in new[] { i - 1, i + 1, i - N, i + N })
                            if (set[d])
                            {
                                nc[i] = cols[d];
                                ns[i] = true;
                                break;
                            }
                    }

                cols = nc;
                set = ns;
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px2 = new Color[N * N];
            for (var i = 0; i < px2.Length; i++)
            {
                var g = 0.93f + 0.14f * Mathf.PerlinNoise((i % N) * 0.35f, (i / N) * 0.35f);
                var c = set[i] ? cols[i] : orange;
                px2[i] = new Color(c.r * g, c.g * g, c.b * g, 1f);
            }

            tex.SetPixels(px2);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif

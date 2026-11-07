#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    // Scenario 1 workshop hall rebuilt to the client reference render: dark steel gable roof with corrugated sheets and
    // two skylights, rough concrete walls, a waist-high concrete wall behind the drums, shelving with goods, a lit
    // workshop through a doorway, SAFETY FIRST board, dirty stained floor. Switch: Sc01UseReferenceHall (false = old hall).
    public static partial class PresentableSceneBuilder
    {
        // ONE switch for the SC01 look. false builds the previous warehouse (code kept in BuildShell & co).
        private const bool Sc01UseReferenceHall = true;

        private const string Sc01TexDir = ArtRoot + "/2D/Sc01Tex";
        // the four hero drums (and their pallets) sit this far to the right of the hall axis, like the reference
        private const float Sc01HeroShift = 0.35f;

        // hall box: x -3.9..3.9 (one continuous wall on each side, no partitions), z -9..16; concrete to 3.6, corrugated cladding to the eave 4.7, ridge 7.5
        private const float HX0 = -3.9f, HX1 = 3.9f, HZ0 = -9f, HZ1 = 16f;
        private const float HWall = 3.6f, HEave = 4.7f, HRidge = 7.5f;

        private static Texture2D Sc01Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Sc01TexDir + "/" + name + ".png");

        private static void ConfigureSc01Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc01TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var normal = file.EndsWith("_Normal");
                var linear = file.EndsWith("_MS");
                var decal = file.StartsWith("Sign_") || file.StartsWith("Label_");
                var changed = false;
                var wanted = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != wanted)
                {
                    ti.textureType = wanted;
                    changed = true;
                }

                if (!normal && ti.sRGBTexture == linear)
                {
                    ti.sRGBTexture = !linear;
                    changed = true;
                }

                var wrap = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                var bias = decal ? 0f : 0.4f;
                if (!Mathf.Approximately(ti.mipMapBias, bias))
                {
                    ti.mipMapBias = bias;
                    changed = true;
                }

                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 ||
                    ti.alphaIsTransparency != decal || (!decal && ti.anisoLevel < 4))
                {
                    ti.wrapMode = wrap;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = decal;
                    ti.anisoLevel = decal ? 1 : 4;
                    changed = true;
                }

                if (changed)
                    ti.SaveAndReimport();
            }
        }

        private static Material Sc01Pbr(string name, string set, Color tint, float smooth, float bump = 1f,
            bool metal = false)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;
            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
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

            m.SetTexture("_BaseMap", Sc01Tex(set + "_Albedo"));
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", Sc01Tex(set + "_Normal"));
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", Sc01Tex(set + "_MS"));
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", smooth);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (!metal)
                m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // ---------------------------------------------------------------- metric-UV mesh helpers

        private sealed class MeshBuf
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
        }

        // Quad o, o+u, o+u+v, o+v facing `want`; UV in metres / tile.
        private static void AddQuad(MeshBuf b, Vector3 o, Vector3 u, Vector3 v, Vector3 want, float tileU, float tileV)
        {
            var n = Vector3.Cross(v, u).normalized;
            var flip = Vector3.Dot(n, want) < 0f;
            var f = flip ? -n : n;
            var i0 = b.V.Count;
            var us = u.magnitude / tileU;
            var vs = v.magnitude / tileV;
            b.V.Add(o);
            b.V.Add(o + v);
            b.V.Add(o + u + v);
            b.V.Add(o + u);
            b.UV.Add(new Vector2(0f, 0f));
            b.UV.Add(new Vector2(0f, vs));
            b.UV.Add(new Vector2(us, vs));
            b.UV.Add(new Vector2(us, 0f));
            for (var k = 0; k < 4; k++)
                b.N.Add(f);
            if (!flip)
                b.T.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            else
                b.T.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }

        // Triangle on a vertical plane facing `want`, UV projected on the horizontal axis of that plane.
        private static void AddTri(MeshBuf b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 want, float tile)
        {
            var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            var flip = Vector3.Dot(n, want) < 0f;
            var f = flip ? -n : n;
            var ua = Vector3.Cross(Vector3.up, want).normalized;
            var i0 = b.V.Count;
            foreach (var p in new[] { p0, p1, p2 })
            {
                b.V.Add(p);
                b.N.Add(f);
                b.UV.Add(new Vector2(Vector3.Dot(p, ua) / tile, p.y / tile));
            }

            if (!flip)
                b.T.AddRange(new[] { i0, i0 + 1, i0 + 2 });
            else
                b.T.AddRange(new[] { i0, i0 + 2, i0 + 1 });
        }

        private static GameObject MeshGo(string name, Transform parent, MeshBuf b, Material mat)
        {
            var mesh = new Mesh { name = name };
            if (b.V.Count > 60000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.V);
            mesh.SetNormals(b.N);
            mesh.SetUVs(0, b.UV);
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static GameObject Sheet(string name, Transform parent, Vector3 o, Vector3 u, Vector3 v, Vector3 want,
            Material mat, float tileU, float tileV)
        {
            var b = new MeshBuf();
            AddQuad(b, o, u, v, want, tileU, tileV);
            return MeshGo(name, parent, b, mat);
        }

        // Axis-aligned-or-yawed box with metric UVs on the 5 visible faces (no bottom), optional invisible collider.
        private static GameObject TexBox(string name, Transform parent, Vector3 centre, Vector3 size, float yaw,
            Material mat, float tile, bool solid)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var ex = rot * Vector3.right * size.x;
            var ey = Vector3.up * size.y;
            var ez = rot * Vector3.forward * size.z;
            var c0 = centre - ex * 0.5f - ez * 0.5f - ey * 0.5f; // bottom corner (-,-,-)
            var b = new MeshBuf();
            AddQuad(b, c0 + ex, ez, ey, rot * Vector3.right, tile, tile);                 // +x
            AddQuad(b, c0, ez, ey, -(rot * Vector3.right), tile, tile);                   // -x
            AddQuad(b, c0 + ez, ex, ey, rot * Vector3.forward, tile, tile);               // +z
            AddQuad(b, c0, ex, ey, -(rot * Vector3.forward), tile, tile);                 // -z
            AddQuad(b, c0 + ey, ex, ez, Vector3.up, tile, tile);                          // top
            var go = MeshGo(name, parent, b, mat);
            if (solid)
                AddSolidBox(parent, name + "Solid", centre, new Vector3(size.x, size.y, size.z)); // yaw 0 / 90 only
            return go;
        }

        private static GameObject Beam(string name, Transform parent, Vector3 a, Vector3 b2, float w, float h, Material mat)
        {
            var mid = (a + b2) * 0.5f;
            var d = b2 - a;
            var go = Prim(name, PrimitiveType.Cube, parent, mid, new Vector3(w, h, d.magnitude), mat);
            go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            return go;
        }

        // ---------------------------------------------------------------- the hall

        private static void BuildSc01Hall(Transform env, PresentableArt art)
        {
            var hall = new GameObject("Hall").transform;
            hall.SetParent(env, false);
            var len = HZ1 - HZ0;
            var cz = (HZ0 + HZ1) * 0.5f;

            var wallMat = Sc01Pbr("Sc01Wall", "Wall", new Color(1f, 0.99f, 0.96f), 0.3f, 0.8f);
            var floorMat = Sc01Pbr("Sc01Floor", "Floor", new Color(0.8f, 0.79f, 0.78f), 0.4f, 0.7f);
            var roofMat = Sc01Pbr("Sc01Roof", "Roof", new Color(0.46f, 0.47f, 0.5f), 0.5f, 0.55f, true);
            var cladMat = Sc01Pbr("Sc01Clad", "Roof", new Color(0.4f, 0.41f, 0.43f), 0.45f, 0.55f, true);
            var steel = Mat("Sc01Steel", new Color(0.14f, 0.15f, 0.17f), 0.35f, 0.3f);

            // collision floor (teleport + standing) is no longer drawn; the textured quad is
            var floorBox = Box("Floor", env, new Vector3(0f, -0.05f, cz), new Vector3(HX1 - HX0 + 0.2f, 0.1f, len + 0.2f),
                floorMat);
            floorBox.AddComponent<TeleportationArea>();
            floorBox.GetComponent<MeshRenderer>().enabled = false;
            Sheet("Floor_Hall", hall, new Vector3(HX0, 0.004f, HZ0), new Vector3(0f, 0f, len),
                new Vector3(HX1 - HX0, 0f, 0f), Vector3.up, floorMat, 6f, 6f);

            // concrete lower walls, corrugated upper cladding
            const float doorA = 7.0f, doorB = 9.0f, doorH = 2.5f, doorT = 0.4f;
            Sheet("Wall_Right", hall, new Vector3(HX1, 0f, HZ0), new Vector3(0f, 0f, len), new Vector3(0f, HWall, 0f),
                Vector3.left, wallMat, 3f, 3f);
            Sheet("Wall_Left_A", hall, new Vector3(HX0, 0f, HZ0), new Vector3(0f, 0f, doorA - HZ0),
                new Vector3(0f, HWall, 0f), Vector3.right, wallMat, 3f, 3f);
            Sheet("Wall_Left_B", hall, new Vector3(HX0, 0f, doorB), new Vector3(0f, 0f, HZ1 - doorB),
                new Vector3(0f, HWall, 0f), Vector3.right, wallMat, 3f, 3f);
            Sheet("Wall_Left_Lintel", hall, new Vector3(HX0, doorH, doorA), new Vector3(0f, 0f, doorB - doorA),
                new Vector3(0f, HWall - doorH, 0f), Vector3.right, wallMat, 3f, 3f);
            // the back of the hall is dark corrugated sheeting from floor to roof (reference), with a concrete block in it
            Sheet("Wall_Back", hall, new Vector3(HX0, 0f, HZ1), new Vector3(HX1 - HX0, 0f, 0f),
                new Vector3(0f, HWall, 0f), Vector3.back, cladMat, 1.3f, 1.3f);
            Sheet("Wall_Front", hall, new Vector3(HX0, 0f, HZ0), new Vector3(HX1 - HX0, 0f, 0f),
                new Vector3(0f, HWall, 0f), Vector3.forward, wallMat, 3f, 3f);
            Sheet("Clad_Right", hall, new Vector3(HX1, HWall, HZ0), new Vector3(0f, 0f, len),
                new Vector3(0f, HEave - HWall, 0f), Vector3.left, cladMat, 1.3f, 1.3f);
            Sheet("Clad_Left", hall, new Vector3(HX0, HWall, HZ0), new Vector3(0f, 0f, len),
                new Vector3(0f, HEave - HWall, 0f), Vector3.right, cladMat, 1.3f, 1.3f);
            foreach (var endZ in new[] { HZ0, HZ1 })
            {
                var face = endZ > 0f ? Vector3.back : Vector3.forward;
                Sheet("Clad_End", hall, new Vector3(HX0, HWall, endZ), new Vector3(HX1 - HX0, 0f, 0f),
                    new Vector3(0f, HEave - HWall, 0f), face, cladMat, 1.3f, 1.3f);
                var gb = new MeshBuf();
                AddTri(gb, new Vector3(HX0, HEave, endZ), new Vector3(HX1, HEave, endZ), new Vector3(0f, HRidge, endZ),
                    face, 1.3f);
                MeshGo("Gable_End", hall, gb, cladMat);
            }

            // gable roof (inner faces), two slopes
            var rise = HRidge - HEave;
            var half = HX1;
            Sheet("Roof_Left", hall, new Vector3(HX0, HEave, HZ0), new Vector3(0f, 0f, len), new Vector3(half, rise, 0f),
                new Vector3(rise, -half, 0f).normalized, roofMat, 1.3f, 2.2f);
            Sheet("Roof_Right", hall, new Vector3(HX1, HEave, HZ0), new Vector3(0f, 0f, len), new Vector3(-half, rise, 0f),
                new Vector3(-rise, -half, 0f).normalized, roofMat, 1.3f, 2.2f);

            // walls block hands / body
            AddSolidBox(hall, "Solid_Right", new Vector3(HX1 + 0.1f, 2.5f, cz), new Vector3(0.2f, 5f, len));
            AddSolidBox(hall, "Solid_Back", new Vector3(0f, 2.5f, HZ1 + 0.1f), new Vector3(HX1 - HX0, 5f, 0.2f));
            AddSolidBox(hall, "Solid_Front", new Vector3(0f, 2.5f, HZ0 - 0.1f), new Vector3(HX1 - HX0, 5f, 0.2f));
            AddSolidBox(hall, "Solid_LeftA", new Vector3(HX0 - 0.1f, 2.5f, (HZ0 + doorA) * 0.5f),
                new Vector3(0.2f, 5f, doorA - HZ0));
            AddSolidBox(hall, "Solid_LeftB", new Vector3(HX0 - 0.1f, 2.5f, (doorB + HZ1) * 0.5f),
                new Vector3(0.2f, 5f, HZ1 - doorB));

            BuildSc01Trusses(hall, steel);
            BuildSc01Skylights(hall);
            BuildSc01Doorway(hall, wallMat, doorA, doorB, doorH, doorT);
            BuildSc01Concrete(hall, wallMat);
            BuildSc01Racks(hall);
            BuildSc01Props(hall, art);
            BuildSc01Lights(env);
            // the rotating ceiling fans (as before), now in the gable hall: two rows offset to either side of the ridge, between the
            // trusses, low enough to be seen from the drums (rotor at 5.0 m, rod hung from the rafters)
            var fanSpots = new List<Vector3>();
            var fi = 0;
            foreach (var fz in new[] { 2f, 6f, 10f }) // three fans (client), left / right / left, over the drum bay
                fanSpots.Add(new Vector3((fi++ % 2 == 0 ? -1f : 1f) * 1.0f, 6.13f, fz));
            // 4.25 m: the rotors hang BELOW the black tie beams (4.7 m) of the trusses (client), 0.85 keeps the blades clear of the walls
            BuildCeilingFans(env, fanSpots.ToArray(), 6.13f, 4.25f, 0.85f);

            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.36f, 0.38f, 0.42f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.27f, 0.28f, 0.3f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.12f, 0.12f, 0.13f);
            mso.FindProperty("fogColor").colorValue = new Color(0.17f, 0.18f, 0.2f);
            mso.FindProperty("fogStart").floatValue = 11f;
            mso.FindProperty("fogEnd").floatValue = 42f;
            mso.FindProperty("sunIntensity").floatValue = 0.28f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.7f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            MarkStatic(hall);
        }

        private static Vector3 RoofPoint(float side, float t) =>
            new Vector3(side * HX1 * (1f - t), HEave + (HRidge - HEave) * t, 0f);

        private static void BuildSc01Trusses(Transform hall, Material steel)
        {
            var trusses = new GameObject("Trusses").transform;
            trusses.SetParent(hall, false);
            var rise = HRidge - HEave;
            foreach (var z in new[] { -8f, -4f, 0f, 4f, 8f, 12f, 15.85f })
            {
                var last = z > 15f;
                // rafters under the roof, tie beam at the eave
                foreach (var s in new[] { -1f, 1f })
                {
                    Beam("Rafter", trusses, new Vector3(s * (HX1 - 0.05f), HEave - 0.1f, z),
                        new Vector3(0f, HRidge - 0.12f, z), 0.1f, 0.2f, steel);
                    Beam("Strut", trusses, new Vector3(0f, HEave + 0.05f, z),
                        new Vector3(s * 3.1f, HEave + rise * (1f - 3.1f / HX1) - 0.12f, z), 0.07f, 0.12f, steel);
                }

                Beam("Tie", trusses, new Vector3(HX0, HEave, z), new Vector3(HX1, HEave, z), 0.1f, 0.2f, steel);
                if (!last)
                    Beam("KingPost", trusses, new Vector3(0f, HEave, z), new Vector3(0f, HRidge - 0.1f, z), 0.1f, 0.1f, steel);
            }

            // purlins along the hall on both slopes + ridge
            foreach (var s in new[] { -1f, 1f })
            {
                foreach (var t in new[] { 0.28f, 0.58f, 0.86f })
                {
                    var p = RoofPoint(s, t);
                    Beam("Purlin", trusses, new Vector3(p.x, p.y - 0.14f, HZ0), new Vector3(p.x, p.y - 0.14f, HZ1), 0.08f, 0.1f, steel);
                }
            }

            Beam("Ridge", trusses, new Vector3(0f, HRidge - 0.1f, HZ0), new Vector3(0f, HRidge - 0.1f, HZ1), 0.12f, 0.12f, steel);
        }

        // Bright rooflight panels set in the slopes + a soft shaft of daylight under the two near the drums.
        private static void BuildSc01Skylights(Transform hall)
        {
            var glow = Mat("Sc01Skylight", new Color(0.86f, 0.93f, 1f), 0f, 0f, null, null, true);
            var rim = Mat("Sc01SkyRim", new Color(0.1f, 0.11f, 0.12f), 0.3f, 0.5f);
            var shaftMat = MatAlpha("Sc01Shaft", EnvTex("FX_Shaft"), new Color(1f, 0.97f, 0.88f, 0.11f), true);
            var rise = HRidge - HEave;
            var defs = new[] { (-1f, 3f, 0.34f, 0.72f), (1f, 8f, 0.34f, 0.72f), (-1f, 12.5f, 0.34f, 0.72f), (1f, -2f, 0.34f, 0.72f) };
            var idx = 0;
            foreach (var (side, z, t0, t1) in defs)
            {
                var p0 = RoofPoint(side, t0) + new Vector3(0f, 0f, z);
                var p1 = RoofPoint(side, t1) + new Vector3(0f, 0f, z);
                var along = p1 - p0;
                var want = new Vector3(-side * rise, -HX1, 0f).normalized; // into the hall, down
                Sheet("SkyRim", hall, p0 - along.normalized * 0.1f - new Vector3(0f, 0f, 0.14f) + want * 0.015f,
                    new Vector3(0f, 0f, 2.28f), along + along.normalized * 0.2f, want, rim, 1f, 1f);
                Sheet("Skylight", hall, p0 + want * 0.035f, new Vector3(0f, 0f, 2f), along, want, glow, 1f, 1f);
                idx++;
            }
        }

        // Open doorway in the left wall onto a bright workshop.
        private static void BuildSc01Doorway(Transform hall, Material wallMat, float zA, float zB, float h, float t)
        {
            var x0 = HX0 - t;
            var inner = Sc01Pbr("Sc01WallLit", "Wall", new Color(1f, 1f, 0.97f), 0.3f, 0.8f);
            Sheet("DoorReveal_A", hall, new Vector3(x0, 0f, zA), new Vector3(t, 0f, 0f), new Vector3(0f, h, 0f),
                Vector3.forward, wallMat, 3f, 3f);
            Sheet("DoorReveal_B", hall, new Vector3(x0, 0f, zB), new Vector3(t, 0f, 0f), new Vector3(0f, h, 0f),
                Vector3.back, wallMat, 3f, 3f);
            Sheet("DoorReveal_Top", hall, new Vector3(x0, h, zA), new Vector3(0f, 0f, zB - zA), new Vector3(t, 0f, 0f),
                Vector3.down, wallMat, 3f, 3f);

            // workshop room behind the door: floor, ceiling, back + side walls, tube lights, bench and machines
            const float wx0 = -8.2f, wz0 = 5.9f, wz1 = 10.1f, wh = 3.1f;
            var floorMat = Sc01Pbr("Sc01Floor", "Floor", new Color(0.8f, 0.79f, 0.78f), 0.5f, 1f);
            var xr = x0 - wx0;
            Sheet("Shop_Floor", hall, new Vector3(wx0, 0.004f, wz0), new Vector3(0f, 0f, wz1 - wz0), new Vector3(xr, 0f, 0f),
                Vector3.up, floorMat, 3f, 3f);
            Sheet("Shop_Ceiling", hall, new Vector3(wx0, wh, wz0), new Vector3(0f, 0f, wz1 - wz0), new Vector3(xr, 0f, 0f),
                Vector3.down, inner, 3f, 3f);
            Sheet("Shop_Back", hall, new Vector3(wx0, 0f, wz0), new Vector3(0f, 0f, wz1 - wz0), new Vector3(0f, wh, 0f),
                Vector3.right, inner, 3f, 3f);
            Sheet("Shop_SideA", hall, new Vector3(wx0, 0f, wz0), new Vector3(xr, 0f, 0f), new Vector3(0f, wh, 0f),
                Vector3.forward, inner, 3f, 3f);
            Sheet("Shop_SideB", hall, new Vector3(wx0, 0f, wz1), new Vector3(xr, 0f, 0f), new Vector3(0f, wh, 0f),
                Vector3.back, inner, 3f, 3f);
            var tube = Mat("Sc01Tube", new Color(1f, 0.99f, 0.94f), 0f, 0f, null, null, true);
            foreach (var z in new[] { 7.0f, 9.0f })
                Prim("ShopTube", PrimitiveType.Cube, hall, new Vector3(-7.0f, wh - 0.05f, z), new Vector3(1.8f, 0.05f, 0.12f), tube);
            var dark = Mat("Sc01Machine", new Color(0.2f, 0.22f, 0.25f), 0.35f, 0.5f);
            var light = Mat("Sc01Bench", new Color(0.55f, 0.57f, 0.6f), 0.3f, 0.4f);
            Prim("Bench", PrimitiveType.Cube, hall, new Vector3(-7.6f, 0.45f, 8.1f), new Vector3(0.7f, 0.9f, 2.4f), light);
            Prim("Machine", PrimitiveType.Cube, hall, new Vector3(-7.7f, 0.9f, 6.4f), new Vector3(0.8f, 1.8f, 0.7f), dark);
            // what the learner sees through the door at an angle: the far side wall with a work bench, monitors, a dark inner door,
            // shelving with boxes and a pegboard of tools, all lit by the tubes (reference render)
            var benchTop = Mat("Sc01BenchTop", new Color(0.42f, 0.3f, 0.2f), 0.2f);
            var screen = Mat("Sc01Screen", new Color(0.55f, 0.85f, 1f), 0f, 0f, null, null, true);
            var innerDoor = Mat("Sc01InnerDoor", new Color(0.04f, 0.045f, 0.05f), 0.3f, 0.3f);
            var boxes = Mat("Sc01ShopBox", new Color(0.7f, 0.58f, 0.42f), 0.1f, 0f, Sc01Tex("Box_Albedo"));
            Prim("Bench2", PrimitiveType.Cube, hall, new Vector3(-7.1f, 0.45f, 9.7f), new Vector3(2.2f, 0.9f, 0.7f), light);
            Prim("BenchTop2", PrimitiveType.Cube, hall, new Vector3(-7.1f, 0.92f, 9.7f), new Vector3(2.3f, 0.05f, 0.78f), benchTop);
            Prim("Monitor1", PrimitiveType.Cube, hall, new Vector3(-7.5f, 1.25f, 9.9f), new Vector3(0.5f, 0.32f, 0.04f), screen);
            Prim("Monitor2", PrimitiveType.Cube, hall, new Vector3(-6.8f, 1.22f, 9.9f), new Vector3(0.42f, 0.28f, 0.04f), screen);
            Sheet("ShopWindow", hall, new Vector3(-7.9f, 1.25f, 10.075f), new Vector3(1.5f, 0f, 0f), new Vector3(0f, 0.95f, 0f),
                Vector3.back, Mat("Sc01ShopWindow", new Color(0.95f, 0.97f, 1f), 0f, 0f, null, null, true), 1f, 1f);
            Prim("InnerDoor", PrimitiveType.Cube, hall, new Vector3(-5.95f, 1.05f, 10.06f), new Vector3(0.8f, 2.1f, 0.05f), innerDoor);
            Prim("PegBoard", PrimitiveType.Cube, hall, new Vector3(-7.1f, 1.95f, 10.07f), new Vector3(2.0f, 0.7f, 0.03f), benchTop);
            for (var i = 0; i < 6; i++)
                Prim("Tool", PrimitiveType.Cube, hall, new Vector3(-7.9f + i * 0.35f, 1.95f, 10.04f), new Vector3(0.05f, 0.35f, 0.03f), dark);
            Prim("ShopShelf", PrimitiveType.Cube, hall, new Vector3(-8.0f, 1.2f, 7.4f), new Vector3(0.3f, 2.4f, 1.6f), light);
            for (var i = 0; i < 4; i++)
                Prim("ShopBox", PrimitiveType.Cube, hall, new Vector3(-7.85f, 0.4f + i * 0.55f, 7.4f + (i % 2) * 0.2f), new Vector3(0.28f, 0.34f, 0.5f), boxes);
            Prim("Cabinet", PrimitiveType.Cube, hall, new Vector3(-7.75f, 1.0f, 9.6f), new Vector3(0.5f, 2.0f, 0.7f),
                Mat("Sc01Cabinet", new Color(0.7f, 0.72f, 0.74f), 0.3f, 0.5f));
        }

        // Waist-high concrete wall behind the drums, a wall stub at the left, wooden door in the back wall.
        private static void BuildSc01Concrete(Transform hall, Material wallMat)
        {
            // reference: the half wall starts just left of the first drum and runs into a tall plastered wall on the right
            // (the cart stands in that corner); the SAFETY FIRST board hangs on the tall wall
            TexBox("HalfWall", hall, new Vector3(1.45f, 0.6f, 7.55f), new Vector3(4.9f, 1.2f, 0.4f), 0f, wallMat, 2.4f, true);
            // concrete block in the back wall with a wooden vent grille
            TexBox("BackBlock", hall, new Vector3(1.4f, 1.2f, HZ1 - 0.3f), new Vector3(3.6f, 2.4f, 0.6f), 0f, wallMat, 2.4f, false);
            var grille = Mat("Sc01Grille", new Color(0.3f, 0.17f, 0.1f), 0.1f);
            Prim("VentGrille", PrimitiveType.Cube, hall, new Vector3(1.4f, 1.5f, HZ1 - 0.61f), new Vector3(1.0f, 0.65f, 0.04f), grille);
            var slat = Mat("Sc01GrilleSlat", new Color(0.16f, 0.09f, 0.05f), 0.1f);
            for (var i = 0; i < 6; i++)
                Prim("VentSlat", PrimitiveType.Cube, hall, new Vector3(1.4f, 1.24f + i * 0.1f, HZ1 - 0.64f), new Vector3(1.0f, 0.025f, 0.03f), slat);
        }

        // ---------------------------------------------------------------- shelving with goods

        private static void BuildSc01Racks(Transform hall)
        {
            var racks = new GameObject("Racks").transform;
            racks.SetParent(hall, false);
            // along the back wall, left and right of the door; along the left and right walls further in
            BuildRackRun(racks, new Vector3(-2.9f, 0f, HZ1 - 0.5f), 0f, 2, 11);
            BuildRackRun(racks, new Vector3(HX1 - 0.5f, 0f, 10.8f), -90f, 3, 13); // right wall, starts right behind the half wall (reference)
            BuildRackRun(racks, new Vector3(HX0 + 0.5f, 0f, 12.2f), 90f, 2, 14);
            BuildRackRun(racks, new Vector3(-0.3f, 0f, 11.6f), 90f, 2, 15); // free-standing, seen end-on
        }

        // Local frame: bays along x, front facing +z. Dark blue uprights, orange beams, goods on every level.
        private static void BuildRackRun(Transform parent, Vector3 pos, float yaw, int bays, int seed)
        {
            var run = new GameObject("RackRun").transform;
            run.SetParent(parent, false);
            run.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var grunge = Sc01Tex("Grunge_Albedo");
            var upright = Mat("Sc01RackBlue", new Color(0.1f, 0.12f, 0.2f), 0.3f, 0.25f, grunge);
            var beam = Mat("Sc01RackOrange", new Color(0.62f, 0.27f, 0.09f), 0.3f, 0.2f, grunge);
            var deck = Mat("Sc01RackDeck", new Color(0.4f, 0.32f, 0.22f), 0.1f, 0f, grunge);
            var cardboard = Mat("Sc01Cardboard", new Color(0.6f, 0.55f, 0.48f), 0.1f, 0f, Sc01Tex("Box_Albedo"));
            var white = Mat("Sc01Plastic", new Color(0.78f, 0.77f, 0.73f), 0.3f, 0f, grunge);
            var blueDrum = Mat("Sc01SmallBlue", new Color(0.1f, 0.2f, 0.42f), 0.35f, 0.2f, grunge);
            var sack = Mat("Sc01Sack", new Color(0.7f, 0.67f, 0.6f), 0.08f, 0f, grunge);
            var rnd = new System.Random(seed);
            const float bayW = 1.8f, depth = 0.75f, height = 3.3f;
            var levels = new[] { 0.28f, 1.38f, 2.48f };
            for (var k = 0; k <= bays; k++)
            {
                var x = (k - bays * 0.5f) * bayW;
                foreach (var dz in new[] { -depth * 0.5f, depth * 0.5f })
                    Prim("Upright", PrimitiveType.Cube, run, new Vector3(x, height * 0.5f, dz), new Vector3(0.08f, height, 0.08f), upright);
            }

            for (var b = 0; b < bays; b++)
            {
                var cx = (b - (bays - 1) * 0.5f) * bayW;
                foreach (var y in levels)
                {
                    foreach (var dz in new[] { -depth * 0.5f, depth * 0.5f })
                        Prim("Beam", PrimitiveType.Cube, run, new Vector3(cx, y, dz), new Vector3(bayW, 0.11f, 0.06f), beam);
                    Prim("Deck", PrimitiveType.Cube, run, new Vector3(cx, y + 0.06f, 0f), new Vector3(bayW - 0.04f, 0.03f, depth), deck);
                    var jerry = Mat("Sc01Jerrycan", new Color(0.66f, 0.52f, 0.12f), 0.3f, 0f, grunge);
                    var boxLabel = Mat("Sc01BoxLabel", Color.white, 0.1f, 0f, Sc01Tex("Label_Box"), null, false, true);
                    var lids = new[]
                    {
                        Mat("Sc01LidBlue", new Color(0.12f, 0.24f, 0.5f), 0.3f, 0f, grunge),
                        Mat("Sc01LidRed", new Color(0.55f, 0.12f, 0.1f), 0.3f, 0f, grunge),
                        Mat("Sc01LidGrey", new Color(0.5f, 0.5f, 0.5f), 0.3f, 0f, grunge)
                    };
                    var tinSilver = Mat("Sc01Tin", new Color(0.66f, 0.66f, 0.68f), 0.5f, 0.7f, grunge);
                    var bands = new[]
                    {
                        Mat("Sc01TinBandRed", new Color(0.62f, 0.12f, 0.1f), 0.3f),
                        Mat("Sc01TinBandYellow", new Color(0.78f, 0.62f, 0.1f), 0.3f),
                        Mat("Sc01TinBandBlue", new Color(0.1f, 0.25f, 0.55f), 0.3f)
                    };
                    foreach (var rowZ in new[] { 0.18f, -0.18f }) // two rows of goods deep: the shelves read as full
                    {
                        var x = cx - bayW * 0.5f + 0.14f + (float)rnd.NextDouble() * 0.1f;
                        while (x < cx + bayW * 0.5f - 0.2f)
                        {
                            var r = rnd.NextDouble();
                            float w;
                            if (rnd.NextDouble() < 0.04)
                            {
                                x += 0.3f;
                                continue;
                            }

                            var zj = rowZ + (float)(rnd.NextDouble() - 0.5) * 0.05f;
                            if (r < 0.38)
                            {
                                w = 0.32f + (float)rnd.NextDouble() * 0.18f;
                                var h = 0.24f + (float)rnd.NextDouble() * 0.24f;
                                var stack = rnd.NextDouble() < 0.5 ? 2 : 1;
                                for (var s = 0; s < stack; s++)
                                {
                                    var bx = Prim("Box", PrimitiveType.Cube, run, new Vector3(x + w * 0.5f, y + 0.075f + h * 0.5f + s * h, zj),
                                        new Vector3(w, h, 0.32f), cardboard);
                                    bx.transform.localRotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() - 0.5) * 10f, 0f);
                                    if (rowZ > 0f && rnd.NextDouble() < 0.7) // printed shipping label on the front face
                                        QuadObj("BoxLabel", run, new Vector3(x + w * 0.5f, y + 0.075f + h * 0.5f + s * h, zj + 0.162f),
                                            new Vector3(0f, 180f, 0f), new Vector2(w * 0.55f, h * 0.4f), boxLabel);
                                }
                            }
                            else if (r < 0.6)
                            {
                                w = 0.3f;
                                var h = 0.28f + (float)rnd.NextDouble() * 0.12f;
                                var lid = lids[rnd.Next(lids.Length)];
                                var stackB = rnd.NextDouble() < 0.5 ? 2 : 1;
                                for (var s = 0; s < stackB; s++)
                                {
                                    var by = y + 0.075f + h * (0.5f + s);
                                    Prim("Bucket", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, by, zj), new Vector3(w, h * 0.5f, w), white);
                                    Prim("BucketLid", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, by + h * 0.5f, zj), new Vector3(w * 1.05f, 0.012f, w * 1.05f), lid);
                                    Prim("BucketRim", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, by - h * 0.45f, zj), new Vector3(w * 1.03f, 0.01f, w * 1.03f), lid);
                                }
                            }
                            else if (r < 0.52)
                            {
                                // paint tins: silver body, coloured label band, lid
                                w = 0.2f;
                                var nTin = rnd.Next(1, 3);
                                for (var s = 0; s < nTin; s++)
                                {
                                    var ty = y + 0.075f + 0.1f + s * 0.2f;
                                    Prim("Tin", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, ty, zj), new Vector3(w, 0.1f, w), tinSilver);
                                    Prim("TinBand", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, ty, zj), new Vector3(w * 1.01f, 0.05f, w * 1.01f), bands[rnd.Next(bands.Length)]);
                                    Prim("TinLid", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, ty + 0.1f, zj), new Vector3(w * 1.03f, 0.006f, w * 1.03f), tinSilver);
                                }
                            }
                            else if (r < 0.7)
                            {
                                w = 0.36f;
                                Prim("SmallDrum", PrimitiveType.Cylinder, run, new Vector3(x + w * 0.5f, y + 0.075f + 0.3f, zj), new Vector3(w, 0.3f, w), blueDrum);
                            }
                            else if (r < 0.84)
                            {
                                w = 0.24f;
                                Prim("Jerrycan", PrimitiveType.Cube, run, new Vector3(x + w * 0.5f, y + 0.075f + 0.2f, zj), new Vector3(w, 0.4f, 0.18f), jerry);
                            }
                            else
                            {
                                w = 0.5f;
                                var sk = Prim("Sack", PrimitiveType.Cube, run, new Vector3(x + w * 0.5f, y + 0.075f + 0.1f, zj), new Vector3(w, 0.2f, 0.32f), sack);
                                sk.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rnd.NextDouble() - 0.5) * 8f);
                            }

                            x += w + 0.03f + (float)rnd.NextDouble() * 0.05f;
                        }
                    }
                }

                // diagonal bracing across the back of the bay, like the reference racks
                for (var i = 0; i < levels.Length - 1; i++)
                {
                    var flip = (i + b) % 2 == 0 ? 1f : -1f;
                    Beam("BackBrace", run, new Vector3(cx - flip * bayW * 0.5f, levels[i], -depth * 0.5f - 0.03f),
                        new Vector3(cx + flip * bayW * 0.5f, levels[i + 1], -depth * 0.5f - 0.03f), 0.035f, 0.035f, upright);
                }
            }

            // cross bracing on the end frames
            var braceMat = upright;
            for (var k = 0; k <= bays; k += bays)
            {
                var x = (k - bays * 0.5f) * bayW;
                Beam("Brace", run, new Vector3(x, 0.2f, -depth * 0.5f), new Vector3(x, 1.4f, depth * 0.5f), 0.04f, 0.04f, braceMat);
                Beam("Brace", run, new Vector3(x, 1.4f, -depth * 0.5f), new Vector3(x, 2.5f, depth * 0.5f), 0.04f, 0.04f, braceMat);
            }

            AddSolidBox(run, "RackSolid", pos + new Vector3(0f, 1.6f, 0f), new Vector3(bays * bayW, 3.2f, depth)); // local axes: turns with the run
        }

        // ---------------------------------------------------------------- dressing: sign, cables, box, drums, extinguisher

        // The left wall's electrics like the reference: grey distribution board with door, handle and louvres, junction boxes, an
        // isolator switch, PVC conduit with clamps, a sagging bundle of black cables that splits into the boxes, a pinned notice.
        private static void BuildSc01ElectricalWall(Transform props)
        {
            var wall = new GameObject("ElectricalWall").transform;
            wall.SetParent(props, false);
            var grunge = Sc01Tex("Grunge_Albedo");
            var board = Mat("Sc01Board", new Color(0.46f, 0.5f, 0.48f), 0.4f, 0.55f, grunge);
            var boardDoor = Mat("Sc01BoardDoor", new Color(0.55f, 0.58f, 0.55f), 0.45f, 0.5f, grunge);
            var pvc = Mat("Sc01Pvc", new Color(0.66f, 0.66f, 0.62f), 0.3f, 0f, grunge);
            var cable = Mat("Sc01Cable", new Color(0.035f, 0.035f, 0.04f), 0.5f);
            var dark = Mat("Sc01ElecDark", new Color(0.05f, 0.05f, 0.055f), 0.3f);
            var yellow = Mat("Sc01ElecYellow", new Color(0.85f, 0.68f, 0.05f), 0.3f);
            var red = Mat("Sc01ElecRed", new Color(0.7f, 0.08f, 0.06f), 0.4f);
            var paper = Mat("Sc01Notice", new Color(0.82f, 0.8f, 0.72f), 0.05f, 0f, grunge);
            const float x0 = HX0; // wall face, items stand out towards +x
            Vector3 P(float off, float y, float z) => new Vector3(x0 + off, y, z);

            // conduit: two horizontal runs with clamps + verticals into the board
            foreach (var y in new[] { 2.95f, 2.78f })
            {
                var run = Prim("Conduit", PrimitiveType.Cylinder, wall, P(0.04f, y, 2.7f), new Vector3(0.045f, 4.0f, 0.045f), pvc);
                run.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (var z = -0.9f; z < 6.4f; z += 0.9f)
                    Prim("Clamp", PrimitiveType.Cube, wall, P(0.035f, y, z), new Vector3(0.07f, 0.075f, 0.04f), dark);
            }

            foreach (var z in new[] { 4.82f, 5.18f })
            {
                Prim("ConduitDrop", PrimitiveType.Cylinder, wall, P(0.04f, 2.55f, z), new Vector3(0.04f, 0.4f, 0.04f), pvc);
                Prim("Clamp", PrimitiveType.Cube, wall, P(0.035f, 2.5f, z), new Vector3(0.07f, 0.04f, 0.075f), dark);
                Prim("ConduitUp", PrimitiveType.Cylinder, wall, P(0.04f, 2.87f, z), new Vector3(0.04f, 0.14f, 0.04f), pvc);
            }

            // distribution board
            const float bz = 5.0f, by = 1.7f;
            Prim("Board", PrimitiveType.Cube, wall, P(0.09f, by, bz), new Vector3(0.18f, 0.84f, 0.64f), board);
            Prim("BoardDoor", PrimitiveType.Cube, wall, P(0.19f, by, bz), new Vector3(0.025f, 0.78f, 0.58f), boardDoor);
            Prim("BoardHandle", PrimitiveType.Cube, wall, P(0.215f, by, bz + 0.2f), new Vector3(0.03f, 0.16f, 0.025f), dark);
            for (var i = 0; i < 5; i++)
                Prim("Louvre", PrimitiveType.Cube, wall, P(0.205f, by - 0.22f - i * 0.03f, bz - 0.05f), new Vector3(0.012f, 0.012f, 0.3f), dark);
            Prim("WarnSticker", PrimitiveType.Cube, wall, P(0.205f, by + 0.22f, bz - 0.12f), new Vector3(0.008f, 0.12f, 0.14f), yellow);
            Prim("WarnMark", PrimitiveType.Cube, wall, P(0.21f, by + 0.22f, bz - 0.12f), new Vector3(0.006f, 0.07f, 0.02f), dark);
            Prim("WarnDot", PrimitiveType.Cube, wall, P(0.21f, by + 0.17f, bz - 0.12f), new Vector3(0.006f, 0.02f, 0.02f), dark);

            // junction boxes + isolator switch
            Prim("JunctionBox", PrimitiveType.Cube, wall, P(0.06f, 2.2f, 3.6f), new Vector3(0.12f, 0.24f, 0.24f), pvc);
            Prim("JunctionLid", PrimitiveType.Cube, wall, P(0.13f, 2.2f, 3.6f), new Vector3(0.02f, 0.2f, 0.2f), boardDoor);
            Prim("JunctionBoxSmall", PrimitiveType.Cube, wall, P(0.05f, 1.35f, 4.1f), new Vector3(0.1f, 0.16f, 0.16f), pvc);
            Prim("Isolator", PrimitiveType.Cube, wall, P(0.07f, 1.5f, 5.85f), new Vector3(0.14f, 0.26f, 0.2f), pvc);
            var disc = Prim("IsolatorKnob", PrimitiveType.Cylinder, wall, P(0.17f, 1.5f, 5.85f), new Vector3(0.12f, 0.012f, 0.12f), yellow);
            disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim("IsolatorHandle", PrimitiveType.Cube, wall, P(0.19f, 1.5f, 5.85f), new Vector3(0.03f, 0.1f, 0.04f), red);
            Prim("ConduitIsolator", PrimitiveType.Cylinder, wall, P(0.04f, 0.78f, 5.85f), new Vector3(0.04f, 0.72f, 0.04f), pvc);

            // cables: a bundle of four along the wall that splits into the junction box and the conduit; the board feeds the isolator
            void Cable(Vector3 a, Vector3 b, float sag, int segs = 9, float w = 0.03f)
            {
                var prev = a;
                for (var i = 1; i <= segs; i++)
                {
                    var t = i / (float)segs;
                    var pt = Vector3.Lerp(a, b, t) + Vector3.down * (sag * Mathf.Sin(t * Mathf.PI));
                    Beam("Cable", wall, prev, pt, w, w, cable);
                    prev = pt;
                }
            }

            for (var i = 0; i < 4; i++)
            {
                var y = 2.58f + i * 0.045f;
                Cable(P(0.07f + i * 0.012f, y, -1.6f), P(0.07f + i * 0.012f, y, 1.4f), 0.07f);
                Cable(P(0.07f + i * 0.012f, y, 1.4f), P(0.07f + i * 0.012f, y + 0.02f, 3.2f), 0.06f);
            }

            Cable(P(0.07f, 2.6f, 3.2f), P(0.07f, 2.34f, 3.55f), 0.0f, 5);   // into the junction box
            Cable(P(0.09f, 2.64f, 3.2f), P(0.09f, 2.34f, 3.65f), 0.0f, 5);
            Cable(P(0.075f, 2.66f, 3.2f), P(0.075f, 2.95f, 3.7f), 0.03f, 6); // up onto the conduit run
            Cable(P(0.08f, 2.7f, 3.2f), P(0.08f, 2.78f, 3.9f), 0.04f, 6);
            Cable(P(0.07f, 2.1f, 3.6f), P(0.07f, 1.45f, 4.1f), 0.05f, 7);    // junction box -> small box
            Cable(P(0.2f, 1.62f, 5.32f), P(0.2f, 1.5f, 5.78f), 0.09f, 8);    // board -> isolator
            Cable(P(0.21f, 1.55f, 5.32f), P(0.21f, 1.45f, 5.78f), 0.12f, 8);

            // pinned notice
            Prim("Notice", PrimitiveType.Cube, wall, P(0.012f, 1.75f, 2.3f), new Vector3(0.012f, 0.3f, 0.22f), paper);
            foreach (var d in new[] { new Vector2(0.12f, 0.08f), new Vector2(-0.12f, 0.08f) })
                Prim("Pin", PrimitiveType.Cube, wall, P(0.022f, 1.75f + d.x, 2.3f + d.y), new Vector3(0.012f, 0.014f, 0.014f), red);
        }

        private static void BuildSc01Props(Transform hall, PresentableArt art)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(hall, false);

            var signMat = Mat("Sc01Sign", Color.white, 0.3f, 0f, Sc01Tex("Sign_SafetyFirst"), null, false, true);
            // on the tall wall at the right (partition face x = 3.75), a little off level like an old screwed-on board
            QuadObj("Sign_SafetyFirst", props, new Vector3(HX1 - 0.015f, 2.3f, 6.55f), new Vector3(0f, 90f, 1.2f), new Vector2(1.7f, 0.85f), signMat);

            BuildSc01ElectricalWall(props);

            // blue drums at the left: the same Drum.glb model as the hero drums (not the simple procedural drum)
            var dA = PlaceModel("3D/Drum", "LeftDrum1", props, new Vector3(-3.35f, 0f, 5.1f), 30f, out var dr, out var dh);
            var dB = PlaceModel("3D/Drum", "LeftDrum2", props, new Vector3(-3.25f, 0f, 5.75f), 200f);
            var dC = PlaceModel("3D/Drum", "MidDrum", props, new Vector3(-3.1f, 0f, 6.7f), 100f);
            foreach (var d in new[] { dA, dB, dC })
                AddSolidBox(props, d.name + "Solid", d.transform.position + Vector3.up * (dh * 0.5f), new Vector3(dr * 2f, dh, dr * 2f));
            var label = LabelMat("Sc01Left_Faded", "faded");
            var lq = QuadObj("Label", props, new Vector3(-3.35f + dr + 0.004f, dh * 0.5f, 5.1f), new Vector3(0f, 90f, 0f), new Vector2(0.34f, 0.37f), label);
            lq.name = "LeftDrumLabel";

            // extinguisher beside the doorway, red hose reel cable on the floor
            BuildExtinguisher(props, new Vector3(HX0, 0f, 6.5f), 90f);
            // stain decals: break the repeat of the floor texture and put dirt on the plaster like the reference
            var oilM = MatAlpha("Sc01StainOil", EnvTex("STAIN_Oil"), new Color(0.6f, 0.6f, 0.6f, 0.85f), false);
            var rustM = MatAlpha("Sc01StainRust", EnvTex("STAIN_Rust"), new Color(0.26f, 0.19f, 0.15f, 0.5f), false);
            var floorStains = new[]
            {
                (oilM, new Vector3(0.3f, 0.012f, 1.2f), 1.7f, 20f), (rustM, new Vector3(-1.4f, 0.012f, 4.3f), 1.4f, 80f),
                (oilM, new Vector3(1.9f, 0.012f, 3.4f), 1.2f, 140f), (oilM, new Vector3(-0.4f, 0.012f, 5.3f), 1.5f, 200f),
                (rustM, new Vector3(2.5f, 0.012f, 5.0f), 1.0f, 30f), (oilM, new Vector3(-2.7f, 0.012f, 1.6f), 1.6f, 260f),
                (oilM, new Vector3(1.2f, 0.012f, -1.3f), 1.8f, 100f), (rustM, new Vector3(-2.2f, 0.012f, 7.4f), 1.3f, 310f)
            };
            foreach (var (m, p, size, yaw) in floorStains)
                QuadObj("FloorStain", props, p, new Vector3(90f, yaw, 0f), new Vector2(size, size), m);
            QuadObj("WallStain", props, new Vector3(HX1 - 0.008f, 0.55f, 2.8f), new Vector3(0f, 90f, 0f), new Vector2(1.8f, 1.2f), oilM);
            QuadObj("WallStain", props, new Vector3(HX1 - 0.008f, 1.6f, 4.0f), new Vector3(0f, 90f, 0f), new Vector2(1.0f, 2.2f), rustM);
            QuadObj("WallStain", props, new Vector3(HX1 - 0.008f, 0.6f, 5.6f), new Vector3(0f, 90f, 0f), new Vector2(1.6f, 1.1f), oilM);
            QuadObj("WallStain", props, new Vector3(0.2f, 0.55f, 7.338f), Vector3.zero, new Vector2(1.7f, 1.0f), oilM);
            QuadObj("WallStain", props, new Vector3(2.6f, 0.8f, 7.338f), Vector3.zero, new Vector2(1.2f, 1.2f), rustM);
            QuadObj("WallStain", props, new Vector3(HX0 + 0.008f, 0.6f, 3.0f), new Vector3(0f, -90f, 0f), new Vector2(1.7f, 1.1f), oilM);
            QuadObj("WallStain", props, new Vector3(HX0 + 0.008f, 1.7f, 2.2f), new Vector3(0f, -90f, 0f), new Vector2(0.9f, 2.0f), rustM);
            Prim("FloorCable", PrimitiveType.Cube, props, new Vector3(-3.3f, 0.02f, 5.0f), new Vector3(1.6f, 0.03f, 0.04f),
                Mat("Sc01RedCable", new Color(0.55f, 0.05f, 0.04f), 0.3f)).transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        }

        // ---------------------------------------------------------------- yellow flat-platform cart (client reference)

        // Local frame: platform front = +z, tall handle frame at -z. The root faces yaw -90 (platform toward -x, the drums).
        private static GameObject BuildSc01Cart(Transform env, PresentableArt art, Vector3 floorPos,
            out FloatingTouchButton button, out GameObject loadDrum)
        {
            var root = new GameObject("DrumCarrier").transform;
            root.SetParent(env, false);
            // parked in the corner between the half wall and the tall wall (reference), platform toward the learner
            root.SetPositionAndRotation(floorPos, Quaternion.Euler(0f, 180f, 0f));
            // the client's own 3D trolley (Trolley.glb): flat deck, wheels, handle frame at -z. Scaled so the deck is ~0.2 m high
            // and the handle ~1.3 m; the model is 4 x 7.1 x 6.6 units, deck top at y 1.10, centred on x / z.
            const float modelScale = 0.18f;
            const float top = 1.10f * modelScale;
            const float w = 0.74f, d = 1.1f;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Trolley.glb");
            if (asset != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                model.transform.SetParent(root, false);
                model.transform.localScale = Vector3.one * modelScale;
                var tb = WorldBounds(model);
                model.transform.position += root.position - new Vector3(tb.center.x, tb.min.y, tb.center.z);
            }
            else
            {
                Debug.LogWarning("[Presentable] Trolley.glb missing");
            }

            AddSolidBox(root, "CartSolid", root.TransformPoint(new Vector3(0f, 0.15f, 0f)), new Vector3(w, 0.3f, d));
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // floating Touch button (finger flow), on the learner's side of the cart
            var btnGo = new GameObject("CarrierTouchButton");
            btnGo.transform.SetParent(root, false);
            btnGo.transform.position = root.TransformPoint(new Vector3(-0.2f, 1.3f, 0.15f));
            button = btnGo.AddComponent<FloatingTouchButton>();
            SetRef(button, "art", art);
            var bso = new SerializedObject(button);
            bso.FindProperty("label").stringValue = "Touch";
            bso.ApplyModifiedPropertiesWithoutUndo();

            // the drum that rides on the platform: same model + Isopropanol label as the correct hero drum
            loadDrum = BuildLoadedDrum(root);
            var hb = WorldBounds(loadDrum);
            var seatY = top + hb.size.y * 0.5f + 0.004f;
            loadDrum.transform.position = root.TransformPoint(new Vector3(0f, seatY, 0.056f));
            loadDrum.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // label toward the learner
            loadDrum.SetActive(false);
            // visible from the start like in the reference render (the director rolls it to the correct drum)
            return root.gameObject;
        }

        // Two wooden pallets side by side, two drums each (drums stand at x -1.02, -0.34, 0.34, 1.02).
        private static void BuildHeroPallets(Transform parent, float z, float h)
        {
            var wood = Mat("Sc01Pallet", new Color(0.78f, 0.74f, 0.7f), 0.04f, 0f, Sc01Tex("Pallet_Albedo")); // old, dull, no shine
            foreach (var cx0 in new[] { -0.69f, 0.69f })
            {
                var cx = cx0 + Sc01HeroShift;
                var holder = new GameObject("HeroPallet").transform;
                holder.SetParent(parent, false);
                const float w = 1.36f, d = 1.0f;
                for (var i = 0; i < 5; i++) // top boards
                    Prim("Board", PrimitiveType.Cube, holder, new Vector3(cx, h - 0.011f, z - d * 0.5f + 0.09f + i * 0.205f),
                        new Vector3(w, 0.022f, 0.15f), wood);
                for (var i = 0; i < 3; i++) // runners
                    Prim("Runner", PrimitiveType.Cube, holder, new Vector3(cx - w * 0.5f + 0.06f + i * (w - 0.12f) * 0.5f, 0.05f, z),
                        new Vector3(0.1f, 0.09f, d), wood);
                for (var i = 0; i < 3; i++) // bottom boards
                    Prim("BottomBoard", PrimitiveType.Cube, holder, new Vector3(cx, 0.011f, z - d * 0.5f + 0.09f + i * 0.41f),
                        new Vector3(w, 0.022f, 0.12f), wood);
                var solid = new GameObject("PalletSolid");
                solid.transform.SetParent(holder, false);
                solid.layer = SolidLayer;
                solid.transform.position = new Vector3(cx, h * 0.5f, z);
                solid.AddComponent<BoxCollider>().size = new Vector3(w, h, d);
            }
        }

        private static void BuildSc01Lights(Transform env)
        {
            var lights = new GameObject("Sc01Lights").transform;
            lights.SetParent(env, false);
            Light L(string name, LightType type, Vector3 pos, Color color, float intensity, float range, float angle = 60f)
            {
                var go = new GameObject(name);
                go.transform.SetParent(lights, false);
                go.transform.position = pos;
                var l = go.AddComponent<Light>();
                l.type = type;
                l.color = color;
                l.intensity = intensity;
                l.range = range;
                l.spotAngle = angle;
                l.shadows = LightShadows.None;
                return l;
            }

            // soft fill from the learner's side (no hard top light on the drum lids); the workshop door glows
            var key = L("DrumFill", LightType.Spot, new Vector3(0.4f, 2.6f, 0.5f), new Color(1f, 0.97f, 0.92f), 2.4f, 12f, 100f);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, 0.8f, 6.6f) - key.transform.position);
            L("DoorGlow", LightType.Point, new Vector3(-6.4f, 2.3f, 8.0f), new Color(1f, 0.97f, 0.9f), 6f, 9f);
            L("DoorSpill", LightType.Point, new Vector3(-3.4f, 1.8f, 7.6f), new Color(1f, 0.95f, 0.85f), 1.6f, 4f);
            L("BackFill", LightType.Point, new Vector3(1f, 3.4f, 12.5f), new Color(1f, 0.92f, 0.8f), 2.2f, 10f);
        }
    }
}
#endif

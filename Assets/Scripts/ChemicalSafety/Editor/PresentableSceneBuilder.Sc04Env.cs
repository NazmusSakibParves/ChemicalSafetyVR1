#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    // Scenario 4 lab realism: PBR materials (tile, epoxy, steel, ceiling) and set dressing.
    // Textures come from Tools/generate_sc04_env.py.
    public static partial class PresentableSceneBuilder
    {
        private const string Sc04TexDir = ArtRoot + "/2D/Sc04Tex";

        private static Texture2D Sc04Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Sc04TexDir + "/" + name + ".png");

        private static void ConfigureSc04Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc04TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var normal = file.EndsWith("_Normal");
                var linear = file.EndsWith("_MS");
                var decal = file.StartsWith("AO_");
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
                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 ||
                    ti.alphaIsTransparency != decal)
                {
                    ti.wrapMode = wrap;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = decal;
                    changed = true;
                }

                if (!decal && ti.anisoLevel < 4)
                {
                    ti.anisoLevel = 4;
                    changed = true;
                }

                if (changed)
                    ti.SaveAndReimport();
            }
        }

        // URP Lit with albedo + normal + metal(R)/smooth(A) maps from Sc04Tex.
        private static Material Sc04Pbr(string name, string set, Color tint, Vector2 tiling, float smoothScale = 1f,
            float bump = 1f, bool hasMs = true)
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

            m.SetTexture("_BaseMap", Sc04Tex(set + "_Albedo"));
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", Sc04Tex(set + "_Normal"));
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            if (hasMs && Sc04Tex(set + "_MS") != null)
            {
                m.SetTexture("_MetallicGlossMap", Sc04Tex(set + "_MS"));
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", smoothScale);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                m.SetFloat("_Smoothness", 0.1f);
                m.SetFloat("_Metallic", 0f);
            }

            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureScale("_BumpMap", tiling);
            m.SetTextureScale("_MetallicGlossMap", tiling);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // Soft black radial shadow lying on a surface (alpha blended, unlit).
        private static Material Sc04AoMat(float alpha)
        {
            var name = "Sc04Ao" + Mathf.RoundToInt(alpha * 100f);
            var m = Mat(name, new Color(0f, 0f, 0f, alpha), 0f, 0f, Sc04Tex("AO_Soft"), null, true);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
            return m;
        }

        // Dark soft pool of shadow on the floor under a bench, so the legs do not hover over a bright floor.
        private static void FloorAo(Transform room, string name, Bounds b, float alpha, float grow)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(room, true);
            q.transform.position = new Vector3(b.center.x, 0.004f, b.center.z);
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(b.size.x + grow, b.size.z + grow, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = Sc04AoMat(alpha);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static GameObject Cyl(string name, Transform room, Vector3 baseAt, float height, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(room, true);
            go.transform.position = baseAt + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void Bottle(Transform room, string name, Vector3 baseAt, float height, float radius, Material body,
            Material cap, bool label)
        {
            Cyl(name, room, baseAt, height * 0.78f, radius, body);
            Cyl(name + "Neck", room, baseAt + Vector3.up * (height * 0.78f), height * 0.12f, radius * 0.45f, body);
            Cyl(name + "Cap", room, baseAt + Vector3.up * (height * 0.9f), height * 0.1f, radius * 0.55f, cap);
            if (!label)
                return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name + "Label";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(room, true);
            q.transform.position = baseAt + new Vector3(0f, height * 0.38f, radius + 0.002f);
            q.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            q.transform.localScale = new Vector3(radius * 1.5f, height * 0.34f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Sc04LabelWhite", new Color(0.93f, 0.93f, 0.9f), 0.15f, 0f);
        }

        private static Material FridgeDoorGlass()
        {
            var glass = Mat("Sc04FridgeGlass", new Color(0.85f, 0.93f, 0.96f, 0.12f), 0.96f, 0.02f);
            glass.SetFloat("_Surface", 1f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = (int)RenderQueue.Transparent;
            return glass;
        }

        // Reagent bottles, jars, boxes and a rack of vials on the glass shelves.
        private static void StockFridge(Transform room, Vector3 pos, float w, float d, float[] shelfYs)
        {
            var amber = Mat("Sc04Amber", new Color(0.42f, 0.2f, 0.05f), 0.7f, 0.05f);
            var clear = LabGlass();
            var blue = Mat("Sc04CapBlue", new Color(0.12f, 0.35f, 0.8f), 0.4f, 0f);
            var red = Mat("Sc04CapRed", new Color(0.8f, 0.12f, 0.1f), 0.4f, 0f);
            var white = Mat("Sc04CapWhite", new Color(0.95f, 0.95f, 0.95f), 0.4f, 0f);
            var green = Mat("Sc04CapGreen", new Color(0.12f, 0.55f, 0.25f), 0.4f, 0f);
            var liquid = Mat("Sc04LiquidBlue", new Color(0.55f, 0.8f, 0.95f, 0.75f), 0.8f, 0f);
            var box = Mat("Sc04Carton", new Color(0.72f, 0.58f, 0.4f), 0.15f, 0f);
            var caps = new[] { blue, red, white, green };
            for (var s = 0; s < shelfYs.Length; s++)
            {
                var y = pos.y + shelfYs[s] + 0.008f;
                var n = 4;
                for (var i = 0; i < n; i++)
                {
                    var x = pos.x - w * 0.5f + 0.13f + i * ((w - 0.26f) / (n - 1));
                    var z = pos.z + (s % 2 == 0 ? 0.04f : -0.02f) + ((i % 2) * 0.06f - 0.03f);
                    var body = (i + s) % 3 == 0 ? amber : (i + s) % 3 == 1 ? clear : liquid;
                    var h = 0.14f + 0.05f * ((i * 7 + s * 3) % 4) / 3f;
                    if (s == 3 && i % 2 == 1)
                    {
                        Box("FridgeCarton" + s + i, room, new Vector3(x, y + 0.06f, z), new Vector3(0.14f, 0.12f, 0.16f), box, false);
                        continue;
                    }

                    Bottle(room, "FridgeBottle" + s + i, new Vector3(x, y, z), h, 0.036f + 0.006f * (i % 2), body,
                        caps[(i + s) % caps.Length], body == amber);
                }
            }
        }

        // A solid stainless lab table (same build as the Scenario 6 table, without a drawer cabinet): thick top with rolled edges,
        // an apron, square legs and, for the fixed benches, a lower shelf. 'follower' tables (the glove bench moves with the
        // head) get legs that reach far below the floor so they never hang in the air when raised. 'alongZ' = the long side runs
        // along Z. 'bounds' is the footprint (and top height = bounds.max.y) of the thin glb table it replaces.
        private static void Sc04SolidTable(Transform parent, Bounds bounds, bool alongZ, Material steel, bool follower)
        {
            var trim = Mat("Sc06SteelPlain", new Color(0.76f, 0.78f, 0.81f), 0.72f, 0.15f);
            var dark = Mat("Sc06TableDark", new Color(0.30f, 0.32f, 0.34f), 0.5f, 0.2f);
            const float th = 0.05f;
            var L = alongZ ? bounds.size.z : bounds.size.x;
            var D = alongZ ? bounds.size.x : bounds.size.z;
            var cx = bounds.center.x;
            var cz = bounds.center.z;
            var T = bounds.max.y;
            Vector3 M(float u, float y, float v) => new Vector3(cx + (alongZ ? v : u), y, cz + (alongZ ? u : v));
            Vector3 S(float a, float h, float b) => alongZ ? new Vector3(b, h, a) : new Vector3(a, h, b);

            Box("TableTop", parent, M(0f, T - th * 0.5f, 0f), S(L, th, D), steel, false);
            var er = th * 0.52f;
            var ey = T - th * 0.5f;
            Sc06Rod("TableEdgeA", parent, M(-L * 0.5f, ey, D * 0.5f), M(L * 0.5f, ey, D * 0.5f), er, trim);
            Sc06Rod("TableEdgeB", parent, M(-L * 0.5f, ey, -D * 0.5f), M(L * 0.5f, ey, -D * 0.5f), er, trim);
            Sc06Rod("TableEdgeC", parent, M(L * 0.5f, ey, -D * 0.5f), M(L * 0.5f, ey, D * 0.5f), er, trim);
            Sc06Rod("TableEdgeD", parent, M(-L * 0.5f, ey, -D * 0.5f), M(-L * 0.5f, ey, D * 0.5f), er, trim);
            var ay = T - th - 0.05f;
            Box("ApronA", parent, M(0f, ay, D * 0.5f - 0.05f), S(L - 0.1f, 0.1f, 0.022f), trim, false);
            Box("ApronB", parent, M(0f, ay, -D * 0.5f + 0.05f), S(L - 0.1f, 0.1f, 0.022f), trim, false);
            Box("ApronC", parent, M(L * 0.5f - 0.05f, ay, 0f), S(0.022f, 0.1f, D - 0.1f), trim, false);
            Box("ApronD", parent, M(-L * 0.5f + 0.05f, ay, 0f), S(0.022f, 0.1f, D - 0.1f), trim, false);
            var legBottom = follower ? -1.2f : 0f;
            var legH = T - th - legBottom;
            foreach (var lu in new[] { -L * 0.5f + 0.08f, L * 0.5f - 0.08f })
            {
                foreach (var lv in new[] { -D * 0.5f + 0.07f, D * 0.5f - 0.07f })
                {
                    Box("TableLeg", parent, M(lu, legBottom + legH * 0.5f, lv), S(0.055f, legH, 0.055f), trim, false);
                    if (!follower)
                        Cyl("TableFoot", parent, M(lu, 0f, lv), 0.02f, 0.032f, dark);
                }
            }

            if (follower)
                return;
            var sl = L - 0.16f;
            Box("TableShelf", parent, M(0f, 0.2f, 0f), S(sl, 0.03f, D - 0.12f), steel, false);
            Box("TableShelfLipA", parent, M(0f, 0.225f, D * 0.5f - 0.06f), S(sl, 0.03f, 0.015f), trim, false);
            Box("TableShelfLipB", parent, M(0f, 0.225f, -D * 0.5f + 0.06f), S(sl, 0.03f, 0.015f), trim, false);
        }

        // Stainless bench along the back wall with a balance, centrifuge, glassware and a waste bin beside it.
        private static void LabBackBench(Transform room, float ox)
        {
            var bench = SpawnSc04("table02.glb", room, new Vector3(ox - 0.4f, 0f, -2.75f), 90f, 2.6f);
            if (bench == null)
                return;
            SitOn(bench, 0f);
            var b0 = WorldBounds(bench);
            bench.transform.position += new Vector3(0f, 0f, -3.1f - b0.min.z);
            var b = WorldBounds(bench);
            var top = b.max.y;
            Object.DestroyImmediate(bench);
            Sc04SolidTable(room, b, false, Steel(), false);
            FloorAo(room, "AoBackBench", b, 0.4f, 0.4f);

            var white = Mat("Sc04Balance", new Color(0.92f, 0.93f, 0.94f), 0.4f, 0.05f);
            var dark = Mat("Sc04BalanceTop", new Color(0.12f, 0.13f, 0.14f), 0.3f, 0.4f);
            var screen = Mat("Sc04BalanceScreen", new Color(0.1f, 0.2f, 0.12f), 0f, 0f, null, new Color(0.15f, 0.7f, 0.35f), true);
            var bx = b.center.x + 0.55f;
            Box("BalanceBase", room, new Vector3(bx, top + 0.035f, b.center.z + 0.05f), new Vector3(0.32f, 0.07f, 0.36f), white, false);
            Cyl("BalancePan", room, new Vector3(bx, top + 0.07f, b.center.z + 0.02f), 0.012f, 0.1f, dark);
            Box("BalanceScreen", room, new Vector3(bx, top + 0.06f, b.center.z + 0.235f), new Vector3(0.14f, 0.04f, 0.01f), screen, false);

            // benchtop centrifuge: body, lid
            var cx = b.center.x - 0.1f;
            Cyl("CentrifugeBody", room, new Vector3(cx, top, b.center.z), 0.17f, 0.2f, white);
            Cyl("CentrifugeLid", room, new Vector3(cx, top + 0.17f, b.center.z), 0.03f, 0.21f, dark);
            Box("CentrifugePanel", room, new Vector3(cx, top + 0.08f, b.center.z + 0.2f), new Vector3(0.18f, 0.06f, 0.012f), dark, false);

            UseGlass(PlacePiece("chemistry_glassware.glb", "lab_beaker_a", room,
                new Vector3(b.center.x - 0.65f, top, b.center.z + 0.08f), 0f, 0.12f, top));
            UseGlass(PlacePiece("chemistry_glassware.glb", "lab_erlenmeyer_a_0", room,
                new Vector3(b.center.x - 0.85f, top, b.center.z - 0.04f), 10f, 0.2f, top));
            UseGlass(PlacePiece("chemistry_glassware.glb", "lab_cylinder_a", room,
                new Vector3(b.center.x - 0.5f, top, b.center.z - 0.12f), 0f, 0.22f, top));

            // grey waste bin and a stool
            var grey = Mat("Sc04Bin", new Color(0.38f, 0.4f, 0.42f), 0.35f, 0.1f);
            Cyl("WasteBin", room, new Vector3(b.max.x + 0.3f, 0f, -2.85f), 0.5f, 0.18f, grey);
            Cyl("WasteBinLid", room, new Vector3(b.max.x + 0.3f, 0.5f, -2.85f), 0.03f, 0.19f,
                Mat("Sc04BinLid", new Color(0.18f, 0.2f, 0.22f), 0.3f, 0.1f));
        }

        // Open reagent shelves on the back wall above the bench.
        private static void LabShelving(Transform room, float ox)
        {
            var board = Mat("Sc04ShelfBoard", new Color(0.86f, 0.88f, 0.9f), 0.45f, 0.35f);
            var amber = Mat("Sc04Amber", new Color(0.42f, 0.2f, 0.05f), 0.7f, 0.05f);
            var clear = LabGlass();
            var caps = new[]
            {
                Mat("Sc04CapBlue", new Color(0.12f, 0.35f, 0.8f), 0.4f, 0f),
                Mat("Sc04CapRed", new Color(0.8f, 0.12f, 0.1f), 0.4f, 0f),
                Mat("Sc04CapWhite", new Color(0.95f, 0.95f, 0.95f), 0.4f, 0f)
            };
            var x0 = ox + 1.45f;
            var ys = new[] { 1.5f, 1.88f };
            Box("ShelfBracketL", room, new Vector3(x0 - 0.62f, 1.7f, -3.08f), new Vector3(0.03f, 0.5f, 0.06f), board, false);
            Box("ShelfBracketR", room, new Vector3(x0 + 0.62f, 1.7f, -3.08f), new Vector3(0.03f, 0.5f, 0.06f), board, false);
            for (var s = 0; s < ys.Length; s++)
            {
                Box("WallShelf" + s, room, new Vector3(x0, ys[s], -3.0f), new Vector3(1.3f, 0.025f, 0.26f), board, false);
                for (var i = 0; i < 6; i++)
                {
                    var body = (i + s) % 2 == 0 ? amber : clear;
                    Bottle(room, "ShelfBottle" + s + i, new Vector3(x0 - 0.5f + i * 0.2f, ys[s] + 0.0125f, -3.0f),
                        0.17f + 0.03f * ((i + s) % 3), 0.04f, body, caps[(i + s) % caps.Length], body == amber);
                }
            }
        }

        // Set dressing from the designer photo: cove base, T-bar ceiling lights, under-cabinet strip, door, sockets.
        private static void DressLabDetails(Transform room, float ox, Bounds sideBox, Bounds gloveBox)
        {
            var cove = Mat("Sc04Cove", new Color(0.32f, 0.34f, 0.36f), 0.35f, 0.05f);
            Box("CoveBack", room, new Vector3(ox + 0.47f, 0.06f, -3.12f), new Vector3(6.8f, 0.12f, 0.03f), cove, false);
            Box("CoveFront", room, new Vector3(ox + 0.47f, 0.06f, 3.52f), new Vector3(6.8f, 0.12f, 0.03f), cove, false);
            Box("CoveLeft", room, new Vector3(ox + 3.86f, 0.06f, 0.2f), new Vector3(0.03f, 0.12f, 6.6f), cove, false);
            Box("CoveRight", room, new Vector3(ox - 2.92f, 0.06f, 0.2f), new Vector3(0.03f, 0.12f, 6.6f), cove, false);

            // glowing strip under the wall cabinet, as in the photo
            var strip = Mat("Sc04Strip", new Color(0.95f, 0.97f, 0.95f), 0f, 0f, null, new Color(1.6f, 1.65f, 1.55f), true);
            Box("CabStrip", room, new Vector3(ox + 3.6f, 1.84f, sideBox.center.z), new Vector3(0.1f, 0.02f, Mathf.Max(1.6f, sideBox.size.z * 0.7f)),
                strip, false);
            var tube = Mat("Sc04TubePanel", new Color(0.96f, 0.97f, 0.96f), 0f, 0f, null, new Color(1.25f, 1.28f, 1.22f), true);
            for (var i = 0; i < 4; i++)
            {
                var x = ox - 2.2f + (i % 2) * 2.4f;
                var z = -1.8f + (i / 2) * 2.8f;
                Box("CeilingPanel" + i, room, new Vector3(x, 3.17f, z), new Vector3(1.2f, 0.02f, 0.3f), tube, false);
            }

            // door in the viewer's-right wall behind the eyewash
            var door = Mat("Sc04Door", new Color(0.82f, 0.84f, 0.85f), 0.45f, 0.35f);
            var frame = Mat("Sc04DoorFrame", new Color(0.55f, 0.58f, 0.6f), 0.5f, 0.6f);
            var dz = 0.9f;
            Box("DoorFrame", room, new Vector3(ox - 2.915f, 1.08f, dz), new Vector3(0.04f, 2.2f, 1.08f), frame, false);
            Box("Door", room, new Vector3(ox - 2.895f, 1.04f, dz), new Vector3(0.04f, 2.1f, 0.96f), door, false);
            Box("DoorWindow", room, new Vector3(ox - 2.87f, 1.55f, dz), new Vector3(0.02f, 0.62f, 0.34f),
                Mat("Sc04DoorGlass", new Color(0.12f, 0.16f, 0.18f), 0.9f, 0.1f), false);
            Box("DoorPlate", room, new Vector3(ox - 2.87f, 1.0f, dz + 0.36f), new Vector3(0.02f, 0.14f, 0.05f), frame, false);

            // red extinguisher beside the door
            var red = Mat("Sc04Extinguisher", new Color(0.75f, 0.08f, 0.06f), 0.55f, 0.1f);
            var ext = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ext.name = "Extinguisher";
            Object.DestroyImmediate(ext.GetComponent<Collider>());
            ext.transform.SetParent(room, true);
            ext.transform.position = new Vector3(ox - 2.82f, 0.85f, dz - 0.85f);
            ext.transform.localScale = new Vector3(0.16f, 0.26f, 0.16f);
            ext.GetComponent<MeshRenderer>().sharedMaterial = red;
            Box("ExtinguisherHead", room, new Vector3(ox - 2.82f, 1.14f, dz - 0.85f), new Vector3(0.07f, 0.06f, 0.05f),
                Mat("Sc04ExtHead", new Color(0.1f, 0.1f, 0.11f), 0.4f, 0.3f), false);

            // sockets and a switch on the back wall
            var plate = Mat("Sc04Socket", new Color(0.9f, 0.91f, 0.9f), 0.4f, 0.02f);
            for (var i = 0; i < 3; i++)
                Box("Socket" + i, room, new Vector3(ox + 1.4f - i * 0.9f, 1.12f, -3.115f), new Vector3(0.08f, 0.08f, 0.012f), plate, false);
            Box("Switch", room, new Vector3(ox - 2.5f, 1.3f, -3.115f), new Vector3(0.06f, 0.1f, 0.012f), plate, false);

            FloorAo(room, "AoGloveTable", gloveBox, 0.45f, 0.5f);
            FloorAo(room, "AoSideTable", sideBox, 0.45f, 0.5f);
        }
    }
}
#endif

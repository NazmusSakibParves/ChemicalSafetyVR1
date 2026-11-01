#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 12: the PPE Donning Station, modelled on the Scene 12 workflow pictures. The user faces -Z. A narrow grey stucco
    /// corridor (acoustic ceiling tiles with light panels, polished concrete floor) ends in a yellow wall band
    /// "PPE DONNING STATION" above four wooden lockers (Goggles / Face Shield, Gloves, Respirators, Aprons and Suits).
    /// Left wall: the bilingual "Don PPE" sign and the SDS board. Right wall: a large window onto the process plant (tanks, pipes).
    /// Each locker holds four options of one pick round, named Item_{round}_{option}; option 0 is the right one. Env origin x = 500.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc12Refs
        {
            public Transform spawn;
            public Transform startStand;
            public Transform hereStand;
            public Transform[] lockers = new Transform[4];
            public Transform[,] items = new Transform[4, 4];
            public FloorHotspot hotspot;
            public Sc12Station station;
            public Sc06Gate gate;
        }

        private const float Sc12Ox = 500f;
        private const float Sc12HalfW = 2.5f;
        private const float Sc12FarZ = -4.0f;
        private const float Sc12BackZ = 4.6f;
        private const float Sc12H = 3.0f;
        private const string Sc12TexDir = ArtRoot + "/2D/Sc12Tex";
        private const string Sc12ModelDir = ArtRoot + "/3D/Sc12";

        private static Texture2D Sc12Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc12TexDir + "/" + name + ".png");

        private static Texture2D Sc12Art(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc12/" + name + ".png");

        private static void Sc12FixTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc12TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var normal = path.EndsWith("_Normal.png");
                var wanted = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != wanted || ti.wrapMode != TextureWrapMode.Repeat || ti.anisoLevel < 8 || ti.maxTextureSize != 1024)
                {
                    ti.textureType = wanted;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.anisoLevel = 8;
                    ti.maxTextureSize = 1024;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }
        }

        // a quad that faces the given direction (the primitive Quad looks toward -Z)
        private static GameObject Sc12Poster(string name, Transform parent, Vector3 pos, float yaw, Vector2 size, Material mat)
        {
            var q = QuadObj(name, parent, pos, new Vector3(0f, yaw, 0f), size, mat);
            q.transform.SetParent(parent, true);
            return q;
        }

        /// <summary>One logical part (all its sub-meshes) of Sc12Ppe.glb / Sc12Plant.glb, pivot at the bottom centre.</summary>
        private static GameObject Sc12Part(string file, string part, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Sc12ModelDir + "/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc12 model " + file);
                return new GameObject(part + "_missing");
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var kill = new List<GameObject>();
            foreach (Transform c in go.transform)
                if (!c.name.StartsWith(part + "_"))
                    kill.Add(c.gameObject);
            foreach (var k in kill)
                Object.DestroyImmediate(k);
            go.name = part;
            go.transform.SetParent(parent, false);
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            return go;
        }

        private static void Sc12Tint(GameObject go, string matName, Color tint, float smooth, float metal = 0f)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterial;
                var tex = src != null && src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                var m = Mat(matName, tint, smooth, metal, tex);
                var mats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < mats.Length; i++)
                    mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        private static GameObject Sc12Cyl(string name, Transform parent, Vector3 pos, float radius, float length, Material mat, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material Sc12Tiled(string name, Texture2D tex, Color color, float smooth, float metal, Vector2 tiles, Texture2D normal = null)
        {
            var m = Mat(name, color, smooth, metal, tex);
            m.SetTextureScale("_BaseMap", tiles);
            if (normal != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.6f);
                m.SetTextureScale("_BumpMap", tiles);
            }

            return m;
        }

        // wall slab whose texture repeats about every 2 m
        private static GameObject Sc12Wall(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = Box(name, parent, center, size, mat);
            go.isStatic = true;
            return go;
        }

        private static Sc12Refs BuildSc12Room(Transform env, PresentableArt art)
        {
            const float ox = Sc12Ox;
            var refs = new Sc12Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            Sc12FixTextures();
            var solidLayer = SolidLayer;

            // indoor light: no sun, soft sky ambient, the light panels and a few point lights do the rest
            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.66f, 0.68f, 0.70f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.56f, 0.57f, 0.58f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.34f, 0.33f, 0.32f);
            mso.FindProperty("fogColor").colorValue = new Color(0.60f, 0.63f, 0.66f);
            mso.FindProperty("fogStart").floatValue = 22f;
            mso.FindProperty("fogEnd").floatValue = 120f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.5f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var len = Sc12BackZ - Sc12FarZ; // 8.6
            var midZ = (Sc12BackZ + Sc12FarZ) * 0.5f;
            var normal = Sc12Tex("Sc12_Stucco_Normal");
            var wallMat = Sc12Tiled("Sc12WallMat", Sc12Tex("Sc12_Wall"), new Color(1.05f, 1.05f, 1.03f), 0.12f, 0f, new Vector2(2.6f, 1.5f), normal);
            var wallMatSide = Sc12Tiled("Sc12WallMatSide", Sc12Tex("Sc12_Wall"), new Color(1.05f, 1.05f, 1.03f), 0.12f, 0f, new Vector2(4.4f, 1.5f), normal);
            var floorMat = Sc12Tiled("Sc12FloorMat", Sc12Tex("Sc12_Floor"), new Color(1.0f, 1.0f, 1.0f), 0.62f, 0.05f, new Vector2(2.5f, 4.3f));
            var ceilMat = Sc12Tiled("Sc12CeilingMat", Sc12Tex("Sc12_Ceiling"), new Color(1f, 1f, 0.98f), 0.1f, 0f, new Vector2(Sc12HalfW * 2f / 1.2f, len / 1.2f));
            var skirtMat = Mat("Sc12Skirt", new Color(0.30f, 0.31f, 0.32f), 0.35f, 0f);
            var steelMat = Mat("Sc12SteelMat", new Color(0.62f, 0.64f, 0.66f), 0.55f, 0.85f, Sc12Tex("Sc12_Steel"));

            // ---------------------------------------------------------------- shell: floor, ceiling, four walls (right wall has the window)
            var floor = Box("Floor", room, new Vector3(ox, -0.05f, midZ), new Vector3(Sc12HalfW * 2f, 0.1f, len), floorMat);
            floor.AddComponent<TeleportationArea>();
            if (solidLayer >= 0)
                floor.layer = solidLayer;
            Box("Ceiling", room, new Vector3(ox, Sc12H + 0.05f, midZ), new Vector3(Sc12HalfW * 2f, 0.1f, len), ceilMat, false);
            Sc12Wall("FarWall", room, new Vector3(ox, Sc12H * 0.5f, Sc12FarZ - 0.1f), new Vector3(Sc12HalfW * 2f + 0.4f, Sc12H, 0.2f), wallMat);
            Sc12Wall("BackWall", room, new Vector3(ox, Sc12H * 0.5f, Sc12BackZ + 0.1f), new Vector3(Sc12HalfW * 2f + 0.4f, Sc12H, 0.2f), wallMat);
            Sc12Wall("LeftWall", room, new Vector3(ox + Sc12HalfW + 0.1f, Sc12H * 0.5f, midZ), new Vector3(0.2f, Sc12H, len), wallMatSide);
            if (solidLayer >= 0)
                foreach (var n in new[] { "FarWall", "BackWall", "LeftWall" })
                    room.Find(n).gameObject.layer = solidLayer;

            // right wall around the window opening: z -2.7 .. 1.5, y 0.95 .. 2.7
            const float wz0 = -2.7f, wz1 = 1.5f, wy0 = 0.95f, wy1 = 2.7f;
            var rx = ox - Sc12HalfW - 0.1f;
            Sc12Wall("RightWall_Below", room, new Vector3(rx, wy0 * 0.5f, midZ), new Vector3(0.2f, wy0, len), wallMatSide);
            Sc12Wall("RightWall_Above", room, new Vector3(rx, (wy1 + Sc12H) * 0.5f, midZ), new Vector3(0.2f, Sc12H - wy1, len), wallMatSide);
            Sc12Wall("RightWall_Far", room, new Vector3(rx, (wy0 + wy1) * 0.5f, (Sc12FarZ + wz0) * 0.5f), new Vector3(0.2f, wy1 - wy0, wz0 - Sc12FarZ), wallMatSide);
            Sc12Wall("RightWall_Near", room, new Vector3(rx, (wy0 + wy1) * 0.5f, (wz1 + Sc12BackZ) * 0.5f), new Vector3(0.2f, wy1 - wy0, Sc12BackZ - wz1), wallMatSide);
            if (solidLayer >= 0)
                foreach (var n in new[] { "RightWall_Below", "RightWall_Above", "RightWall_Far", "RightWall_Near" })
                    room.Find(n).gameObject.layer = solidLayer;

            // window: steel frame, mullions, glass; a deep sill; the inner reveal in the wall colour
            var wx = ox - Sc12HalfW + 0.02f;
            var wcz = (wz0 + wz1) * 0.5f;
            var ww = wz1 - wz0;
            Box("WinFrameTop", room, new Vector3(wx, wy1 + 0.04f, wcz), new Vector3(0.12f, 0.1f, ww + 0.16f), steelMat, false);
            Box("WinFrameBottom", room, new Vector3(wx, wy0 - 0.04f, wcz), new Vector3(0.22f, 0.1f, ww + 0.16f), steelMat, false);
            Box("WinFrameFar", room, new Vector3(wx, (wy0 + wy1) * 0.5f, wz0 - 0.04f), new Vector3(0.12f, wy1 - wy0, 0.1f), steelMat, false);
            Box("WinFrameNear", room, new Vector3(wx, (wy0 + wy1) * 0.5f, wz1 + 0.04f), new Vector3(0.12f, wy1 - wy0, 0.1f), steelMat, false);
            for (var i = 1; i < 3; i++)
                Box("WinMullion" + i, room, new Vector3(wx, (wy0 + wy1) * 0.5f, wz0 + ww * i / 3f), new Vector3(0.07f, wy1 - wy0, 0.07f), steelMat, false);
            var glass = Sc06GlassMat("Sc12WindowGlass", new Color(0.85f, 0.92f, 0.96f), 0.06f, 0.5f);
            var gq = QuadObj("WindowGlass", room, new Vector3(wx - 0.03f, (wy0 + wy1) * 0.5f, wcz), new Vector3(0f, 270f, 0f), new Vector2(ww, wy1 - wy0), glass);
            gq.transform.SetParent(room, true);

            // skirting boards
            Box("SkirtLeft", room, new Vector3(ox - Sc12HalfW + 0.02f, 0.07f, midZ), new Vector3(0.04f, 0.14f, len), skirtMat, false);
            Box("SkirtRight", room, new Vector3(ox + Sc12HalfW - 0.02f, 0.07f, midZ), new Vector3(0.04f, 0.14f, len), skirtMat, false);

            Sc12Ceiling(room, ox, steelMat);
            Sc12Lockers(room, refs, art, ox);
            Sc12Signs(room, ox);
            Sc12Plant(room, ox, steelMat);

            refs.spawn = Marker("Spawn_SC12", room, new Vector3(ox + 0.1f, 0f, 3.7f), 180f);
            refs.startStand = Marker("Stand_SC12_Start", room, new Vector3(ox + 0.1f, 0f, 3.0f), 180f);
            refs.hereStand = Marker("Stand_SC12_Here", room, new Vector3(ox, 0f, -2.55f), 180f);
            refs.hotspot = Hotspot("Hotspot_SC12", room, art, new Vector3(ox, 0f, -2.1f));

            var stationGo = new GameObject("Sc12StationHost");
            stationGo.transform.SetParent(room, false);
            refs.station = stationGo.AddComponent<Sc12Station>();
            var sso = new SerializedObject(refs.station);
            sso.FindProperty("art").objectReferenceValue = art;
            FillArray(sso.FindProperty("items"), 16, (pp, k) => pp.objectReferenceValue = refs.items[k / 4, k % 4]);
            sso.ApplyModifiedPropertiesWithoutUndo();

            var gateGo = new GameObject("Sc12GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc12Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc12Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc12Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }

        // ---------------------------------------------------------------- ceiling: light panels, point lights, the exposed pipe run
        private static void Sc12Ceiling(Transform room, float ox, Material steelMat)
        {
            var panelMat = Mat("Sc12LightPanel", new Color(0.9f, 0.92f, 0.95f), 0.3f, 0f, null, new Color(2.6f, 2.65f, 2.7f));
            var frameMat = Mat("Sc12PanelFrame", new Color(0.82f, 0.83f, 0.84f), 0.4f, 0.4f);
            var zs = new[] { -3.0f, -0.35f, 2.4f };
            for (var i = 0; i < zs.Length; i++)
            {
                var big = i == 0;
                var size = big ? new Vector3(2.4f, 0.04f, 0.6f) : new Vector3(1.2f, 0.04f, 0.6f);
                Box("LightFrame" + i, room, new Vector3(ox, Sc12H - 0.01f, zs[i]), new Vector3(size.x + 0.12f, 0.03f, size.z + 0.12f), frameMat, false);
                Box("LightPanel" + i, room, new Vector3(ox, Sc12H - 0.04f, zs[i]), size, panelMat, false);
                var l = new GameObject("CeilingLight" + i).AddComponent<Light>();
                l.transform.SetParent(room, true);
                l.transform.position = new Vector3(ox, Sc12H - 0.4f, zs[i] + (big ? 0.8f : 0f));
                l.type = LightType.Point;
                l.color = new Color(1f, 0.97f, 0.92f);
                l.intensity = big ? 2.4f : 1.6f;
                l.range = 6.5f;
                l.shadows = LightShadows.None;
            }

            // a bundle of conduit and a pipe along the left of the ceiling, with brackets (step04)
            var pipeMat = Mat("Sc12CeilingPipe", new Color(0.46f, 0.48f, 0.50f), 0.5f, 0.7f);
            Sc12Cyl("CeilingPipe", room, new Vector3(ox + 1.9f, Sc12H - 0.25f, 0.3f), 0.07f, Sc12BackZ - Sc12FarZ - 0.4f, pipeMat, new Vector3(90f, 0f, 0f));
            Sc12Cyl("CeilingConduit", room, new Vector3(ox + 1.62f, Sc12H - 0.16f, 0.3f), 0.025f, Sc12BackZ - Sc12FarZ - 0.4f, steelMat, new Vector3(90f, 0f, 0f));
            for (var z = -3.4f; z < Sc12BackZ - 0.3f; z += 1.6f)
                Box("PipeBracket", room, new Vector3(ox + 1.8f, Sc12H - 0.16f, z), new Vector3(0.45f, 0.03f, 0.05f), steelMat, false);
        }

        // ---------------------------------------------------------------- the lockers and what is in them
        private static readonly string[] Sc12LabelFiles = { "Sc12_LblGoggles", "Sc12_LblGloves", "Sc12_LblRespirators", "Sc12_LblAprons" };

        private static void Sc12Lockers(Transform room, Sc12Refs refs, PresentableArt art, float ox)
        {
            var woodMat = Sc12Tiled("Sc12LockerWood", Sc12Tex("Sc12_Wood"), new Color(1.05f, 1.0f, 0.95f), 0.5f, 0f, new Vector2(1f, 1f));
            var woodDark = Mat("Sc12LockerWoodDark", new Color(0.50f, 0.34f, 0.17f), 0.4f, 0f, Sc12Tex("Sc12_Wood"));
            var backMat = Mat("Sc12LockerBack", new Color(0.78f, 0.58f, 0.32f), 0.25f, 0f, Sc12Tex("Sc12_Wood"));
            var yellowMat = Sc12Tiled("Sc12BandYellow", Sc12Tex("Sc12_Yellow"), Color.white, 0.35f, 0f, new Vector2(1.5f, 0.5f));
            var solid = SolidLayer;

            const float lw = 0.78f, lh = 2.0f, ld = 0.46f;
            const float front = Sc12FarZ + 0.55f; // front plane of the lockers, z = -3.45
            var cz = Sc12FarZ + ld * 0.5f + 0.05f;
            var xs = new[] { 1.17f, 0.39f, -0.39f, -1.17f };

            // yellow band on the far wall above the lockers and the station plaque
            Box("YellowBand", room, new Vector3(ox, (lh + Sc12H) * 0.5f + 0.02f, Sc12FarZ + 0.015f), new Vector3(3.6f, Sc12H - lh - 0.04f, 0.03f), yellowMat, false);
            var plaqueMat = Mat("Sc12PlaqueStation", Color.white, 0.4f, 0.1f, Sc12Art("Sc12_Station"));
            Sc12Poster("Plaque_Station", room, new Vector3(ox, 2.5f, Sc12FarZ + 0.045f), 180f, new Vector2(2.9f, 0.384f), plaqueMat);

            var shelfY = new[] { 0.40f, 0.88f, 1.36f, 1.84f }; // floor of bay 0 (boots), then three shelves for the items, then the top
            for (var k = 0; k < 4; k++)
            {
                var x = ox + xs[k];
                var root = new GameObject("Locker_" + k).transform;
                root.SetParent(room, false);
                refs.lockers[k] = root;
                // carcass: two sides, a back, the shelves and a top, a plinth
                Box("SideL", root, new Vector3(x - lw * 0.5f + 0.02f, lh * 0.5f, cz), new Vector3(0.04f, lh, ld), woodMat).layer = Mathf.Max(0, solid);
                Box("SideR", root, new Vector3(x + lw * 0.5f - 0.02f, lh * 0.5f, cz), new Vector3(0.04f, lh, ld), woodMat).layer = Mathf.Max(0, solid);
                Box("Back", root, new Vector3(x, lh * 0.5f, Sc12FarZ + 0.07f), new Vector3(lw - 0.06f, lh, 0.02f), backMat, false);
                for (var s = 0; s < shelfY.Length; s++)
                {
                    if (k == 3 && s >= 1 && s <= 2)
                        continue;
                    Box("Shelf" + s, root, new Vector3(x, shelfY[s], cz), new Vector3(lw - 0.06f, 0.035f, ld - 0.02f), woodMat, false);
                }

                if (k == 3)
                    Sc12Cyl("HangRail", root, new Vector3(x, 1.78f, cz + 0.02f), 0.012f, lw - 0.08f, Mat("Sc12HangRail", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.9f), new Vector3(0f, 0f, 90f));
                Box("Top", root, new Vector3(x, lh + 0.02f, cz), new Vector3(lw + 0.02f, 0.05f, ld + 0.04f), woodDark, false);
                Box("Plinth", root, new Vector3(x, 0.05f, front + 0.01f), new Vector3(lw - 0.04f, 0.1f, 0.02f), woodDark, false);
                // label plaque on a header board across the top compartment
                Box("Header", root, new Vector3(x, 1.87f, front + 0.005f), new Vector3(lw - 0.06f, 0.26f, 0.02f), woodMat, false);
                var lblMat = Mat("Sc12" + Sc12LabelFiles[k], Color.white, 0.4f, 0.1f, Sc12Art(Sc12LabelFiles[k]));
                Sc12Poster("Label", root, new Vector3(x, 1.87f, front + 0.02f), 180f, new Vector2(0.62f, 0.283f), lblMat);
                // a shelf-edge shadow strip so the open fronts read as depth
                Box("ShelfLip", root, new Vector3(x, 0.4f, front - 0.01f), new Vector3(lw - 0.06f, 0.035f, 0.02f), woodDark, false);
            }

            Sc12Items(room, refs, ox, xs, shelfY, cz);
            Sc12Floorgear(room, ox, xs, front);
        }

        private static void Sc12Items(Transform room, Sc12Refs refs, float ox, float[] xs, float[] shelfY, float cz)
        {
            // one entry per option: (round, option) -> a model part, tint and height. Option 0 is always the right one.
            Transform Place(int round, int option, GameObject go, float height, Vector3 at, float yaw)
            {
                go.name = "Item_" + round + "_" + option;
                go.transform.SetParent(refs.lockers[round], true);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                FitHeight(go, height);
                var b = WorldBounds(go);
                go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
                // a thin trigger so the finger can later pick the item (the station adds the PokeTarget)
                var bc = go.AddComponent<BoxCollider>();
                var lb = WorldBounds(go);
                bc.center = go.transform.InverseTransformPoint(lb.center);
                bc.size = lb.size;
                bc.isTrigger = true;
                refs.items[round, option] = go.transform;
                return go.transform;
            }

            var xz = ox;
            // shelf slots: three shelves above the boot bay, left and right of the centre line
            Vector3 Slot(int locker, int shelf, int side) =>
                new Vector3(xz + xs[locker] + (side == 0 ? -0.14f : side == 1 ? 0.14f : 0f), shelfY[shelf] + 0.02f, cz + 0.03f);

            // 0 goggles / face shield locker: splash goggles, spectacles, welding shield, dust goggles
            var g0 = Sc08Goggles(room, "tmpG0", Vector3.zero, 1f, Quaternion.identity).gameObject;
            Place(0, 0, g0, 0.13f, Slot(0, 0, 0), 180f);
            var g1 = Sc08Goggles(room, "tmpG1", Vector3.zero, 1f, Quaternion.identity).gameObject;
            Place(0, 1, g1, 0.11f, Slot(0, 0, 1), 180f);
            var g2 = Sc12Part("Sc12Ppe.glb", "FaceShield", room);
            Place(0, 2, g2, 0.34f, Slot(0, 1, 0), 90f);
            Sc12Tint(g2, "Sc12WeldShield", new Color(0.05f, 0.18f, 0.12f), 0.5f);
            var g3 = Sc08Goggles(room, "tmpG3", Vector3.zero, 1f, Quaternion.identity).gameObject;
            Place(0, 3, g3, 0.13f, Slot(0, 2, 0), 180f);

            // 1 gloves: neoprene (green), latex (pale), cotton (cream), leather (brown)
            var tints = new[] { new Color(0.04f, 0.45f, 0.35f), new Color(0.86f, 0.80f, 0.70f), new Color(0.88f, 0.86f, 0.80f), new Color(0.40f, 0.24f, 0.12f) };
            var names = new[] { "Neoprene", "Latex", "Cotton", "Leather" };
            var slots = new[] { Slot(1, 0, 2), Slot(1, 1, 2), Slot(1, 2, 0), Slot(1, 2, 1) };
            for (var o = 0; o < 4; o++)
            {
                var gl = Sc12Part("Sc12Ppe.glb", "Glove", room);
                Sc12Tint(gl, "Sc12Glove" + names[o], tints[o], o == 3 ? 0.25f : 0.55f);
                Place(1, o, gl, 0.36f, slots[o], 90f);
            }

            // 2 respirators: acid-gas cartridge (full mask), dust mask, organic vapour (full mask, other tint), cloth mask
            var r0 = Sc12Part("Sc12Ppe.glb", "CartMask", room);
            Place(2, 0, r0, 0.26f, Slot(2, 1, 2), 180f);
            var r1 = Sc12Part("Sc12Ppe.glb", "HalfMask", room);
            Place(2, 1, r1, 0.20f, Slot(2, 0, 0), 180f);
            var r2 = Sc12Part("Sc12Ppe.glb", "CartMask", room);
            Place(2, 2, r2, 0.26f, Slot(2, 2, 2), 180f);
            Sc12Tint(r2, "Sc12MaskOrganic", new Color(0.55f, 0.52f, 0.46f), 0.4f);
            var r3 = Sc12Part("Sc12Ppe.glb", "HalfMask", room);
            Place(2, 3, r3, 0.20f, Slot(2, 0, 1), 180f);
            Sc12Tint(r3, "Sc12MaskCloth", new Color(0.35f, 0.5f, 0.75f), 0.15f);

            // 3 aprons and suits: acid-resistant apron (blue), antistatic apron (grey), chemical suit (white), coveralls (orange)
            // hanging from the rail (top at y 1.76): four hangers across the locker, the suits a little longer than the aprons
            float Hang(float h) => 1.76f - h;
            var hx = new[] { xz + xs[3] + 0.17f, xz + xs[3] + 0.10f, xz + xs[3] + 0.04f, xz + xs[3] - 0.03f };
            var a0 = Sc12Part("Sc12Ppe.glb", "Apron", room);
            Sc12Tint(a0, "Sc12ApronBlue", new Color(0.10f, 0.32f, 0.72f), 0.55f);
            Place(3, 0, a0, 0.78f, new Vector3(hx[0], Hang(0.78f), cz + 0.08f), 180f);
            var a1 = Sc12Part("Sc12Ppe.glb", "Apron", room);
            Sc12Tint(a1, "Sc12ApronGrey", new Color(0.55f, 0.56f, 0.55f), 0.2f);
            Place(3, 1, a1, 0.78f, new Vector3(hx[1], Hang(0.78f), cz + 0.05f), 180f);
            var a2 = Sc12Part("Sc12Ppe.glb", "HazmatSuit", room);
            Place(3, 2, a2, 0.92f, new Vector3(hx[2], Hang(0.92f), cz + 0.0f), 180f);
            var a3 = Sc12Part("Sc12Ppe.glb", "HazmatSuit", room);
            Sc12Tint(a3, "Sc12CoverallOrange", new Color(0.86f, 0.38f, 0.10f), 0.25f);
            Place(3, 3, a3, 0.92f, new Vector3(hx[3], Hang(0.92f), cz - 0.04f), 180f);
        }

        // boots and hard hats in the lower bays (decoration)
        private static void Sc12Floorgear(Transform room, float ox, float[] xs, float front)
        {
            var hat = Mat("Sc12HatYellow", new Color(0.95f, 0.72f, 0.05f), 0.6f, 0f);
            var bootCols = new[] { new Color(0.45f, 0.38f, 0.22f), new Color(0.06f, 0.06f, 0.07f), new Color(0.08f, 0.08f, 0.09f), new Color(0.09f, 0.10f, 0.11f) };
            for (var k = 0; k < 4; k++)
            {
                var x = ox + xs[k];
                for (var s = -1; s <= 1; s += 2)
                {
                    var boot = Sc12Part("Sc12Ppe.glb", "Boot", room);
                    boot.name = "Boot";
                    boot.transform.SetParent(room, true);
                    Sc12Tint(boot, "Sc12Boot" + k, bootCols[k], 0.5f);
                    boot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    FitHeight(boot, 0.30f);
                    var b = WorldBounds(boot);
                    boot.transform.position += new Vector3(x + s * 0.12f - 0.1f - b.center.x, 0.05f - b.min.y, front - 0.12f - b.center.z + 0.02f);
                }

                var helmet = Sc12Part("Sc12Ppe.glb", "Helmet", room);
                helmet.transform.SetParent(room, true);
                Sc12Tint(helmet, "Sc12Hat", hat.color, 0.6f);
                helmet.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                FitHeight(helmet, 0.12f);
                var hb = WorldBounds(helmet);
                helmet.transform.position += new Vector3(x + 0.18f - hb.center.x, 0.05f - hb.min.y, front + 0.1f - hb.center.z - 0.12f);
            }
        }

        // ---------------------------------------------------------------- left wall: the "Don PPE" sign and the SDS board
        private static void Sc12Signs(Transform room, float ox)
        {
            var lx = ox + Sc12HalfW - 0.025f;
            var signMat = Mat("Sc12SignMat", new Color(1.1f, 1.1f, 1.1f), 0.4f, 0.1f, Sc12Art("Sc12_Sign"));
            var frame = Mat("Sc12SignFrame", new Color(0.78f, 0.80f, 0.82f), 0.45f, 0.6f);
            Box("SignFrame", room, new Vector3(lx + 0.005f, 2.05f, -2.3f), new Vector3(0.03f, 0.84f, 2.34f), frame, false);
            Sc12Poster("Sign_DonPpe", room, new Vector3(lx - 0.012f, 2.05f, -2.3f), 90f, new Vector2(2.25f, 0.75f), signMat);
            var boardMat = Mat("Sc12SdsBoardMat", Color.white, 0.4f, 0.05f, Sc12Art("Sc12_SdsBoard"));
            var dark = Mat("Sc12SdsFrame", new Color(0.16f, 0.14f, 0.13f), 0.4f, 0.3f);
            Box("SdsFrame", room, new Vector3(lx + 0.005f, 1.2f, -0.55f), new Vector3(0.04f, 1.28f, 1.07f), dark, false);
            Sc12Poster("SdsBoard", room, new Vector3(lx - 0.018f, 1.2f, -0.55f), 90f, new Vector2(0.98f, 1.19f), boardMat);
        }

        // ---------------------------------------------------------------- outside the window: the process plant
        private static void Sc12Plant(Transform room, float ox, Material steelMat)
        {
            var plant = new GameObject("Plant").transform;
            plant.SetParent(room, false);
            var pavement = Sc12Tiled("Sc12PlantFloor", Sc12Tex("Sc12_Floor"), new Color(0.8f, 0.82f, 0.82f), 0.5f, 0.05f, new Vector2(14f, 8f));
            Box("PlantFloor", plant, new Vector3(ox - 18f, -0.06f, -8f), new Vector3(34f, 0.1f, 40f), pavement, false);
            var wallMat = Sc12Tiled("Sc12PlantWall", Sc12Tex("Sc12_Wall"), new Color(0.9f, 0.9f, 0.88f), 0.12f, 0f, new Vector2(14f, 3f));
            Box("PlantBackWall", plant, new Vector3(ox - 34.5f, 4f, -8f), new Vector3(0.4f, 8f, 40f), wallMat, false);
            Box("PlantSideWall", plant, new Vector3(ox - 18f, 4f, -28.5f), new Vector3(34f, 8f, 0.4f), wallMat, false);
            Box("PlantSideWallNear", plant, new Vector3(ox - 18f, 4f, 12.5f), new Vector3(34f, 8f, 0.4f), wallMat, false);
            Box("PlantCeiling", plant, new Vector3(ox - 18f, 8.1f, -8f), new Vector3(34f, 0.2f, 40f), wallMat, false);
            for (var i = 0; i < 3; i++)
            {
                var pl = new GameObject("PlantLight" + i).AddComponent<Light>();
                pl.transform.SetParent(plant, true);
                pl.transform.position = new Vector3(ox - 7f - i * 7f, 6f, -7f);
                pl.type = LightType.Point;
                pl.color = new Color(0.95f, 0.97f, 1f);
                pl.intensity = 5f;
                pl.range = 16f;
                pl.shadows = LightShadows.None;
            }

            var panelMat = Mat("Sc12PlantLamp", Color.white, 0.3f, 0f, null, new Color(2.2f, 2.2f, 2.2f));
            for (var i = 0; i < 6; i++)
                Box("PlantLamp" + i, plant, new Vector3(ox - 8f - i * 5f, 7.9f, -6f - (i % 2) * 8f), new Vector3(2.4f, 0.1f, 0.5f), panelMat, false);

            // tanks: the designer module (a vessel with its pipe stand) several times, fading into the distance
            var spots = new[]
            {
                new Vector3(ox - 6.5f, 0f, -3.5f), new Vector3(ox - 6.5f, 0f, -9.5f), new Vector3(ox - 12f, 0f, -2f),
                new Vector3(ox - 13f, 0f, -9f), new Vector3(ox - 19f, 0f, -5f), new Vector3(ox - 20f, 0f, -12f), new Vector3(ox - 26f, 0f, -8f)
            };
            for (var i = 0; i < spots.Length; i++)
            {
                var tank = Sc12Part("Sc12Plant.glb", "TankBig", plant);
                tank.name = "Tank" + i;
                tank.transform.rotation = Quaternion.Euler(0f, (i % 2 == 0) ? 90f : -90f, 0f);
                tank.transform.localScale = Vector3.one * (0.85f + 0.1f * (i % 3));
                tank.transform.position = spots[i];
            }

            var pw = Sc12Part("Sc12Plant.glb", "PipeWall", plant);
            pw.transform.position = new Vector3(ox - 32f, 0f, -8f);
            pw.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var cp = Sc12Part("Sc12Plant.glb", "ControlPanel", plant);
            cp.transform.position = new Vector3(ox - 9f, 0f, 6f);
            // yellow catwalk rail along the window side (step01)
            var rail = Mat("Sc12PlantRail", new Color(0.95f, 0.72f, 0.05f), 0.4f, 0.1f);
            for (var z = -22f; z <= 6f; z += 2f)
                Sc12Cyl("RailPost", plant, new Vector3(ox - 5.2f, 0.5f, z), 0.025f, 1.0f, rail, Vector3.zero);
            Sc12Cyl("RailTop", plant, new Vector3(ox - 5.2f, 1.0f, -8f), 0.025f, 28f, rail, new Vector3(90f, 0f, 0f));
            Sc12Cyl("RailMid", plant, new Vector3(ox - 5.2f, 0.5f, -8f), 0.02f, 28f, rail, new Vector3(90f, 0f, 0f));
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 7: factory hall with a burning electrical cabinet, a concrete pillar with the Water / CO2 / ABC extinguishers
    /// and stainless tanks, modelled on the Scene 07 workflow. The viewer stands facing -Z (the viewer's left is +X).
    /// Partition wall at z = -3, the pillar in front of it (x = ox+0.2..ox+2.2), the cabinet to the viewer's right of it.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc07Refs
        {
            public Transform spawn;
            public Transform standB;
            public Transform fireAt;
            public Transform cabinet;
            public Transform beacon;
            public readonly Transform[] ext = new Transform[3];
            public Transform room;
            public Sc07Human[] people;
            public Sc07Fire fire;
            public Sc07Beacon beaconFx;
            public Sc07Ambience ambience;
            public ExtinguisherInspect inspect;
            public Sc06Gate gate;
            public Sc07Crowd crowd;
            public Transform heroStart;
            public Sc07Door fleeDoor;
        }

        private const float Sc07Ox = 240f;
        private const string Sc07TexDir = ArtRoot + "/2D/Sc07Tex";

        private static Texture2D Sc07Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc07TexDir + "/" + name + ".png");

        private static void ConfigureSc07Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc07TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = Path.GetFileNameWithoutExtension(path);
                var tile = file == "HazardStripe";
                var changed = false;
                if (ti.textureType != TextureImporterType.Default)
                {
                    ti.textureType = TextureImporterType.Default;
                    changed = true;
                }

                var wrap = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var decal = !file.StartsWith("Worker_") && !tile;
                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 || ti.alphaIsTransparency != (decal || file.StartsWith("Worker_Lash") || file.StartsWith("Worker_Eye")))
                {
                    ti.wrapMode = wrap;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = decal || file.StartsWith("Worker_Lash") || file.StartsWith("Worker_Eye");
                    changed = true;
                }

                if (changed)
                    ti.SaveAndReimport();
            }
        }

        // Instantiates a Sc07 glb as a plain (unpacked) object so its nodes can be re-parented.
        private static GameObject SpawnSc07(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc07/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc07 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        // Copies of the imported materials with a capped metallic value (metals render black in a hall without reflections).
        private static void Sc07Matte(GameObject root, float maxMetal, float maxSmooth)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null)
                        continue;
                    var key = "Sc07_" + src.name.Replace(" ", "_").Replace("#", "") + "_" + (int)(maxMetal * 100);
                    if (!Mats.TryGetValue(key, out var copy))
                    {
                        var path = GenDir + "/M_" + key + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (copy == null)
                        {
                            copy = new Material(src);
                            AssetDatabase.CreateAsset(copy, path);
                        }
                        else
                        {
                            copy.CopyPropertiesFromMaterial(src);
                        }

                        if (copy.HasProperty("_Metallic"))
                            copy.SetFloat("_Metallic", Mathf.Min(copy.GetFloat("_Metallic"), maxMetal));
                        if (copy.HasProperty("_Smoothness"))
                            copy.SetFloat("_Smoothness", Mathf.Min(copy.GetFloat("_Smoothness"), maxSmooth));
                        EditorUtility.SetDirty(copy);
                        Mats[key] = copy;
                    }

                    mats[i] = copy;
                    changed = true;
                }

                if (changed)
                    r.sharedMaterials = mats;
            }
        }

        private static Sc07Refs BuildSc07Room(Transform env, PresentableArt art)
        {
            const float ox = Sc07Ox;
            var refs = new Sc07Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            refs.room = room;
            var solidLayer = SolidLayer;
            ConfigureSc05Textures();
            ConfigureSc07Textures();

            // hazy, brighter industrial air than the dyeing factory: pale grey-green haze, soft ambient
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.80f, 0.83f, 0.81f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.64f, 0.66f, 0.64f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.34f, 0.34f, 0.33f);
            mso.FindProperty("fogColor").colorValue = new Color(0.56f, 0.60f, 0.58f);
            mso.FindProperty("fogStart").floatValue = 8f;
            mso.FindProperty("fogEnd").floatValue = 42f;
            mso.FindProperty("sunIntensity").floatValue = 0.05f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.3f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            const float zBack = -12f;
            const float zFront = 6f;
            const float hallH = 5.6f;
            var x0 = ox - 19f;
            var x1 = ox + 6.5f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (zBack + zFront) * 0.5f;
            var w = x1 - x0;
            var d = zFront - zBack;
            const float wallZ = -3.0f;       // partition wall (front face at wallZ + 0.2)
            const float wallFace = wallZ + 0.2f;
            const float pillarFace = -1.8f;

            var floorMat = Sc05Pbr("Sc07FloorPbr", "Floor07", new Color(1f, 1f, 1f), new Vector2(w / 2.4f, d / 2.4f), 0.75f, 0.9f);
            var wallMat = Sc05Pbr("Sc07WallPbr", "Wall07", new Color(1f, 1f, 1f), new Vector2(w / 2.4f, 2.2f), 0.55f, 1.2f);
            var wallSideMat = Sc05Pbr("Sc07WallSidePbr", "Wall07", new Color(0.96f, 0.96f, 0.95f), new Vector2(d / 2.4f, 2.2f), 0.55f, 1.2f);
            var partMat = Sc05Pbr("Sc07PartitionPbr", "Wall07", new Color(1f, 1f, 1f), new Vector2(2.8f, 2.3f), 0.55f, 1.3f);
            var pillarMat = Sc05Pbr("Sc07PillarPbr", "Wall07", new Color(1f, 1f, 1f), new Vector2(0.9f, 2.3f), 0.5f, 1.4f);
            var ceilMat = Sc05Pbr("Sc07CeilPbr", "Ceil07", new Color(0.8f, 0.82f, 0.8f), new Vector2(w / 2.4f, d / 2.4f), 1f, 0.8f, false);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solidLayer >= 0)
                    go.layer = solidLayer;
            }

            var floor = Box("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(cx, hallH * 0.5f, zBack), new Vector3(w, hallH, 0.15f), wallMat);
            Shell("Wall_Front", new Vector3(cx, hallH * 0.5f, zFront), new Vector3(w, hallH, 0.15f), wallMat);
            Shell("Wall_Left", new Vector3(x1, hallH * 0.5f, cz), new Vector3(0.15f, hallH, d), wallSideMat);
            Shell("Wall_Right", new Vector3(x0, hallH * 0.5f, cz), new Vector3(0.15f, hallH, d), wallSideMat);
            Box("Ceiling", room, new Vector3(cx, hallH, cz), new Vector3(w, 0.1f, d), ceilMat, false);

            // partition wall the cabinet and the pillar stand on (ends at ox-3.4, the hall opens to the viewer's right)
            var partX0 = ox - 3.4f;
            Shell("Partition", new Vector3((partX0 + x1) * 0.5f, hallH * 0.5f, wallZ), new Vector3(x1 - partX0, hallH, 0.4f), partMat);
            Shell("PartitionEndColumn", new Vector3(partX0 - 0.1f, hallH * 0.5f, wallZ), new Vector3(0.5f, hallH, 0.7f), pillarMat);
            var skirt = Mat("Sc07Skirt", new Color(0.13f, 0.13f, 0.14f), 0.2f, 0.1f);
            Box("PartitionSkirt", room, new Vector3((partX0 + x1) * 0.5f, 0.12f, wallFace + 0.01f), new Vector3(x1 - partX0, 0.24f, 0.03f), skirt, false);

            // concrete pillar with the extinguishers (workflow step 04): x ox+0.2..ox+2.2, front face z = -1.8
            var pillarCx = ox + 1.2f;
            Shell("Pillar", new Vector3(pillarCx, hallH * 0.5f, (wallFace + pillarFace) * 0.5f), new Vector3(2.0f, hallH, pillarFace - wallFace), pillarMat);
            Box("PillarFooting", room, new Vector3(pillarCx, 0.1f, pillarFace + 0.02f), new Vector3(2.06f, 0.2f, 0.06f), skirt, false);
            var steel = Mat("Sc07Steel", new Color(0.42f, 0.44f, 0.46f), 0.4f, 0.25f);
            var darkSteel = Mat("Sc07DarkSteel", new Color(0.18f, 0.19f, 0.20f), 0.35f, 0.3f);
            var hoseRed = Mat("Sc07HoseRed", new Color(0.55f, 0.07f, 0.06f), 0.35f, 0.05f);
            var wheelMat = Mat("Sc07ReelWheel", new Color(0.38f, 0.12f, 0.09f), 0.3f, 0.35f);
            Box("PillarRailHigh", room, new Vector3(pillarCx, 2.7f, pillarFace + 0.02f), new Vector3(2.04f, 0.05f, 0.04f), steel, false);

            // hose reels on both sides of the pillar (coil of red hose, dark centre, steel hub), a big dark wheel above it
            for (var i = 0; i < 2; i++)
            {
                var rx = i == 0 ? ox + 2.95f : ox - 0.35f;
                var ry = 0.85f;
                var rz = wallFace + 0.2f;
                var coil = Cyl("HoseReel" + i, room, new Vector3(rx, ry, rz), 0.18f, 0.54f, hoseRed);
                coil.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                coil.transform.position = new Vector3(rx, ry, rz);
                var ring = Cyl("HoseReelRing" + i, room, new Vector3(rx, ry, rz), 0.2f, 0.36f, Mat("Sc07HoseDark", new Color(0.30f, 0.04f, 0.04f), 0.3f, 0.05f));
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.position = new Vector3(rx, ry, rz + 0.01f);
                var hub = Cyl("HoseReelHub" + i, room, new Vector3(rx, ry, rz), 0.22f, 0.14f, steel);
                hub.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                hub.transform.position = new Vector3(rx, ry, rz + 0.02f);
            }

            var wheel = Cyl("ReelWheel", room, new Vector3(pillarCx, 4.55f, pillarFace + 0.04f), 0.07f, 0.55f, wheelMat);
            wheel.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            wheel.transform.position = new Vector3(pillarCx, 4.6f, pillarFace + 0.07f);
            Cyl("ReelWheelHub", room, new Vector3(pillarCx, 4.5f, pillarFace + 0.02f), 0.1f, 0.1f, steel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // ---- the three extinguishers (Extinguishers.glb: Water, CO2, ABC, label side = +X in the file)
            var ex = SpawnSc07("Extinguishers.glb", room);
            if (ex != null)
            {
                ex.name = "ExtinguishersSrc";
                Sc07Matte(ex, 0.25f, 0.55f);
                var nodes = new List<Transform>();
                foreach (var r in ex.GetComponentsInChildren<MeshRenderer>(true))
                    nodes.Add(r.transform);
                nodes.Sort((a, b) => a.GetComponent<Renderer>().bounds.center.z.CompareTo(b.GetComponent<Renderer>().bounds.center.z));
                var names = new[] { "Water", "CO2", "ABC" };
                // from the player's left (+X) to the right (-X): Water, CO2, ABC
                var xs = new[] { pillarCx + 0.58f, pillarCx, pillarCx - 0.58f };
                for (var i = 0; i < 3 && i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    var group = new GameObject("Ext_" + names[i]).transform;
                    group.SetParent(room, true);
                    group.position = new Vector3(xs[i], 1.32f, pillarFace + 0.20f);
                    group.rotation = Quaternion.identity; // local +Z = world +Z, the side that faces the player
                    n.SetParent(group, true);
                    // the label faces +X in the file: turn it to +Z (yaw -90), then scale 2.0 -> 0.74 m tall
                    n.rotation = Quaternion.Euler(0f, -90f, 0f) * n.rotation;
                    n.localScale *= 0.37f;
                    var nb = BoundsOfTree(n);
                    n.position += group.position - nb.center;
                    nb = BoundsOfTree(group);
                    // back of the cylinder 2 cm in front of the pillar (the horn and hose stick out toward the player)
                    group.position += new Vector3(0f, 0f, pillarFace + 0.03f + 0.09f - group.position.z);
                    refs.ext[i] = group;
                    // wall bracket straps
                    Box("Strap" + names[i] + "A", room, new Vector3(xs[i], group.position.y + 0.12f, pillarFace + 0.035f), new Vector3(0.13f, 0.035f, 0.04f), darkSteel, false);
                    Box("Strap" + names[i] + "B", room, new Vector3(xs[i], group.position.y - 0.12f, pillarFace + 0.035f), new Vector3(0.13f, 0.035f, 0.04f), darkSteel, false);
                    var col = group.gameObject.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    nb = BoundsOfTree(group);
                    // padded toward the player and upward so the fingertip reaches it before the pillar stops it
                    col.center = group.InverseTransformPoint(nb.center + new Vector3(0f, 0.03f, 0.12f));
                    col.size = nb.size + new Vector3(0.14f, 0.14f, 0.30f);
                    var poke = group.gameObject.AddComponent<PokeTarget>();
                    var pso = new SerializedObject(poke);
                    pso.FindProperty("reach").floatValue = 0.07f;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }

                Object.DestroyImmediate(ex.GetComponent<Collider>());
                if (ex.transform.childCount == 0)
                    Object.DestroyImmediate(ex);
            }

            // ---- the burning cabinet: ElectricPanel.glb, hazard stripes, signs, the red beacon above it
            var cabX = ox - 1.65f;
            var cabFrontZ = wallFace + 0.9f;
            var panel = SpawnSc07("ElectricPanel.glb", room);
            var panelMat = Mat("Sc07PanelGrey", new Color(0.50f, 0.52f, 0.52f), 0.35f, 0.25f);
            if (panel != null)
            {
                panel.name = "ElectricCabinet";
                Sc07Matte(panel, 0.1f, 0.25f);
                FitHeight(panel, 2.35f);
                var pb = BoundsOfTree(panel.transform);
                // the front (signs) faces +Z in the file; the back against the partition wall
                panel.transform.position += new Vector3(cabX - pb.center.x, -pb.min.y + 0.05f, (wallFace + 0.02f) - pb.min.z);
                pb = BoundsOfTree(panel.transform);
                refs.cabinet = panel.transform;
                cabFrontZ = pb.max.z;
                var hs = Mat("Sc07Hazard", Color.white, 0.25f, 0f, Sc07Tex("HazardStripe"), null, false);
                hs.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                var zf = pb.max.z + 0.006f;
                var stripe = 0.1f;
                Box("HazardTop", room, new Vector3(pb.center.x, pb.max.y - stripe * 0.5f, zf), new Vector3(pb.size.x, stripe, 0.012f), hs, false);
                Box("HazardBottom", room, new Vector3(pb.center.x, pb.min.y + stripe * 0.5f, zf), new Vector3(pb.size.x, stripe, 0.012f), hs, false);
                Box("HazardLeft", room, new Vector3(pb.min.x + stripe * 0.5f, pb.center.y, zf), new Vector3(stripe, pb.size.y, 0.012f), hs, false);
                Box("HazardRight", room, new Vector3(pb.max.x - stripe * 0.5f, pb.center.y, zf), new Vector3(stripe, pb.size.y, 0.012f), hs, false);
                Box("HazardMid", room, new Vector3(pb.center.x, pb.center.y, zf), new Vector3(stripe, pb.size.y, 0.012f), hs, false);
                var plinth = Box("CabinetPlinth", room, new Vector3(pb.center.x, 0.05f, (pb.min.z + pb.max.z) * 0.5f), new Vector3(pb.size.x + 0.1f, 0.1f, pb.size.z + 0.08f), skirt, false);
                plinth.layer = solidLayer >= 0 ? solidLayer : plinth.layer;
            }

            var signE = Mat("Sc07SignElectrical", Color.white, 0.3f, 0f, Sc07Tex("Sign_Electrical"), null, false, true);
            var signF = Mat("Sc07SignFlammable", Color.white, 0.3f, 0f, Sc07Tex("Sign_Flammable"), null, false, true);
            QuadObj("SignE0", room, new Vector3(ox - 2.2f, 1.75f, wallFace + 0.5f), new Vector3(0f, 180f, 0f), new Vector2(0.24f, 0.24f), signE);
            QuadObj("SignE1", room, new Vector3(ox - 1.1f, 1.75f, wallFace + 0.5f), new Vector3(0f, 180f, 0f), new Vector2(0.24f, 0.24f), signE);
            QuadObj("SignF0", room, new Vector3(ox - 2.2f, 1.3f, wallFace + 0.5f), new Vector3(0f, 180f, 0f), new Vector2(0.2f, 0.2f), signF);

            // fire alarm beacon above the cabinet
            var beaconAt = new Vector3(cabX, 3.35f, wallFace + 0.12f);
            var alarm = SpawnSc07("FireAlarm.glb", room);
            if (alarm != null)
            {
                alarm.name = "FireAlarmBeacon";
                FitHeight(alarm, 0.30f);
                var ab = BoundsOfTree(alarm.transform);
                alarm.transform.position += new Vector3(beaconAt.x - ab.center.x, beaconAt.y - ab.center.y, wallFace + 0.02f - ab.min.z);
                refs.beacon = alarm.transform;
            }

            refs.beaconFx = Sc07BuildBeacon(room, refs.beacon, beaconAt, art);

            // small breaker box between the pillar and the cabinet
            Box("BreakerBox", room, new Vector3(ox - 0.55f + 0.0f, 1.7f, wallFace + 0.05f), new Vector3(0.42f, 0.52f, 0.1f),
                Mat("Sc07BreakerBody", new Color(0.46f, 0.48f, 0.49f), 0.3f, 0.25f), false);
            QuadObj("BreakerFace", room, new Vector3(ox - 0.55f, 1.7f, wallFace + 0.105f), new Vector3(0f, 180f, 0f), new Vector2(0.38f, 0.48f),
                Mat("Sc07BreakerFace", Color.white, 0.3f, 0f, Sc07Tex("BreakerPanel"), null, false));

            // conduit pipes down the wall
            Cyl("Conduit0", room, new Vector3(ox - 3.2f, 0f, wallFace + 0.05f), hallH, 0.03f, steel);
            Cyl("Conduit1", room, new Vector3(ox + 3.7f, 0f, wallFace + 0.05f), hallH, 0.04f, steel);

            // ---- floor markings: yellow lines around the pillar zone, hazard stripes at the cabinet, oil stains
            var line = Mat("Sc07YellowLine", Color.white, 0.2f, 0f, Sc07Tex("YellowLine"), null, true, true);
            line.SetTexture("_BaseMap", Sc07Tex("YellowLine"));
            void FloorLine(string name, Vector3 a, Vector3 b)
            {
                var mid = (a + b) * 0.5f;
                var len = Vector3.Distance(a, b);
                var q = QuadObj(name, room, new Vector3(mid.x, 0.006f, mid.z), new Vector3(90f, Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg + 90f, 0f),
                    new Vector2(len, 0.16f), line);
                q.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg - 90f, 0f);
            }

            FloorLine("LineA", new Vector3(ox - 3.4f, 0f, -0.6f), new Vector3(ox + 4.6f, 0f, -0.6f));
            FloorLine("LineB", new Vector3(ox + 4.6f, 0f, -0.6f), new Vector3(ox + 4.6f, 0f, -2.7f));
            FloorLine("LineC", new Vector3(ox - 3.4f, 0f, -0.6f), new Vector3(ox - 3.4f, 0f, -2.7f));
            FloorLine("LaneEdgeA", new Vector3(ox - 3.7f, 0f, zBack + 0.6f), new Vector3(ox - 3.7f, 0f, 5.2f));
            FloorLine("LaneEdgeB", new Vector3(ox - 9.4f, 0f, zBack + 0.6f), new Vector3(ox - 9.4f, 0f, 5.2f));
            var oilMat = MatAlpha("Sc07Oil", Sc07Tex("OilStain"), new Color(1f, 1f, 1f, 0.9f), false);
            QuadObj("Oil0", room, new Vector3(ox + 1.0f, 0.008f, -0.9f), new Vector3(90f, 0f, 0f), new Vector2(1.5f, 0.9f), oilMat);
            QuadObj("Oil1", room, new Vector3(ox - 5.5f, 0.008f, 0.4f), new Vector3(90f, 30f, 0f), new Vector2(1.8f, 1.2f), oilMat);
            QuadObj("Oil2", room, new Vector3(ox + 3.4f, 0.008f, 2.4f), new Vector3(90f, 80f, 0f), new Vector2(1.2f, 0.8f), oilMat);

            // ---- workshop on the viewer's left: blue steel shelving with boxes and tools, a bench (workflow step 04)
            var blue = Mat("Sc07ShelfBlue", new Color(0.12f, 0.30f, 0.62f), 0.4f, 0.3f);
            var gridGrey = Mat("Sc07ShelfGrey", new Color(0.60f, 0.62f, 0.63f), 0.35f, 0.3f);
            var cardboard = Mat("Sc07Cardboard", new Color(0.55f, 0.42f, 0.27f), 0.1f, 0f);
            var toolRed = Mat("Sc07ToolRed", new Color(0.65f, 0.1f, 0.08f), 0.4f, 0.1f);
            var shelfX = ox + 5.2f;
            for (var s = 0; s < 3; s++)
            {
                var sx = shelfX + s * 1.4f;
                if (sx > x1 - 0.5f)
                    break;
                for (var u = 0; u < 2; u++)
                    Box("ShelfPost" + s + u, room, new Vector3(sx + (u == 0 ? -0.6f : 0.6f), 1.0f, wallFace + 0.35f), new Vector3(0.05f, 2.0f, 0.05f), blue, false);
                for (var lvl = 0; lvl < 4; lvl++)
                {
                    var y = 0.15f + lvl * 0.62f;
                    Box("Shelf" + s + lvl, room, new Vector3(sx, y, wallFace + 0.38f), new Vector3(1.25f, 0.03f, 0.5f), gridGrey, false);
                    for (var b = 0; b < 3; b++)
                    {
                        if ((s + lvl + b) % 2 == 0)
                            Box("Crate" + s + lvl + b, room, new Vector3(sx - 0.38f + b * 0.38f, y + 0.12f, wallFace + 0.38f), new Vector3(0.28f, 0.2f, 0.34f), (s + b) % 3 == 0 ? toolRed : cardboard, false);
                    }
                }
            }

            // ---- the machine hall: stainless tanks and pipe walls (Factory02.glb), scaled to a believable size
            var tankMat = Mat("Sc07Tank", new Color(0.66f, 0.68f, 0.69f), 0.55f, 0.25f);
            var pipeMat = Mat("Sc07Pipe", new Color(0.72f, 0.74f, 0.75f), 0.6f, 0.2f);
            // two stainless machine blocks on the viewer's right; the walking lane (x ox-9.4 .. ox-3.7) runs between them and the fire
            for (var mi = 0; mi < 2; mi++)
            {
                var factory = SpawnSc07("Factory.glb", room);
                if (factory == null)
                    continue;
                factory.name = mi == 0 ? "Machines" : "Machines2";
                factory.transform.localScale = Vector3.one * (mi == 0 ? 0.27f : 0.24f);
                factory.transform.rotation = Quaternion.Euler(0f, mi == 0 ? 0f : 180f, 0f); // the long row runs along the lane
                var fb0 = BoundsOfTree(factory.transform);
                var fit = Mathf.Min(1f, 15.5f / fb0.size.z, 6.5f / fb0.size.x);
                factory.transform.localScale *= fit;
                var fb = BoundsOfTree(factory.transform);
                var edge = ox - (mi == 0 ? 9.9f : 15.4f);
                var zc = mi == 0 ? -4.4f : -3.0f;
                factory.transform.position += new Vector3(edge - fb.max.x, -fb.min.y, zc - fb.center.z);
                foreach (var r in factory.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var mn = mats[i] != null ? mats[i].name : "";
                        if (mats[i] == null)
                            mats[i] = tankMat; // a mesh without a material renders magenta
                        else if (mn.Contains("Stainless") || mn.Contains("metal"))
                            mats[i] = mn.Contains("PipeEngine") ? pipeMat : tankMat;
                        else if (mn.Contains("ControlPanel") || mn.Contains("Pano"))
                            mats[i] = panelMat;
                    }

                    r.sharedMaterials = mats;
                }

                Sc07Matte(factory, 0.3f, 0.6f);
            }

            // big horizontal boilers along the lane (workflow step 01/02: round stainless vessels on saddles with a hatch and a gauge)
            var hatchMat = Mat("Sc07BoilerHatch", new Color(0.60f, 0.62f, 0.64f), 0.5f, 0.3f);
            var bandMat = Mat("Sc07BoilerBand", new Color(0.50f, 0.52f, 0.54f), 0.35f, 0.3f);
            var saddleMat = Mat("Sc07BoilerSaddle", new Color(0.32f, 0.35f, 0.38f), 0.3f, 0.2f);
            var gaugeFace = Mat("Sc07GaugeFace", new Color(0.92f, 0.93f, 0.9f), 0.3f, 0f);
            var boilerZs = new[] { -8.6f, -4.4f, -0.2f };
            for (var bi = 0; bi < boilerZs.Length; bi++)
            {
                var bc = new Vector3(ox - 11.2f, 1.45f, boilerZs[bi]);
                var brad = 1.2f;
                var blen = 3.3f;
                var shell = Cyl("Boiler" + bi, room, bc, blen, brad, tankMat);
                shell.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                shell.transform.position = bc;
                foreach (var sgn in new[] { -1f, 1f })
                {
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = "BoilerCap" + bi;
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    cap.transform.SetParent(room, true);
                    cap.transform.position = bc + new Vector3(0f, 0f, sgn * blen * 0.5f);
                    cap.transform.localScale = new Vector3(brad * 2f, brad * 2f, brad * 0.8f);
                    cap.GetComponent<MeshRenderer>().sharedMaterial = tankMat;
                    // two saddles (steel cradles) under the shell
                    Box("BoilerSaddle" + bi, room, bc + new Vector3(0f, -1.0f, sgn * 1.05f), new Vector3(1.5f, 0.46f, 0.12f), saddleMat, false);
                    Box("BoilerBase" + bi, room, new Vector3(bc.x, 0.09f, bc.z + sgn * 1.05f), new Vector3(1.8f, 0.18f, 0.3f), saddleMat, false);
                }

                // welded seams: thin raised bands around the shell
                foreach (var bz in new[] { -1.2f, -0.4f, 0.4f, 1.2f })
                {
                    var band = Cyl("BoilerBand" + bi, room, bc, 0.07f, brad + 0.015f, bandMat);
                    band.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    band.transform.position = bc + new Vector3(0f, 0f, bz);
                }

                // manway: a flanged ring and a plate facing the lane (+X), bolts around it
                var flange = Cyl("BoilerFlange" + bi, room, bc, 0.06f, 0.40f, hatchMat);
                flange.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                flange.transform.position = bc + new Vector3(brad - 0.01f, 0.15f, 0.15f);
                var plate = Cyl("BoilerPlate" + bi, room, bc, 0.05f, 0.31f, bandMat);
                plate.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                plate.transform.position = bc + new Vector3(brad + 0.04f, 0.15f, 0.15f);
                for (var bo = 0; bo < 8; bo++)
                {
                    var ang = bo * Mathf.PI * 0.25f;
                    var bolt = Cyl("BoilerBolt" + bi, room, bc, 0.05f, 0.022f, darkSteel);
                    bolt.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    bolt.transform.position = bc + new Vector3(brad + 0.045f, 0.15f + Mathf.Sin(ang) * 0.35f, 0.15f + Mathf.Cos(ang) * 0.35f);
                }

                // pressure gauge: white dial in a steel rim on a short stub
                var rim = Cyl("BoilerGaugeRim" + bi, room, bc, 0.05f, 0.11f, hatchMat);
                rim.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                rim.transform.position = bc + new Vector3(brad + 0.1f, 0.75f, -0.9f);
                var dial = Cyl("BoilerGaugeDial" + bi, room, bc, 0.052f, 0.09f, gaugeFace);
                dial.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                dial.transform.position = bc + new Vector3(brad + 0.105f, 0.75f, -0.9f);
                Cyl("BoilerGaugeStub" + bi, room, bc + new Vector3(brad - 0.05f, 0.75f, -0.9f), 0.01f, 0.025f, hatchMat);
                // feed pipe up through the top with an elbow toward the ceiling duct
                var feedPipe = Cyl("BoilerFeed" + bi, room, bc, 1.2f, 0.07f, pipeMat);
                feedPipe.transform.position = bc + new Vector3(0.2f, brad + 0.55f, 0.6f);
                var elbow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                elbow.name = "BoilerElbow" + bi;
                Object.DestroyImmediate(elbow.GetComponent<Collider>());
                elbow.transform.SetParent(room, true);
                elbow.transform.position = bc + new Vector3(0.2f, brad + 1.15f, 0.6f);
                elbow.transform.localScale = Vector3.one * 0.17f;
                elbow.GetComponent<MeshRenderer>().sharedMaterial = pipeMat;
                Box("BoilerPanel" + bi, room, new Vector3(ox - 9.9f, 0.9f, boilerZs[bi] - 1.9f), new Vector3(0.12f, 1.2f, 0.5f), panelMat, false);
            }

            // a steel trolley beside the cabinet (workflow step 02)

            Box("TrolleyTop", room, new Vector3(ox - 4.1f, 0.82f, -1.6f), new Vector3(0.9f, 0.04f, 0.55f), steel, false);
            Box("TrolleyShelf", room, new Vector3(ox - 4.1f, 0.2f, -1.6f), new Vector3(0.84f, 0.03f, 0.5f), steel, false);
            foreach (var tx in new[] { -0.4f, 0.4f })
                foreach (var tz in new[] { -0.22f, 0.22f })
                    Box("TrolleyLeg", room, new Vector3(ox - 4.1f + tx, 0.42f, -1.6f + tz), new Vector3(0.03f, 0.8f, 0.03f), darkSteel, false);
            // more equipment so the hall is not empty: stainless process tanks on legs, control cabinets, drums by the back wall
            var tankSpots = new[] { new Vector3(ox - 11.6f, 0f, 3.4f), new Vector3(ox - 13.6f, 0f, 4.3f), new Vector3(ox - 16.6f, 0f, 3.2f) };
            for (var ti = 0; ti < tankSpots.Length; ti++)
            {
                var tp = tankSpots[ti];
                var rad = 0.62f + 0.14f * (ti % 2);
                var body = Cyl("ProcTank" + ti, room, tp + Vector3.up * 0.55f, 1.9f + 0.3f * (ti % 3), rad, tankMat);
                body.transform.localScale = new Vector3(rad * 2f, body.transform.localScale.y, rad * 2f);
                var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                top.name = "ProcTankDome" + ti;
                Object.DestroyImmediate(top.GetComponent<Collider>());
                top.transform.SetParent(room, true);
                top.transform.position = tp + Vector3.up * (0.55f + 1.9f + 0.3f * (ti % 3));
                top.transform.localScale = new Vector3(rad * 2f, rad * 0.9f, rad * 2f);
                top.GetComponent<MeshRenderer>().sharedMaterial = tankMat;
                foreach (var lx in new[] { -1f, 1f })
                    foreach (var lz in new[] { -1f, 1f })
                        Box("ProcTankLeg", room, tp + new Vector3(lx * rad * 0.72f, 0.28f, lz * rad * 0.72f), new Vector3(0.07f, 0.56f, 0.07f), darkSteel, false);
                var feed = Cyl("ProcTankPipe" + ti, room, tp + new Vector3(rad * 0.9f, 0f, 0f), 3.4f, 0.06f, pipeMat);
                feed.transform.position = tp + new Vector3(rad * 0.9f, 1.7f, 0f);
                Box("ProcTankCabinet" + ti, room, tp + new Vector3(-rad - 0.55f, 0.75f, 0.1f), new Vector3(0.5f, 1.5f, 0.35f), panelMat, false);
                Box("ProcTankScreen" + ti, room, tp + new Vector3(-rad - 0.55f, 1.15f, 0.29f), new Vector3(0.3f, 0.2f, 0.01f), Mat("Sc07Screen", new Color(0.1f, 0.5f, 0.35f), 0f, 0f, null, new Color(0.15f, 0.8f, 0.5f), true), false);
            }

            var drumBlue = Mat("Sc07DrumBlue", new Color(0.12f, 0.28f, 0.6f), 0.4f, 0.2f);
            var drumYellow = Mat("Sc07DrumYellow", new Color(0.85f, 0.65f, 0.08f), 0.4f, 0.2f);
            for (var di = 0; di < 6; di++)
                Cyl("Drum" + di, room, new Vector3(ox - 9.0f + (di % 3) * 0.6f, 0f, -11.1f + (di / 3) * 0.6f), 0.9f, 0.27f, di % 2 == 0 ? drumBlue : drumYellow);
            Box("TrolleyHandle", room, new Vector3(ox - 4.55f, 1.0f, -1.6f), new Vector3(0.03f, 0.4f, 0.5f), darkSteel, false);

            // ---- the FIRE EXIT door straight ahead at the far end of the lane (back wall): a hinged green steel door under a big lit
            // sign, daylight behind it. The first runner opens it (Sc07Door), everyone flees through it along the lane.
            var fleeX = ox - 7.6f;
            const float doorW = 1.5f;
            const float doorH = 2.3f;
            var doorMat = Mat("Sc07FireDoor", new Color(0.26f, 0.50f, 0.32f), 0.35f, 0.2f);
            var barMat = Mat("Sc07PushBar", new Color(0.85f, 0.86f, 0.88f), 0.7f, 0.5f);
            var frameMat = Mat("Sc07DoorFrame", new Color(0.18f, 0.19f, 0.2f), 0.3f, 0.3f);
            var fleeRoot = new GameObject("FleeDoor").transform;
            fleeRoot.SetParent(room, false);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(fleeRoot, false);
            hinge.position = new Vector3(fleeX + doorW * 0.5f, 0f, zBack + 0.12f); // hinge on the right-hand edge, the leaf extends toward -X and swings into the hall
            Box("DoorLeaf", hinge, new Vector3(fleeX, doorH * 0.5f, zBack + 0.12f), new Vector3(doorW, doorH, 0.07f), doorMat, false);
            Box("DoorBar", hinge, new Vector3(fleeX, 1.05f, zBack + 0.18f), new Vector3(doorW * 0.75f, 0.05f, 0.03f), barMat, false);
            Box("DoorFrameL", fleeRoot, new Vector3(fleeX - doorW * 0.5f - 0.06f, doorH * 0.5f + 0.04f, zBack + 0.1f), new Vector3(0.12f, doorH + 0.1f, 0.14f), frameMat, false);
            Box("DoorFrameR", fleeRoot, new Vector3(fleeX + doorW * 0.5f + 0.06f, doorH * 0.5f + 0.04f, zBack + 0.1f), new Vector3(0.12f, doorH + 0.1f, 0.14f), frameMat, false);
            Box("DoorFrameT", fleeRoot, new Vector3(fleeX, doorH + 0.09f, zBack + 0.1f), new Vector3(doorW + 0.24f, 0.12f, 0.14f), frameMat, false);
            // daylight behind the open door: a bright panel in the wall plane, a white light that is only on while the door is open
            var daylightMat = Mat("Sc07Daylight", new Color(1f, 1f, 0.95f), 0f, 0f, null, new Color(2.4f, 2.4f, 2.2f), true);
            QuadObj("DoorDaylight", fleeRoot, new Vector3(fleeX, doorH * 0.5f, zBack + 0.083f), new Vector3(0f, 180f, 0f), new Vector2(doorW, doorH), daylightMat);
            var dayGo = new GameObject("DoorDaylightLight");
            dayGo.transform.SetParent(fleeRoot, false);
            dayGo.transform.position = new Vector3(fleeX, 1.8f, zBack + 1.3f);
            var dayLight = dayGo.AddComponent<Light>();
            dayLight.type = LightType.Point;
            dayLight.color = new Color(1f, 0.98f, 0.9f);
            dayLight.range = 9f;
            dayLight.intensity = 0f;
            dayLight.shadows = LightShadows.None;
            // the green "FIRE EXIT" board over the door: big (2.6 x 0.975 m), emissive, with a thin dark backing
            Box("FireExitBacking", fleeRoot, new Vector3(fleeX, 3.55f, zBack + 0.13f), new Vector3(2.72f, 1.08f, 0.06f), frameMat, false);
            QuadObj("FireExitSign", fleeRoot, new Vector3(fleeX, 3.55f, zBack + 0.162f), new Vector3(0f, 180f, 0f), new Vector2(2.6f, 0.975f),
                Mat("Sc07FireExitSign", Color.white, 0f, 0f, Sc07Tex("FireExitSign"), new Color(0.85f, 1.0f, 0.88f), true));
            var fleeGlow = new GameObject("FireExitGlow");
            fleeGlow.transform.SetParent(fleeRoot, false);
            fleeGlow.transform.position = new Vector3(fleeX, 3.3f, zBack + 1.4f);
            var fgl = fleeGlow.AddComponent<Light>();
            fgl.type = LightType.Point;
            fgl.color = new Color(0.4f, 1f, 0.55f);
            fgl.range = 6f;
            fgl.intensity = 2.2f;
            fgl.shadows = LightShadows.None;
            var fleeDoorComp = fleeRoot.gameObject.AddComponent<Sc07Door>();
            var fdo = new SerializedObject(fleeDoorComp);
            fdo.FindProperty("leaf").objectReferenceValue = hinge;
            fdo.FindProperty("daylight").objectReferenceValue = dayLight;
            fdo.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc07/Door_Open.wav");
            fdo.ApplyModifiedPropertiesWithoutUndo();
            refs.fleeDoor = fleeDoorComp;
            // green guide arrows on the floor along the lane toward the exit
            var arrowFloor = MatAlpha("Sc07FloorArrow", Sc07Tex("FloorArrow"), new Color(1f, 1f, 1f, 0.92f), false);
            for (var ai = 0; ai < 3; ai++)
            {
                var fa = QuadObj("FloorArrow" + ai, room, new Vector3(fleeX, 0.01f, -3.0f - ai * 2.4f), Vector3.zero, new Vector2(0.5f, 1.0f), arrowFloor);
                fa.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
            }

            // the entry doorway (open, dark) in the back wall at the lane's near edge, where the workers come from
            var entryX = ox - 4.6f;
            Box("EntryFrameL", room, new Vector3(entryX - 0.75f, 1.2f, zBack + 0.08f), new Vector3(0.1f, 2.4f, 0.14f), frameMat, false);
            Box("EntryFrameR", room, new Vector3(entryX + 0.75f, 1.2f, zBack + 0.08f), new Vector3(0.1f, 2.4f, 0.14f), frameMat, false);
            Box("EntryFrameT", room, new Vector3(entryX, 2.4f, zBack + 0.08f), new Vector3(1.6f, 0.1f, 0.14f), frameMat, false);
            QuadObj("EntryDark", room, new Vector3(entryX, 1.2f, zBack + 0.118f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 2.4f),
                Mat("Sc07EntryDark", new Color(0.05f, 0.06f, 0.06f), 0f, 0f));

            // ---- ceiling: steel trusses, purlins, tube lights, greenish skylights (step 01)
            var beam = Mat("Sc07Beam", new Color(0.20f, 0.21f, 0.22f), 0.25f, 0.4f);
            for (var z = zBack + 1.5f; z < zFront; z += 3.2f)
                Box("Truss", room, new Vector3(cx, hallH - 0.35f, z), new Vector3(w, 0.4f, 0.22f), beam, false);
            for (var x = x0 + 1f; x < x1; x += 4.2f)
                Box("Purlin", room, new Vector3(x, hallH - 0.18f, cz), new Vector3(0.2f, 0.3f, d), beam, false);
            var skyMat = Mat("Sc07Skylight", new Color(0.55f, 0.85f, 0.62f), 0f, 0f, null, new Color(0.55f, 1.0f, 0.65f), true);
            for (var i = 0; i < 5; i++)
                Box("Skylight" + i, room, new Vector3(ox - 16f + i * 4.6f, hallH - 0.06f, -6.5f), new Vector3(2.6f, 0.04f, 5.5f), skyMat, false);
            var tubeMat = Mat("Sc07Tube", new Color(0.95f, 0.97f, 0.95f), 0.2f, 0f, null, new Color(1.25f, 1.25f, 1.2f), false);
            var housing = Mat("Sc07TubeHousing", new Color(0.55f, 0.56f, 0.56f), 0.4f, 0.3f);
            var spots = new List<Vector3>
            {
                new Vector3(ox + 3.2f, hallH - 0.5f, 0.0f), new Vector3(ox + 0.4f, hallH - 0.5f, 1.8f), new Vector3(ox - 2.4f, hallH - 0.5f, 0.2f),
                new Vector3(ox - 6.5f, hallH - 0.5f, -1.5f), new Vector3(ox - 12f, hallH - 0.5f, -2.5f), new Vector3(ox - 12f, hallH - 0.5f, -8f),
                new Vector3(ox - 16f, hallH - 0.5f, -9f), new Vector3(ox + 1.5f, hallH - 0.5f, 3.8f),
                new Vector3(ox - 6.2f, hallH - 0.5f, 3.0f), new Vector3(ox - 6.2f, hallH - 0.5f, -4.5f), new Vector3(ox - 6.2f, hallH - 0.5f, -9.5f),
                new Vector3(ox - 14.5f, hallH - 0.5f, -3.0f)
            };
            for (var i = 0; i < spots.Count; i++)
            {
                var p = spots[i];
                Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(0.16f, 0.05f, 1.5f), housing, false);
                Box("Tube" + i, room, p - new Vector3(0.04f, 0f, 0f), new Vector3(0.04f, 0.04f, 1.4f), tubeMat, false);
                Box("TubeB" + i, room, p + new Vector3(0.04f, 0f, 0f), new Vector3(0.04f, 0.04f, 1.4f), tubeMat, false);
                var lightGo = new GameObject("TubeLight" + i);
                lightGo.transform.SetParent(room, false);
                lightGo.transform.position = p + Vector3.down * 0.6f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.92f, 0.96f, 0.93f);
                l.range = 13f;
                l.intensity = i < 3 ? 3.8f : 2.8f;
                l.shadows = LightShadows.None;
            }

            foreach (var fp in new[] { new Vector3(ox + 1.2f, 2.6f, 1.0f), new Vector3(ox - 1.6f, 2.6f, 0.8f) })
            {
                var fgo = new GameObject("WallFill");
                fgo.transform.SetParent(room, false);
                fgo.transform.position = fp;
                var fl = fgo.AddComponent<Light>();
                fl.type = LightType.Point;
                fl.color = new Color(0.96f, 0.98f, 1f);
                fl.range = 5.5f;
                fl.intensity = 2.0f;
                fl.shadows = LightShadows.None;
            }

            // clerestory windows high on the back wall with louvres (greenish daylight, as in the reference), a painted dado band below
            var winMat = Mat("Sc07Clerestory", new Color(0.50f, 0.68f, 0.56f), 0f, 0f, null, new Color(0.42f, 0.62f, 0.48f), true);
            for (var wi = 0; wi < 5; wi++)
            {
                var wx = ox - 17f + wi * 3.9f;
                QuadObj("Clerestory" + wi, room, new Vector3(wx, 4.45f, zBack + 0.09f), new Vector3(0f, 180f, 0f), new Vector2(3.0f, 1.1f), winMat);
                for (var lv = 0; lv < 6; lv++)
                    Box("Louvre" + wi + lv, room, new Vector3(wx, 3.95f + lv * 0.2f, zBack + 0.1f), new Vector3(3.0f, 0.03f, 0.04f), beam, false);
                Box("WinMull" + wi, room, new Vector3(wx - 1.5f, 4.45f, zBack + 0.1f), new Vector3(0.08f, 1.2f, 0.06f), beam, false);
            }

            // ceiling ducts, a cable tray and wall pipe bundles (workflow step 01: a working factory hall)
            var ductMat = Mat("Sc07Duct", new Color(0.40f, 0.42f, 0.43f), 0.3f, 0.3f);
            foreach (var dx in new[] { ox - 8.4f, ox - 13.2f })
            {
                Box("CeilingDuct", room, new Vector3(dx, hallH - 0.95f, cz), new Vector3(0.7f, 0.5f, d - 1f), ductMat, false);
                for (var dz = zBack + 1.5f; dz < zFront - 1f; dz += 2.4f)
                    Box("DuctFlange", room, new Vector3(dx, hallH - 0.95f, dz), new Vector3(0.88f, 0.68f, 0.04f), darkSteel, false);
                for (var dz = zBack + 2.5f; dz < zFront - 1f; dz += 4.8f)
                    Box("DuctHanger", room, new Vector3(dx, hallH - 0.5f, dz), new Vector3(0.03f, 0.5f, 0.03f), darkSteel, false);
            }

            Box("CableTray", room, new Vector3(ox - 5.4f, hallH - 0.75f, cz - 1f), new Vector3(0.45f, 0.07f, d - 4f), darkSteel, false);
            for (var cz2 = zBack + 1.5f; cz2 < zFront - 2f; cz2 += 1.6f)
                Box("CableRung", room, new Vector3(ox - 5.4f, hallH - 0.73f, cz2), new Vector3(0.45f, 0.03f, 0.04f), steel, false);
            for (var wp = 0; wp < 3; wp++)
            {
                var wpipe = Cyl("WallPipe" + wp, room, new Vector3(x0 + 0.25f + wp * 0.18f, 0f, cz), d - 1f, 0.06f, wp == 1 ? hoseRed : pipeMat);
                wpipe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                wpipe.transform.position = new Vector3(x0 + 0.25f + wp * 0.18f, 3.2f + wp * 0.0f, cz);
            }

            Box("BackDado", room, new Vector3(cx, 0.5f, zBack + 0.09f), new Vector3(w, 1.0f, 0.03f), Mat("Sc07Dado", new Color(0.36f, 0.42f, 0.40f), 0.2f, 0.1f), false);

            var dustMat = ParticleMat("Sc07Dust", art.radialGlow != null ? art.radialGlow.texture : null, true);
            BuildDust(room, new Vector3(ox - 6f, 2.4f, -2f), new Vector3(22f, 3.6f, 12f), dustMat);

            // ---- the workers: hero + 5 others, each with their own skin tone, overalls, helmet and height
            var crowdRoot = new GameObject("Crowd").transform;
            crowdRoot.SetParent(room, false);
            var prefabHi = Sc07MakeHuman("Hi");
            var prefabLo = Sc07MakeHuman("Lo");
            var walkClips = Sc07Clips("Step_Walk_", 6);
            var runClips = Sc07Clips("Step_Run_", 6);
            refs.people = new Sc07Human[Sc07People.Length];
            for (var i = 0; i < Sc07People.Length; i++)
            {
                var person = Sc07People[i];
                var prefab = i == 0 ? prefabHi : prefabLo;
                if (prefab == null)
                    continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = "Worker_" + person.name;
                inst.transform.SetParent(crowdRoot, false);
                inst.transform.localScale = Vector3.one * (person.height / 3.64f);
                inst.transform.position = i == 0
                    ? new Vector3(ox - 0.55f, 0f, 1.1f)
                    : new Vector3(ox - 3.4f - i * 0.9f, 0f, 1.1f + (i % 2) * 1.3f);
                inst.transform.rotation = Quaternion.Euler(0f, i == 0 ? 180f : 215f, 0f);
                Sc07Dress(inst, person);
                // two of them were at work when the alarm rang: the confused one (3) holds a toolbox, the frozen one (4) a long wrench
                if (i == 3 || i == 4)
                    Sc07GiveTool(inst.transform, i == 3, person.height / 3.64f);
                var human = inst.AddComponent<Sc07Human>();
                var hso = new SerializedObject(human);
                FillArray(hso.FindProperty("stepWalk"), walkClips.Length, (pp, k) => pp.objectReferenceValue = walkClips[k]);
                FillArray(hso.FindProperty("stepRun"), runClips.Length, (pp, k) => pp.objectReferenceValue = runClips[k]);
                hso.ApplyModifiedPropertiesWithoutUndo();
                human.Evaluate(0f, 0f); // saved in the idle pose (arms down) instead of the T-pose
                var shadowQuad = QuadObj("ContactShadow", inst.transform, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector2(0.7f, 0.7f) / (person.height / 3.64f),
                    MatAlpha("Sc07WorkerShadow", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png"), new Color(0f, 0f, 0f, 0.5f), false));
                shadowQuad.transform.localPosition = new Vector3(0f, 0.012f / (person.height / 3.64f), 0f);
                refs.people[i] = human;
            }

            refs.fireAt = Marker("FireAt", room, new Vector3(cabX, 0.05f, cabFrontZ + 0.35f), 0f);
            // ---- fire, sound
            refs.fire = Sc07BuildFire(room, new Vector3(cabX, 0.05f, cabFrontZ + 0.35f), cabFrontZ, 2.0f, art);
            var ambGo = new GameObject("Sc07Ambience");
            ambGo.transform.SetParent(room, false);
            refs.ambience = ambGo.AddComponent<Sc07Ambience>();
            var aso = new SerializedObject(refs.ambience);
            string AudioPath(string n) => "Assets/Art/Audio/Sc07/" + n + ".wav";
            aso.FindProperty("hall").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath("Hall_Loop"));
            aso.FindProperty("fire").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath("Fire_Loop"));
            aso.FindProperty("alarm").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath("Alarm_Loop"));
            aso.FindProperty("fireAt").objectReferenceValue = refs.fire.transform;
            aso.FindProperty("alarmAt").objectReferenceValue = refs.beaconFx.transform;
            aso.ApplyModifiedPropertiesWithoutUndo();

            // ---- inspection logic, the gate card, the people director
            var arrow = new GameObject("ExtArrow");
            arrow.transform.SetParent(room, false);
            arrow.SetActive(false);
            var inspectGo = new GameObject("ExtinguisherInspect");
            inspectGo.transform.SetParent(room, false);
            refs.inspect = inspectGo.AddComponent<ExtinguisherInspect>();
            var iso = new SerializedObject(refs.inspect);
            iso.FindProperty("art").objectReferenceValue = art;
            iso.FindProperty("arrow").objectReferenceValue = arrow.transform;
            var iarr = iso.FindProperty("items");
            iarr.arraySize = 3;
            for (var i = 0; i < 3; i++)
                iarr.GetArrayElementAtIndex(i).objectReferenceValue = refs.ext[i];
            iso.ApplyModifiedPropertiesWithoutUndo();

            var gateGo = new GameObject("Sc07GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc07Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc07Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc07Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            Transform T(string name, Vector3 p, float yaw) => Marker(name, room, p, yaw);
            var doorStart = T("Sc07DoorStart", new Vector3(entryX, 0f, zBack + 0.9f), 0f);
            var exitP = T("Sc07ExitPoint", new Vector3(fleeX, 0f, zBack + 0.25f), 180f);
            // four irregular places for the bystanders to stop and stare (no row, no grid)
            var noticeXZ = new[]
            {
                new Vector2(-4.4f, 0.4f), new Vector2(-6.2f, 1.6f), new Vector2(-5.0f, 2.8f), new Vector2(-7.4f, -0.6f)
            };
            var notice = new Transform[noticeXZ.Length];
            for (var i = 0; i < notice.Length; i++)
                notice[i] = T("Notice" + (i + 1), new Vector3(ox + noticeXZ[i].x, 0f, noticeXZ[i].y), 0f);
            var exitApproach = T("Sc07ExitApproach", new Vector3(fleeX, 0f, zBack + 3.4f), 180f);
            var heroSpot = T("Sc07HeroSpot", new Vector3(ox - 1.7f, 0f, 3.0f), 180f);
            var pillarGrab = T("Sc07PillarGrab", new Vector3(pillarCx, 0f, pillarFace + 0.62f), 180f);
            var spray = T("Sc07SpraySpot", new Vector3(cabX + 1.9f, 0f, cabFrontZ + 1.6f), 180f); // beside the fire, seen in profile from the first view
            refs.heroStart = T("Sc07HeroStart", new Vector3(pillarCx + 1.7f, 0f, pillarFace + 1.3f), 180f);
            var crowdGo = new GameObject("Sc07Crowd");
            crowdGo.transform.SetParent(room, false);
            refs.crowd = crowdGo.AddComponent<Sc07Crowd>();
            var cso = new SerializedObject(refs.crowd);
            var parr = cso.FindProperty("people");
            parr.arraySize = refs.people.Length;
            for (var i = 0; i < refs.people.Length; i++)
                parr.GetArrayElementAtIndex(i).objectReferenceValue = refs.people[i];
            cso.FindProperty("doorStart").objectReferenceValue = doorStart;
            var narr = cso.FindProperty("noticeSpots");
            narr.arraySize = notice.Length;
            for (var i = 0; i < notice.Length; i++)
                narr.GetArrayElementAtIndex(i).objectReferenceValue = notice[i];
            cso.FindProperty("exitPoint").objectReferenceValue = exitP;
            cso.FindProperty("exitApproach").objectReferenceValue = exitApproach;
            cso.FindProperty("fleeDoor").objectReferenceValue = refs.fleeDoor;
            cso.FindProperty("heroSpot").objectReferenceValue = heroSpot;
            cso.FindProperty("pillarGrab").objectReferenceValue = pillarGrab;
            cso.FindProperty("spraySpot").objectReferenceValue = spray;
            cso.FindProperty("fireAt").objectReferenceValue = refs.fireAt != null ? refs.fireAt : refs.fire.transform;
            cso.FindProperty("co2Extinguisher").objectReferenceValue = refs.ext[1];
            cso.FindProperty("fire").objectReferenceValue = refs.fire;
            cso.FindProperty("beacon").objectReferenceValue = refs.beaconFx;
            cso.FindProperty("ambience").objectReferenceValue = refs.ambience;
            cso.FindProperty("hiss").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc07/Co2_Hiss.wav");
            cso.ApplyModifiedPropertiesWithoutUndo();

            // ---- markers
            refs.spawn = Marker("Spawn_SC07", room, new Vector3(ox - 0.8f, 0f, 4.3f), 210f); // a little to the right: pillar at the left edge, cabinet left of centre, the hall and the crowd right
            refs.standB = Marker("Stand_SC07_Pillar", room, new Vector3(pillarCx, 0f, pillarFace + 0.95f), 180f);
            return refs;
        }

        // A hand-held tool glued to a hand bone. The arm hangs down along the bone's local X (right hand +X, left hand -X).
        private static void Sc07GiveTool(Transform worker, bool box, float scale)
        {
            Transform hand = null;
            foreach (var t in worker.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == (box ? "HandL" : "HandR"))
                {
                    hand = t;
                    break;
                }
            }

            if (hand == null)
                return;
            var dir = box ? -1f : 1f;
            GameObject Part(string name, Vector3 meters, Vector3 posMeters, Material mat)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(hand, false);
                // bone axes: X along the arm. meters are given as (along arm, across, depth)
                go.transform.localPosition = new Vector3(posMeters.x * dir, posMeters.y, posMeters.z) / scale;
                go.transform.localScale = new Vector3(meters.x, meters.y, meters.z) / scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                return go;
            }

            var metal = Mat("Sc07ToolMetal", new Color(0.55f, 0.57f, 0.60f), 0.7f, 0.45f);
            var red = Mat("Sc07ToolBoxRed", new Color(0.70f, 0.12f, 0.08f), 0.4f, 0.1f);
            var dark = Mat("Sc07ToolGrip", new Color(0.10f, 0.10f, 0.11f), 0.2f, 0f);
            if (box)
            {
                Part("ToolBox", new Vector3(0.22f, 0.20f, 0.42f), new Vector3(0.14f, 0f, 0f), red);
                Part("ToolBoxLid", new Vector3(0.05f, 0.205f, 0.44f), new Vector3(0.03f, 0f, 0f), dark);
            }
            else
            {
                Part("Wrench", new Vector3(0.62f, 0.035f, 0.05f), new Vector3(0.22f, 0f, 0f), metal);
                Part("WrenchJaw", new Vector3(0.09f, 0.035f, 0.11f), new Vector3(0.55f, 0f, 0f), metal);
                Part("WrenchGrip", new Vector3(0.20f, 0.04f, 0.055f), new Vector3(0.08f, 0f, 0f), dark);
            }
        }

        private const string Sc07OnlyKey ="ChemSafetySc07Only";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 7")]
        private static void ToggleSc07Only()
        {
            EditorPrefs.SetBool(Sc07OnlyKey, !EditorPrefs.GetBool(Sc07OnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 7", true)]
        private static bool ToggleSc07OnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 7", EditorPrefs.GetBool(Sc07OnlyKey, false));
            return true;
        }

        private static AudioClip[] Sc07Clips(string prefix, int count)
        {
            var list = new List<AudioClip>();
            for (var i = 0; i < count; i++)
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc07/" + prefix + i + ".wav");
                if (c != null)
                    list.Add(c);
            }

            return list.ToArray();
        }

        // ------------------------------------------------------------------ editor preview (no play mode needed)

        // Reads Temp/sc07_views.txt: one view per line "name x y z yawDeg pitchDeg fov" (x and z relative to the SC07 origin,
        // y absolute eye height). Writes Temp/Smoke/sc07_<name>.png.
        [MenuItem("Chemical Safety/Presentable/Preview SC07 (screenshots)")]
        public static void PreviewSc07()
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
            GameObject env7 = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (!r.name.StartsWith("Environment_SC"))
                    continue;
                states[r] = r.activeSelf;
                if (r.name == "Environment_SC07")
                    env7 = r;
            }

            if (env7 == null)
            {
                Debug.LogError("[Presentable] Environment_SC07 missing; build the scene first.");
                return;
            }

            foreach (var kv in states)
                kv.Key.SetActive(kv.Key == env7);

            var oldMode = RenderSettings.ambientMode;
            var oldSky = RenderSettings.ambientSkyColor;
            var oldEq = RenderSettings.ambientEquatorColor;
            var oldGr = RenderSettings.ambientGroundColor;
            var oldFog = RenderSettings.fogColor;
            var oldFs = RenderSettings.fogStartDistance;
            var oldFe = RenderSettings.fogEndDistance;
            var oldRef = RenderSettings.reflectionIntensity;
            var oldFogOn = RenderSettings.fog;
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

            var mso = new SerializedObject(env7.GetComponent<Sc03Mood>());
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = mso.FindProperty("ambientSky").colorValue;
            RenderSettings.ambientEquatorColor = mso.FindProperty("ambientEquator").colorValue;
            RenderSettings.ambientGroundColor = mso.FindProperty("ambientGround").colorValue;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = mso.FindProperty("fogColor").colorValue;
            RenderSettings.fogStartDistance = mso.FindProperty("fogStart").floatValue;
            RenderSettings.fogEndDistance = mso.FindProperty("fogEnd").floatValue;
            RenderSettings.reflectionIntensity = mso.FindProperty("reflectionIntensity").floatValue;
            if (sun != null)
                sun.intensity = mso.FindProperty("sunIntensity").floatValue;

            var views = new List<string>();
            var viewFile = "Temp/sc07_views.txt";
            if (File.Exists(viewFile))
                views.AddRange(File.ReadAllLines(viewFile));
            else
            {
                views.Add("a 0.6 1.6 2.7 0 4 80");
                views.Add("b 1.2 1.6 -0.85 0 2 80");
                views.Add("wide -3 2.2 4 25 8 90");
            }

            // optional Temp/sc07_pose.txt: one worker per line "index x z yawDeg mode" (x, z relative to the SC07 origin;
            // mode = idle, walk, run, startle, carry, aim, reach, walk:0.3 = gait phase) to check the procedural poses
            var poseFile = "Temp/sc07_pose.txt";
            var crowdPreview = env7.GetComponentInChildren<Sc07Crowd>(true);
            if (File.Exists(poseFile) && crowdPreview != null)
            {
                var folk = env7.GetComponentsInChildren<Sc07Human>(true);
                foreach (var line in File.ReadAllLines(poseFile))
                {
                    var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 5)
                        continue;
                    var idx = int.Parse(p[0]);
                    if (idx < 0 || idx >= folk.Length)
                        continue;
                    var h = folk[idx];
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    h.gameObject.SetActive(true);
                    h.transform.position = new Vector3(Sc07Ox + float.Parse(p[1], inv), 0f, float.Parse(p[2], inv));
                    h.transform.rotation = Quaternion.Euler(0f, float.Parse(p[3], inv), 0f);
                    var mode = p[4];
                    var phase = 0.25f;
                    var colon = mode.IndexOf(':');
                    if (colon > 0)
                    {
                        phase = float.Parse(mode.Substring(colon + 1), inv);
                        mode = mode.Substring(0, colon);
                    }

                    switch (mode)
                    {
                        case "walk": h.SnapPose(1f, 0f, 0f, 0f, 0f, 0f, phase); break;
                        case "run": h.SnapPose(1f, 1f, 0f, 0f, 0f, 0f, phase); break;
                        case "startle": h.SnapPose(0f, 0f, 1f, 0f, 0f, 0f, phase); break;
                        case "carry": h.SnapPose(0f, 0f, 0f, 1f, 0f, 0f, phase); crowdPreview.PreviewHold(false); break;
                        case "carrywalk": h.SnapPose(1f, 0f, 0f, 1f, 0f, 0f, phase); crowdPreview.PreviewHold(false); break;
                        case "aim": h.SnapPose(0f, 0f, 0f, 0f, 1f, 0f, phase); crowdPreview.PreviewHold(true); break;
                        case "reach": h.SnapPose(0f, 0f, 0f, 0f, 0f, 1f, phase); break;
                        case "shocked": h.SnapPose(0f, 0f, 0f, 0f, 0f, 0f, phase, Sc07Reaction.Shocked); break;
                        case "head": h.SnapPose(0f, 0f, 0f, 0f, 0f, 0f, phase, Sc07Reaction.Head); break;
                        case "confused": h.SnapPose(0f, 0f, 0f, 0f, 0f, 0f, phase, Sc07Reaction.Confused); break;
                        case "frozen": h.SnapPose(0f, 0f, 0f, 0f, 0f, 0f, phase, Sc07Reaction.Frozen); break;
                        default: h.SnapPose(0f, 0f, 0f, 0f, 0f, 0f, phase); break;
                    }
                }
            }

            foreach (var f in env7.GetComponentsInChildren<Sc07Fire>(true))
                f.SetIntensity(1f);
            foreach (var ps in env7.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Simulate(4.5f, true, true);
            }

            Directory.CreateDirectory("Temp/Smoke");
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.4f, 0.42f, 0.41f);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            foreach (var line in views)
            {
                var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 7)
                    continue;
                float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
                cam.transform.position = new Vector3(Sc07Ox + F(1), F(2), F(3));
                cam.transform.rotation = Quaternion.Euler(F(5), 180f - F(4), 0f);
                cam.fieldOfView = F(6);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/Smoke/sc07_" + p[0] + ".png", tex.EncodeToPNG());
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
            RenderSettings.fog = oldFogOn;
            RenderSettings.fogColor = oldFog;
            RenderSettings.fogStartDistance = oldFs;
            RenderSettings.fogEndDistance = oldFe;
            RenderSettings.reflectionIntensity = oldRef;
            if (sun != null)
                sun.intensity = oldSun;
            // the preview poses workers, parents the extinguisher to the hero and runs the fire: reload the saved scene so a later Play starts clean
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[SC07] preview written to Temp/Smoke/sc07_*.png");
        }
    }
}
#endif

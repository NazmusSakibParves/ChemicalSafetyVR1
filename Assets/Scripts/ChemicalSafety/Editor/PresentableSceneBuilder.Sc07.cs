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

            const float zBack = -14f;
            const float zFront = 6f;
            const float hallH = 5.6f;
            var x0 = ox - 15f;
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
            FloorLine("LineAisle", new Vector3(ox - 14f, 0f, 1.6f), new Vector3(ox - 3.4f, 0f, 1.6f));
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
            var factory = SpawnSc07("Factory.glb", room);
            if (factory != null)
            {
                factory.name = "Machines";
                factory.transform.localScale = Vector3.one * 0.36f;
                factory.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var fb = BoundsOfTree(factory.transform);
                factory.transform.position += new Vector3((ox - 9.0f) - fb.center.x, -fb.min.y, (-10.5f) - fb.center.z);
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

            // ---- exit door with the green sign in the hall's far wall
            // second door in the right wall further back: the workers flee through it, in full view of the player
            Box("FleeDoor", room, new Vector3(x0 + 0.1f, 1.1f, -4.6f), new Vector3(0.08f, 2.2f, 1.3f), Mat("Sc07DoorGrey", new Color(0.55f, 0.58f, 0.58f), 0.3f, 0.25f), false);
            QuadObj("FleeSign", room, new Vector3(x0 + 0.17f, 2.55f, -4.6f), new Vector3(0f, 90f, 0f), new Vector2(0.62f, 0.31f),
                Mat("Sc07ExitSign", Color.white, 0f, 0f, Sc07Tex("ExitSign"), new Color(0.5f, 1.0f, 0.55f), true));
            var door = Box("ExitDoor", room, new Vector3(x0 + 0.1f, 1.1f, 3.0f), new Vector3(0.08f, 2.2f, 1.3f), Mat("Sc07DoorGrey", new Color(0.55f, 0.58f, 0.58f), 0.3f, 0.25f), false);
            var sign = QuadObj("ExitSign", room, new Vector3(x0 + 0.17f, 2.55f, 3.0f), new Vector3(0f, 90f, 0f), new Vector2(0.62f, 0.31f),
                Mat("Sc07ExitSign", Color.white, 0f, 0f, Sc07Tex("ExitSign"), new Color(0.5f, 1.0f, 0.55f), true));
            var exitLight = new GameObject("ExitGlow");
            exitLight.transform.SetParent(room, false);
            exitLight.transform.position = new Vector3(x0 + 0.9f, 2.4f, 3.0f);
            var el = exitLight.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.35f, 1f, 0.5f);
            el.range = 5f;
            el.intensity = 0.8f;
            el.shadows = LightShadows.None;

            // ---- ceiling: steel trusses, purlins, tube lights, greenish skylights (step 01)
            var beam = Mat("Sc07Beam", new Color(0.20f, 0.21f, 0.22f), 0.25f, 0.4f);
            for (var z = zBack + 1.5f; z < zFront; z += 3.2f)
                Box("Truss", room, new Vector3(cx, hallH - 0.35f, z), new Vector3(w, 0.4f, 0.22f), beam, false);
            for (var x = x0 + 1f; x < x1; x += 4.2f)
                Box("Purlin", room, new Vector3(x, hallH - 0.18f, cz), new Vector3(0.2f, 0.3f, d), beam, false);
            var skyMat = Mat("Sc07Skylight", new Color(0.55f, 0.85f, 0.62f), 0f, 0f, null, new Color(0.55f, 1.0f, 0.65f), true);
            for (var i = 0; i < 5; i++)
                Box("Skylight" + i, room, new Vector3(ox - 11f + i * 4.4f, hallH - 0.06f, -8f), new Vector3(2.6f, 0.04f, 5.5f), skyMat, false);
            var tubeMat = Mat("Sc07Tube", new Color(0.95f, 0.97f, 0.95f), 0.2f, 0f, null, new Color(1.25f, 1.25f, 1.2f), false);
            var housing = Mat("Sc07TubeHousing", new Color(0.55f, 0.56f, 0.56f), 0.4f, 0.3f);
            var spots = new List<Vector3>
            {
                new Vector3(ox + 3.2f, hallH - 0.5f, 0.0f), new Vector3(ox + 0.4f, hallH - 0.5f, 1.8f), new Vector3(ox - 2.4f, hallH - 0.5f, 0.2f),
                new Vector3(ox - 6.5f, hallH - 0.5f, -1.5f), new Vector3(ox - 10.5f, hallH - 0.5f, -2.5f), new Vector3(ox - 6.5f, hallH - 0.5f, -8f),
                new Vector3(ox - 11f, hallH - 0.5f, -9f), new Vector3(ox + 1.5f, hallH - 0.5f, 3.8f)
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

            var dustMat = ParticleMat("Sc07Dust", art.radialGlow != null ? art.radialGlow.texture : null, true);
            BuildDust(room, new Vector3(ox - 3f, 2.4f, -1f), new Vector3(16f, 3.6f, 9f), dustMat);

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
                    : new Vector3(ox - 4.8f - (i % 4) * 1.1f, 0f, 0.4f + (i / 4) * 1.1f);
                inst.transform.rotation = Quaternion.Euler(0f, i == 0 ? 180f : 215f, 0f);
                Sc07Dress(inst, person);
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
            var doorStart = T("Sc07DoorStart", new Vector3(x0 + 1.2f, 0f, 3.0f), 90f);
            var exitP = T("Sc07ExitPoint", new Vector3(x0 + 0.1f, 0f, -4.6f), 90f);
            // eleven places for the workers to stop and stare, in three loose rows facing the fire
            var noticeXZ = new[]
            {
                new Vector2(-3.3f, 0.3f), new Vector2(-4.2f, 0.4f), new Vector2(-5.1f, 0.2f), new Vector2(-6.0f, 0.4f),
                new Vector2(-3.0f, 1.5f), new Vector2(-3.9f, 1.6f), new Vector2(-4.8f, 1.4f), new Vector2(-5.7f, 1.6f),
                new Vector2(-3.4f, 2.7f), new Vector2(-4.4f, 2.8f), new Vector2(-5.4f, 2.6f)
            };
            var notice = new Transform[noticeXZ.Length];
            for (var i = 0; i < notice.Length; i++)
                notice[i] = T("Notice" + (i + 1), new Vector3(ox + noticeXZ[i].x, 0f, noticeXZ[i].y), 0f);
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
            refs.spawn = Marker("Spawn_SC07", room, new Vector3(ox - 0.8f, 0f, 4.3f), 208f); // a little to the right: pillar at the left edge, cabinet left of centre, the hall and the crowd right
            refs.standB = Marker("Stand_SC07_Pillar", room, new Vector3(pillarCx, 0f, pillarFace + 0.95f), 180f);
            return refs;
        }

        private const string Sc07OnlyKey = "ChemSafetySc07Only";

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
            Debug.Log("[SC07] preview written to Temp/Smoke/sc07_*.png");
        }
    }
}
#endif

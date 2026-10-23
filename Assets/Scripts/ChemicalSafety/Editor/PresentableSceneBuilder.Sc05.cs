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
    /// Scenario 5: dyeing factory, modelled on the Scene 05 workflow. The viewer stands at Spawn_SC05 facing -Z (the
    /// viewer's left is +X) in front of a concrete pillar with the Vapor Hazard sign, the gas detector, the SDS holder, an
    /// extinguisher and a hook rail with four respirators. Dye machines, steam, fast fans and flickering tubes behind.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc05Refs
        {
            public Transform spawn;
            public MaskInspect masks;
            public Sc05Ambience ambience;
        }

        private const float Sc05Ox = 160f;
        private const string Sc05TexDir = ArtRoot + "/2D/Sc05Tex";
        private const float FactoryScale = 0.5f;

        private static Texture2D Sc05Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Sc05TexDir + "/" + name + ".png");

        private static void ConfigureSc05Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc05TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var normal = file.EndsWith("_Normal");
                var linear = file.EndsWith("_MS");
                var decal = file.StartsWith("Steam_") || file.StartsWith("Label_") || file.StartsWith("Detector");
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
                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 || ti.alphaIsTransparency != decal)
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

        private static Material Sc05Pbr(string name, string set, Color tint, Vector2 tiling, float smoothScale, float bump,
            bool hasMs = true)
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

            m.SetTexture("_BaseMap", Sc05Tex(set + "_Albedo"));
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", Sc05Tex(set + "_Normal"));
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            if (hasMs && Sc05Tex(set + "_MS") != null)
            {
                m.SetTexture("_MetallicGlossMap", Sc05Tex(set + "_MS"));
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", smoothScale);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                m.SetFloat("_Smoothness", 0.08f);
                m.SetFloat("_Metallic", 0f);
            }

            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureScale("_BumpMap", tiling);
            m.SetTextureScale("_MetallicGlossMap", tiling);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // Instantiates a Sc05 glb as a plain (unpacked) object so its nodes can be re-parented.
        private static GameObject SpawnSc05(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc05/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc05 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        private static Bounds BoundsOfTree(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
                return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            for (var i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Sc05Refs BuildSc05Room(Transform env, PresentableArt art)
        {
            const float ox = Sc05Ox;
            var refs = new Sc05Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solidLayer = SolidLayer;
            ConfigureSc05Textures();

            // dark, hazy factory air (reuses the room-mood component of Scenario 3 with its own values)
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.52f, 0.53f, 0.54f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.44f, 0.44f, 0.45f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.20f, 0.20f, 0.20f);
            mso.FindProperty("fogColor").colorValue = new Color(0.27f, 0.28f, 0.28f);
            mso.FindProperty("fogStart").floatValue = 4f;
            mso.FindProperty("fogEnd").floatValue = 30f;
            mso.FindProperty("sunIntensity").floatValue = 0.05f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.25f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            const float hallL = -20f;     // back wall z
            const float hallF = 3.6f;     // wall behind the player
            const float hallH = 4.4f;
            var x0 = ox - 4f;
            var x1 = ox + 8f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (hallL + hallF) * 0.5f;
            var w = x1 - x0;
            var d = hallF - hallL;

            var floorMat = Sc05Pbr("Sc05FloorPbr", "Floor", new Color(0.9f, 0.9f, 0.9f), new Vector2(w / 2.4f, d / 2.4f), 0.9f, 0.8f);
            var wallBackMat = Sc05Pbr("Sc05WallBackPbr", "Wall", new Color(0.95f, 0.95f, 0.95f), new Vector2(w / 2.4f, 1.9f), 0.9f, 1f);
            var wallSideMat = Sc05Pbr("Sc05WallSidePbr", "Wall", new Color(0.95f, 0.95f, 0.95f), new Vector2(d / 2.4f, 1.9f), 0.9f, 1f);
            var ceilMat = Sc05Pbr("Sc05CeilPbr", "Ceil", Color.white, new Vector2(w / 2.4f, d / 2.4f), 1f, 0.8f, false);
            var pillarMat = Sc05Pbr("Sc05PillarPbr", "Wall", new Color(1f, 1f, 1f), new Vector2(0.8f, 1.8f), 0.9f, 1f);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solidLayer >= 0)
                    go.layer = solidLayer;
            }

            var floor = Box("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(cx, hallH * 0.5f, hallL), new Vector3(w, hallH, 0.12f), wallBackMat);
            Shell("Wall_Front", new Vector3(cx, hallH * 0.5f, hallF), new Vector3(w, hallH, 0.12f), wallBackMat);
            Shell("Wall_Left", new Vector3(x1, hallH * 0.5f, cz), new Vector3(0.12f, hallH, d), wallSideMat);
            Shell("Wall_Right", new Vector3(x0, hallH * 0.5f, cz), new Vector3(0.12f, hallH, d), wallSideMat);
            Box("Ceiling", room, new Vector3(cx, hallH, cz), new Vector3(w, 0.1f, d), ceilMat, false);

            // ---- the pillar the respirators hang on (workflow step01: right half of the first view)
            var pillarFront = 0.6f;
            Shell("Pillar", new Vector3(ox - 0.9f, hallH * 0.5f, 0f), new Vector3(1.9f, hallH, 1.2f), pillarMat);
            var steel = Mat("Sc05Steel", new Color(0.42f, 0.44f, 0.46f), 0.4f, 0.25f);
            var rust = Mat("Sc05Rust", new Color(0.32f, 0.2f, 0.12f), 0.3f, 0.4f);
            Cyl("PillarConduit", room, new Vector3(ox - 1.75f, 0f, pillarFront + 0.03f), hallH, 0.028f, steel);
            Cyl("PillarConduit2", room, new Vector3(ox - 1.62f, 0f, pillarFront + 0.03f), hallH, 0.02f, steel);
            Box("PillarBand", room, new Vector3(ox - 0.9f, 2.9f, pillarFront + 0.01f), new Vector3(1.92f, 0.1f, 0.03f), steel, false);
            Box("PillarBandLow", room, new Vector3(ox - 0.9f, 0.18f, pillarFront + 0.01f), new Vector3(1.92f, 0.36f, 0.03f),
                Mat("Sc05Skirt", new Color(0.12f, 0.12f, 0.13f), 0.2f, 0.1f), false);

            // Vapor Hazard sign (yellow, Bangla + English), left part of the pillar face
            var signTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc05/VaporSign.png");
            var signMat = Mat("Sc05Sign", Color.white, 0.35f, 0.05f, signTex, null, false, true);
            QuadObj("VaporSign", room, new Vector3(ox - 0.30f, 2.08f, pillarFront + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.62f, 0.5f), signMat).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // gas detector, reads 85 and pulses red
            var detBody = Mat("Sc05DetectorBody", new Color(0.12f, 0.13f, 0.14f), 0.5f, 0.3f);
            Box("DetectorBody", room, new Vector3(ox - 0.30f, 1.52f, pillarFront + 0.03f), new Vector3(0.17f, 0.29f, 0.06f), detBody, false);
            var detFace = Mat("Sc05DetectorFace", Color.white, 0f, 0f, Sc05Tex("Detector"), null, true, true);
            QuadObj("DetectorFace", room, new Vector3(ox - 0.30f, 1.52f, pillarFront + 0.064f), new Vector3(0f, 180f, 0f),
                new Vector2(0.158f, 0.25f), detFace);
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var detGlowMat = MatAlpha("Sc05DetectorGlow", glowTex, new Color(1f, 0.15f, 0.1f, 0.3f), true);
            var detGlow = QuadObj("DetectorGlow", room, new Vector3(ox - 0.30f, 1.57f, pillarFront + 0.075f), new Vector3(0f, 180f, 0f),
                new Vector2(0.5f, 0.5f), detGlowMat).GetComponent<Renderer>();
            var detLightGo = new GameObject("DetectorLight");
            detLightGo.transform.SetParent(room, false);
            detLightGo.transform.position = new Vector3(ox - 0.30f, 1.58f, pillarFront + 0.35f);
            var detLight = detLightGo.AddComponent<Light>();
            detLight.type = LightType.Point;
            detLight.color = new Color(1f, 0.15f, 0.1f);
            detLight.range = 1.1f;
            detLight.intensity = 0.1f;
            detLight.shadows = LightShadows.None;

            // a white fill in front of the pillar so the respirators read (the hall itself stays dark and hazy)
            var fillGo = new GameObject("PillarFill");
            fillGo.transform.SetParent(room, false);
            fillGo.transform.position = new Vector3(ox - 0.3f, 2.3f, 1.6f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.95f, 0.97f, 1f);
            fill.range = 4.5f;
            fill.intensity = 1.5f;
            fill.shadows = LightShadows.None;

            // OSHA SDS holder under it
            var holderMat = Mat("Sc05SdsHolder", Color.white, 0.35f, 0f, Sc05Tex("Label_Sds"), null, false, true);
            QuadObj("SdsHolder", room, new Vector3(ox - 0.30f, 0.98f, pillarFront + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.40f, 0.5f), holderMat);

            // ---- the hook rail with the four respirators (Masks.glb)
            var maskRoot = SpawnSc05("Masks.glb", room);
            var maskNodes = new Transform[4];
            if (maskRoot != null)
            {
                maskRoot.name = "Respirators";
                maskRoot.transform.localScale = Vector3.one * 0.30f;
                var plane = FindNode(maskRoot.transform, "Plane");
                if (plane != null)
                    plane.gameObject.SetActive(false);
                var rail = FindNode(maskRoot.transform, "Object_7");
                var names = new[] { "Cylinder_Facemask_0", "Cylinder.002", "MedicalMask1:cord_MedicalMask1:medm_0", "Object_2" };
                for (var i = 0; i < names.Length; i++)
                    maskNodes[i] = FindNode(maskRoot.transform, names[i]);
                // rail on the pillar face, centred left to right on the rail area (the masks hang in front of it)
                var rb = rail != null ? BoundsOfTree(rail) : BoundsOfTree(maskRoot.transform);
                maskRoot.transform.position += new Vector3((ox - 1.22f) - rb.center.x, 1.72f - rb.center.y, 0f);
                rb = rail != null ? BoundsOfTree(rail) : BoundsOfTree(maskRoot.transform);
                maskRoot.transform.position += new Vector3(0f, 0f, pillarFront + 0.005f - rb.min.z);
                // glTF import can mirror the model: the masks must hang on the player's side (+Z) of the rail
                var allB = BoundsOfTree(maskRoot.transform);
                var mb = maskNodes[0] != null ? BoundsOfTree(maskNodes[0]) : allB;
                rb = rail != null ? BoundsOfTree(rail) : allB;
                if (mb.center.z < rb.center.z)
                {
                    var pivot = new Vector3(rb.center.x, 0f, pillarFront);
                    maskRoot.transform.RotateAround(pivot, Vector3.up, 180f);
                    rb = BoundsOfTree(rail != null ? rail : maskRoot.transform);
                    maskRoot.transform.position += new Vector3(0f, 0f, pillarFront + 0.005f - rb.min.z);
                }
            }

            var maskGroups = new Transform[4];
            for (var i = 0; i < 4; i++)
            {
                if (maskNodes[i] == null)
                    continue;
                var nb = BoundsOfTree(maskNodes[i]);
                var group = new GameObject("Mask_" + i).transform;
                group.SetParent(room, true);
                group.position = nb.center;
                group.rotation = Quaternion.identity; // local +Z = world +Z, the side that faces the player at home
                maskNodes[i].SetParent(group, true);
                maskGroups[i] = group;
                var col = group.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                nb = BoundsOfTree(group);
                // padded toward the player (+Z) and upward so the fingertip reaches it before the rail wall stops it
                col.center = group.InverseTransformPoint(nb.center + new Vector3(0f, 0.03f, 0.12f));
                col.size = nb.size + new Vector3(0.10f, 0.14f, 0.30f);
                var poke = group.gameObject.AddComponent<PokeTarget>();
                var pso = new SerializedObject(poke);
                pso.FindProperty("reach").floatValue = 0.07f;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- fire extinguisher at the pillar's right-front corner
            var ext = SpawnSc05("Extinguisher.glb", room);
            if (ext != null)
            {
                ext.name = "Extinguisher";
                FitHeight(ext, 0.72f);
                var eb = BoundsOfTree(ext.transform);
                ext.transform.position += new Vector3((ox - 1.62f) - eb.center.x, 0.04f - eb.min.y, (pillarFront + 0.1f) - eb.center.z);
            }

            // ---- the factory: dye machines in a row down the hall (Factory.glb, half size), rotated to run away from the viewer
            // metals look black without reflections in this hazy hall, so the tanks are painted steel instead of mirrors
            var vatMat = Mat("Sc05Vat", new Color(0.50f, 0.54f, 0.55f), 0.3f, 0.0f);
            var panelMat = Mat("Sc05Panel", new Color(0.36f, 0.48f, 0.58f), 0.3f, 0.0f);
            var factory = SpawnSc05("Factory.glb", room);
            var fan = new List<Sc05FactoryFx.Spinner>();
            var shake = new List<Sc05FactoryFx.Shaker>();
            var sway = new List<Sc05FactoryFx.Swayer>();
            var machineCentres = new List<Vector3>();
            if (factory != null)
            {
                factory.name = "DyeMachines";
                factory.transform.localScale = Vector3.one * FactoryScale;
                factory.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var fb = BoundsOfTree(factory.transform);
                // the machine row (original x -2.4..0.9, z 0..15) lands at x ox+1.3..2.9 and runs away from the viewer
                factory.transform.position = new Vector3(ox + 1.7f, -fb.min.y, -0.2f);
                foreach (var r in factory.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    var inside = b.center.z < 0.4f && b.center.z > -17f && b.center.x > ox + 0.3f && b.center.x < x1 - 0.2f && b.max.y < hallH + 0.1f;
                    if (!inside)
                    {
                        r.gameObject.SetActive(false);
                        continue;
                    }

                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (r.sharedMaterial == null)
                        r.sharedMaterial = vatMat; // a mesh without a material renders magenta
                    else if (mn.Contains("ControlPanel") || mn.Contains("Default"))
                        r.sharedMaterial = panelMat;
                    if (mn.Contains("Brushed") || mn.Contains("Metal"))
                    {
                        r.sharedMaterial = vatMat;
                        shake.Add(new Sc05FactoryFx.Shaker { target = r.transform, amplitude = 0.0035f, frequency = 11f + shake.Count });
                        machineCentres.Add(b.center);
                    }
                    else if (mn.Contains("cloth") || mn.Contains("Cloth") || mn == "Material.001")
                    {
                        sway.Add(new Sc05FactoryFx.Swayer { target = r.transform, degrees = 1.4f, speed = 0.9f + 0.17f * sway.Count });
                    }
                }
            }

            // ---- ceiling: beams, pipes, fast fans, fluorescent tubes
            var beam = Mat("Sc05Beam", new Color(0.2f, 0.2f, 0.21f), 0.2f, 0.3f);
            for (var z = -2f; z > hallL; z -= 3.5f)
                Box("CeilBeam", room, new Vector3(cx, hallH - 0.3f, z), new Vector3(w, 0.32f, 0.25f), beam, false);
            for (var i = 0; i < 3; i++)
            {
                var p = Cyl("CeilPipe" + i, room, new Vector3(ox + 2.2f + i * 1.7f, hallH - 0.55f - i * 0.12f, hallF), d - 0.4f, 0.07f - i * 0.012f, i == 1 ? rust : steel);
                p.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                p.transform.position = new Vector3(ox + 2.2f + i * 1.7f, hallH - 0.55f - i * 0.12f, hallF - (d - 0.4f) * 0.5f - 0.2f);
            }

            var fanHub = Mat("Sc05FanHub", new Color(0.42f, 0.43f, 0.44f), 0.35f, 0.4f);
            var fanBlade = Mat("Sc05FanBlade", new Color(0.62f, 0.62f, 0.6f), 0.3f, 0.2f);
            var fanSpots = new[] { new Vector3(ox + 0.5f, hallH - 0.9f, -2.2f), new Vector3(ox + 3.0f, hallH - 0.9f, -8.0f), new Vector3(ox - 0.5f, hallH - 0.9f, -13.5f) };
            for (var i = 0; i < fanSpots.Length; i++)
            {
                var spin = new GameObject("CeilingFan" + i).transform;
                spin.SetParent(room, false);
                spin.position = fanSpots[i];
                Cyl("Rod", room, fanSpots[i] + Vector3.up * 0.1f, hallH - fanSpots[i].y - 0.1f, 0.012f, hanger());
                Cyl("Motor", spin, fanSpots[i] + Vector3.up * 0.0f, 0.12f, 0.1f, fanHub).transform.SetParent(spin, true);
                for (var b = 0; b < 4; b++)
                {
                    var blade = Box("Blade" + b, spin, fanSpots[i], new Vector3(0.62f, 0.012f, 0.16f), fanBlade, false);
                    blade.transform.SetParent(spin, true);
                    blade.transform.localPosition = Quaternion.Euler(0f, b * 90f, 0f) * new Vector3(0.42f, 0.04f, 0f);
                    blade.transform.localRotation = Quaternion.Euler(0f, b * 90f, 7f);
                }

                fan.Add(new Sc05FactoryFx.Spinner { target = spin, axis = Vector3.up, degreesPerSecond = 640f + i * 70f });
            }

            var tubeMat = Mat("Sc05Tube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(1.15f, 1.15f, 1.1f), false);
            var housing = Mat("Sc05TubeHousing", new Color(0.55f, 0.56f, 0.56f), 0.4f, 0.3f);
            var flicks = new List<Sc05FactoryFx.Flicker>();
            var tubeSpots = new[]
            {
                new Vector3(ox + 0.3f, hallH - 0.25f, 0.8f), new Vector3(ox + 2.6f, hallH - 0.25f, -3.5f),
                new Vector3(ox + 0.6f, hallH - 0.25f, -6.5f), new Vector3(ox + 3.2f, hallH - 0.25f, -10f),
                new Vector3(ox + 0.8f, hallH - 0.25f, -14f)
            };
            for (var i = 0; i < tubeSpots.Length; i++)
            {
                var p = tubeSpots[i];
                Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(0.14f, 0.05f, 1.3f), housing, false);
                var t1 = Box("Tube" + i + "a", room, p - new Vector3(0.035f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.2f), tubeMat, false);
                var t2 = Box("Tube" + i + "b", room, p + new Vector3(0.035f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.2f), tubeMat, false);
                var lightGo = new GameObject("TubeLight" + i);
                lightGo.transform.SetParent(room, false);
                lightGo.transform.position = p + Vector3.down * 0.4f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.92f, 0.95f, 0.9f);
                l.range = 9f;
                l.intensity = i == 0 ? 2.2f : 1.9f;
                l.shadows = LightShadows.None;
                if (i == 1 || i == 3)
                    flicks.Add(new Sc05FactoryFx.Flicker { light = l, glow = t1.GetComponent<Renderer>(), baseIntensity = l.intensity, seed = i * 7.3f });
                else
                    t2.GetComponent<Renderer>().sharedMaterial = tubeMat;
            }

            // ---- steam / vapor from the machines and pipe vents, plus slow haze
            var steamMat = ParticleMat("Sc05SteamPuff", Sc05Tex("Steam_Puff"), false);
            var wispMat = ParticleMat("Sc05SteamWisp", Sc05Tex("Steam_Wisp"), false);
            var vents = new List<Vector3>();
            foreach (var c in machineCentres)
                if (vents.Count < 5)
                    vents.Add(new Vector3(c.x, 1.05f, c.z));
            if (vents.Count == 0)
                vents.Add(new Vector3(ox + 2f, 1.1f, -3f));
            vents.Add(new Vector3(ox + 0.9f, 2.5f, -2.0f));
            vents.Add(new Vector3(ox + 2.6f, 2.6f, -7.0f));
            for (var i = 0; i < vents.Count; i++)
                Steam(room, "Steam" + i, vents[i], i < vents.Count - 2 ? 0.85f : 0.5f, 3.6f, 0.4f, steamMat, 0.5f);
            // work lights over the machine row so the dye tanks read like in the workflow shots
            for (var i = 0; i < 3; i++)
            {
                var wl = new GameObject("MachineLight" + i);
                wl.transform.SetParent(room, false);
                wl.transform.position = new Vector3(ox + 1.7f + (i % 2) * 0.4f, 2.7f, -2.5f - i * 4f);
                var wlight = wl.AddComponent<Light>();
                wlight.type = LightType.Point;
                wlight.color = new Color(0.95f, 0.96f, 0.9f);
                wlight.range = 7f;
                wlight.intensity = 1.5f;
                wlight.shadows = LightShadows.None;
            }
            Steam(room, "MistLow", new Vector3(ox + 1.2f, 0.25f, -3.5f), 1.4f, 2.2f, 0.06f, wispMat, 0.16f, 6f);
            Steam(room, "MistFar", new Vector3(ox + 1.5f, 0.3f, -11f), 1.8f, 2.2f, 0.06f, wispMat, 0.18f, 7f);
            var dustMat = ParticleMat("Sc05Dust", glowTex, true);
            BuildDust(room, new Vector3(ox + 1.2f, 2.2f, -4f), new Vector3(8f, 3.4f, 12f), dustMat);

            // ---- floor drain, hoses and a few drums so the aisle is not empty
            var barrelBlue = Mat("Sc05BarrelBlue", new Color(0.12f, 0.24f, 0.45f), 0.4f, 0.3f);
            var barrelGrey = Mat("Sc05BarrelGrey", new Color(0.35f, 0.37f, 0.38f), 0.35f, 0.4f);
            var rolls = Mat("Sc05ClothRoll", new Color(0.62f, 0.56f, 0.46f), 0.12f, 0f);
            for (var i = 0; i < 4; i++)
            {
                var bx = ox + 4.4f + (i % 2) * 0.7f;
                var bz = -1.2f - (i / 2) * 0.75f - (i % 2) * 0.2f;
                Cyl("Barrel" + i, room, new Vector3(bx, 0f, bz), 0.88f, 0.29f, i % 3 == 0 ? barrelBlue : barrelGrey);
                Cyl("BarrelLid" + i, room, new Vector3(bx, 0.88f, bz), 0.02f, 0.3f, steel);
            }

            for (var i = 0; i < 3; i++)
            {
                var roll = Cyl("ClothRoll" + i, room, new Vector3(ox + 5.6f, 0f, -3.6f - i * 1.1f), 1.3f, 0.2f, rolls);
                roll.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                roll.transform.position = new Vector3(ox + 5.6f, 0.25f + (i % 2) * 0.4f, -3.6f - i * 1.1f);
            }

            Disc("FloorDrainA", room, new Vector3(ox + 0.4f, 0.01f, -1.6f), 0.4f, 0.01f,
                Mat("Sc05Drain", new Color(0.1f, 0.1f, 0.11f), 0.3f, 0.5f));
            // dye puddles reflect the tubes
            var puddle = Mat("Sc05Puddle", new Color(0.05f, 0.1f, 0.18f), 0.96f, 0.1f);
            for (var i = 0; i < 3; i++)
                Disc("Puddle" + i, room, new Vector3(ox + 1.0f + i * 0.9f, 0.006f, -2.6f - i * 1.7f), 0.9f + i * 0.2f, 0.004f, puddle);

            // ---- life: fans, shaking panels, swaying cloth, flickering tubes, pulsing detector
            var fxGo = new GameObject("FactoryFx");
            fxGo.transform.SetParent(room, false);
            var fx = fxGo.AddComponent<Sc05FactoryFx>();
            var fso = new SerializedObject(fx);
            FillArray(fso.FindProperty("spinners"), fan.Count, (p, i) =>
            {
                p.FindPropertyRelative("target").objectReferenceValue = fan[i].target;
                p.FindPropertyRelative("axis").vector3Value = fan[i].axis;
                p.FindPropertyRelative("degreesPerSecond").floatValue = fan[i].degreesPerSecond;
            });
            FillArray(fso.FindProperty("shakers"), shake.Count, (p, i) =>
            {
                p.FindPropertyRelative("target").objectReferenceValue = shake[i].target;
                p.FindPropertyRelative("amplitude").floatValue = shake[i].amplitude;
                p.FindPropertyRelative("frequency").floatValue = shake[i].frequency;
            });
            FillArray(fso.FindProperty("swayers"), sway.Count, (p, i) =>
            {
                p.FindPropertyRelative("target").objectReferenceValue = sway[i].target;
                p.FindPropertyRelative("degrees").floatValue = sway[i].degrees;
                p.FindPropertyRelative("speed").floatValue = sway[i].speed;
            });
            FillArray(fso.FindProperty("flickers"), flicks.Count, (p, i) =>
            {
                p.FindPropertyRelative("light").objectReferenceValue = flicks[i].light;
                p.FindPropertyRelative("glow").objectReferenceValue = flicks[i].glow;
                p.FindPropertyRelative("baseIntensity").floatValue = flicks[i].baseIntensity;
                p.FindPropertyRelative("seed").floatValue = flicks[i].seed;
            });
            fso.FindProperty("detectorLight").objectReferenceValue = detLight;
            fso.FindProperty("detectorGlow").objectReferenceValue = detGlow;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // ---- machine sound (the director lowers it under every voice-over)
            var ambGo = new GameObject("Sc05Ambience");
            ambGo.transform.SetParent(room, false);
            refs.ambience = ambGo.AddComponent<Sc05Ambience>();
            var aso = new SerializedObject(refs.ambience);
            aso.FindProperty("loop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc05/Machine_Loop.wav");
            aso.ApplyModifiedPropertiesWithoutUndo();

            // ---- inspection logic + spawn (about 0.9 m from the rail so the fingertip reaches the masks)
            var arrow = new GameObject("MaskArrow");
            arrow.transform.SetParent(room, false);
            arrow.SetActive(false);
            var inspectGo = new GameObject("MaskInspect");
            inspectGo.transform.SetParent(room, false);
            refs.masks = inspectGo.AddComponent<MaskInspect>();
            var iso = new SerializedObject(refs.masks);
            iso.FindProperty("art").objectReferenceValue = art;
            iso.FindProperty("arrow").objectReferenceValue = arrow.transform;
            var arr = iso.FindProperty("masks");
            arr.arraySize = 4;
            for (var i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = maskGroups[i];
            iso.ApplyModifiedPropertiesWithoutUndo();
            refs.spawn = Marker("Spawn_SC05", room, new Vector3(ox - 1.0f, 0f, pillarFront + 1.35f), 180f);
            return refs;
        }

        private static Material hangerMat;

        private static Material hanger() =>
            hangerMat != null ? hangerMat : hangerMat = Mat("Sc05Hanger", new Color(0.25f, 0.26f, 0.27f), 0.3f, 0.5f);

        private static void FillArray(SerializedProperty prop, int count, System.Action<SerializedProperty, int> fill)
        {
            prop.arraySize = count;
            for (var i = 0; i < count; i++)
                fill(prop.GetArrayElementAtIndex(i), i);
        }

        // Soft vapor puffs rising from a vent (alpha-blended sprite particles, grows and fades).
        private static void Steam(Transform parent, string name, Vector3 at, float size, float rate, float rise, Material mat,
            float alpha, float life = 4.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(0.86f, 0.88f, 0.88f, alpha);
            main.maxParticles = 36;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = Mathf.Max(0.05f, size * 0.25f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); // all three axes must use the same curve mode
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.3f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.1f)));
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }
    }
}
#endif

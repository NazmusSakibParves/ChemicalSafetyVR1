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
    /// Scenario 8: acid dilution lab, modelled on the Scene 08 workflow. Same shell as the Scenario 6 lab (viewer at Spawn_SC08
    /// facing -Z, viewer's left = +X). Back wall, left to right: safety shower and eye wash in the corner, sink counter with the
    /// Acid Dilution Rules poster, SDS holder, the fume hood with its sliding sash, the control panel. The black-topped island table
    /// holds the white tray with the Distilled Water and Sulfuric Acid beakers. The PPE Donning Station is on the right wall.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc08Refs
        {
            public Transform spawn;
            public Transform tableStand;
            public Transform hoodStand;
            public Transform panelStand;
            public Transform ppeStand;
            public Transform benchStand;
            public Sc06Lab lab;
            public Sc08Mix mix;
            public Sc08Ppe ppe;
            public Sc06Gate gate;
            public Sc06Ambience ambience;
        }

        private const float Sc08Ox = 280f;

        private static GameObject SpawnSc08(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc08/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc08 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        // A quad placed in WORLD coordinates and then parented (QuadObj treats the position as local to the parent).
        private static GameObject Sc08QuadWorld(string name, Transform parent, Vector3 worldPos, Vector3 euler, Vector2 size, Material mat)
        {
            var q = QuadObj(name, null, worldPos, euler, size, mat);
            q.transform.SetParent(parent, true);
            return q;
        }

        private static PokeTarget Sc08Poke(string name, Transform parent, Vector3 worldCentre, Vector3 size, float reach)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = worldCentre;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            var pt = go.AddComponent<PokeTarget>();
            var pso = new SerializedObject(pt);
            pso.FindProperty("reach").floatValue = reach;
            pso.ApplyModifiedPropertiesWithoutUndo();
            return pt;
        }

        private static Sc08Refs BuildSc08Room(Transform env, PresentableArt art)
        {
            const float ox = Sc08Ox;
            var refs = new Sc08Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solidLayer = SolidLayer;
            ConfigureSc06Textures();

            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.80f, 0.82f, 0.84f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.68f, 0.68f, 0.67f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.36f, 0.36f, 0.35f);
            mso.FindProperty("fogColor").colorValue = new Color(0.74f, 0.75f, 0.74f);
            mso.FindProperty("fogStart").floatValue = 9f;
            mso.FindProperty("fogEnd").floatValue = 40f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.35f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            const float zBack = -2.9f;
            const float zFront = 3.4f;
            const float h = 3.2f;
            var x0 = ox - 4.2f;
            var x1 = ox + 3.7f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (zBack + zFront) * 0.5f;
            var w = x1 - x0;
            var d = zFront - zBack;

            var floorMat = Sc06Pbr("Sc06FloorPbr", "Floor", Color.white, new Vector2(w / 2.4f, d / 2.4f), 0.85f, 0.6f);
            // warm cream walls like the designer's picture (the SC06 wall texture, tinted)
            var wallBackMat = Sc06Pbr("Sc08WallBackPbr", "Wall", new Color(1.0f, 0.985f, 0.93f), new Vector2(w / 2.4f, h / 2.4f), 0.8f, 0.6f);
            var wallSideMat = Sc06Pbr("Sc08WallSidePbr", "Wall", new Color(1.0f, 0.985f, 0.93f), new Vector2(d / 2.4f, h / 2.4f), 0.8f, 0.6f);
            var ceilMat = Sc06Pbr("Sc06CeilPbr", "Ceil", Color.white, new Vector2(w / 2.4f, d / 2.4f), 1f, 0.6f, false);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solidLayer >= 0)
                    go.layer = solidLayer;
            }

            var floor = Box("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(cx, h * 0.5f, zBack - 0.06f), new Vector3(w, h, 0.12f), wallBackMat);
            Shell("Wall_Front", new Vector3(cx, h * 0.5f, zFront + 0.06f), new Vector3(w, h, 0.12f), wallBackMat);
            Shell("Wall_Left", new Vector3(x1 + 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), wallSideMat);
            Shell("Wall_Right", new Vector3(x0 - 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), wallSideMat);
            Box("Ceiling", room, new Vector3(cx, h + 0.05f, cz), new Vector3(w, 0.1f, d), ceilMat, false);

            var trim = Mat("Sc08Skirting", new Color(0.40f, 0.42f, 0.42f), 0.3f, 0f);
            Box("SkirtBack", room, new Vector3(cx, 0.06f, zBack + 0.015f), new Vector3(w, 0.12f, 0.03f), trim, false);
            Box("SkirtLeft", room, new Vector3(x1 - 0.015f, 0.06f, cz), new Vector3(0.03f, 0.12f, d), trim, false);
            Box("SkirtRight", room, new Vector3(x0 + 0.015f, 0.06f, cz), new Vector3(0.03f, 0.12f, d), trim, false);

            // ceiling light panels (emissive) with soft lights under them, and the air-conditioning grille
            var panelMat = Mat("Sc06LightPanelGrid", new Color(1.04f, 1.04f, 1.02f), 0f, 0f, Sc06Tex("Label_LightGrid"), null, true);
            var housing = Mat("Sc06PanelFrame", new Color(0.82f, 0.83f, 0.82f), 0.3f, 0f);
            var panelSpots = new[]
            {
                new Vector3(ox + 1.9f, 0f, -0.1f), new Vector3(ox + 1.9f, 0f, -1.9f), new Vector3(ox - 0.6f, 0f, -0.1f),
                new Vector3(ox - 0.6f, 0f, -1.9f), new Vector3(ox - 3.0f, 0f, -0.1f), new Vector3(ox - 3.0f, 0f, -1.9f),
                new Vector3(ox + 1.9f, 0f, 1.7f), new Vector3(ox - 0.6f, 0f, 1.7f)
            };
            for (var i = 0; i < panelSpots.Length; i++)
            {
                var p = panelSpots[i];
                Box("LightFrame" + i, room, new Vector3(p.x, h - 0.012f, p.z), new Vector3(1.26f, 0.025f, 0.66f), housing, false);
                Box("LightPanel" + i, room, new Vector3(p.x, h - 0.026f, p.z), new Vector3(1.16f, 0.01f, 0.58f), panelMat, false);
                if (i < 6)
                {
                    var lg = new GameObject("RoomLight" + i);
                    lg.transform.SetParent(room, false);
                    lg.transform.position = new Vector3(p.x, h - 0.5f, p.z);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.98f, 0.94f);
                    l.range = 6.5f;
                    l.intensity = 1.05f;
                    l.shadows = LightShadows.None;
                }
            }

            Box("AcFrame", room, new Vector3(ox + 0.4f, h - 0.012f, -1.0f), new Vector3(0.84f, 0.025f, 0.84f), housing, false);
            QuadObj("AcDiffuser", room, new Vector3(ox + 0.4f, h - 0.027f, -1.0f), new Vector3(-90f, 0f, 0f), new Vector2(0.78f, 0.78f),
                Sc06Label("Sc06Diffuser", "Label_Diffuser", false, 0.3f));

            var aoMat = MatAlpha("Sc06AO", Sc06Tex("Fx_AOStrip"), new Color(0f, 0f, 0f, 0.42f), false);
            void Ao(string name, Vector3 pos, Vector3 euler, Vector2 size)
            {
                var q = QuadObj(name, room, pos, euler, size, aoMat);
                Object.DestroyImmediate(q.GetComponent<Collider>());
            }

            Ao("AoCeilBack", new Vector3(cx, h - 0.2f, zBack + 0.004f), new Vector3(0f, 180f, 0f), new Vector2(w, 0.4f));
            Ao("AoCeilFront", new Vector3(cx, h - 0.2f, zFront - 0.004f), new Vector3(0f, 0f, 0f), new Vector2(w, 0.4f));
            Ao("AoCeilLeft", new Vector3(x1 - 0.004f, h - 0.2f, cz), new Vector3(0f, 90f, 0f), new Vector2(d, 0.4f));
            Ao("AoCeilRight", new Vector3(x0 + 0.004f, h - 0.2f, cz), new Vector3(0f, -90f, 0f), new Vector2(d, 0.4f));
            Ao("AoFloorBack", new Vector3(cx, 0.3f, zBack + 0.004f), new Vector3(0f, 180f, 180f), new Vector2(w, 0.36f));
            Ao("AoFloorLeft", new Vector3(x1 - 0.004f, 0.3f, cz), new Vector3(0f, 90f, 180f), new Vector2(d, 0.36f));
            Ao("AoFloorRight", new Vector3(x0 + 0.004f, 0.3f, cz), new Vector3(0f, -90f, 180f), new Vector2(d, 0.36f));
            Ao("AoCornerBackLeft", new Vector3(x1 - 0.17f, h * 0.5f, zBack + 0.005f), new Vector3(0f, 180f, 90f), new Vector2(h, 0.34f));
            Ao("AoCornerBackRight", new Vector3(x0 + 0.17f, h * 0.5f, zBack + 0.005f), new Vector3(0f, 180f, -90f), new Vector2(h, 0.34f));

            var steelPaint = Mat("Sc06SteelPaint", new Color(0.72f, 0.74f, 0.76f), 0.55f, 0f);
            var darkSteel = Mat("Sc06DarkSteel", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0f);
            var white = Mat("Sc06Paint", new Color(0.90f, 0.90f, 0.88f), 0.45f, 0f);

            // ---------------------------------------------------------------- left wall: interior window (like the picture) and a door at the front
            var frameGrey = Mat("Sc08WindowFrame", new Color(0.38f, 0.40f, 0.40f), 0.35f, 0f);
            var winGlass = Sc06GlassMat("Sc08WindowGlass", new Color(0.78f, 0.86f, 0.9f), 0.2f, 0.5f);
            var corridor = Mat("Sc08Corridor", new Color(0.85f, 0.87f, 0.86f), 0.1f, 0f, null, new Color(0.55f, 0.58f, 0.58f), false);
            const float winZ = 0.2f;
            Box("WindowBehind", room, new Vector3(x1 + 0.03f, 1.55f, winZ), new Vector3(0.02f, 1.2f, 1.45f), corridor, false);
            Box("WindowGlass", room, new Vector3(x1 - 0.02f, 1.55f, winZ), new Vector3(0.012f, 1.14f, 1.4f), winGlass, false);
            Box("WinFrameT", room, new Vector3(x1 - 0.03f, 2.17f, winZ), new Vector3(0.07f, 0.07f, 1.55f), frameGrey, false);
            Box("WinFrameB", room, new Vector3(x1 - 0.03f, 0.93f, winZ), new Vector3(0.07f, 0.07f, 1.55f), frameGrey, false);
            Box("WinFrameA", room, new Vector3(x1 - 0.03f, 1.55f, winZ - 0.76f), new Vector3(0.07f, 1.25f, 0.07f), frameGrey, false);
            Box("WinFrameB2", room, new Vector3(x1 - 0.03f, 1.55f, winZ + 0.76f), new Vector3(0.07f, 1.25f, 0.07f), frameGrey, false);
            Box("WinSill", room, new Vector3(x1 - 0.08f, 0.9f, winZ), new Vector3(0.14f, 0.03f, 1.62f), white, false);

            // ---------------------------------------------------------------- back wall dressing: SDS holder (as in SC06) and the Acid Dilution Rules poster
            var posterMat = Sc06Label("Sc08RulesPoster", "Label_Sc08Rules", true, 0.35f);
            Box("RulesPosterBack", room, new Vector3(ox + 1.95f, 1.78f, zBack + 0.006f), new Vector3(0.9f, 1.3f, 0.012f), white, false);
            QuadObj("PosterRules", room, new Vector3(ox + 1.95f, 1.78f, zBack + 0.016f), new Vector3(0f, 180f, 0f),
                new Vector2(0.88f, 1.268f), posterMat);
            QuadObj("PosterSds", room, new Vector3(ox + 0.35f, 1.55f, zBack + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.40f, 0.56f), Sc06Label("Sc06SdsPoster", "Label_SdsPoster"));
            QuadObj("SdsPocket", room, new Vector3(ox + 0.78f, 1.42f, zBack + 0.03f), new Vector3(0f, 180f, 0f),
                new Vector2(0.34f, 0.34f), Sc06Label("Sc06SdsPocket", "Label_SdsPocket", true));
            Box("SdsPocketBack", room, new Vector3(ox + 0.78f, 1.38f, zBack + 0.012f), new Vector3(0.28f, 0.26f, 0.02f), darkSteel, false);
            Sc06Disc("ClockRim", room, new Vector3(ox + 3.0f, 2.65f, zBack + 0.025f), 0.34f, 0.02f, darkSteel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var clockFace = Mat("Sc06ClockFace", new Color(0.96f, 0.96f, 0.94f), 0.2f, 0f);
            Sc06Disc("ClockFace", room, new Vector3(ox + 3.0f, 2.65f, zBack + 0.037f), 0.31f, 0.01f, clockFace).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Box("ClockH", room, new Vector3(ox + 3.0f, 2.67f, zBack + 0.045f), new Vector3(0.012f, 0.08f, 0.004f), darkSteel, false);
            Box("ClockM", room, new Vector3(ox + 3.03f, 2.65f, zBack + 0.045f), new Vector3(0.1f, 0.01f, 0.004f), darkSteel, false);
            Disc("SmokeDetector", room, new Vector3(ox - 1.7f, h - 0.03f, 0.9f), 0.14f, 0.025f, white);

            // ---------------------------------------------------------------- the fume hood (FumeHood.glb), as in Scenario 6
            const float hoodX = -1.15f;
            var hoodRoot = SpawnSc06("FumeHood.glb", room);
            float surfaceY = 0.9f, sashW = 1.7f, sashH = 1.2f, sashZ = zBack + 1.0f, hoodCentreX = ox + hoodX;
            float interiorBack = zBack + 0.1f;
            float hoodFrontZ = zBack + 1.0f;
            float interiorDepth = 0.8f;
            if (hoodRoot != null)
            {
                hoodRoot.name = "FumeHood";
                MeshFilter hoodMf = null;
                MeshRenderer hoodMr = null;
                foreach (var mf in hoodRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.subMeshCount > 3)
                    {
                        hoodMf = mf;
                        hoodMr = mf.GetComponent<MeshRenderer>();
                    }
                }

                if (hoodMf != null && Sc06SubBounds(hoodMf, hoodMr, "body", out var bb) && Sc06SubBounds(hoodMf, hoodMr, "Glass", out var gb))
                {
                    var v = gb.center - bb.center;
                    v.y = 0f;
                    if (v.sqrMagnitude > 1e-6f)
                        hoodRoot.transform.rotation = Quaternion.FromToRotation(v.normalized, Vector3.forward) * hoodRoot.transform.rotation;
                    Sc06SubBounds(hoodMf, hoodMr, "body", out bb);
                    hoodRoot.transform.localScale *= 2.7f / Mathf.Max(0.01f, bb.size.y);
                    Sc06SubBounds(hoodMf, hoodMr, "body", out bb);
                    hoodRoot.transform.position += new Vector3(ox + hoodX - bb.center.x, -bb.min.y, zBack + 0.02f - bb.min.z);
                    Sc06SubBounds(hoodMf, hoodMr, "body", out bb);
                    Sc06SubBounds(hoodMf, hoodMr, "Glass", out gb);
                    if (Sc06SubBounds(hoodMf, hoodMr, "Material.001", out var wb))
                    {
                        surfaceY = wb.max.y;
                        interiorBack = wb.min.z;
                        interiorDepth = wb.size.z;
                        hoodCentreX = wb.center.x;
                    }

                    hoodFrontZ = bb.max.z;
                    sashW = Mathf.Max(0.8f, gb.size.x);
                    sashH = Mathf.Max(0.6f, gb.size.y);
                    sashZ = gb.center.z;
                    var hoodPaint = Mat("Sc06HoodPaint", new Color(0.90f, 0.91f, 0.89f), 0.5f, 0f);
                    var hoodInner = Mat("Sc06HoodInner", new Color(0.64f, 0.71f, 0.75f), 0.45f, 0f);
                    var slot = Mat("Sc06HoodSlot", new Color(0.56f, 0.62f, 0.66f), 0.35f, 0f);
                    var mats = hoodMr.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var mn = mats[i] != null ? mats[i].name : "";
                        mats[i] = mn == "Material.001" ? hoodInner : mn == "air_in" ? slot : hoodPaint;
                    }

                    hoodMr.sharedMaterials = mats;
                    Sc06DropSubmeshes(hoodMf, hoodMr, "Mesh_Sc06HoodBody", "Glass", "frame", "Material.002");
                }
                else
                {
                    Debug.LogWarning("[SC08] hood mesh not found or sub-meshes renamed");
                }

                var knobGreen = Mat("Sc06KnobGreen", new Color(0.18f, 0.5f, 0.28f), 0.5f, 0.1f);
                var knobBlue = Mat("Sc06KnobBlue", new Color(0.2f, 0.32f, 0.6f), 0.5f, 0.1f);
                var knobRed = Mat("Sc06KnobRed", new Color(0.7f, 0.12f, 0.12f), 0.5f, 0.1f);
                foreach (var r in hoodRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.StartsWith("Cylinder"))
                    {
                        var idx = r.name == "Cylinder" ? 0 : int.Parse(r.name.Substring("Cylinder.".Length));
                        var km = idx % 3 == 0 ? knobGreen : idx % 3 == 1 ? knobBlue : knobRed;
                        var km2 = r.sharedMaterials;
                        for (var ki = 0; ki < km2.Length; ki++)
                            km2[ki] = km;
                        r.sharedMaterials = km2;
                    }
                    else if (r.name == "Cube")
                    {
                        r.gameObject.SetActive(false);
                    }
                }
            }

            var sashGo = new GameObject("Sash");
            sashGo.transform.SetParent(room, false);
            sashGo.transform.position = new Vector3(hoodCentreX, surfaceY + 0.05f + sashH * 0.5f, sashZ);
            var sash = sashGo.transform;
            var glass = Sc06GlassMat("Sc06SashGlass", new Color(0.80f, 0.90f, 0.95f), 0.05f, 0.55f);
            var sashGlass = Box("SashGlass", sash, sashGo.transform.position, new Vector3(sashW - 0.1f, sashH - 0.1f, 0.01f), glass, false);
            sashGlass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var frameMat = Mat("Sc06SashFrame", new Color(0.86f, 0.87f, 0.86f), 0.5f, 0f);
            var sp = sashGo.transform.position;
            Box("SashTop", sash, sp + new Vector3(0f, sashH * 0.5f - 0.03f, 0f), new Vector3(sashW, 0.06f, 0.05f), frameMat, false);
            Box("SashBottom", sash, sp - new Vector3(0f, sashH * 0.5f - 0.03f, 0f), new Vector3(sashW, 0.06f, 0.05f), frameMat, false);
            Box("SashL", sash, sp + new Vector3(sashW * 0.5f - 0.025f, 0f, 0f), new Vector3(0.05f, sashH, 0.05f), frameMat, false);
            Box("SashR", sash, sp - new Vector3(sashW * 0.5f - 0.025f, 0f, 0f), new Vector3(0.05f, sashH, 0.05f), frameMat, false);
            Box("SashHandle", sash, sp - new Vector3(0f, sashH * 0.5f - 0.1f, -0.045f), new Vector3(0.34f, 0.025f, 0.03f), steelPaint, false);
            QuadObj("SashStrip", sash, sp - new Vector3(0f, sashH * 0.5f - 0.03f, -0.0265f), new Vector3(0f, 180f, 0f),
                new Vector2(sashW - 0.1f, 0.05f), Sc06Label("Sc06SashStrip", "Label_Sash", true, 0.1f));
            var plateOnMat = Sc06Label("Sc06HoodPlate", "Label_HoodPlate");
            var plateOffMat = Sc06Label("Sc06HoodPlateOff", "Label_HoodPlateOff");
            QuadObj("HoodPlate", room, new Vector3(hoodCentreX + 0.55f, surfaceY + sashH + 0.28f, sashZ + 0.03f), new Vector3(0f, 180f, 0f),
                new Vector2(0.4f, 0.15f), plateOffMat);

            // hood interior: lamp, back-wall tint, a few vessels at the sides, a rubber mat for the tray
            var tubeMat = Mat("Sc06HoodTube", new Color(0.95f, 0.98f, 1f), 0.2f, 0f, null, new Color(1.3f, 1.38f, 1.45f), false);
            var tubeOffMat = Mat("Sc06HoodTubeOff", new Color(0.72f, 0.74f, 0.76f), 0.2f, 0f);
            Box("HoodTube", room, new Vector3(hoodCentreX, surfaceY + sashH - 0.04f, sashZ - 0.1f), new Vector3(sashW - 0.3f, 0.03f, 0.05f), tubeOffMat, false);
            var tint = Mat("Sc06HoodBackTint", new Color(0.62f, 0.78f, 0.88f), 0.3f, 0f, null, new Color(0.18f, 0.26f, 0.32f), false);
            Box("HoodBackTint", room, new Vector3(hoodCentreX, surfaceY + sashH * 0.5f, interiorBack + 0.015f), new Vector3(sashW - 0.15f, sashH, 0.01f), tint, false);
            var hl = new GameObject("HoodLight");
            hl.transform.SetParent(room, false);
            hl.transform.position = new Vector3(hoodCentreX, surfaceY + sashH - 0.1f, sashZ - 0.25f);
            var hoodLight = hl.AddComponent<Light>();
            hoodLight.type = LightType.Point;
            hoodLight.color = new Color(0.86f, 0.94f, 1f);
            hoodLight.range = 2.6f;
            hoodLight.intensity = 0.45f;
            hoodLight.shadows = LightShadows.None;
            hoodLight.enabled = false;
            Sc08HoodContents(room, hoodCentreX, surfaceY, interiorBack, sashZ);
            Box("SillVent", room, new Vector3(hoodCentreX, surfaceY + 0.004f, sashZ + 0.06f), new Vector3(sashW - 0.2f, 0.008f, 0.03f), darkSteel, false);
            Box("BaffleSlot", room, new Vector3(hoodCentreX, surfaceY + 0.12f, interiorBack + 0.04f), new Vector3(sashW - 0.3f, 0.05f, 0.012f), darkSteel, false);

            // ---------------------------------------------------------------- control panel (right of the hood)
            const float panelX = -2.62f;
            const float panelY = 1.55f;
            var panelZ = hoodFrontZ;
            var pillarMat = Sc06Pbr("Sc08PillarPbr", "Wall", new Color(1.0f, 0.985f, 0.93f), new Vector2(0.5f, h / 2.4f), 0.8f, 0.6f);
            Shell("PanelPillar", new Vector3(ox + panelX, h * 0.5f, (zBack + panelZ) * 0.5f), new Vector3(0.7f, h, panelZ - zBack), pillarMat);
            var panelBack = Box("PanelBox", room, new Vector3(ox + panelX, panelY, panelZ + 0.03f), new Vector3(0.40f, 0.56f, 0.06f), steelPaint, false);
            QuadObj("PanelFace", room, new Vector3(ox + panelX, panelY, panelZ + 0.062f), new Vector3(0f, 180f, 0f),
                new Vector2(0.36f, 0.54f), Sc06Label("Sc06PanelFace", "Label_Panel", false, 0.35f));
            panelBack.name = "PanelBox";
            var ledRed = Mat("Sc06LedRed", new Color(0.55f, 0.03f, 0.02f), 0.5f, 0f, null, new Color(5.0f, 0.2f, 0.12f), false);
            var ledGreen = Mat("Sc06LedGreen", new Color(0.03f, 0.5f, 0.08f), 0.5f, 0f, null, new Color(0.3f, 5.0f, 0.7f), false);
            var ledBezel = Mat("Sc06LedBezel", new Color(0.10f, 0.10f, 0.11f), 0.6f, 0.3f);
            var ledGlowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var ledGlowGreen = MatAlpha("Sc06LedGlowGreen", ledGlowTex, new Color(0.2f, 1f, 0.35f, 0.7f), true);
            var ledGlowRed = MatAlpha("Sc06LedGlowRed", ledGlowTex, new Color(1f, 0.15f, 0.1f, 0.6f), true);
            var rockerMat = Mat("Sc06Rocker", new Color(0.12f, 0.13f, 0.14f), 0.5f, 0f);
            var rockerPlate = Mat("Sc06RockerPlate", new Color(0.85f, 0.86f, 0.84f), 0.4f, 0f);
            const float k = 0.36f / 640f;
            var startOn = new[] { false, false, true };
            for (var i = 0; i < 3; i++)
            {
                var top = 20f + i * 312f;
                float Wx(float px) => ox + panelX - (px - 320f) * k;
                float Wy(float py) => panelY + (480f - py) * k;
                var rockerAt = new Vector3(Wx(150f), Wy(top + 206f), panelZ + 0.075f);
                Box("RockerPlate" + i, room, rockerAt, new Vector3(0.06f, 0.078f, 0.014f), rockerPlate, false);
                var rocker = Box("Rocker" + i, room, rockerAt + new Vector3(0f, 0f, 0.016f), new Vector3(0.040f, 0.07f, 0.022f), rockerMat, false);
                rocker.transform.rotation = Quaternion.Euler(startOn[i] ? 16f : -16f, 0f, 0f);
                var ledAt = new Vector3(Wx(470f), Wy(top + 206f), panelZ + 0.078f);
                var led = Prim("Led" + i, PrimitiveType.Sphere, room, Vector3.zero, Vector3.one * 0.05f, startOn[i] ? ledGreen : ledRed);
                led.transform.position = ledAt;
                led.transform.localScale = new Vector3(0.05f, 0.05f, 0.03f);
                Sc06Disc("LedBezel" + i, room, ledAt - new Vector3(0f, 0f, 0.004f), 0.074f, 0.005f, ledBezel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var ledGlow = QuadObj("LedGlow" + i, room, ledAt + new Vector3(0f, 0f, 0.008f), new Vector3(0f, 180f, 0f), new Vector2(0.17f, 0.17f), startOn[i] ? ledGlowGreen : ledGlowRed);
                Object.DestroyImmediate(ledGlow.GetComponent<Collider>());
            }

            Box("WallSocket", room, new Vector3(ox + hoodX + 1.45f, 1.05f, zBack + 0.015f), new Vector3(0.09f, 0.09f, 0.025f), white, false);

            // ---------------------------------------------------------------- sink counter (left of the hood), safety shower and eye wash in the left corner
            const float counterTop = 0.9f;
            var cX0 = ox + 0.95f;
            var cX1 = ox + 2.85f;
            var counterBody = Mat("Sc08CounterBody", new Color(0.78f, 0.79f, 0.77f), 0.4f, 0f);
            var epoxy = Mat("Sc08Epoxy", new Color(0.055f, 0.057f, 0.06f), 0.62f, 0f);
            Box("CounterBody", room, new Vector3((cX0 + cX1) * 0.5f, 0.44f, zBack + 0.31f), new Vector3(cX1 - cX0, 0.88f, 0.62f), counterBody);
            Box("CounterTop", room, new Vector3((cX0 + cX1) * 0.5f, counterTop - 0.02f, zBack + 0.33f), new Vector3(cX1 - cX0 + 0.03f, 0.04f, 0.68f), epoxy, false);
            Box("CounterSplash", room, new Vector3((cX0 + cX1) * 0.5f, counterTop + 0.05f, zBack + 0.02f), new Vector3(cX1 - cX0, 0.1f, 0.02f), epoxy, false);
            for (var i = 0; i <= 3; i++)
            {
                var sx = Mathf.Lerp(cX0 + 0.02f, cX1 - 0.02f, i / 3f);
                Box("CounterSeam" + i, room, new Vector3(sx, 0.44f, zBack + 0.625f), new Vector3(0.012f, 0.8f, 0.01f), darkSteel, false);
                if (i < 3)
                    Box("CounterHandle" + i, room, new Vector3(sx + (cX1 - cX0) / 6f - 0.06f, 0.66f, zBack + 0.635f), new Vector3(0.1f, 0.018f, 0.02f), steelPaint, false);
            }

            // sink basin (steel) and gooseneck tap
            var sinkSteel = Mat("Sc08SinkSteel", new Color(0.74f, 0.76f, 0.78f), 0.7f, 0.1f);
            Box("SinkBasin", room, new Vector3(ox + 1.7f, counterTop + 0.002f, zBack + 0.34f), new Vector3(0.62f, 0.006f, 0.42f), sinkSteel, false);
            Cyl("TapPost", room, new Vector3(ox + 1.7f, counterTop, zBack + 0.1f), 0.28f, 0.013f, sinkSteel);
            Box("TapArm", room, new Vector3(ox + 1.7f, counterTop + 0.28f, zBack + 0.18f), new Vector3(0.022f, 0.022f, 0.16f), sinkSteel, false);

            var shower = SpawnSc08("SafetyShower.glb", room);
            if (shower != null)
            {
                shower.name = "SafetyShower";
                FitHeight(shower, 2.45f);
                var sb = BoundsOfTree(shower.transform);
                shower.transform.position += new Vector3(ox + 3.25f - sb.center.x, -sb.min.y, zBack + 0.28f - sb.center.z);
                var shMat = Mat("Sc08ShowerGreen", new Color(0.12f, 0.46f, 0.30f), 0.5f, 0.1f);
                foreach (var r in shower.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == null)
                        r.sharedMaterial = shMat;
                Debug.Log("[SC08] shower bounds " + BoundsOfTree(shower.transform).min.ToString("F2") + " " + BoundsOfTree(shower.transform).max.ToString("F2"));
            }

            QuadObj("ShowerSign", room, new Vector3(ox + 3.22f, 1.95f, zBack + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.3f, 0.39f),
                Sc06Label("Sc08ShowerSign", "Label_Sc08ShowerSign", true, 0.3f));
            QuadObj("EyewashSign", room, new Vector3(ox + 3.22f, 1.5f, zBack + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.3f, 0.39f),
                Sc06Label("Sc08EyewashSign", "Label_Sc08EyewashSign", true, 0.3f));
            var hazard = Mat("Sc06HazardTape", new Color(0.9f, 0.76f, 0.1f), 0.3f, 0f);
            Box("ShowerFloorMark", room, new Vector3(ox + 3.0f, 0.004f, zBack + 0.9f), new Vector3(1.0f, 0.004f, 0.05f), hazard, false);
            Box("FloorTape", room, new Vector3(ox + hoodX, 0.004f, zBack + 1.5f), new Vector3(2.4f, 0.004f, 0.05f), hazard, false);

            // ---------------------------------------------------------------- the island table (black epoxy top, grey drawers, like the picture)
            const float tableX = -1.15f;
            const float tableZ = -0.35f;
            var table = SpawnSc06("Table.glb", room);
            var tableTop = 0.92f;
            var cabinetMat = Mat("Sc08Cabinet", new Color(0.70f, 0.71f, 0.70f), 0.42f, 0f);
            if (table != null)
            {
                table.name = "IslandTable";
                Renderer cabinetR = null;
                var thin = new List<Renderer>();
                foreach (var r in table.GetComponentsInChildren<Renderer>(true))
                {
                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.Contains("Scratched"))
                        thin.Add(r);
                    else
                        cabinetR = r;
                }

                var tb = BoundsOfTree(table.transform);
                if (tb.size.z > tb.size.x)
                    table.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * table.transform.rotation;
                tb = BoundsOfTree(table.transform);
                table.transform.localScale *= 2.1f / Mathf.Max(0.01f, tb.size.x);
                tb = BoundsOfTree(table.transform);
                if (cabinetR != null && cabinetR.bounds.center.x < tb.center.x)
                {
                    table.transform.RotateAround(tb.center, Vector3.up, 180f);
                    tb = BoundsOfTree(table.transform);
                }

                table.transform.position += new Vector3(ox + tableX - tb.center.x, -tb.min.y, tableZ - tb.center.z);
                tb = BoundsOfTree(table.transform);
                tableTop = tb.max.y;
                foreach (var r in thin)
                    r.gameObject.SetActive(false);
                var cabinetBounds = new Bounds(Vector3.zero, Vector3.zero);
                if (cabinetR != null)
                {
                    var ms = cabinetR.sharedMaterials;
                    for (var mi = 0; mi < ms.Length; mi++)
                        ms[mi] = cabinetMat;
                    cabinetR.sharedMaterials = ms;
                    cabinetBounds = cabinetR.bounds;
                }

                Sc06SolidTable(room, new Vector3(ox + tableX, tableTop, tableZ), cabinetBounds, epoxy);
            }

            // ---------------------------------------------------------------- on the table: the white transparent tray, notebook, goggles
            var T = tableTop;
            var trayCentre = new Vector3(ox + tableX, T, -0.12f);
            var tray = Sc08BuildTray(room, trayCentre, art, out var trayParts);

            QuadObj("Notebook", room, new Vector3(ox + tableX - 0.62f, T + 0.006f, 0.22f), new Vector3(90f, 12f, 0f), new Vector2(0.21f, 0.27f),
                Sc06Label("Sc08Notebook", "Label_Sc08Notebook", false, 0.2f));
            var glassMat = Sc06GlassMat("Sc06BenchGlass", new Color(0.82f, 0.92f, 0.95f), 0.07f, 0.75f);
            Sc08Goggles(room, "TableGoggles", new Vector3(ox + tableX + 0.62f, T + 0.034f, 0.18f), 0.8f, Quaternion.Euler(0f, 200f, 0f), true);
            var shadowMat = ShadowMat(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png"));
            void Shadow(string name, float sx, float sz, float w2, float d2, float y)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = name;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(room, true);
                q.transform.position = new Vector3(sx, y + 0.0015f, sz);
                q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(w2, d2, 1f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = shadowMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            Shadow("ShadowTray", trayCentre.x, trayCentre.z, 0.88f, 0.64f, T);
            Shadow("ShadowGoggles", ox + tableX + 0.62f, 0.18f, 0.22f, 0.14f, T);
            Shadow("ShadowNotebook", ox + tableX - 0.62f, 0.22f, 0.3f, 0.36f, T);

            // the tray's resting place in the hood and the hood-side poke volumes
            var trayRest = new Vector3(hoodCentreX, surfaceY + 0.004f, sashZ - 0.2f);
            var rest = new GameObject("TrayRest").transform;
            rest.SetParent(room, true);
            rest.position = trayRest;
            Box("HoodRubberMat", room, new Vector3(trayRest.x, surfaceY + 0.004f, trayRest.z), new Vector3(0.9f, 0.006f, 0.62f), Mat("Sc06Rubber", new Color(0.05f, 0.05f, 0.055f), 0.15f, 0f), false);
            Shadow("ShadowHoodTray", trayRest.x, trayRest.z, 0.88f, 0.64f, surfaceY + 0.006f);

            // ---------------------------------------------------------------- PPE donning station on the right wall
            refs.ppe = Sc08BuildPpeStation(room, x0, art, out refs.ppeStand);

            // ---------------------------------------------------------------- an extinguisher, as in the other labs
            var ext = SpawnSc05("Extinguisher.glb", room);
            if (ext != null)
            {
                ext.name = "Extinguisher";
                FitHeight(ext, 0.72f);
                var eb = BoundsOfTree(ext.transform);
                ext.transform.position += new Vector3((ox + 3.55f) - eb.center.x, 0.0f - eb.min.y, (1.6f) - eb.center.z);
            }

            // ---------------------------------------------------------------- interaction objects
            for (var i = 0; i < 3; i++)
            {
                var rk = room.Find("Rocker" + i);
                if (rk != null)
                    Sc08Poke("SwitchPoke" + i, room, rk.position + new Vector3(0f, 0f, 0.05f), new Vector3(0.11f, 0.17f, 0.16f), 0.05f);
            }

            var sashCentre = sash.position;
            Sc08Poke("SashPoke", room, sashCentre + new Vector3(0f, 0f, 0.07f), new Vector3(sashW, sashH, 0.16f), 0.05f);
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var flash = QuadObj("SashFlash", room, sashCentre + new Vector3(0f, 0f, 0.03f), new Vector3(0f, 180f, 0f), new Vector2(sashW, sashH),
                MatAlpha("Sc06SashFlash", null, new Color(1f, 0.15f, 0.12f, 0f), false));
            flash.SetActive(false);
            var stripPivot = new GameObject("Strip").transform;
            stripPivot.SetParent(sash, true);
            stripPivot.position = sashCentre + new Vector3(sashW * 0.28f, sashH * 0.5f - 0.06f, 0.035f);
            var stripQuad = QuadObj("StripPaper", stripPivot, Vector3.zero, new Vector3(0f, 180f, 0f), new Vector2(0.035f, 0.2f),
                Mat("Sc06StripPaper", new Color(0.96f, 0.96f, 0.92f), 0.2f, 0f));
            stripQuad.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            Object.DestroyImmediate(stripQuad.GetComponent<Collider>());
            var stripBack = Object.Instantiate(stripQuad, stripPivot);
            stripBack.name = "StripPaperBack";
            stripBack.transform.localRotation = Quaternion.identity;

            // tray: touch volume + glow on the table, touch volume + ring over the water beaker (they travel with the tray)
            var trayPoke = Sc08Poke("TrayPoke", tray, trayCentre + new Vector3(0f, 0.12f, 0.04f), new Vector3(0.9f, 0.4f, 0.7f), 0.05f);
            var trayGlow = Sc08QuadWorld("TrayGlow", tray, trayCentre + new Vector3(0f, 0.26f, 0.12f), new Vector3(0f, 180f, 0f), new Vector2(0.7f, 0.7f),
                MatAlpha("Sc08TrayGlow", glowTex, new Color(0.2f, 1f, 0.4f, 0.55f), true));
            Object.DestroyImmediate(trayGlow.GetComponent<Collider>());
            trayGlow.SetActive(false);
            var waterAt = trayParts.waterBase;
            Sc08Poke("WaterPoke", tray, waterAt + new Vector3(0f, 0.13f, 0.14f), new Vector3(0.34f, 0.40f, 0.5f), 0.06f);
            var ring = Sc08QuadWorld("WaterRing", tray, waterAt + Vector3.up * (0.115f * 1.8f + 0.012f), new Vector3(90f, 0f, 0f), new Vector2(1f, 1f),
                MatAlpha("Sc08Ring", Sc06Tex("Fx_Ripple"), new Color(0.25f, 1f, 0.45f, 0.8f), true));
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.SetActive(false);
            var wispMat = ParticleMat("Sc06Wisp", Sc06Tex("Fx_Wisp"), false);
            var vaporMat = ParticleMat("Sc06Vapor", Sc06Tex("Fx_Vapor"), false);
            // a thin heat haze over the water beaker while the acid goes in (switched on by Sc08Mix)
            Steam(tray, "MixWisp", waterAt + Vector3.up * (0.115f * 1.8f), 0.05f, 3f, 0.14f, wispMat, 0.2f, 2.6f);

            // ---------------------------------------------------------------- logic: lab, mix, gate, sounds
            var ambGo = new GameObject("Sc08Ambience");
            ambGo.transform.SetParent(room, false);
            refs.ambience = ambGo.AddComponent<Sc06Ambience>();
            var audioDir = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc06/";
            var aso = new SerializedObject(refs.ambience);
            aso.FindProperty("roomLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Room_Loop.wav");
            aso.FindProperty("hoodLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Hood_Loop.wav");
            aso.FindProperty("clickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Switch_Click.wav");
            aso.FindProperty("slideClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Sash_Slide.wav");
            aso.FindProperty("pourLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Pour_Loop.wav");
            aso.ApplyModifiedPropertiesWithoutUndo();

            var labGo = new GameObject("Sc08Lab");
            labGo.transform.SetParent(room, false);
            refs.lab = labGo.AddComponent<Sc06Lab>();
            var lso = new SerializedObject(refs.lab);
            lso.FindProperty("art").objectReferenceValue = art;
            lso.FindProperty("ambience").objectReferenceValue = refs.ambience;
            lso.FindProperty("ledOn").objectReferenceValue = ledGreen;
            lso.FindProperty("ledOff").objectReferenceValue = ledRed;
            lso.FindProperty("glowOn").objectReferenceValue = ledGlowGreen;
            lso.FindProperty("glowOff").objectReferenceValue = ledGlowRed;
            lso.FindProperty("tubeOn").objectReferenceValue = tubeMat;
            lso.FindProperty("tubeOff").objectReferenceValue = tubeOffMat;
            lso.FindProperty("plateOn").objectReferenceValue = plateOnMat;
            lso.FindProperty("plateOff").objectReferenceValue = plateOffMat;
            lso.FindProperty("streakMaterial").objectReferenceValue = ParticleMat("Sc06Streak", Sc06Tex("Fx_Streak"), false);
            lso.FindProperty("puffMaterial").objectReferenceValue = vaporMat;
            lso.FindProperty("wispMaterial").objectReferenceValue = wispMat;
            lso.FindProperty("vaporScale").floatValue = 0.55f;
            var hb = new Bounds(new Vector3(hoodCentreX, surfaceY + sashH * 0.5f, (interiorBack + sashZ) * 0.5f),
                new Vector3(sashW - 0.15f, sashH, sashZ - interiorBack));
            lso.FindProperty("hoodBounds").boundsValue = hb;
            lso.ApplyModifiedPropertiesWithoutUndo();

            var gateGo = new GameObject("Sc08GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc08Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc08Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc08Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            refs.mix = labGo.AddComponent<Sc08Mix>();
            var mso2 = new SerializedObject(refs.mix);
            mso2.FindProperty("lab").objectReferenceValue = refs.lab;
            mso2.FindProperty("ambience").objectReferenceValue = refs.ambience;
            mso2.FindProperty("streamMaterial").objectReferenceValue = MatAlpha("Sc08Stream", null, new Color(0.96f, 0.93f, 0.7f, 0.55f), false);
            mso2.FindProperty("dropMaterial").objectReferenceValue = ParticleMat("Sc06Drop", Sc06Tex("Fx_Drop"), false);
            mso2.FindProperty("rippleMaterial").objectReferenceValue = MatAlpha("Sc06Ripple", Sc06Tex("Fx_Ripple"), Color.white, false);
            mso2.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- spawn and stands
            refs.spawn = Marker("Spawn_SC08", room, new Vector3(ox + tableX + 0.1f, 0f, 1.35f), 180f);
            refs.tableStand = Marker("Stand_SC08_Table", room, new Vector3(ox + tableX + 0.1f, 0f, 1.35f), 180f);
            refs.benchStand = Marker("Stand_SC08_Bench", room, new Vector3(ox + tableX + 0.1f, 0f, 0.62f), 180f);
            refs.panelStand = Marker("Stand_SC08_Panel", room, new Vector3(ox - 2.15f, 0f, -1.3f), 180f);
            refs.hoodStand = Marker("Stand_SC08_Hood", room, new Vector3(ox - 1.2f, 0f, -1.1f), 180f);
            return refs;
        }

        // A few vessels in the hood, left and right of the tray (as in the picture: flask on the left, retort stand on the right).
        private static void Sc08HoodContents(Transform room, float cx, float surfaceY, float backZ, float sashZ)
        {
            var steel = Mat("Sc06StandSteel", new Color(0.70f, 0.72f, 0.74f), 0.75f, 0.1f);
            var clampMat = Mat("Sc06Clamp", new Color(0.10f, 0.10f, 0.11f), 0.4f, 0f);
            var glass = Sc06GlassMat("Sc06HoodGlass", new Color(0.82f, 0.92f, 0.96f), 0.07f, 0.8f);
            var rubber = Mat("Sc06Rubber", new Color(0.05f, 0.05f, 0.055f), 0.15f, 0f);
            var z0 = backZ + 0.3f;
            Box("RubberMat0", room, new Vector3(cx + 0.66f, surfaceY + 0.006f, z0), new Vector3(0.24f, 0.012f, 0.24f), rubber, false);
            Box("RubberMat1", room, new Vector3(cx - 0.62f, surfaceY + 0.006f, z0), new Vector3(0.24f, 0.012f, 0.24f), rubber, false);
            Sc06Mesh("FlaskA", room, Sc06RoundFlaskMesh(), glass, new Vector3(cx + 0.66f, surfaceY + 0.012f, z0), null, Vector3.one * 0.9f);
            Sc06Mesh("FlaskB", room, Sc06ConicalMesh(), glass, new Vector3(cx + 0.5f, surfaceY + 0.006f, z0 + 0.26f), null, Vector3.one * 0.9f);
            Box("StandBase", room, new Vector3(cx - 0.62f, surfaceY + 0.008f, z0 - 0.04f), new Vector3(0.18f, 0.016f, 0.12f), clampMat, false);
            Cyl("StandRod", room, new Vector3(cx - 0.62f, surfaceY + 0.016f, z0 - 0.06f), 0.62f, 0.006f, steel);
            Box("Clamp", room, new Vector3(cx - 0.62f, surfaceY + 0.34f, z0 - 0.0f), new Vector3(0.05f, 0.025f, 0.12f), clampMat, false);
            Sc06ChemGroup(room, "Beaker", new[] { "glass", "liquid" }, new Vector3(cx - 0.62f, surfaceY + 0.012f, z0 + 0.12f), 0f);
        }

        // The white transparent tray (thin plastic walls, rolled rim) with the Distilled Water beaker (two rods) and the Sulfuric Acid beaker.
        private struct Sc08TrayParts
        {
            public Vector3 waterBase;
            public Vector3 acidBase;
        }

        private static Transform Sc08BuildTray(Transform room, Vector3 at, PresentableArt art, out Sc08TrayParts parts)
        {
            var root = new GameObject("Sc08Tray").transform;
            root.SetParent(room, true);
            root.position = at;
            var trayGlass = Sc06GlassMat("Sc08TrayGlass", new Color(0.93f, 0.96f, 0.98f), 0.14f, 0.9f);
            var rim = Mat("Sc08TrayRim", new Color(0.88f, 0.9f, 0.9f, 1f), 0.5f, 0f);
            const float L = 0.74f, D = 0.48f, H = 0.08f;
            Box("TrayFloor", root, at + new Vector3(0f, 0.006f, 0f), new Vector3(L, 0.012f, D), trayGlass, false);
            Box("TrayWallF", root, at + new Vector3(0f, H * 0.5f, D * 0.5f - 0.004f), new Vector3(L, H, 0.008f), trayGlass, false);
            Box("TrayWallB", root, at + new Vector3(0f, H * 0.5f, -D * 0.5f + 0.004f), new Vector3(L, H, 0.008f), trayGlass, false);
            Box("TrayWallL", root, at + new Vector3(L * 0.5f - 0.004f, H * 0.5f, 0f), new Vector3(0.008f, H, D), trayGlass, false);
            Box("TrayWallR", root, at + new Vector3(-L * 0.5f + 0.004f, H * 0.5f, 0f), new Vector3(0.008f, H, D), trayGlass, false);
            // rolled rim (frosted so the outline reads on the dark table)
            Box("TrayRimF", root, at + new Vector3(0f, H, D * 0.5f - 0.006f), new Vector3(L + 0.01f, 0.012f, 0.02f), rim, false);
            Box("TrayRimB", root, at + new Vector3(0f, H, -D * 0.5f + 0.006f), new Vector3(L + 0.01f, 0.012f, 0.02f), rim, false);
            Box("TrayRimL", root, at + new Vector3(L * 0.5f - 0.006f, H, 0f), new Vector3(0.02f, 0.012f, D), rim, false);
            Box("TrayRimR", root, at + new Vector3(-L * 0.5f + 0.006f, H, 0f), new Vector3(0.02f, 0.012f, D), rim, false);

            const float s = 1.8f;
            var glass = Sc06GlassMat("Sc08BeakerGlass", new Color(0.84f, 0.93f, 0.96f), 0.07f, 0.75f);
            var beaker = Sc06BeakerMesh();
            var waterBase = at + new Vector3(0.155f, 0.012f, 0.02f);   // viewer's left is +X
            var acidBase = at + new Vector3(-0.17f, 0.012f, 0.02f);
            parts.waterBase = waterBase;
            parts.acidBase = acidBase;

            // water beaker
            var water = new GameObject("WaterBeaker").transform;
            water.SetParent(root, true);
            water.position = waterBase;
            water.localScale = Vector3.one * s;
            Sc06Mesh("WaterGlass", water, beaker, glass, waterBase, null, Vector3.one);
            var liqMesh = Sc06LiquidDisc("Sc08Liq", 0.0372f, 0.10f);
            var wl = Sc06Mesh("WaterLiquid", water, liqMesh, Sc06Liquid("Sc08WaterLiquid", new Color(0.9f, 0.95f, 0.98f, 0.28f)), waterBase + Vector3.up * 0.003f * s, null, Vector3.one);
            wl.transform.SetParent(water, true);
            Sc08QuadWorld("WaterLabel", root, waterBase + new Vector3(0f, 0.055f * s, 0.0405f * s + 0.002f), new Vector3(0f, 180f, 0f), new Vector2(0.080f, 0.054f),
                Sc06Label("Sc08WaterLabel", "Label_Sc08Water", true, 0.3f)).transform.SetParent(water, true);

            // acid beaker: pale yellow liquid, big label, vapor from the open rim
            var acid = new GameObject("AcidBeaker").transform;
            acid.SetParent(root, true);
            acid.position = acidBase;
            acid.localScale = Vector3.one * s;
            Sc06Mesh("AcidGlass", acid, beaker, glass, acidBase, null, Vector3.one);
            var al = Sc06Mesh("AcidLiquid", acid, liqMesh, Sc06Liquid("Sc08AcidLiquid", new Color(0.93f, 0.86f, 0.5f, 0.55f)), acidBase + Vector3.up * 0.003f * s, null, Vector3.one);
            al.transform.SetParent(acid, true);
            Sc08QuadWorld("AcidLabel", root, acidBase + new Vector3(0f, 0.055f * s, 0.0405f * s + 0.002f), new Vector3(0f, 180f, 0f), new Vector2(0.090f, 0.066f),
                Sc06Label("Sc08AcidLabel", "Label_Sc08Acid", true, 0.3f)).transform.SetParent(acid, true);
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(acid, true);
            mouth.position = acidBase + Vector3.up * (0.115f * s);

            // two glass rods standing in the water beaker (RodA is the stirring rod, RodB stays)
            var rodGlass = Sc06GlassMat("Sc08RodGlass", new Color(0.86f, 0.94f, 0.97f), 0.1f, 0.85f);
            var pivot = new GameObject("RodA").transform;
            pivot.SetParent(root, true);
            pivot.position = waterBase + new Vector3(0f, 0.012f, 0f);
            Sc06Rod("RodAGlass", pivot, pivot.position, pivot.position + Vector3.up * 0.36f, 0.0045f, rodGlass);
            pivot.rotation = Quaternion.Euler(-4f, 0f, -12f); // leans toward +X, against the rim
            var rb0 = waterBase + new Vector3(0f, 0.012f, 0f);
            Sc06Rod("RodB", root, rb0, rb0 + new Vector3(-0.04f, 0.36f, 0.05f), 0.0045f, rodGlass);
            return root;
        }
    }
}
#endif

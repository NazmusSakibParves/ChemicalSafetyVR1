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
    /// Scenario 10: acid spill response, modelled on the Scene 10 workflow. Same shell dimensions as the Scenario 8 lab (viewer at
    /// Spawn_SC10 facing -Z, viewer's left = +X) with a white tile floor and a concrete left wall that carries the red SPILL KIT, the
    /// drain, the CAUTION sign, the bin and the spill. Back wall: fume hood + switch panel (copied from the SC08 builder, Sc06Lab logic).
    /// The PPE Donning Station is on the right wall (Sc08 station). Env origin x = 360.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc10Refs
        {
            public Transform spawn;
            public Transform spillStand;
            public Transform ppeStand;
            public Transform hoodStand;
            public Transform panelStand;
            public Transform kitStand;
            public Transform disposeStand;
            public Sc06Lab lab;
            public Sc08Ppe ppe;
            public Sc10Spill spill;
            public Sc09Guide guide;
            public Sc06Gate gate;
            public Sc06Ambience ambience;
        }

        private const float Sc10Ox = 360f;

        private static void Sc10FixTextures()
        {
            foreach (var n in new[] { "Label_Sc10Tile", "Label_Sc10Concrete", "Label_Sc10Pad", "Label_Sc10Sock" })
            {
                var path = Sc06TexDir + "/" + n + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                if (ti.wrapMode != TextureWrapMode.Repeat || ti.alphaIsTransparency || ti.anisoLevel < 8)
                {
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.alphaIsTransparency = false;
                    ti.anisoLevel = 8;
                    ti.SaveAndReimport();
                }
            }
        }

        private static Sc10Refs BuildSc10Room(Transform env, PresentableArt art)
        {
            const float ox = Sc10Ox;
            var refs = new Sc10Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solidLayer = SolidLayer;
            ConfigureSc06Textures();
            Sc10FixTextures();

            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.80f, 0.83f, 0.86f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.66f, 0.68f, 0.69f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.36f, 0.37f, 0.38f);
            mso.FindProperty("fogColor").colorValue = new Color(0.72f, 0.75f, 0.76f);
            mso.FindProperty("fogStart").floatValue = 9f;
            mso.FindProperty("fogEnd").floatValue = 40f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.4f;
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

            // white ceramic tiles (0.6 m) with grout like the picture; light grey concrete on the spill wall
            var floorMat = Mat("Sc10TileMat", Color.white, 0.72f, 0f, Sc06Tex("Label_Sc10Tile"));
            floorMat.SetTextureScale("_BaseMap", new Vector2(w / 1.2f, d / 1.2f));
            var wallBackMat = Sc06Pbr("Sc08WallBackPbr", "Wall", new Color(1.0f, 0.985f, 0.93f), new Vector2(w / 2.4f, h / 2.4f), 0.8f, 0.6f);
            var wallSideMat = Sc06Pbr("Sc08WallSidePbr", "Wall", new Color(1.0f, 0.985f, 0.93f), new Vector2(d / 2.4f, h / 2.4f), 0.8f, 0.6f);
            var concreteMat = Mat("Sc10ConcreteMat", new Color(1.35f, 1.35f, 1.35f), 0.18f, 0f, Sc06Tex("Label_Sc10Concrete"));
            concreteMat.SetTextureScale("_BaseMap", new Vector2(d / 1.6f, h / 1.6f));
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
            Shell("Wall_Left", new Vector3(x1 + 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), concreteMat);
            Shell("Wall_Right", new Vector3(x0 - 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), wallSideMat);
            Box("Ceiling", room, new Vector3(cx, h + 0.05f, cz), new Vector3(w, 0.1f, d), ceilMat, false);

            // the L: a concrete corner block fills the back-left corner of the box. Its +Z face (z -0.3) carries the SDS table, the x1 wall carries the Spill Kit.
            var blockX0 = ox + 1.5f;
            const float blockZ1 = -0.3f;
            var blockMat = Mat("Sc10ConcreteBlock", new Color(1.35f, 1.35f, 1.35f), 0.18f, 0f, Sc06Tex("Label_Sc10Concrete"));
            blockMat.SetTextureScale("_BaseMap", new Vector2(1.4f, h / 1.6f));
            Shell("CornerBlock", new Vector3((blockX0 + x1) * 0.5f, h * 0.5f, (zBack + blockZ1) * 0.5f), new Vector3(x1 - blockX0, h, blockZ1 - zBack), blockMat);
            Box("CornerBlockSkirtFace", room, new Vector3((blockX0 + x1) * 0.5f, 0.06f, blockZ1 + 0.015f), new Vector3(x1 - blockX0, 0.12f, 0.03f), Mat("Sc08Skirting", new Color(0.40f, 0.42f, 0.42f), 0.3f, 0f), false);

            var trim = Mat("Sc08Skirting", new Color(0.40f, 0.42f, 0.42f), 0.3f, 0f);
            Box("SkirtBack", room, new Vector3(cx, 0.06f, zBack + 0.015f), new Vector3(w, 0.12f, 0.03f), trim, false);
            Box("SkirtRight", room, new Vector3(x0 + 0.015f, 0.06f, cz), new Vector3(0.03f, 0.12f, d), trim, false);

            // ceiling light panels (emissive) with soft lights under them, and the air-conditioning grille
            var panelMat = Mat("Sc06LightPanelGrid", new Color(1.04f, 1.04f, 1.02f), 0f, 0f, Sc06Tex("Label_LightGrid"), null, true);
            var housing = Mat("Sc06PanelFrame", new Color(0.82f, 0.83f, 0.82f), 0.3f, 0f);
            var panelSpots = new[]
            {
                new Vector3(ox + 1.9f, 0f, 0.5f), new Vector3(ox + 0.4f, 0f, 0.9f), new Vector3(ox - 0.6f, 0f, -0.1f),
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
            var wispMat = ParticleMat("Sc06Wisp", Sc06Tex("Fx_Wisp"), false);
            var vaporMat = ParticleMat("Sc06Vapor", Sc06Tex("Fx_Vapor"), false);
            // ---------------------------------------------------------------- logic: lab, mix, gate, sounds
            var ambGo = new GameObject("Sc08Ambience");
            ambGo.transform.SetParent(room, false);
            refs.ambience = ambGo.AddComponent<Sc06Ambience>();
            var audioDir = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc06/";
            var aso = new SerializedObject(refs.ambience);
            aso.FindProperty("roomLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Room_Loop.wav");
            aso.FindProperty("hoodLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir.Replace("/Sc06/", "/Sc10/") + "Hood_Loop.wav");
            aso.FindProperty("hoodLevel").floatValue = 0.26f;
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

            var gateGo = new GameObject("Sc10GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc10Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc10Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc10Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var guideGo = new GameObject("Sc10Guide");
            guideGo.transform.SetParent(room, false);
            guideGo.transform.localScale = Vector3.one * 0.55f;
            refs.guide = guideGo.AddComponent<Sc09Guide>();
            SetRef(refs.guide, "art", art);

            // ---------------------------------------------------------------- PPE donning station on the right wall (as in SC08)
            refs.ppe = Sc08BuildPpeStation(room, x0, art, out refs.ppeStand);
            refs.ppeStand.name = "Stand_SC10_Ppe";

            // ---------------------------------------------------------------- the spill bay on the concrete wall
            Sc10Bay(room, ox, x1, zBack, zFront, h, art, refs);

            // ---------------------------------------------------------------- an extinguisher, as in the other labs
            var ext = SpawnSc05("Extinguisher.glb", room);
            if (ext != null)
            {
                ext.name = "Extinguisher";
                FitHeight(ext, 0.72f);
                var eb = BoundsOfTree(ext.transform);
                ext.transform.position += new Vector3((x0 + 0.3f) - eb.center.x, 0.0f - eb.min.y, 2.7f - eb.center.z);
            }

            Sc10LabDress(room, ox, x0, x1, zBack, zFront, h);

            refs.spawn = Marker("Spawn_SC10", room, new Vector3(ox + 0.4f, 0f, 2.3f), 180f);
            refs.panelStand = Marker("Stand_SC10_Panel", room, new Vector3(ox - 2.62f, 0f, hoodFrontZ + 0.8f), 180f);
            return refs;
        }
    }
}
#endif

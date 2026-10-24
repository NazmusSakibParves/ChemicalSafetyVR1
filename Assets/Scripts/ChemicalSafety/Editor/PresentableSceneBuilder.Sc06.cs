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
    /// Scenario 6: chemistry lab, modelled on the Scene 06 workflow. The viewer stands at Spawn_SC06 facing -Z (the viewer's
    /// left is +X). Back wall, left to right: Hierarchy of Controls poster, SDS poster and pocket, the fume hood with its glass
    /// sash, the control panel (Fume Hood / LEV Arm / General Ventilation switches), the spill-kit cabinet. The LEV arm hangs in the
    /// left corner. The steel table with the open Volatile Solvent jug stands in front of the hood.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc06Refs
        {
            public Transform spawn;
            public Transform tableStand;
            public Transform hoodStand;
            public Transform panelStand;
            public Transform benchStand;
            public Sc06Lab lab;
            public Sc06Jug jug;
            public Sc06Pour pour;
            public Sc06Gate gate;
            public Sc06Ambience ambience;
        }

        private const float Sc06Ox = 200f;

        private static Sc06Refs BuildSc06Room(Transform env, PresentableArt art)
        {
            const float ox = Sc06Ox;
            var refs = new Sc06Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solidLayer = SolidLayer;
            ConfigureSc06Textures();

            // bright clinical air (the other labs are dark or tinted)
            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.80f, 0.82f, 0.84f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.66f, 0.67f, 0.68f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.34f, 0.34f, 0.35f);
            mso.FindProperty("fogColor").colorValue = new Color(0.72f, 0.74f, 0.74f);
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
            var wallBackMat = Sc06Pbr("Sc06WallBackPbr", "Wall", Color.white, new Vector2(w / 2.4f, h / 2.4f), 0.8f, 0.6f);
            var wallSideMat = Sc06Pbr("Sc06WallSidePbr", "Wall", Color.white, new Vector2(d / 2.4f, h / 2.4f), 0.8f, 0.6f);
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

            // skirting, wall base trim
            var trim = Mat("Sc06Skirting", new Color(0.30f, 0.32f, 0.34f), 0.3f, 0f);
            Box("SkirtBack", room, new Vector3(cx, 0.06f, zBack + 0.015f), new Vector3(w, 0.12f, 0.03f), trim, false);
            Box("SkirtLeft", room, new Vector3(x1 - 0.015f, 0.06f, cz), new Vector3(0.03f, 0.12f, d), trim, false);
            Box("SkirtRight", room, new Vector3(x0 + 0.015f, 0.06f, cz), new Vector3(0.03f, 0.12f, d), trim, false);

            // ceiling light panels (emissive) + the air-conditioning grille, with soft lights under them
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

            // soft ambient-occlusion strips where the walls meet the ceiling and the floor (the designer's picture is darker in the corners)
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

            var steelPaintEarly = Mat("Sc06SteelPaint", new Color(0.72f, 0.74f, 0.76f), 0.55f, 0f);
            var white = Mat("Sc06Paint", new Color(0.90f, 0.90f, 0.88f), 0.45f, 0f);
            // door in the right wall (viewer's right) with a green exit sign: a lab has a way out
            var doorMat = Mat("Sc06Door", new Color(0.52f, 0.55f, 0.57f), 0.4f, 0f);
            var doorFrame = Mat("Sc06DoorFrame", new Color(0.30f, 0.32f, 0.34f), 0.35f, 0f);
            Box("DoorLeaf", room, new Vector3(x0 + 0.04f, 1.03f, 1.3f), new Vector3(0.05f, 2.06f, 0.96f), doorMat, false);
            Box("DoorFrameTop", room, new Vector3(x0 + 0.05f, 2.09f, 1.3f), new Vector3(0.07f, 0.06f, 1.08f), doorFrame, false);
            Box("DoorFrameA", room, new Vector3(x0 + 0.05f, 1.03f, 1.3f - 0.52f), new Vector3(0.07f, 2.06f, 0.06f), doorFrame, false);
            Box("DoorFrameB", room, new Vector3(x0 + 0.05f, 1.03f, 1.3f + 0.52f), new Vector3(0.07f, 2.06f, 0.06f), doorFrame, false);
            Box("DoorHandle", room, new Vector3(x0 + 0.11f, 1.0f, 1.3f + 0.36f), new Vector3(0.05f, 0.04f, 0.16f), steelPaintEarly, false);
            Box("DoorWindow", room, new Vector3(x0 + 0.068f, 1.52f, 1.3f), new Vector3(0.01f, 0.5f, 0.3f), Mat("Sc06DoorGlass", new Color(0.55f, 0.7f, 0.78f), 0.8f, 0f), false);
            Box("ExitSign", room, new Vector3(x0 + 0.06f, 2.32f, 1.3f), new Vector3(0.05f, 0.14f, 0.34f),
                Mat("Sc06ExitSign", new Color(0.05f, 0.5f, 0.2f), 0.3f, 0f, null, new Color(0.1f, 1.5f, 0.4f), false), false);
            var steelPaint = Mat("Sc06SteelPaint", new Color(0.72f, 0.74f, 0.76f), 0.55f, 0f);
            var darkSteel = Mat("Sc06DarkSteel", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0f);

            // ---------------------------------------------------------------- back wall dressing
            var posterMat = Sc06Label("Sc06PosterPaper", "Poster_Paper");
            QuadObj("PosterHierarchy", room, new Vector3(ox + 1.95f, 1.72f, zBack + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(1.5f, 1.10f), posterMat);
            QuadObj("PosterSds", room, new Vector3(ox + 0.35f, 1.55f, zBack + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.40f, 0.56f), Sc06Label("Sc06SdsPoster", "Label_SdsPoster"));
            QuadObj("SdsPocket", room, new Vector3(ox + 0.78f, 1.42f, zBack + 0.03f), new Vector3(0f, 180f, 0f),
                new Vector2(0.34f, 0.34f), Sc06Label("Sc06SdsPocket", "Label_SdsPocket", true));
            Box("SdsPocketBack", room, new Vector3(ox + 0.78f, 1.38f, zBack + 0.012f), new Vector3(0.28f, 0.26f, 0.02f), darkSteel, false);

            // wall clock and a smoke detector for scale and life
            Sc06Disc("ClockRim", room, new Vector3(ox + 1.95f, 2.65f, zBack + 0.025f), 0.34f, 0.02f, darkSteel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var clockFace = Mat("Sc06ClockFace", new Color(0.96f, 0.96f, 0.94f), 0.2f, 0f);
            Sc06Disc("ClockFace", room, new Vector3(ox + 1.95f, 2.65f, zBack + 0.037f), 0.31f, 0.01f, clockFace).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Box("ClockH", room, new Vector3(ox + 1.95f, 2.67f, zBack + 0.045f), new Vector3(0.012f, 0.08f, 0.004f), darkSteel, false);
            Box("ClockM", room, new Vector3(ox + 1.98f, 2.65f, zBack + 0.045f), new Vector3(0.1f, 0.01f, 0.004f), darkSteel, false);
            Disc("SmokeDetector", room, new Vector3(ox - 1.7f, h - 0.03f, 0.9f), 0.14f, 0.025f, white);

            // ---------------------------------------------------------------- the fume hood (FumeHood.glb)
            const float hoodX = -1.15f;
            var hoodRoot = SpawnSc06("FumeHood.glb", room);
            float surfaceY = 0.9f, sashW = 1.7f, sashH = 1.2f, sashZ = zBack + 1.0f, hoodCentreX = ox + hoodX;
            float interiorBack = zBack + 0.1f;
            float hoodFrontZ = zBack + 1.0f;
            float interiorDepth = 0.8f;
            Transform sash = null;
            if (hoodRoot != null)
            {
                hoodRoot.name = "FumeHood";
                var hoodMf = (MeshFilter)null;
                var hoodMr = (MeshRenderer)null;
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
                    Debug.Log("[SC06] hood body " + bb.min.ToString("F2") + " " + bb.max.ToString("F2") + " worktop y " + surfaceY.ToString("F2")
                        + " glass " + gb.min.ToString("F2") + " " + gb.max.ToString("F2"));
                    // the sash is rebuilt below, so the model's raised glass and frame go away
                    var hoodPaint = Mat("Sc06HoodPaint", new Color(0.90f, 0.91f, 0.89f), 0.5f, 0f);
                    var hoodInner = Mat("Sc06HoodInner", new Color(0.64f, 0.71f, 0.75f), 0.45f, 0f);
                    var slot = Mat("Sc06HoodSlot", new Color(0.56f, 0.62f, 0.66f), 0.35f, 0f);
                    var mats = hoodMr.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var mn = mats[i] != null ? mats[i].name : "";
                        Debug.Log("[SC06] hood submesh " + i + " " + mn);
                        mats[i] = mn == "Material.001" ? hoodInner : mn == "air_in" ? slot : hoodPaint;
                    }

                    hoodMr.sharedMaterials = mats;
                    Sc06DropSubmeshes(hoodMf, hoodMr, "Mesh_Sc06HoodBody", "Glass", "frame", "Material.002");
                }
                else
                {
                    Debug.LogWarning("[SC06] hood mesh not found or sub-meshes renamed");
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
                        r.gameObject.SetActive(false); // stray display block of the model, not wanted
                    }
                }
            }

            // sliding glass sash (own geometry so it can open bottom to top)
            var sashGo = new GameObject("Sash");
            sashGo.transform.SetParent(room, false);
            sashGo.transform.position = new Vector3(hoodCentreX, surfaceY + 0.05f + sashH * 0.5f, sashZ);
            sash = sashGo.transform;
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
            // FUME HOOD plate with the face-velocity readout above the opening
            var plateOnMat = Sc06Label("Sc06HoodPlate", "Label_HoodPlate");
            var plateOffMat = Sc06Label("Sc06HoodPlateOff", "Label_HoodPlateOff");
            QuadObj("HoodPlate", room, new Vector3(hoodCentreX + 0.55f, surfaceY + sashH + 0.28f, sashZ + 0.03f), new Vector3(0f, 180f, 0f),
                new Vector2(0.4f, 0.15f), plateOffMat);

            // ---------------------------------------------------------------- hood interior: lamp, back-wall tint, apparatus, jar
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
            hoodLight.enabled = false; // switched on by the Fume Hood switch
            Sc06BuildApparatus(room, hoodCentreX, surfaceY, interiorBack, interiorDepth);
            // work-surface vent slot at the front sill, back baffle slot
            Box("SillVent", room, new Vector3(hoodCentreX, surfaceY + 0.004f, sashZ + 0.06f), new Vector3(sashW - 0.2f, 0.008f, 0.03f), darkSteel, false);
            Box("BaffleSlot", room, new Vector3(hoodCentreX, surfaceY + 0.12f, interiorBack + 0.04f), new Vector3(sashW - 0.3f, 0.05f, 0.012f), darkSteel, false);
            // the large clear jar the solvent is poured into
            var jarGlass = Sc06GlassMat("Sc06JarGlass", new Color(0.82f, 0.92f, 0.95f), 0.08f, 0.8f);
            var jarPos = new Vector3(hoodCentreX - 0.05f, surfaceY, sashZ - 0.2f);
            var jar = new GameObject("PourJar").transform;
            jar.SetParent(room, true);
            jar.position = jarPos;
            Sc06Mesh("JarGlass", jar, Sc06JarMesh(), jarGlass, jarPos);
            var jarLiquid = Cyl("JarLiquid", jar, jarPos + Vector3.up * 0.006f, 0.17f, 0.08f,
                Mat("Sc06JarLiquid", new Color(0.94f, 0.97f, 0.98f, 0.30f), 0.95f, 0f));
            MakeTransparent(jarLiquid.GetComponent<MeshRenderer>().sharedMaterial);
            jarLiquid.transform.SetParent(jar, true);
            jarLiquid.transform.localScale = new Vector3(jarLiquid.transform.localScale.x, 0.0001f, jarLiquid.transform.localScale.z);

            // ---------------------------------------------------------------- control panel (right of the hood)
            const float panelX = -2.62f;
            const float panelY = 1.55f;
            var panelZ = hoodFrontZ;
            var pillarMat = Sc06Pbr("Sc06PillarPbr", "Wall", Color.white, new Vector2(0.5f, h / 2.4f), 0.8f, 0.6f);
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
            var startOn = new[] { false, false, true }; // Fume Hood off, LEV Arm off, General Ventilation on
            for (var i = 0; i < 3; i++)
            {
                var top = 20f + i * 312f;
                // texture pixel (px, py) -> world: left of the viewer is +X
                float Wx(float px) => ox + panelX - (px - 320f) * k;
                float Wy(float py) => panelY + (480f - py) * k;
                var rockerAt = new Vector3(Wx(150f), Wy(top + 206f), panelZ + 0.075f);
                var plate = Box("RockerPlate" + i, room, rockerAt, new Vector3(0.06f, 0.078f, 0.014f), rockerPlate, false);
                var rocker = Box("Rocker" + i, room, rockerAt + new Vector3(0f, 0f, 0.016f), new Vector3(0.040f, 0.07f, 0.022f), rockerMat, false);
                rocker.transform.rotation = Quaternion.Euler(startOn[i] ? 16f : -16f, 0f, 0f); // down = ON, up = OFF (as at home)
                var ledAt = new Vector3(Wx(470f), Wy(top + 206f), panelZ + 0.078f);
                var led = Prim("Led" + i, PrimitiveType.Sphere, room, Vector3.zero, Vector3.one * 0.05f, startOn[i] ? ledGreen : ledRed);
                led.transform.position = ledAt;
                led.transform.localScale = new Vector3(0.05f, 0.05f, 0.03f);
                Sc06Disc("LedBezel" + i, room, ledAt - new Vector3(0f, 0f, 0.004f), 0.074f, 0.005f, ledBezel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var ledGlow = QuadObj("LedGlow" + i, room, ledAt + new Vector3(0f, 0f, 0.008f), new Vector3(0f, 180f, 0f), new Vector2(0.17f, 0.17f), startOn[i] ? ledGlowGreen : ledGlowRed);
                Object.DestroyImmediate(ledGlow.GetComponent<Collider>());
            }

            // the hood model carries its own three valve knobs per side (colours set above)
            Box("WallSocket", room, new Vector3(ox + hoodX + 1.45f, 1.05f, zBack + 0.015f), new Vector3(0.09f, 0.09f, 0.025f), white, false);

            // ---------------------------------------------------------------- spill kit cabinet + signs (far right)
            const float spillX = -3.55f;
            var cab = SpawnSc06("SpillKit.glb", room);
            if (cab != null)
            {
                cab.name = "SpillKitCabinet";
                cab.transform.localScale = new Vector3(0.5f, 0.53f, 0.3f);
                var cabB = BoundsOfTree(cab.transform);
                cab.transform.position += new Vector3(ox + spillX - cabB.center.x, -cabB.min.y, zBack + 0.02f - cabB.min.z);
                var cabMat = Mat("Sc06SpillCabinet", new Color(0.74f, 0.75f, 0.76f), 0.5f, 0f);
                foreach (var r in cab.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = cabMat;
            }
            else
            {
                Box("SpillKitCabinet", room, new Vector3(ox + spillX, 0.53f, zBack + 0.3f), new Vector3(1.0f, 1.06f, 0.6f), steelPaint, true);
            }

            QuadObj("SpillSignFront", room, new Vector3(ox + spillX, 0.78f, zBack + 0.02f + 0.6f + 0.006f), new Vector3(0f, 180f, 0f), new Vector2(0.62f, 0.23f),
                Sc06Label("Sc06SpillKit", "Label_SpillKit"));
            QuadObj("SpillSignWall", room, new Vector3(ox + spillX, 1.45f, zBack + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.62f, 0.23f),
                Sc06Label("Sc06SpillKitWall", "Label_SpillKit"));
            QuadObj("JerrySign", room, new Vector3(ox + spillX, 1.98f, zBack + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.4f, 0.4f),
                Sc06Label("Sc06JerrySign", "Label_Jerry"));

            // ---------------------------------------------------------------- LEV arm in the left corner
            var levNozzlePos = new Vector3(ox + 2.8f, 1.0f, -1.9f);
            // counter along the back wall under the poster (the designer's picture: the arm stands on it)
            const float counterTop = 0.9f;
            var cX0 = ox + 1.05f;
            var cX1 = x1;
            var counterBody = Mat("Sc06CounterBody", new Color(0.84f, 0.85f, 0.83f), 0.4f, 0f);
            Box("CounterBody", room, new Vector3((cX0 + cX1) * 0.5f, 0.44f, zBack + 0.31f), new Vector3(cX1 - cX0, 0.88f, 0.62f), counterBody);
            var counterSteel = Sc06Pbr("Sc06CounterSteel", "Steel", new Color(0.82f, 0.84f, 0.86f), new Vector2(2f, 1f), 0.8f, 0.3f);
            Box("CounterTop", room, new Vector3((cX0 + cX1) * 0.5f, counterTop - 0.02f, zBack + 0.33f), new Vector3(cX1 - cX0 + 0.03f, 0.04f, 0.68f), counterSteel, false);
            for (var i = 0; i <= 4; i++)
            {
                var sx = Mathf.Lerp(cX0 + 0.02f, cX1 - 0.02f, i / 4f);
                Box("CounterSeam" + i, room, new Vector3(sx, 0.44f, zBack + 0.625f), new Vector3(0.012f, 0.8f, 0.01f), darkSteel, false);
                if (i < 4)
                    Box("CounterHandle" + i, room, new Vector3(sx + (cX1 - cX0) / 8f - 0.06f, 0.66f, zBack + 0.635f), new Vector3(0.1f, 0.018f, 0.02f), steelPaintEarly, false);
            }

            var lev = SpawnSc06("LevArm.glb", room);
            if (lev != null)
            {
                lev.name = "LevArm";
                var lb = BoundsOfTree(lev.transform);
                lev.transform.localScale *= 2.1f / Mathf.Max(0.01f, lb.size.y);
                // the base column (lowest vertices) stands on the counter's left end, the arm reaches toward the room (-X, the viewer's right)
                Vector3 baseP = Vector3.zero, nozP = Vector3.zero;
                var minYv = 0f;
                void Measure()
                {
                    minYv = float.MaxValue;
                    foreach (var mf0 in lev.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf0.sharedMesh == null)
                            continue;
                        foreach (var v0 in mf0.sharedMesh.vertices)
                            minYv = Mathf.Min(minYv, mf0.transform.TransformPoint(v0).y);
                    }

                    var sum = Vector3.zero;
                    var cnt = 0;
                    nozP = BoundsOfTree(lev.transform).center;
                    foreach (var mf0 in lev.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf0.sharedMesh == null)
                            continue;
                        foreach (var v0 in mf0.sharedMesh.vertices)
                        {
                            var wv0 = mf0.transform.TransformPoint(v0);
                            if (wv0.y < minYv + 0.04f)
                            {
                                sum += wv0;
                                cnt++;
                            }
                        }

                        var mr0 = mf0.GetComponent<MeshRenderer>();
                        if (mr0 == null)
                            continue;
                        foreach (var m0 in mr0.sharedMaterials)
                        {
                            if (m0 != null && m0.name.Contains("Iron Old") && Sc06SubBounds(mf0, mr0, m0.name, out var nb0))
                                nozP = nb0.center;
                        }
                    }

                    baseP = cnt > 0 ? sum / cnt : lb.center;
                }

                Measure();
                var reach = new Vector3(nozP.x - baseP.x, 0f, nozP.z - baseP.z);
                if (reach.sqrMagnitude > 1e-4f)
                    lev.transform.RotateAround(baseP, Vector3.up, Vector3.SignedAngle(reach, Vector3.left, Vector3.up));
                Measure();
                lev.transform.position += new Vector3(ox + 3.28f - baseP.x, counterTop - minYv, zBack + 0.34f - baseP.z);
                foreach (var lmf in lev.GetComponentsInChildren<MeshFilter>(true))
                {
                    var lmr = lmf.GetComponent<MeshRenderer>();
                    if (lmf.sharedMesh == null || lmr == null)
                        continue;
                    foreach (var lm in lmr.sharedMaterials)
                    {
                        if (lm != null && lm.name.Contains("Iron Old") && Sc06SubBounds(lmf, lmr, lm.name, out var nb))
                            levNozzlePos = nb.center;
                    }
                }

                var armMat = Mat("Sc06LevArm", new Color(0.62f, 0.64f, 0.66f), 0.45f, 0f);
                var armDark = Mat("Sc06LevArmDark", new Color(0.22f, 0.23f, 0.25f), 0.35f, 0f);
                foreach (var r in lev.GetComponentsInChildren<Renderer>(true))
                {
                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    r.sharedMaterial = mn.Contains("Black") ? armDark : armMat;
                }

                // the duct from the highest point of the arm up into the ceiling (as in the picture)
                var topY = float.MinValue;
                var topPos = Vector3.zero;
                foreach (var lmf2 in lev.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (lmf2.sharedMesh == null)
                        continue;
                    foreach (var lv in lmf2.sharedMesh.vertices)
                    {
                        var wv = lmf2.transform.TransformPoint(lv);
                        if (wv.y > topY)
                        {
                            topY = wv.y;
                            topPos = wv;
                        }
                    }
                }

                var duct = Mat("Sc06Duct", new Color(0.70f, 0.72f, 0.74f), 0.55f, 0f);
                Cyl("LevDuct", room, new Vector3(topPos.x, topY - 0.08f, topPos.z), h - topY + 0.08f, 0.075f, duct);
                Cyl("LevDuctCollar", room, new Vector3(topPos.x, h - 0.12f, topPos.z), 0.06f, 0.095f, duct);
                Debug.Log("[SC06] lev arm bounds " + BoundsOfTree(lev.transform).min.ToString("F2") + " " + BoundsOfTree(lev.transform).max.ToString("F2"));
            }

            // ---------------------------------------------------------------- the steel table
            const float tableX = -1.15f;
            const float tableZ = -0.35f;
            var table = SpawnSc06("Table.glb", room);
            var tableTop = 0.92f;
            var steelMat = Sc06Pbr("Sc06SteelPbr", "Steel", new Color(0.80f, 0.82f, 0.85f), new Vector2(1f, 1f), 0.8f, 0.3f);
            if (table != null)
            {
                table.name = "SteelTable";
                // the glb's thin top, frame and shelf look like bent sheet metal: only its drawer cabinet is kept
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
                // drawer cabinet on the viewer's left (+X) end: flip if it landed on the other side
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
                        ms[mi] = steelMat;
                    cabinetR.sharedMaterials = ms;
                    cabinetBounds = cabinetR.bounds;
                }

                Sc06SolidTable(room, new Vector3(ox + tableX, tableTop, tableZ), cabinetBounds, steelMat);
                Debug.Log("[SC06] table top " + tableTop.ToString("F3") + " cabinet " + cabinetBounds.min.ToString("F2") + " " + cabinetBounds.max.ToString("F2"));
            }

            // carton under the table (as in the pictures)
            var carton = Mat("Sc06Carton", new Color(0.50f, 0.38f, 0.24f), 0.2f, 0f);
            Box("CartonUnderTable", room, new Vector3(ox + tableX - 0.55f, 0.22f, tableZ - 0.12f), new Vector3(0.6f, 0.42f, 0.46f), carton, false);

            // ---------------------------------------------------------------- bench props (u = along the table, + is the viewer's left)
            var T = tableTop;
            float Bx(float u) => ox + tableX + u;
            float Bz(float dz) => tableZ + dz;
            var amber = Sc06Amber();
            var cap = Mat("Sc06BlackCap", new Color(0.08f, 0.08f, 0.09f), 0.5f, 0f);
            var glassMat = Sc06GlassMat("Sc06BenchGlass", new Color(0.82f, 0.92f, 0.95f), 0.07f, 0.75f);
            var white2 = Mat("Sc06Hdpe", new Color(0.93f, 0.94f, 0.92f), 0.5f, 0f);
            // the designer's bottles (glass, liquid, cap and label band as separate parts)
            Sc06ChemGroup(room, "Alcohol", new[] { "glassdark", "label", "liquid" }, new Vector3(Bx(0.95f), T, Bz(-0.12f)), 12f, "Label_Bottle0");
            Sc06ChemGroup(room, "NaOH", new[] { "glass", "cap", "label" }, new Vector3(Bx(0.77f), T, Bz(0.0f)), -25f, "Label_Bottle1");
            Sc06ChemGroup(room, "Napthol", new[] { "glassdark", "capblack", "label" }, new Vector3(Bx(1.0f), T, Bz(0.14f)), 40f, "Label_Bottle2");
            Sc06ChemGroup(room, "PillBottle", new[] { "plastic", "label", "capwhite" }, new Vector3(Bx(0.88f), T, Bz(0.33f)), 0f);
            Sc06ChemGroup(room, "Phenyl", new[] { "glass", "capblue", "label" }, new Vector3(Bx(0.05f), T, Bz(-0.37f)), 0f, "Label_Bottle2");
            Sc06ChemGroup(room, "Dropper", new[] { "glass", "plastic", "capwhite" }, new Vector3(Bx(-0.92f), T, Bz(-0.2f)), 0f);
            // stainless milk can (designer picture: left of the white jug)
            var canSteel = Mat("Sc06MilkCan", new Color(0.74f, 0.76f, 0.78f), 0.75f, 0.15f);
            var canAt = new Vector3(Bx(0.69f), T, Bz(-0.32f));
            Cyl("MilkCan", room, canAt, 0.25f, 0.105f, canSteel);
            Cyl("MilkCanShoulder", room, canAt + Vector3.up * 0.25f, 0.035f, 0.082f, canSteel);
            Cyl("MilkCanLid", room, canAt + Vector3.up * 0.285f, 0.018f, 0.088f, canSteel);
            Cyl("MilkCanKnob", room, canAt + Vector3.up * 0.303f, 0.02f, 0.015f, canSteel);
            Box("MilkCanHandleL", room, canAt + new Vector3(0.12f, 0.18f, 0f), new Vector3(0.04f, 0.05f, 0.03f), canSteel, false);
            Box("MilkCanHandleR", room, canAt + new Vector3(-0.12f, 0.18f, 0f), new Vector3(0.04f, 0.05f, 0.03f), canSteel, false);
            // small white jug behind the cans: the same jerrycan model at 70 percent, cap on
            var smallJugAt = new Vector3(Bx(0.43f), T, Bz(-0.35f));
            var smallJug = new GameObject("SmallJug").transform;
            smallJug.SetParent(room, true);
            smallJug.position = smallJugAt;
            smallJug.localScale = Vector3.one * 0.7f;
            smallJug.rotation = Quaternion.Euler(0f, 15f, 0f);
            var sjBody = Sc06Mesh("SmallJugBody", smallJug, Sc06ChemMesh("Sc06_Jerry"), Mat("Sc06Hdpe", new Color(0.95f, 0.95f, 0.93f), 0.6f, 0f), smallJugAt, smallJug.rotation, smallJug.localScale);
            Sc06Mesh("SmallJugCap", smallJug, Sc06ChemMesh("Sc06_JerryCap"), Mat("Sc06SmallJugCap", new Color(0.88f, 0.88f, 0.86f), 0.5f, 0f), smallJugAt, smallJug.rotation, smallJug.localScale);
            // glassware: measuring cups with handles, a beaker with stirring rods, a conical flask, a test-tube rack
            Sc06MeasuringCup(room, "MeasuringCup0", new Vector3(Bx(0.04f), T, Bz(-0.1f)), 200f, glassMat);
            Sc06MeasuringCup(room, "MeasuringCup1", new Vector3(Bx(-0.24f), T, Bz(-0.22f)), 160f, glassMat);
            var iceBeaker = Sc06ChemGroup(room, "Beaker", new[] { "glass", "liquid" }, new Vector3(Bx(0.28f), T, Bz(-0.28f)), 0f);
            var iceBeaker2 = Sc06ChemGroup(room, "Beaker", new[] { "glass", "liquid" }, new Vector3(Bx(-0.04f), T, Bz(0.12f)), 90f);
            iceBeaker2.transform.localScale = Vector3.one * 0.78f;
            Sc06GlassVessel(room, "ConicalFlask", Sc06ConicalMesh(), new Vector3(Bx(0.22f), T, Bz(0.0f)), 0.85f, glassMat,
                Sc06Liquid("Sc06ConicalLiquid", new Color(0.92f, 0.94f, 0.9f, 0.3f)), 0.047f, 0.035f);
            for (var i = 0; i < 3; i++)
            {
                var a0 = new Vector3(Bx(0.28f) + (i - 1) * 0.012f, T + 0.012f, Bz(-0.28f));
                Sc06Rod("StirRod" + i, room, a0, a0 + new Vector3((i - 1) * 0.06f, 0.2f, 0.02f * (i - 1)), 0.0042f, glassMat);
            }

            Sc06TestTubeRack(room, new Vector3(Bx(0.5f), T, Bz(0.2f)), glassMat);
            Sc06Rod("LyingRod0", room, new Vector3(Bx(0.5f), T + 0.006f, Bz(0.31f)), new Vector3(Bx(0.1f), T + 0.006f, Bz(0.28f)), 0.0048f, glassMat);
            Sc06Rod("LyingRod1", room, new Vector3(Bx(0.45f), T + 0.006f, Bz(0.37f)), new Vector3(Bx(0.05f), T + 0.006f, Bz(0.36f)), 0.0048f, glassMat);
            // clipboard
            Box("Clipboard", room, new Vector3(Bx(0.66f), T + 0.006f, Bz(0.16f)), new Vector3(0.22f, 0.012f, 0.3f), Mat("Sc06ClipBoard", new Color(0.48f, 0.3f, 0.14f), 0.3f, 0f), false)
                .transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            Box("ClipPaper", room, new Vector3(Bx(0.66f), T + 0.013f, Bz(0.16f)), new Vector3(0.19f, 0.002f, 0.26f), Mat("Sc06Paper", new Color(0.96f, 0.96f, 0.93f), 0.2f, 0f), false)
                .transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            // red cap bowl (watch glass style)
            Disc("CapBowl", room, new Vector3(Bx(-0.95f), T + 0.012f, Bz(0.22f)), 0.085f, 0.012f, Mat("Sc06RedBowl", new Color(0.6f, 0.06f, 0.07f), 0.5f, 0f));

            // the Volatile Solvent jug, open, with a loose cap beside it
            var jugAt = new Vector3(Bx(-0.45f), T, Bz(0.0f));
            var jug = Sc06BuildJug(room, jugAt, art, out var mouth, out var jugCap);
            jugCap.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            jugCap.transform.position = new Vector3(Bx(-0.70f), T + 0.02f, Bz(0.2f));

            // soft contact shadows so nothing floats on the steel
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

            Shadow("ShadowJug", jugAt.x, jugAt.z, 0.34f, 0.28f, T);
            Shadow("ShadowCap", Bx(-0.70f), Bz(0.2f), 0.1f, 0.1f, T);
            Shadow("ShadowBowl", Bx(-0.95f), Bz(0.22f), 0.15f, 0.15f, T);
            Shadow("ShadowBottleA", Bx(0.95f), Bz(-0.12f), 0.16f, 0.16f, T);
            Shadow("ShadowBottleB", Bx(0.78f), Bz(0.0f), 0.18f, 0.18f, T);
            Shadow("ShadowSample", Bx(-0.92f), Bz(-0.2f), 0.1f, 0.1f, T);
            Shadow("ShadowNapthol", Bx(1.0f), Bz(0.14f), 0.13f, 0.13f, T);
            Shadow("ShadowPill", Bx(0.88f), Bz(0.33f), 0.11f, 0.11f, T);
            Shadow("ShadowPhenyl", Bx(0.05f), Bz(-0.37f), 0.14f, 0.14f, T);
            Shadow("ShadowCan", Bx(0.69f), Bz(-0.32f), 0.3f, 0.3f, T);
            Shadow("ShadowSmallJug", smallJugAt.x, smallJugAt.z, 0.17f, 0.17f, T);
            Shadow("ShadowCup0", Bx(0.04f), Bz(-0.1f), 0.2f, 0.2f, T);
            Shadow("ShadowCup1", Bx(-0.24f), Bz(-0.22f), 0.2f, 0.2f, T);
            Shadow("ShadowBeaker", Bx(0.28f), Bz(-0.28f), 0.14f, 0.14f, T);
            Shadow("ShadowBeaker2", Bx(-0.02f), Bz(0.13f), 0.12f, 0.12f, T);
            Shadow("ShadowConical", Bx(0.22f), Bz(0.0f), 0.17f, 0.17f, T);
            Shadow("ShadowRack", Bx(0.5f), Bz(0.2f), 0.26f, 0.12f, T);
            Shadow("ShadowClip", Bx(0.66f), Bz(0.16f), 0.3f, 0.38f, T);
            Shadow("ShadowJar", hoodCentreX - 0.05f, sashZ - 0.2f, 0.28f, 0.28f, surfaceY);

            // a faint wisp from the red bowl (the open solvent jug makes its own vapor: Sc06Lab)
            var wispMat = ParticleMat("Sc06Wisp", Sc06Tex("Fx_Wisp"), false);
            var vaporMat = ParticleMat("Sc06Vapor", Sc06Tex("Fx_Vapor"), false);
            Steam(room, "BowlWisp", new Vector3(Bx(-0.95f), T + 0.03f, Bz(0.22f)), 0.08f, 1.6f, 0.12f, wispMat, 0.22f, 3f);

            // ---------------------------------------------------------------- extras: extinguisher, stool, waste bin, floor tape, flammables cabinet
            var ext = SpawnSc05("Extinguisher.glb", room);
            if (ext != null)
            {
                ext.name = "Extinguisher";
                FitHeight(ext, 0.72f);
                var eb = BoundsOfTree(ext.transform);
                ext.transform.position += new Vector3((ox + 3.7f) - eb.center.x, 0.0f - eb.min.y, (-0.05f) - eb.center.z);
            }

            var yellow = Mat("Sc06FlamCab", new Color(0.88f, 0.72f, 0.12f), 0.45f, 0f);
            Box("FlammablesCabinet", room, new Vector3(x1 - 0.28f, 0.9f, 1.1f), new Vector3(0.5f, 1.8f, 1.0f), yellow, true);
            Box("FlamDoorSplit", room, new Vector3(x1 - 0.545f, 0.9f, 1.1f), new Vector3(0.012f, 1.7f, 0.012f), darkSteel, false);
            Box("FlamHandleA", room, new Vector3(x1 - 0.55f, 0.95f, 1.03f), new Vector3(0.03f, 0.2f, 0.02f), darkSteel, false);
            Box("FlamHandleB", room, new Vector3(x1 - 0.55f, 0.95f, 1.17f), new Vector3(0.03f, 0.2f, 0.02f), darkSteel, false);
            QuadObj("FlamSign", room, new Vector3(x1 - 0.551f, 1.45f, 1.1f), new Vector3(0f, -90f, 0f), new Vector2(0.4f, 0.4f), Sc06Label("Sc06FlamSign", "Label_Flam"));
                        var tape = Mat("Sc06HazardTape", new Color(0.9f, 0.76f, 0.1f), 0.3f, 0f);
            Box("FloorTape", room, new Vector3(ox + hoodX, 0.004f, zBack + 1.5f), new Vector3(2.4f, 0.004f, 0.05f), tape, false);
            QuadObj("EyeWashSign", room, new Vector3(x0 + 0.01f, 1.9f, 0.2f), new Vector3(0f, 90f, 0f), new Vector2(0.4f, 0.4f),
                Mat("Sc06EyeWash", new Color(0.1f, 0.5f, 0.25f), 0.2f, 0f, null, new Color(0.02f, 0.12f, 0.06f), false));

            // ---------------------------------------------------------------- interaction objects (finger poke volumes, glow, flash, strip)
            PokeTarget MakePoke(string name, Transform parent, Vector3 worldCentre, Vector3 size, float reach)
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

            for (var i = 0; i < 3; i++)
            {
                var rk = room.Find("Rocker" + i);
                if (rk != null)
                    MakePoke("SwitchPoke" + i, room, rk.position + new Vector3(0f, 0f, 0.05f), new Vector3(0.11f, 0.17f, 0.16f), 0.05f);
            }

            var sashCentre = sash.position;
            MakePoke("SashPoke", room, sashCentre + new Vector3(0f, 0f, 0.07f), new Vector3(sashW, sashH, 0.16f), 0.05f);
            var jugRoot = jug.transform;
            var jugPoke = MakePoke("JugPoke", jugRoot, jugRoot.position + new Vector3(0f, JugH * 0.5f, 0f), new Vector3(JugW + 0.12f, JugH + 0.1f, JugD + 0.18f), 0.05f);
            jugPoke.transform.rotation = Quaternion.identity;
            MakePoke("JarPoke", room, jarPos + new Vector3(0f, 0.14f, 0.1f), new Vector3(0.3f, 0.36f, 0.34f), 0.05f);
            var jarRing = QuadObj("JarRing", room, jarPos + new Vector3(0f, 0.012f, 0f), new Vector3(90f, 0f, 0f), new Vector2(1f, 1f),
                MatAlpha("Sc06JarRing", Sc06Tex("Fx_Ripple"), new Color(0.25f, 1f, 0.45f, 0.8f), true));
            jarRing.SetActive(false);
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            MakePoke("CapPoke", room, jugCap.transform.position + new Vector3(0f, 0.02f, 0.05f), new Vector3(0.15f, 0.12f, 0.17f), 0.05f);
            var capGlow = QuadObj("CapGlow", room, jugCap.transform.position + new Vector3(0f, 0.03f, 0.07f), new Vector3(0f, 180f, 0f), new Vector2(0.3f, 0.3f),
                MatAlpha("Sc06CapGlow", glowTex, new Color(0.2f, 1f, 0.4f, 0.6f), true));
            capGlow.SetActive(false);
            var jugGlow = QuadObj("JugGlow", room, jugRoot.position + new Vector3(0f, JugH * 0.5f, JugD * 0.5f + 0.1f), new Vector3(0f, 180f, 0f),
                new Vector2(0.55f, 0.55f), MatAlpha("Sc06JugGlow", glowTex, new Color(0.2f, 1f, 0.4f, 0.55f), true));
            jugGlow.SetActive(false);
            var flash = QuadObj("SashFlash", room, sashCentre + new Vector3(0f, 0f, 0.03f), new Vector3(0f, 180f, 0f), new Vector2(sashW, sashH),
                MatAlpha("Sc06SashFlash", null, new Color(1f, 0.15f, 0.12f, 0f), false));
            flash.SetActive(false);
            // paper strip taped to the top of the sash: hinged at the top edge, blown inward when the hood runs
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

            // ---------------------------------------------------------------- logic: lab, jug, sounds
            var ambGo = new GameObject("Sc06Ambience");
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

            var labGo = new GameObject("Sc06Lab");
            labGo.transform.SetParent(room, false);
            refs.lab = labGo.AddComponent<Sc06Lab>();
            var nozzle = new GameObject("LevNozzle").transform;
            nozzle.SetParent(room, true);
            nozzle.position = levNozzlePos;
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
            lso.FindProperty("levNozzle").objectReferenceValue = nozzle;
            var hb = new Bounds(new Vector3(hoodCentreX, surfaceY + sashH * 0.5f, (interiorBack + sashZ) * 0.5f),
                new Vector3(sashW - 0.15f, sashH, sashZ - interiorBack));
            lso.FindProperty("hoodBounds").boundsValue = hb;
            lso.ApplyModifiedPropertiesWithoutUndo();
            refs.jug = labGo.AddComponent<Sc06Jug>();
            var gateGo = new GameObject("Sc06GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            refs.pour = labGo.AddComponent<Sc06Pour>();
            var pso = new SerializedObject(refs.pour);
            pso.FindProperty("lab").objectReferenceValue = refs.lab;
            pso.FindProperty("jug").objectReferenceValue = refs.jug;
            pso.FindProperty("ambience").objectReferenceValue = refs.ambience;
            pso.FindProperty("streamMaterial").objectReferenceValue = MatAlpha("Sc06Stream", null, new Color(0.92f, 0.97f, 1f, 0.5f), false);
            pso.FindProperty("dropMaterial").objectReferenceValue = ParticleMat("Sc06Drop", Sc06Tex("Fx_Drop"), false);
            pso.FindProperty("rippleMaterial").objectReferenceValue = MatAlpha("Sc06Ripple", Sc06Tex("Fx_Ripple"), Color.white, false);
            pso.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- spawn and stands
            refs.spawn = Marker("Spawn_SC06", room, new Vector3(ox + tableX + 0.1f, 0f, 1.35f), 180f);
            // where the cards open: back at the start spot, so the whole room is in view behind them
            refs.tableStand = Marker("Stand_SC06_Table", room, new Vector3(ox + tableX + 0.1f, 0f, 1.35f), 180f);
            // in front of the table, the loose cap and the solvent container within arm's reach
            refs.benchStand = Marker("Stand_SC06_Bench", room, new Vector3(ox + tableX - 0.45f, 0f, 0.42f), 180f);
            refs.panelStand = Marker("Stand_SC06_Panel", room, new Vector3(ox - 2.15f, 0f, -1.3f), 180f);
            refs.hoodStand = Marker("Stand_SC06_Hood", room, new Vector3(ox - 1.2f, 0f, -1.3f), 180f);
            return refs;
        }

        // A baked reflection probe in the middle of the lab so the steel table, the hood glass and the bottle glass reflect the room instead of the sky.
        private static void Sc06BakeProbe(Transform env1, Transform env6)
        {
            try
            {
                var room = env6.Find("Room");
                var go = new GameObject("Sc06ReflectionProbe");
                go.transform.SetParent(room, false);
                go.transform.position = new Vector3(Sc06Ox - 1.0f, 1.5f, -0.4f);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = 128;
                probe.size = new Vector3(8.6f, 3.3f, 6.6f);
                probe.center = new Vector3(-0.2f, 0.1f, 0.2f);
                probe.boxProjection = true;
                probe.intensity = 1f;
                probe.importance = 1;
                var path = GenDir + "/Sc06Probe.exr";
                var envWasActive = env1.gameObject.activeSelf;
                env1.gameObject.SetActive(false);
                env6.gameObject.SetActive(true);
                var ok = UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
                env1.gameObject.SetActive(envWasActive);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else
                {
                    Debug.LogWarning("[SC06] reflection probe bake failed, the room uses the default reflections");
                    Object.DestroyImmediate(go);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SC06] reflection probe skipped: " + e.Message);
            }
        }

        private const string Sc06OnlyKey = "ChemSafetySc06Only";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 6")]
        private static void ToggleSc06Only()
        {
            EditorPrefs.SetBool(Sc06OnlyKey, !EditorPrefs.GetBool(Sc06OnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 6", true)]
        private static bool ToggleSc06OnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 6", EditorPrefs.GetBool(Sc06OnlyKey, false));
            return true;
        }

        // ------------------------------------------------------------------ editor preview (no play mode needed)

        // Reads Temp/sc06_views.txt: one view per line "name x y z yawDeg pitchDeg fov" (x and z relative to the SC06 origin,
        // y absolute eye height). Writes Temp/Smoke/sc06_<name>.png. Default views when the file is missing.
        [MenuItem("Chemical Safety/Presentable/Preview SC06 (screenshots)")]
        public static void PreviewSc06()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Presentable] Exit Play mode first.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var states = new Dictionary<GameObject, bool>();
            GameObject env6 = null;
            foreach (var r in roots)
            {
                if (!r.name.StartsWith("Environment_SC"))
                    continue;
                states[r] = r.activeSelf;
                if (r.name == "Environment_SC06")
                    env6 = r;
            }

            if (env6 == null)
            {
                Debug.LogError("[Presentable] Environment_SC06 missing; build the scene first.");
                return;
            }

            foreach (var kv in states)
                kv.Key.SetActive(kv.Key == env6);

            // the room-mood component applies its values in OnEnable (play mode only); do it by hand
            var oldMode = RenderSettings.ambientMode;
            var oldSky = RenderSettings.ambientSkyColor;
            var oldEq = RenderSettings.ambientEquatorColor;
            var oldGr = RenderSettings.ambientGroundColor;
            var oldFog = RenderSettings.fogColor;
            var oldFs = RenderSettings.fogStartDistance;
            var oldFe = RenderSettings.fogEndDistance;
            var oldRef = RenderSettings.reflectionIntensity;
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

            var mso = new SerializedObject(env6.GetComponent<ChemicalSafety.Presentable.Sc03Mood>());
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = mso.FindProperty("ambientSky").colorValue;
            RenderSettings.ambientEquatorColor = mso.FindProperty("ambientEquator").colorValue;
            RenderSettings.ambientGroundColor = mso.FindProperty("ambientGround").colorValue;
            RenderSettings.fogColor = mso.FindProperty("fogColor").colorValue;
            RenderSettings.fogStartDistance = mso.FindProperty("fogStart").floatValue;
            RenderSettings.fogEndDistance = mso.FindProperty("fogEnd").floatValue;
            RenderSettings.reflectionIntensity = mso.FindProperty("reflectionIntensity").floatValue;
            if (sun != null)
                sun.intensity = mso.FindProperty("sunIntensity").floatValue;

            var views = new List<string>();
            var viewFile = "Temp/sc06_views.txt";
            if (File.Exists(viewFile))
                views.AddRange(File.ReadAllLines(viewFile));
            else
            {
                views.Add("main 0.35 1.55 1.0 0 8 75");
                views.Add("hood -0.9 1.5 -0.6 0 5 70");
                views.Add("table 0.8 1.5 0.0 0 25 70");
            }

            foreach (var ps in env6.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Simulate(4.5f, true, true);
            }

            Directory.CreateDirectory("Temp/Smoke");
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            foreach (var line in views)
            {
                var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 7)
                    continue;
                float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
                cam.transform.position = new Vector3(Sc06Ox + F(1), F(2), F(3));
                cam.transform.rotation = Quaternion.Euler(F(5), 180f - F(4), 0f);
                cam.fieldOfView = F(6);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/Smoke/sc06_" + p[0] + ".png", tex.EncodeToPNG());
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
            RenderSettings.fogColor = oldFog;
            RenderSettings.fogStartDistance = oldFs;
            RenderSettings.fogEndDistance = oldFe;
            RenderSettings.reflectionIntensity = oldRef;
            if (sun != null)
                sun.intensity = oldSun;
            Debug.Log("[SC06] preview written to Temp/Smoke/sc06_*.png");
        }
    }
}
#endif

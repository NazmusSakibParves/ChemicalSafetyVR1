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
    /// Scenario 7 as the client's garment-factory hall (look-alike of the client's picture "7.jpeg"): stained concrete pillars, floor and
    /// ceiling with beams, red and grey pipes, hanging tube lights, fast white ceiling fans, yellow floor lanes, a row of sewing machine
    /// benches on both sides, fabric bundles, the burning cabinet on the pillar to the left and the Silver / Cream / Red extinguishers on
    /// the nearer pillar to the right, a grey emergency door at the end of the aisle.
    /// <c>Sc07UseFactory = false</c> + Build Presentable Scene gives the previous chemical hall back (nothing of it is deleted from the code).
    /// The old hall is built first; here everything of it that the new hall does not use is destroyed and the kept pieces (extinguishers,
    /// cabinet, fire, alarm, workers, logic hosts) are re-placed.
    /// Viewer faces -Z (left = +X, right = -X). Aisle x ox-2.4 .. ox+1.8, the player starts at the aisle entrance (z = +6.5).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc07UseFactory = true;

        // direct children of the old room that stay: extinguishers, cabinet + its stripes, fire, alarm, workers, logic hosts and markers
        private static readonly string[] Sc07KeepPrefixes =
        {
            "Ext_", "Strap", "ElectricCabinet", "Hazard", "CabinetPlinth", "FireAlarmBeacon", "BeaconFx", "Fire", "Sc07", "Spawn_SC07",
            "Stand_SC07", "Notice", "Crowd", "ExtinguisherInspect", "ExtArrow", "Extinguishers"
        };

        private const float Sc07LabelZ = 0.108f; // label quad distance in front of the extinguisher body centre

        private static void Sc07Static(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static void Sc07ApplyFactory(Transform room, Sc07Refs refs, PresentableArt art, float pillarCx, float cabX, float cabFrontZ)
        {
            const float ox = Sc07Ox;
            const float zBack = -16f;
            const float zFront = 9f;
            const float h = 4.3f;
            var x0 = ox - 13f;
            var x1 = ox + 13f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (zBack + zFront) * 0.5f;
            var w = x1 - x0;
            var d = zFront - zBack;
            const float extFace = -1.8f;   // extinguisher pillar front face
            const float cabFace = -2.8f;   // wall face the cabinet stands in front of

            // ---- 1. destroy what the new hall does not use
            var doomed = new List<GameObject>();
            foreach (Transform child in room)
            {
                var keep = false;
                foreach (var p in Sc07KeepPrefixes)
                    if (child.name.StartsWith(p))
                    {
                        keep = true;
                        break;
                    }

                if (!keep)
                    doomed.Add(child.gameObject);
            }

            foreach (var g in doomed)
                Object.DestroyImmediate(g);

            // ---- extinguishers like the client picture: Silver (Water) | Cream (Foam) | Red (CO2 / ABC dry chemical)
            var silverMat = Mat("Sc07ExtSilver", new Color(0.80f, 0.82f, 0.85f), 0.65f, 0.55f);
            var creamMat = Mat("Sc07ExtCream", new Color(0.93f, 0.86f, 0.64f), 0.45f, 0.05f);
            var blackMat = Mat("Sc07ExtBlack", new Color(0.05f, 0.05f, 0.06f), 0.35f, 0f);
            void Recolour(string groupName, Material bodyMat, string labelKey, string labelTexName)
            {
                var g = Sc06Find(room, groupName);
                if (g == null)
                    return;
                var tex = Sc07Tex(labelTexName);
                var labelMat = tex != null ? Mat("Sc07" + labelTexName + "Mat", Color.white, 0.35f, 0f, tex, null, false) : null;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    var hit = false;
                    for (var k = 0; k < ms.Length; k++)
                    {
                        var n = ms[k] != null ? ms[k].name : "";
                        if (n.Contains("extincteur"))
                        {
                            ms[k] = bodyMat;
                            hit = true;
                        }
                        else if (n.Contains(labelKey) && labelMat != null)
                        {
                            ms[k] = labelMat; // the model's label UVs: the new label wraps round the body like the original
                            hit = true;
                        }
                        else if (n.Contains("Material.004"))
                        {
                            ms[k] = blackMat; // the blue hose / base of the ABC model: black like the picture
                            hit = true;
                        }
                    }

                    if (hit)
                        r.sharedMaterials = ms;
                }
            }

            Recolour("Ext_Water", silverMat, "Material.002", "Label_Silver");
            Recolour("Ext_ABC", creamMat, "Material.003", "Label_Cream");

            // the old cabinet glb goes: the picture has a plain grey panel on the pillar and a small tin on fire below it
            foreach (var nm in new[] { "ElectricCabinet", "CabinetPlinth" })
            {
                var o = Sc06Find(room, nm);
                if (o != null)
                    Object.DestroyImmediate(o.gameObject);
            }

            for (var guard = 0; guard < 40; guard++)
            {
                var hz = Sc06Find(room, "HazardTop") ?? Sc06Find(room, "HazardBottom") ?? Sc06Find(room, "HazardLeft") ?? Sc06Find(room, "HazardRight") ?? Sc06Find(room, "HazardMid");
                if (hz == null)
                    break;
                Object.DestroyImmediate(hz.gameObject);
            }

            // ---- 2. mood: bright dusty daylight-like hall, haze only far away (like the picture)
            var mood = room.parent != null ? room.parent.GetComponent<Sc03Mood>() : null;
            if (mood != null)
            {
                var mso = new SerializedObject(mood);
                mso.FindProperty("ambientSky").colorValue = new Color(1.0f, 0.98f, 0.95f);
                mso.FindProperty("ambientEquator").colorValue = new Color(0.88f, 0.86f, 0.83f);
                mso.FindProperty("ambientGround").colorValue = new Color(0.52f, 0.50f, 0.47f);
                mso.FindProperty("fogColor").colorValue = new Color(0.62f, 0.61f, 0.60f);
                mso.FindProperty("fogStart").floatValue = 10f;
                mso.FindProperty("fogEnd").floatValue = 48f;
                mso.FindProperty("reflectionIntensity").floatValue = 0.35f;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            var solid = SolidLayer;
            var concrete = new Color(1.55f, 1.54f, 1.50f);
            var wallMat = Sc05Pbr("Sc07FacWall", "Wall", concrete, new Vector2(w / 2.4f, h / 2.4f), 0.5f, 1.1f);
            var wallSide = Sc05Pbr("Sc07FacWallSide", "Wall", concrete, new Vector2(d / 2.4f, h / 2.4f), 0.5f, 1.1f);
            var floorTex = Sc07Tex("Floor_Concrete");
            var floorMat = floorTex != null
                ? Mat("Sc07FacFloorConcreteMatte", new Color(0.92f, 0.91f, 0.89f), 0.04f, 0f, floorTex, null, false)
                : Sc05Pbr("Sc07FacFloor", "Floor", new Color(1.25f, 1.22f, 1.18f), new Vector2(w / 2.4f, d / 2.4f), 0.55f, 1.0f);
            if (floorTex != null)
            {
                floorMat.SetTextureScale("_BaseMap", new Vector2(w / 3.2f, d / 3.2f));
                floorMat.SetTextureScale("_MainTex", new Vector2(w / 3.2f, d / 3.2f));
            }
            var ceilMat = Sc05Pbr("Sc07FacCeil", "Ceil", new Color(1.5f, 1.5f, 1.46f), new Vector2(w / 2.4f, d / 2.4f), 0.3f, 0.9f);
            var pillarPhoto = Sc07Tex("Pillar_Photo");
            var pillarMat = pillarPhoto != null
                ? Mat("Sc07FacPillarPhoto", new Color(1.0f, 1.0f, 0.98f), 0.15f, 0f, pillarPhoto, null, false) // whitewashed long ago: white, old, dirt built up
                : Sc05Pbr("Sc07FacPillar", "Wall", new Color(1.55f, 1.54f, 1.5f), new Vector2(0.7f, 1.8f), 0.45f, 1.2f);
            var pillarWhite = Sc05Pbr("Sc07FacPillarWhite", "Wall", new Color(1.9f, 1.88f, 1.8f), new Vector2(0.7f, 1.8f), 0.35f, 1.0f);
            var beamMat = Sc05Pbr("Sc07FacBeam", "Wall", new Color(1.35f, 1.35f, 1.31f), new Vector2(3f, 0.3f), 0.4f, 1.0f);

            // ---- 3. shell
            var floor = Box("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), floorMat);
            if (solid >= 0)
                floor.layer = solid;
            floor.AddComponent<TeleportationArea>();
            Sc07Static(floor);
            void Wall(string n, Vector3 pos, Vector3 size, Material m)
            {
                var g = Box(n, room, pos, size, m);
                if (solid >= 0)
                    g.layer = solid;
                Sc07Static(g);
            }

            Wall("Wall_Back", new Vector3(cx, h * 0.5f, zBack), new Vector3(w, h, 0.15f), wallMat);
            Wall("Wall_Front", new Vector3(cx, h * 0.5f, zFront), new Vector3(w, h, 0.15f), wallMat);
            Wall("Wall_Left", new Vector3(x1, h * 0.5f, cz), new Vector3(0.15f, h, d), wallSide);
            Wall("Wall_Right", new Vector3(x0, h * 0.5f, cz), new Vector3(0.15f, h, d), wallSide);
            Sc07Static(Box("Ceiling", room, new Vector3(cx, h, cz), new Vector3(w, 0.1f, d), ceilMat, false));

            // ---- 4. dirt: oil blots on the floor, smudges on walls and pillars
            var oil = MatAlpha("Sc07FacOil", EnvTex("STAIN_Oil"), new Color(0.08f, 0.06f, 0.05f, 0.60f), false);
            var rust = MatAlpha("Sc07FacRust", EnvTex("STAIN_Rust"), new Color(0.45f, 0.26f, 0.13f, 0.55f), false);
            var rs = new System.Random(71);
            for (var i = 0; i < 44; i++)
                Sc07Static(QuadObj("FloorBlot" + i, room, new Vector3(x0 + 0.8f + (float)rs.NextDouble() * (w - 1.6f), 0.011f + i * 0.0003f, zBack + 0.8f + (float)rs.NextDouble() * (d - 1.6f)),
                    new Vector3(90f, (float)rs.NextDouble() * 360f, 0f), Vector2.one * (0.9f + (float)rs.NextDouble() * 1.8f), i % 4 == 3 ? rust : oil));
            var blotTex = Sc03Tex("Stain_Blot");
            var grimeTex = Sc03Tex("Grime_Drip");
            var smudge = blotTex != null ? MatAlpha("Sc07FacSmudge", blotTex, new Color(0.30f, 0.27f, 0.23f, 0.30f), false) : null;
            var drip = grimeTex != null ? MatAlpha("Sc07FacDrip", grimeTex, new Color(0.28f, 0.25f, 0.2f, 0.34f), false) : null;
            if (smudge != null)
                for (var i = 0; i < 26; i++)
                    Sc07Static(QuadObj("WallSmudge" + i, room, new Vector3(x0 + 1f + (float)rs.NextDouble() * (w - 2f), 0.5f + (float)rs.NextDouble() * 3f, zBack + 0.09f),
                        new Vector3(0f, 180f, (float)rs.NextDouble() * 360f), Vector2.one * (0.8f + (float)rs.NextDouble() * 1.4f), smudge));
            if (drip != null)
                for (var i = 0; i < 10; i++)
                    Sc07Static(QuadObj("WallDrip" + i, room, new Vector3(x0 + 1.5f + i * (w - 3f) / 9f, 2.0f, zBack + 0.085f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 2.3f), drip));

            // ---- 5. pillars with a painted footing; the extinguisher pillar (wide, white) and the fire pillar behind the cabinet
            var grimeHeavy = blotTex != null ? MatAlpha("Sc07FacGrimeHeavy", blotTex, new Color(0.22f, 0.19f, 0.15f, 0.55f), false) : null;
            var dripHeavy = grimeTex != null ? MatAlpha("Sc07FacDripHeavy", grimeTex, new Color(0.24f, 0.2f, 0.15f, 0.5f), false) : null;
            var footing = Mat("Sc07FacFooting", new Color(0.26f, 0.26f, 0.27f), 0.2f, 0f);
            const float cabW = 1.2f;
            void Pillar(string n, float px, float zc, float sx, float sz, Material m)
            {
                var g = Box(n, room, new Vector3(px, h * 0.5f, zc), new Vector3(sx, h, sz), m);
                if (solid >= 0)
                    g.layer = solid;
                Sc07Static(g);
                Sc07Static(Box(n + "Foot", room, new Vector3(px, 0.14f, zc), new Vector3(sx + 0.04f, 0.28f, sz + 0.04f), footing, false));
                if (grimeHeavy != null)
                {
                    foreach (var side in new[] { 1f, -1f })
                    {
                        // soot / old dirt on both sides of the pillar and long drips from the ceiling
                        Sc07Static(QuadObj(n + "GrimeS", room, new Vector3(px + side * (sx * 0.5f + 0.012f), 1.6f, zc), new Vector3(0f, side > 0 ? 90f : -90f, 0f), new Vector2(sz * 0.9f, 2.4f), grimeHeavy));
                        Sc07Static(QuadObj(n + "Drip", room, new Vector3(px + side * 0.12f * sx, 3.0f, zc + sz * 0.5f + 0.016f), new Vector3(0f, 180f, 0f), new Vector2(Mathf.Min(sx, 0.9f), 2.6f), dripHeavy));
                    }

                    Sc07Static(QuadObj(n + "Base", room, new Vector3(px, 0.6f, zc + sz * 0.5f + 0.014f), new Vector3(0f, 180f, 0f), new Vector2(sx, 1.0f), grimeHeavy));
                }

                if (smudge != null)
                {
                    Sc07Static(QuadObj(n + "Dirt0", room, new Vector3(px, 0.9f, zc + sz * 0.5f + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(Mathf.Min(sx, 1.8f), 1.4f), smudge));
                    Sc07Static(QuadObj(n + "Dirt1", room, new Vector3(px, 2.6f, zc + sz * 0.5f + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(Mathf.Min(sx, 1.4f), 1.1f), smudge));
                }
            }

            Pillar("PillarExt", pillarCx, (extFace - 1.0f + extFace) * 0.5f, 1.55f, 1.0f, pillarWhite);
            Pillar("PillarFire", cabX, cabFace - 0.5f, 1.25f, 1.0f, pillarMat);
            foreach (var px in new[] { ox - 8.9f, ox - 3.4f, ox + 2.6f, ox + 8.1f })
                foreach (var pz in new[] { 3.7f, -8.3f })
                    if (!(Mathf.Abs(px - ox) < 5f && pz > 0f)) // none beside the player
                    Pillar("PillarB" + (int)(px - ox) + "_" + (int)pz, px, pz, 0.8f, 0.8f, pillarMat);
            foreach (var px in new[] { ox - 8.9f, ox + 8.1f })
                Pillar("PillarB" + (int)(px - ox) + "_m2", px, -2.3f, 0.8f, 0.8f, pillarMat);

            // ---- 5b. the picture's electrical panel on the fire pillar (grey box, vents, gauges, buttons, cables) and the tin on fire below it
            var panelGrey = Mat("Sc07PanelBody", new Color(0.60f, 0.62f, 0.62f), 0.35f, 0.3f);
            var panelDoor = Mat("Sc07PanelDoor", new Color(0.54f, 0.56f, 0.56f), 0.3f, 0.3f);
            var panelDark = Mat("Sc07PanelDark", new Color(0.18f, 0.19f, 0.2f), 0.3f, 0.3f);
            var gaugeWhite = Mat("Sc07PanelGauge", new Color(0.93f, 0.93f, 0.9f), 0.3f, 0f);
            var pzf = cabFace + 0.24f;
            Box("PanelBody", room, new Vector3(cabX, 1.8f, cabFace + 0.12f), new Vector3(1.0f, 1.2f, 0.24f), panelGrey, false);
            Box("PanelDoor", room, new Vector3(cabX, 1.8f, pzf + 0.004f), new Vector3(0.92f, 1.12f, 0.012f), panelDoor, false);
            for (var vi = 0; vi < 5; vi++)
                Box("PanelVent" + vi, room, new Vector3(cabX - 0.27f, 2.22f - vi * 0.05f, pzf + 0.014f), new Vector3(0.26f, 0.022f, 0.01f), panelDark, false);
            foreach (var gx in new[] { 0.12f, 0.33f })
            {
                Sc06HCyl("GaugeRim", room, new Vector3(cabX + gx, 2.1f, pzf + 0.02f), 0.025f, 0.075f, panelDark, false);
                Sc06HCyl("GaugeFace", room, new Vector3(cabX + gx, 2.1f, pzf + 0.034f), 0.012f, 0.062f, gaugeWhite, false);
            }

            var knobCols = new[] { new Color(0.8f, 0.1f, 0.08f), new Color(0.9f, 0.7f, 0.1f), new Color(0.15f, 0.7f, 0.25f), new Color(0.8f, 0.1f, 0.08f), new Color(0.85f, 0.85f, 0.85f) };
            for (var ki = 0; ki < knobCols.Length; ki++)
                Box("PanelKnob" + ki, room, new Vector3(cabX - 0.30f + ki * 0.15f, 1.72f, pzf + 0.016f), new Vector3(0.07f, 0.07f, 0.02f),
                    Mat("Sc07PanelKnob" + ki, knobCols[ki], 0.4f, 0f), false);
            for (var ri = 0; ri < 3; ri++)
                Box("PanelRow" + ri, room, new Vector3(cabX, 1.5f - ri * 0.13f, pzf + 0.012f), new Vector3(0.78f, 0.06f, 0.012f), panelDark, false);
            // the picture's bundle of grey conduits (the current pipes) running from the panel up the pillar to the ceiling
            var conduitMat = Mat("Sc07PanelConduit", new Color(0.55f, 0.56f, 0.57f), 0.4f, 0.3f);
            foreach (var co in new[] { -0.33f, -0.25f, -0.17f, -0.09f })
            {
                Sc07Static(Cyl("PanelConduit", room, new Vector3(cabX + co, 2.38f, cabFace + 0.04f), h - 2.38f - 0.1f, 0.03f, conduitMat));
                Sc07Static(Box("PanelConduitClip", room, new Vector3(cabX + co, 3.1f, cabFace + 0.02f), new Vector3(0.05f, 0.04f, 0.04f), panelDark, false));
            }

            // cables hanging down the pillar into the burning tin
            foreach (var cx2 in new[] { -0.34f, -0.18f, 0.28f, 0.4f })
                Sc07Static(Cyl("PanelCable", room, new Vector3(cabX + cx2, 0.55f, cabFace + 0.03f), 1.2f, 0.012f, panelDark));
            // the tin: grey steel, rust, a red flammable diamond on its front
            var tinMat = Mat("Sc07FireTin", new Color(0.46f, 0.47f, 0.48f), 0.35f, 0.4f);
            var tinAt = new Vector3(cabX - 0.1f, 0f, cabFace + 0.34f); // against the pillar, under the panel
            Box("FireTin", room, tinAt + new Vector3(0f, 0.33f, 0f), new Vector3(0.58f, 0.66f, 0.58f), tinMat, false);
            Box("FireTinInside", room, tinAt + new Vector3(0f, 0.665f, 0f), new Vector3(0.5f, 0.012f, 0.5f), panelDark, false);
            QuadObj("FireTinLabel", room, tinAt + new Vector3(0f, 0.33f, 0.292f), new Vector3(0f, 180f, 0f), new Vector2(0.34f, 0.34f),
                Mat("Sc07FireTinLabel", Color.white, 0.3f, 0f, Sc07Tex("Sign_Flammable"), null, false, true));
            // everything above was built on the pillar's front face; turn it onto the face that looks at the aisle (the picture's panel
            // is on that side of the left pillar)
            var rig = new GameObject("PanelRig").transform;
            rig.SetParent(room, false);
            rig.position = new Vector3(cabX, 0f, cabFace);
            var rigParts = new List<Transform>();
            foreach (Transform ch in room)
                if (ch.name.StartsWith("Panel") && ch.name != "PanelRig" || ch.name.StartsWith("Gauge") || ch.name.StartsWith("FireTin"))
                    rigParts.Add(ch);
            foreach (var part in rigParts)
                part.SetParent(rig, true);
            rig.rotation = Quaternion.Euler(0f, -90f, 0f);
            rig.position = new Vector3(cabX - 0.625f, 0f, cabFace - 0.5f);
            var tinT = Sc06Find(room, "FireTin");
            var fireNew = (tinT != null ? tinT.position : tinAt + new Vector3(0f, 0.33f, 0f)) + new Vector3(0f, 0.29f, 0f);
            if (refs.fire != null)
                refs.fire.transform.position += fireNew - new Vector3(cabX, 0.05f, cabFrontZ + 0.35f);
            var fireAtT = Sc06Find(room, "FireAt");
            if (fireAtT != null)
                fireAtT.position = fireNew;

            // ---- 6. ceiling: concrete beam grid, pipes, cable trays
            foreach (var bz in new[] { 3.7f, -2.3f, -8.3f, -14.2f })
                Sc07Static(Box("BeamX", room, new Vector3(cx, h - 0.33f, bz), new Vector3(w, 0.55f, 0.5f), beamMat, false));
            foreach (var bx in new[] { ox - 8.9f, ox - 3.4f, ox + 2.6f, ox + 8.1f })
                Sc07Static(Box("BeamZ", room, new Vector3(bx, h - 0.33f, cz), new Vector3(0.5f, 0.55f, d), beamMat, false));
            var redPipe = Mat("Sc07FacRedPipe", new Color(0.55f, 0.10f, 0.08f), 0.4f, 0.2f);
            var greyPipe = Mat("Sc07FacGreyPipe", new Color(0.50f, 0.52f, 0.54f), 0.35f, 0.4f);
            var darkSteel = Mat("Sc07FacDarkSteel", new Color(0.22f, 0.23f, 0.25f), 0.3f, 0.3f);
            var steel = Mat("Sc07FacSteel", new Color(0.62f, 0.64f, 0.66f), 0.4f, 0.3f);
            Sc07Static(Sc06HCyl("RedPipeX", room, new Vector3(cx, h - 0.8f, zBack + 0.45f), w, 0.09f, redPipe, true));
            Sc07Static(Sc06HCyl("RedPipeZ", room, new Vector3(ox + 0.7f, h - 0.75f, cz), d, 0.075f, redPipe, false));
            Sc07Static(Sc06HCyl("RedPipeZ2", room, new Vector3(ox + 0.95f, h - 0.75f, cz), d, 0.04f, greyPipe, false));
            Sc07Static(Sc06HCyl("GreyPipeZ", room, new Vector3(ox - 1.2f, h - 0.7f, cz), d, 0.06f, greyPipe, false));
            Sc07Static(Sc06HCyl("GreyPipeX", room, new Vector3(cx, h - 0.62f, 1.0f), w, 0.05f, greyPipe, true));
            for (var cz2 = zBack + 1.5f; cz2 < zFront - 1f; cz2 += 1.5f)
                Sc07Static(Box("TrayRung", room, new Vector3(ox - 1.9f, h - 0.5f, cz2), new Vector3(0.5f, 0.03f, 0.04f), steel, false));
            Sc07Static(Box("CableTray", room, new Vector3(ox - 1.9f, h - 0.52f, cz), new Vector3(0.5f, 0.06f, d - 1f), darkSteel, false));
            // (the bundle of conduits above the panel is built with the panel below)

            // ---- 7. tube lights on chains (emissive meshes; a few real lights), angled like the picture
            var tubeMat = Mat("Sc07FacTube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(2.6f, 2.6f, 2.5f), false);
            var housing = Mat("Sc07FacHousing", new Color(0.62f, 0.63f, 0.63f), 0.4f, 0.3f);
            var tubeSpots = new List<Vector2>();
            foreach (var tz in new[] { 6.5f, 2.0f, -2.9f, -6.5f, -11.0f })
            {
                tubeSpots.Add(new Vector2(ox - 0.3f, tz));
                tubeSpots.Add(new Vector2(ox - 6.4f, tz + 0.8f));
                tubeSpots.Add(new Vector2(ox + 6.0f, tz - 0.6f));
                tubeSpots.Add(new Vector2(ox - 10.8f, tz));
                tubeSpots.Add(new Vector2(ox + 10.4f, tz));
            }

            for (var i = 0; i < tubeSpots.Count; i++)
            {
                var p = new Vector3(tubeSpots[i].x, h - 0.75f, tubeSpots[i].y);
                var parts = new List<GameObject>
                {
                    Box("TubeChainA", room, p + new Vector3(-0.55f, 0.28f, 0f), new Vector3(0.012f, 0.55f, 0.012f), housing, false),
                    Box("TubeChainB", room, p + new Vector3(0.55f, 0.28f, 0f), new Vector3(0.012f, 0.55f, 0.012f), housing, false),
                    Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(1.3f, 0.05f, 0.16f), housing, false),
                    Box("Tube" + i + "a", room, p - new Vector3(0f, 0f, 0.04f), new Vector3(1.25f, 0.04f, 0.04f), tubeMat, false),
                    Box("Tube" + i + "b", room, p + new Vector3(0f, 0f, 0.04f), new Vector3(1.25f, 0.04f, 0.04f), tubeMat, false)
                };
                foreach (var part in parts)
                {
                    part.transform.RotateAround(p, Vector3.up, 90f + (i % 3 - 1) * 8f); // along the aisle, a little off-line like hanging fixtures
                    Sc07Static(part);
                }
            }

            // real lights: aisle + bays (kept few for the Quest)
            var lightSpots = new[]
            {
                new Vector3(ox - 0.3f, h - 1.1f, 5.5f), new Vector3(ox - 0.3f, h - 1.1f, 0.5f), new Vector3(ox - 0.3f, h - 1.1f, -5.5f), new Vector3(ox - 0.3f, h - 1.1f, -11.5f),
                new Vector3(ox - 6.4f, h - 1.1f, 1.5f), new Vector3(ox + 6.0f, h - 1.1f, 1.5f), new Vector3(ox - 6.4f, h - 1.1f, -7.0f), new Vector3(ox + 6.0f, h - 1.1f, -7.0f)
            };
            for (var i = 0; i < lightSpots.Length; i++)
            {
                var lg = new GameObject("HallLight" + i);
                lg.transform.SetParent(room, false);
                lg.transform.position = lightSpots[i];
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.97f, 0.97f, 0.92f);
                l.range = 13f;
                l.intensity = i < 4 ? 6.0f : 5.0f;
                l.shadows = LightShadows.None;
            }

            // ---- 8. white ceiling fans, spinning fast (aisle and both bays)
            var fanWhite = Mat("Sc07FanWhite", new Color(0.93f, 0.93f, 0.91f), 0.35f, 0.1f);
            var fanRod = Mat("Sc07FanRod", new Color(0.80f, 0.80f, 0.78f), 0.3f, 0.2f);
            foreach (var fp in new[]
                     {
                         new Vector2(ox - 0.3f, 3.0f), new Vector2(ox - 0.3f, -4.5f), new Vector2(ox - 0.3f, -10.0f),
                         new Vector2(ox - 7.2f, 1.0f), new Vector2(ox + 6.8f, 1.0f), new Vector2(ox - 7.2f, -7.0f), new Vector2(ox + 6.8f, -7.0f)
                     })
            {
                var fan = new GameObject("CeilingFan").transform;
                fan.SetParent(room, false);
                fan.position = new Vector3(fp.x, h - 0.1f, fp.y);
                const float rod = 1.05f; // hung well below the beams and the tube lights
                Cyl("DownRod", fan, fan.position + Vector3.down * rod, rod, 0.025f, fanRod).transform.SetParent(fan, true);
                var rotor = new GameObject("Rotor").transform;
                rotor.SetParent(fan, false);
                rotor.position = fan.position + Vector3.down * (rod + 0.05f);
                var hub = Cyl("Hub", rotor, rotor.position + Vector3.down * 0.08f, 0.14f, 0.12f, fanWhite);
                hub.transform.SetParent(rotor, true);
                for (var k = 0; k < 3; k++)
                {
                    var pivot = new GameObject("Blade" + k).transform;
                    pivot.SetParent(rotor, false);
                    pivot.localPosition = Vector3.zero;
                    pivot.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
                    var arm = Box("Arm", pivot, rotor.position + pivot.rotation * new Vector3(0.28f, 0f, 0f), new Vector3(0.36f, 0.025f, 0.07f), fanWhite, false);
                    var paddle = Box("Paddle", pivot, rotor.position + pivot.rotation * new Vector3(0.82f, 0f, 0f), new Vector3(0.95f, 0.012f, 0.2f), fanWhite, false);
                    paddle.transform.rotation = pivot.rotation * Quaternion.Euler(12f, 0f, 0f);
                    arm.transform.rotation = pivot.rotation;
                }

                var spin = rotor.gameObject.AddComponent<CeilingFanSpin>();
                var sso = new SerializedObject(spin);
                sso.FindProperty("degreesPerSecond").floatValue = 520f;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 9. yellow lane lines, perpendicular boxes at the pillars, bay borders
            WhYellowLine(room, new Vector3(ox + 1.25f, 0f, zFront - 1f), new Vector3(ox + 1.25f, 0f, zBack + 3f), 0.12f);
            WhYellowLine(room, new Vector3(ox - 1.95f, 0f, zFront - 1f), new Vector3(ox - 1.95f, 0f, zBack + 3f), 0.12f);
            WhYellowLine(room, new Vector3(ox - 2.4f, 0f, extFace + 0.45f), new Vector3(ox + 1.25f, 0f, extFace + 0.45f), 0.12f);
            WhYellowLine(room, new Vector3(ox - 5.6f, 0f, -0.5f), new Vector3(ox - 2.4f, 0f, -0.5f), 0.12f);
            WhYellowLine(room, new Vector3(ox + 1.8f, 0f, -0.9f), new Vector3(ox + 4.6f, 0f, -0.9f), 0.12f);
            WhYellowLine(room, new Vector3(ox + 4.6f, 0f, -0.9f), new Vector3(ox + 4.6f, 0f, -12.5f), 0.12f);
            WhYellowLine(room, new Vector3(ox - 5.6f, 0f, -0.5f), new Vector3(ox - 5.6f, 0f, -12.5f), 0.12f);
            WhYellowLine(room, new Vector3(ox + 4.6f, 0f, 6.5f), new Vector3(ox + 4.6f, 0f, 1.8f), 0.12f);
            WhYellowLine(room, new Vector3(ox - 5.6f, 0f, 6.5f), new Vector3(ox - 5.6f, 0f, 1.8f), 0.12f);

            // ---- 10. signs: FIRE EXTINGUISHER (red) and EMERGENCY EXIT (green) above the extinguishers, like the picture
            var signFire = Mat("Sc07SignFireExt", Color.white, 0.3f, 0f, Sc07Tex("Sign_FireExt"), null, false);
            var signExit = Mat("Sc07SignExitBn", Color.white, 0.3f, 0f, Sc07Tex("Sign_EmergencyExit"), null, false);
            var signBack = Mat("Sc07SignBack", new Color(0.92f, 0.92f, 0.9f), 0.3f, 0f);
            Box("SignFireBack", room, new Vector3(pillarCx + 0.39f, 2.35f, extFace + 0.008f), new Vector3(0.74f, 0.22f, 0.012f), signBack, false);
            QuadObj("SignFireExt", room, new Vector3(pillarCx + 0.39f, 2.35f, extFace + 0.016f), new Vector3(0f, 180f, 0f), new Vector2(0.71f, 0.178f), signFire);
            Box("SignExitBack", room, new Vector3(pillarCx - 0.39f, 2.35f, extFace + 0.008f), new Vector3(0.74f, 0.22f, 0.012f), signBack, false);
            QuadObj("SignEmergencyExit", room, new Vector3(pillarCx - 0.39f, 2.35f, extFace + 0.016f), new Vector3(0f, 180f, 0f), new Vector2(0.71f, 0.178f), signExit);
            // a narrow red vertical sign on the pillar's side and a small red alarm lamp, like the picture
            QuadObj("SignFireSide", room, new Vector3(pillarCx + 0.778f, 2.0f, extFace - 0.35f), new Vector3(0f, 90f, 0f), new Vector2(0.5f, 0.125f), signFire);
            var lampMat = Mat("Sc07FacLamp", new Color(0.9f, 0.08f, 0.06f), 0.5f, 0f, null, new Color(1.6f, 0.12f, 0.08f), false);
            foreach (var lz in new[] { extFace - 0.5f })
            {
                var lamp = Sc06HCyl("AlarmLamp", room, new Vector3(pillarCx + 0.91f, 2.35f, lz), 0.18f, 0.1f, lampMat, true);
                Box("AlarmLampBack", room, new Vector3(pillarCx + 0.80f, 2.35f, lz), new Vector3(0.04f, 0.26f, 0.26f), darkSteel, false);
                var lgo = new GameObject("AlarmLampLight");
                lgo.transform.SetParent(room, false);
                lgo.transform.position = lamp.transform.position + new Vector3(0.4f, 0f, 0.3f);
                var al = lgo.AddComponent<Light>();
                al.type = LightType.Point;
                al.color = new Color(1f, 0.1f, 0.06f);
                al.range = 5f;
                al.intensity = 1.6f;
                al.shadows = LightShadows.None;
            }

            // ---- 11. the grey emergency door at the end of the aisle (Sc07Door: the first runner opens it), green sign above
            const float fleeX = ox - 0.3f;
            const float doorW = 1.9f;
            const float doorH = 2.4f;
            var doorMat = Mat("Sc07FacDoor", new Color(0.50f, 0.52f, 0.53f), 0.35f, 0.3f);
            var barMat = Mat("Sc07FacPushBar", new Color(0.85f, 0.86f, 0.88f), 0.7f, 0.5f);
            var frameMat = Mat("Sc07FacDoorFrame", new Color(0.30f, 0.31f, 0.32f), 0.3f, 0.3f);
            var fleeRoot = new GameObject("FleeDoor").transform;
            fleeRoot.SetParent(room, false);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(fleeRoot, false);
            hinge.position = new Vector3(fleeX + doorW * 0.5f, 0f, zBack + 0.12f);
            Box("DoorLeaf", hinge, new Vector3(fleeX, doorH * 0.5f, zBack + 0.12f), new Vector3(doorW, doorH, 0.07f), doorMat, false);
            Box("DoorBar", hinge, new Vector3(fleeX, 1.05f, zBack + 0.18f), new Vector3(doorW * 0.75f, 0.05f, 0.03f), barMat, false);
            Box("DoorFrameL", fleeRoot, new Vector3(fleeX - doorW * 0.5f - 0.06f, doorH * 0.5f + 0.04f, zBack + 0.1f), new Vector3(0.12f, doorH + 0.1f, 0.14f), frameMat, false);
            Box("DoorFrameR", fleeRoot, new Vector3(fleeX + doorW * 0.5f + 0.06f, doorH * 0.5f + 0.04f, zBack + 0.1f), new Vector3(0.12f, doorH + 0.1f, 0.14f), frameMat, false);
            Box("DoorFrameT", fleeRoot, new Vector3(fleeX, doorH + 0.09f, zBack + 0.1f), new Vector3(doorW + 0.24f, 0.12f, 0.14f), frameMat, false);
            var daylightMat = Mat("Sc07FacDaylight", new Color(1f, 1f, 0.95f), 0f, 0f, null, new Color(2.4f, 2.4f, 2.2f), true);
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
            QuadObj("FireExitSign", fleeRoot, new Vector3(fleeX, 3.2f, zBack + 0.1f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 0.375f), signExit);
            var fleeGlow = new GameObject("FireExitGlow");
            fleeGlow.transform.SetParent(fleeRoot, false);
            fleeGlow.transform.position = new Vector3(fleeX, 3.0f, zBack + 1.4f);
            var fgl = fleeGlow.AddComponent<Light>();
            fgl.type = LightType.Point;
            fgl.color = new Color(0.4f, 1f, 0.55f);
            fgl.range = 5f;
            fgl.intensity = 1.6f;
            fgl.shadows = LightShadows.None;
            var door = fleeRoot.gameObject.AddComponent<Sc07Door>();
            var dso = new SerializedObject(door);
            dso.FindProperty("leaf").objectReferenceValue = hinge;
            dso.FindProperty("daylight").objectReferenceValue = dayLight;
            dso.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc07/Door_Open.wav");
            dso.ApplyModifiedPropertiesWithoutUndo();
            refs.fleeDoor = door;
            // the green exit sign on the near wall and a second one over the worker entry, the arrows on the floor along the aisle
            var arrowFloor = MatAlpha("Sc07FacFloorArrow", Sc07Tex("FloorArrow"), new Color(1f, 1f, 1f, 0.9f), false);
            for (var ai = 0; ai < 4; ai++)
            {
                var fa = QuadObj("FloorArrow" + ai, room, new Vector3(fleeX, 0.012f, -5.0f - ai * 2.3f), Vector3.zero, new Vector2(0.5f, 1.0f), arrowFloor);
                fa.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
                Sc07Static(fa);
            }

            // worker entry: a dark doorway on the back wall, right bay (they walk in from there, stare at the fire, run out through the exit door)
            var entryX = ox + 1.75f; // the workers walk in along the aisle (no pillar in their way) and run out the same way
            Box("EntryFrameL", room, new Vector3(entryX - 0.75f, 1.2f, zBack + 0.08f), new Vector3(0.1f, 2.4f, 0.14f), frameMat, false);
            Box("EntryFrameR", room, new Vector3(entryX + 0.75f, 1.2f, zBack + 0.08f), new Vector3(0.1f, 2.4f, 0.14f), frameMat, false);
            Box("EntryFrameT", room, new Vector3(entryX, 2.4f, zBack + 0.08f), new Vector3(1.6f, 0.1f, 0.14f), frameMat, false);
            QuadObj("EntryDark", room, new Vector3(entryX, 1.2f, zBack + 0.1f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 2.4f), Mat("Sc07FacEntryDark", new Color(0.05f, 0.06f, 0.06f), 0f, 0f));

            // ---- 12. sewing machine benches: real glb in the two inner rows, cheap proxies further out, stools, fabric
            var bays = new GameObject("Bays").transform;
            bays.SetParent(room, false);
            var woodMat = Mat("Sc07FacWood", new Color(0.50f, 0.34f, 0.20f), 0.25f, 0f);
            var tableWhite = Mat("Sc07FacTableTop", new Color(0.90f, 0.90f, 0.88f), 0.3f, 0f);
            var machineGrey = Mat("Sc07FacMachine", new Color(0.62f, 0.64f, 0.65f), 0.4f, 0.3f);
            var cloth = new[]
            {
                Mat("Sc07FacClothNavy", new Color(0.10f, 0.14f, 0.30f), 0.1f, 0f), Mat("Sc07FacClothMaroon", new Color(0.40f, 0.10f, 0.14f), 0.1f, 0f),
                Mat("Sc07FacClothGrey", new Color(0.45f, 0.46f, 0.48f), 0.1f, 0f), Mat("Sc07FacClothBeige", new Color(0.70f, 0.62f, 0.46f), 0.1f, 0f)
            };
            // fabric in many colours and kinds, on the benches and beside them (real-feel: garments being sewn)
            var palette = new[]
            {
                new Color(0.78f, 0.12f, 0.16f), new Color(0.12f, 0.20f, 0.46f), new Color(0.22f, 0.55f, 0.82f), new Color(0.15f, 0.55f, 0.32f),
                new Color(0.93f, 0.78f, 0.15f), new Color(0.92f, 0.45f, 0.12f), new Color(0.90f, 0.45f, 0.62f), new Color(0.92f, 0.92f, 0.90f),
                new Color(0.45f, 0.46f, 0.50f), new Color(0.48f, 0.22f, 0.60f), new Color(0.10f, 0.50f, 0.52f), new Color(0.40f, 0.10f, 0.14f)
            };
            var fabricMats = new Material[palette.Length];
            for (var pi = 0; pi < palette.Length; pi++)
                fabricMats[pi] = Mat("Sc07Fabric" + pi, palette[pi], 0.08f, 0f);
            var fr = new System.Random(5);
            void Fabric(string name, Vector3 centre, Vector3 size, float yaw, int colour)
            {
                var f = Box(name, bays, centre, size, fabricMats[colour % fabricMats.Length], false);
                f.transform.rotation = Quaternion.Euler(fr.Next(-4, 5), yaw, fr.Next(-4, 5));
                Sc07Static(f);
            }

            var unitZ = new[] { 3.2f, 1.7f, 0.2f, -1.3f, -2.8f, -4.3f, -5.8f, -7.3f, -8.8f, -10.3f, -11.8f };
            var rowDefs = new[]
            {
                new { x = ox + 4.6f, yaw = 90f, real = true }, new { x = ox - 5.1f, yaw = 270f, real = true },
                new { x = ox + 7.0f, yaw = 90f, real = true }, new { x = ox - 7.5f, yaw = 270f, real = true },
                new { x = ox + 10.2f, yaw = 90f, real = false }, new { x = ox - 10.6f, yaw = 270f, real = false }
            };
            var pillarSpots = new List<Vector2>();
            foreach (var px in new[] { ox - 8.9f, ox - 3.4f, ox + 2.6f, ox + 8.1f })
                foreach (var pz in new[] { 3.7f, -2.3f, -8.3f })
                    pillarSpots.Add(new Vector2(px, pz));
            var unitIndex = 0;
            foreach (var row in rowDefs)
            {
                for (var i = 0; i < unitZ.Length; i++)
                {
                    var uz = unitZ[i];
                    var blocked = false;
                    foreach (var ps in pillarSpots)
                        if (Mathf.Abs(row.x - ps.x) < 1.5f && Mathf.Abs(uz - ps.y) < 1.1f)
                            blocked = true;
                    if (blocked)
                        continue;
                    unitIndex++;
                    if (!row.real)
                    {
                        var stoolX = row.x + (row.yaw < 180f ? -0.85f : 0.85f);
                        Sc07Static(Cyl("StoolTop", bays, new Vector3(stoolX, 0.43f, uz), 0.05f, 0.2f, woodMat));
                        Sc07Static(Cyl("StoolLeg", bays, new Vector3(stoolX, 0f, uz), 0.43f, 0.035f, darkSteel));
                    }

                    if (row.real)
                    {
                        var model = SpawnSc06("SewingMachine.glb", bays);
                        if (model == null)
                            continue;
                        model.name = "SewingMachine" + unitIndex;
                        // the glb's own shader graph renders flat and washed out in the hall: lit URP materials with the same textures
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
                        // native proportions (no stretching): the scale is set so that the table top (the wood sub-mesh) is 0.80 m high
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
                        model.transform.rotation = Quaternion.Euler(0f, row.yaw, 0f) * model.transform.rotation;
                        b = BoundsOfTree(model.transform);
                        model.transform.position += new Vector3(row.x - b.center.x, -b.min.y, uz - b.center.z);
                        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                            r.shadowCastingMode = ShadowCastingMode.Off;
                        b = BoundsOfTree(model.transform);
                        // where the operator's stool is (it belongs to the glb): cloth goes on the table, a bundle by the stool
                        var stoolPos = new Vector3(b.center.x, 0f, b.center.z);
                        foreach (var tr in model.GetComponentsInChildren<Transform>(true))
                            if (tr.name.StartsWith("stool"))
                            {
                                var sr = tr.GetComponentInChildren<Renderer>();
                                if (sr != null)
                                    stoolPos = new Vector3(sr.bounds.center.x, 0f, sr.bounds.center.z);
                            }

                        var tableC = new Vector3(b.center.x, 0.80f, b.center.z);
                        var toStool = new Vector3(stoolPos.x - tableC.x, 0f, stoolPos.z - tableC.z);
                        var dirS = toStool.sqrMagnitude > 0.0001f ? toStool.normalized : Vector3.right;
                        var perp = new Vector3(-dirS.z, 0f, dirS.x);
                        var yawS = Mathf.Atan2(dirS.x, dirS.z) * Mathf.Rad2Deg;
                        var c0 = fr.Next(0, palette.Length);
                        // thread cones on a small stand at the far corner of the table, like the picture
                        var standAt = tableC - dirS * 0.42f + perp * 0.20f;
                        Sc07Static(Cyl("ThreadStandBase", bays, new Vector3(standAt.x, 0.80f, standAt.z), 0.012f, 0.07f, darkSteel));
                        Sc07Static(Cyl("ThreadStandPole", bays, new Vector3(standAt.x, 0.80f, standAt.z), 0.50f, 0.007f, darkSteel));
                        var coneCols = new[] { new Color(0.05f, 0.45f, 0.25f), new Color(0.05f, 0.42f, 0.45f), new Color(0.12f, 0.30f, 0.65f), new Color(0.90f, 0.78f, 0.15f), new Color(0.85f, 0.15f, 0.15f), new Color(0.92f, 0.92f, 0.9f) };
                        for (var cn = 0; cn < 2; cn++)
                            Sc07Static(Cyl("ThreadCone", bays, new Vector3(standAt.x, 0.80f + 0.30f + cn * 0.18f, standAt.z), 0.16f, 0.04f,
                                Mat("Sc07Thread" + ((unitIndex + cn * 2) % coneCols.Length), coneCols[(unitIndex + cn * 2) % coneCols.Length], 0.3f, 0f)));
                        Fabric("SewnCloth", tableC + dirS * 0.05f + Vector3.up * 0.012f, new Vector3(0.30f, 0.014f, 0.22f), yawS + fr.Next(-15, 15), c0);
                        Fabric("SewnCloth2", tableC + dirS * 0.10f + Vector3.up * 0.028f, new Vector3(0.22f, 0.014f, 0.18f), yawS + fr.Next(-25, 25), c0 + 3);
                        if (unitIndex % 2 == 0)
                            for (var st = 0; st < 3; st++)
                                Fabric("FoldedStack", tableC - dirS * 0.40f + perp * 0.05f + Vector3.up * (0.03f + st * 0.04f), new Vector3(0.24f, 0.036f, 0.20f), yawS + fr.Next(-12, 12), c0 + st * 2 + 1);
                        if (unitIndex % 3 == 0)
                        {
                            Fabric("FloorBundle", stoolPos + perp * 0.75f + Vector3.up * 0.13f, new Vector3(0.5f, 0.26f, 0.40f), fr.Next(0, 180), c0 + 5);
                            Fabric("FloorBundleTop", stoolPos + perp * 0.75f + Vector3.up * 0.30f, new Vector3(0.40f, 0.10f, 0.32f), fr.Next(0, 180), c0 + 7);
                        }

                        foreach (var t in model.GetComponentsInChildren<Transform>(true))
                            if (t.name == "dikis_makinesi_ignesi")
                            {
                                var bob = t.gameObject.AddComponent<Sc06Bobber>();
                                var bso = new SerializedObject(bob);
                                bso.FindProperty("amplitude").floatValue = 0.012f;
                                bso.FindProperty("hertz").floatValue = 3f + (unitIndex % 5) * 0.3f;
                                bso.ApplyModifiedPropertiesWithoutUndo();
                                break;
                            }
                    }
                    else
                    {
                        // proxy bench: white-topped table, wooden legs, a grey machine head
                        Sc07Static(Box("ProxyTop", bays, new Vector3(row.x, 0.8f, uz), new Vector3(0.8f, 0.04f, 1.3f), tableWhite, false));
                        foreach (var lx in new[] { -0.35f, 0.35f })
                            foreach (var lz in new[] { -0.6f, 0.6f })
                                Sc07Static(Box("ProxyLeg", bays, new Vector3(row.x + lx, 0.4f, uz + lz), new Vector3(0.04f, 0.8f, 0.04f), woodMat, false));
                        Sc07Static(Box("ProxyMachine", bays, new Vector3(row.x, 0.93f, uz), new Vector3(0.34f, 0.22f, 0.6f), machineGrey, false));
                        Sc07Static(Box("ProxyHead", bays, new Vector3(row.x + (row.yaw < 180f ? 0.08f : -0.08f), 1.1f, uz - 0.1f), new Vector3(0.24f, 0.18f, 0.24f), machineGrey, false));
                        if (unitIndex % 3 == 1)
                            Sc07Static(Box("ProxyCloth", bays, new Vector3(row.x, 0.88f, uz + 0.3f), new Vector3(0.55f, 0.1f, 0.38f), cloth[unitIndex % cloth.Length], false));
                    }
                }
            }

            // fabric piles and bales (the picture: dark blue heaps near the fire pillar, brown bales and cartons at the end of the aisle)
            var bale = Mat("Sc07FacBale", new Color(0.52f, 0.42f, 0.28f), 0.1f, 0f);
            var carton = Mat("Sc07FacCarton", new Color(0.55f, 0.42f, 0.27f), 0.1f, 0f);
            for (var i = 0; i < 4; i++)
            {
                var bb = Cyl("Bale" + i, bays, new Vector3(ox + 3.3f + (i % 2) * 0.95f, i < 2 ? 0f : 0.9f, -12.2f), 0.9f, 0.46f, bale);
                bb.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                bb.transform.position = new Vector3(ox + 3.3f + (i % 2) * 0.95f, i < 2 ? 0.46f : 1.38f, -12.2f);
                bb.transform.localScale = new Vector3(0.92f, 0.45f, 0.92f);
                Sc07Static(bb);
            }

            foreach (var cp in new[] { new Vector3(ox + 3.6f, 0.2f, -6.0f), new Vector3(ox + 3.9f, 0.55f, -6.1f), new Vector3(ox - 5.1f, 0.2f, -5.0f), new Vector3(ox + 3.4f, 0.2f, 1.0f) })
            {
                var pile = Box("ClothPile", bays, cp, new Vector3(1.0f, 0.5f, 0.8f), cloth[(int)Mathf.Abs(cp.x + cp.z) % cloth.Length], false);
                pile.transform.rotation = Quaternion.Euler(0f, 20f * cp.z, 0f);
                Sc07Static(pile);
            }

            Sc07Static(Box("CartonStack0", bays, new Vector3(ox - 5.2f, 0.3f, 3.2f), new Vector3(0.9f, 0.6f, 0.7f), carton, false));
            Sc07Static(Box("CartonStack1", bays, new Vector3(ox + 4.0f, 0.3f, -10.0f), new Vector3(0.9f, 0.6f, 0.7f), carton, false));

            // dust in the air
            var dustMat = ParticleMat("Sc07FacDust", art.radialGlow != null ? art.radialGlow.texture : null, true);
            BuildDust(room, new Vector3(ox - 0.3f, 2.4f, -3f), new Vector3(12f, 3.6f, 18f), dustMat);

            // the red alarm beacon (pulsing, with its halo) goes on the extinguisher pillar's aisle-facing side, under the static lamp, like the
            // client picture (two red lamps stacked on the pillar edge)
            var beaconTarget = new Vector3(pillarCx + 0.775f + 0.10f, 3.25f, extFace - 0.55f);
            var beaconModelT = Sc06Find(room, "FireAlarmBeacon");
            if (beaconModelT != null)
            {
                var bbm = BoundsOfTree(beaconModelT);
                beaconModelT.position += beaconTarget - bbm.center;
                beaconModelT.RotateAround(beaconTarget, Vector3.up, 90f);
            }

            var beaconFxT = Sc06Find(room, "BeaconFx");
            if (beaconFxT != null)
            {
                beaconFxT.position = beaconTarget;
                beaconFxT.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // ---- 13. re-place the kept markers (a player at the aisle entrance sees both pillars and the whole hall)
            void Put(string n, Vector3 pos, float yaw)
            {
                var t = Sc06Find(room, n);
                if (t != null)
                    t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }

            Put("Spawn_SC07", new Vector3(ox - 0.35f, 0f, 2.4f), 180f);
            Put("Stand_SC07_Pillar", new Vector3(pillarCx, 0f, extFace + 1.15f), 180f);
            Put("Stand_SC07_Question", new Vector3(ox - 0.35f, 0f, 2.4f), 180f);
            Put("Sc07HeroWait", new Vector3(ox - 1.5f, 0f, -1.3f), 0f); // right side of the aisle: the burning cabinet stays visible
            Put("Sc07HeroSpot", new Vector3(ox - 0.6f, 0f, 0.4f), 180f);
            Put("Sc07PillarGrab", new Vector3(pillarCx - 0.45f, 0f, extFace + 0.85f), 180f); // well in front of the pillar and the CO2 extinguisher, never touching the wall
            Put("Sc07SpraySpot", new Vector3(fireNew.x - 1.0f, 0f, fireNew.z + 2.3f), 180f);
            Put("Sc07HeroStart", new Vector3(pillarCx + 2.1f, 0f, extFace + 1.7f), 180f); // starts out in the aisle, far from the pillar
            Put("Sc07DoorStart", new Vector3(entryX, 0f, zBack + 0.9f), 0f);
            Put("Sc07ExitPoint", new Vector3(fleeX, 0f, zBack + 0.25f), 180f);
            Put("Sc07ExitApproach", new Vector3(fleeX, 0f, zBack + 3.4f), 180f);
            var noticeAt = new[] { new Vector3(ox - 0.6f, 0f, -0.4f), new Vector3(ox - 1.1f, 0f, 0.8f), new Vector3(ox - 2.0f, 0f, 0.2f), new Vector3(ox - 0.3f, 0f, 0.8f) };
            for (var i = 0; i < noticeAt.Length; i++)
                Put("Notice" + (i + 1), noticeAt[i], 0f);
            // the workers wait at the entry doorway until the alarm
            var crowdRoot = Sc06Find(room, "Crowd");
            if (crowdRoot != null)
                foreach (Transform worker in crowdRoot)
                {
                    if (worker.name == "Worker_Hero")
                        worker.SetPositionAndRotation(new Vector3(ox - 1.5f, 0f, -1.3f), Quaternion.Euler(0f, 0f, 0f));
                    else
                        worker.SetPositionAndRotation(new Vector3(entryX, 0f, zBack + 0.9f), Quaternion.Euler(0f, 0f, 0f));
                }

            // garment workers: short hair, a measuring tape round the neck, cloth / scissors instead of the old tools
            if (crowdRoot != null)
                foreach (Transform worker in crowdRoot)
                {
                    if (worker.name == "Worker_Hero" || !worker.name.StartsWith("Worker_P"))
                        continue;
                    Sc07Garmentify(worker, worker.name == "Worker_P3", worker.name == "Worker_P4", worker.name == "Worker_P1" ? 0 : worker.name == "Worker_P2" ? 1 : worker.name == "Worker_P3" ? 2 : 3);
                }

            // the crowd director points at the new door
            if (refs.crowd != null)
            {
                var cso = new SerializedObject(refs.crowd);
                cso.FindProperty("fleeDoor").objectReferenceValue = refs.fleeDoor;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Short hair, a yellow measuring tape round the neck and, by worker, cloth in the left hand / scissors in the right.
        private static void Sc07Garmentify(Transform worker, bool cloth, bool scissors, int style)
        {
            Transform Bone(string n)
            {
                foreach (var t in worker.GetComponentsInChildren<Transform>(true))
                    if (t.name == n)
                        return t;
                return null;
            }

            var sc = Mathf.Max(0.01f, worker.localScale.x);
            var head = Bone("Head");
            var neck = Bone("Neck");
            var handL = Bone("HandL");
            var handR = Bone("HandR");
            // the old tools (toolbox, wrench) go
            foreach (var hand in new[] { handL, handR })
                if (hand != null)
                    for (var i = hand.childCount - 1; i >= 0; i--)
                    {
                        var c = hand.GetChild(i);
                        if (c.name == "ToolBox" || c.name == "ToolBoxLid" || c.name.StartsWith("Wrench"))
                            Object.DestroyImmediate(c.gameObject);
                    }

            GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = localScale;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return go;
            }

        }
    }
}
#endif

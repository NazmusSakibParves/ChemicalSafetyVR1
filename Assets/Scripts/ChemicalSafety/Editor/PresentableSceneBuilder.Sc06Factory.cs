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
    /// Scenario 6, the client's garment-factory mixing room (look-alike of the client's picture): old dirty plaster with a broken patch
    /// showing brick, a pale-blue band, concrete floor, tubes, the fume hood with its two switch boxes and the wall exhaust fan, a steel
    /// table carrying the cardboard box and the three containers (empty MIXTURE jar, acid can, water jug) and a sewing machine bench.
    /// <c>Sc06UseFactory = false</c> + Build Presentable Scene gives the previous chemistry lab back (nothing of it is deleted).
    /// The old lab is built first and its pieces that the new room does not use are simply switched off here.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc06UseFactory = true;

        // pieces of the old lab that stay (the hood and its sash, the switches and their lamps, the hood light, the logic hosts, the SDS)
        private static readonly string[] Sc06KeepPrefixes =
        {
            "FumeHood", "Sash", "HoodTube", "HoodBackTint", "HoodLight", "HoodPlate", "SillVent", "BaffleSlot", "Rocker", "Led", "SwitchPoke",
            "Sc06Lab", "Sc06Ambience", "Sc06GateHost", "LevNozzle", "Sc06ReflectionProbe", "PosterSds", "SdsPocket"
        };

        // Crisp small text: uncompressed on every platform, 2048 max, strong anisotropic filtering.
        private static void Sc06SharpTexture(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
                return;
            var changed = false;
            if (imp.anisoLevel < 16)
            {
                imp.anisoLevel = 16;
                changed = true;
            }

            if (imp.textureCompression != TextureImporterCompression.Uncompressed)
            {
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            foreach (var platform in new[] { "Android", "Standalone" })
            {
                var ps = imp.GetPlatformTextureSettings(platform);
                if (!ps.overridden || ps.format != TextureImporterFormat.RGBA32 || ps.maxTextureSize < 2048)
                {
                    ps.overridden = true;
                    ps.format = TextureImporterFormat.RGBA32;
                    ps.maxTextureSize = 2048;
                    imp.SetPlatformTextureSettings(ps);
                    changed = true;
                }
            }

            if (changed)
                imp.SaveAndReimport();
        }

        private static Texture2D Sc06FacTex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc06/" + n + ".png");

        private static GameObject Sc06HCyl(string name, Transform parent, Vector3 centre, float length, float radius, Material mat, bool alongX)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(alongX ? 0f : 90f, 0f, alongX ? 90f : 0f);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // Moves a named object of the old lab (found anywhere under the room) by a world offset.
        private static Transform Sc06Find(Transform room, string name)
        {
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void Sc06Shift(Transform room, string name, Vector3 delta)
        {
            var t = Sc06Find(room, name);
            if (t != null)
                t.position += delta;
        }

        private static void Sc06ApplyFactory(Transform room, Sc06Refs refs, PresentableArt art, float hoodCentreX, float surfaceY, float sashZ,
            float interiorBack, float hoodFrontZ)
        {
            const float ox = Sc06Ox;
            const float zBack = -2.9f;
            const float zFront = 3.4f;
            const float h = 3.2f;
            var x0 = ox - 7.6f;
            var x1 = ox + 3.7f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (zBack + zFront) * 0.5f;
            var w = x1 - x0;
            var d = zFront - zBack;

            // ---- switch off every piece of the old lab that the factory room does not use
            foreach (Transform child in room)
            {
                var keep = false;
                foreach (var p in Sc06KeepPrefixes)
                    if (child.name.StartsWith(p))
                    {
                        keep = true;
                        break;
                    }

                if (!keep)
                    child.gameObject.SetActive(false);
            }

            // the hood body in stainless steel like the picture
            if (Mats.TryGetValue("Sc06HoodPaint", out var hoodSteel))
            {
                hoodSteel.SetColor("_BaseColor", new Color(0.66f, 0.69f, 0.71f));
                hoodSteel.SetFloat("_Metallic", 0.35f);
                hoodSteel.SetFloat("_Smoothness", 0.38f);
            }

            // ---- mood: bright dusty grey like the picture, haze only far away
            var moodHost = room.parent;
            var mood = moodHost != null ? moodHost.GetComponent<Sc03Mood>() : null;
            if (mood != null)
            {
                var mso = new SerializedObject(mood);
                mso.FindProperty("ambientSky").colorValue = new Color(1.0f, 0.99f, 0.96f);
                mso.FindProperty("ambientEquator").colorValue = new Color(0.84f, 0.83f, 0.80f);
                mso.FindProperty("ambientGround").colorValue = new Color(0.40f, 0.39f, 0.37f);
                mso.FindProperty("fogColor").colorValue = new Color(0.74f, 0.75f, 0.74f);
                mso.FindProperty("fogStart").floatValue = 9f;
                mso.FindProperty("fogEnd").floatValue = 46f;
                mso.FindProperty("reflectionIntensity").floatValue = 0.5f;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- shell: old plaster, concrete floor and ceiling (metric UVs), pale-blue band
            var wallMat = Sc01Pbr("Sc06FacWall", "Wall", new Color(1.55f, 1.55f, 1.52f), 0.12f, 1f);
            var floorMat = Sc01Pbr("Sc06FacFloor", "Wall", new Color(0.80f, 0.77f, 0.70f), 0.25f, 1f);
            var ceilMat = Sc01Pbr("Sc06FacCeil", "Wall", new Color(1.05f, 1.04f, 1.0f), 0.1f, 0.9f);
            var bandMat = Sc01Pbr("Sc06FacBand", "Wall", new Color(0.60f, 0.70f, 0.80f), 0.2f, 1f);
            TexBox("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), 0f, floorMat, 2.4f, true);
            var floorSolid = room.Find("FloorSolid");
            if (floorSolid != null)
                floorSolid.gameObject.AddComponent<TeleportationArea>();
            TexBox("Wall_Back", room, new Vector3(cx, h * 0.5f, zBack - 0.06f), new Vector3(w, h, 0.12f), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Front", room, new Vector3(cx, h * 0.5f, zFront + 0.06f), new Vector3(w, h, 0.12f), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Left", room, new Vector3(x1 + 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Right", room, new Vector3(x0 - 0.06f, h * 0.5f, cz), new Vector3(0.12f, h, d), 0f, wallMat, 2.4f, true);
            Sheet("Ceiling", room, new Vector3(x0, h, zBack), new Vector3(w, 0f, 0f), new Vector3(0f, 0f, d), Vector3.down, ceilMat, 2.4f, 2.4f);
            Box("BandBack", room, new Vector3(cx, 0.55f, zBack + 0.006f), new Vector3(w, 1.1f, 0.012f), bandMat, false);
            Box("BandLeft", room, new Vector3(x1 - 0.006f, 0.55f, cz), new Vector3(0.012f, 1.1f, d), bandMat, false);
            Box("BandRight", room, new Vector3(x0 + 0.006f, 0.55f, cz), new Vector3(0.012f, 1.1f, d), bandMat, false);
            Box("BandFront", room, new Vector3(cx, 0.55f, zFront - 0.006f), new Vector3(w, 1.1f, 0.012f), bandMat, false);
            var bandEdge = Mat("Sc06FacBandEdge", new Color(0.30f, 0.34f, 0.38f), 0.2f, 0f);
            Box("BandEdgeBack", room, new Vector3(cx, 1.105f, zBack + 0.008f), new Vector3(w, 0.02f, 0.016f), bandEdge, false);
            Box("BandEdgeLeft", room, new Vector3(x1 - 0.008f, 1.105f, cz), new Vector3(0.016f, 0.02f, d), bandEdge, false);
            Box("BandEdgeRight", room, new Vector3(x0 + 0.008f, 1.105f, cz), new Vector3(0.016f, 0.02f, d), bandEdge, false);

            // ---- dirt: oil blots and rust on the floor, smudges and drips on the walls (quads face -Z: a 180 turn on +Z-facing walls)
            var oil = MatAlpha("Sc06StainOil", EnvTex("STAIN_Oil"), new Color(0.10f, 0.08f, 0.06f, 0.38f), false);
            var rust = MatAlpha("Sc06StainRust", EnvTex("STAIN_Rust"), new Color(0.5f, 0.28f, 0.14f, 0.45f), false);
            var rs = new System.Random(61);
            for (var i = 0; i < 70; i++)
                QuadObj("FloorBlot" + i, room, new Vector3(x0 + 0.6f + (float)rs.NextDouble() * (w - 1.2f), 0.011f + i * 0.0003f, zBack + 0.5f + (float)rs.NextDouble() * (d - 1f)),
                    new Vector3(90f, (float)rs.NextDouble() * 360f, 0f), Vector2.one * (0.9f + (float)rs.NextDouble() * 2.0f), i % 4 == 3 ? rust : oil);
            var blotTex = Sc03Tex("Stain_Blot");
            if (blotTex != null)
            {
                var smudge = MatAlpha("Sc06FacSmudgeSoft", blotTex, new Color(0.34f, 0.30f, 0.25f, 0.20f), false);
                for (var i = 0; i < 56; i++)
                {
                    var y = 0.3f + (float)rs.NextDouble() * 2.7f;
                    var sz = 0.7f + (float)rs.NextDouble() * 1.5f;
                    var kind = i % 4;
                    if (kind == 0)
                        QuadObj("SmudgeL" + i, room, new Vector3(x1 - 0.012f, y, zBack + 0.5f + (float)rs.NextDouble() * (d - 1f)), new Vector3(0f, 90f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                    else if (kind == 1)
                        QuadObj("SmudgeR" + i, room, new Vector3(x0 + 0.012f, y, zBack + 0.5f + (float)rs.NextDouble() * (d - 1f)), new Vector3(0f, -90f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                    else
                        QuadObj("SmudgeB" + i, room, new Vector3(x0 + 0.6f + (float)rs.NextDouble() * (w - 1.2f), y, zBack + 0.012f), new Vector3(0f, 180f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                }
            }

            var grimeTex = Sc03Tex("Grime_Drip");
            if (grimeTex != null)
            {
                var drip = MatAlpha("Sc06FacDripSoft", grimeTex, new Color(0.30f, 0.26f, 0.21f, 0.34f), false);
                for (var i = 0; i < 9; i++)
                    QuadObj("WallDrip" + i, room, new Vector3(x0 + 0.8f + i * (w - 1.6f) / 8f, 2.0f, zBack + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(1.6f, 2.4f), drip);
            }

            // broken plaster showing old brick (left wall near the front, a smaller one on the back wall, like the picture)
            var brickTex = Sc06FacTex("Wall_BrickPatch");
            if (brickTex != null)
            {
                var brick = Mat("Sc06BrickPatch", Color.white, 0.15f, 0f, brickTex, null, false, true);
                QuadObj("BrickPatchL", room, new Vector3(x1 - 0.014f, 1.45f, -3.0f), new Vector3(0f, 90f, 0f), new Vector2(1.0f, 1.25f), brick);
                QuadObj("BrickPatchL2", room, new Vector3(x1 - 0.014f, 0.55f, -2.6f), new Vector3(0f, 90f, 0f), new Vector2(0.5f, 0.62f), brick);
                QuadObj("BrickPatchB", room, new Vector3(ox + 3.5f, 1.45f, zBack + 0.014f), new Vector3(0f, 180f, 0f), new Vector2(1.0f, 1.25f), brick);
                QuadObj("BrickPatchC", room, new Vector3(ox - 4.55f, 1.75f, zBack + 0.014f), new Vector3(0f, 180f, 0f), new Vector2(0.85f, 1.05f), brick);
                QuadObj("BrickPatchD", room, new Vector3(ox + 0.35f, 2.75f, zBack + 0.014f), new Vector3(0f, 180f, 0f), new Vector2(0.4f, 0.5f), brick);
            }

            // yellow floor lanes
            WhYellowLine(room, new Vector3(ox + 1.75f, 0f, 3.2f), new Vector3(ox + 1.75f, 0f, -2.6f));
            WhYellowLine(room, new Vector3(ox - 2.5f, 0f, 3.2f), new Vector3(ox - 2.5f, 0f, -0.8f));
            WhYellowLine(room, new Vector3(ox - 2.5f, 0f, -0.8f), new Vector3(ox - 0.2f, 0f, -0.8f));

            // ---- ceiling: beams, pipes, a cable tray and five hanging tube fixtures (two flicker)
            var beam = Mat("Sc06FacBeam", new Color(0.60f, 0.59f, 0.56f), 0.12f, 0f);
            var pipeMat = Mat("Sc06FacPipe", new Color(0.42f, 0.44f, 0.46f), 0.4f, 0.5f);
            var housing = Mat("Sc06FacHousing", new Color(0.56f, 0.57f, 0.57f), 0.4f, 0.3f);
            for (var z = 2.4f; z > zBack; z -= 2.6f)
                Box("CeilBeam", room, new Vector3(cx, h - 0.17f, z), new Vector3(w, 0.3f, 0.26f), beam, false);
            Sc06HCyl("CeilPipeX", room, new Vector3(cx, h - 0.36f, 1.0f), w, 0.05f, pipeMat, true);
            Sc06HCyl("CeilPipeX2", room, new Vector3(cx, h - 0.30f, 1.12f), w, 0.03f, pipeMat, true);
            var tubeMat = Mat("Sc06FacTube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(2.4f, 2.4f, 2.3f), false);
            var tubeSpots = new[] { new Vector2(ox + 1.0f, 1.5f), new Vector2(ox - 1.8f, 1.2f), new Vector2(ox + 0.4f, -0.9f), new Vector2(ox - 3.0f, -1.2f), new Vector2(ox + 2.3f, -1.9f) };
            var flicks = new List<Sc05FactoryFx.Flicker>();
            for (var i = 0; i < tubeSpots.Length; i++)
            {
                var p = new Vector3(tubeSpots[i].x, h - 0.5f, tubeSpots[i].y);
                var parts = new List<GameObject>
                {
                    Box("TubeChainA", room, p + new Vector3(-0.95f, 0.22f, 0f), new Vector3(0.012f, 0.4f, 0.012f), housing, false),
                    Box("TubeChainB", room, p + new Vector3(0.95f, 0.22f, 0f), new Vector3(0.012f, 0.4f, 0.012f), housing, false),
                    Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(2.2f, 0.05f, 0.18f), housing, false)
                };
                var t1 = Box("Tube" + i + "a", room, p - new Vector3(0f, 0f, 0.04f), new Vector3(2.1f, 0.04f, 0.04f), tubeMat, false);
                parts.Add(t1);
                parts.Add(Box("Tube" + i + "b", room, p + new Vector3(0f, 0f, 0.04f), new Vector3(2.1f, 0.04f, 0.04f), tubeMat, false));
                foreach (var part in parts)
                    part.transform.RotateAround(p, Vector3.up, i % 2 == 0 ? 20f : -14f); // angled like the picture's hanging fixtures
                var lg = new GameObject("TubeLight" + i);
                lg.transform.SetParent(room, false);
                lg.transform.position = p + Vector3.down * 0.5f;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.94f, 0.96f, 0.92f);
                l.range = 8f;
                l.intensity = 3.4f;
                l.shadows = LightShadows.None;
                if (i == 1 || i == 3)
                    flicks.Add(new Sc05FactoryFx.Flicker { light = l, glow = t1.GetComponent<Renderer>(), baseIntensity = l.intensity, seed = i * 5.1f });
            }

            var fxGo = new GameObject("FactoryFx06");
            fxGo.transform.SetParent(room, false);
            var fx = fxGo.AddComponent<Sc05FactoryFx>();
            var fso = new SerializedObject(fx);
            FillArray(fso.FindProperty("flickers"), flicks.Count, (p, i) =>
            {
                p.FindPropertyRelative("light").objectReferenceValue = flicks[i].light;
                p.FindPropertyRelative("glow").objectReferenceValue = flicks[i].glow;
                p.FindPropertyRelative("baseIntensity").floatValue = flicks[i].baseIntensity;
                p.FindPropertyRelative("seed").floatValue = flicks[i].seed;
            });
            fso.ApplyModifiedPropertiesWithoutUndo();

            // ---- back wall: the hierarchy-of-controls poster (the client's picture, readable), signs, spiral duct, hood duct
            var steel = Mat("Sc06FacSteelPaint", new Color(0.70f, 0.72f, 0.74f), 0.5f, 0.3f);
            var darkSteel = Mat("Sc06FacDarkSteel", new Color(0.26f, 0.28f, 0.30f), 0.4f, 0.3f);
            var posterTex = Sc06FacTex("HierarchyPoster");
            Sc06SharpTexture(ArtRoot + "/2D/Sc06/HierarchyPoster.png"); // no compression blur, high anisotropy: the small print stays readable
            // the hierarchy-of-controls poster, large (2.5 m wide) and lit by its own spot so it reads from the learner's spot
            const float posterX = ox + 2.1f;
            const float posterY = 1.85f;
            const float posterW = 2.46f;
            var posterH = posterW * 618f / 960f;
            // client (VR): the white poster glared and made the instruction box unreadable: an aged, dimmer sheet and a soft spot (still readable)
            Box("PosterBack", room, new Vector3(posterX, posterY, zBack + 0.012f), new Vector3(posterW + 0.1f, posterH + 0.1f, 0.02f), Mat("Sc06FacPosterBack", new Color(0.62f, 0.62f, 0.58f), 0.2f, 0f), false);
            if (posterTex != null)
                QuadObj("PosterHierarchy", room, new Vector3(posterX, posterY, zBack + 0.024f), new Vector3(0f, 180f, 0f), new Vector2(posterW, posterH),
                    MatAlpha("Sc06FacPoster", posterTex, new Color(0.66f, 0.66f, 0.64f, 1f), false));
            Box("PosterFrameT", room, new Vector3(posterX, posterY + posterH * 0.5f + 0.05f, zBack + 0.02f), new Vector3(posterW + 0.14f, 0.04f, 0.03f), darkSteel, false);
            Box("PosterFrameB", room, new Vector3(posterX, posterY - posterH * 0.5f - 0.05f, zBack + 0.02f), new Vector3(posterW + 0.14f, 0.04f, 0.03f), darkSteel, false);
            Box("PosterFrameL", room, new Vector3(posterX + posterW * 0.5f + 0.05f, posterY, zBack + 0.02f), new Vector3(0.04f, posterH + 0.14f, 0.03f), darkSteel, false);
            Box("PosterFrameR", room, new Vector3(posterX - posterW * 0.5f - 0.05f, posterY, zBack + 0.02f), new Vector3(0.04f, posterH + 0.14f, 0.03f), darkSteel, false);
            var posterLightGo = new GameObject("PosterSpot");
            posterLightGo.transform.SetParent(room, false);
            posterLightGo.transform.position = new Vector3(posterX, 2.95f, zBack + 1.7f);
            posterLightGo.transform.rotation = Quaternion.LookRotation(new Vector3(posterX, posterY, zBack) - posterLightGo.transform.position, Vector3.up);
            var posterLight = posterLightGo.AddComponent<Light>();
            posterLight.type = LightType.Spot;
            posterLight.spotAngle = 75f;
            posterLight.range = 5.5f;
            posterLight.intensity = 4f;
            posterLight.color = new Color(1f, 0.99f, 0.96f);
            posterLight.shadows = LightShadows.None;

            // spiral duct column (rings) beside the poster and the short duct from the hood to the ceiling
            var ductMat = Mat("Sc06FacDuct", new Color(0.66f, 0.68f, 0.70f), 0.55f, 0.55f);
            var ductRing = Mat("Sc06FacDuctRing", new Color(0.48f, 0.50f, 0.52f), 0.45f, 0.5f);
            Cyl("SpiralDuct", room, new Vector3(ox + 0.55f, 0f, zBack + 0.45f), h, 0.27f, ductMat);
            for (var k = 0; k < 24; k++)
                Cyl("SpiralRing" + k, room, new Vector3(ox + 0.55f, 0.1f + k * 0.13f, zBack + 0.45f), 0.025f, 0.285f, ductRing);
            Cyl("HoodDuct", room, new Vector3(hoodCentreX, 2.62f, interiorBack + 0.4f), h - 2.62f, 0.26f, ductMat);
            Cyl("HoodDuctCollar", room, new Vector3(hoodCentreX, 2.62f, interiorBack + 0.4f), 0.07f, 0.29f, ductRing);

            // ---- right of the hood: the exhaust fan, the FUME HOOD AREA sign, the two switch boxes
            var fanX = ox - 3.35f;
            const float fanY = 2.45f;
            Box("FanFrame", room, new Vector3(fanX, fanY, zBack + 0.07f), new Vector3(1.3f, 1.3f, 0.14f), darkSteel, false);
            var fanGlowMat = Mat("Sc06FanGlow", new Color(0.36f, 0.40f, 0.44f), 0f, 0f, null, null, true);
            QuadObj("FanBackdrop", room, new Vector3(fanX, fanY, zBack + 0.145f), new Vector3(0f, 180f, 0f), new Vector2(1.14f, 1.14f), fanGlowMat);
            var rotor = new GameObject("FanRotor").transform;
            rotor.SetParent(room, false);
            rotor.position = new Vector3(fanX, fanY, zBack + 0.165f);
            var bladeMat = Mat("Sc06FanBlade", new Color(0.46f, 0.47f, 0.48f), 0.4f, 0.6f);
            for (var b = 0; b < 6; b++)
            {
                var blade = Box("FanBlade" + b, rotor, rotor.position + new Vector3(0f, 0.25f, 0f), new Vector3(0.17f, 0.46f, 0.012f), bladeMat, false);
                blade.transform.RotateAround(rotor.position, Vector3.forward, b * 60f);
                blade.transform.Rotate(0f, 22f, 0f, Space.Self); // pitched like a real blade
            }

            Cyl("FanHub", rotor, rotor.position - new Vector3(0f, 0f, 0.02f), 0.05f, 0.07f, darkSteel).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (var g = 0; g <= 6; g++)
            {
                Box("FanGuardH" + g, room, new Vector3(fanX, fanY - 0.54f + g * 0.18f, zBack + 0.205f), new Vector3(1.12f, 0.012f, 0.012f), steel, false);
                Box("FanGuardV" + g, room, new Vector3(fanX - 0.54f + g * 0.18f, fanY, zBack + 0.205f), new Vector3(0.012f, 1.12f, 0.012f), steel, false);
            }

            var fanLightGo = new GameObject("FanGlowLight");
            fanLightGo.transform.SetParent(room, false);
            fanLightGo.transform.position = new Vector3(fanX, fanY, zBack + 0.6f);
            var fanLight = fanLightGo.AddComponent<Light>();
            fanLight.type = LightType.Point;
            fanLight.color = new Color(0.85f, 0.92f, 1f);
            fanLight.range = 4.5f;
            fanLight.intensity = 0.2f;
            fanLight.shadows = LightShadows.None;
            var fanHost = new GameObject("Sc06ExhaustFan");
            fanHost.transform.SetParent(room, false);
            refs.fan = fanHost.AddComponent<Sc06ExhaustFan>();
            var fanSo = new SerializedObject(refs.fan);
            fanSo.FindProperty("lab").objectReferenceValue = refs.lab;
            fanSo.FindProperty("rotor").objectReferenceValue = rotor;
            fanSo.FindProperty("axis").vector3Value = Vector3.forward;
            fanSo.FindProperty("glow").objectReferenceValue = fanLight;
            fanSo.ApplyModifiedPropertiesWithoutUndo();
            // the lab's "LEV" air-flow draws toward the fan
            var nozzle = Sc06Find(room, "LevNozzle");
            if (nozzle != null)
                nozzle.position = new Vector3(fanX, fanY - 0.2f, zBack + 0.5f);

            var signTex = Sc06FacTex("Sign_FumeHoodArea");
            if (signTex != null)
                QuadObj("SignFumeHoodArea", room, new Vector3(fanX, 1.5f, zBack + 0.016f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 0.45f),
                    Mat("Sc06SignFumeHoodArea", Color.white, 0.3f, 0f, signTex));

            // two switch boxes: the lab's rocker switches and lamps (switch 0 = FUME HOOD, 1 = EXHAUST FAN) move onto them, switch 2 is hidden
            var boxXs = new[] { ox - 2.85f, ox - 3.85f };
            var plates = new[] { "Plate_FumeHood", "Plate_ExhaustFan" };
            for (var i = 0; i < 2; i++)
            {
                var bx = boxXs[i];
                Box("SwitchBox" + i, room, new Vector3(bx, 0.98f, zBack + 0.05f), new Vector3(0.40f, 0.52f, 0.10f), steel, false);
                Box("SwitchBoxRim" + i, room, new Vector3(bx, 0.98f, zBack + 0.102f), new Vector3(0.42f, 0.54f, 0.012f), darkSteel, false);
                var pt = Sc06FacTex(plates[i]);
                if (pt != null)
                    QuadObj("SwitchPlate" + i, room, new Vector3(bx, 1.14f, zBack + 0.112f), new Vector3(0f, 180f, 0f), new Vector2(0.34f, 0.133f), Mat("Sc06" + plates[i], Color.white, 0.25f, 0f, pt));
                var rocker = Sc06Find(room, "Rocker" + i);
                if (rocker != null)
                {
                    var target = new Vector3(bx - 0.07f, 0.9f, zBack + 0.135f);
                    var delta = target - rocker.position;
                    foreach (var nm in new[] { "Rocker" + i, "RockerPlate" + i, "SwitchPoke" + i })
                        Sc06Shift(room, nm, delta);
                }

                var led = Sc06Find(room, "Led" + i);
                if (led != null)
                {
                    var target = new Vector3(bx + 0.09f, 0.9f, zBack + 0.13f);
                    var delta = target - led.position;
                    foreach (var nm in new[] { "Led" + i, "LedBezel" + i, "LedGlow" + i })
                        Sc06Shift(room, nm, delta);
                }
            }

            foreach (var nm in new[] { "Rocker2", "RockerPlate2", "SwitchPoke2", "Led2", "LedBezel2", "LedGlow2" })
            {
                var t = Sc06Find(room, nm);
                if (t != null)
                    t.gameObject.SetActive(false);
            }

            var blinkGo = new GameObject("Sc06SwitchBlink");
            blinkGo.transform.SetParent(room, false);
            refs.blink = blinkGo.AddComponent<Sc06SwitchBlink>();
            var bso = new SerializedObject(refs.blink);
            bso.FindProperty("lab").objectReferenceValue = refs.lab;
            var ledArr = bso.FindProperty("leds");
            var glowArr = bso.FindProperty("glows");
            for (var i = 0; i < 2; i++)
            {
                var led = Sc06Find(room, "Led" + i);
                var glow = Sc06Find(room, "LedGlow" + i);
                ledArr.GetArrayElementAtIndex(i).objectReferenceValue = led != null ? led.GetComponent<Renderer>() : null;
                glowArr.GetArrayElementAtIndex(i).objectReferenceValue = glow;
            }

            bso.ApplyModifiedPropertiesWithoutUndo();

            // ---- the quiet hood hum (the old default was louder)
            if (refs.ambience != null)
            {
                var aso = new SerializedObject(refs.ambience);
                aso.FindProperty("hoodLevel").floatValue = 0.12f;
                aso.FindProperty("roomLevel").floatValue = 0.16f;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- signs on the right wall, door and GHS box on the left side, safety board
            var cautionTex = Sc06FacTex("Sign_CautionToxic");
            if (cautionTex != null)
                QuadObj("SignCaution", room, new Vector3(ox - 4.85f, 1.65f, zBack + 0.016f), new Vector3(0f, 180f, 0f), new Vector2(1.2f, 0.56f), Mat("Sc06SignCaution", Color.white, 0.3f, 0f, cautionTex));
            var boardTex = Sc06FacTex("Sign_BlueBoard");
            if (boardTex != null)
                QuadObj("SignBoard", room, new Vector3(x1 - 0.016f, 1.62f, -2.55f), new Vector3(0f, 90f, 0f), new Vector2(0.46f, 0.72f), Mat("Sc06SignBoard", Color.white, 0.3f, 0f, boardTex));

            // sliding steel door with a mesh window and a rail (left wall)
            var doorMat = Mat("Sc06FacDoor", new Color(0.50f, 0.52f, 0.54f), 0.35f, 0.4f);
            var doorGlass = Mat("Sc06FacDoorMesh", new Color(0.36f, 0.4f, 0.42f), 0.2f, 0.3f);
            Box("DoorLeaf06", room, new Vector3(x1 - 0.05f, 1.05f, -1.5f), new Vector3(0.05f, 2.1f, 1.25f), doorMat, false);
            Box("DoorRail06", room, new Vector3(x1 - 0.07f, 2.18f, -1.35f), new Vector3(0.05f, 0.05f, 2.2f), darkSteel, false);
            Box("DoorWindow06", room, new Vector3(x1 - 0.078f, 1.55f, -1.5f), new Vector3(0.012f, 0.44f, 0.34f), doorGlass, false);
            Box("DoorHandle06", room, new Vector3(x1 - 0.085f, 1.0f, -1.95f), new Vector3(0.04f, 0.2f, 0.03f), darkSteel, false);

            // concrete pillar on the left wall near the camera (the picture): light-blue lower half, broken plaster with brick at its edge,
            // the grey GHS box with three lamps and a red button on its face, conduits running up the face
            const float gz = 0.2f;
            var pillarX = x1 - 0.36f;
            TexBox("LeftPillar", room, new Vector3(pillarX, h * 0.5f, gz), new Vector3(0.72f, h, 0.72f), 0f, wallMat, 2.4f, true);
            Box("LeftPillarBand", room, new Vector3(pillarX - 0.002f, 0.62f, gz), new Vector3(0.724f, 1.24f, 0.724f), bandMat, false);
            var gx = pillarX - 0.36f; // the pillar's face toward the room (-X)
            Box("GhsBox", room, new Vector3(gx - 0.1f, 1.55f, gz), new Vector3(0.2f, 0.6f, 0.42f), steel, false);
            var ghsTex = Sc06FacTex("Plate_Ghs");
            if (ghsTex != null)
                QuadObj("GhsPlate", room, new Vector3(gx - 0.202f, 1.78f, gz), new Vector3(0f, 90f, 0f), new Vector2(0.3f, 0.11f), Mat("Sc06GhsPlate", Color.white, 0.25f, 0f, ghsTex));
            var lampR = Mat("Sc06GhsRed", new Color(0.6f, 0.05f, 0.04f), 0.4f, 0f, null, new Color(2.6f, 0.2f, 0.12f), false);
            var lampY = Mat("Sc06GhsYellow", new Color(0.7f, 0.55f, 0.05f), 0.4f, 0f, null, new Color(2.4f, 1.8f, 0.2f), false);
            var lampG = Mat("Sc06GhsGreen", new Color(0.05f, 0.5f, 0.1f), 0.4f, 0f, null, new Color(0.2f, 2.4f, 0.4f), false);
            var lamps = new[] { lampG, lampY, lampR };
            for (var i = 0; i < 3; i++)
                Prim("GhsLamp" + i, PrimitiveType.Sphere, room, new Vector3(gx - 0.205f, 1.62f, gz - 0.14f + i * 0.14f), Vector3.one * 0.045f, lamps[i]);
            Prim("GhsButton", PrimitiveType.Sphere, room, new Vector3(gx - 0.205f, 1.4f, gz), Vector3.one * 0.07f, Mat("Sc06GhsButton", new Color(0.75f, 0.1f, 0.08f), 0.5f, 0f));
            Box("GhsSwitchBox", room, new Vector3(gx - 0.05f, 1.75f, gz - 0.5f), new Vector3(0.1f, 0.2f, 0.12f), steel, false);
            for (var c = 0; c < 3; c++)
                Cyl("Conduit" + c, room, new Vector3(gx - 0.03f - c * 0.045f, 1.0f, gz + 0.3f + c * 0.06f), h - 1.0f, 0.018f, pipeMat);
            Sc06HCyl("ConduitRunWall", room, new Vector3(x1 - 0.06f, 2.45f, -1.8f), 5.4f, 0.02f, pipeMat, false);
            Sc06HCyl("ConduitRunWall2", room, new Vector3(x1 - 0.06f, 2.3f, -1.8f), 5.4f, 0.018f, pipeMat, false);
            var brickTexL = Sc06FacTex("Wall_BrickPatch");
            if (brickTexL != null)
            {
                var brickL = Mat("Sc06BrickPatch", Color.white, 0.15f, 0f, brickTexL, null, false, true);
                // on the pillar's broad side facing the learner (not on the GHS face): one on the upper plaster, one low on the blue band
                var sideZ = gz + 0.36f + 0.004f;
                QuadObj("BrickPatchPillar", room, new Vector3(pillarX - 0.08f, 1.8f, sideZ), new Vector3(0f, 180f, 0f), new Vector2(0.62f, 0.8f), brickL);
                QuadObj("BrickPatchPillar2", room, new Vector3(pillarX + 0.06f, 0.5f, sideZ), new Vector3(0f, 180f, 0f), new Vector2(0.5f, 0.62f), brickL);
            }

            // ---- the steel table: two tops, steel legs, a lower shelf with a black crate and a blue tub (as in the picture)
            const float T = 0.92f;
            var tableX = ox - 0.1f;
            const float tableZ = 0.5f;
            var steelPbr = Sc06Pbr("Sc06FacSteelPbr", "Steel", new Color(0.66f, 0.68f, 0.70f), new Vector2(1f, 1f), 0.8f, 0.7f);
            var trim = Mat("Sc06FacSteelTrim", new Color(0.52f, 0.54f, 0.57f), 0.7f, 0.5f);
            Box("TableTopA", room, new Vector3(tableX + 0.62f, T - 0.025f, tableZ - 0.02f), new Vector3(1.28f, 0.05f, 0.9f), steelPbr);
            Box("TableTopB", room, new Vector3(tableX - 0.62f, T - 0.025f, tableZ + 0.02f), new Vector3(1.28f, 0.05f, 0.9f), steelPbr);
            foreach (var sx in new[] { -1.2f, 0f, 1.2f })
                foreach (var sz in new[] { -0.4f, 0.4f })
                    Box("TableLeg", room, new Vector3(tableX + sx, (T - 0.05f) * 0.5f, tableZ + sz), new Vector3(0.05f, T - 0.05f, 0.05f), trim, false);
            Box("TableShelf", room, new Vector3(tableX, 0.28f, tableZ), new Vector3(2.5f, 0.03f, 0.8f), trim, false);
            Box("TableRailF", room, new Vector3(tableX, T - 0.07f, tableZ + 0.44f), new Vector3(2.55f, 0.06f, 0.02f), trim, false);
            Box("CrateUnder", room, new Vector3(tableX + 0.55f, 0.46f, tableZ), new Vector3(0.55f, 0.29f, 0.4f), Mat("Sc06FacCrate", new Color(0.1f, 0.1f, 0.11f), 0.3f, 0f), false);
            Cyl("TubUnder", room, new Vector3(tableX - 0.55f, 0.295f, tableZ), 0.25f, 0.2f, Mat("Sc06FacTub", new Color(0.12f, 0.28f, 0.6f), 0.4f, 0f));

            // ---- the cardboard box, the three containers on the table (Jar1.glb parts) and the acid can
            Sc06BuildTableItems(room, refs, art, tableX, tableZ, T, hoodCentreX, surfaceY, sashZ, interiorBack);

            // the table top: scratches, oil smears and water marks so it does not look brand new
            var scuffMat = MatAlpha("Sc06TableScuff", EnvTex("STAIN_Oil"), new Color(0.05f, 0.05f, 0.05f, 0.30f), false);
            var rsT = new System.Random(88);
            for (var i = 0; i < 26; i++)
            {
                var sx = tableX + (float)(rsT.NextDouble() * 2.3 - 1.15);
                var sz = tableZ + (float)(rsT.NextDouble() * 0.7 - 0.35);
                QuadObj("TableScuff", room, new Vector3(sx, T + 0.0012f + i * 0.00003f, sz), new Vector3(90f, (float)rsT.NextDouble() * 360f, 0f),
                    Vector2.one * (0.12f + (float)rsT.NextDouble() * 0.32f), scuffMat);
            }

            // the table is turned toward the viewer (the picture shows it at an angle); containers and labels turn with it
            var tableRoot = new GameObject("TableRoot").transform;
            tableRoot.SetParent(room, false);
            tableRoot.position = new Vector3(tableX, 0f, tableZ);
            var tableNames = new HashSet<string>
            {
                "TableTopA", "TableTopB", "TableLeg", "TableShelf", "TableRailF", "CrateUnder", "TubUnder", "CardboardBox", "MixtureJar", "WaterJug",
                "AcidCan", "ShadowItem", "TableScuff"
            };
            var tableKids = new List<Transform>();
            foreach (Transform kid in room)
                if (tableNames.Contains(kid.name))
                    tableKids.Add(kid);
            foreach (var kid in tableKids)
                kid.SetParent(tableRoot, true);
            tableRoot.rotation = Quaternion.identity; // straight (the user asked for it)

            // ---- the sewing machine bench on the back wall at the far right (Sewing Machine.glb)
            Sc06BuildSewingRow(room, ox - 5.3f, steel, darkSteel);

            // ---- where the learner stands: the whole room in view (table, hood, fan, door, floor and ceiling); no locomotion
            if (refs.spawn != null)
                refs.spawn.SetPositionAndRotation(new Vector3(ox + 1.2f, 0f, 3.0f), Quaternion.Euler(0f, 187f, 0f));
            if (refs.tableStand != null)
                refs.tableStand.SetPositionAndRotation(new Vector3(ox + 1.2f, 0f, 3.0f), Quaternion.Euler(0f, 187f, 0f));
        }

        // Parts of Jar1.glb re-parented under their own base-centred root (so a container can be carried as one object).
        // The root is created at the bottom centre of the parts, the parts go under it, THEN it is turned and scaled about that point
        // and finally moved to floorPos (turning / scaling before re-parenting would be undone by the parent compensation).
        private static Transform Sc06Pack(GameObject model, string[] nodeNames, string rootName, Transform parent, Vector3 floorPos, float scale, float yaw)
        {
            var nodes = new List<Transform>();
            foreach (var n in nodeNames)
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == n)
                    {
                        nodes.Add(t);
                        break;
                    }

            if (nodes.Count == 0)
                return null;
            var bb = new Bounds();
            var first = true;
            foreach (var n in nodes)
                foreach (var r in n.GetComponentsInChildren<Renderer>(false))
                {
                    if (first)
                    {
                        bb = r.bounds;
                        first = false;
                    }
                    else
                    {
                        bb.Encapsulate(r.bounds);
                    }
                }

            var root = new GameObject(rootName).transform;
            root.SetParent(parent, true);
            root.position = new Vector3(bb.center.x, bb.min.y, bb.center.z);
            foreach (var n in nodes)
                n.SetParent(root, true);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            root.position = floorPos;
            return root;
        }

        private static Bounds Sc06Bounds(Transform root)
        {
            var b = new Bounds(root.position, Vector3.zero);
            var first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (first)
                {
                    b = r.bounds;
                    first = false;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        // Vertices of the body meshes (not caps, labels or my rim / base discs) in a horizontal band: front (max z) and radius from the axis.
        private static bool Sc06BodyVertex(MeshFilter mf)
        {
            var n = mf.name;
            return mf.sharedMesh != null && !n.StartsWith("_gltfNode") && !n.Contains("Label") && !n.StartsWith("Jar") && !n.StartsWith("JugWater");
        }

        private static float Sc06FrontZ(Transform root, float y, float halfBand, float cx, float halfW, float fallback)
        {
            var best = float.MinValue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!Sc06BodyVertex(mf))
                    continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    if (Mathf.Abs(w.y - y) < halfBand && Mathf.Abs(w.x - cx) < halfW && w.z > best)
                        best = w.z;
                }
            }

            return best > float.MinValue ? best : fallback;
        }

        private static float Sc06BandRadius(Transform root, float y, float halfBand, float cx, float cz)
        {
            var best = 0f;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!Sc06BodyVertex(mf))
                    continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    if (Mathf.Abs(w.y - y) < halfBand)
                        best = Mathf.Max(best, new Vector2(w.x - cx, w.z - cz).magnitude);
                }
            }

            return best;
        }

        // A label wrapped around a cylinder (front at +Z, text reads left to right as seen from +Z).
        private static Mesh Sc06CurvedLabel(float radius, float height, float arcDeg, int seg = 18)
        {
            var mesh = new Mesh { name = "Sc06CurvedLabel" };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            var half = arcDeg * 0.5f * Mathf.Deg2Rad;
            for (var i = 0; i <= seg; i++)
            {
                var k = i / (float)seg;
                var a = Mathf.Lerp(-half, half, k);
                var x = Mathf.Sin(a) * radius;
                var z = Mathf.Cos(a) * radius;
                var nn = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Add(new Vector3(x, -height * 0.5f, z));
                v.Add(new Vector3(x, height * 0.5f, z));
                n.Add(nn);
                n.Add(nn);
                uv.Add(new Vector2(1f - k, 0f));
                uv.Add(new Vector2(1f - k, 1f));
                if (i < seg)
                {
                    var b = i * 2;
                    t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                }
            }

            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Vertices of the body in a band: the BACK (min z), like Sc06FrontZ for the other face.
        private static float Sc06BackZ(Transform root, float y, float halfBand, float cx, float halfW, float fallback)
        {
            var best = float.MaxValue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!Sc06BodyVertex(mf))
                    continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    if (Mathf.Abs(w.y - y) < halfBand && Mathf.Abs(w.x - cx) < halfW && w.z < best)
                        best = w.z;
                }
            }

            return best < float.MaxValue ? best : fallback;
        }

        // Label paper that shows on ONE side only (no mirrored text seen through the plastic from behind).
        private static Material Sc06LabelPaperOneSided(string name, Texture2D tex)
        {
            var m = Mat(name, Color.white, 0.25f, 0f, tex);
            m.SetFloat("_Cull", 2f);
            m.renderQueue = 3100;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Label paper: opaque, both sides, drawn after the see-through plastic so the plastic cannot wash the text out.
        private static Material Sc06LabelPaper(string name, Texture2D tex)
        {
            var m = Mat(name, Color.white, 0.25f, 0f, tex);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = 3100;
            EditorUtility.SetDirty(m);
            return m;
        }

        // The empty MIXTURE container of the picture: a plain straight, slightly flared clear plastic cup (no neck, no lid), a thin hollow shell.
        private static Transform Sc06BuildCup(Transform parent, Vector3 floorPos)
        {
            var root = new GameObject("MixtureJar").transform;
            root.SetParent(parent, true);
            root.position = floorPos;
            // rounded foot, gentle flare, a thick moulded wall
            var prof = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.062f, 0f), new Vector2(0.079f, 0.004f), new Vector2(0.088f, 0.014f), new Vector2(0.093f, 0.04f),
                new Vector2(0.096f, 0.15f), new Vector2(0.100f, 0.27f), new Vector2(0.103f, 0.292f), new Vector2(0.1035f, 0.300f)
            };
            var mesh = Sc06Lathe("Sc06MixCupReal", prof, 0.0034f, 48, true);
            var body = new GameObject("CupBody");
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>();

            List<Vector3> Ring(float radius, float y)
            {
                var pts = new List<Vector3>();
                for (var k = 0; k <= 48; k++)
                {
                    var a = k / 48f * Mathf.PI * 2f;
                    pts.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }

                return pts;
            }

            // the rolled rim and three moulded ribs (they catch the light like real PET)
            Sc08Part("CupRim", root, Sc08SweepTube("Sc08CupRim", Ring(0.1045f, 0.2985f), 0.0038f, 8), null, Vector3.zero);
            var ribY = new[] { 0.070f, 0.095f, 0.120f };
            var ribR = new[] { 0.0944f, 0.0952f, 0.0959f };
            for (var k = 0; k < 3; k++)
                Sc08Part("CupRib" + k, root, Sc08SweepTube("Sc08CupRib" + k, Ring(ribR[k], ribY[k]), 0.0016f, 6), null, Vector3.zero);
            return root;
        }

        private static Transform Sc06FindUnder(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void Sc06BuildTableItems(Transform room, Sc06Refs refs, PresentableArt art, float tableX, float tableZ, float T,
            float hoodCentreX, float surfaceY, float sashZ, float interiorBack)
        {
            var model = SpawnSc06("Jar1.glb", room);
            var hoodMidZ = (interiorBack + sashZ) * 0.5f;
            var jarSlot = Marker("JarSlot", room, new Vector3(hoodCentreX, surfaceY + 0.002f, hoodMidZ), 0f);
            var waterSlot = Marker("WaterSlot", room, new Vector3(hoodCentreX + 0.52f, surfaceY + 0.002f, hoodMidZ + 0.02f), 0f);
            var acidSlot = Marker("AcidSlot", room, new Vector3(hoodCentreX - 0.52f, surfaceY + 0.002f, hoodMidZ + 0.02f), 0f);

            // the client's own four items (Jar1.glb): open cardboard box, the empty MIXTURE jar (clear ribbed jar), the acid can (dirty
            // plastic, blue cap) and the WATER jug (clear, blue cap). Left to right on the table, like the picture. Their labels face the
            // viewer after a 90 degree turn (the glb has them toward -X). Scale 1.5 so they read clearly from the learner's spot.
            const float scale = 1.8f;
            var boxPos = new Vector3(tableX + 0.92f, T, tableZ + 0.0f);
            var mixPos = new Vector3(tableX + 0.38f, T, tableZ - 0.08f);
            var acidPos = new Vector3(tableX - 0.12f, T, tableZ - 0.1f);
            var waterPos = new Vector3(tableX - 0.62f, T, tableZ - 0.08f);
            Transform box = null, jar = null, acid = null, water = null;
            if (model != null)
            {
                box = Sc06Pack(model, new[] { "Cardboard Box_MainMat_0" }, "CardboardBox", room, boxPos, 1.35f, 18f);
                jar = Sc06BuildCup(room, mixPos); // the picture's plain clear plastic cup (the glb jar has a neck and ribs)
                acid = Sc06Pack(model, new[] { "model_diffuse-material" }, "AcidCan", room, acidPos, scale, 90f);
                water = Sc06Pack(model, new[] { "model_diffuse-material.002" }, "WaterJug", room, waterPos, scale, 90f);
            }

            // fallbacks if a part is missing (keeps the scene buildable)
            var glass = Sc06GlassMat("Sc06FacGlass", new Color(0.84f, 0.93f, 0.96f), 0.07f, 0.8f);
            Transform Fallback(string name, Vector3 pos, float height, float radius)
            {
                var fb = new GameObject(name).transform;
                fb.SetParent(room, true);
                fb.position = pos;
                Cyl(name + "Body", fb, pos, height, radius, glass).transform.SetParent(fb, true);
                return fb;
            }

            if (jar == null)
                jar = Fallback("MixtureJar", mixPos, 0.2f, 0.09f);
            if (acid == null)
                acid = Fallback("AcidCan", acidPos, 0.42f, 0.1f);
            if (water == null)
                water = Fallback("WaterJug", waterPos, 0.42f, 0.1f);

            // the jar's glass: lit glass with bright edges (the glb's own shader reads as a flat white blob)
            var jarGlassLit = Sc06GlassMat("Sc06FacJarGlass", new Color(0.88f, 0.94f, 0.97f), 0.12f, 0.9f); // clear PET: fresnel edges, reflections
            foreach (var r in jar.GetComponentsInChildren<Renderer>(true))
            {
                var sm = r.sharedMaterials;
                if (sm == null || sm.Length == 0)
                    sm = new Material[1];
                for (var k = 0; k < sm.Length; k++)
                    if (sm[k] == null || !sm[k].name.Contains("Lable"))
                        sm[k] = jarGlassLit; // (a procedural cup starts with no material at all: that was the magenta)
                r.sharedMaterials = sm;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // the client's label on the jar (MIXTURE / mixture of acid), on the face toward the viewer; the baked label plate is hidden
            var plate = Sc06FindUnder(jar, "defaultMaterial.001");
            if (plate != null)
                plate.gameObject.SetActive(false);
            var jb = Sc06Bounds(jar);
            var jarH = jb.size.y;
            var jarR = Mathf.Max(0.05f, Mathf.Min(jb.extents.x, jb.extents.z));
            var labelTex = Sc06FacTex("Label_MixtureJar");
            if (labelTex != null)
            {
                var bandY = jb.min.y + jarH * 0.27f; // low on the jar (like the picture): the liquid and the rod stay visible above the label
                var radius = Sc06BandRadius(jar, bandY, jarH * 0.15f, jar.position.x, jar.position.z);
                radius = radius > 0.02f ? radius : jarR;
                var lblGo = new GameObject("MixtureLabel");
                lblGo.transform.SetParent(jar, true);
                lblGo.transform.position = new Vector3(jar.position.x, bandY, jar.position.z);
                lblGo.AddComponent<MeshFilter>().sharedMesh = Sc06CurvedLabel(radius + 0.003f, jarH * 0.38f, 120f);
                var lmr = lblGo.AddComponent<MeshRenderer>();
                lmr.sharedMaterial = Sc06LabelPaper("Sc06LabelMixture", labelTex);
                lmr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // the acid can's label (orange band, CONC. H2SO4, GHS diamonds) on its broad face toward the viewer
            var ab = Sc06Bounds(acid);
            var aLabelTex = Sc06FacTex("Label_AcidCan");
            if (aLabelTex != null)
            {
                var lw = Mathf.Min(0.34f, ab.size.x * 0.86f);
                var ly = ab.min.y + ab.size.y * 0.42f;
                var fz = Sc06FrontZ(acid, ly, lw * 0.5f, ab.center.x, lw * 0.5f, ab.max.z);
                var lq = QuadObj("AcidLabel", room, new Vector3(ab.center.x, ly, fz + 0.0022f), new Vector3(0f, 180f, 0f),
                    new Vector2(lw, lw * 0.75f), Sc06LabelPaper("Sc06LabelAcid", aLabelTex));
                lq.transform.SetParent(acid, true);
                Object.DestroyImmediate(lq.GetComponent<Collider>());
            }

            // the glb's shader makes the acid can look like dark brass under room light: matte, light plastic like the picture
            foreach (var r in acid.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("_gltfNode"))
                    continue;
                var sm = r.sharedMaterials;
                for (var k = 0; k < sm.Length; k++)
                {
                    if (sm[k] == null)
                        continue;
                    var c = new Material(sm[k]);
                    if (c.HasProperty("metallicFactor"))
                        c.SetFloat("metallicFactor", 0f);
                    if (c.HasProperty("roughnessFactor"))
                        c.SetFloat("roughnessFactor", 0.55f);
                    if (c.HasProperty("baseColorFactor"))
                        c.SetColor("baseColorFactor", new Color(1.5f, 1.45f, 1.3f, 1f));
                    sm[k] = c;
                }

                r.sharedMaterials = sm;
            }

            // a soft light over the table so the four items read crystal clear
            var tl = new GameObject("TableSpot");
            tl.transform.SetParent(room, false);
            tl.transform.position = new Vector3(tableX + 0.3f, 2.4f, tableZ + 0.7f);
            tl.transform.rotation = Quaternion.LookRotation(new Vector3(tableX, T, tableZ) - tl.transform.position, Vector3.up);
            var tspot = tl.AddComponent<Light>();
            tspot.type = LightType.Spot;
            tspot.spotAngle = 70f;
            tspot.range = 5f;
            tspot.intensity = 14f;
            tspot.color = new Color(1f, 0.98f, 0.94f);
            tspot.shadows = LightShadows.None;

            // the jug: clear glass and WATER-coloured water (the glb's glass shader makes both dark)
            var jugClear = Mat("Sc06FacJugPlastic", new Color(0.80f, 0.88f, 0.96f, 0.5f), 0.62f, 0f);
            MakeTransparent(jugClear);
            var waterBlue = Mat("Sc06FacWaterPlasticLit", new Color(0.30f, 0.62f, 0.98f, 0.82f), 0.5f, 0f);
            MakeTransparent(waterBlue);
            foreach (var r in water.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("_gltfNode"))
                    continue;
                var sm = r.sharedMaterials;
                for (var k = 0; k < sm.Length; k++)
                {
                    var n = sm[k] != null ? sm[k].name : "";
                    if (n.Contains("Dark Glass"))
                        sm[k] = waterBlue;
                    else if (n.Contains("Glass Clear"))
                        sm[k] = jugClear;
                }

                r.sharedMaterials = sm;
            }

            // the water jug's own big label (the baked one is small)
            var wLabelTex = Sc06FacTex("Label_WaterJug");
            if (wLabelTex != null)
            {
                var wbb = Sc06Bounds(water);
                var wl = Mathf.Min(0.32f, wbb.size.x * 0.84f);
                var wy = wbb.min.y + wbb.size.y * 0.42f;
                var wz = Sc06FrontZ(water, wy, wl * 0.5f, wbb.center.x, wl * 0.5f, wbb.max.z);
                var wq = QuadObj("WaterLabel", room, new Vector3(wbb.center.x, wy, wz + 0.0022f), new Vector3(0f, 180f, 0f),
                    new Vector2(wl, wl * 0.62f), Sc06LabelPaperOneSided("Sc06LabelWaterOne", wLabelTex));
                wq.transform.SetParent(water, true);
                Object.DestroyImmediate(wq.GetComponent<Collider>());
                // the same label on the back face: when the jug turns its neck toward the jar to pour, this one faces the learner
                var wzb = Sc06BackZ(water, wy, wl * 0.5f, wbb.center.x, wl * 0.5f, wbb.min.z);
                var wqb = QuadObj("WaterLabelBack", room, new Vector3(wbb.center.x, wy, wzb - 0.0022f), Vector3.zero,
                    new Vector2(wl, wl * 0.62f), Sc06LabelPaperOneSided("Sc06LabelWaterOne", wLabelTex));
                wqb.transform.SetParent(water, true);
                Object.DestroyImmediate(wqb.GetComponent<Collider>());
            }

            // blue caps (come off during the pour) and the spouts (the cap end of each can)
            var acidCap = Sc06FindUnder(acid, "_gltfNode_6.002");
            var waterCap = Sc06FindUnder(water, "_gltfNode_6.001");
            Transform SpoutAt(string name, Transform cap, Transform owner, Bounds ob)
            {
                var sp = new GameObject(name).transform;
                sp.SetParent(owner, true);
                var cr = cap != null ? cap.GetComponentInChildren<Renderer>(true) : null;
                sp.position = cr != null ? new Vector3(cr.bounds.center.x, cr.bounds.center.y, cr.bounds.center.z) : new Vector3(ob.center.x, ob.max.y, ob.center.z);
                return sp;
            }

            var wb = Sc06Bounds(water);
            var acidSpout = SpoutAt("AcidSpout", acidCap, acid, ab);
            var waterSpout = SpoutAt("WaterSpout", waterCap, water, wb);

            // liquid pivot at the jar floor (scaled in Y by the pour), the liquid cylinder hangs from it
            var liquidPivot = new GameObject("JarLiquidPivot").transform;
            liquidPivot.SetParent(jar, true);
            liquidPivot.position = new Vector3(jar.position.x, jb.min.y + 0.012f, jar.position.z);
            var liquidMat = MatAlpha("Sc06FacLiquid", null, new Color(0.93f, 0.92f, 0.76f, 0.55f), false);
            var fullH = jarH * 0.78f;
            var liquidCyl = Cyl("JarLiquid06", jar, liquidPivot.position, fullH, jarR * 0.8f, liquidMat);
            liquidCyl.transform.SetParent(liquidPivot, true);
            // the surface of the liquid: a thin bright disc at the top of the liquid cylinder (it rides up while the jar fills and
            // shows from above through the open mouth and the glass)
            var liquidTopMat = MatAlpha("Sc06FacLiquidTop", null, new Color(0.96f, 0.98f, 0.92f, 0.8f), false);
            var liquidTop = Cyl("JarLiquidTop", jar, liquidPivot.position + Vector3.up * (fullH - 0.003f), 0.004f, jarR * 0.805f, liquidTopMat);
            liquidTop.transform.SetParent(liquidPivot, true);
            var mouth = new GameObject("JarMouth").transform;
            mouth.SetParent(jar, true);
            mouth.position = new Vector3(jar.position.x, jb.max.y, jar.position.z);

            // contact shadows so nothing floats on the steel
            var shadowMat = ShadowMat(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png"));
            foreach (var s in new[] { (mixPos, 0.2f), (waterPos, 0.34f), (acidPos, 0.3f) })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "ShadowItem";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(room, true);
                q.transform.position = s.Item1 + Vector3.up * 0.0015f;
                q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(s.Item2, s.Item2, 1f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = shadowMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // stream, splash ripple and acid fumes
            var streamMat = MatAlpha("Sc06FacStream", null, new Color(0.95f, 0.93f, 0.78f, 0.8f), false);
            var stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stream.name = "PourStream";
            Object.DestroyImmediate(stream.GetComponent<Collider>());
            stream.transform.SetParent(room, true);
            stream.GetComponent<MeshRenderer>().sharedMaterial = streamMat;
            stream.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            stream.SetActive(false);
            var splash = QuadObj("PourSplash", room, mouth.position, new Vector3(90f, 0f, 0f), Vector2.one * 0.14f, MatAlpha("Sc06FacSplash", Sc06Tex("Fx_Ripple"), new Color(0.9f, 0.97f, 1f, 0.8f), true));
            Object.DestroyImmediate(splash.GetComponent<Collider>());
            splash.SetActive(false);

            var fumesGo = new GameObject("AcidFumes");
            fumesGo.transform.SetParent(room, false);
            fumesGo.transform.position = mouth.position + Vector3.up * 0.05f;
            var ps = fumesGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 2.2f;
            main.startSpeed = 0.18f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(0.95f, 0.92f, 0.8f, 0.22f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 14f;
            em.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, -0.1f); // drawn toward the back of the hood
            var pr = fumesGo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = ParticleMat("Sc06FacFumes", Sc06Tex("Fx_Wisp"), false);
            pr.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();

            // the demonstration component
            var autoGo = new GameObject("Sc06Auto");
            autoGo.transform.SetParent(room, false);
            refs.auto = autoGo.AddComponent<Sc06AutoPour>();
            var so = new SerializedObject(refs.auto);
            so.FindProperty("jar").objectReferenceValue = jar;
            so.FindProperty("water").objectReferenceValue = water;
            so.FindProperty("acid").objectReferenceValue = acid;
            so.FindProperty("jarMouth").objectReferenceValue = mouth;
            so.FindProperty("waterSpout").objectReferenceValue = waterSpout;
            so.FindProperty("acidSpout").objectReferenceValue = acidSpout;
            so.FindProperty("jarSlot").objectReferenceValue = jarSlot;
            so.FindProperty("waterSlot").objectReferenceValue = waterSlot;
            so.FindProperty("acidSlot").objectReferenceValue = acidSlot;
            so.FindProperty("liquid").objectReferenceValue = liquidPivot;
            so.FindProperty("liquidFullHeight").floatValue = fullH;
            so.FindProperty("liquidRenderer").objectReferenceValue = liquidCyl.GetComponent<Renderer>();
            so.FindProperty("stream").objectReferenceValue = stream.transform;
            so.FindProperty("streamRenderer").objectReferenceValue = stream.GetComponent<Renderer>();
            so.FindProperty("splash").objectReferenceValue = splash.transform;
            so.FindProperty("fumes").objectReferenceValue = ps;
            so.FindProperty("ambience").objectReferenceValue = refs.ambience;
            so.FindProperty("acidCap").objectReferenceValue = acidCap != null ? acidCap.gameObject : null;
            so.FindProperty("waterCap").objectReferenceValue = waterCap != null ? waterCap.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (model != null)
                Object.DestroyImmediate(model);
        }

        // A row of sewing machine benches along the right wall (the picture shows the bench at the right; the client asked for a row).
        private static void Sc06BuildSewingRow(Transform room, float x0, Material steel, Material darkSteel)
        {
            var zs = new[] { 2.4f, 1.6f, 0.8f, 0.0f, -0.8f, -1.6f, -2.35f };
            for (var i = 0; i < zs.Length; i++)
            {
                var model = SpawnSc06("SewingMachine.glb", room);
                if (model == null)
                    return;
                model.name = "SewingMachineRow" + i;
                var b = BoundsOfTree(model.transform);
                if (b.size.y > 0.01f)
                    model.transform.localScale *= 1.2f / b.size.y;
                model.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.rotation;
                b = BoundsOfTree(model.transform);
                model.transform.position += new Vector3(x0 - b.center.x, -b.min.y, zs[i] - b.center.z);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == "dikis_makinesi_ignesi")
                    {
                        var bob = t.gameObject.AddComponent<Sc06Bobber>();
                        var bso = new SerializedObject(bob);
                        bso.FindProperty("amplitude").floatValue = 0.012f;
                        bso.FindProperty("hertz").floatValue = 3f + i * 0.3f;
                        bso.ApplyModifiedPropertiesWithoutUndo();
                        break;
                    }
            }
        }

        // A sewing machine bench (Sewing Machine.glb): the machine on a table with a stool, the needle bobbing.
        private static void Sc06BuildSewing(Transform room, float x, float zBack, Material steel, Material darkSteel)
        {
            var model = SpawnSc06("SewingMachine.glb", room);
            if (model == null)
                return;
            model.name = "SewingMachine";
            var b = BoundsOfTree(model.transform);
            if (b.size.y > 0.01f)
                model.transform.localScale *= 1.25f / b.size.y;
            b = BoundsOfTree(model.transform);
            model.transform.position += new Vector3(x - b.center.x, -b.min.y, zBack + 0.2f + b.extents.z - b.center.z);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
            // a second bench with a lower shelf beside it (the picture)
            var wood = Mat("Sc06BenchWood", new Color(0.74f, 0.62f, 0.44f), 0.25f, 0f);
            var benchZ = zBack + 1.55f;
            Box("BenchTop2", room, new Vector3(x + 0.05f, 0.8f, benchZ), new Vector3(0.62f, 0.04f, 1.1f), wood, false);
            Box("BenchShelf2", room, new Vector3(x + 0.05f, 0.3f, benchZ), new Vector3(0.58f, 0.025f, 1.04f), steel, false);
            foreach (var lx in new[] { -0.26f, 0.26f })
                foreach (var lz in new[] { -0.5f, 0.5f })
                    Box("BenchLeg2", room, new Vector3(x + 0.05f + lx, 0.4f, benchZ + lz), new Vector3(0.04f, 0.8f, 0.04f), steel, false);
            Box("BenchBox2", room, new Vector3(x + 0.08f, 0.9f, benchZ - 0.2f), new Vector3(0.32f, 0.2f, 0.26f), Mat("Sc06BenchCarton", new Color(0.62f, 0.5f, 0.34f), 0.2f, 0f), false);

            // the needle bar goes up and down
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "dikis_makinesi_ignesi")
                {
                    var bob = t.gameObject.AddComponent<Sc06Bobber>();
                    var bso = new SerializedObject(bob);
                    bso.FindProperty("amplitude").floatValue = 0.012f;
                    bso.FindProperty("hertz").floatValue = 3f;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
        }
    }
}
#endif

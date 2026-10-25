#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 4 lab, modelled on the Scene 04 workflow. Hidden until Scenario 3 finishes.
    /// Viewer stands at Spawn_SC04 facing -Z, so the viewer's left is +X.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc04Refs
        {
            public Transform spawn;
            public GloveInspect inspect;
            public Sc04BenchHeight bench;
            public Sc06Gate gate;
            public Transform gateStand;
        }

        private const float Sc04Ox = 120f;

        private static Sc04Refs BuildSc04Room(Transform env, PresentableArt art)
        {
            const float ox = Sc04Ox;
            var refs = new Sc04Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solid = SolidLayer;

            ConfigureSc04Textures();
            // PBR: glossy epoxy floor, 15 cm wall tiles with grout (one image = 0.6 m), T-bar ceiling panels.
            var floorMat = Sc04Pbr("Sc04FloorPbr2", "Epoxy", Color.white, new Vector2(3.5f, 3.6f), 1f, 0.6f);
            // 30 cm tiles like the designer's lab (one image = 1.2 m of 4x4 tiles)
            var wallMat = Sc04Pbr("Sc04WallPbrC", "Tile", Color.white, new Vector2(5.85f, 2.75f), 0.9f, 0.5f);
            var wallSide = Sc04Pbr("Sc04WallPbrD", "Tile", Color.white, new Vector2(5.7f, 2.75f), 0.9f, 0.5f);
            var ceilMat = Sc04Pbr("Sc04CeilPbr2", "Ceiling", Color.white, new Vector2(5.85f, 6f), 1f, 0.7f, false);
            // lit (not unlit) so the cloth shades and the gloves sit on it instead of floating over a flat white card
            var cloth = Mat("Sc04ClothLit", new Color(0.9f, 0.9f, 0.88f), 0.04f, 0f);
            var lampMat = Mat("Sc04Lamp", new Color(0.92f, 0.94f, 0.93f), 0f, 0f, null, new Color(0.35f, 0.38f, 0.36f), true);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solid >= 0)
                    go.layer = solid;
            }

            // A tighter room (7 m wide) so the benches, hood, fridge and eyewash fill the view like the designer's lab.
            var cx = ox + 0.47f;
            var floor = Box("Floor", room, new Vector3(cx, -0.05f, 0.2f), new Vector3(7f, 0.1f, 7.2f), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(cx, 1.6f, -3.2f), new Vector3(7f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Front", new Vector3(cx, 1.6f, 3.6f), new Vector3(7f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Left", new Vector3(ox + 3.94f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallSide);
            Shell("Wall_Right", new Vector3(ox - 3.0f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallSide);
            Shell("Ceiling", new Vector3(cx, 3.22f, 0.2f), new Vector3(7f, 0.08f, 7.2f), ceilMat);
            Box("LampA", room, new Vector3(ox + 2.9f, 3.14f, 0.45f), new Vector3(1.6f, 0.04f, 0.18f), lampMat, false);
            Box("LampB", room, new Vector3(ox + 2.9f, 3.14f, -1.05f), new Vector3(1.6f, 0.04f, 0.18f), lampMat, false);
            LabLight(room, new Vector3(ox + 0.4f, 2.9f, 0.9f), 1.15f);
            LabLight(room, new Vector3(ox - 0.8f, 2.9f, -1.1f), 0.9f);
            LabLight(room, new Vector3(ox + 2.4f, 2.7f, -0.2f), 0.65f);

            // Viewer's left (+X) is the return bench against the tiled wall. The near bench
            // meets it and holds the gloves. The player stands at +Z looking toward -Z.
            var side = SpawnSc04("table02.glb", room, new Vector3(ox + 3.15f, 0f, -0.2f), 0f, 2.35f);
            var sideBox = new Bounds(new Vector3(ox + 3.15f, 0.45f, -0.2f), new Vector3(0.8f, 0.9f, 2.2f));
            if (side != null)
            {
                SitOn(side, 0f);
                var sb = WorldBounds(side);
                side.transform.position += new Vector3((ox + 3.82f) - sb.max.x, 0f, 0f);
                sideBox = WorldBounds(side);
            }

            var gloveTable = SpawnSc04("table02.glb", room, new Vector3(ox + 1.15f, 0f, 1.2f), 90f, 2.4f);
            var gloveBox = sideBox;
            if (gloveTable != null)
            {
                SitOn(gloveTable, 0f);
                gloveBox = WorldBounds(gloveTable);
                // meets the return bench exactly at its near end: the two benches form an L
                gloveTable.transform.position += new Vector3(0f, 0f, sideBox.max.z - gloveBox.min.z);
                gloveBox = WorldBounds(gloveTable);
            }

            // The glb tables are thin sheets and 1.07 m high: keep only their measured footprint and build solid steel benches of
            // 0.9 m. The glove table is stretched along +X to the wall so it joins the return bench like in the lab photo. Both
            // benches (and what stands on the return bench) live under BenchRoot and move together with the head height
            // (Sc04BenchHeight, clamp 0.75-1.1 m): not too high when seated, not too low when standing.
            const float tableTop = 0.9f;
            var steelMat = Steel();
            if (side != null)
                Object.DestroyImmediate(side);
            if (gloveTable != null)
                Object.DestroyImmediate(gloveTable);
            var sideTop = tableTop;
            var gloveTop = tableTop;
            sideBox = new Bounds(new Vector3(sideBox.center.x, tableTop * 0.5f, sideBox.center.z),
                new Vector3(sideBox.size.x, tableTop, sideBox.size.z));
            gloveBox = new Bounds(new Vector3(gloveBox.center.x, tableTop * 0.5f, gloveBox.center.z),
                new Vector3(gloveBox.size.x, tableTop, gloveBox.size.z));
            var benchGo = new GameObject("BenchRoot");
            var bench = benchGo.transform;
            bench.SetParent(room, false);
            Sc04SolidTable(bench, sideBox, true, steelMat);
            var lMin = gloveBox.min.x;
            var lMax = sideBox.max.x;
            Sc04SolidTable(bench, new Bounds(new Vector3((lMin + lMax) * 0.5f, tableTop * 0.5f, gloveBox.center.z),
                new Vector3(lMax - lMin, tableTop, gloveBox.size.z)), false, steelMat);

            var clothAt = new Vector3(gloveBox.center.x - 0.05f, gloveTop + 0.008f, gloveBox.center.z);
            Box("Cloth", bench, clothAt, new Vector3(1.55f, 0.008f, 0.82f), cloth, false);

            var sideItemsFrom = room.childCount; // everything from here to the fume hood stands on the return bench
            // Near (+Z, toward the player) to far (-Z): water beaker, two corked flasks, then the tray.
            var benchX = sideBox.center.x;
            var zNear = sideBox.max.z - 0.22f;
            var beakerA = PlacePiece("chemistry_glassware.glb", "lab_beaker_a", room,
                new Vector3(benchX - 0.08f, sideTop, zNear), 0f, 0.12f, sideTop);
            var beakerB = PlacePiece("chemistry_glassware.glb", "lab_beaker_b", room,
                new Vector3(benchX + 0.12f, sideTop, zNear - 0.06f), 20f, 0.09f, sideTop);
            UseGlass(beakerA);
            UseGlass(beakerB);
            var flaskA = PlacePiece("chemistry_glassware.glb", "lab_erlenmeyer_a_0", room,
                new Vector3(benchX - 0.08f, sideTop, zNear - 0.28f), 18f, 0.2f, sideTop);
            var flaskB = PlacePiece("chemistry_glassware.glb", "lab_erlenmeyer_a_0.001", room,
                new Vector3(benchX + 0.1f, sideTop, zNear - 0.42f), -12f, 0.18f, sideTop);
            UseGlass(flaskA);
            UseGlass(flaskB);
            Cork(room, flaskA);
            Cork(room, flaskB);

            var trayAt = new Vector3(benchX, 0f, zNear - 0.72f);
            var tray = SpawnSc04("tray.glb", room, trayAt, 8f, 0.36f);
            var trayTop = sideTop + 0.04f;
            if (tray != null)
            {
                SitOn(tray, sideTop);
                var tb = WorldBounds(tray);
                trayTop = tb.max.y;
                trayAt = tb.center;
            }

            var bottle = PlacePiece("chemistry_glassware.glb", "Glass jar", room, trayAt, 0f, 0.28f, trayTop);
            if (bottle != null)
            {
                bottle.name = "HclBottle";
                DressJar(bottle);
                var bb = WorldBounds(bottle);
                var labelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/BottleLabel.png");
                var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
                label.name = "BottleLabel";
                Object.DestroyImmediate(label.GetComponent<Collider>());
                label.transform.SetParent(room, true);
                label.transform.position = bb.center + Vector3.forward * (bb.extents.z + 0.006f);
                label.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // Quad shows its face toward -Z, so turn it to face the player (+Z)
                label.transform.localScale = new Vector3(Mathf.Max(0.1f, bb.size.x * 0.82f),
                    Mathf.Max(0.12f, bb.size.y * 0.5f), 1f);
                label.GetComponent<MeshRenderer>().sharedMaterial =
                    Mat("Sc04BottleLabel", Color.white, 0.12f, 0f, labelTex, null, true);
            }

            var jarB = PlacePiece("chemistry_glassware.glb", "jar.001", room,
                new Vector3(benchX + 0.16f, sideTop, zNear - 0.58f), 30f, 0.14f, sideTop);
            UseGlass(jarB);
            var cylA = PlacePiece("chemistry_glassware.glb", "lab_cylinder_a", room,
                new Vector3(benchX - 0.14f, sideTop, zNear - 0.95f), 0f, 0.2f, sideTop);
            var cylB = PlacePiece("chemistry_glassware.glb", "lab_cylinder_b", room,
                new Vector3(benchX, sideTop, zNear - 0.98f), 8f, 0.16f, sideTop);
            var cylC = PlacePiece("chemistry_glassware.glb", "lab_cylinder_c", room,
                new Vector3(benchX + 0.14f, sideTop, zNear - 0.92f), -10f, 0.13f, sideTop);
            UseGlass(cylA);
            UseGlass(cylB);
            UseGlass(cylC);

            PipetteRack(room, new Vector3(benchX, sideTop, zNear - 1.22f), sideTop);
            Hotplate(room, new Vector3(benchX, sideTop, zNear - 1.5f), sideTop);
            var sideItemsTo = room.childCount;
            for (var ci = sideItemsTo - 1; ci >= sideItemsFrom; ci--)
                room.GetChild(ci).SetParent(bench, true);

            var lampA = room.Find("LampA");
            var lampB = room.Find("LampB");
            if (lampA != null)
                lampA.position = new Vector3(benchX - 0.05f, 3.14f, sideBox.center.z + 0.35f);
            if (lampB != null)
                lampB.position = new Vector3(benchX - 0.05f, 3.14f, sideBox.center.z - 0.85f);

            // Glass faces local X. Yaw 90 turns it toward the player. The hood closes the far
            // corner of the left bench, back against the rear wall.
            var hood = SpawnSc04("fume_cupboards.glb", room, new Vector3(benchX, 0f, sideBox.min.z), 90f, 1.6f);
            if (hood != null)
            {
                FitHeight(hood, 2.05f);
                CapHorizontal(hood, 1.7f);
                SitOn(hood, 0f);
                var hb = WorldBounds(hood);
                hood.transform.position += new Vector3((ox + 3.55f) - hb.center.x, 0f, -2.88f - hb.min.z);
                DressHood(hood);
                hb = WorldBounds(hood);
                Box("HoodMouth", room,
                    new Vector3(hb.center.x, hb.center.y + 0.08f, hb.max.z - 0.06f),
                    new Vector3(Mathf.Min(1.15f, hb.size.x * 0.62f), Mathf.Min(1.2f, hb.size.y * 0.48f), 0.04f),
                    Mat("Sc04HoodMouth", new Color(0.16f, 0.18f, 0.2f), 0.12f, 0.04f), false);
                var shelf = sideTop;
                UseGlass(PlacePiece("chemistry_glassware.glb", "Glass jar", room,
                    new Vector3(hb.center.x - 0.12f, 0f, hb.center.z), 12f, 0.12f, shelf));
                UseGlass(PlacePiece("chemistry_glassware.glb", "jar.001", room,
                    new Vector3(hb.center.x + 0.1f, 0f, hb.center.z - 0.06f), -18f, 0.1f, shelf));
            }

            // On the back wall, screen facing into the room so 25°C is readable from the bench.
            BuildAc(room, new Vector3(ox - 0.4f, 2.15f, -2.96f));

            var glovesModel = SpawnSc04("gloves1.glb", room, clothAt + new Vector3(0f, 0.02f, 0f), 0f, 0.72f);
            if (glovesModel != null)
            {
                glovesModel.transform.SetParent(bench, true);
                foreach (var rend in glovesModel.GetComponentsInChildren<Renderer>(true))
                {
                    // The file includes flat shadow cards. They read as a rotating dark wedge.
                    if (rend.name.IndexOf("Plane", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rend.gameObject.SetActive(false);
                        continue;
                    }

                    rend.transform.rotation = Quaternion.Euler(0f, 180f, 180f);
                }

                SitOn(glovesModel, gloveTop + 0.004f);
            }

            DressLab(room, bench, ox, gloveTop, gloveBox, sideTop, sideBox);
            DressLabDetails(room, ox, sideBox, gloveBox);
            Transform[] gloveGroups = null;
            if (glovesModel != null)
                // rest height = cloth top (gloveTop + 0.012) + 8 mm: a glove that sank into the cloth looked like it came out from under it
                gloveGroups = SplitGloves(glovesModel, bench, gloveTop + 0.020f);

            var height = benchGo.AddComponent<Sc04BenchHeight>();
            var hso = new SerializedObject(height);
            hso.FindProperty("builtTopY").floatValue = gloveTop;
            hso.FindProperty("minTop").floatValue = 0.75f;
            hso.FindProperty("maxTop").floatValue = 1.1f;
            hso.ApplyModifiedPropertiesWithoutUndo();
            refs.bench = height;

            // The same floating arrow as over the SC01 drums, one per glove. GloveInspect moves each one above its glove.
            var arrows = new FloatingTouchButton[3];
            for (var i = 0; i < 3; i++)
            {
                var abGo = new GameObject("GloveTouchArrow_" + (i + 1));
                abGo.transform.SetParent(room, false);
                var at = gloveGroups != null && i < gloveGroups.Length && gloveGroups[i] != null
                    ? gloveGroups[i].position + Vector3.up * 0.3f
                    : new Vector3(gloveBox.center.x, gloveTop + 0.3f, gloveBox.center.z);
                abGo.transform.position = at;
                arrows[i] = abGo.AddComponent<FloatingTouchButton>();
                SetRef(arrows[i], "art", art);
                var tso = new SerializedObject(arrows[i]);
                tso.FindProperty("touchOffset").vector3Value = new Vector3(0f, -0.08f, 0.1f); // the fingertip meets it toward the player (+Z)
                tso.FindProperty("pressRadius").floatValue = 0.12f;
                tso.FindProperty("hoverRadius").floatValue = 0.22f;
                tso.ApplyModifiedPropertiesWithoutUndo();
            }

            // card + MCQ open from a spot well behind the glove table, so the whole lab is in view
            refs.gateStand = Marker("Stand_SC04_Gate", room, new Vector3(gloveBox.center.x, 0f, 3.15f), 180f);
            var gateGo = new GameObject("Sc04GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc04Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc04Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc04Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var inspectGo = new GameObject("GloveInspect");
            inspectGo.transform.SetParent(room, false);
            var inspect = inspectGo.AddComponent<GloveInspect>();
            var so = new SerializedObject(inspect);
            so.FindProperty("art").objectReferenceValue = art;
            var arrowProp = so.FindProperty("arrows");
            arrowProp.arraySize = 3;
            for (var i = 0; i < 3; i++)
                arrowProp.GetArrayElementAtIndex(i).objectReferenceValue = arrows[i];
            var arr = so.FindProperty("gloves");
            arr.arraySize = 3;
            for (var i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue =
                    gloveGroups != null && i < gloveGroups.Length ? gloveGroups[i] : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            refs.inspect = inspect;
            // Far enough back that the eye-level instruction card sits in the open, not in the bench.
            refs.spawn = Marker("Spawn_SC04", room, new Vector3(ox, 0f, 3.15f), 180f);
            return refs;
        }

        private static void DressLab(Transform room, Transform bench, float ox, float gloveTop, Bounds gloveBox, float sideTop, Bounds sideBox)
        {
            var white = Mat("Sc04Cabinet", new Color(0.96f, 0.97f, 0.97f), 0.28f, 0.04f);
            var seam = Mat("Sc04CabinetSeam", new Color(0.75f, 0.78f, 0.8f), 0.2f, 0.08f);
            var green = Mat("Sc04Eyewash", new Color(0.1f, 0.45f, 0.18f), 0.4f, 0.15f);
            var yellow = Mat("Sc04Basin", new Color(0.95f, 0.78f, 0.12f), 0.45f, 0.05f);
            var steel = Steel();

            var cabX = ox + 3.72f;
            var cabY = 2.15f;
            var cabLen = Mathf.Max(1.6f, sideBox.size.z * 0.72f);
            Box("WallCab", room, new Vector3(cabX, cabY, sideBox.center.z), new Vector3(0.34f, 0.58f, cabLen), white);
            Box("WallCabSeam", room, new Vector3(cabX - 0.16f, cabY, sideBox.center.z),
                new Vector3(0.012f, 0.5f, 0.012f), seam, false);
            Box("WallCabRail", room, new Vector3(cabX - 0.16f, cabY - 0.26f, sideBox.center.z),
                new Vector3(0.02f, 0.02f, cabLen * 0.96f), seam, false);

            // Open floor on the viewer's right, in front of the benches.
            var ex = ox - 1.2f;
            var ez = 1.55f;
            Box("EyePole", room, new Vector3(ex, 1.2f, ez), new Vector3(0.07f, 2.4f, 0.07f), green);
            Box("EyeArm", room, new Vector3(ex + 0.22f, 1.95f, ez), new Vector3(0.42f, 0.04f, 0.04f), green);
            Box("EyeNeck", room, new Vector3(ex + 0.4f, 1.78f, ez), new Vector3(0.04f, 0.28f, 0.04f), green);
            Disc("EyeBowl", room, new Vector3(ex + 0.42f, 1.52f, ez), 0.32f, 0.04f, yellow);
            Disc("EyeBowlRim", room, new Vector3(ex + 0.42f, 1.56f, ez), 0.36f, 0.012f, yellow);
            Box("EyeArmLow", room, new Vector3(ex + 0.16f, 1.05f, ez + 0.16f), new Vector3(0.26f, 0.035f, 0.035f), green);
            Disc("EyeBowlLow", room, new Vector3(ex + 0.28f, 0.92f, ez + 0.18f), 0.2f, 0.03f, yellow);
            TriangleSign(room, new Vector3(ex + 0.08f, 1.55f, ez + 0.16f), yellow);
            Disc("FloorDrain", room, new Vector3(ex + 0.7f, 0.012f, ez + 0.15f), 0.26f, 0.008f,
                Mat("Sc04Drain", new Color(0.15f, 0.16f, 0.17f), 0.2f, 0.4f));

            Binder(bench, new Vector3(gloveBox.min.x + 0.32f, gloveTop, gloveBox.center.z), gloveTop, steel);
            TestTubes(bench, new Vector3(gloveBox.max.x - 0.22f, gloveTop, gloveBox.center.z), gloveTop);
            Fridge(room, new Vector3(ox - 2.3f, 0f, -2.8f));
            LabBackBench(room, ox);
            LabShelving(room, ox);
        }

        // White angled rack with four blue pipettes standing in a row, as on the return bench.
        private static void PipetteRack(Transform room, Vector3 at, float surfaceY)
        {
            var stand = Mat("Sc04PipetteStand", Color.white, 0.45f, 0.02f);
            var blue = Mat("Sc04Pipette", new Color(0.12f, 0.38f, 0.82f), 0.55f, 0.04f);
            Box("PipetteBase", room, at + new Vector3(0f, 0.012f, 0f), new Vector3(0.1f, 0.016f, 0.28f), stand, false);
            var back = Box("PipetteBack", room, at + new Vector3(0.02f, 0.07f, 0f), new Vector3(0.012f, 0.1f, 0.26f), stand, false);
            back.transform.rotation = Quaternion.Euler(0f, 0f, -22f);

            var donor = SpawnRaw("mechanical_pipette.glb", room, at, 0f);
            Mesh mesh = null;
            if (donor != null)
            {
                HideNamed(donor, "empty");
                foreach (var rend in donor.GetComponentsInChildren<Renderer>(true))
                {
                    var filter = rend.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && rend.name.Contains("Cube_Plastic"))
                    {
                        mesh = filter.sharedMesh;
                        break;
                    }
                }

                donor.SetActive(false);
            }

            if (mesh == null)
                return;
            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject("Pipette" + i);
                go.transform.SetParent(room, true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = blue;
                OrientUpright(go);
                FitHeight(go, 0.18f);
                PlaceOn(go, at + new Vector3(0.01f, 0f, -0.09f + i * 0.06f), surfaceY + 0.02f);
            }
        }

        private static void Hotplate(Transform room, Vector3 at, float surfaceY)
        {
            var body = Mat("Sc04Stirrer", Color.white, 0.35f, 0.02f);
            var dark = Mat("Sc04StirrerTop", new Color(0.12f, 0.12f, 0.13f), 0.25f, 0.4f);
            var y = surfaceY + 0.025f;
            Box("Stirrer", room, new Vector3(at.x, y, at.z), new Vector3(0.2f, 0.045f, 0.15f), body, false);
            Box("StirrerTop", room, new Vector3(at.x - 0.01f, y + 0.026f, at.z), new Vector3(0.12f, 0.008f, 0.1f), dark, false);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = "StirrerKnob";
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(room, true);
            knob.transform.position = new Vector3(at.x + 0.07f, y + 0.03f, at.z + 0.02f);
            knob.transform.localScale = new Vector3(0.028f, 0.01f, 0.028f);
            knob.GetComponent<MeshRenderer>().sharedMaterial = dark;
        }

        private static void Cork(Transform room, GameObject flask)
        {
            if (flask == null)
                return;
            var b = WorldBounds(flask);
            if (b.size.y < 0.02f)
                return;
            var cork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cork.name = "Cork";
            cork.transform.SetParent(room, true);
            cork.transform.position = new Vector3(b.center.x, b.max.y - 0.004f, b.center.z);
            cork.transform.localScale = new Vector3(b.size.x * 0.28f, 0.012f, b.size.z * 0.28f);
            cork.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04Cork", new Color(0.45f, 0.3f, 0.16f), 0.15f, 0f);
        }

        private static GameObject PlacePiece(string file, string exact, Transform room, Vector3 at, float yaw,
            float height, float surfaceY)
        {
            var go = SpawnRaw(file, room, at, yaw);
            if (go == null)
                return null;
            IsolateExact(go, exact);
            FitHeight(go, height);
            PlaceOn(go, at, surfaceY);
            return go;
        }

        private static void IsolateExact(GameObject go, string exact)
        {
            Transform keep = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exact)
                {
                    keep = t;
                    break;
                }
            }

            if (keep == null)
            {
                go.SetActive(false);
                return;
            }

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == go.transform || t == keep || t.IsChildOf(keep) || keep.IsChildOf(t))
                    continue;
                if (t.GetComponent<Renderer>() != null)
                    t.gameObject.SetActive(false);
            }
        }

        private static void CapHorizontal(GameObject go, float maxWidth)
        {
            var b = WorldBounds(go);
            var wide = Mathf.Max(b.size.x, b.size.z);
            if (wide <= maxWidth || wide < 0.01f)
                return;
            var factor = maxWidth / wide;
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x * factor, s.y, s.z * factor);
        }

        private static void Disc(string name, Transform room, Vector3 pos, float diameter, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(room, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(diameter, height, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void TriangleSign(Transform room, Vector3 pos, Material mat)
        {
            var path = GenDir + "/Mesh_EyeTriangle.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "EyeTriangle" };
                mesh.vertices = new[]
                {
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(0.46f, -0.4f, 0f),
                    new Vector3(-0.46f, -0.4f, 0f)
                };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }

            var go = new GameObject("EyeSign");
            go.transform.SetParent(room, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            go.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Material Steel()
        {
            return Sc04Pbr("Sc04SteelPbr", "Steel", new Color(0.93f, 0.94f, 0.95f), Vector2.one, 0.95f, 0.8f);
        }

        private static void DressSteel(GameObject table)
        {
            if (table == null)
                return;
            var steel = Steel();
            foreach (var rend in table.GetComponentsInChildren<Renderer>(true))
                rend.sharedMaterial = steel;
        }

        private static void Binder(Transform room, Vector3 at, float top, Material steel)
        {
            var y = top + 0.36f;
            Box("BinderBack", room, new Vector3(at.x, y, at.z - 0.014f), new Vector3(0.46f, 0.62f, 0.012f),
                Mat("Sc04BinderBack", new Color(0.36f, 0.22f, 0.14f), 0.25f, 0.04f), false);

            var sds = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/SDS_SC04.png");
            var page = GameObject.CreatePrimitive(PrimitiveType.Quad);
            page.name = "TableSds";
            Object.DestroyImmediate(page.GetComponent<Collider>());
            page.transform.SetParent(room, true);
            page.transform.position = new Vector3(at.x, y, at.z + 0.012f);
            page.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            page.transform.localScale = new Vector3(0.44f, 0.6f, 1f);
            page.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04TableSds", Color.white, 0.2f, 0f, sds, new Color(0.15f, 0.15f, 0.15f), true);

            for (var i = 0; i < 3; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "BinderRing" + i;
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(room, true);
                ring.transform.position = new Vector3(at.x - 0.1f + i * 0.1f, y + 0.28f, at.z + 0.02f);
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.05f, 0.006f, 0.05f);
                ring.GetComponent<MeshRenderer>().sharedMaterial = steel;
            }

            var tabs = new[]
            {
                new Color(0.2f, 0.45f, 0.85f),
                new Color(0.2f, 0.65f, 0.3f),
                new Color(0.9f, 0.55f, 0.12f),
                new Color(0.85f, 0.35f, 0.55f)
            };
            for (var i = 0; i < tabs.Length; i++)
            {
                Box("SdsTab" + i, room, new Vector3(at.x - 0.24f, y + 0.12f - i * 0.09f, at.z + 0.01f),
                    new Vector3(0.035f, 0.055f, 0.012f), Mat("Sc04Tab" + i, tabs[i], 0.25f, 0f), false);
            }
        }

        private static void BuildAc(Transform room, Vector3 pos)
        {
            var body = Mat("Sc04Ac", new Color(0.78f, 0.8f, 0.82f), 0.4f, 0.35f);
            var vent = Mat("Sc04Vent", new Color(0.12f, 0.13f, 0.15f), 0.25f, 0.55f);
            var bezel = Mat("Sc04AcBezel", new Color(0.06f, 0.08f, 0.09f), 0.45f, 0.4f);
            Box("AC", room, pos, new Vector3(1.1f, 0.46f, 0.24f), body);
            Box("ACVent", room, pos + new Vector3(0f, -0.15f, 0.12f), new Vector3(0.94f, 0.07f, 0.02f), vent, false);
            Box("ACBezel", room, pos + new Vector3(0.18f, 0.05f, 0.12f), new Vector3(0.46f, 0.22f, 0.016f), bezel, false);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/AC_Display.png");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "ACDisplay";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(room, true);
            screen.transform.position = pos + new Vector3(0.18f, 0.05f, 0.135f);
            screen.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            screen.transform.localScale = new Vector3(0.4f, 0.18f, 1f);
            screen.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04AcDisplay", Color.white, 0f, 0f, tex, new Color(0.25f, 0.7f, 0.5f), true);
            screen.AddComponent<Sc04DisplayPulse>();
        }

        private static void TestTubes(Transform room, Vector3 at, float surfaceY)
        {
            var stand = Mat("Sc04TubeRack", Color.white, 0.35f, 0.04f);
            var y = surfaceY + 0.02f;
            Box("TubeRack", room, new Vector3(at.x, y, at.z), new Vector3(0.28f, 0.04f, 0.1f), stand, false);
            Box("TubeRackLip", room, new Vector3(at.x, y + 0.03f, at.z), new Vector3(0.28f, 0.012f, 0.1f), stand, false);
            var glass = TubeGlass();
            for (var i = 0; i < 6; i++)
            {
                var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tube.name = "TestTube" + i;
                Object.DestroyImmediate(tube.GetComponent<Collider>());
                tube.transform.SetParent(room, true);
                tube.transform.position = new Vector3(at.x - 0.1f + i * 0.04f, y + 0.09f, at.z);
                tube.transform.localScale = new Vector3(0.016f, 0.07f, 0.016f);
                tube.GetComponent<MeshRenderer>().sharedMaterial = glass;
            }
        }

        private static void Fridge(Transform room, Vector3 pos)
        {
            var body = Mat("Sc04Fridge", new Color(0.86f, 0.88f, 0.9f), 0.35f, 0.12f);
            var steel = Steel();
            var gasket = Mat("Sc04Gasket", new Color(0.1f, 0.1f, 0.11f), 0.25f, 0.05f);
            var inside = Mat("Sc04FridgeInside", new Color(0.78f, 0.86f, 0.88f), 0.18f, 0.02f);
            const float h = 1.7f;
            const float w = 0.72f;
            const float d = 0.62f;
            var c = pos + new Vector3(0f, h * 0.5f, 0f);
            // Hollow cabinet (back, sides, top, bottom) with a glass door, so the contents show from outside.
            const float t = 0.04f;
            Box("FridgeBack", room, c + new Vector3(0f, 0f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), inside);
            Box("FridgeSideL", room, c + new Vector3(w * 0.5f - t * 0.5f, 0f, 0f), new Vector3(t, h, d), body);
            Box("FridgeSideR", room, c + new Vector3(-w * 0.5f + t * 0.5f, 0f, 0f), new Vector3(t, h, d), body);
            Box("FridgeTop", room, pos + new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, d), body);
            Box("FridgeBottom", room, pos + new Vector3(0f, 0.12f, 0f), new Vector3(w, t, d), body);
            Box("FridgePlinth", room, pos + new Vector3(0f, 0.06f, 0f), new Vector3(w - 0.04f, 0.12f, d - 0.04f), steel);
            var glow = Mat("Sc04FridgeLight", new Color(0.92f, 0.96f, 0.98f), 0f, 0f, null, new Color(1.5f, 1.6f, 1.6f), true);
            Box("FridgeLight", room, pos + new Vector3(0f, h - 0.1f, d * 0.5f - 0.1f), new Vector3(w - 0.2f, 0.02f, 0.03f), glow, false);
            var shelfZ = -0.02f;
            var shelfYs = new[] { 0.5f, 0.88f, 1.26f, 1.58f };
            foreach (var sy in shelfYs)
                Box("FridgeShelf", room, pos + new Vector3(0f, sy, shelfZ), new Vector3(w - 0.1f, 0.012f, d - 0.1f),
                    Mat("Sc04ShelfGlass", new Color(0.8f, 0.9f, 0.92f), 0.8f, 0.2f), false);
            StockFridge(room, pos, w, d, shelfYs);
            var doorZ = pos.z + d * 0.5f + 0.01f;
            var door = GameObject.CreatePrimitive(PrimitiveType.Quad);
            door.name = "FridgeDoor";
            Object.DestroyImmediate(door.GetComponent<Collider>());
            door.transform.SetParent(room, true);
            door.transform.position = new Vector3(pos.x, c.y, doorZ);
            door.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            door.transform.localScale = new Vector3(w - 0.12f, h - 0.22f, 1f);
            door.GetComponent<MeshRenderer>().sharedMaterial = FridgeDoorGlass();
            Box("FridgeTrimTop", room, new Vector3(pos.x, pos.y + h - 0.035f, doorZ), new Vector3(w, 0.04f, 0.02f), steel, false);
            Box("FridgeTrimBot", room, new Vector3(pos.x, pos.y + 0.05f, doorZ), new Vector3(w, 0.05f, 0.02f), steel, false);
            Box("FridgeTrimL", room, new Vector3(pos.x + w * 0.5f - 0.02f, c.y, doorZ), new Vector3(0.035f, h, 0.02f), steel, false);
            Box("FridgeTrimR", room, new Vector3(pos.x - w * 0.5f + 0.02f, c.y, doorZ), new Vector3(0.035f, h, 0.02f), steel, false);
            Box("FridgeHandle", room, new Vector3(pos.x - w * 0.5f + 0.09f, c.y, doorZ + 0.025f),
                new Vector3(0.028f, 0.46f, 0.03f), gasket, false);
        }

        private static Material LabGlass()
        {
            var glass = Mat("Sc04Glass", new Color(0.78f, 0.9f, 0.95f, 0.28f), 0.94f, 0.02f);
            glass.SetFloat("_Surface", 1f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return glass;
        }

        private static Material TubeGlass()
        {
            var glass = Mat("Sc04TubeGlass", new Color(0.82f, 0.9f, 0.93f, 0.82f), 0.75f, 0.02f);
            glass.SetFloat("_Surface", 1f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return glass;
        }

        private static void UseGlass(GameObject go)
        {
            if (go == null)
                return;
            var glass = LabGlass();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = glass;
        }

        private static void DressJar(GameObject go)
        {
            var glass = LabGlass();
            var acid = Mat("Sc04Acid", new Color(0.92f, 0.88f, 0.35f, 0.9f), 0.55f, 0.02f);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                r.sharedMaterial = n.Contains("gel") ? acid : glass;
            }
        }

        private static void DressHood(GameObject hood)
        {
            var white = Mat("Sc04Hood", new Color(0.94f, 0.95f, 0.96f), 0.42f, 0.06f);
            var glass = LabGlass();
            foreach (var r in hood.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("air"))
                    r.gameObject.SetActive(false);
                else if (n.Contains("glass"))
                    r.sharedMaterial = glass;
                else if (n.Contains("mesa"))
                    r.sharedMaterial = Mat("Sc04HoodInner", new Color(0.22f, 0.24f, 0.26f), 0.2f, 0.08f);
                else
                    r.sharedMaterial = white;
            }
        }

        // Soft gray so the floor is glossy epoxy, not a flat plastic colour and not a tile grid.
        private static Texture2D FloorTex()
        {
            var path = ArtRoot + "/2D/Sc04/FloorGray.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            var rnd = new System.Random(4);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var g = 0.56f + (float)rnd.NextDouble() * 0.07f;
                tex.SetPixel(x, y, new Color(g, g + 0.012f, g + 0.02f));
            }

            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void OrientUpright(GameObject go)
        {
            var best = go.transform.rotation;
            var bestH = WorldBounds(go).size.y;
            var turns = new[]
            {
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(-90f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 90f),
                Quaternion.Euler(0f, 0f, -90f)
            };
            foreach (var turn in turns)
            {
                go.transform.rotation = turn;
                var h = WorldBounds(go).size.y;
                if (h > bestH)
                {
                    bestH = h;
                    best = turn;
                }
            }

            go.transform.rotation = best;
        }

        private static GameObject SpawnSc04(string file, Transform parent, Vector3 pos, float yaw, float targetSize)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc04/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc04 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var b = WorldBounds(go);
            var wide = Mathf.Max(b.size.x, b.size.z);
            if (wide > 0.01f)
                go.transform.localScale *= targetSize / wide;
            return go;
        }

        private static void LabLight(Transform room, Vector3 at, float intensity)
        {
            var go = new GameObject("LabLight");
            go.transform.SetParent(room, false);
            go.transform.position = at;
            var lamp = go.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(0.9f, 0.94f, 0.9f);
            lamp.intensity = intensity;
            lamp.range = 7.5f;
            lamp.shadows = LightShadows.None;
        }

        private static GameObject SpawnRaw(string file, Transform parent, Vector3 pos, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc04/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc04 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        private static void HideNamed(GameObject go, string part)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t != go.transform && t.name.Contains(part))
                    t.gameObject.SetActive(false);
            }
        }

        // Leaves only the pieces whose name contains the word, so the reagent jar can be placed on its own.
        private static void KeepNamed(GameObject go, string part)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var keep = false;
                for (var t = r.transform; t != null; t = t.parent)
                {
                    if (t.name.Contains(part))
                        keep = true;
                }

                if (!keep)
                    r.gameObject.SetActive(false);
            }
        }

        private static void FitHeight(GameObject go, float height)
        {
            var b = WorldBounds(go);
            if (b.size.y > 0.01f)
                go.transform.localScale *= height / b.size.y;
        }

        private static void FitFootprint(GameObject go, float size)
        {
            var b = WorldBounds(go);
            var wide = Mathf.Max(b.size.x, b.size.z);
            if (wide > 0.01f)
                go.transform.localScale *= size / wide;
        }

        private static void PlaceOn(GameObject go, Vector3 at, float surfaceY)
        {
            var b = WorldBounds(go);
            go.transform.position += new Vector3(at.x - b.center.x, surfaceY - b.min.y, at.z - b.center.z);
        }

        private static void SitOn(GameObject go, float y)
        {
            var b = WorldBounds(go);
            go.transform.position += new Vector3(0f, y - b.min.y, 0f);
        }

        // Left to right in the player's view (+X to -X): cotton, nitrile, neoprene.
        private static Transform[] SplitGloves(GameObject model, Transform room, float restY)
        {
            var rends = model.GetComponentsInChildren<Renderer>();
            System.Array.Sort(rends, (a, b) => b.bounds.center.x.CompareTo(a.bounds.center.x));
            var groups = new Transform[3];
            var n = rends.Length;
            for (var g = 0; g < 3; g++)
            {
                var from = g * n / 3;
                var to = (g + 1) * n / 3;
                if (to <= from)
                    continue;
                var parent = new GameObject("Glove" + g).transform;
                parent.SetParent(room, true);
                var center = Vector3.zero;
                for (var i = from; i < to; i++)
                    center += rends[i].bounds.center;
                parent.position = center / (to - from);
                var parts = new Transform[to - from];
                for (var i = from; i < to; i++)
                    parts[i - from] = rends[i].transform;

                var follow = parent.gameObject.AddComponent<GloveParts>();
                var fso = new SerializedObject(follow);
                var partProp = fso.FindProperty("parts");
                partProp.arraySize = parts.Length;
                for (var i = 0; i < parts.Length; i++)
                    partProp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                fso.ApplyModifiedPropertiesWithoutUndo();
                var col = parent.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                var poke = parent.gameObject.AddComponent<PokeTarget>();
                var pso = new SerializedObject(poke);
                pso.FindProperty("reach").floatValue = 0.07f;
                pso.ApplyModifiedPropertiesWithoutUndo();
                groups[g] = parent;
                PaintGlove(parts, g);
                var grown = rends[from].bounds;
                for (var i = from; i < to; i++)
                    grown.Encapsulate(rends[i].bounds);
                var padded = grown.center + new Vector3(0f, 0.05f, 0.03f);
                col.center = parent.InverseTransformPoint(padded);
                col.size = grown.size + new Vector3(0.06f, 0.14f, 0.1f);
            }

            SeatGlovesInOrder(groups);
            GroundGloves(groups, restY, room);
            KeepGlovesOnCloth(groups, restY, room);
            return groups;
        }

        // Each glove lies on the cloth (lowest vertex 2 mm above it) with a soft contact shadow under it, so it
        // reads as resting on the table instead of hovering above it.
        private static void GroundGloves(Transform[] groups, float restY, Transform bench)
        {
            var shadowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png");
            var shadowMat = ShadowMat(shadowTex);
            for (var g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                    continue;
                var parts = groups[g].GetComponent<GloveParts>();
                var so = new SerializedObject(parts);
                var prop = so.FindProperty("parts");
                var rends = new System.Collections.Generic.List<Renderer>();
                for (var i = 0; i < prop.arraySize; i++)
                {
                    var part = prop.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    var r = part != null ? part.GetComponent<Renderer>() : null;
                    if (r != null)
                        rends.Add(r);
                }

                if (rends.Count == 0)
                    continue;
                var b = rends[0].bounds;
                foreach (var r in rends)
                    b.Encapsulate(r.bounds);
                var dy = restY + 0.002f - b.min.y;
                foreach (var r in rends)
                    r.transform.position += new Vector3(0f, dy, 0f);
                groups[g].position += new Vector3(0f, dy, 0f);
                b.center += new Vector3(0f, dy, 0f);

                var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shadow.name = "GloveShadow" + g;
                Object.DestroyImmediate(shadow.GetComponent<Collider>());
                shadow.transform.SetParent(bench, true);
                shadow.transform.position = new Vector3(b.center.x + 0.012f, restY - 0.0055f, b.center.z - 0.012f);
                shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                shadow.transform.localScale = new Vector3(b.size.x * 1.12f, b.size.z * 1.12f, 1f);
                shadow.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;
                shadow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Every glove (both pieces of a pair) must lie fully inside the cloth rectangle: a piece that overhangs the cloth edge
        // looks like it comes out from under the towel. Shifts a whole glove (and its contact shadow) into the cloth, with a margin.
        private static void KeepGlovesOnCloth(Transform[] groups, float restY, Transform bench)
        {
            var cloth = bench.Find("Cloth");
            if (cloth == null)
                return;
            var cb = cloth.GetComponent<Renderer>().bounds;
            const float margin = 0.03f;
            for (var g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                    continue;
                var follow = groups[g].GetComponent<GloveParts>();
                if (follow == null || follow.Parts == null || follow.Parts.Length == 0)
                    continue;
                Bounds b = default;
                var any = false;
                foreach (var p in follow.Parts)
                {
                    var r = p != null ? p.GetComponent<Renderer>() : null;
                    if (r == null)
                        continue;
                    if (!any)
                    {
                        b = r.bounds;
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }

                if (!any)
                    continue;
                var dx = 0f;
                var dz = 0f;
                if (b.min.x < cb.min.x + margin)
                    dx = cb.min.x + margin - b.min.x;
                else if (b.max.x > cb.max.x - margin)
                    dx = cb.max.x - margin - b.max.x;
                if (b.min.z < cb.min.z + margin)
                    dz = cb.min.z + margin - b.min.z;
                else if (b.max.z > cb.max.z - margin)
                    dz = cb.max.z - margin - b.max.z;
                if (Mathf.Abs(dx) < 1e-4f && Mathf.Abs(dz) < 1e-4f)
                    continue;
                var delta = new Vector3(dx, 0f, dz);
                foreach (var p in follow.Parts)
                    if (p != null)
                        p.position += delta;
                groups[g].position += delta;
                var shadow = bench.Find("GloveShadow" + g);
                if (shadow != null)
                    shadow.position += delta;
            }
        }

        private static Material ShadowMat(Texture2D tex)
        {
            var m = Mat("Sc04ContactShadow", new Color(0f, 0f, 0f, 0.55f), 0f, 0f, tex, null, true);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.55f));
            return m;
        }

        // Viewer's left to right: white cotton, blue nitrile, large black neoprene.
        private static void PaintGlove(Transform[] parts, int group)
        {
            var mats = new[]
            {
                Mat("Sc04GloveWhite", new Color(0.97f, 0.97f, 0.98f), 0.08f, 0f),
                Mat("Sc04GloveBlue", new Color(0.30f, 0.62f, 0.95f), 0.4f, 0.02f),
                Mat("Sc04GloveBlack", new Color(0.05f, 0.05f, 0.06f), 0.55f, 0.06f)
            };
            var mat = mats[Mathf.Clamp(group, 0, 2)];
            var grow = group == 2 ? 1.25f : 1f;
            // grow the pair as one piece (positions scale about the group centre too), so the two gloves of the pair keep their
            // spacing: scaling each only about its own centre pushed one glove through the other.
            var all = default(Bounds);
            var anyPart = false;
            for (var i = 0; i < parts.Length; i++)
            {
                var r0 = parts[i] != null ? parts[i].GetComponent<Renderer>() : null;
                if (r0 == null)
                    continue;
                if (!anyPart)
                {
                    all = r0.bounds;
                    anyPart = true;
                }
                else
                {
                    all.Encapsulate(r0.bounds);
                }
            }

            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                var rend = parts[i].GetComponent<Renderer>();
                if (rend == null)
                    continue;
                rend.sharedMaterial = mat;
                if (grow <= 1.01f)
                    continue;
                var center = rend.bounds.center;
                parts[i].localScale *= grow;
                var wanted = all.center + (center - all.center) * grow;
                parts[i].position += wanted - rend.bounds.center;
            }
        }

        private static void SeatGlovesInOrder(Transform[] groups)
        {
            var origin = Vector3.zero;
            var n = 0;
            for (var g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                    continue;
                origin += groups[g].position;
                n++;
            }

            if (n == 0)
                return;
            origin /= n;
            var slots = new[] { 0.34f, 0f, -0.38f }; // the black glove is the widest, keep it clear of the blue one
            for (var g = 0; g < groups.Length && g < slots.Length; g++)
            {
                if (groups[g] == null)
                    continue;
                var follow = groups[g].GetComponent<GloveParts>();
                var desired = new Vector3(origin.x + slots[g], groups[g].position.y, origin.z);
                var delta = desired - groups[g].position;
                if (follow != null)
                {
                    var so = new SerializedObject(follow);
                    var prop = so.FindProperty("parts");
                    for (var i = 0; i < prop.arraySize; i++)
                    {
                        var part = prop.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        if (part != null)
                            part.position += delta;
                    }
                }

                groups[g].position = desired;
            }
        }
    }
}
#endif

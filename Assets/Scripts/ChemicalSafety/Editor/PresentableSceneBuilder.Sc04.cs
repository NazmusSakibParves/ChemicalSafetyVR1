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
        }

        private const float Sc04Ox = 120f;

        private static Sc04Refs BuildSc04Room(Transform env, PresentableArt art)
        {
            const float ox = Sc04Ox;
            var refs = new Sc04Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solid = SolidLayer;

            var floorMat = Mat("Sc04Floor", new Color(0.74f, 0.76f, 0.78f), 0.72f, 0.12f);
            var tile = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/WallTile.png");
            var wallMat = Mat("Sc04Wall", Color.white, 0.12f, 0f, tile);
            wallMat.SetTextureScale("_BaseMap", new Vector2(10f, 5f));
            var ceilMat = Mat("Sc04Ceil", new Color(0.93f, 0.94f, 0.93f), 0.08f, 0f);
            var cloth = Mat("Sc04Cloth", Color.white, 0.04f, 0f);
            var lampMat = Mat("Sc04Lamp", new Color(0.92f, 0.94f, 0.93f), 0f, 0f, null, new Color(0.35f, 0.38f, 0.36f), true);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solid >= 0)
                    go.layer = solid;
            }

            var floor = Box("Floor", room, new Vector3(ox, -0.05f, 0.2f), new Vector3(8f, 0.1f, 7.2f), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(ox, 1.6f, -3.2f), new Vector3(8f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Front", new Vector3(ox, 1.6f, 3.6f), new Vector3(8f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Left", new Vector3(ox + 3.94f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallMat);
            Shell("Wall_Right", new Vector3(ox - 3.94f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallMat);
            Shell("Ceiling", new Vector3(ox, 3.22f, 0.2f), new Vector3(8f, 0.08f, 7.2f), ceilMat);
            Box("LampA", room, new Vector3(ox + 2.9f, 3.14f, 0.45f), new Vector3(1.6f, 0.04f, 0.18f), lampMat, false);
            Box("LampB", room, new Vector3(ox + 2.9f, 3.14f, -1.05f), new Vector3(1.6f, 0.04f, 0.18f), lampMat, false);
            LabLight(room, new Vector3(ox + 0.4f, 2.9f, 0.9f), 1.15f);
            LabLight(room, new Vector3(ox - 0.8f, 2.9f, -1.1f), 0.9f);
            LabLight(room, new Vector3(ox + 2.4f, 2.7f, -0.2f), 0.65f);

            // Viewer's left (+X) is the return bench against the tiled wall. The near bench
            // meets it and holds the gloves. The player stands at +Z looking toward -Z.
            var side = SpawnSc04("table02.glb", room, new Vector3(ox + 3.15f, 0f, -0.2f), 0f, 2.35f);
            var sideTop = 0.9f;
            var sideBox = new Bounds(new Vector3(ox + 3.15f, 0.9f, -0.2f), new Vector3(0.8f, 0.02f, 2.2f));
            if (side != null)
            {
                SitOn(side, 0f);
                var sb = WorldBounds(side);
                side.transform.position += new Vector3((ox + 3.82f) - sb.max.x, 0f, 0f);
                sideBox = WorldBounds(side);
                sideTop = sideBox.max.y;
            }

            var gloveTable = SpawnSc04("table02.glb", room, new Vector3(ox + 1.15f, 0f, 1.2f), 90f, 2.4f);
            var gloveTop = sideTop;
            var gloveBox = sideBox;
            if (gloveTable != null)
            {
                SitOn(gloveTable, 0f);
                gloveBox = WorldBounds(gloveTable);
                // Meet the return bench: the near bench's left end tucks under the return bench's near edge.
                gloveTable.transform.position += new Vector3(0f, 0f, sideBox.max.z - gloveBox.min.z - 0.08f);
                gloveBox = WorldBounds(gloveTable);
                gloveTop = gloveBox.max.y;
            }

            var clothAt = new Vector3(gloveBox.center.x - 0.05f, gloveTop + 0.008f, gloveBox.center.z);
            Box("Cloth", room, clothAt, new Vector3(1.05f, 0.01f, 0.48f), cloth, false);

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
                label.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
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
                hood.transform.position += new Vector3((ox + 3.55f) - hb.center.x, 0f, -3.02f - hb.min.z);
                ClearHoodGlass(hood);
            }

            // On the back wall, screen facing into the room so 25°C is readable from the bench.
            BuildAc(room, new Vector3(ox - 0.4f, 2.35f, -3.02f));

            var glovesModel = SpawnSc04("gloves1.glb", room, clothAt + new Vector3(0f, 0.02f, 0f), 0f, 0.72f);
            if (glovesModel != null)
            {
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

            DressLab(room, ox, gloveTop, gloveBox, sideTop, sideBox);
            Transform[] gloveGroups = null;
            if (glovesModel != null)
                gloveGroups = SplitGloves(glovesModel, room);

            var arrow = new GameObject("GloveArrow");
            arrow.transform.SetParent(room, false);
            arrow.SetActive(false);

            var inspectGo = new GameObject("GloveInspect");
            inspectGo.transform.SetParent(room, false);
            var inspect = inspectGo.AddComponent<GloveInspect>();
            var so = new SerializedObject(inspect);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("arrow").objectReferenceValue = arrow.transform;
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

        private static void DressLab(Transform room, float ox, float gloveTop, Bounds gloveBox, float sideTop, Bounds sideBox)
        {
            var white = Mat("Sc04Cabinet", new Color(0.96f, 0.97f, 0.97f), 0.28f, 0.04f);
            var seam = Mat("Sc04CabinetSeam", new Color(0.75f, 0.78f, 0.8f), 0.2f, 0.08f);
            var green = Mat("Sc04Eyewash", new Color(0.1f, 0.45f, 0.18f), 0.4f, 0.15f);
            var yellow = Mat("Sc04Basin", new Color(0.95f, 0.78f, 0.12f), 0.45f, 0.05f);
            var steel = Mat("Sc04Steel", new Color(0.78f, 0.8f, 0.82f), 0.7f, 0.85f);

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

            Binder(room, new Vector3(gloveBox.min.x + 0.28f, gloveTop, gloveBox.center.z + 0.02f), gloveTop, steel);
            TestTubes(room, new Vector3(gloveBox.center.x + 0.42f, gloveTop, gloveBox.center.z - 0.08f), gloveTop);
            Fridge(room, new Vector3(ox - 2.7f, 0f, -1.6f));
        }

        // White rack. The model contributes the pipettes (the huge empty helper is hidden).
        private static void PipetteRack(Transform room, Vector3 at, float surfaceY)
        {
            var stand = Mat("Sc04PipetteStand", Color.white, 0.3f, 0.04f);
            var blue = Mat("Sc04Pipette", new Color(0.12f, 0.32f, 0.72f), 0.4f, 0.12f);
            Box("PipetteBase", room, at + new Vector3(0f, 0.012f, 0f), new Vector3(0.28f, 0.018f, 0.12f), stand, false);
            var back = Box("PipetteBack", room, at + new Vector3(0f, 0.08f, -0.03f), new Vector3(0.26f, 0.11f, 0.018f), stand, false);
            back.transform.rotation = Quaternion.Euler(-28f, 0f, 0f);

            var model = SpawnRaw("mechanical_pipette.glb", room, at, 0f);
            if (model == null)
                return;
            HideNamed(model, "empty");
            foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
            {
                if (rend.name.Contains("Cube_Plastic"))
                    rend.sharedMaterial = blue;
            }

            FitHeight(model, 0.24f);
            PlaceOn(model, at + new Vector3(0f, 0f, 0.01f), surfaceY + 0.02f);
            var b = WorldBounds(model);
            var extra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            extra.name = "PipetteExtra";
            Object.DestroyImmediate(extra.GetComponent<Collider>());
            extra.transform.SetParent(room, true);
            extra.transform.position = new Vector3(b.max.x + 0.03f, b.center.y, b.center.z);
            extra.transform.localScale = new Vector3(0.012f, Mathf.Max(0.08f, b.size.y * 0.45f), 0.012f);
            extra.GetComponent<MeshRenderer>().sharedMaterial = blue;
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

        private static void Binder(Transform room, Vector3 at, float top, Material steel)
        {
            var board = Mat("Sc04BinderBoard", new Color(0.16f, 0.32f, 0.52f), 0.35f, 0.12f);
            var y = top + 0.26f;
            Box("BinderFoot", room, new Vector3(at.x, top + 0.012f, at.z), new Vector3(0.26f, 0.018f, 0.18f), steel, false);
            Box("BinderBoard", room, new Vector3(at.x, y, at.z), new Vector3(0.30f, 0.42f, 0.016f), board, false);
            // Clip rings across the top of the board, facing the player.
            for (var i = 0; i < 3; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "BinderRing" + i;
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(room, true);
                ring.transform.position = new Vector3(at.x - 0.06f + i * 0.06f, y + 0.16f, at.z + 0.02f);
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.045f, 0.008f, 0.045f);
                ring.GetComponent<MeshRenderer>().sharedMaterial = steel;
            }

            var sds = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/SDS_SC04.png");
            var page = GameObject.CreatePrimitive(PrimitiveType.Quad);
            page.name = "TableSds";
            Object.DestroyImmediate(page.GetComponent<Collider>());
            page.transform.SetParent(room, true);
            page.transform.position = new Vector3(at.x, y - 0.01f, at.z + 0.012f);
            page.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            page.transform.localScale = new Vector3(0.26f, 0.36f, 1f);
            page.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04TableSds", Color.white, 0.2f, 0f, sds, new Color(0.15f, 0.15f, 0.15f), true);
            var tabs = new[]
            {
                new Color(0.2f, 0.45f, 0.85f),
                new Color(0.2f, 0.65f, 0.3f),
                new Color(0.9f, 0.55f, 0.12f),
                new Color(0.75f, 0.18f, 0.16f)
            };
            for (var i = 0; i < tabs.Length; i++)
            {
                Box("SdsTab" + i, room, new Vector3(at.x + 0.155f, y + 0.12f - i * 0.07f, at.z),
                    new Vector3(0.028f, 0.045f, 0.012f), Mat("Sc04Tab" + i, tabs[i], 0.2f, 0f), false);
            }
        }

        private static void BuildAc(Transform room, Vector3 pos)
        {
            var body = Mat("Sc04Ac", new Color(0.75f, 0.77f, 0.78f), 0.35f, 0.2f);
            var vent = Mat("Sc04Vent", new Color(0.25f, 0.28f, 0.3f), 0.2f, 0.4f);
            Box("AC", room, pos, new Vector3(1.05f, 0.38f, 0.18f), body);
            Box("ACVent", room, pos + new Vector3(0f, -0.08f, 0.07f), new Vector3(0.78f, 0.05f, 0.02f), vent, false);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/AC_Display.png");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "ACDisplay";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(room, true);
            screen.transform.position = pos + new Vector3(0f, 0.05f, 0.1f);
            screen.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            screen.transform.localScale = new Vector3(0.48f, 0.16f, 1f);
            screen.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04AcDisplay", Color.white, 0f, 0f, tex, new Color(0.25f, 0.55f, 0.4f), true);
        }

        private static void TestTubes(Transform room, Vector3 at, float surfaceY)
        {
            var stand = Mat("Sc04TubeRack", Color.white, 0.3f, 0.04f);
            var y = surfaceY + 0.02f;
            Box("TubeRack", room, new Vector3(at.x, y, at.z), new Vector3(0.22f, 0.035f, 0.08f), stand, false);
            var glass = LabGlass();
            for (var i = 0; i < 6; i++)
            {
                var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tube.name = "TestTube" + i;
                Object.DestroyImmediate(tube.GetComponent<Collider>());
                tube.transform.SetParent(room, true);
                tube.transform.position = new Vector3(at.x - 0.075f + i * 0.03f, y + 0.07f, at.z);
                tube.transform.localScale = new Vector3(0.012f, 0.055f, 0.012f);
                tube.GetComponent<MeshRenderer>().sharedMaterial = glass;
            }
        }

        private static void Fridge(Transform room, Vector3 pos)
        {
            var white = Mat("Sc04Fridge", new Color(0.94f, 0.95f, 0.96f), 0.45f, 0.2f);
            var inside = Mat("Sc04FridgeInside", new Color(0.72f, 0.8f, 0.82f), 0.2f, 0.05f);
            const float h = 1.65f;
            Box("Fridge", room, pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.7f, h, 0.58f), white);
            Box("FridgeInside", room, pos + new Vector3(0.04f, h * 0.5f, 0f), new Vector3(0.52f, h - 0.16f, 0.46f), inside, false);
            Box("FridgeShelf", room, pos + new Vector3(0.04f, 0.78f, 0f), new Vector3(0.5f, 0.02f, 0.44f), white, false);
            var door = GameObject.CreatePrimitive(PrimitiveType.Quad);
            door.name = "FridgeDoor";
            Object.DestroyImmediate(door.GetComponent<Collider>());
            door.transform.SetParent(room, true);
            door.transform.position = pos + new Vector3(0.36f, h * 0.5f, 0f);
            door.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            door.transform.localScale = new Vector3(0.52f, h - 0.18f, 1f);
            door.GetComponent<MeshRenderer>().sharedMaterial = LabGlass();
            var low = pos.y + 0.42f;
            var high = pos.y + 0.95f;
            UseGlass(PlacePiece("chemistry_glassware.glb", "Glass jar", room,
                pos + new Vector3(0.02f, 0f, -0.08f), 15f, 0.16f, low));
            UseGlass(PlacePiece("chemistry_glassware.glb", "jar.001", room,
                pos + new Vector3(0.02f, 0f, 0.1f), -20f, 0.13f, low));
            UseGlass(PlacePiece("chemistry_glassware.glb", "lab_beaker_a", room,
                pos + new Vector3(0.02f, 0f, 0f), 0f, 0.1f, high));
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

        private static void ClearHoodGlass(GameObject hood)
        {
            var glass = LabGlass();
            foreach (var r in hood.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.ToLowerInvariant().Contains("glass"))
                    r.sharedMaterial = glass;
            }
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
        private static Transform[] SplitGloves(GameObject model, Transform room)
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
                var bb = rends[from].bounds;
                for (var i = from; i < to; i++)
                {
                    parts[i - from] = rends[i].transform;
                    bb.Encapsulate(rends[i].bounds);
                }

                var follow = parent.gameObject.AddComponent<GloveParts>();
                var fso = new SerializedObject(follow);
                var partProp = fso.FindProperty("parts");
                partProp.arraySize = parts.Length;
                for (var i = 0; i < parts.Length; i++)
                    partProp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                fso.ApplyModifiedPropertiesWithoutUndo();
                var col = parent.gameObject.AddComponent<BoxCollider>();
                col.center = parent.InverseTransformPoint(bb.center);
                col.size = bb.size;
                if (SolidLayer >= 0)
                    parent.gameObject.layer = SolidLayer;
                var poke = parent.gameObject.AddComponent<PokeTarget>();
                var pso = new SerializedObject(poke);
                pso.FindProperty("reach").floatValue = 0.07f;
                pso.ApplyModifiedPropertiesWithoutUndo();
                groups[g] = parent;
            }

            return groups;
        }
    }
}
#endif

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
    /// Scenario 5 as the client's chemical-storage warehouse: stained concrete, pallet racks full of blue and red steel drums and jerry
    /// cans, drums on pallets, a window with daylight, flickering tubes, rolling smoke, work clothes and a wash trough at the left, a
    /// screen-printing machine at the back. One switch: <see cref="Sc05UseWarehouse"/> = true builds this; false + Build Presentable
    /// Scene gives the earlier dye hall back (it is still built, but off). The pillar with the mask rail, the masks and the director
    /// wiring are shared by both looks.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private static readonly bool Sc05UseWarehouse = true;

        private const float WhH = 4.6f;
        private const float WhBack = -14f;
        private const float WhFront = 4.2f;

        private static Material WhYellow() => Mat("Sc05WhYellow", new Color(0.86f, 0.68f, 0.08f), 0.25f, 0f);

        private static void WhYellowLine(Transform parent, Vector3 a, Vector3 b, float width = 0.11f)
        {
            var mid = (a + b) * 0.5f;
            var d = b - a;
            var g = Box("YellowLine", parent, new Vector3(mid.x, 0.004f, mid.z), new Vector3(width, 0.006f, d.magnitude), WhYellow(), false);
            g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        private static void WhYellowRect(Transform parent, Vector3 centre, float w, float d)
        {
            var hx = w * 0.5f;
            var hz = d * 0.5f;
            WhYellowLine(parent, centre + new Vector3(-hx, 0, -hz), centre + new Vector3(hx, 0, -hz));
            WhYellowLine(parent, centre + new Vector3(hx, 0, -hz), centre + new Vector3(hx, 0, hz));
            WhYellowLine(parent, centre + new Vector3(hx, 0, hz), centre + new Vector3(-hx, 0, hz));
            WhYellowLine(parent, centre + new Vector3(-hx, 0, hz), centre + new Vector3(-hx, 0, -hz));
        }

        private static Mesh _whJerry;

        // A cheap jerry can (about 100 triangles): body, shoulder, handle block and cap, as one mesh, pivot at the base centre.
        private static Mesh WhJerryMesh()
        {
            if (_whJerry != null)
                return _whJerry;
            var b = new MeshBuf();

            void BoxAt(Vector3 c, Vector3 s)
            {
                var o = c - s * 0.5f;
                AddQuad(b, o, new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), Vector3.back, 1f, 1f);
                AddQuad(b, o + new Vector3(0, 0, s.z), new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), Vector3.forward, 1f, 1f);
                AddQuad(b, o, new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), Vector3.left, 1f, 1f);
                AddQuad(b, o + new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), Vector3.right, 1f, 1f);
                AddQuad(b, o + new Vector3(0, s.y, 0), new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), Vector3.up, 1f, 1f);
            }

            BoxAt(new Vector3(0f, 0.17f, 0f), new Vector3(0.30f, 0.34f, 0.20f));
            BoxAt(new Vector3(0f, 0.355f, 0f), new Vector3(0.26f, 0.04f, 0.17f));        // shoulder
            BoxAt(new Vector3(-0.085f, 0.40f, 0f), new Vector3(0.095f, 0.07f, 0.06f));    // handle block
            BoxAt(new Vector3(0.075f, 0.395f, 0f), new Vector3(0.05f, 0.05f, 0.05f));     // cap
            var mesh = new Mesh { name = "WhJerry" };
            mesh.SetVertices(b.V);
            mesh.SetNormals(b.N);
            mesh.SetUVs(0, b.UV);
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateBounds();
            _whJerry = mesh;
            return mesh;
        }

        private static void WhPallet(Transform parent, Vector3 floorCentre, float w, float d, float yaw)
        {
            var g = new GameObject("Pallet").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(floorCentre, Quaternion.Euler(0f, yaw, 0f));
            var wood = Mat("Sc05WhPallet", new Color(0.78f, 0.7f, 0.58f), 0.1f, 0f, Sc01Tex("Pallet_Albedo"));
            TexBox("PalletTop", g, floorCentre + new Vector3(0f, 0.125f, 0f), new Vector3(w, 0.035f, d), yaw, wood, 1f, false);
            TexBox("PalletBottom", g, floorCentre + new Vector3(0f, 0.02f, 0f), new Vector3(w, 0.03f, d), yaw, wood, 1f, false);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (var k = -1; k <= 1; k++)
                TexBox("PalletRunner", g, floorCentre + rot * new Vector3(k * (w * 0.5f - 0.06f), 0.07f, 0f), new Vector3(0.1f, 0.07f, d), yaw, wood, 1f, false);
        }

        private static Material[] _whBlues;
        private static Material[] _whReds;

        private static void WhDrumMats()
        {
            var detail = EnvTex("DRUM_Detail");
            _whBlues = new[]
            {
                Mat("DrumBlueA", new Color(0.05f, 0.23f, 0.66f), 0.55f, 0.45f, detail),
                Mat("DrumBlueB", new Color(0.07f, 0.28f, 0.72f), 0.55f, 0.45f, detail),
                Mat("DrumBlueC", new Color(0.04f, 0.18f, 0.52f), 0.5f, 0.45f, detail)
            };
            _whReds = new[]
            {
                Mat("DrumRedA", new Color(0.78f, 0.09f, 0.06f), 0.55f, 0.45f, detail),
                Mat("DrumRedB", new Color(0.62f, 0.07f, 0.05f), 0.5f, 0.45f, detail)
            };
        }

        // One steel drum standing on y (floor, pallet or shelf): detailed mesh near, the 12-sided one far; optional GHS label facing +Z.
        private static void WhDrum(Transform parent, Vector3 baseAt, System.Random rnd, bool near, bool label, Material[] labelMats, float redShare = 0.28f,
            bool hero = false)
        {
            var isRed = rnd.NextDouble() < redShare;
            if (hero)
            {
                isRed = false; // the client's drum is blue: tinting it red turned it black, so the red drums are the procedural ones
                // the client's own Drum.glb (about 10k triangles: only the drums close to the learner)
                var world = parent.TransformPoint(baseAt);
                var model = PlaceModel("3D/Drum", "HeroDrum", parent, world, (float)rnd.NextDouble() * 360f, out var radius, out var height);
                if (isRed)
                {
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = r.sharedMaterials;
                        for (var k = 0; k < ms.Length; k++)
                        {
                            var c = new Material(ms[k]);
                            c.SetColor("baseColorFactor", new Color(1f, 0.28f, 0.2f, 1f));
                            ms[k] = c;
                        }

                        r.sharedMaterials = ms;
                    }
                }

                if (label && labelMats != null)
                {
                    var lh = new GameObject("Label");
                    lh.transform.SetParent(parent, false);
                    lh.transform.localPosition = baseAt + new Vector3(0f, height * 0.52f, 0f);
                    lh.transform.localRotation = Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 50f, 0f);
                    lh.transform.localScale = Vector3.one * ((radius + 0.004f) / 0.3f);
                    lh.AddComponent<MeshFilter>().sharedMesh = CurvedLabelMesh(0.3f, 0.36f);
                    var lmr = lh.AddComponent<MeshRenderer>();
                    lmr.sharedMaterial = labelMats[rnd.Next(labelMats.Length)];
                    lmr.shadowCastingMode = ShadowCastingMode.Off;
                }

                return;
            }

            var mat = isRed ? _whReds[rnd.Next(_whReds.Length)] : _whBlues[rnd.Next(_whBlues.Length)];
            var mesh = near ? RealisticDrumMesh() : LowPolyDrumMesh();
            var go = MeshObj("Drum", parent, mesh, baseAt + new Vector3(0f, 0.44f, 0f), new Vector3(0.58f, 0.88f, 0.58f), mat);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            if (!label || labelMats == null)
                return;
            var l = new GameObject("Label");
            l.transform.SetParent(parent, false);
            l.transform.localPosition = baseAt + new Vector3(0f, 0.46f, 0f);
            l.transform.localRotation = Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 50f, 0f);
            l.transform.localScale = Vector3.one * (0.294f / 0.3f);
            l.AddComponent<MeshFilter>().sharedMesh = CurvedLabelMesh(0.3f, 0.36f);
            var mr = l.AddComponent<MeshRenderer>();
            mr.sharedMaterial = labelMats[rnd.Next(labelMats.Length)];
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Pallet rack run along X (front, and the labels, toward local +Z): blue uprights, orange beams, three levels of pallets with
        // drums (front row detailed, back rows 12-sided) and jerry cans. 'yaw' turns the whole run (-90: it runs along Z, front to -X).
        private static void BuildSc05Rack(Transform parent, Vector3 pos, int bays, int seed, Material[] labelMats, bool nearFront, float yaw)
        {
            var run = new GameObject("DrumRack").transform;
            run.SetParent(parent, false);
            run.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var grunge = Sc01Tex("Grunge_Albedo");
            var upright = Mat("Sc05RackBlue", new Color(0.09f, 0.13f, 0.27f), 0.3f, 0.25f, grunge);
            var beam = Mat("Sc05RackOrange", new Color(0.70f, 0.30f, 0.07f), 0.3f, 0.2f, grunge);
            var wood = Mat("Sc05WhPallet", new Color(0.78f, 0.7f, 0.58f), 0.1f, 0f, Sc01Tex("Pallet_Albedo"));
            var jerryGrey = Mat("Sc05JerryGrey", new Color(0.55f, 0.56f, 0.55f), 0.35f, 0f, grunge);
            var jerryBlue = Mat("Sc05JerryBlue", new Color(0.16f, 0.30f, 0.62f), 0.35f, 0f, grunge);
            var rnd = new System.Random(seed);
            const float bayW = 1.95f, depth = 1.3f, height = 3.45f;
            var levels = new[] { 0.14f, 1.30f, 2.46f };
            for (var k = 0; k <= bays; k++)
            {
                var x = (k - bays * 0.5f) * bayW;
                foreach (var dz in new[] { -depth * 0.5f, depth * 0.5f })
                    Prim("Upright", PrimitiveType.Cube, run, new Vector3(x, height * 0.5f, dz), new Vector3(0.09f, height, 0.09f), upright);
            }

            for (var b = 0; b < bays; b++)
            {
                var cx = (b - (bays - 1) * 0.5f) * bayW;
                for (var li = 0; li < levels.Length; li++)
                {
                    var y = levels[li];
                    foreach (var dz in new[] { -depth * 0.5f, depth * 0.5f })
                        Prim("Beam", PrimitiveType.Cube, run, new Vector3(cx, y, dz), new Vector3(bayW, 0.12f, 0.06f), beam);
                    Prim("Deck", PrimitiveType.Cube, run, new Vector3(cx, y + 0.07f, 0f), new Vector3(bayW - 0.06f, 0.035f, depth - 0.06f), wood);
                    // the picture: drums on the top level, jerry cans (grey and blue) on the middle level, a few drums low
                    var cans = li == 1 && rnd.NextDouble() < 0.8;
                    if (cans)
                    {
                        foreach (var rowZ in new[] { 0.3f, -0.3f })
                            for (var c = 0; c < 6; c++)
                            {
                                var cm = rnd.NextDouble() < 0.5 ? jerryGrey : jerryBlue;
                                var go = MeshObj("Jerry", run, WhJerryMesh(), new Vector3(cx - 0.75f + c * 0.3f, y + 0.09f, rowZ), Vector3.one, cm);
                                go.transform.localRotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() - 0.5) * 16f, 0f);
                            }

                        continue;
                    }

                    foreach (var rowZ in new[] { 0.3f, -0.3f })
                        foreach (var dx in new[] { -0.62f, 0f, 0.62f })
                            WhDrum(run, new Vector3(cx + dx, y + 0.09f, rowZ), rnd, nearFront && rowZ > 0f, rowZ > 0f && rnd.NextDouble() < 0.6, labelMats);
                }

                for (var i = 0; i < levels.Length - 1; i++)
                {
                    var flip = (i + b) % 2 == 0 ? 1f : -1f;
                    Beam("BackBrace", run, new Vector3(cx - flip * bayW * 0.5f, levels[i], -depth * 0.5f - 0.03f),
                        new Vector3(cx + flip * bayW * 0.5f, levels[i + 1], -depth * 0.5f - 0.03f), 0.035f, 0.035f, upright);
                }
            }

            AddSolidBox(run, "RackSolid", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height * 0.5f, 0f), new Vector3(bays * bayW, height, depth));
        }

        // Screen-printing machine: base, print bed, a slow carousel with two framed mesh screens, a squeegee arm, control box with lamps.
        private static void BuildSc05ScreenPrinter(Transform parent, Vector3 floorCentre, float yaw, List<Sc05FactoryFx.Spinner> spinners)
        {
            var root = new GameObject("ScreenPrinter").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(floorCentre, Quaternion.Euler(0f, yaw, 0f));
            var grey = Mat("Sc05PrinterGrey", new Color(0.55f, 0.57f, 0.58f), 0.4f, 0.3f);
            var dark = Mat("Sc05PrinterDark", new Color(0.16f, 0.17f, 0.18f), 0.35f, 0.3f);
            var steel = Mat("Sc05PrinterSteel", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.6f);
            var frameMat = Mat("Sc05ScreenFrame", new Color(0.66f, 0.66f, 0.62f), 0.3f, 0.3f);
            var meshMat = Mat("Sc05ScreenMesh", new Color(0.78f, 0.86f, 0.45f), 0.2f, 0f, null, new Color(0.25f, 0.32f, 0.08f), false);
            var green = Mat("Sc05PrinterGreen", new Color(0.30f, 0.42f, 0.36f), 0.4f, 0.2f);
            var lampR = Mat("Sc05PrinterLampR", new Color(1f, 0.15f, 0.1f), 0.2f, 0f, null, new Color(2f, 0.2f, 0.1f), false);
            var lampG = Mat("Sc05PrinterLampG", new Color(0.2f, 1f, 0.3f), 0.2f, 0f, null, new Color(0.3f, 2f, 0.4f), false);
            Vector3 L(float x, float y, float z) => floorCentre + Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, y, z);

            // base cabinet and print bed
            var baseBox = Box("PrinterBase", root, L(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 1.2f), grey, false);
            baseBox.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var bed = Box("PrintBed", root, L(0f, 0.93f, 0f), new Vector3(2.5f, 0.06f, 1.25f), steel, false);
            bed.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            for (var k = 0; k < 3; k++)
            {
                var pal = Box("PrintPallet", root, L(-0.8f + k * 0.8f, 0.985f, 0f), new Vector3(0.7f, 0.04f, 0.9f), dark, false);
                pal.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // column and carousel arms (spin slowly)
            Cyl("Column", root, L(0f, 0.9f, 0f), 1.7f, 0.1f, dark);
            var arms = new GameObject("CarouselArms").transform;
            arms.SetParent(root, false);
            arms.position = L(0f, 2.15f, 0f);
            arms.rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var sgn in new[] { -1f, 1f })
            {
                var arm = Box("Arm", arms, L(sgn * 0.62f, 2.15f, 0f), new Vector3(1.25f, 0.1f, 0.12f), grey, false);
                arm.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                // framed screen hanging at the arm end (tilted like a raised screen)
                var fr = new GameObject("Screen").transform;
                fr.SetParent(arms, false);
                fr.position = L(sgn * 1.12f, 1.62f, 0f);
                fr.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, sgn * 8f);
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mesh.name = "ScreenMesh";
                Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.transform.SetParent(fr, false);
                mesh.transform.localScale = new Vector3(0.9f, 1.05f, 1f);
                mesh.GetComponent<MeshRenderer>().sharedMaterial = meshMat;
                var mt = mesh.GetComponent<MeshRenderer>();
                mt.shadowCastingMode = ShadowCastingMode.Off;
                var back = Object.Instantiate(mesh, fr);
                back.name = "ScreenMeshBack";
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var e in new[] { new Vector4(0f, 0.55f, 0.96f, 0.05f), new Vector4(0f, -0.55f, 0.96f, 0.05f), new Vector4(-0.47f, 0f, 0.05f, 1.1f), new Vector4(0.47f, 0f, 0.05f, 1.1f) })
                {
                    var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    f.name = "ScreenFrame";
                    Object.DestroyImmediate(f.GetComponent<Collider>());
                    f.transform.SetParent(fr, false);
                    f.transform.localPosition = new Vector3(e.x, e.y, 0f);
                    f.transform.localScale = new Vector3(e.z, e.w, 0.05f);
                    f.GetComponent<MeshRenderer>().sharedMaterial = frameMat;
                }
            }

            spinners.Add(new Sc05FactoryFx.Spinner { target = arms, axis = Vector3.up, degreesPerSecond = 7f });

            // squeegee bar over the bed, control box with lamps
            var bar = Box("Squeegee", root, L(0f, 1.25f, 0.5f), new Vector3(2.1f, 0.06f, 0.06f), steel, false);
            bar.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var ctrl = Box("ControlBox", root, L(1.5f, 1.15f, 0.5f), new Vector3(0.42f, 0.6f, 0.3f), green, false);
            ctrl.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var l in new[] { (lampR, -0.1f), (lampG, 0.05f) })
            {
                var lamp = Box("Lamp", root, L(1.5f + l.Item2, 1.35f, 0.66f), new Vector3(0.05f, 0.05f, 0.03f), l.Item1, false);
                lamp.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        // Hanging rail with work clothes (they sway) and a wash trough with a tap, on the left wall (x1), facing -X.
        private static void BuildSc05LeftWall(Transform parent, float x1, float zMid, PresentableArt art, List<Sc05FactoryFx.Swayer> sway)
        {
            var steel = Mat("Sc05WhSteel", new Color(0.55f, 0.57f, 0.58f), 0.5f, 0.5f);
            var dark = Mat("Sc05WhDark", new Color(0.16f, 0.17f, 0.18f), 0.3f, 0.2f);
            // rail
            Sc04RollerZ("ClothesRail", parent, new Vector3(x1 - 0.32f, 1.9f, zMid), 2.6f, 0.014f, steel);
            foreach (var dz in new[] { -1.3f, 1.3f })
                Box("RailBracket", parent, new Vector3(x1 - 0.16f, 1.9f, zMid + dz), new Vector3(0.3f, 0.03f, 0.03f), steel, false);
            var tints = new[] { new Color(0.40f, 0.43f, 0.47f), new Color(0.22f, 0.32f, 0.46f), new Color(0.62f, 0.62f, 0.6f), new Color(0.3f, 0.34f, 0.3f) };
            for (var i = 0; i < tints.Length; i++)
            {
                var g = Sc12Part("Sc12Ppe.glb", "Apron", parent);
                g.name = "WorkApron" + i;
                foreach (var c in g.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
                Sc12Tint(g, "Sc05Apron" + i, tints[i], 0.15f);
                FitHeight(g, 1.0f);
                var b = BoundsOfTree(g.transform);
                g.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                b = BoundsOfTree(g.transform);
                g.transform.position += new Vector3(x1 - 0.36f - b.center.x, 1.86f - b.max.y, zMid - 1.0f + i * 0.62f - b.center.z);
                sway.Add(new Sc05FactoryFx.Swayer { target = g.transform, degrees = 2.2f, speed = 0.7f + 0.15f * i });
            }

            // wash trough: steel top with a basin, legs, gooseneck tap, wall pipe
            var zT = zMid + 0.25f; // the trough right under the clothes (the picture)
            Box("TroughTop", parent, new Vector3(x1 - 0.33f, 0.86f, zT), new Vector3(0.6f, 0.06f, 1.5f), steel, false);
            Box("TroughBasin", parent, new Vector3(x1 - 0.33f, 0.84f, zT), new Vector3(0.42f, 0.04f, 0.9f), dark, false);
            foreach (var dz in new[] { -0.68f, 0.68f })
                foreach (var dx in new[] { -0.26f, 0.26f })
                    Box("TroughLeg", parent, new Vector3(x1 - 0.33f + dx, 0.42f, zT + dz), new Vector3(0.05f, 0.84f, 0.05f), steel, false);
            Cyl("TapPipe", parent, new Vector3(x1 - 0.1f, 0.86f, zT), 0.45f, 0.018f, steel);
            Box("TapArm", parent, new Vector3(x1 - 0.2f, 1.31f, zT), new Vector3(0.22f, 0.03f, 0.03f), steel, false);
            Cyl("TapNozzle", parent, new Vector3(x1 - 0.3f, 1.25f, zT), 0.07f, 0.016f, steel);
            // a bucket and a stack of rags
            Cyl("Bucket", parent, new Vector3(x1 - 0.5f, 0f, zT + 1.25f), 0.3f, 0.14f, Mat("Sc05WhBucket", new Color(0.18f, 0.32f, 0.6f), 0.35f, 0f));

            // SPOT CLEANING sign
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc05/Sign_SpotCleaning.png");
            if (tex != null)
                QuadObj("SpotCleaningSign", parent, new Vector3(x1 - 0.03f, 2.4f, zT), new Vector3(0f, 90f, 0f), new Vector2(0.62f, 0.32f),
                    Mat("Sc05SpotCleaning", Color.white, 0.2f, 0f, tex, null, false, true));
        }

        private static ParticleSystem BuildSc05VaporFx(Transform parent)
        {
            var go = new GameObject("MaskVaporFx");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = 2f;
            main.startSpeed = 0.28f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(1f, 0.62f, 0.25f, 0.5f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.055f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.8f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.25f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat("Sc05VaporWisp", Sc05Tex("Steam_Wisp"), false);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void BuildSc05Warehouse(Transform room, float ox, float pox, PresentableArt art, List<Sc05FactoryFx.Spinner> fan,
            List<Sc05FactoryFx.Shaker> shake, List<Sc05FactoryFx.Swayer> sway, List<Sc05FactoryFx.Flicker> flicks, out ParticleSystem vaporFx)
        {
            // ---- mood: dim, hazy, grey
            var moodHost = room.parent;
            var mood = moodHost != null ? moodHost.GetComponent<Sc03Mood>() : null;
            if (mood != null)
            {
                var mso = new SerializedObject(mood);
                mso.FindProperty("ambientSky").colorValue = new Color(0.74f, 0.74f, 0.73f);
                mso.FindProperty("ambientEquator").colorValue = new Color(0.62f, 0.62f, 0.61f);
                mso.FindProperty("ambientGround").colorValue = new Color(0.30f, 0.30f, 0.29f);
                mso.FindProperty("fogColor").colorValue = new Color(0.70f, 0.70f, 0.69f);
                mso.FindProperty("fogStart").floatValue = 1.2f;
                mso.FindProperty("fogEnd").floatValue = 17f;
                mso.FindProperty("sunIntensity").floatValue = 0.06f;
                mso.FindProperty("reflectionIntensity").floatValue = 0.3f;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            WhDrumMats();
            var labelMats = new[]
            {
                LabelMat("RackLabel_Faded", "faded"), LabelMat("RackLabel_Methanol", "Methanol"), LabelMat("RackLabel_Isopropanol", "Isopropanol")
            };
            var x0 = ox - 4.2f;
            var x1 = ox + 3.4f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (WhBack + WhFront) * 0.5f;
            var w = x1 - x0;
            var d = WhFront - WhBack;

            // ---- shell: stained concrete (metric UVs)
            var wallMat = Sc01Pbr("Sc05WhWallLight", "Wall", new Color(0.96f, 0.93f, 0.87f), 0.25f, 1.0f);
            var floorMat = Sc01Pbr("Sc05WhFloorLight", "Floor", new Color(0.92f, 0.90f, 0.86f), 0.35f, 1.0f);
            var ceilMat = Sc01Pbr("Sc05WhCeilLight", "Wall", new Color(0.78f, 0.77f, 0.74f), 0.2f, 0.9f);
            TexBox("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), 0f, floorMat, 2.4f, true);
            var floorSolid = room.Find("FloorSolid");
            if (floorSolid != null)
                floorSolid.gameObject.AddComponent<TeleportationArea>();
            TexBox("Wall_Back", room, new Vector3(cx, WhH * 0.5f, WhBack), new Vector3(w, WhH, 0.12f), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Front", room, new Vector3(cx, WhH * 0.5f, WhFront), new Vector3(w, WhH, 0.12f), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Left", room, new Vector3(x1, WhH * 0.5f, cz), new Vector3(0.12f, WhH, d), 0f, wallMat, 2.4f, true);
            TexBox("Wall_Right", room, new Vector3(x0, WhH * 0.5f, cz), new Vector3(0.12f, WhH, d), 0f, wallMat, 2.4f, true);
            // TexBox has no bottom face: the ceiling is one downward-facing sheet
            Sheet("Ceiling", room, new Vector3(x0, WhH, WhBack), new Vector3(w, 0f, 0f), new Vector3(0f, 0f, d), Vector3.down, ceilMat, 2.4f, 2.4f);

            // ---- stains: oil blots and rust on the floor, drips down the walls, a dark skirt along the walls
            var oil = MatAlpha("Sc05StainOil", EnvTex("STAIN_Oil"), new Color(0.04f, 0.04f, 0.04f, 0.6f), false);
            var rust = MatAlpha("Sc05StainRust", EnvTex("STAIN_Rust"), new Color(0.5f, 0.28f, 0.14f, 0.5f), false);
            var spots = new[]
            {
                new Vector4(ox + 0.2f, 0.9f, 2.6f, 20f), new Vector4(ox - 1.4f, -1.0f, 2.0f, 80f), new Vector4(ox + 1.6f, -3.4f, 2.6f, 140f),
                new Vector4(ox - 0.4f, -5.8f, 3.0f, 30f), new Vector4(ox + 0.6f, -9.0f, 2.8f, 200f), new Vector4(ox - 2.4f, 2.8f, 1.8f, 60f)
            };
            for (var i = 0; i < spots.Length; i++)
                QuadObj("Stain" + i, room, new Vector3(spots[i].x, 0.012f + i * 0.0006f, spots[i].y), new Vector3(90f, spots[i].w, 0f),
                    new Vector2(spots[i].z, spots[i].z), i % 3 == 2 ? rust : oil);
            // the picture is stained all over: many more oil blots on the floor and dirty smudges on every wall
            var rs = new System.Random(77);
            for (var i = 0; i < 22; i++)
                QuadObj("FloorBlot" + i, room, new Vector3(x0 + 0.8f + (float)rs.NextDouble() * (w - 1.6f), 0.011f + i * 0.0003f, WhBack + 1f + (float)rs.NextDouble() * (d - 2f)),
                    new Vector3(90f, (float)rs.NextDouble() * 360f, 0f), Vector2.one * (0.9f + (float)rs.NextDouble() * 1.6f), i % 4 == 3 ? rust : oil);
            var blotTex = Sc03Tex("Stain_Blot");
            if (blotTex != null)
            {
                var smudge = Mat("Sc05WhSmudge", new Color(0.2f, 0.18f, 0.15f, 0.26f), 0.1f, 0f, blotTex, null, false, true);
                for (var i = 0; i < 26; i++)
                {
                    var wallZ = WhBack + 0.5f + (float)rs.NextDouble() * (d - 1f);
                    var y = 0.4f + (float)rs.NextDouble() * 3.4f;
                    var sz = 0.9f + (float)rs.NextDouble() * 1.8f;
                    if (i % 3 == 0)
                        QuadObj("SmudgeL" + i, room, new Vector3(x1 - 0.07f, y, wallZ), new Vector3(0f, 90f, 0f), Vector2.one * sz, smudge);
                    else if (i % 3 == 1)
                        QuadObj("SmudgeR" + i, room, new Vector3(x0 + 0.07f, y, wallZ), new Vector3(0f, -90f, 0f), Vector2.one * sz, smudge);
                    else
                        QuadObj("SmudgeB" + i, room, new Vector3(x0 + 0.5f + (float)rs.NextDouble() * (w - 1f), y, WhBack + 0.07f), Vector3.zero, Vector2.one * sz, smudge);
                }
            }

            var grimeTex = Sc03Tex("Grime_Drip");
            if (grimeTex != null)
            {
                var drip = Mat("Sc05WhDrip", new Color(0.12f, 0.1f, 0.08f, 0.55f), 0.1f, 0f, grimeTex, null, false, true);
                for (var i = 0; i < 6; i++)
                    QuadObj("WallDrip" + i, room, new Vector3(x1 - 0.07f, 1.9f, WhBack + 2.0f + i * 2.9f), new Vector3(0f, 90f, 0f), new Vector2(2.2f, 3.2f), drip);
            }

            // ---- yellow floor lines: lane edges and a box around the floor pallets
            WhYellowLine(room, new Vector3(ox - 0.2f, 0f, 3.2f), new Vector3(ox - 0.2f, 0f, -12.5f));
            WhYellowLine(room, new Vector3(ox + 1.0f, 0f, -0.2f), new Vector3(ox + 3.0f, 0f, -0.2f));
            WhYellowRect(room, new Vector3(ox - 0.35f, 0f, 0.95f), 1.5f, 1.35f);
            WhYellowRect(room, new Vector3(ox + 1.6f, 0f, -1.1f), 1.6f, 1.4f);
            WhYellowRect(room, new Vector3(ox + 0.2f, 0f, -5.2f), 1.8f, 1.5f);

            // ---- ceiling: beams, a duct and pipes, tubes with flicker
            var beam = Mat("Sc05WhBeamConcrete", new Color(0.66f, 0.65f, 0.62f), 0.15f, 0f); // concrete beams, same colour family as the ceiling (the picture)
            var duct = Mat("Sc05WhDuct", new Color(0.5f, 0.52f, 0.53f), 0.35f, 0.5f);
            var pipeMat = Mat("Sc05WhPipe", new Color(0.4f, 0.42f, 0.44f), 0.4f, 0.5f);
            for (var z = 2.4f; z > WhBack; z -= 3.2f)
                Box("CeilBeam", room, new Vector3(cx, WhH - 0.35f, z), new Vector3(w, 0.4f, 0.28f), beam, false);
            Box("Duct", room, new Vector3(ox + 1.3f, WhH - 0.62f, cz), new Vector3(0.9f, 0.55f, d - 0.6f), duct, false);
            Sc04RollerZ("CeilPipe", room, new Vector3(ox - 1.4f, WhH - 0.55f, cz), d - 0.6f, 0.08f, pipeMat);
            Sc04RollerZ("CeilPipeB", room, new Vector3(ox - 1.75f, WhH - 0.5f, cz), d - 0.6f, 0.045f, pipeMat);
            var tubeMat = Mat("Sc05WhTube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(1.2f, 1.2f, 1.15f), false);
            var housing = Mat("Sc05WhTubeHousing", new Color(0.55f, 0.56f, 0.56f), 0.4f, 0.3f);
            var tubeSpots = new[] { new Vector2(ox - 0.9f, 1.4f), new Vector2(ox + 1.5f, -1.6f), new Vector2(ox - 0.7f, -4.6f), new Vector2(ox + 1.7f, -7.6f), new Vector2(ox - 0.3f, -10.6f) };
            for (var i = 0; i < tubeSpots.Length; i++)
            {
                var p = new Vector3(tubeSpots[i].x, WhH - 0.3f, tubeSpots[i].y);
                // hanging fixture: two chains, housing, two tubes
                Box("TubeChainA", room, p + new Vector3(-0.5f, 0.2f, 0f), new Vector3(0.01f, 0.3f, 0.01f), housing, false);
                Box("TubeChainB", room, p + new Vector3(0.5f, 0.2f, 0f), new Vector3(0.01f, 0.3f, 0.01f), housing, false);
                Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(1.3f, 0.05f, 0.14f), housing, false);
                var t1 = Box("Tube" + i + "a", room, p - new Vector3(0f, 0f, 0.035f), new Vector3(1.2f, 0.035f, 0.035f), tubeMat, false);
                Box("Tube" + i + "b", room, p + new Vector3(0f, 0f, 0.035f), new Vector3(1.2f, 0.035f, 0.035f), tubeMat, false);
                var lightGo = new GameObject("TubeLight" + i);
                lightGo.transform.SetParent(room, false);
                lightGo.transform.position = p + Vector3.down * 0.5f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.92f, 0.95f, 0.9f);
                l.range = 8.5f;
                l.intensity = i < 2 ? 3.6f : 2.7f;
                l.shadows = LightShadows.None;
                if (i == 1 || i == 3)
                    flicks.Add(new Sc05FactoryFx.Flicker { light = l, glow = t1.GetComponent<Renderer>(), baseIntensity = l.intensity, seed = i * 7.3f });
            }

            // ---- window on the left wall (top-left of the view): emissive pane, bars, cool fill and a warm cookie spot with a shaft
            var glow = Mat("Sc05WhWindow", new Color(0.88f, 0.94f, 1f), 0f, 0f, null, new Color(1.25f, 1.32f, 1.4f), true);
            var frame = Mat("Sc05WhWinFrame", new Color(0.3f, 0.31f, 0.3f), 0.3f, 0.3f);
            const float wz = -2.6f;
            const float wy = 3.35f;
            Box("WinGlass", room, new Vector3(x1 - 0.07f, wy, wz), new Vector3(0.02f, 1.4f, 2.4f), glow, false);
            Box("WinFrameTop", room, new Vector3(x1 - 0.09f, wy + 0.72f, wz), new Vector3(0.08f, 0.08f, 2.5f), frame, false);
            Box("WinFrameBot", room, new Vector3(x1 - 0.09f, wy - 0.72f, wz), new Vector3(0.1f, 0.1f, 2.5f), frame, false);
            for (var k = 0; k <= 4; k++)
                Box("WinBarV", room, new Vector3(x1 - 0.1f, wy, wz - 1.2f + k * 0.6f), new Vector3(0.04f, 1.4f, 0.04f), frame, false);
            Box("WinBarH", room, new Vector3(x1 - 0.1f, wy, wz), new Vector3(0.04f, 0.04f, 2.4f), frame, false);
            var fill = new GameObject("WindowFill");
            fill.transform.SetParent(room, false);
            fill.transform.position = new Vector3(x1 - 0.9f, wy, wz);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = new Color(0.85f, 0.92f, 1f);
            fl.range = 7f;
            fl.intensity = 3.0f;
            fl.shadows = LightShadows.None;
            var sunGo = new GameObject("WindowSun");
            sunGo.transform.SetParent(room, false);
            sunGo.transform.position = new Vector3(x1 - 0.6f, wy, wz);
            var target = new Vector3(ox - 0.4f, 0f, -2.0f);
            sunGo.transform.rotation = Quaternion.LookRotation(target - sunGo.transform.position, Vector3.up);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Spot;
            sun.color = new Color(1f, 0.94f, 0.8f);
            sun.spotAngle = 44f;
            sun.range = 10f;
            sun.intensity = 12f;
            sun.shadows = LightShadows.None;
            sun.cookie = Sc04WindowCookie();
            var shaftTex = EnvTex("FX_Shaft");
            if (shaftTex != null)
            {
                var shaftMat = MatAlpha("Sc05WhShaft", shaftTex, new Color(1f, 0.97f, 0.88f, 0.13f), true);
                var from = new Vector3(x1 - 0.3f, wy + 0.2f, wz);
                var to = target + new Vector3(0f, 0.3f, 0f);
                var dir = (to - from);
                for (var i = 0; i < 2; i++)
                {
                    var q = QuadObj("WindowShaft", room, Vector3.zero, Vector3.zero, new Vector2(1.7f, dir.magnitude), shaftMat);
                    q.transform.position = from + dir * 0.5f;
                    q.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized) * Quaternion.Euler(0f, i * 90f, 0f);
                }
            }

            // ---- the picture: ONE rack at the left of the aisle, running away from the learner, front toward the aisle (-X)
            BuildSc05Rack(room, new Vector3(ox + 2.1f, 0f, -4.65f), 3, 11, labelMats, true, -90f);

            // ---- floor pallets with drums: two beside the pillar (hero drums), one at the rack foot, one in the smoke further back
            var rnd = new System.Random(5);
            var pallets = new[]
            {
                new Vector4(ox - 0.75f, 1.3f, 8f, 4f), new Vector4(ox + 0.9f, -2.4f, -6f, 4f), new Vector4(ox - 0.35f, -5.4f, 14f, 4f),
                new Vector4(ox + 0.1f, -7.4f, 0f, 3f)
            };
            for (var p = 0; p < pallets.Length; p++)
            {
                var centre = new Vector3(pallets[p].x, 0f, pallets[p].y);
                WhPallet(room, centre, 1.2f, 1.0f, pallets[p].z);
                var rot = Quaternion.Euler(0f, pallets[p].z, 0f);
                var n = (int)pallets[p].w;
                for (var k = 0; k < n; k++)
                {
                    var off = rot * new Vector3(-0.3f + (k % 2) * 0.6f, 0.14f, -0.28f + (k / 2) * 0.56f);
                    WhDrum(room, centre + off, rnd, p < 3, (k / 2) == 1 && rnd.NextDouble() < 0.7, labelMats, 0.4f, p < 2); // the two pallets near the learner use the client's Drum.glb
                }
            }

            // a few more drums standing on the floor along the right and far left, jerry cans stacked at the rack foot
            WhDrum(room, new Vector3(ox - 1.6f, 0f, -2.0f), rnd, false, false, labelMats, 0.5f);
            WhDrum(room, new Vector3(ox - 2.0f, 0f, -3.1f), rnd, false, false, labelMats, 0.3f);
            var jg = Mat("Sc05JerryGrey", new Color(0.55f, 0.56f, 0.55f), 0.35f, 0f, Sc01Tex("Grunge_Albedo"));
            var jb = Mat("Sc05JerryBlue", new Color(0.16f, 0.30f, 0.62f), 0.35f, 0f, Sc01Tex("Grunge_Albedo"));
            for (var i = 0; i < 10; i++)
            {
                var go = MeshObj("FloorJerry", room, WhJerryMesh(), new Vector3(ox + 0.95f, (i % 5 >= 3 ? 0.34f : 0f), -0.9f - (i % 5) * 0.27f - (i / 5) * 0.9f), Vector3.one,
                    i % 3 == 0 ? jb : jg);
                go.transform.rotation = Quaternion.Euler(0f, 90f + (float)(rnd.NextDouble() - 0.5) * 18f, 0f);
            }

            // ---- back of the hall, right of centre in the smoke: the screen-printing machine; a calender at the far left
            BuildSc05ScreenPrinter(room, new Vector3(ox - 2.1f, 0f, -8.4f), 0f, fan);
            Sc04Calender(room, "BackCalender", new Vector3(ox + 2.4f, 0f, -12.4f), 2.6f, 1.1f, 1.95f, -1f, true);

            // ---- left wall: hanging work clothes and the wash trough
            BuildSc05LeftWall(room, x1, -2.5f, art, sway);

            // ---- pillar: a yellow-black hazard band at its foot, the client's VAPOR HAZARD sign is in the pillar code
            var black = Mat("Sc05WhBlack", new Color(0.06f, 0.06f, 0.07f), 0.3f, 0f);
            for (var k = 0; k < 9; k++)
                Box("PillarHazard" + k, room, new Vector3(pox - 1.78f + k * 0.2f, 0.09f, 0.62f), new Vector3(0.2f, 0.18f, 0.02f), k % 2 == 0 ? WhYellow() : black, false);

            // ---- air: dust in the window light, rolling smoke around the pallets, low mist
            var glowTex = art != null && art.radialGlow != null ? art.radialGlow.texture : null;
            BuildDust(room, new Vector3(ox + 0.6f, 2.1f, -2.0f), new Vector3(7f, 3.4f, 9f), ParticleMat("Sc05Dust", glowTex, true));
            var puff = ParticleMat("Sc05SteamPuff", Sc05Tex("Steam_Puff"), false);
            var wisp = ParticleMat("Sc05SteamWisp", Sc05Tex("Steam_Wisp"), false);
            Steam(room, "SmokeA", new Vector3(ox + 1.6f, 0.5f, -1.1f), 1.7f, 4f, 0.28f, puff, 0.4f, 7f);
            Steam(room, "SmokeA2", new Vector3(ox + 1.9f, 0.4f, -1.4f), 1.4f, 3f, 0.22f, wisp, 0.3f, 8f);
            Steam(room, "SmokeB", new Vector3(ox + 0.2f, 0.5f, -5.2f), 2.1f, 4f, 0.3f, puff, 0.42f, 8f);
            Steam(room, "SmokeB2", new Vector3(ox + 0.5f, 0.4f, -4.6f), 1.6f, 3f, 0.2f, wisp, 0.32f, 8f);
            Steam(room, "SmokeC", new Vector3(ox - 0.35f, 0.5f, 0.95f), 1.2f, 2.5f, 0.2f, wisp, 0.26f, 6f);
            Steam(room, "SmokeBack", new Vector3(ox - 1.0f, 0.6f, -9.5f), 2.4f, 3f, 0.25f, puff, 0.32f, 9f);
            Steam(room, "MistLow", new Vector3(ox + 0.5f, 0.2f, -3.0f), 1.8f, 2.2f, 0.05f, wisp, 0.18f, 7f);
            Steam(room, "MistLow2", new Vector3(ox - 0.8f, 0.2f, -7.5f), 2.0f, 2.2f, 0.05f, wisp, 0.18f, 7f);

            vaporFx = BuildSc05VaporFx(room);
        }
    }
}
#endif

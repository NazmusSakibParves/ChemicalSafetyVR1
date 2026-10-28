#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 10 lab realism: the hazardous waste bin (hidden until DISPOSE), the SDS table and wall poster, the sink counter and
    /// bench run, dispensers, exit door, shelves with reagent bottles, ducts and sprinklers, the window, floor wear and a baked
    /// reflection probe, so the room reads as a real chemistry lab and not a box with a spill in it.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        // ---------------------------------------------------------------- hazardous waste bin (root is inactive until the DISPOSE step)
        private static Transform Sc10Bin(Transform parent, float bx, float bz)
        {
            var yellow = Mat("Sc10BinYellow2", new Color(0.96f, 0.77f, 0.05f), 0.58f, 0f);
            var rib = Mat("Sc10BinYellowRib", new Color(0.84f, 0.64f, 0.03f), 0.5f, 0f);
            var dark = Mat("Sc10BinTrim", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0f);
            var inside = Mat("Sc10BinInside", new Color(0.12f, 0.10f, 0.03f), 0.2f, 0f);
            var root = new GameObject("Sc10Bin").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(bx, 0f, bz);
            GameObject B(string n, Vector3 p, Vector3 s, Material m) => Sc08LocalBox(n, root, p, s, m);

            B("Plinth", new Vector3(0f, 0.04f, 0f), new Vector3(0.56f, 0.08f, 0.52f), dark);
            B("WallFront", new Vector3(-0.2375f, 0.42f, 0f), new Vector3(0.025f, 0.68f, 0.5f), yellow);
            B("WallBack", new Vector3(0.2375f, 0.42f, 0f), new Vector3(0.025f, 0.68f, 0.5f), yellow);
            B("WallL", new Vector3(0f, 0.42f, 0.2375f), new Vector3(0.5f, 0.68f, 0.025f), yellow);
            B("WallR", new Vector3(0f, 0.42f, -0.2375f), new Vector3(0.5f, 0.68f, 0.025f), yellow);
            B("Floor", new Vector3(0f, 0.09f, 0f), new Vector3(0.46f, 0.012f, 0.46f), inside);
            B("RimF", new Vector3(-0.26f, 0.765f, 0f), new Vector3(0.03f, 0.03f, 0.54f), yellow);
            B("RimB", new Vector3(0.26f, 0.765f, 0f), new Vector3(0.03f, 0.03f, 0.54f), yellow);
            B("RimL", new Vector3(0f, 0.765f, 0.26f), new Vector3(0.5f, 0.03f, 0.03f), yellow);
            B("RimR", new Vector3(0f, 0.765f, -0.26f), new Vector3(0.5f, 0.03f, 0.03f), yellow);
            for (var i = -1; i <= 1; i++)
            {
                B("RibF" + i, new Vector3(-0.2535f, 0.42f, i * 0.17f), new Vector3(0.006f, 0.62f, 0.04f), rib);
                B("RibL" + i, new Vector3(i * 0.17f, 0.42f, 0.2535f), new Vector3(0.04f, 0.62f, 0.006f), rib);
                B("RibR" + i, new Vector3(i * 0.17f, 0.42f, -0.2535f), new Vector3(0.04f, 0.62f, 0.006f), rib);
            }

            B("HandleL", new Vector3(0f, 0.64f, 0.262f), new Vector3(0.22f, 0.05f, 0.018f), dark);
            B("HandleR", new Vector3(0f, 0.64f, -0.262f), new Vector3(0.22f, 0.05f, 0.018f), dark);
            B("Pedal", new Vector3(-0.31f, 0.06f, 0f), new Vector3(0.1f, 0.02f, 0.16f), dark);
            B("PedalArm", new Vector3(-0.27f, 0.12f, 0f), new Vector3(0.02f, 0.1f, 0.03f), dark);
            for (var s = -1; s <= 1; s += 2)
            {
                var wheel = Prim("Wheel" + s, PrimitiveType.Cylinder, root, new Vector3(0.18f, 0.05f, s * 0.24f), new Vector3(0.1f, 0.02f, 0.1f), dark);
                wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // label: a dark plate with a margin, then the sticker, with room around the symbol and the text
            B("LabelPlate", new Vector3(-0.2535f, 0.42f, 0f), new Vector3(0.006f, 0.47f, 0.39f), dark);
            QuadObj("BinLabel", root, new Vector3(-0.2575f, 0.42f, 0f), new Vector3(0f, 90f, 0f), new Vector2(0.35f, 0.4375f),
                Sc06Label("Sc10BinLabelMat", "Label_Sc10BinLabel", false, 0.4f));

            var lid = new GameObject("Sc10BinLid").transform;
            lid.SetParent(root, false);
            lid.localPosition = new Vector3(0.27f, 0.78f, 0f);
            lid.localRotation = Quaternion.Euler(0f, 270f, 0f);
            Sc08LocalBox("LidSlab", lid, new Vector3(0f, 0.015f, 0.275f), new Vector3(0.58f, 0.03f, 0.56f), yellow);
            Sc08LocalBox("LidLip", lid, new Vector3(0f, -0.005f, 0.275f), new Vector3(0.5f, 0.02f, 0.5f), rib);
            Sc08LocalBox("LidGrip", lid, new Vector3(0f, 0.04f, 0.52f), new Vector3(0.24f, 0.03f, 0.04f), dark);
            Marker("Sc10BinPoint", root, new Vector3(bx, 0.1f, bz), 0f);
            root.gameObject.SetActive(false);
            return root;
        }

        // ---------------------------------------------------------------- the SDS table: steel frame, black epoxy top, drawers, papers, a folder and a wall poster
        private static void Sc10Table(Transform bay, float x, float z, float wallX, Material steel, Material darkSteel)
        {
            // brushed stainless steel like the picture: top, solid side and back panels, legs
            var top = Mat("Sc10TableSteel", new Color(0.66f, 0.68f, 0.70f), 0.62f, 0.55f);
            var m = Sc08FurnMats(top);
            m.edge = Mat("Sc10TableSteelEdge", new Color(0.55f, 0.57f, 0.60f), 0.6f, 0.5f);
            Sc08Top(bay, "LabTable", m, new Vector3(x, 0.9f, z), 0.72f, 1.3f, 0.04f);
            Box("TableSidePanelA", bay, new Vector3(x, 0.55f, z - 0.62f), new Vector3(0.66f, 0.66f, 0.02f), top, false);
            Box("TableSidePanelB", bay, new Vector3(x, 0.55f, z + 0.62f), new Vector3(0.66f, 0.66f, 0.02f), top, false);
            Box("TableBackPanel", bay, new Vector3(x + 0.34f, 0.55f, z), new Vector3(0.02f, 0.66f, 1.22f), top, false);
            for (var sx = -1; sx <= 1; sx += 2)
                for (var sz = -1; sz <= 1; sz += 2)
                    Box("TableLeg", bay, new Vector3(x + sx * 0.32f, 0.43f, z + sz * 0.6f), new Vector3(0.04f, 0.86f, 0.04f), darkSteel, false);
            Box("TableShelf", bay, new Vector3(x, 0.2f, z), new Vector3(0.64f, 0.02f, 1.2f), steel, false);
            Box("TableApronF", bay, new Vector3(x - 0.33f, 0.83f, z), new Vector3(0.02f, 0.08f, 1.22f), darkSteel, false);
            Box("TableApronB", bay, new Vector3(x + 0.33f, 0.83f, z), new Vector3(0.02f, 0.08f, 1.22f), darkSteel, false);
            // drawer unit under the far end of the top
            Box("TableCarcass", bay, new Vector3(x, 0.62f, z + 0.43f), new Vector3(0.62f, 0.4f, 0.4f), m.body, false);
            for (var i = 0; i < 2; i++)
            {
                var y = 0.7f - i * 0.2f;
                Box("TableDrawer" + i, bay, new Vector3(x - 0.318f, y, z + 0.43f), new Vector3(0.014f, 0.185f, 0.37f), m.panel, false);
                Box("TableGrip" + i, bay, new Vector3(x - 0.332f, y + 0.04f, z + 0.43f), new Vector3(0.014f, 0.02f, 0.16f), m.handle, false);
            }

            // papers: a yellow folder, the SDS sheet laid on it (big SDS title), a second sheet and a pen
            var folder = Box("SdsFolder", bay, new Vector3(x + 0.05f, 0.912f, z - 0.2f), new Vector3(0.3f, 0.012f, 0.4f), Mat("Sc10Folder", new Color(0.95f, 0.77f, 0.08f), 0.35f, 0f), false);
            folder.transform.rotation = Quaternion.Euler(0f, -16f, 0f);
            var paperMat = Sc06Label("Sc10PaperMat", "Label_Sc10Paper", false, 0.2f);
            QuadObj("SdsPaper", bay, new Vector3(x - 0.02f, 0.9205f, z - 0.2f), new Vector3(90f, 106f, 0f), new Vector2(0.3f, 0.42f), paperMat);
            QuadObj("SdsPaper2", bay, new Vector3(x - 0.14f, 0.918f, z + 0.16f), new Vector3(90f, 80f, 0f), new Vector2(0.26f, 0.36f), paperMat);
            var pen = Box("Pen", bay, new Vector3(x - 0.17f, 0.921f, z - 0.18f), new Vector3(0.14f, 0.008f, 0.008f), Mat("Sc10Pen", new Color(0.1f, 0.2f, 0.6f), 0.5f, 0f), false);
            pen.transform.rotation = Quaternion.Euler(0f, 28f, 0f);

            // wall poster + a yellow SDS pocket on the concrete above the table
            Box("SdsPosterFrame", bay, new Vector3(wallX - 0.012f, 1.72f, z), new Vector3(0.012f, 0.69f, 0.5f), darkSteel, false);
            QuadObj("SdsPoster", bay, new Vector3(wallX - 0.019f, 1.72f, z), new Vector3(0f, 90f, 0f), new Vector2(0.45f, 0.63f),
                Sc06Label("Sc10SdsPosterMat", "Label_Sc10SdsPoster", false, 0.3f));
            Box("SdsPocket", bay, new Vector3(wallX - 0.05f, 1.28f, z), new Vector3(0.07f, 0.16f, 0.4f), Mat("Sc10Folder", new Color(0.95f, 0.77f, 0.08f), 0.35f, 0f), false);
        }

        // ---------------------------------------------------------------- lab dressing: counter, benches, shelves, door, window, ceiling, floor
        private static void Sc10LabDress(Transform room, float ox, float x0, float x1, float zBack, float zFront, float h)
        {
            var white = Mat("Sc06Paint", new Color(0.90f, 0.90f, 0.88f), 0.45f, 0f);
            var darkSteel = Mat("Sc06DarkSteel", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0f);
            var steel = Mat("Sc08CabinetHandle", new Color(0.70f, 0.72f, 0.75f), 0.8f, 0.55f);
            var plasticWhite = Mat("Sc08Dispenser", new Color(0.93f, 0.94f, 0.93f), 0.55f, 0f);
            var epoxy = Mat("Sc08Epoxy", new Color(0.05f, 0.052f, 0.056f), 0.78f, 0f);
            var m = Sc08FurnMats(epoxy);

            // sink counter on the back wall right of the hood (same unit as the Scenario 8 lab) and its dispensers
            Sc08Counter(room, ox - 0.2f, ox + 1.45f, zBack, 0.9f, ox + 0.6f, epoxy);
            Box("PaperTowel", room, new Vector3(ox + 1.2f, 1.40f, zBack + 0.07f), new Vector3(0.28f, 0.36f, 0.14f), plasticWhite, false);
            Box("PaperTowelSlot", room, new Vector3(ox + 1.2f, 1.25f, zBack + 0.145f), new Vector3(0.22f, 0.02f, 0.01f), darkSteel, false);
            Box("SoapDispenser", room, new Vector3(ox + 0.12f, 1.28f, zBack + 0.05f), new Vector3(0.10f, 0.18f, 0.09f), plasticWhite, false);
            Box("SoapPump", room, new Vector3(ox + 0.12f, 1.17f, zBack + 0.10f), new Vector3(0.02f, 0.03f, 0.05f), steel, false);
            Sc08QuadWorld("ShowerSignBack", room, new Vector3(ox + 0.95f, 1.9f, zBack + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.3f, 0.39f),
                Sc06Label("Sc08EyewashSign", "Label_Sc08EyewashSign", true, 0.3f));

            // front wall: a bench run with drawers and doors, black epoxy top, shelves above with reagent bottles
            const float benchL = 3.4f;
            var bcx = ox - 1.9f;
            const float depth = 0.64f;
            var bz = zFront - depth * 0.5f;
            Box("BenchCarcass", room, new Vector3(bcx, 0.46f, bz), new Vector3(benchL - 0.1f, 0.72f, depth - 0.08f), m.body, false);
            Box("BenchKick", room, new Vector3(bcx, 0.05f, bz), new Vector3(benchL - 0.16f, 0.1f, depth - 0.14f), m.kick, false);
            Sc08Bays(room, "FrontBench", m, bcx, zFront - depth + 0.04f, -1f, benchL - 0.1f, 0.1f, 0.86f);
            Sc08Top(room, "FrontBenchTop", m, new Vector3(bcx, 0.9f, bz), benchL, depth, 0.04f);
            Box("FrontSplash", room, new Vector3(bcx, 1.0f, zFront - 0.02f), new Vector3(benchL, 0.2f, 0.03f), m.panel, false);
            var amber = Mat("Sc10BottleAmber", new Color(0.42f, 0.20f, 0.05f), 0.75f, 0f);
            var clear = Mat("Sc10BottleWhite", new Color(0.9f, 0.92f, 0.93f), 0.7f, 0f);
            var blue = Mat("Sc10BottleBlue", new Color(0.12f, 0.28f, 0.55f), 0.7f, 0f);
            var capRed = Mat("Sc10CapRed", new Color(0.72f, 0.08f, 0.07f), 0.5f, 0f);
            for (var shelf = 0; shelf < 2; shelf++)
            {
                var sy = 1.45f + shelf * 0.5f;
                Box("WallShelf" + shelf, room, new Vector3(bcx, sy, zFront - 0.13f), new Vector3(benchL - 0.3f, 0.03f, 0.26f), steel, false);
                for (var b = 0; b < 9; b++)
                {
                    var bxx = bcx - (benchL - 0.7f) * 0.5f + b * ((benchL - 0.7f) / 8f);
                    var mat = (b + shelf) % 3 == 0 ? amber : ((b + shelf) % 3 == 1 ? clear : blue);
                    var hgt = 0.17f + ((b * 7 + shelf * 3) % 4) * 0.025f;
                    Prim("Bottle" + shelf + "_" + b, PrimitiveType.Cylinder, room, new Vector3(bxx, sy + 0.015f + hgt * 0.5f, zFront - 0.13f), new Vector3(0.075f, hgt * 0.5f, 0.075f), mat);
                    Prim("Cap" + shelf + "_" + b, PrimitiveType.Cylinder, room, new Vector3(bxx, sy + 0.03f + hgt, zFront - 0.13f), new Vector3(0.04f, 0.012f, 0.04f), capRed);
                }
            }

            // left of the spill bay along the front wall: a run of base cabinets with a steel top (as in the picture), clear of the waste bin
            {
                var cl = 2.0f;
                var ccx = ox + 1.2f;
                var cz = zFront - depth * 0.5f;
                var steelTop = Mat("Sc10TableSteel", new Color(0.66f, 0.68f, 0.70f), 0.62f, 0.55f);
                var mc = Sc08FurnMats(steelTop);
                mc.edge = Mat("Sc10TableSteelEdge", new Color(0.55f, 0.57f, 0.60f), 0.6f, 0.5f);
                Box("LeftCarcass", room, new Vector3(ccx, 0.46f, cz), new Vector3(cl - 0.05f, 0.72f, depth - 0.08f), mc.body, false);
                Box("LeftKick", room, new Vector3(ccx, 0.05f, cz), new Vector3(cl - 0.1f, 0.1f, depth - 0.14f), mc.kick, false);
                Sc08Bays(room, "LeftBench", mc, ccx, zFront - depth + 0.04f, -1f, cl - 0.05f, 0.1f, 0.86f);
                Sc08Top(room, "LeftBenchTop", mc, new Vector3(ccx, 0.9f, cz), cl, depth, 0.04f);
            }

            // base cabinets with a steel top along the −X face of the corner block (background left in the picture): built like the front bench, then turned
            {
                const float cl2 = 1.5f;
                var cx2 = ox + 1.5f - depth * 0.5f;
                const float cz2 = -1.3f;
                var rig = new GameObject("Sc10BlockCabinets").transform;
                rig.SetParent(room, false);
                var steelTop2 = Mat("Sc10TableSteel", new Color(0.66f, 0.68f, 0.70f), 0.62f, 0.55f);
                var mc2 = Sc08FurnMats(steelTop2);
                mc2.edge = Mat("Sc10TableSteelEdge", new Color(0.55f, 0.57f, 0.60f), 0.6f, 0.5f);
                Box("BlockCarcass", rig, new Vector3(cx2, 0.46f, cz2), new Vector3(cl2 - 0.05f, 0.72f, depth - 0.08f), mc2.body, false);
                Box("BlockKick", rig, new Vector3(cx2, 0.05f, cz2), new Vector3(cl2 - 0.1f, 0.1f, depth - 0.14f), mc2.kick, false);
                Sc08Bays(rig, "BlockBench", mc2, cx2, cz2 - depth * 0.5f + 0.04f, -1f, cl2 - 0.05f, 0.1f, 0.86f);
                Sc08Top(rig, "BlockBenchTop", mc2, new Vector3(cx2, 0.9f, cz2), cl2, depth, 0.04f);
                rig.RotateAround(new Vector3(cx2, 0f, cz2), Vector3.up, 90f);
            }

            // right wall: exit door, first aid box, green safety sign
            const float doorZ = -1.75f;
            var doorMat = Mat("Sc08DoorLeaf", new Color(0.70f, 0.72f, 0.70f), 0.35f, 0f);
            var frame = Mat("Sc08DoorFrame", new Color(0.32f, 0.34f, 0.35f), 0.4f, 0.1f);
            Box("DoorLeaf", room, new Vector3(x0 + 0.045f, 1.03f, doorZ), new Vector3(0.045f, 2.06f, 0.94f), doorMat, false);
            Box("DoorFrameTop", room, new Vector3(x0 + 0.05f, 2.09f, doorZ), new Vector3(0.08f, 0.07f, 1.08f), frame, false);
            Box("DoorFrameA", room, new Vector3(x0 + 0.05f, 1.04f, doorZ - 0.52f), new Vector3(0.08f, 2.08f, 0.07f), frame, false);
            Box("DoorFrameB", room, new Vector3(x0 + 0.05f, 1.04f, doorZ + 0.52f), new Vector3(0.08f, 2.08f, 0.07f), frame, false);
            Box("DoorVisionFrame", room, new Vector3(x0 + 0.075f, 1.50f, doorZ), new Vector3(0.012f, 0.62f, 0.26f), frame, false);
            Box("DoorVision", room, new Vector3(x0 + 0.083f, 1.50f, doorZ), new Vector3(0.006f, 0.56f, 0.20f), Sc06GlassMat("Sc08DoorGlass", new Color(0.62f, 0.74f, 0.80f), 0.3f, 0.5f), false);
            Box("DoorKick", room, new Vector3(x0 + 0.076f, 0.18f, doorZ), new Vector3(0.008f, 0.30f, 0.88f), steel, false);
            Box("DoorLever", room, new Vector3(x0 + 0.10f, 1.0f, doorZ + 0.38f), new Vector3(0.05f, 0.025f, 0.12f), steel, false);
            Sc08QuadWorld("ExitSign", room, new Vector3(x0 + 0.015f, 2.34f, doorZ), new Vector3(0f, -90f, 0f), new Vector2(0.44f, 0.19f),
                Sc06Label("Sc08ExitSign", "Label_Sc08Exit", true, 0.3f));
            Box("FirstAidBox", room, new Vector3(x0 + 0.06f, 1.45f, 2.0f), new Vector3(0.10f, 0.34f, 0.28f), Mat("Sc08FirstAidBox", new Color(0.15f, 0.55f, 0.32f), 0.4f, 0f), false);
            Sc08QuadWorld("FirstAidSign", room, new Vector3(x0 + 0.1105f, 1.45f, 2.0f), new Vector3(0f, -90f, 0f), new Vector2(0.24f, 0.24f),
                Sc06Label("Sc08FirstAid", "Label_Sc08FirstAid", true, 0.3f));

            // front wall interior window with a corridor glow and half-drawn blinds
            var glass = Sc06GlassMat("Sc08WindowGlass", new Color(0.78f, 0.86f, 0.9f), 0.2f, 0.5f);
            var corridor = Mat("Sc08Corridor", new Color(0.85f, 0.87f, 0.86f), 0.1f, 0f, null, new Color(0.55f, 0.58f, 0.58f), false);
            var frameGrey = Mat("Sc08WindowFrame", new Color(0.38f, 0.40f, 0.40f), 0.35f, 0f);
            var wx = ox + 2.9f;
            Box("WindowBehind", room, new Vector3(wx, 1.75f, zFront + 0.03f), new Vector3(1.6f, 1.0f, 0.02f), corridor, false);
            Box("WindowGlass", room, new Vector3(wx, 1.75f, zFront - 0.02f), new Vector3(1.55f, 0.96f, 0.012f), glass, false);
            Box("WinFrameT", room, new Vector3(wx, 2.27f, zFront - 0.03f), new Vector3(1.7f, 0.07f, 0.07f), frameGrey, false);
            Box("WinFrameB", room, new Vector3(wx, 1.23f, zFront - 0.03f), new Vector3(1.7f, 0.07f, 0.07f), frameGrey, false);
            Box("WinFrameL", room, new Vector3(wx - 0.82f, 1.75f, zFront - 0.03f), new Vector3(0.07f, 1.05f, 0.07f), frameGrey, false);
            Box("WinFrameR", room, new Vector3(wx + 0.82f, 1.75f, zFront - 0.03f), new Vector3(0.07f, 1.05f, 0.07f), frameGrey, false);
            Sc08QuadWorld("WindowBlinds", room, new Vector3(wx, 1.9f, zFront - 0.05f), new Vector3(0f, 0f, 0f), new Vector2(1.5f, 0.75f),
                Sc06Label("Sc08Blinds", "Label_Sc08Blinds", true, 0.25f));

            // ceiling: ventilation duct, sprinkler heads
            var duct = Mat("Sc10Duct", new Color(0.78f, 0.80f, 0.82f), 0.6f, 0.4f);
            var ductPipe = Prim("DuctRun", PrimitiveType.Cylinder, room, new Vector3(ox - 0.2f, h - 0.3f, zFront - 0.45f), new Vector3(0.3f, 3.6f, 0.3f), duct);
            ductPipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var i = 0; i < 5; i++)
                Prim("DuctBand" + i, PrimitiveType.Cylinder, room, new Vector3(ox - 3.2f + i * 1.45f, h - 0.3f, zFront - 0.45f), new Vector3(0.33f, 0.03f, 0.33f), darkSteel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var i = 0; i < 4; i++)
            {
                var p = new Vector3(ox - 2.2f + i * 1.9f, h, 0.6f + (i % 2) * 1.2f);
                Cyl("SprinklerPipe" + i, room, new Vector3(p.x, h - 0.06f, p.z), 0.06f, 0.008f, steel);
                Sc06Disc("SprinklerDeflector" + i, room, new Vector3(p.x, h - 0.07f, p.z), 0.04f, 0.004f, steel);
                Sc06Disc("SprinklerCollar" + i, room, new Vector3(p.x, h - 0.008f, p.z), 0.05f, 0.012f, white);
            }

            // ------------------------------------------------------------------ real-lab dressing: wall tile dado, storage, notice board, clock, pipes, glassware
            {
                var dadoR = Mat("Sc10DadoTileR", Color.white, 0.8f, 0f, Sc06Tex("Label_Sc10Tile"));
                dadoR.SetTextureScale("_BaseMap", new Vector2((zFront - zBack) / 1.2f, 1f));
                var dadoF = Mat("Sc10DadoTileF", Color.white, 0.8f, 0f, Sc06Tex("Label_Sc10Tile"));
                dadoF.SetTextureScale("_BaseMap", new Vector2((x1 - x0) / 1.2f, 1f));
                var trimBlue = Mat("Sc10DadoTrim", new Color(0.18f, 0.32f, 0.5f), 0.5f, 0f);
                Box("DadoRight", room, new Vector3(x0 + 0.011f, 0.6f, (zBack + zFront) * 0.5f), new Vector3(0.022f, 1.2f, zFront - zBack), dadoR, false);
                Box("DadoRightTrim", room, new Vector3(x0 + 0.016f, 1.215f, (zBack + zFront) * 0.5f), new Vector3(0.032f, 0.03f, zFront - zBack), trimBlue, false);
                Box("DadoFront", room, new Vector3((x0 + x1) * 0.5f, 0.6f, zFront - 0.011f), new Vector3(x1 - x0, 1.2f, 0.022f), dadoF, false);
                Box("DadoFrontTrim", room, new Vector3((x0 + x1) * 0.5f, 1.215f, zFront - 0.016f), new Vector3(x1 - x0, 0.03f, 0.032f), trimBlue, false);

                // yellow flammable storage cabinet on the back wall left of the fume hood (built facing +X, then turned to face +Z)
                var cabY = Mat("Sc10CabinetYellow", new Color(0.93f, 0.74f, 0.08f), 0.5f, 0.1f);
                var cabRed = Mat("Sc10CabinetRed", new Color(0.72f, 0.07f, 0.06f), 0.5f, 0f);
                var cabX = ox - 3.3f;
                var cabZ = zBack + 0.30f;
                var cabRig = new GameObject("Sc10FlamCabinetRig").transform;
                cabRig.SetParent(room, false);
                Box("FlamCabinet", cabRig, new Vector3(cabX, 0.95f, cabZ), new Vector3(0.56f, 1.9f, 0.92f), cabY, false);
                Box("FlamBand", cabRig, new Vector3(cabX + 0.285f, 1.45f, cabZ), new Vector3(0.01f, 0.1f, 0.92f), cabRed, false);
                Box("FlamDoorSplit", cabRig, new Vector3(cabX + 0.286f, 0.95f, cabZ), new Vector3(0.008f, 1.8f, 0.012f), darkSteel, false);
                Box("FlamHandleA", cabRig, new Vector3(cabX + 0.30f, 0.95f, cabZ - 0.06f), new Vector3(0.025f, 0.12f, 0.02f), steel, false);
                Box("FlamHandleB", cabRig, new Vector3(cabX + 0.30f, 0.95f, cabZ + 0.06f), new Vector3(0.025f, 0.12f, 0.02f), steel, false);
                Box("FlamLabel", cabRig, new Vector3(cabX + 0.289f, 1.7f, cabZ), new Vector3(0.006f, 0.2f, 0.34f), white, false);
                Box("FlamLabelRed", cabRig, new Vector3(cabX + 0.2925f, 1.7f, cabZ), new Vector3(0.006f, 0.12f, 0.26f), cabRed, false);
                cabRig.RotateAround(new Vector3(cabX, 0f, cabZ), Vector3.up, -90f);

                // notice board with pinned papers, and a wall clock
                Box("BoardFrame", room, new Vector3(x0 + 0.02f, 1.55f, -0.83f), new Vector3(0.03f, 0.46f, 0.46f), darkSteel, false);
                Box("BoardCork", room, new Vector3(x0 + 0.037f, 1.55f, -0.83f), new Vector3(0.012f, 0.40f, 0.40f), Mat("Sc10Cork", new Color(0.62f, 0.45f, 0.28f), 0.2f, 0f), false);
                var paperCols = new[] { new Color(0.95f, 0.95f, 0.92f), new Color(0.95f, 0.85f, 0.4f), new Color(0.75f, 0.88f, 0.95f), new Color(0.95f, 0.95f, 0.92f) };
                for (var i = 0; i < 4; i++)
                    Box("BoardPaper" + i, room, new Vector3(x0 + 0.047f, 1.55f + (i % 2 - 0.5f) * 0.17f, -0.83f + (i / 2 - 0.5f) * 0.17f),
                        new Vector3(0.006f, 0.14f, 0.12f), Mat("Sc10Paper" + i, paperCols[i], 0.2f, 0f), false);
                var clock = Prim("WallClock", PrimitiveType.Cylinder, room, new Vector3(x0 + 0.04f, 2.45f, -0.83f), new Vector3(0.36f, 0.02f, 0.36f), Mat("Sc10ClockFace", new Color(0.95f, 0.95f, 0.93f), 0.3f, 0f));
                clock.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Box("ClockHandH", room, new Vector3(x0 + 0.062f, 2.47f, -0.83f), new Vector3(0.008f, 0.1f, 0.012f), darkSteel, false);
                Box("ClockHandM", room, new Vector3(x0 + 0.064f, 2.45f, -0.79f), new Vector3(0.008f, 0.012f, 0.14f), darkSteel, false);

                // pipes near the ceiling along the back wall
                var pipeRed = Mat("Sc10PipeRed", new Color(0.7f, 0.1f, 0.08f), 0.55f, 0.3f);
                for (var i = 0; i < 2; i++)
                {
                    var pipe = Prim("WallPipe" + i, PrimitiveType.Cylinder, room, new Vector3(ox - 1.9f, 2.95f - i * 0.14f, zBack + 0.08f + i * 0.02f), new Vector3(0.07f, 2.4f, 0.07f), i == 0 ? pipeRed : steel);
                    pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                // glassware and equipment on the front bench
                var glassB = Sc06GlassMat("Sc10Beaker", new Color(0.8f, 0.9f, 0.92f), 0.2f, 0.9f);
                var liquids = new[] { new Color(0.25f, 0.5f, 0.85f), new Color(0.9f, 0.7f, 0.2f), new Color(0.3f, 0.7f, 0.4f) };
                for (var i = 0; i < 5; i++)
                {
                    var bxg = ox - 3.2f + i * 0.4f;
                    var bzg = zFront - 0.3f;
                    if (i % 2 == 0)
                    {
                        Prim("Beaker" + i, PrimitiveType.Cylinder, room, new Vector3(bxg, 0.97f, bzg), new Vector3(0.09f, 0.05f, 0.09f), glassB);
                        Prim("BeakerLiquid" + i, PrimitiveType.Cylinder, room, new Vector3(bxg, 0.94f, bzg), new Vector3(0.082f, 0.03f, 0.082f), Mat("Sc10Liquid" + i, liquids[i / 2 % 3], 0.8f, 0f));
                    }
                    else
                    {
                        var fl = Sc06Lathe("Sc10FlaskMesh", new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.065f, 0.02f), new Vector2(0.03f, 0.13f), new Vector2(0.018f, 0.17f), new Vector2(0.02f, 0.2f) }, 0.003f, 24);
                        Sc08Part("Flask" + i, room, fl, glassB, new Vector3(bxg, 0.92f, bzg));
                    }
                }

                Box("BalanceBase", room, new Vector3(ox - 0.65f, 0.95f, zFront - 0.32f), new Vector3(0.3f, 0.09f, 0.34f), white, false);
            }

            // floor wear, away from the spill
            var stain = MatAlpha("Sc08Stain", Sc06Tex("Label_Sc08Stain"), new Color(1f, 1f, 1f, 0.45f), false);
            var spots = new[]
            {
                new Vector4(ox - 1.2f, 1.0f, 2.2f, 1.6f), new Vector4(ox + 0.6f, -1.2f, 1.8f, 1.2f),
                new Vector4(ox - 2.8f, 0.4f, 1.6f, 1.6f), new Vector4(ox - 0.4f, 2.4f, 2.4f, 1.0f)
            };
            for (var i = 0; i < spots.Length; i++)
            {
                var q = QuadObj("FloorWearLab" + i, null, new Vector3(spots[i].x, 0.003f + i * 0.0003f, spots[i].y), new Vector3(90f, i * 63f, 0f), new Vector2(spots[i].z, spots[i].w), stain);
                q.transform.SetParent(room, true);
                Object.DestroyImmediate(q.GetComponent<Collider>());
            }
        }

        // A baked reflection probe so the tiles, steel and glass reflect the room.
        private static void Sc10BakeProbe(Transform env1, Transform env10)
        {
            try
            {
                var room = env10.Find("Room");
                var go = new GameObject("Sc10ReflectionProbe");
                go.transform.SetParent(room, false);
                go.transform.position = new Vector3(Sc10Ox - 0.3f, 1.5f, 0.2f);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = 128;
                probe.size = new Vector3(8.6f, 3.3f, 6.6f);
                probe.center = new Vector3(0f, 0.1f, 0f);
                probe.boxProjection = true;
                probe.intensity = 1f;
                probe.importance = 1;
                var path = GenDir + "/Sc10Probe.exr";
                var envWasActive = env1.gameObject.activeSelf;
                env1.gameObject.SetActive(false);
                env10.gameObject.SetActive(true);
                var ok = UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
                env1.gameObject.SetActive(envWasActive);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else
                {
                    Debug.LogWarning("[SC10] reflection probe bake failed, the room uses the default reflections");
                    Object.DestroyImmediate(go);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SC10] reflection probe skipped: " + e.Message);
            }
        }
    }
}
#endif

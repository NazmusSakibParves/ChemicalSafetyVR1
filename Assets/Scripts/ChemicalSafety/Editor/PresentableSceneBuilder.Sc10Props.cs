#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 10 spill bay: the red SPILL KIT with a glass door and its contents, the broken bottle and the clear puddle, the floor
    /// drain, the yellow CAUTION - SPILL sign, the steel desk with the SDS paper, the hazardous waste bin and the waste bag.
    /// The wall that carries it is at x = x1 (the viewer faces +X there, so the viewer's left is +Z).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        // A pillow-shaped bag, front toward +Z, flat crimps at the top and bottom. Origin = bottom centre.
        private static Mesh Sc10BagMesh(string name, float w, float h, float bulge)
        {
            const int nx = 12, ny = 16;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var j = 0; j <= ny; j++)
            {
                var v = j / (float)ny;
                for (var i = 0; i <= nx; i++)
                {
                    var u = i / (float)nx;
                    var edge = 1f - Mathf.Pow(Mathf.Abs(u * 2f - 1f), 2f);
                    var body = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01((v - 0.05f) / 0.9f) * Mathf.PI));
                    var z = bulge * Mathf.Pow(body, 0.7f) * edge + 0.004f * Mathf.Sin(u * 38f + v * 5f) * edge;
                    // the sides pinch a little toward the crimps
                    var x = (u - 0.5f) * w * (0.93f + 0.07f * body);
                    verts.Add(new Vector3(x, v * h, z));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (var j = 0; j < ny; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var a = j * (nx + 1) + i;
                    var c = a + nx + 1;
                    tris.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            }

            return Sc08SaveMesh(name, verts, uvs, tris);
        }

        // Bag = front and back shell, front toward the local +Z.
        private static Transform Sc10Bag(Transform parent, string name, Vector3 localPos, float yaw, float w, float h, float bulge,
            Material front, Material back, string meshTag)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var mesh = Sc10BagMesh("Sc10Bag_" + meshTag, w, h, bulge);
            Sc08Part("Front", root, mesh, front, Vector3.zero);
            Sc08Part("Back", root, mesh, back, Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
            return root;
        }

        // Dustpan, origin = base centre, opening toward +Z, handle toward -Z.
        private static Transform Sc10PanObject(Transform parent, string name, Vector3 localPos, float yaw, float scale)
        {
            var mat = Mat("Sc10PlasticDark", new Color(0.20f, 0.22f, 0.24f), 0.55f, 0f);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            Sc08LocalBox("PanBase", root, new Vector3(0f, 0.006f, 0f), new Vector3(0.26f, 0.012f, 0.22f), mat);
            Sc08LocalBox("PanBack", root, new Vector3(0f, 0.04f, -0.11f), new Vector3(0.26f, 0.07f, 0.012f), mat);
            Sc08LocalBox("PanSideL", root, new Vector3(0.124f, 0.026f, 0f), new Vector3(0.012f, 0.045f, 0.22f), mat);
            Sc08LocalBox("PanSideR", root, new Vector3(-0.124f, 0.026f, 0f), new Vector3(0.012f, 0.045f, 0.22f), mat);
            var handle = Sc08LocalBox("PanHandle", root, new Vector3(0f, 0.085f, -0.2f), new Vector3(0.035f, 0.014f, 0.2f), mat);
            handle.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            Sc08LocalBox("PanGrip", root, new Vector3(0f, 0.13f, -0.285f), new Vector3(0.045f, 0.02f, 0.07f), mat).transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            return root;
        }

        // Hand brush, origin = head centre, bristles down, handle toward -Z.
        private static Transform Sc10BrushObject(Transform parent, string name, Vector3 localPos, float yaw, float scale)
        {
            var dark = Mat("Sc10PlasticDark", new Color(0.20f, 0.22f, 0.24f), 0.55f, 0f);
            var bristle = Mat("Sc10Bristle", new Color(0.62f, 0.54f, 0.38f), 0.2f, 0f);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            Sc08LocalBox("BrushHead", root, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.022f, 0.22f), dark);
            Sc08LocalBox("Bristles", root, new Vector3(0f, -0.025f, 0f), new Vector3(0.042f, 0.05f, 0.205f), bristle);
            var handle = Sc08LocalBox("BrushHandle", root, new Vector3(0f, 0.04f, -0.01f), new Vector3(0.028f, 0.02f, 0.02f), dark);
            handle.transform.localScale = new Vector3(0.028f, 0.03f, 0.03f);
            var stick = Sc08LocalBox("BrushStick", root, new Vector3(0f, 0.12f, -0.01f), new Vector3(0.026f, 0.2f, 0.026f), dark);
            stick.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            return root;
        }

        private static void Sc10AddPoke(Transform item, Vector3 worldPad, float reach)
        {
            var b = BoundsOfTree(item);
            var col = item.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = item.InverseTransformPoint(b.center + worldPad * 0.5f);
            var sz = item.InverseTransformVector(new Vector3(b.size.x + Mathf.Abs(worldPad.x), b.size.y + 0.06f, b.size.z + 0.06f));
            col.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            var poke = item.gameObject.AddComponent<PokeTarget>();
            var pso = new SerializedObject(poke);
            pso.FindProperty("reach").floatValue = reach;
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Sc10Bay(Transform room, float ox, float x1, float zBack, float zFront, float h, PresentableArt art, Sc10Refs refs)
        {
            var bay = new GameObject("Sc10Bay").transform;
            bay.SetParent(room, false);

            var red = Mat("Sc10Red", new Color(0.74f, 0.07f, 0.05f), 0.74f, 0f);
            var redIn = Mat("Sc10RedInside", new Color(0.52f, 0.05f, 0.04f), 0.4f, 0f);
            var steel = Mat("Sc06SteelPaint", new Color(0.72f, 0.74f, 0.76f), 0.55f, 0f);
            var darkSteel = Mat("Sc06DarkSteel", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0f);
            var yellow = Mat("Sc10Yellow", new Color(0.95f, 0.78f, 0.06f), 0.45f, 0f);
            var glass = Sc06GlassMat("Sc10DoorGlass", new Color(0.86f, 0.93f, 0.96f), 0.03f, 0.6f);
            var plasticGrey = Mat("Sc10PadGrey", new Color(0.60f, 0.62f, 0.63f), 0.15f, 0f);
            var padMat = Mat("Sc10PadMat", Color.white, 0.1f, 0f, Sc06Tex("Label_Sc10Pad"));
            padMat.SetTextureScale("_BaseMap", new Vector2(1.6f, 1.3f));
            var sockMat = Mat("Sc10SockMat", Color.white, 0.12f, 0f, Sc06Tex("Label_Sc10Sock"));
            sockMat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));

            // ------------------------------------------------------------------ the red cabinet
            const float kz = 0.35f, kitY = 1.45f, kitW = 1.25f, kitH = 0.95f, kitD = 0.2f;
            var fx = x1 - kitD;
            Box("KitBack", bay, new Vector3(x1 - 0.015f, kitY, kz), new Vector3(0.03f, kitH, kitW), redIn, false);
            Box("KitTop", bay, new Vector3(x1 - kitD * 0.5f, kitY + kitH * 0.5f - 0.035f, kz), new Vector3(kitD, 0.07f, kitW), red, false);
            Box("KitBottom", bay, new Vector3(x1 - kitD * 0.5f, kitY - kitH * 0.5f + 0.035f, kz), new Vector3(kitD, 0.07f, kitW), red, false);
            Box("KitSideL", bay, new Vector3(x1 - kitD * 0.5f, kitY, kz + kitW * 0.5f - 0.03f), new Vector3(kitD, kitH, 0.06f), red, false);
            Box("KitSideR", bay, new Vector3(x1 - kitD * 0.5f, kitY, kz - kitW * 0.5f + 0.03f), new Vector3(kitD, kitH, 0.06f), red, false);
            Box("KitShelf", bay, new Vector3(x1 - kitD * 0.5f + 0.01f, kitY, kz), new Vector3(kitD - 0.02f, 0.03f, kitW - 0.1f), red, false);
            Box("KitDivider", bay, new Vector3(x1 - kitD * 0.5f + 0.01f, kitY - kitH * 0.25f, kz), new Vector3(kitD - 0.02f, kitH * 0.46f, 0.02f), red, false);
            Box("KitHeader", bay, new Vector3(x1 - kitD * 0.5f, kitY + kitH * 0.5f + 0.05f, kz), new Vector3(kitD, 0.1f, kitW), red, false);
            QuadObj("KitTitle", bay, new Vector3(fx - 0.003f, kitY + kitH * 0.5f + 0.05f, kz), new Vector3(0f, 90f, 0f), new Vector2(0.64f, 0.08f),
                Sc06Label("Sc10SpillKitTitleMat", "Label_Sc10SpillKitTitle", true, 0.3f));
            Box("KitHingeA", bay, new Vector3(fx - 0.012f, kitY + 0.30f, kz + kitW * 0.5f - 0.005f), new Vector3(0.02f, 0.07f, 0.014f), steel, false);
            Box("KitHingeB", bay, new Vector3(fx - 0.012f, kitY - 0.30f, kz + kitW * 0.5f - 0.005f), new Vector3(0.02f, 0.07f, 0.014f), steel, false);
            Box("KitLatch", bay, new Vector3(fx - 0.016f, kitY, kz - kitW * 0.5f + 0.02f), new Vector3(0.02f, 0.08f, 0.02f), steel, false);

            // glass door: hinged on the +Z edge, swings toward the room
            var doorPivot = new GameObject("Sc10KitDoor").transform;
            doorPivot.SetParent(bay, false);
            doorPivot.position = new Vector3(fx - 0.008f, kitY, kz + kitW * 0.5f - 0.03f);
            var dw = kitW - 0.06f;
            Sc08LocalBox("DoorGlass", doorPivot, new Vector3(0f, 0f, -dw * 0.5f), new Vector3(0.006f, kitH - 0.1f, dw - 0.05f), glass);
            Sc08LocalBox("DoorTop", doorPivot, new Vector3(0f, (kitH - 0.1f) * 0.5f, -dw * 0.5f), new Vector3(0.016f, 0.03f, dw), red);
            Sc08LocalBox("DoorBottom", doorPivot, new Vector3(0f, -(kitH - 0.1f) * 0.5f, -dw * 0.5f), new Vector3(0.016f, 0.03f, dw), red);
            Sc08LocalBox("DoorHingeSide", doorPivot, new Vector3(0f, 0f, -0.015f), new Vector3(0.016f, kitH - 0.1f, 0.03f), red);
            Sc08LocalBox("DoorLatchSide", doorPivot, new Vector3(0f, 0f, -dw + 0.015f), new Vector3(0.016f, kitH - 0.1f, 0.03f), red);

            var kitPoke = Sc08Poke("Sc10KitPoke", bay, new Vector3(fx - 0.12f, kitY, kz), new Vector3(0.3f, kitH, kitW), 0.06f);

            // ------------------------------------------------------------------ items (viewer faces +X: left = +Z, so z decreases to the right)
            var topY = kitY + 0.015f;
            var botY = kitY - kitH * 0.5f + 0.07f;
            var ix = x1 - 0.1f;

            // 0 absorbent sock, rolled up
            var sock = new GameObject("Sc10Item0").transform;
            sock.SetParent(bay, false);
            sock.position = new Vector3(ix, topY + 0.105f, 0.20f);
            sock.rotation = Quaternion.Euler(0f, 270f, 0f);
            var spiral = new List<Vector3>();
            for (var i = 0; i <= 90; i++)
            {
                var a = i / 90f * Mathf.PI * 2f * 2.6f;
                var r = 0.052f + a / (Mathf.PI * 2f) * 0.026f;
                spiral.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, (a / (Mathf.PI * 2f) - 1.3f) * 0.045f));
            }

            Sc08Part("SockCoil", sock, Sc08SweepTube("Sc10SockCoil", spiral, 0.027f, 10, 0.9f), sockMat, Vector3.zero);
            Sc10AddPoke(sock, new Vector3(-0.12f, 0f, 0f), 0.07f);

            // 1 soda ash bag
            var sodaFront = Mat("Sc10SodaAshMat", Color.white, 0.35f, 0f, Sc06Tex("Label_Sc10SodaAsh"));
            var sodaBack = Mat("Sc10SodaAshBack", new Color(0.92f, 0.92f, 0.90f), 0.35f, 0f);
            var soda = Sc10Bag(bay, "Sc10Item1", new Vector3(ix, topY, 0.80f), 270f, 0.27f, 0.40f, 0.07f, sodaFront, sodaBack, "Soda");
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(soda, false);
            mouth.localPosition = new Vector3(0f, 0.40f, 0f);
            Sc10AddPoke(soda, new Vector3(-0.12f, 0f, 0f), 0.07f);

            // 2 absorbent pads (stack)
            var pads = new GameObject("Sc10Item2").transform;
            pads.SetParent(bay, false);
            pads.position = new Vector3(ix, topY + 0.15f, 0.50f);
            pads.rotation = Quaternion.Euler(0f, 270f, 0f);
            for (var i = 0; i < 6; i++)
            {
                var sheet = Sc08LocalBox("Sheet" + i, pads, new Vector3(0f, 0f, -0.035f + i * 0.014f + (i % 2) * 0.002f), new Vector3(0.30f, 0.30f - (i % 3) * 0.004f, 0.012f), i == 5 ? padMat : plasticGrey);
                sheet.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 2.5f) * 0.6f);
            }

            Sc10AddPoke(pads, new Vector3(-0.12f, 0f, 0f), 0.07f);

            // 3 dustpan and brush
            var tools = new GameObject("Sc10Item3").transform;
            tools.SetParent(bay, false);
            tools.position = new Vector3(ix, botY, 0.82f);
            tools.rotation = Quaternion.identity;
            var panShelf = Sc10PanObject(tools, "PanShelf", Vector3.zero, 270f, 1f);
            panShelf.localRotation = Quaternion.Euler(-72f, 270f, 0f);
            panShelf.localPosition = new Vector3(0f, 0.12f, 0f);
            var brushShelf = Sc10BrushObject(tools, "BrushShelf", new Vector3(-0.015f, 0.1f, -0.1f), 270f, 1f);
            brushShelf.localRotation = Quaternion.Euler(-72f, 270f, 0f);
            Sc10AddPoke(tools, new Vector3(-0.12f, 0f, 0f), 0.07f);

            // 4 hazardous waste bag, folded
            var bagYellow = Mat("Sc10BagYellow", new Color(0.94f, 0.76f, 0.06f), 0.55f, 0f);
            var bagFront = Mat("Sc10WasteBagMat", Color.white, 0.5f, 0f, Sc06Tex("Label_Sc10WasteBag"));
            var wasteBag = Sc10Bag(bay, "Sc10Item4", new Vector3(ix, botY, 0.52f), 270f, 0.25f, 0.30f, 0.05f, bagFront, bagYellow, "Waste");
            Sc10AddPoke(wasteBag, new Vector3(-0.12f, 0f, 0f), 0.07f);

            // decor in the kit: vermiculite bag, hazard tag, goggles, gloves, face shield, green apron roll
            var verFront = Mat("Sc10VermiculiteMat", Color.white, 0.25f, 0f, Sc06Tex("Label_Sc10Vermiculite"));
            Sc10Bag(bay, "VermiculiteBag", new Vector3(ix, topY, -0.10f), 270f, 0.27f, 0.38f, 0.07f, verFront, Mat("Sc10VermBack", new Color(0.72f, 0.64f, 0.46f), 0.2f, 0f), "Verm");
            Box("HazTagBack", bay, new Vector3(x1 - 0.045f, botY + 0.20f, 0.28f), new Vector3(0.012f, 0.27f, 0.2f), yellow, false);
            QuadObj("HazTag", bay, new Vector3(x1 - 0.053f, botY + 0.20f, 0.28f), new Vector3(0f, 90f, 0f), new Vector2(0.2f, 0.27f),
                Sc06Label("Sc10HazTagMat", "Label_Sc10HazTag", false, 0.3f));
            var ppeHolder = new GameObject("KitPpe").transform;
            ppeHolder.SetParent(bay, false);
            ppeHolder.position = new Vector3(ix, botY, 0f);
            ppeHolder.rotation = Quaternion.Euler(0f, 270f, 0f);
            Sc08Goggles(ppeHolder, "KitGoggles", new Vector3(-0.0f, 0.2f, 0f), 0.95f, Quaternion.identity);
            Sc08FaceShield(ppeHolder, "KitShield", new Vector3(-0.2f, 0.12f, 0f), 0.9f, Quaternion.identity);
            Sc08GlovePair(ppeHolder, new Vector3(0.14f, 0.12f, 0.0f));
            var green = Mat("Sc10GreenBag", new Color(0.10f, 0.52f, 0.30f), 0.55f, 0f);
            Sc08LocalBox("GreenRoll", ppeHolder, new Vector3(-0.2f, 0.025f, 0.0f), new Vector3(0.24f, 0.05f, 0.2f), green);

            // ------------------------------------------------------------------ the spill on the floor
            var c = new Vector3(x1 - 1.3f, 0f, 1.3f);
            var spill = new GameObject("Sc10Spill").transform;
            spill.SetParent(bay, false);
            spill.position = c;
            var puddleMat = MatAlpha("Sc10PuddleMat", Sc06Tex("Label_Sc10Puddle"), new Color(0.68f, 0.74f, 0.79f, 1f), false);
            var puddle = QuadObj("Sc10Puddle", bay, c + new Vector3(0f, 0.004f, 0f), new Vector3(90f, 90f, 0f), new Vector2(1.65f, 1.95f), puddleMat);
            puddle.GetComponent<MeshRenderer>().sharedMaterial = puddleMat;
            var powderMat = MatAlpha("Sc10PowderDecalMat", Sc06Tex("Label_Sc10Powder"), new Color(1f, 1f, 1f, 0.85f), false);
            var powderDecal = QuadObj("Sc10PowderDecal", bay, c + new Vector3(0f, 0.007f, 0f), new Vector3(90f, 90f, 0f), new Vector2(1.1f, 0.8f), powderMat);
            powderDecal.SetActive(false);
            var stainMat = MatAlpha("Sc10StainMat", Sc06Tex("Label_Sc10Stain"), new Color(1f, 1f, 1f, 0.55f), false);
            QuadObj("Sc10StainA", bay, new Vector3(x1 - 2.5f, 0.003f, 2.3f), new Vector3(90f, 20f, 0f), new Vector2(0.9f, 0.9f), stainMat);
            QuadObj("Sc10StainB", bay, new Vector3(x1 - 2.9f, 0.003f, 0.3f), new Vector3(90f, -35f, 0f), new Vector2(0.7f, 0.7f), stainMat);
            QuadObj("Sc10StainC", bay, new Vector3(x1 - 1.1f, 0.003f, 2.9f), new Vector3(90f, 80f, 0f), new Vector2(0.6f, 0.6f), stainMat);

            // broken bottle: jagged body and neck lying on the floor, plus loose shards
            var bottleGlass = Sc06GlassMat("Sc10BottleGlass", new Color(0.88f, 0.94f, 0.96f), 0.03f, 0.7f);
            var body = Sc09JaggedShell("Sc10BottleBody", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.046f, 0.012f), new Vector2(0.049f, 0.06f), new Vector2(0.047f, 0.12f), new Vector2(0.041f, 0.15f)
            }, 28, 0.016f, true);
            Sc08Part("Sc10Bottle0", bay, body, bottleGlass, c + new Vector3(-0.30f, 0.05f, 0.10f), Quaternion.Euler(0f, 28f, 90f));
            var neck = Sc06Lathe("Sc10BottleNeck", new[]
            {
                new Vector2(0.023f, 0f), new Vector2(0.021f, 0.03f), new Vector2(0.0175f, 0.06f), new Vector2(0.0175f, 0.085f), new Vector2(0.021f, 0.09f), new Vector2(0.0225f, 0.1f)
            }, 0.0025f, 20);
            Sc08Part("Sc10Bottle1", bay, neck, bottleGlass, c + new Vector3(0.04f, 0.023f, -0.2f), Quaternion.Euler(0f, -70f, 90f));
            var rnd = new System.Random(310);
            for (var i = 0; i < 16; i++)
            {
                var ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var rr = 0.12f + (float)rnd.NextDouble() * 0.45f;
                var pos = c + new Vector3(Mathf.Cos(ang) * rr * 1.15f - 0.1f, 0.017f, Mathf.Sin(ang) * rr * 0.8f);
                var sz = 0.018f + (float)rnd.NextDouble() * 0.028f;
                Sc08Part("Sc10Shard" + i, bay, Sc09ShardMesh("Sc10Shard" + (i % 6), 40 + i % 6, sz), bottleGlass, pos,
                    Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
            }

            // pads laid on the spill (hidden until ABSORB)
            for (var i = 0; i < 4; i++)
            {
                var p = Box("Sc10Pad" + i, bay, c + new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.012f, 0.5f), padMat, false);
                var pmr = p.GetComponent<MeshRenderer>();
                pmr.shadowCastingMode = ShadowCastingMode.Off;
                pmr.receiveShadows = false;
                p.SetActive(false);
            }

            // dustpan and brush used for the sweep (hidden until CLEAN-UP GLASS)
            var pan = Sc10PanObject(bay, "Sc10Pan", c + new Vector3(-1.2f, 0.01f, 0.1f), 90f, 1.15f);
            pan.gameObject.SetActive(false);
            var brush = Sc10BrushObject(bay, "Sc10Brush", c + new Vector3(0f, 0.2f, 0f), 90f, 1.15f);
            brush.gameObject.SetActive(false);
            Marker("Sc10PanPoint", bay, c + new Vector3(-1.2f, 0.02f, 0.1f), 90f);

            // ------------------------------------------------------------------ floor drain along the wall
            var drainX = x1 - 0.55f;
            Box("DrainFrame", bay, new Vector3(drainX, 0.004f, 0.5f), new Vector3(0.24f, 0.008f, 1.34f), darkSteel, false);
            Box("DrainSump", bay, new Vector3(drainX, 0.0045f, 0.5f), new Vector3(0.19f, 0.008f, 1.28f), Mat("Sc10DrainDark", new Color(0.03f, 0.03f, 0.035f), 0.2f, 0f), false);
            for (var i = 0; i < 24; i++)
                Box("DrainSlat" + i, bay, new Vector3(drainX, 0.011f, 0.5f - 0.62f + i * 0.054f), new Vector3(0.18f, 0.012f, 0.016f), steel, false);

            // ------------------------------------------------------------------ CAUTION - SPILL A-frame sign (two leaning panels)
            var sx = x1 - 0.95f;
            var sz2 = 0.45f;
            var cautionMat = Sc06Label("Sc10CautionMat", "Label_Sc10Caution", false, 0.4f);
            var gA = new GameObject("SignFront").transform;
            gA.SetParent(bay, false);
            gA.position = new Vector3(sx - 0.055f, 0.31f, sz2);
            gA.rotation = Quaternion.Euler(0f, 0f, -10f);
            Sc08LocalBox("Panel", gA, Vector3.zero, new Vector3(0.012f, 0.62f, 0.31f), yellow);
            var qa = QuadObj("Face", gA, new Vector3(-0.0066f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector2(0.30f, 0.60f), cautionMat);
            qa.GetComponent<MeshRenderer>().sharedMaterial = cautionMat;
            var gB = new GameObject("SignBack").transform;
            gB.SetParent(bay, false);
            gB.position = new Vector3(sx + 0.055f, 0.31f, sz2);
            gB.rotation = Quaternion.Euler(0f, 0f, 10f);
            Sc08LocalBox("Panel", gB, Vector3.zero, new Vector3(0.012f, 0.62f, 0.31f), yellow);
            QuadObj("Face", gB, new Vector3(0.0066f, 0f, 0f), new Vector3(0f, -90f, 0f), new Vector2(0.30f, 0.60f), cautionMat);

            // ------------------------------------------------------------------ steel desk with the SDS paper (back left, as in the picture)
            var dx = x1 - 0.5f;
            var dz = 2.95f;
            Box("DeskTop", bay, new Vector3(dx, 0.9f, dz), new Vector3(0.7f, 0.04f, 0.72f), steel, false);
            Box("DeskSide", bay, new Vector3(dx - 0.33f, 0.45f, dz), new Vector3(0.025f, 0.86f, 0.7f), steel, false);
            Box("DeskLegA", bay, new Vector3(dx + 0.31f, 0.45f, dz + 0.32f), new Vector3(0.04f, 0.86f, 0.04f), darkSteel, false);
            Box("DeskLegB", bay, new Vector3(dx + 0.31f, 0.45f, dz - 0.32f), new Vector3(0.04f, 0.86f, 0.04f), darkSteel, false);
            Box("DeskBrace", bay, new Vector3(dx - 0.1f, 0.15f, dz), new Vector3(0.5f, 0.02f, 0.7f), darkSteel, false);
            QuadObj("SdsPaper", bay, new Vector3(dx - 0.06f, 0.923f, dz), new Vector3(90f, 105f, 0f), new Vector2(0.30f, 0.42f),
                Sc06Label("Sc10PaperMat", "Label_Sc10Paper", false, 0.2f));

            // ------------------------------------------------------------------ hazardous waste bin with a hinged lid
            var bx = x1 - 0.55f;
            var bz = 2.2f;
            var binMat = Mat("Sc10BinYellow", new Color(0.95f, 0.76f, 0.05f), 0.5f, 0f);
            var binIn = Mat("Sc10BinInside", new Color(0.12f, 0.10f, 0.03f), 0.2f, 0f);
            var bin = new GameObject("Sc10Bin").transform;
            bin.SetParent(bay, false);
            Box("BinBottom", bin, new Vector3(bx, 0.02f, bz), new Vector3(0.5f, 0.04f, 0.5f), binMat, false);
            Box("BinInner", bin, new Vector3(bx, 0.045f, bz), new Vector3(0.45f, 0.01f, 0.45f), binIn, false);
            Box("BinWallFront", bin, new Vector3(bx - 0.2375f, 0.39f, bz), new Vector3(0.025f, 0.78f, 0.5f), binMat, false);
            Box("BinWallBack", bin, new Vector3(bx + 0.2375f, 0.39f, bz), new Vector3(0.025f, 0.78f, 0.5f), binMat, false);
            Box("BinWallL", bin, new Vector3(bx, 0.39f, bz + 0.2375f), new Vector3(0.5f, 0.78f, 0.025f), binMat, false);
            Box("BinWallR", bin, new Vector3(bx, 0.39f, bz - 0.2375f), new Vector3(0.5f, 0.78f, 0.025f), binMat, false);
            Box("BinRim", bin, new Vector3(bx - 0.2375f, 0.78f, bz), new Vector3(0.04f, 0.02f, 0.52f), binMat, false);
            QuadObj("BinLabel", bay, new Vector3(bx - 0.2515f, 0.40f, bz), new Vector3(0f, 90f, 0f), new Vector2(0.34f, 0.425f),
                Sc06Label("Sc10BinLabelMat", "Label_Sc10BinLabel", false, 0.4f));
            var lid = new GameObject("Sc10BinLid").transform;
            lid.SetParent(bay, false);
            lid.position = new Vector3(bx + 0.26f, 0.8f, bz);
            lid.rotation = Quaternion.Euler(0f, 270f, 0f);
            Sc08LocalBox("LidSlab", lid, new Vector3(0f, 0.012f, 0.265f), new Vector3(0.54f, 0.03f, 0.53f), binMat);
            Sc08LocalBox("LidHandle", lid, new Vector3(0f, 0.04f, 0.45f), new Vector3(0.22f, 0.025f, 0.04f), darkSteel);
            Marker("Sc10BinPoint", bay, new Vector3(bx, 0.05f, bz), 0f);

            // open bag (hidden until DISPOSE): body, flared neck, a filling mound
            var bagBody = Sc06Lathe("Sc10BagBody", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.17f, 0.05f), new Vector2(0.18f, 0.25f), new Vector2(0.155f, 0.4f), new Vector2(0.11f, 0.45f)
            }, 0.003f, 28);
            var bagOpen = new GameObject("Sc10BagOpen").transform;
            bagOpen.SetParent(bay, false);
            bagOpen.rotation = Quaternion.Euler(0f, 270f, 0f);
            var bagTwo = Mat("Sc10BagYellow", new Color(0.94f, 0.76f, 0.06f), 0.55f, 0f);
            Sc08TwoSided(bagTwo);
            Sc08Part("Body", bagOpen, bagBody, bagTwo, Vector3.zero);
            var neckGo = new GameObject("Sc10BagNeck").transform;
            neckGo.SetParent(bagOpen, false);
            neckGo.localPosition = new Vector3(0f, 0.45f, 0f);
            var neckMesh = Sc06Lathe("Sc10BagNeckMesh", new[] { new Vector2(0.11f, 0f), new Vector2(0.13f, 0.05f), new Vector2(0.17f, 0.12f) }, 0.003f, 28);
            Sc08Part("NeckMesh", neckGo, neckMesh, bagTwo, Vector3.zero);
            var fillGo = new GameObject("Sc10BagFill").transform;
            fillGo.SetParent(bagOpen, false);
            fillGo.localPosition = new Vector3(0f, 0.2f, 0f);
            Prim("Mound", PrimitiveType.Sphere, fillGo, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.3f, 0.3f), plasticGrey);
            fillGo.localScale = new Vector3(1f, 0.4f, 1f);
            var bagLabel = QuadObj("Label", bagOpen, new Vector3(0f, 0.24f, 0.186f), new Vector3(0f, 180f, 0f), new Vector2(0.22f, 0.29f),
                Sc06Label("Sc10WasteBagLabelMat", "Label_Sc10WasteBag", false, 0.5f));
            bagLabel.GetComponent<MeshRenderer>().sharedMaterial = bagLabel.GetComponent<MeshRenderer>().sharedMaterial;
            bagOpen.gameObject.SetActive(false);

            // ------------------------------------------------------------------ logic host
            var logicGo = new GameObject("Sc10SpillLogic");
            logicGo.transform.SetParent(room, false);
            var spillLogic = logicGo.AddComponent<Sc10Spill>();
            var sfx = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc09/";
            var sfx10 = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc10/";
            AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var so = new SerializedObject(spillLogic);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("sockMaterial").objectReferenceValue = sockMat;
            so.FindProperty("powderMaterial").objectReferenceValue = ParticleMat("Sc10PowderFxMat", Sc06Tex("Label_Sc10Powder"), false);
            so.FindProperty("fizzMaterial").objectReferenceValue = ParticleMat("Sc10FizzFxMat", Sc06Tex("Fx_Drop"), false);
            so.FindProperty("thudClip").objectReferenceValue = Clip(sfx + "Flask_Knock.wav");
            so.FindProperty("glassClip").objectReferenceValue = Clip(sfx + "Glass_Break.wav");
            so.FindProperty("fizzClip").objectReferenceValue = Clip(sfx10 + "Fizz.wav");
            so.FindProperty("powderClip").objectReferenceValue = Clip(sfx10 + "Powder.wav");
            so.FindProperty("sweepClip").objectReferenceValue = Clip(sfx10 + "Sweep.wav");
            so.FindProperty("rustleClip").objectReferenceValue = Clip(sfx10 + "Rustle.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            refs.spill = spillLogic;

            refs.spillStand = Marker("Stand_SC10_Spill", room, new Vector3(x1 - 3.3f, 0f, 1.3f), 90f);
            refs.kitStand = Marker("Stand_SC10_Kit", room, new Vector3(x1 - 0.95f, 0f, 0.45f), 90f);
            refs.disposeStand = Marker("Stand_SC10_Dispose", room, new Vector3(x1 - 2.2f, 0f, 2.2f), 90f);
        }
    }
}
#endif

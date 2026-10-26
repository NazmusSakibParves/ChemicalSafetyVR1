#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 8: the PPE Donning Station (white wall board, four recessed slots, items on hooks) and the PPE models.</summary>
    public static partial class PresentableSceneBuilder
    {
        // a flat disc whose axis points along Z (a lens, a rim)
        private static GameObject Sc08Lens(string name, Transform parent, Vector3 at, float diameter, float thick, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, thick * 0.5f, diameter);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ---------------------------------------------------------------- the PPE models (all built facing +Z in their own local space)

        private static List<Vector3> Sc08ArcPath(float r, float y, float fromDeg, float toDeg, float zShift, int steps)
        {
            var pts = new List<Vector3>();
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                pts.Add(new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r - r + zShift));
            }

            return pts;
        }

        // Wrap-around splash goggles, about 17 cm wide: curved clear lens, thin rubber rims, soft foam seal behind, indirect vents,
        // nose bridge, elastic strap with side buckles.
        private static Transform Sc08Goggles(Transform parent, string name, Vector3 localPos, float scale, Quaternion localRot)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = localRot;
            root.localScale = Vector3.one * scale;
            var rubber = Mat("Sc08GoggleRim", new Color(0.07f, 0.08f, 0.09f), 0.55f, 0f);
            var foam = Mat("Sc08GoggleFoam", new Color(0.14f, 0.15f, 0.16f), 0.12f, 0f);
            var lens = Sc06GlassMat("Sc08GoggleLens", new Color(0.86f, 0.95f, 0.98f), 0.05f, 0.9f);
            var strapMat = Mat("Sc08GoggleStrap", new Color(0.05f, 0.05f, 0.06f), 0.2f, 0f);
            var steel = Mat("Sc08Buckle", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.3f);
            var vent = Mat("Sc08GoggleVent", new Color(0.55f, 0.58f, 0.62f), 0.45f, 0f);
            const float r = 0.082f;
            Sc08Part("GoggleLens", root, Sc08ArcShell("Sc08GoggleLensM", r, 0.058f, 156f, 0.0025f, 36), lens, Vector3.zero);
            foreach (var sy in new[] { -1f, 1f })
            {
                Sc08Part("GoggleRim", root, Sc08SweepTube("Sc08GoggleRimM" + (sy > 0 ? "T" : "B"), Sc08ArcPath(r + 0.002f, sy * 0.030f, -82f, 82f, 0f, 24), 0.0042f, 8),
                    rubber, Vector3.zero);
                Sc08Part("GoggleFoam", root, Sc08SweepTube("Sc08GoggleFoamM" + (sy > 0 ? "T" : "B"), Sc08ArcPath(r - 0.011f, sy * 0.027f, -78f, 78f, 0f, 24), 0.0075f, 8),
                    foam, Vector3.zero);
            }

            foreach (var sx in new[] { -1f, 1f })
            {
                var a = sx * 82f * Mathf.Deg2Rad;
                var end = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r - r);
                Sc08LocalBox("GoggleSide", root, end, new Vector3(0.010f, 0.066f, 0.014f), rubber).transform.localRotation = Quaternion.Euler(0f, sx * 82f, 0f);
                var va = sx * 60f * Mathf.Deg2Rad;
                var vp = new Vector3(Mathf.Sin(va) * (r + 0.004f), 0.018f, Mathf.Cos(va) * (r + 0.004f) - r);
                var ventGo = Sc08LocalBox("GoggleVent", root, vp, new Vector3(0.016f, 0.012f, 0.007f), vent);
                ventGo.transform.localRotation = Quaternion.Euler(0f, sx * 60f, 0f);
                // elastic strap going round the head, flat
                var pts = new List<Vector3>
                {
                    end + new Vector3(sx * 0.004f, 0f, -0.004f), end + new Vector3(sx * 0.010f, 0f, -0.030f),
                    end + new Vector3(sx * 0.006f, 0f, -0.060f), new Vector3(sx * 0.072f, 0f, -0.140f),
                    new Vector3(sx * 0.050f, 0f, -0.172f), new Vector3(sx * 0.020f, 0f, -0.186f), new Vector3(0f, 0f, -0.188f)
                };
                Sc08Part("GoggleStrap", root, Sc08SweepTube("Sc08GoggleStrapM" + (sx > 0 ? "R" : "L"), pts, 0.0042f, 6), strapMat, Vector3.zero, null,
                    new Vector3(1f, 5f, 1f));
                Sc08LocalBox("GoggleBuckle", root, end + new Vector3(sx * 0.012f, 0f, -0.034f), new Vector3(0.006f, 0.030f, 0.020f), steel);
            }

            // nose bridge
            Sc08LocalBox("GoggleBridge", root, new Vector3(0f, -0.016f, -0.010f), new Vector3(0.026f, 0.022f, 0.012f), foam);
            return root;
        }

        // Face shield: acrylic visor hanging from a brow bar, adjustable headband with foam brow pad and a ratchet knob at the back.
        private static Transform Sc08FaceShield(Transform parent, string name, Vector3 localPos, float scale, Quaternion localRot)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = localRot;
            root.localScale = Vector3.one * scale;
            var visor = Sc06GlassMat("Sc08ShieldGlass", new Color(0.88f, 0.96f, 0.99f), 0.07f, 0.85f);
            var band = Mat("Sc08ShieldBand", new Color(0.12f, 0.34f, 0.62f), 0.45f, 0f);
            var foam = Mat("Sc08ShieldFoam", new Color(0.92f, 0.92f, 0.9f), 0.12f, 0f);
            var dark = Mat("Sc08ShieldDark", new Color(0.08f, 0.09f, 0.10f), 0.4f, 0f);
            const float vr = 0.108f;
            Sc08Part("ShieldVisor", root, Sc08ArcShell("Sc08ShieldVisorM", vr, 0.19f, 168f, 0.0018f, 40, 0.012f), visor, new Vector3(0f, -0.108f, 0f));
            Sc08Part("ShieldBrowBar", root, Sc08SweepTube("Sc08ShieldBrowM", Sc08ArcPath(vr + 0.002f, -0.009f, -84f, 84f, 0f, 24), 0.0055f, 8), dark, Vector3.zero);
            // headband: open at the back, a flat ribbon
            Sc08Part("ShieldBand", root, Sc08SweepTube("Sc08ShieldBandM", Sc08ArcPath(0.092f, 0.004f, -118f, 118f, -0.020f, 32), 0.0045f, 6), band,
                Vector3.zero, null, new Vector3(1f, 4.2f, 1f));
            Sc08Part("ShieldPad", root, Sc08SweepTube("Sc08ShieldPadM", Sc08ArcPath(0.086f, 0.002f, -44f, 44f, -0.020f, 14), 0.0105f, 8), foam,
                Vector3.zero, null, new Vector3(1f, 1.5f, 1f));
            foreach (var sx in new[] { -1f, 1f })
            {
                var a = sx * 70f * Mathf.Deg2Rad;
                Sc08LocalBox("ShieldHinge", root, new Vector3(Mathf.Sin(a) * 0.098f, -0.004f, Mathf.Cos(a) * 0.098f - 0.108f - 0.010f),
                    new Vector3(0.012f, 0.030f, 0.026f), band).transform.localRotation = Quaternion.Euler(0f, sx * 70f, 0f);
            }

            // ratchet adjuster at the back
            Sc08LocalBox("ShieldRatchet", root, new Vector3(0f, 0.004f, -0.092f - 0.020f - 0.108f + 0.108f - 0.014f), new Vector3(0.050f, 0.030f, 0.020f), band);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = "ShieldKnob";
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(root, false);
            knob.transform.localPosition = new Vector3(0f, 0.004f, -0.138f);
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            knob.transform.localScale = new Vector3(0.026f, 0.006f, 0.026f);
            knob.GetComponent<MeshRenderer>().sharedMaterial = dark;
            return root;
        }

        // The designer's glove model (Scenario 4, gloves1.glb), tinted neoprene green, standing upright with the palm toward +Z.
        private static void Sc08GlovePair(Transform parent, Vector3 localPos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc04/gloves1.glb");
            var green = Mat("Sc08Neoprene", new Color(0.05f, 0.47f, 0.30f), 0.38f, 0f);
            var xs = new[] { -0.085f, 0.085f };
            var names = new[] { "Plane.009", "Plane.004" };
            if (asset == null)
                return;
            for (var k = 0; k < 2; k++)
            {
                Mesh mesh = null;
                foreach (var mf in asset.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.name == names[k])
                        mesh = mf.sharedMesh;
                if (mesh == null)
                {
                    Debug.LogWarning("[SC08] glove mesh " + names[k] + " not found");
                    continue;
                }

                var b = mesh.bounds;
                var s = b.size;
                int thin = 0, tall = 0;
                for (var i = 1; i < 3; i++)
                {
                    if (s[i] < s[thin])
                        thin = i;
                    if (s[i] > s[tall])
                        tall = i;
                }

                var eT = Vector3.zero;
                eT[thin] = Sc08GloveFlipFront ? -1f : 1f;
                var eL = Vector3.zero;
                eL[tall] = Sc08GloveFlipUp ? -1f : 1f;
                var rot = Quaternion.Inverse(Quaternion.LookRotation(eT, eL));
                var go = new GameObject("Glove" + k);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = green;
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.transform.localRotation = rot * Quaternion.Euler(0f, 0f, k == 0 ? -5f : 5f);
                go.transform.localPosition = localPos + new Vector3(xs[k], 0f, 0f) - go.transform.localRotation * b.center;
            }
        }

        private const bool Sc08GloveFlipFront = false;
        private const bool Sc08GloveFlipUp = false;

        // Acid-resistant apron: PVC-coated grey cloth with folds, shaped bib and armholes, neck strap, waist ties, pockets with stitching,
        // grommets and a piping hem. Front = +Z, the neck strap meets at the top (the hook).
        private static void Sc08Apron(Transform parent, string name, Vector3 localPos, float scale)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localScale = Vector3.one * scale;
            var fabric = Sc06Pbr("Sc08ApronPbr", "Apron", Color.white, new Vector2(1f, 1f), 0.5f, 0.8f, false);
            fabric.SetFloat("_Smoothness", 0.42f);
            Sc08TwoSided(fabric);
            var strapMat = Mat("Sc08ApronStrap", new Color(0.18f, 0.20f, 0.22f), 0.3f, 0f);
            var steel = Mat("Sc08Buckle", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.3f);
            var stitch = Mat("Sc08Stitch", new Color(0.88f, 0.88f, 0.84f), 0.2f, 0f);

            float Hw(float y)
            {
                var ys = new[] { 0f, -0.10f, -0.20f, -0.25f, -0.32f, -0.80f };
                var ws = new[] { 0.085f, 0.100f, 0.125f, 0.185f, 0.235f, 0.255f };
                for (var i = 0; i < ys.Length - 1; i++)
                    if (y <= ys[i] && y >= ys[i + 1])
                        return Mathf.Lerp(ws[i], ws[i + 1], Mathf.SmoothStep(0f, 1f, (ys[i] - y) / (ys[i] - ys[i + 1])));
                return ws[ws.Length - 1];
            }

            Sc08Part("ApronCloth", root, Sc08Cloth("Sc08ApronCloth", Hw, -0.80f, 22, 44, 0.006f, 4f), fabric, Vector3.zero);
            // piping along the hem and the neck strap
            Sc08LocalBox("ApronHem", root, new Vector3(0f, -0.797f, 0.0f), new Vector3(0.52f, 0.014f, 0.007f), strapMat);
            foreach (var sx in new[] { -1f, 1f })
            {
                // neck strap up to the hook
                var pts = new List<Vector3>();
                for (var i = 0; i <= 8; i++)
                {
                    var u = i / 8f;
                    pts.Add(new Vector3(Mathf.Lerp(sx * 0.080f, 0f, u), Mathf.Lerp(0.0f, 0.20f, u), Mathf.Lerp(0.010f, 0.0f, u)));
                }

                Sc08Part("ApronNeckStrap", root, Sc08SweepTube("Sc08ApronNeckM" + (sx > 0 ? "R" : "L"), pts, 0.0035f, 6), strapMat, Vector3.zero, null,
                    new Vector3(1f, 1f, 4f));
                // waist ties hanging from the sides
                var tie = new List<Vector3>();
                for (var i = 0; i <= 12; i++)
                {
                    var u = i / 12f;
                    tie.Add(new Vector3(sx * (0.236f + 0.012f * Mathf.Sin(u * 4f)), -0.30f - 0.34f * u, 0.014f + 0.02f * Mathf.Sin(u * 3f) * sx));
                }

                Sc08Part("ApronTie", root, Sc08SweepTube("Sc08ApronTieM" + (sx > 0 ? "R" : "L"), tie, 0.0045f, 6), strapMat, Vector3.zero, null,
                    new Vector3(2.2f, 1f, 1f));
                var gr = Sc08LocalBox("ApronGrommet", root, new Vector3(sx * 0.226f, -0.30f, 0.010f), new Vector3(0.016f, 0.016f, 0.006f), steel);
                gr.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            Sc08LocalBox("ApronBuckle", root, new Vector3(0.060f, 0.075f, 0.002f), new Vector3(0.026f, 0.016f, 0.006f), steel);
            // two patch pockets with stitching
            foreach (var px in new[] { -0.115f, 0.115f })
            {
                Sc08Part("ApronPocket", root, Sc08Cloth("Sc08ApronPocket", y => 0.065f, -0.15f, 8, 8, 0.0015f, 4f), fabric, new Vector3(px, -0.46f, 0.030f));
                Sc08LocalBox("StitchL", root, new Vector3(px - 0.060f, -0.535f, 0.0305f), new Vector3(0.002f, 0.150f, 0.002f), stitch);
                Sc08LocalBox("StitchR", root, new Vector3(px + 0.060f, -0.535f, 0.0305f), new Vector3(0.002f, 0.150f, 0.002f), stitch);
                Sc08LocalBox("StitchB", root, new Vector3(px, -0.610f, 0.0305f), new Vector3(0.122f, 0.002f, 0.002f), stitch);
            }
        }

        // The half-face respirator from the Scenario 5 model (node Cylinder.002), or a simple twin-cartridge mask as a fallback.
        private static void Sc08Respirator(Transform parent, string name, Vector3 at)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = at;
            var masks = SpawnSc05("Masks.glb", parent);
            Transform node = null;
            if (masks != null)
            {
                foreach (var t in masks.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Cylinder.002")
                        node = t;
            }

            if (node != null)
            {
                masks.transform.localScale = Vector3.one * 0.30f;
                var plane = masks.transform.Find("Plane");
                node.SetParent(root, true);
                Object.DestroyImmediate(masks);
                var b = BoundsOfTree(root);
                var s = 0.34f / Mathf.Max(0.05f, b.size.y);
                root.localScale = Vector3.one * s;
                b = BoundsOfTree(root);
                root.position += at - b.center;
                return;
            }

            if (masks != null)
                Object.DestroyImmediate(masks);
            var body = Mat("Sc08MaskBody", new Color(0.22f, 0.24f, 0.26f), 0.4f, 0f);
            var cart = Mat("Sc08MaskCart", new Color(0.78f, 0.45f, 0.12f), 0.4f, 0f);
            Box("MaskFace", root, at, new Vector3(0.16f, 0.12f, 0.06f), body, false);
            foreach (var sx in new[] { -1f, 1f })
                Sc08Lens("MaskCartridge", root, at + new Vector3(sx * 0.085f, -0.01f, 0.03f), 0.07f, 0.06f, cart);
        }

        private static Sc08Ppe Sc08BuildPpeStation(Transform room, float x0, PresentableArt art, out Transform stand)
        {
            var white = Mat("Sc06Paint", new Color(0.90f, 0.90f, 0.88f), 0.45f, 0f);
            var board = Mat("Sc08Board", new Color(0.94f, 0.94f, 0.92f), 0.35f, 0f);
            var slotMat = Mat("Sc08Slot", new Color(0.86f, 0.88f, 0.88f), 0.3f, 0f);
            var edge = Mat("Sc08SlotEdge", new Color(0.62f, 0.64f, 0.64f), 0.4f, 0f);
            var hook = Mat("Sc08Hook", new Color(0.78f, 0.80f, 0.82f), 0.6f, 0.2f);
            const float bz = 0.2f;
            var bx = x0 + 0.03f;
            var holder = new GameObject("PpeStation").transform;
            holder.SetParent(room, false);
            Box("PpeBoard", holder, new Vector3(bx, 1.30f, bz), new Vector3(0.04f, 1.42f, 1.3f), board, false);
            QuadObj("PpeTitle", holder, new Vector3(bx + 0.025f, 2.09f, bz), new Vector3(0f, -90f, 0f), new Vector2(1.18f, 0.185f),
                Sc06Label("Sc08PpeTitle", "Label_Sc08PpeTitle", true, 0.3f));
            Box("PpeTitleBack", holder, new Vector3(bx + 0.005f, 2.09f, bz), new Vector3(0.02f, 0.2f, 1.22f), edge, false);

            var slotsY = new[] { 1.58f, 1.58f, 0.95f, 0.95f };
            var slotsZ = new[] { bz - 0.31f, bz + 0.31f, bz - 0.31f, bz + 0.31f };
            var items = new Transform[4];
            for (var i = 0; i < 4; i++)
            {
                var y = slotsY[i];
                var z = slotsZ[i];
                Box("Slot" + i, holder, new Vector3(bx + 0.024f, y, z), new Vector3(0.012f, 0.50f, 0.52f), slotMat, false);
                Box("SlotEdgeT" + i, holder, new Vector3(bx + 0.03f, y + 0.25f, z), new Vector3(0.012f, 0.008f, 0.52f), edge, false);
                Box("SlotEdgeB" + i, holder, new Vector3(bx + 0.03f, y - 0.25f, z), new Vector3(0.012f, 0.008f, 0.52f), edge, false);
                Box("SlotEdgeL" + i, holder, new Vector3(bx + 0.03f, y, z - 0.26f), new Vector3(0.012f, 0.50f, 0.008f), edge, false);
                Box("SlotEdgeR" + i, holder, new Vector3(bx + 0.03f, y, z + 0.26f), new Vector3(0.012f, 0.50f, 0.008f), edge, false);
                QuadObj("SlotCaption" + i, holder, new Vector3(bx + 0.032f, y + 0.30f, z), new Vector3(0f, -90f, 0f), new Vector2(0.50f, 0.0625f),
                    Sc06Label("Sc08Cap" + i, "Label_Sc08Cap" + i, true, 0.2f));
                if (i != 2)
                {
                    // a wall peg the item hangs from (slot 0: the face shield's peg is off centre)
                    var pz = i == 0 ? z + 0.09f : z;
                    Sc06Rod("PegRod" + i, holder, new Vector3(bx + 0.03f, y + 0.20f, pz), new Vector3(bx + 0.125f, y + 0.20f, pz), 0.0065f, hook);
                    Sc06Disc("PegCap" + i, holder, new Vector3(bx + 0.125f, y + 0.20f, pz), 0.02f, 0.004f, hook).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            // items: built facing +Z at their slot, then the whole item turned so its front faces +X (the room)
            var g0 = new GameObject("Ppe_Goggles").transform;
            g0.SetParent(holder, false);
            g0.position = new Vector3(bx + 0.11f, slotsY[0] - 0.02f, slotsZ[0]);
            Sc08FaceShield(g0, "FaceShield", new Vector3(-0.09f, 0.205f, 0.0f), 1.0f, Quaternion.identity);
            Sc08Goggles(g0, "Goggles", new Vector3(0.105f, 0.02f, 0.035f), 1.0f, Quaternion.Euler(8f, 0f, 0f));
            Sc08LocalBox("GoggleShelf", g0, new Vector3(0.105f, -0.012f, 0.0f), new Vector3(0.18f, 0.008f, 0.10f), hook);
            g0.rotation = Quaternion.Euler(0f, 90f, 0f);
            items[0] = g0;
            var resp = new GameObject("Ppe_Respirator").transform;
            resp.SetParent(holder, true);
            resp.position = new Vector3(bx + 0.12f, slotsY[1] + 0.07f, slotsZ[1]);
            Sc08Respirator(resp, "RespBody", new Vector3(bx + 0.12f, slotsY[1] + 0.07f, slotsZ[1]));
            resp.rotation = Quaternion.Euler(0f, -90f, 0f);
            items[1] = resp;
            var gl = new GameObject("Ppe_Gloves").transform;
            gl.SetParent(holder, false);
            gl.position = new Vector3(bx + 0.085f, slotsY[2] - 0.02f, slotsZ[2]);
            Sc08GlovePair(gl, Vector3.zero);
            Sc08LocalBox("GloveShelf", gl, new Vector3(0f, -0.172f, 0f), new Vector3(0.50f, 0.008f, 0.09f), hook);
            gl.rotation = Quaternion.Euler(0f, 90f, 0f);
            items[2] = gl;
            var ap = new GameObject("Ppe_Apron").transform;
            ap.SetParent(holder, false);
            ap.position = new Vector3(bx + 0.075f, slotsY[3] + 0.056f, slotsZ[3]);
            Sc08Apron(ap, "ApronBody", Vector3.zero, 0.72f);
            ap.rotation = Quaternion.Euler(0f, 90f, 0f);
            items[3] = ap;

            for (var i = 0; i < 4; i++)
            {
                var b = BoundsOfTree(items[i]);
                var col = items[i].gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                // padded toward the player (+X) so the fingertip reaches it before the board stops it
                col.center = items[i].InverseTransformPoint(b.center + new Vector3(0.12f, 0f, 0f));
                var sz = items[i].InverseTransformVector(new Vector3(b.size.x + 0.26f, b.size.y + 0.1f, b.size.z + 0.1f));
                col.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                var poke = items[i].gameObject.AddComponent<PokeTarget>();
                var pso = new SerializedObject(poke);
                pso.FindProperty("reach").floatValue = 0.07f;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }

            // wall extras around the board: a small first-aid sign and a floor mark in front of the station
            Box("PpeFloorMark", holder, new Vector3(x0 + 0.85f, 0.004f, bz), new Vector3(0.05f, 0.004f, 1.5f), Mat("Sc06HazardTape", new Color(0.9f, 0.76f, 0.1f), 0.3f, 0f), false);

            var ppeGo = new GameObject("Sc08PpeStation");
            ppeGo.transform.SetParent(room, false);
            var ppe = ppeGo.AddComponent<Sc08Ppe>();
            var arrow = new GameObject("PpeArrow").transform;
            arrow.SetParent(ppeGo.transform, false);
            var pso2 = new SerializedObject(ppe);
            pso2.FindProperty("art").objectReferenceValue = art;
            pso2.FindProperty("arrow").objectReferenceValue = arrow;
            var arr = pso2.FindProperty("items");
            arr.arraySize = 4;
            for (var i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            pso2.ApplyModifiedPropertiesWithoutUndo();
            // facing -X, 0.85 m from the board (the fingertip reaches the padded touch volumes)
            stand = Marker("Stand_SC08_Ppe", room, new Vector3(x0 + 0.85f, 0f, bz + 0.15f), 270f);
            return ppe;
        }

        // The same wraparound safety goggles as the PPE board, laid on the table with the lens toward the player.
        private static void Sc08TableGoggles(Transform parent, Vector3 at)
        {
            var g = Sc08Goggles(parent, "TableGoggles", at, 2.2f, Quaternion.Euler(-8f, 22f, 0f));
            var lowest = float.MaxValue;
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (r.bounds.min.y < lowest)
                    lowest = r.bounds.min.y;
            }

            if (lowest < float.MaxValue)
                g.position += Vector3.up * (at.y + 0.012f - lowest);
        }

        // Spiral notebook lying on the table: page block, written page, wire rings along the far edge.
        private static void Sc08TableNotebook(Transform parent, Vector3 at)
        {
            var root = new GameObject("Notebook").transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, -14f, 0f);
            var cover = Mat("Sc08NoteCover", new Color(0.90f, 0.90f, 0.88f), 0.25f, 0f);
            var page = Mat("Sc08NotePage", Color.white, 0.12f, 0f, Sc06Tex("Label_Sc08Notebook"), null, true);
            var wire = Mat("Sc08NoteWire", new Color(0.72f, 0.74f, 0.76f), 0.55f, 0.35f);
            Sc08LocalBox("Cover", root, new Vector3(0f, 0.004f, 0f), new Vector3(0.24f, 0.008f, 0.18f), cover);
            Sc08LocalBox("Pages", root, new Vector3(0.006f, 0.012f, 0.006f), new Vector3(0.210f, 0.012f, 0.155f), cover);
            var sheet = QuadObj("Page", root, new Vector3(0.006f, 0.019f, 0.006f), new Vector3(90f, 0f, 0f), new Vector2(0.206f, 0.150f), page);
            sheet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (var i = 0; i < 11; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Spiral";
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(root, false);
                ring.transform.localPosition = new Vector3(-0.100f + i * 0.020f, 0.016f, -0.078f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.014f, 0.004f, 0.014f);
                var rr = ring.GetComponent<MeshRenderer>();
                rr.sharedMaterial = wire;
                rr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
#endif

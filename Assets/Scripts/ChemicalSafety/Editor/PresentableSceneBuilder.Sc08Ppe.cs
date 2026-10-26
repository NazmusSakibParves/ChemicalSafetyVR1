#if UNITY_EDITOR
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

        // Splash goggles, front = +Z, about 17 cm wide: dark rim, clear lenses, nose bridge, elastic strap going back.
        // 'lying' = on the table (strap laid flat behind the lenses).
        private static Mesh _sc08Ring;

        private static Mesh Sc08RingMesh()
        {
            if (_sc08Ring != null)
                return _sc08Ring;
            var pts = new System.Collections.Generic.List<Vector3>();
            for (var i = 0; i <= 28; i++)
            {
                var a = i / 28f * Mathf.PI * 2f;
                pts.Add(new Vector3(Mathf.Cos(a) * 0.044f, Mathf.Sin(a) * 0.044f, 0f));
            }

            _sc08Ring = Sc06Tube("Sc08GoggleRing", pts, 0.0065f, 8);
            return _sc08Ring;
        }

        private static Transform Sc08Goggles(Transform parent, string name, Vector3 at, float scale, Quaternion rot, bool lying)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = at;
            var rim = Mat("Sc08GoggleRim", new Color(0.09f, 0.10f, 0.11f), 0.45f, 0f);
            var lens = Sc06GlassMat("Sc08GoggleLens", new Color(0.86f, 0.95f, 0.98f), 0.08f, 0.85f);
            var strap = Mat("Sc08GoggleStrap", new Color(0.06f, 0.06f, 0.07f), 0.3f, 0f);
            foreach (var sx in new[] { -1f, 1f })
            {
                var c = at + new Vector3(sx * 0.046f, 0f, 0f);
                var ringMesh = Sc08RingMesh();
                var ringGo = Sc06Mesh("GoggleRim", root, ringMesh, rim, c, null, Vector3.one);
                Sc08Lens("GoggleLens", root, c + new Vector3(0f, 0f, 0.012f), 0.078f, 0.012f, lens);
                Box("GoggleStrap", root, at + new Vector3(sx * 0.098f, 0f, -0.045f), new Vector3(0.012f, 0.032f, 0.09f), strap, false)
                    .transform.rotation = Quaternion.Euler(0f, sx * -18f, 0f);
            }

            Box("GoggleBridge", root, at + new Vector3(0f, 0.005f, 0.002f), new Vector3(0.026f, 0.014f, 0.012f), rim, false);
            Box("GoggleTop", root, at + new Vector3(0f, 0.045f, -0.002f), new Vector3(0.16f, 0.008f, 0.018f), rim, false);
            // vent dots on the rim sides
            foreach (var sx in new[] { -1f, 1f })
                Box("GoggleVent", root, at + new Vector3(sx * 0.094f, 0.02f, 0.008f), new Vector3(0.006f, 0.014f, 0.006f), Mat("Sc08GoggleVent", new Color(0.35f, 0.37f, 0.4f), 0.4f, 0f), false);
            root.localScale = Vector3.one * scale;
            root.rotation = rot;
            return root;
        }

        // Neoprene glove, fingers up, front = +Z.
        private static void Sc08Glove(Transform parent, string name, Vector3 at, float yaw, float scale)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = at;
            var green = Mat("Sc08Neoprene", new Color(0.06f, 0.46f, 0.30f), 0.42f, 0f);
            var dark = Mat("Sc08NeopreneDark", new Color(0.03f, 0.2f, 0.16f), 0.4f, 0f);
            Cyl("GloveCuff", root, at, 0.10f, 0.046f, dark);
            var palm = Box("GlovePalm", root, at + new Vector3(0f, 0.15f, 0f), new Vector3(0.088f, 0.10f, 0.026f), green, false);
            var lens = new[] { 0.058f, 0.072f, 0.066f, 0.050f };
            for (var i = 0; i < 4; i++)
            {
                var fx = (i - 1.5f) * 0.0225f;
                var f = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                f.name = "GloveFinger" + i;
                Object.DestroyImmediate(f.GetComponent<Collider>());
                f.transform.SetParent(root, true);
                f.transform.position = at + new Vector3(fx, 0.20f + lens[i] * 0.5f, 0f);
                f.transform.localScale = new Vector3(0.022f, lens[i] * 0.5f + 0.011f, 0.020f);
                f.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? green : green;
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "GloveTip" + i;
                Object.DestroyImmediate(tip.GetComponent<Collider>());
                tip.transform.SetParent(root, true);
                tip.transform.position = at + new Vector3(fx, 0.20f + lens[i], 0f);
                tip.transform.localScale = new Vector3(0.022f, 0.026f, 0.021f);
                tip.GetComponent<MeshRenderer>().sharedMaterial = dark;
            }

            var th = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            th.name = "GloveThumb";
            Object.DestroyImmediate(th.GetComponent<Collider>());
            th.transform.SetParent(root, true);
            th.transform.position = at + new Vector3(0.060f, 0.18f, 0.004f);
            th.transform.rotation = Quaternion.Euler(0f, 0f, -38f);
            th.transform.localScale = new Vector3(0.024f, 0.038f, 0.022f);
            th.GetComponent<MeshRenderer>().sharedMaterial = green;
            palm.transform.rotation = Quaternion.identity;
            root.localScale = Vector3.one * scale;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Acid-resistant apron, front = +Z, hangs from its neck loop: bib, skirt, ties.
        private static void Sc08Apron(Transform parent, string name, Vector3 at)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = at;
            var fabric = Mat("Sc08Apron", new Color(0.64f, 0.67f, 0.68f), 0.5f, 0.15f);
            var strap = Mat("Sc08ApronStrap", new Color(0.30f, 0.32f, 0.33f), 0.35f, 0f);
            Box("ApronBib", root, at + new Vector3(0f, -0.15f, 0f), new Vector3(0.30f, 0.26f, 0.008f), fabric, false);
            Box("ApronSkirt", root, at + new Vector3(0f, -0.57f, 0.004f), new Vector3(0.46f, 0.58f, 0.008f), fabric, false);
            Box("ApronWaist", root, at + new Vector3(0f, -0.29f, 0.006f), new Vector3(0.30f, 0.02f, 0.01f), strap, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                Box("ApronStrap", root, at + new Vector3(sx * 0.07f, -0.04f, 0f), new Vector3(0.016f, 0.20f, 0.006f), strap, false)
                    .transform.rotation = Quaternion.Euler(0f, 0f, sx * 22f);
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
                var s = 0.30f / Mathf.Max(0.05f, b.size.y);
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
                Box("Hook" + i, holder, new Vector3(bx + 0.045f, y + 0.20f, z), new Vector3(0.05f, 0.016f, 0.016f), hook, false);
            }

            // items: built facing +Z at their slot, then the whole item turned so its front faces +X (the room)
            Transform Make(int i, string n)
            {
                var g = new GameObject(n).transform;
                g.SetParent(holder, true);
                g.position = new Vector3(bx + 0.11f, slotsY[i] - 0.02f, slotsZ[i]);
                return g;
            }

            var gog = Sc08Goggles(holder, "Ppe_Goggles", new Vector3(bx + 0.11f, slotsY[0] - 0.02f, slotsZ[0]), 1.45f, Quaternion.Euler(0f, 90f, 0f), false);
            items[0] = gog;
            var resp = new GameObject("Ppe_Respirator").transform;
            resp.SetParent(holder, true);
            resp.position = new Vector3(bx + 0.12f, slotsY[1] - 0.02f, slotsZ[1]);
            Sc08Respirator(resp, "RespBody", new Vector3(bx + 0.12f, slotsY[1] - 0.02f, slotsZ[1]));
            resp.rotation = Quaternion.Euler(0f, -90f, 0f);
            items[1] = resp;
            var gl = new GameObject("Ppe_Gloves").transform;
            gl.SetParent(holder, true);
            gl.position = new Vector3(bx + 0.105f, slotsY[2] - 0.05f, slotsZ[2]);
            Sc08Glove(gl, "GloveA", new Vector3(bx + 0.10f, slotsY[2] - 0.20f, slotsZ[2] - 0.07f), 0f, 1.5f);
            Sc08Glove(gl, "GloveB", new Vector3(bx + 0.11f, slotsY[2] - 0.20f, slotsZ[2] + 0.07f), 0f, 1.5f);
            gl.rotation = Quaternion.Euler(0f, 90f, 0f);
            items[2] = gl;
            var ap = new GameObject("Ppe_Apron").transform;
            ap.SetParent(holder, true);
            ap.position = new Vector3(bx + 0.10f, slotsY[3] + 0.22f, slotsZ[3]);
            Sc08Apron(ap, "ApronBody", new Vector3(bx + 0.10f, slotsY[3] + 0.20f, slotsZ[3]));
            ap.localScale = Vector3.one * 0.55f;
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
    }
}
#endif

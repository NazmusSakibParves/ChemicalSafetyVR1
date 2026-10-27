#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 9 props: the island table with three labelled flasks, tray (goggles, gloves), notebook; the broken-flask pieces; the water fx.</summary>
    public static partial class PresentableSceneBuilder
    {
        private const float Sc09FlaskScale = 1.15f;

        // Erlenmeyer flask: thick base with a rounded heel, beaded rim (wall 3 mm).
        private static Mesh Sc09FlaskMesh() => Sc06Lathe("Sc09Flask", new[]
        {
            new Vector2(0f, 0.0f), new Vector2(0.046f, 0.0f), new Vector2(0.0565f, 0.004f), new Vector2(0.0588f, 0.012f), new Vector2(0.0545f, 0.035f),
            new Vector2(0.050f, 0.06f), new Vector2(0.0415f, 0.088f), new Vector2(0.032f, 0.115f), new Vector2(0.0145f, 0.150f),
            new Vector2(0.0145f, 0.176f), new Vector2(0.0178f, 0.1792f), new Vector2(0.0198f, 0.1828f), new Vector2(0.0185f, 0.1862f), new Vector2(0.0150f, 0.1868f)
        }, 0.003f, 40);

        // Liquid inside Sc09FlaskMesh up to 0.085 m, following the inner wall.
        private static Mesh Sc09FlaskLiquidMesh()
        {
            return Sc06Lathe("Sc09FlaskLiquid", new[]
            {
                new Vector2(0f, 0.004f), new Vector2(0.046f, 0.004f), new Vector2(0.0550f, 0.012f), new Vector2(0.0515f, 0.035f),
                new Vector2(0.047f, 0.060f), new Vector2(0.0395f, 0.085f), new Vector2(0f, 0.085f)
            }, 0.001f, 36, false);
        }

        // Paper label wrapped round the cone, centred on the side that faces the user (-X); u runs left to right as seen from there.
        private static Mesh Sc09CurvedLabelMesh()
        {
            var path = GenDir + "/Mesh_Sc09FlaskLabel.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Sc09FlaskLabel" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var ys = new[] { 0.012f, 0.035f, 0.060f, 0.088f };
            var rs = new[] { 0.0588f, 0.0545f, 0.050f, 0.0415f };
            float R(float y)
            {
                for (var i = 0; i < ys.Length - 1; i++)
                    if (y <= ys[i + 1])
                        return Mathf.Lerp(rs[i], rs[i + 1], Mathf.InverseLerp(ys[i], ys[i + 1], y));
                return rs[rs.Length - 1];
            }

            const int n = 16;
            const float y0 = 0.030f, y1 = 0.084f, half = 52f * Mathf.Deg2Rad;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            for (var k = 0; k <= n; k++)
            {
                var a = (k / (float)n - 0.5f) * 2f * half;
                foreach (var row in new[] { 0, 1 })
                {
                    var y = row == 0 ? y0 : y1;
                    var r = R(y) + 0.0016f;
                    v.Add(new Vector3(-r * Mathf.Cos(a), y, -r * Mathf.Sin(a)));
                    uv.Add(new Vector2(k / (float)n, row));
                }
            }

            var t = new List<int>();
            for (var k = 0; k < n; k++)
            {
                int A = k * 2, C = k * 2 + 1, B = (k + 1) * 2, D = (k + 1) * 2 + 1;
                t.AddRange(new[] { A, C, D, A, D, B });
            }

            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A glass piece with a saw-tooth edge: the profile is turned into a shell, then the rim ring is cut up.
        private static Mesh Sc09JaggedShell(string name, Vector2[] prof, int seg, float jag, bool topEdge)
        {
            var mesh = Sc06Lathe(name, prof, 0.002f, seg);
            var verts = mesh.vertices;
            var yMax = float.MinValue;
            var yMin = float.MaxValue;
            foreach (var v in verts)
            {
                yMax = Mathf.Max(yMax, v.y);
                yMin = Mathf.Min(yMin, v.y);
            }

            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                var edge = topEdge ? v.y >= yMax - 0.0006f : v.y <= yMin + 0.0006f;
                if (!edge)
                    continue;
                var a = Mathf.Atan2(v.z, v.x);
                var j = Mathf.RoundToInt((a < 0f ? a + Mathf.PI * 2f : a) / (Mathf.PI * 2f) * seg) % seg;
                var h = Mathf.Abs(Mathf.Sin(j * 12.9898f) * 43758.5453f) % 1f;
                var cut = jag * (j % 2 == 0 ? 0.35f + 0.65f * h : 0.05f + 0.3f * h);
                v.y += topEdge ? -cut : cut;
                verts[i] = v;
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A flat shard (5-corner polygon, slightly bent), glass shader is two-sided.
        private static Mesh Sc09ShardMesh(string name, int seed, float size)
        {
            var path = GenDir + "/Mesh_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var rnd = new System.Random(seed);
            var v = new List<Vector3> { Vector3.zero };
            const int n = 5;
            for (var i = 0; i < n; i++)
            {
                var a = (i + (float)rnd.NextDouble() * 0.5f) / n * Mathf.PI * 2f;
                var r = size * (0.45f + 0.55f * (float)rnd.NextDouble());
                v.Add(new Vector3(Mathf.Cos(a) * r * 1.2f, 0.18f * r * Mathf.Cos(a * 2f), Mathf.Sin(a) * r * 0.8f));
            }

            var t = new List<int>();
            for (var i = 0; i < n; i++)
                t.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % n });
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void Sc09Props(Transform room, float ox, PresentableArt art, Sc09Layout lab)
        {
            var T = lab.benchTop;
            var cx = lab.flaskBase.x;
            var cz = lab.flaskBase.z;
            var steel = Mat("Sc09BenchTop", new Color(0.60f, 0.62f, 0.64f), 0.55f, 0.5f);
            var dark = Mat("Sc09IslandDark", new Color(0.30f, 0.32f, 0.35f), 0.45f, 0.4f);
            var solid = SolidLayer;

            // ---------------------------------------------------------------- island table (long axis along Z)
            const float L = 2.0f, D = 0.82f;
            var top = Box("Sc09IslandTop", room, new Vector3(cx, T - 0.02f, cz), new Vector3(D, 0.04f, L), steel);
            if (solid >= 0)
                top.layer = solid;
            Box("Sc09IslandEdgeL", room, new Vector3(cx, T - 0.045f, cz + L * 0.5f - 0.01f), new Vector3(D + 0.004f, 0.012f, 0.02f), dark, false);
            Box("Sc09IslandEdgeR", room, new Vector3(cx, T - 0.045f, cz - L * 0.5f + 0.01f), new Vector3(D + 0.004f, 0.012f, 0.02f), dark, false);
            Box("Sc09IslandApronF", room, new Vector3(cx - D * 0.5f + 0.05f, T - 0.11f, cz), new Vector3(0.02f, 0.12f, L - 0.12f), dark, false);
            Box("Sc09IslandApronB", room, new Vector3(cx + D * 0.5f - 0.05f, T - 0.11f, cz), new Vector3(0.02f, 0.12f, L - 0.12f), dark, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                    Box("Sc09IslandLeg", room, new Vector3(cx + sx * (D * 0.5f - 0.06f), (T - 0.05f) * 0.5f, cz + sz * (L * 0.5f - 0.07f)),
                        new Vector3(0.05f, T - 0.05f, 0.05f), dark, false);
            }

            Box("Sc09IslandShelf", room, new Vector3(cx, 0.28f, cz), new Vector3(D - 0.14f, 0.02f, L - 0.16f), dark, false);
            Box("Sc09IslandShelfLip", room, new Vector3(cx - D * 0.5f + 0.075f, 0.30f, cz), new Vector3(0.012f, 0.03f, L - 0.16f), dark, false);

            // ---------------------------------------------------------------- the three flasks (the viewer faces +X; B-Sample 1 on his left = +Z)
            var glass = Sc06GlassMat("Sc09FlaskGlass", new Color(0.84f, 0.93f, 0.96f), 0.07f, 0.75f);
            var cone = Sc09FlaskMesh();
            var labelMesh = Sc09CurvedLabelMesh();
            var liqMesh = Sc09FlaskLiquidMesh();
            var water = Sc06Liquid("Sc09WaterLiquid", new Color(0.9f, 0.95f, 0.98f, 0.28f));
            var purple = Sc06Liquid("Sc09PurpleLiquid", new Color(0.52f, 0.08f, 0.70f, 0.92f));
            const float pitch = 0.27f;
            var contactShadow = ShadowMat(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png"));
            for (var i = 0; i < 3; i++)
            {
                var at = new Vector3(cx, T, cz + (1 - i) * pitch);
                var g = new GameObject("Sc09Flask" + (i + 1)).transform;
                g.SetParent(room, true);
                g.position = at;
                Sc06Mesh("Sc09FlaskGlass" + (i + 1), g, cone, glass, at, null, Vector3.one * Sc09FlaskScale);
                var lq = Sc06Mesh("Sc09Liquid" + (i + 1), g, liqMesh, i == 1 ? purple : water, at + Vector3.up * (0.003f * Sc09FlaskScale), null,
                    Vector3.one * Sc09FlaskScale);
                if (i != 1)
                    lq.transform.localScale = new Vector3(Sc09FlaskScale, Sc09FlaskScale * 0.18f, Sc09FlaskScale);
                var lblMat = Sc06Label("Sc09FlaskLabelMat" + (i + 1), "Label_Sc09Flask" + (i + 1), true, 0.3f);
                Sc08TwoSided(lblMat);
                Sc06Mesh("Sc09FlaskLabel" + (i + 1), g, labelMesh, lblMat, at, null, Vector3.one * Sc09FlaskScale);
                var sh = QuadObj("Sc09FlaskShadow" + (i + 1), g, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector2(0.20f, 0.20f), contactShadow);
                sh.transform.SetParent(g, true);
                sh.transform.position = at + Vector3.up * 0.0015f;
                Object.DestroyImmediate(sh.GetComponent<Collider>());
            }

            var f2 = new Vector3(cx, T, cz);
            var pokeGo = new GameObject("Sc09FlaskPoke");
            pokeGo.transform.SetParent(room, true);
            pokeGo.transform.position = f2 + Vector3.up * 0.12f;
            var col = pokeGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.20f, 0.30f, 0.20f);
            var pt = pokeGo.AddComponent<PokeTarget>();
            var pso = new SerializedObject(pt);
            pso.FindProperty("reach").floatValue = 0.07f;
            pso.ApplyModifiedPropertiesWithoutUndo();
            var ring = QuadObj("Sc09FlaskGlow", room, f2 + Vector3.up * 0.004f, new Vector3(90f, 0f, 0f), new Vector2(0.30f, 0.30f),
                MatAlpha("Sc09FlaskRing", Sc06Tex("Fx_Ripple"), new Color(0.25f, 1f, 0.45f, 0.85f), true));
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.SetActive(false);

            // ---------------------------------------------------------------- more glassware and bottles on the far side of the flasks
            var beaker = new GameObject("Sc09Beaker").transform;
            beaker.SetParent(room, true);
            beaker.position = new Vector3(cx + 0.28f, T, cz + 0.54f);
            Sc06Mesh("Sc09BeakerGlass", beaker, Sc06BeakerMesh(), glass, beaker.position, null, Vector3.one * 1.25f);
            Sc06Mesh("Sc09BeakerLiquid", beaker, Sc06LiquidDisc("Sc09BeakerFill", 0.0372f, 0.05f), water, beaker.position + Vector3.up * 0.004f, null, Vector3.one * 1.25f);
            var cup = new GameObject("Sc09Cup").transform;
            cup.SetParent(room, true);
            cup.position = new Vector3(cx + 0.30f, T, cz + 0.12f);
            Sc06Mesh("Sc09CupGlass", cup, Sc06CupMesh(), glass, cup.position, null, Vector3.one * 1.0f);
            Sc06ChemGroup(room, "Alcohol", new[] { "glassdark", "label", "liquid" }, new Vector3(cx + 0.30f, T, cz - 0.12f), -90f);
            Sc06ChemGroup(room, "NaOH", new[] { "cap", "glass", "label" }, new Vector3(cx + 0.36f, T, cz - 0.42f), -90f);
            Sc06ChemGroup(room, "Phenyl", new[] { "capblue", "glass", "label" }, new Vector3(cx + 0.38f, T, cz - 0.22f), -90f);

            // ---------------------------------------------------------------- tray with goggles and gloves (viewer's left = +Z), notebook, pen, timer, test tube rack (right)
            var trayAt = new Vector3(cx + 0.02f, T, cz + 0.78f);
            var trayGlass = Sc06GlassMat("Sc09TrayGlass", new Color(0.93f, 0.96f, 0.98f), 0.14f, 0.9f);
            var trayRim = Mat("Sc08TrayRim", new Color(0.88f, 0.9f, 0.9f, 1f), 0.5f, 0f);
            const float tl = 0.40f, td = 0.60f, th = 0.045f; // along X, along Z
            var tray = new GameObject("Sc09Tray").transform;
            tray.SetParent(room, true);
            Box("TrayFloor", tray, trayAt + new Vector3(0f, 0.005f, 0f), new Vector3(tl, 0.01f, td), trayGlass, false);
            Box("TrayWallA", tray, trayAt + new Vector3(tl * 0.5f - 0.004f, th * 0.5f, 0f), new Vector3(0.008f, th, td), trayGlass, false);
            Box("TrayWallB", tray, trayAt + new Vector3(-tl * 0.5f + 0.004f, th * 0.5f, 0f), new Vector3(0.008f, th, td), trayGlass, false);
            Box("TrayWallC", tray, trayAt + new Vector3(0f, th * 0.5f, td * 0.5f - 0.004f), new Vector3(tl, th, 0.008f), trayGlass, false);
            Box("TrayWallD", tray, trayAt + new Vector3(0f, th * 0.5f, -td * 0.5f + 0.004f), new Vector3(tl, th, 0.008f), trayGlass, false);
            Box("TrayRimA", tray, trayAt + new Vector3(tl * 0.5f - 0.006f, th, 0f), new Vector3(0.02f, 0.01f, td), trayRim, false);
            Box("TrayRimB", tray, trayAt + new Vector3(-tl * 0.5f + 0.006f, th, 0f), new Vector3(0.02f, 0.01f, td), trayRim, false);
            Box("TrayRimC", tray, trayAt + new Vector3(0f, th, td * 0.5f - 0.006f), new Vector3(tl, 0.01f, 0.02f), trayRim, false);
            Box("TrayRimD", tray, trayAt + new Vector3(0f, th, -td * 0.5f + 0.006f), new Vector3(tl, 0.01f, 0.02f), trayRim, false);
            // blue nitrile gloves lying flat in the tray (the Scenario 4 glove model, laid on its back)
            var holder = new GameObject("Sc09GloveHolder").transform;
            holder.SetParent(tray, false);
            holder.position = trayAt + new Vector3(0.0f, 0.016f, -0.13f);
            holder.rotation = Quaternion.Euler(90f, 90f, 0f);
            Sc08GlovePair(holder, Vector3.zero);
            var nitrile = Mat("Sc09Nitrile", new Color(0.16f, 0.46f, 0.88f), 0.45f, 0f);
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = nitrile;
            holder.localScale = Vector3.one * 0.9f;
            var gg = Sc08Goggles(tray, "Sc09Goggles", trayAt + new Vector3(0f, 0.02f, 0.17f), 1.5f, Quaternion.Euler(-8f, 90f, 0f));
            var lowest = float.MaxValue;
            foreach (var r in gg.GetComponentsInChildren<Renderer>())
                lowest = Mathf.Min(lowest, r.bounds.min.y);
            if (lowest < float.MaxValue)
                gg.position += Vector3.up * (trayAt.y + 0.012f - lowest);

            Sc08TableNotebook(room, new Vector3(cx + 0.02f, T, cz - 0.78f));
            Cyl("Sc09Pen", room, new Vector3(cx - 0.14f, T + 0.004f, cz - 0.64f), 0.14f, 0.005f, Mat("Sc09PenBody", new Color(0.12f, 0.13f, 0.30f), 0.5f, 0f))
                .transform.rotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 24f, 0f);
            Sc06TestTubeRack(room, new Vector3(cx + 0.20f, T, cz - 0.60f), glass);
            var timerBody = Mat("Sc09TimerBody", new Color(0.93f, 0.94f, 0.95f), 0.5f, 0f);
            Box("Sc09Timer", room, new Vector3(cx - 0.24f, T + 0.015f, cz - 0.30f), new Vector3(0.075f, 0.03f, 0.075f), timerBody, false);
            Box("Sc09TimerFace", room, new Vector3(cx - 0.24f, T + 0.031f, cz - 0.30f), new Vector3(0.05f, 0.002f, 0.028f),
                Mat("Sc09TimerLcd", new Color(0.55f, 0.62f, 0.55f), 0.2f, 0f, null, new Color(0.15f, 0.2f, 0.15f)), false);

            // ---------------------------------------------------------------- the broken flask: jagged base and neck, shards, puddle, splatter (hidden until the flask breaks)
            var broken = new GameObject("Sc09Broken").transform;
            broken.SetParent(room, false);
            broken.position = f2;
            var baseMesh = Sc09JaggedShell("Sc09BrokenBase", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.050f, 0f), new Vector2(0.058f, 0.006f), new Vector2(0.0545f, 0.035f), new Vector2(0.050f, 0.06f),
                new Vector2(0.0455f, 0.082f)
            }, 24, 0.028f, true);
            var neckMesh = Sc09JaggedShell("Sc09BrokenNeck", new[]
            {
                new Vector2(0.0405f, 0.098f), new Vector2(0.032f, 0.115f), new Vector2(0.0145f, 0.150f), new Vector2(0.0145f, 0.178f),
                new Vector2(0.0185f, 0.1815f), new Vector2(0.0185f, 0.185f), new Vector2(0.0150f, 0.186f)
            }, 20, 0.022f, false);
            var bb = Sc06Mesh("Sc09BrokenBase", broken, baseMesh, glass, f2 + new Vector3(0f, 0.066f, 0f), Quaternion.Euler(0f, 205f, 86f),
                Vector3.one * Sc09FlaskScale);
            var bl = Sc06Mesh("Sc09BrokenLiquid", broken, Sc06LiquidDisc("Sc09BrokenFill", 0.030f, 0.05f), purple,
                f2 + new Vector3(0.012f, 0.02f, 0.01f), Quaternion.Euler(0f, 0f, 90f), Vector3.one * Sc09FlaskScale);
            bl.SetActive(false);
            Sc06Mesh("Sc09BrokenNeck", broken, neckMesh, glass, f2 + new Vector3(0.10f, 0.030f, -0.09f), Quaternion.Euler(0f, 62f, 90f),
                Vector3.one * Sc09FlaskScale);
            for (var i = 0; i < 12; i++)
            {
                var sz = i < 4 ? 0.028f : 0.016f;
                var m = Sc09ShardMesh("Sc09ShardMesh" + i, 11 + i, sz);
                var a = i / 12f * Mathf.PI * 2f;
                Sc06Mesh("Sc09Shard" + i, broken, m, glass, f2 + new Vector3(Mathf.Cos(a) * 0.04f, 0.02f + (i % 3) * 0.012f, Mathf.Sin(a) * 0.04f),
                    Quaternion.Euler(i * 31f, i * 57f, i * 13f), Vector3.one);
            }

            var puddleMat = Mat("Sc09PuddleMat", new Color(0.44f, 0.06f, 0.60f, 0.93f), 0.95f, 0f, Sc06Tex("Label_Sc09Puddle"));
            MakeTransparent(puddleMat);
            var puddle = QuadObj("Sc09Puddle", broken, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector2(1f, 1f), puddleMat);
            puddle.transform.SetParent(broken, true);
            puddle.transform.position = f2 + new Vector3(0.02f, 0.0035f, 0.0f);
            puddle.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            Object.DestroyImmediate(puddle.GetComponent<Collider>());
            var splatMat = Mat("Sc09SplatMat", new Color(0.40f, 0.05f, 0.56f, 0.95f), 0.92f, 0f, Sc06Tex("Label_Sc09Splat"));
            MakeTransparent(splatMat);
            var rnd = new System.Random(3);
            for (var i = 0; i < 9; i++)
            {
                var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var r = 0.12f + (float)rnd.NextDouble() * 0.30f;
                var size = 0.14f + (float)rnd.NextDouble() * 0.16f;
                var p = f2 + new Vector3(Mathf.Cos(a) * r * 0.8f, 0.0028f + i * 0.0002f, Mathf.Sin(a) * r * 1.3f);
                var sp = QuadObj("Sc09Splat" + i, broken, Vector3.zero, new Vector3(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector2(size, size), splatMat);
                sp.transform.SetParent(broken, true);
                sp.transform.position = p;
                Object.DestroyImmediate(sp.GetComponent<Collider>());
                sp.SetActive(false);
            }

            broken.gameObject.SetActive(false);
        }

        private static void Sc09Shower(Transform room, float ox, PresentableArt art, Sc09Layout lab, Sc09Refs refs)
        {
            var plate = lab.showerPlate;
            var bowl = lab.bowlCentre;
            // push plate marker + touch volume + glow (the designer's green plate is part of the shower model)
            var plateT = new GameObject("Sc09PushPlate").transform;
            plateT.SetParent(room, true);
            plateT.position = plate;
            var pokeGo = new GameObject("Sc09PushPoke");
            pokeGo.transform.SetParent(room, true);
            pokeGo.transform.position = plate + new Vector3(0f, 0f, 0.06f);
            var col = pokeGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.22f, 0.22f, 0.26f);
            var pt = pokeGo.AddComponent<PokeTarget>();
            var pso = new SerializedObject(pt);
            pso.FindProperty("reach").floatValue = 0.07f;
            pso.ApplyModifiedPropertiesWithoutUndo();
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var glow = QuadObj("Sc09PushGlow", room, plate + new Vector3(0f, 0f, 0.09f), new Vector3(0f, 180f, 0f), new Vector2(0.36f, 0.36f),
                MatAlpha("Sc09PushGlowMat", glowTex, new Color(0.2f, 1f, 0.4f, 0.6f), true));
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.SetActive(false);

            // the green STAND HERE mark on the floor at the user's feet
            var stand = QuadObj("Sc09StandHere", room, new Vector3(ox + lab.showerStand.x, 0.008f, lab.showerStand.z), new Vector3(90f, 180f, 0f),
                new Vector2(1.5f, 1.0f), MatAlpha("Sc09StandHereMat", Sc06Tex("Label_Sc09StandHere"), Color.white, false));
            Object.DestroyImmediate(stand.GetComponent<Collider>());
            stand.SetActive(false);

            // floor drain under the shower (recessed steel rim + dark sump + slotted grate), like the designer picture.
            // ChemLab02's shower pad sits ~0.145 m above y=0 — probe it so the grate is not buried under that slab.
            const float drainZ = 0.38f;
            var drainXZ = new Vector3(bowl.x, 0f, bowl.z + drainZ);
            var floorY = Sc09ProbeShowerFloorY(room, drainXZ);
            var drainAt = new Vector3(drainXZ.x, floorY, drainXZ.z);
            var frameM = Mat("Sc09DrainFrame", new Color(0.82f, 0.84f, 0.86f), 0.4f, 0.9f);
            var sumpM = Mat("Sc09DrainSump", new Color(0.03f, 0.03f, 0.04f), 0.12f, 0f);
            var slatM = Mat("Sc09DrainSlat", new Color(0.76f, 0.78f, 0.80f), 0.35f, 0.9f);
            var drain = new GameObject("Sc09Drain").transform;
            drain.SetParent(room, true);
            // dark sump sits below the slats so gaps read as a hole
            Box("DrainSump", drain, drainAt + new Vector3(0f, 0.004f, 0f), new Vector3(0.56f, 0.006f, 0.36f), sumpM, false);
            // rim only (not a filled plate) so the sump shows through
            Box("DrainRimN", drain, drainAt + new Vector3(0f, 0.012f, 0.195f), new Vector3(0.64f, 0.014f, 0.028f), frameM, false);
            Box("DrainRimS", drain, drainAt + new Vector3(0f, 0.012f, -0.195f), new Vector3(0.64f, 0.014f, 0.028f), frameM, false);
            Box("DrainRimE", drain, drainAt + new Vector3(0.306f, 0.012f, 0f), new Vector3(0.028f, 0.014f, 0.42f), frameM, false);
            Box("DrainRimW", drain, drainAt + new Vector3(-0.306f, 0.012f, 0f), new Vector3(0.028f, 0.014f, 0.42f), frameM, false);
            for (var i = 0; i < 11; i++)
                Box("DrainSlat" + i, drain, drainAt + new Vector3(-0.25f + i * 0.05f, 0.016f, 0f), new Vector3(0.022f, 0.010f, 0.34f), slatM, false);
            Box("DrainRibA", drain, drainAt + new Vector3(0f, 0.018f, 0.08f), new Vector3(0.54f, 0.008f, 0.016f), slatM, false);
            Box("DrainRibB", drain, drainAt + new Vector3(0f, 0.018f, -0.08f), new Vector3(0.54f, 0.008f, 0.016f), slatM, false);

            // the hologram text is a world canvas made at runtime by Sc09Shower (always on top, like the other cards; a plain quad was hidden in the headset)

            // water: shower head spray, two eye wash jets, bowl splash and mist, splash on the floor
            var waterMat = ParticleMat("Sc09WaterDrop", Sc06Tex("Fx_Drop"), false);
            var mistMat = ParticleMat("Sc09WaterMist", Sc06Tex("Fx_Wisp"), false);
            var fx = new GameObject("Sc09WaterFx").transform;
            fx.SetParent(room, false);
            var headAt = new Vector3(lab.showerHead.x, lab.showerHead.y, lab.showerHead.z);
            Sc09Jet(fx, "ShowerSpray", headAt, Quaternion.Euler(90f, 0f, 0f), 520, 24f, 3.2f, 0.9f, 0.010f, 0.022f, 0.35f, waterMat, 0.12f);
            var nozzleY = bowl.y + 0.01f;
            Sc09Jet(fx, "EyewashL", new Vector3(bowl.x + 0.115f, nozzleY, bowl.z), Quaternion.Euler(-80f, 0f, 14f), 160, 5f, 1.9f, 0.7f, 0.006f, 0.014f, 1.0f, waterMat, 0.012f);
            Sc09Jet(fx, "EyewashR", new Vector3(bowl.x - 0.115f, nozzleY, bowl.z), Quaternion.Euler(-80f, 0f, -14f), 160, 5f, 1.9f, 0.7f, 0.006f, 0.014f, 1.0f, waterMat, 0.012f);
            Sc09Jet(fx, "BowlSplash", new Vector3(bowl.x, bowl.y - 0.04f, bowl.z), Quaternion.Euler(-90f, 0f, 0f), 90, 60f, 0.9f, 0.5f, 0.005f, 0.012f, 1.0f, waterMat, 0.09f);
            Sc09Jet(fx, "FloorSplash", new Vector3(drainAt.x, drainAt.y + 0.03f, drainAt.z), Quaternion.Euler(-90f, 0f, 0f), 60, 55f, 0.9f, 0.45f, 0.006f, 0.014f, 1.0f, waterMat, 0.28f);
            Sc09Jet(fx, "Mist", new Vector3(bowl.x, 1.2f, bowl.z + 0.3f), Quaternion.Euler(-90f, 0f, 0f), 28, 70f, 0.12f, 2.2f, 0.25f, 0.5f, -0.02f, mistMat, 0.5f, 0.12f);
        }

        // ChemLab02 has a raised pad under the shower (~0.145 m). Probe it so props sit on that slab, not under it.
        private static float Sc09ProbeShowerFloorY(Transform room, Vector3 at)
        {
            var chem = room.Find("ChemLab02");
            if (chem == null)
                return 0.02f;
            var added = new List<MeshCollider>();
            foreach (var mf in chem.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.vertexCount < 1000)
                    continue;
                var col = mf.GetComponent<MeshCollider>();
                if (col == null)
                {
                    col = mf.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                    added.Add(col);
                }
            }

            var floorY = 0.02f;
            var hits = Physics.RaycastAll(new Vector3(at.x, 2.5f, at.z), Vector3.down, 3f);
            for (var i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.point.y < 0.03f || h.point.y > 0.4f)
                    continue;
                if (!h.collider.transform.IsChildOf(chem))
                    continue;
                if (h.point.y > floorY)
                    floorY = h.point.y;
            }

            for (var i = 0; i < added.Count; i++)
                Object.DestroyImmediate(added[i]);
            return floorY;
        }

        // Looping particle emitter; 'dir' turns the system's forward (+Z) to the emit direction.
        private static void Sc09Jet(Transform parent, string name, Vector3 at, Quaternion dir, int rate, float angle, float speed, float life, float s0, float s1,
            float gravity, Material mat, float radius, float alpha = 0.7f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, dir);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(s0, s1);
            main.startColor = new Color(0.82f, 0.92f, 1f, alpha);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(60, rate * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = radius;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
#endif

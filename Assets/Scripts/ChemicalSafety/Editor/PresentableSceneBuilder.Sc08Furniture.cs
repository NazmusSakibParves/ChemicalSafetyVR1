#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 8 lab furniture: cabinet bays with drawers, doors and handles, epoxy worktops with rolled edges, the sink counter.</summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc08FurnitureMats
        {
            public Material body, panel, handle, kick, epoxy, edge, steel;
        }

        private static Sc08FurnitureMats Sc08FurnMats(Material epoxy)
        {
            return new Sc08FurnitureMats
            {
                body = Mat("Sc08CarcassGap", new Color(0.30f, 0.31f, 0.32f), 0.3f, 0f),
                panel = Mat("Sc08CabinetPanel", new Color(0.80f, 0.81f, 0.80f), 0.5f, 0f),
                handle = Mat("Sc08CabinetHandle", new Color(0.70f, 0.72f, 0.75f), 0.8f, 0.55f),
                kick = Mat("Sc08Kick", new Color(0.10f, 0.11f, 0.12f), 0.3f, 0f),
                epoxy = epoxy,
                edge = Mat("Sc08TopEdge", new Color(0.06f, 0.065f, 0.07f), 0.85f, 0f),
                steel = Mat("Sc08SinkSteel", new Color(0.74f, 0.76f, 0.78f), 0.7f, 0.1f)
            };
        }

        // Drawer + door bays on one face. 'dir' = +1 (face toward +Z) or -1; zFace = z of the carcass surface.
        private static void Sc08Bays(Transform room, string tag, Sc08FurnitureMats m, float cx, float zFace, float dir, float length, float yBottom, float yTop)
        {
            var n = Mathf.Max(1, Mathf.RoundToInt(length / 0.5f));
            var bw = length / n;
            var w = bw - 0.006f;
            const float drawerH = 0.17f;
            const float pt = 0.018f;
            for (var i = 0; i < n; i++)
            {
                var x = cx - length * 0.5f + bw * (i + 0.5f);
                var zPanel = zFace + dir * (pt * 0.5f);
                var drawerY = yTop - 0.003f - drawerH * 0.5f;
                Box(tag + "Drawer" + i, room, new Vector3(x, drawerY, zPanel), new Vector3(w, drawerH, pt), m.panel, false);
                var doorTop = yTop - drawerH - 0.009f;
                var doorH = doorTop - (yBottom + 0.003f);
                var doorY = (doorTop + yBottom + 0.003f) * 0.5f;
                Box(tag + "Door" + i, room, new Vector3(x, doorY, zPanel), new Vector3(w, doorH, pt), m.panel, false);
                // handles: a recessed grip at the top of the drawer, a vertical bar on the door
                Box(tag + "GripD" + i, room, new Vector3(x, drawerY + 0.035f, zFace + dir * (pt + 0.007f)), new Vector3(0.15f, 0.012f, 0.014f), m.handle, false);
                var side = (i % 2 == 0 ? 1f : -1f);
                Box(tag + "GripR" + i, room, new Vector3(x + side * (w * 0.5f - 0.05f), doorTop - 0.12f, zFace + dir * (pt + 0.007f)), new Vector3(0.012f, 0.13f, 0.014f), m.handle, false);
            }
        }

        // A worktop slab with rolled (glossy) edges. 'center' is the top surface centre; size x/z.
        private static void Sc08Top(Transform room, string tag, Sc08FurnitureMats m, Vector3 center, float lx, float lz, float th)
        {
            Box(tag + "Slab", room, new Vector3(center.x, center.y - th * 0.5f, center.z), new Vector3(lx, th, lz), m.epoxy, false);
            var er = th * 0.5f;
            var y = center.y - th * 0.5f;
            Sc06Rod(tag + "EdgeF", room, new Vector3(center.x - lx * 0.5f + er, y, center.z + lz * 0.5f - er), new Vector3(center.x + lx * 0.5f - er, y, center.z + lz * 0.5f - er), er, m.edge);
            Sc06Rod(tag + "EdgeB", room, new Vector3(center.x - lx * 0.5f + er, y, center.z - lz * 0.5f + er), new Vector3(center.x + lx * 0.5f - er, y, center.z - lz * 0.5f + er), er, m.edge);
            Sc06Rod(tag + "EdgeL", room, new Vector3(center.x + lx * 0.5f - er, y, center.z - lz * 0.5f + er), new Vector3(center.x + lx * 0.5f - er, y, center.z + lz * 0.5f - er), er, m.edge);
            Sc06Rod(tag + "EdgeR", room, new Vector3(center.x - lx * 0.5f + er, y, center.z - lz * 0.5f + er), new Vector3(center.x - lx * 0.5f + er, y, center.z + lz * 0.5f - er), er, m.edge);
        }

        // The island: cabinet banks facing both ways, black epoxy top with rolled edges, kick plates, a steel service rail along the back edge.
        private static void Sc08Island(Transform room, float cx, float cz, float topY, Material epoxy)
        {
            var m = Sc08FurnMats(epoxy);
            const float L = 2.1f, D = 0.96f, th = 0.04f;
            var bodyL = L - 0.1f;
            var bodyD = D - 0.1f;
            var yBottom = 0.10f;
            var yTop = topY - th;
            Box("IslandCarcass", room, new Vector3(cx, (yBottom + yTop) * 0.5f, cz), new Vector3(bodyL, yTop - yBottom, bodyD), m.body, false);
            Box("IslandKick", room, new Vector3(cx, yBottom * 0.5f, cz), new Vector3(bodyL - 0.06f, yBottom, bodyD - 0.06f), m.kick, false);
            Sc08Bays(room, "IslF", m, cx, cz + bodyD * 0.5f, 1f, bodyL, yBottom, yTop);
            Sc08Bays(room, "IslB", m, cx, cz - bodyD * 0.5f, -1f, bodyL, yBottom, yTop);
            // end panels
            foreach (var sx in new[] { -1f, 1f })
            {
                Box("IslandEnd", room, new Vector3(cx + sx * (bodyL * 0.5f + 0.009f), (yBottom + yTop) * 0.5f, cz), new Vector3(0.018f, yTop - yBottom - 0.006f, bodyD - 0.006f), m.panel, false);
                Box("IslandEndKick", room, new Vector3(cx + sx * (bodyL * 0.5f - 0.02f), yBottom * 0.5f, cz), new Vector3(0.02f, yBottom, bodyD - 0.06f), m.kick, false);
            }

            Sc08Top(room, "IslTop", m, new Vector3(cx, topY, cz), L, D, th);
            // two flush power sockets on the end panel side of the top
            Box("IslandSocketPlate", room, new Vector3(cx + L * 0.5f - 0.15f, topY + 0.002f, cz - D * 0.5f + 0.09f), new Vector3(0.09f, 0.006f, 0.05f), m.handle, false);
            // faint wear: a slightly lighter lab-mat zone is the tray area (a darker ring of use), done with the contact shadow in the room
        }

        // Counter against the back wall: cabinet bays on the front face, epoxy top with a real sink opening, steel basin, tap, splashback.
        private static void Sc08Counter(Transform room, float x0, float x1, float zBack, float topY, float sinkX, Material epoxy)
        {
            var m = Sc08FurnMats(epoxy);
            const float D = 0.64f, th = 0.04f;
            var yBottom = 0.10f;
            var yTop = topY - th;
            var cx = (x0 + x1) * 0.5f;
            var L = x1 - x0;
            var zc = zBack + D * 0.5f;
            var body = Box("CounterBody", room, new Vector3(cx, (yBottom + yTop) * 0.5f, zc), new Vector3(L - 0.02f, yTop - yBottom, D - 0.06f), m.body, true);
            Box("CounterKick", room, new Vector3(cx, yBottom * 0.5f, zc + 0.03f), new Vector3(L - 0.06f, yBottom, D - 0.12f), m.kick, false);
            Sc08Bays(room, "Ctr", m, cx, zc + (D - 0.06f) * 0.5f, 1f, L - 0.02f, yBottom, yTop);
            // top in four pieces round the sink opening
            const float sw = 0.62f, sd = 0.40f;
            var sz = zBack + 0.34f;
            float left = x1 - (sinkX + sw * 0.5f), right = (sinkX - sw * 0.5f) - x0;
            // (viewer's left is +X) x range of the pieces
            Box("CounterTopA", room, new Vector3((sinkX + sw * 0.5f + x1) * 0.5f, topY - th * 0.5f, zc), new Vector3(left, th, D), m.epoxy, false);
            Box("CounterTopB", room, new Vector3((x0 + sinkX - sw * 0.5f) * 0.5f, topY - th * 0.5f, zc), new Vector3(right, th, D), m.epoxy, false);
            var frontZ0 = sz + sd * 0.5f;
            var backZ1 = sz - sd * 0.5f;
            Box("CounterTopF", room, new Vector3(sinkX, topY - th * 0.5f, (frontZ0 + zBack + D) * 0.5f), new Vector3(sw, th, zBack + D - frontZ0), m.epoxy, false);
            Box("CounterTopK", room, new Vector3(sinkX, topY - th * 0.5f, (zBack + backZ1) * 0.5f), new Vector3(sw, th, backZ1 - zBack), m.epoxy, false);
            // rolled edge along the front
            Sc06Rod("CounterEdge", room, new Vector3(x0 + th * 0.5f, topY - th * 0.5f, zBack + D - th * 0.5f), new Vector3(x1 - th * 0.5f, topY - th * 0.5f, zBack + D - th * 0.5f), th * 0.5f, m.edge);
            // basin: floor and four walls, drain, tap
            var basinY = topY - 0.20f;
            Box("BasinFloor", room, new Vector3(sinkX, basinY, sz), new Vector3(sw - 0.02f, 0.012f, sd - 0.02f), m.steel, false);
            Box("BasinWallF", room, new Vector3(sinkX, topY - 0.10f, frontZ0 - 0.008f), new Vector3(sw, 0.20f, 0.012f), m.steel, false);
            Box("BasinWallK", room, new Vector3(sinkX, topY - 0.10f, backZ1 + 0.008f), new Vector3(sw, 0.20f, 0.012f), m.steel, false);
            Box("BasinWallL", room, new Vector3(sinkX + sw * 0.5f - 0.006f, topY - 0.10f, sz), new Vector3(0.012f, 0.20f, sd), m.steel, false);
            Box("BasinWallR", room, new Vector3(sinkX - sw * 0.5f + 0.006f, topY - 0.10f, sz), new Vector3(0.012f, 0.20f, sd), m.steel, false);
            Sc06Disc("BasinDrain", room, new Vector3(sinkX, basinY + 0.008f, sz), 0.07f, 0.004f, m.kick);
            Box("BasinRim", room, new Vector3(sinkX, topY + 0.002f, frontZ0 + 0.004f), new Vector3(sw + 0.03f, 0.005f, 0.02f), m.steel, false);
            // gooseneck tap with two lever handles
            var pts = new System.Collections.Generic.List<Vector3>();
            var tz = zBack + 0.07f;
            pts.Add(new Vector3(sinkX, topY, tz));
            pts.Add(new Vector3(sinkX, topY + 0.18f, tz));
            for (var i = 0; i <= 8; i++)
            {
                var a = i / 8f * Mathf.PI * 0.5f;
                pts.Add(new Vector3(sinkX, topY + 0.18f + Mathf.Sin(a) * 0.10f, tz + (1f - Mathf.Cos(a)) * 0.10f));
            }

            pts.Add(new Vector3(sinkX, topY + 0.20f, tz + 0.18f));
            pts.Add(new Vector3(sinkX, topY + 0.15f, tz + 0.19f));
            Sc08Part("TapPipe", room, Sc08SweepTube("Sc08TapM", pts, 0.011f, 10), m.steel, Vector3.zero);
            foreach (var sx in new[] { -1f, 1f })
            {
                Cyl("TapLeverBase", room, new Vector3(sinkX + sx * 0.10f, topY, tz), 0.03f, 0.015f, m.steel);
                Box("TapLever", room, new Vector3(sinkX + sx * 0.10f, topY + 0.06f, tz), new Vector3(0.012f, 0.012f, 0.07f), m.handle, false)
                    .transform.rotation = Quaternion.Euler(0f, sx * 25f, 0f);
            }

            // splashback along the wall (steel behind the sink, epoxy elsewhere)
            Box("CounterSplash", room, new Vector3(cx, topY + 0.06f, zBack + 0.012f), new Vector3(L, 0.12f, 0.02f), m.epoxy, false);
            Box("SinkSplash", room, new Vector3(sinkX, topY + 0.19f, zBack + 0.012f), new Vector3(sw + 0.1f, 0.26f, 0.008f), m.steel, false);
            if (body != null)
                body.name = "CounterBody";
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 8 set dressing: window blinds, exit door, wall fittings, sprinklers, floor wear, shower drain, reflection probe.</summary>
    public static partial class PresentableSceneBuilder
    {
        private static void Sc08Dress(Transform room, float ox, float x0, float x1, float zBack, float zFront, float h, float winZ)
        {
            var white = Mat("Sc06Paint", new Color(0.90f, 0.90f, 0.88f), 0.45f, 0f);
            var darkSteel = Mat("Sc06DarkSteel", new Color(0.30f, 0.32f, 0.34f), 0.45f, 0f);
            var steel = Mat("Sc08CabinetHandle", new Color(0.70f, 0.72f, 0.75f), 0.8f, 0.55f);
            var plasticWhite = Mat("Sc08Dispenser", new Color(0.93f, 0.94f, 0.93f), 0.55f, 0f);
            var red = Mat("Sc08RedPlastic", new Color(0.72f, 0.08f, 0.07f), 0.5f, 0f);

            // ---- window blinds, half drawn (one alpha-clipped quad: slats, gaps, cord)
            Sc08QuadWorld("WindowBlinds", room, new Vector3(x1 - 0.062f, 1.70f, winZ), new Vector3(0f, 90f, 0f), new Vector2(1.36f, 0.86f),
                Sc06Label("Sc08Blinds", "Label_Sc08Blinds", true, 0.25f));
            Box("BlindsHeadrail", room, new Vector3(x1 - 0.066f, 2.13f, winZ), new Vector3(0.03f, 0.04f, 1.42f), white, false);

            // ---- exit door in the right wall (viewer's right), a way out: leaf, frame, vision panel, kick plate, lever, closer
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
            Box("DoorPush", room, new Vector3(x0 + 0.078f, 0.98f, doorZ - 0.32f), new Vector3(0.008f, 0.26f, 0.05f), steel, false);
            Box("DoorLever", room, new Vector3(x0 + 0.10f, 1.0f, doorZ + 0.38f), new Vector3(0.05f, 0.025f, 0.12f), steel, false);
            Box("DoorCloser", room, new Vector3(x0 + 0.09f, 2.02f, doorZ + 0.22f), new Vector3(0.05f, 0.05f, 0.28f), darkSteel, false);
            Sc08QuadWorld("ExitSign", room, new Vector3(x0 + 0.015f, 2.34f, doorZ), new Vector3(0f, -90f, 0f), new Vector2(0.44f, 0.19f),
                Sc06Label("Sc08ExitSign", "Label_Sc08Exit", true, 0.3f));

            // ---- left wall: fire alarm pull station, first aid box, sockets and a switch
            Box("AlarmBox", room, new Vector3(x1 - 0.03f, 1.30f, -1.35f), new Vector3(0.05f, 0.14f, 0.12f), red, false);
            Box("AlarmHandle", room, new Vector3(x1 - 0.062f, 1.28f, -1.35f), new Vector3(0.016f, 0.045f, 0.05f), white, false);
            Sc08QuadWorld("AlarmLabel", room, new Vector3(x1 - 0.0565f, 1.375f, -1.35f), new Vector3(0f, 90f, 0f), new Vector2(0.09f, 0.045f),
                Sc06Label("Sc08Alarm", "Label_Sc08Alarm", true, 0.3f));
            Box("FirstAidBox", room, new Vector3(x1 - 0.06f, 1.45f, 2.35f), new Vector3(0.10f, 0.34f, 0.28f), Mat("Sc08FirstAidBox", new Color(0.15f, 0.55f, 0.32f), 0.4f, 0f), false);
            Sc08QuadWorld("FirstAidSign", room, new Vector3(x1 - 0.1105f, 1.45f, 2.35f), new Vector3(0f, 90f, 0f), new Vector2(0.24f, 0.24f),
                Sc06Label("Sc08FirstAid", "Label_Sc08FirstAid", true, 0.3f));
            Box("SwitchPlate", room, new Vector3(x1 - 0.012f, 1.2f, 1.7f), new Vector3(0.012f, 0.08f, 0.05f), white, false);
            Box("SocketPlate", room, new Vector3(x1 - 0.012f, 0.5f, 1.35f), new Vector3(0.012f, 0.08f, 0.08f), white, false);
            Box("SocketPlate2", room, new Vector3(x1 - 0.012f, 0.5f, -0.2f), new Vector3(0.012f, 0.08f, 0.08f), white, false);

            // ---- back wall: paper towel and soap dispensers by the sink
            Box("PaperTowel", room, new Vector3(ox + 2.38f, 1.40f, zBack + 0.07f), new Vector3(0.28f, 0.36f, 0.14f), plasticWhite, false);
            Box("PaperTowelSlot", room, new Vector3(ox + 2.38f, 1.25f, zBack + 0.145f), new Vector3(0.22f, 0.02f, 0.01f), darkSteel, false);
            Box("PaperTowelWindow", room, new Vector3(ox + 2.38f, 1.44f, zBack + 0.142f), new Vector3(0.10f, 0.18f, 0.006f), Mat("Sc08DispWindow", new Color(0.35f, 0.40f, 0.42f), 0.8f, 0f), false);
            Box("SoapDispenser", room, new Vector3(ox + 1.22f, 1.28f, zBack + 0.05f), new Vector3(0.10f, 0.18f, 0.09f), plasticWhite, false);
            Box("SoapPump", room, new Vector3(ox + 1.22f, 1.17f, zBack + 0.10f), new Vector3(0.02f, 0.03f, 0.05f), steel, false);

            // ---- ceiling: sprinkler heads and a smoke detector's red LED
            for (var i = 0; i < 4; i++)
            {
                var p = new Vector3(ox - 2.2f + i * 1.9f, h, 0.6f + (i % 2) * 1.2f);
                Cyl("SprinklerPipe" + i, room, new Vector3(p.x, h - 0.06f, p.z), 0.06f, 0.008f, steel);
                Sc06Disc("SprinklerDeflector" + i, room, new Vector3(p.x, h - 0.07f, p.z), 0.04f, 0.004f, steel);
                Sc06Disc("SprinklerCollar" + i, room, new Vector3(p.x, h - 0.008f, p.z), 0.05f, 0.012f, white);
            }

            // ---- floor wear: soft scuffs and stains
            var stain = MatAlpha("Sc08Stain", Sc06Tex("Label_Sc08Stain"), new Color(1f, 1f, 1f, 0.55f), false);
            var spots = new[]
            {
                new Vector4(ox - 1.2f, 1.1f, 2.2f, 1.6f), new Vector4(ox + 0.6f, -1.6f, 1.8f, 1.2f), new Vector4(ox - 2.6f, 0.2f, 1.6f, 1.6f),
                new Vector4(ox + 2.4f, 0.8f, 1.8f, 1.4f), new Vector4(ox - 0.2f, 2.4f, 2.4f, 1.0f)
            };
            for (var i = 0; i < spots.Length; i++)
            {
                var q = QuadObj("FloorWear" + i, null, new Vector3(spots[i].x, 0.003f + i * 0.0003f, spots[i].y), new Vector3(90f, i * 63f, 0f), new Vector2(spots[i].z, spots[i].w), stain);
                q.transform.SetParent(room, true);
                Object.DestroyImmediate(q.GetComponent<Collider>());
            }

            // shower area: a yellow ring on the floor and a drain grate
            var ring = MatAlpha("Sc08ShowerRing", Sc06Tex("Fx_Ripple"), new Color(0.95f, 0.78f, 0.1f, 0.95f), false);
            var rq = QuadObj("ShowerRing", null, new Vector3(ox + 3.2f, 0.006f, zBack + 0.78f), new Vector3(90f, 0f, 0f), new Vector2(1.25f, 1.25f), ring);
            rq.transform.SetParent(room, true);
            Object.DestroyImmediate(rq.GetComponent<Collider>());
            Sc06Disc("ShowerDrain", room, new Vector3(ox + 3.2f, 0.005f, zBack + 0.78f), 0.22f, 0.006f, darkSteel);
            for (var i = -2; i <= 2; i++)
                Box("DrainBar", room, new Vector3(ox + 3.2f + i * 0.036f, 0.0085f, zBack + 0.78f), new Vector3(0.008f, 0.004f, 0.19f), steel, false);
        }

        // A baked reflection probe in the middle of the lab so steel, glass and the glossy worktops reflect the room instead of the sky.
        private static void Sc08BakeProbe(Transform env1, Transform env8)
        {
            try
            {
                var room = env8.Find("Room");
                var go = new GameObject("Sc08ReflectionProbe");
                go.transform.SetParent(room, false);
                go.transform.position = new Vector3(Sc08Ox - 1.0f, 1.5f, -0.4f);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = 128;
                probe.size = new Vector3(8.6f, 3.3f, 6.6f);
                probe.center = new Vector3(-0.2f, 0.1f, 0.2f);
                probe.boxProjection = true;
                probe.intensity = 1f;
                probe.importance = 1;
                var path = GenDir + "/Sc08Probe.exr";
                var envWasActive = env1.gameObject.activeSelf;
                env1.gameObject.SetActive(false);
                env8.gameObject.SetActive(true);
                var ok = UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
                env1.gameObject.SetActive(envWasActive);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else
                {
                    Debug.LogWarning("[SC08] reflection probe bake failed, the room uses the default reflections");
                    Object.DestroyImmediate(go);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SC08] reflection probe skipped: " + e.Message);
            }
        }
    }
}
#endif

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
    /// Scenario 4 as a garment / dyeing factory floor (client: the school-lab look is wrong). One switch:
    /// <see cref="Sc04UseFactory"/> = true builds the factory (hall, pillars with grimy barred windows, dyeing machines, pipes,
    /// the green USE GLOVES sign, one wide stainless table with a raised back edge, the HCl bottle in a clear tray
    /// behind the gloves); false + Build Presentable Scene gives the previous tiled lab back (nothing of it was deleted).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        // ------------------------------------------------------------------ the one switch for the Scenario 4 look
        private static readonly bool Sc04UseFactory = true;

        // The photo of the hall and machines behind the table (ON again: the client wants to see this version in VR first). false = the yard of real 3D machines (Sc04MachineYard).
        private static readonly bool Sc04UsePhotoBackdrop = true;

        private const float Sc04HallBackZ = -16f;
        private const float Sc04HallFrontZ = 3.6f;
        private const float Sc04HallH = 4.4f;

        // Hall shell: floor, four walls, ceiling, window bays on the right wall, mood, lights, pipes, floor lines.
        private static void BuildSc04FactoryShell(Transform room, float ox, PresentableArt art, int solid)
        {
            ConfigureSc05Textures();
            var env = room.parent;
            if (env != null && env.GetComponent<Sc03Mood>() == null)
            {
                var mood = env.gameObject.AddComponent<Sc03Mood>();
                var mso = new SerializedObject(mood);
                mso.FindProperty("ambientSky").colorValue = new Color(0.74f, 0.75f, 0.74f);
                mso.FindProperty("ambientEquator").colorValue = new Color(0.62f, 0.62f, 0.60f);
                mso.FindProperty("ambientGround").colorValue = new Color(0.30f, 0.30f, 0.28f);
                mso.FindProperty("fogColor").colorValue = new Color(0.62f, 0.62f, 0.58f);
                mso.FindProperty("fogStart").floatValue = 9f;
                mso.FindProperty("fogEnd").floatValue = 55f;
                mso.FindProperty("sunIntensity").floatValue = 0.2f;
                mso.FindProperty("reflectionIntensity").floatValue = 0.5f;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            var x0 = ox - 3.0f;   // right wall (viewer's right)
            var x1 = ox + 3.94f;  // left wall
            var cx = (x0 + x1) * 0.5f;
            var cz = (Sc04HallBackZ + Sc04HallFrontZ) * 0.5f;
            var w = x1 - x0;
            var d = Sc04HallFrontZ - Sc04HallBackZ;
            var h = Sc04HallH;

            // grimy pale plaster (like the client's photo), worn painted floor
            var floorMat = Sc05Pbr("Sc04FacFloorConcrete", "Floor", new Color(0.46f, 0.52f, 0.49f), new Vector2(w / 1.6f, d / 1.6f), 0.55f, 0.9f);
            var wallBack = Sc05Pbr("Sc04FacWallBack", "Wall", new Color(0.9f, 0.88f, 0.82f), new Vector2(w / 2.4f, 1.9f), 0.8f, 1f);
            var wallSide = Sc05Pbr("Sc04FacWallSide", "Wall", new Color(0.9f, 0.88f, 0.82f), new Vector2(d / 2.4f, 1.9f), 0.8f, 1f);
            var ceilMat = Sc05Pbr("Sc04FacCeil", "Ceil", new Color(0.7f, 0.7f, 0.7f), new Vector2(w / 2.4f, d / 2.4f), 1f, 0.8f, false);
            var pillarMat = Sc05Pbr("Sc04FacPillar", "Wall", new Color(0.94f, 0.92f, 0.86f), new Vector2(0.8f, 1.8f), 0.8f, 1f);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solid >= 0)
                    go.layer = solid;
            }

            var floor = Box("Floor", room, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(cx, h * 0.5f, Sc04HallBackZ), new Vector3(w, h, 0.12f), wallBack);
            Shell("Wall_Front", new Vector3(cx, h * 0.5f, Sc04HallFrontZ), new Vector3(w, h, 0.12f), wallBack);
            Shell("Wall_Left", new Vector3(x1, h * 0.5f, cz), new Vector3(0.12f, h, d), wallSide);
            Shell("Wall_Right", new Vector3(x0, h * 0.5f, cz), new Vector3(0.12f, h, d), wallSide);
            Box("Ceiling", room, new Vector3(cx, h, cz), new Vector3(w, 0.1f, d), ceilMat, false);

            var steel = Mat("Sc04FacSteel", new Color(0.42f, 0.44f, 0.46f), 0.4f, 0.25f);
            var rust = Mat("Sc04FacRust", new Color(0.32f, 0.2f, 0.12f), 0.3f, 0.4f);
            var blue = Mat("Sc04FacBluePipe", new Color(0.12f, 0.26f, 0.5f), 0.4f, 0.3f);
            var dark = Mat("Sc04FacDark", new Color(0.12f, 0.12f, 0.13f), 0.2f, 0.1f);

            // ---- right wall: plastered pillars with barred windows between them (daylight comes in from the right)
            var glassGlow = Mat("Sc04FacWindow", new Color(0.88f, 0.94f, 1f), 0f, 0f, null, new Color(1.25f, 1.32f, 1.4f), true);
            var frameMat = Mat("Sc04FacWinFrame", new Color(0.32f, 0.33f, 0.32f), 0.3f, 0.3f);
            var pillarZ = new[] { 0.25f, -3.6f, -7.6f, -11.6f };
            foreach (var pz in pillarZ)
                Shell("Pillar", new Vector3(x0 + 0.38f, h * 0.5f, pz), new Vector3(0.78f, h, 0.7f), pillarMat);
            for (var i = 0; i < pillarZ.Length - 1; i++)
            {
                var z0 = pillarZ[i] - 0.35f;
                var z1 = pillarZ[i + 1] + 0.35f;
                var zc = (z0 + z1) * 0.5f;
                var bay = z0 - z1;
                var wx = x0 + 0.07f;
                // glowing glass, steel frame, bars
                Box("WinGlass", room, new Vector3(wx + 0.01f, 2.3f, zc), new Vector3(0.02f, 2.3f, bay - 0.5f), glassGlow, false);
                Box("WinFrameTop", room, new Vector3(wx + 0.03f, 3.5f, zc), new Vector3(0.07f, 0.1f, bay - 0.4f), frameMat, false);
                Box("WinFrameBot", room, new Vector3(wx + 0.03f, 1.12f, zc), new Vector3(0.1f, 0.12f, bay - 0.4f), frameMat, false);
                for (var b = 0; b <= 6; b++)
                    Box("WinBarV", room, new Vector3(wx + 0.04f, 2.3f, zc - (bay - 0.5f) * 0.5f + b * (bay - 0.5f) / 6f),
                        new Vector3(0.03f, 2.3f, 0.03f), frameMat, false);
                for (var b = 0; b <= 4; b++)
                    Box("WinBarH", room, new Vector3(wx + 0.04f, 1.15f + b * 0.575f, zc), new Vector3(0.03f, 0.03f, bay - 0.5f), frameMat, false);
                // low wall under the sill, stained
                var shaft = new GameObject("WindowLight");
                shaft.transform.SetParent(room, false);
                shaft.transform.position = new Vector3(x0 + 1.4f, 2.6f, zc);
                var l = shaft.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.85f, 0.92f, 1f);
                l.range = 6.5f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
                // sun through the barred window: a spot with a pane-grid cookie lands on the table and the floor
                var sun = new GameObject("SunPatch");
                sun.transform.SetParent(room, false);
                sun.transform.position = new Vector3(x0 + 0.5f, 3.0f, zc);
                var target = i == 0 ? new Vector3(ox + 0.1f, 0.9f, 1.5f) : new Vector3(ox - 0.9f, 0f, zc + 1.4f);
                sun.transform.rotation = Quaternion.LookRotation(target - sun.transform.position, Vector3.up);
                var sl = sun.AddComponent<Light>();
                sl.type = LightType.Spot;
                sl.color = new Color(1f, 0.93f, 0.78f);
                sl.spotAngle = i == 0 ? 34f : 50f;
                sl.range = 9f;
                sl.intensity = i == 0 ? 9f : 5f;
                sl.shadows = LightShadows.None;
                sl.cookie = Sc04WindowCookie();
            }

            // pipe runs on the right wall below and above the windows, electric box on the first pillar
            for (var i = 0; i < 3; i++)
            {
                var run = Cyl("WallPipe" + i, room, new Vector3(x0 + 0.13f + i * 0.001f, 0.5f + i * 0.0f, Sc04HallFrontZ - 0.2f),
                    1f, 0.045f - i * 0.008f, i == 1 ? rust : (i == 2 ? blue : steel));
                run.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                run.transform.position = new Vector3(x0 + 0.12f, 0.75f + i * 0.17f, (Sc04HallFrontZ - 0.2f + Sc04HallBackZ + 0.2f) * 0.5f);
                run.transform.localScale = new Vector3((0.09f - i * 0.016f), (Sc04HallFrontZ - Sc04HallBackZ - 0.4f) * 0.5f, (0.09f - i * 0.016f));
            }

            Box("ElecBox", room, new Vector3(x0 + 0.1f, 1.5f, 0.88f), new Vector3(0.1f, 0.28f, 0.2f), dark, false);
            Cyl("PillarConduit", room, new Vector3(x0 + 0.09f, 0f, 0.88f), h, 0.025f, steel);

            // USE GLOVES sign on the nearest pillar, facing the table (client photo: upper right of the view)
            var signTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/Sign_UseGloves.png");
            var signMat = Mat("Sc04UseGlovesSign", Color.white, 0.35f, 0.05f, signTex, null, false, true);
            var sign = QuadObj("UseGlovesSign", room, new Vector3(x0 + 0.5f, 2.15f, 0.25f + 0.355f), new Vector3(0f, 180f, 0f),
                new Vector2(0.52f, 0.78f), signMat);
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---- ceiling: cross beams, pipes, tube lights
            var beam = Mat("Sc04FacBeam", new Color(0.2f, 0.2f, 0.21f), 0.2f, 0.3f);
            for (var z = 1f; z > Sc04HallBackZ; z -= 3.5f)
                Box("CeilBeam", room, new Vector3(cx, h - 0.3f, z), new Vector3(w, 0.32f, 0.25f), beam, false);
            for (var i = 0; i < 3; i++)
            {
                var px = ox - 1.6f + i * 2.1f;
                var py = h - 0.62f - i * 0.1f;
                var p = Cyl("CeilPipe" + i, room, new Vector3(px, py, 0f), 1f, 0.07f, i == 1 ? rust : steel);
                p.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                p.transform.position = new Vector3(px, py, (Sc04HallFrontZ + Sc04HallBackZ) * 0.5f);
                p.transform.localScale = new Vector3(0.13f - i * 0.02f, (Sc04HallFrontZ - Sc04HallBackZ - 0.4f) * 0.5f, 0.13f - i * 0.02f);
            }

            var tubeMat = Mat("Sc04FacTube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(1.15f, 1.15f, 1.1f), false);
            var housing = Mat("Sc04FacTubeHousing", new Color(0.55f, 0.56f, 0.56f), 0.4f, 0.3f);
            var tubeSpots = new[]
            {
                new Vector3(ox + 0.6f, h - 0.25f, 1.3f), new Vector3(ox + 2.4f, h - 0.25f, 1.3f), new Vector3(ox + 0.9f, h - 0.25f, -3f),
                new Vector3(ox + 2.6f, h - 0.25f, -6.5f), new Vector3(ox + 1f, h - 0.25f, -10f)
            };
            for (var i = 0; i < tubeSpots.Length; i++)
            {
                var p = tubeSpots[i];
                Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(0.14f, 0.05f, 1.3f), housing, false);
                Box("Tube" + i + "a", room, p - new Vector3(0.035f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.2f), tubeMat, false);
                Box("Tube" + i + "b", room, p + new Vector3(0.035f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.2f), tubeMat, false);
                var lightGo = new GameObject("TubeLight" + i);
                lightGo.transform.SetParent(room, false);
                lightGo.transform.position = p + Vector3.down * 0.5f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.94f, 0.96f, 0.92f);
                l.range = 8.5f;
                l.intensity = i < 2 ? 3.0f : 2.4f;
                l.shadows = LightShadows.None;
            }

            // ---- yellow lane lines on the floor, running away from the viewer to the machines
            var yellow = Mat("Sc04FacYellowLine", new Color(0.92f, 0.72f, 0.1f), 0.2f, 0f);
            Box("LaneLineA", room, new Vector3(ox - 0.9f, 0.004f, -6f), new Vector3(0.12f, 0.006f, 20f), yellow, false);
            Box("LaneLineB", room, new Vector3(ox + 0.35f, 0.004f, -8.5f), new Vector3(0.12f, 0.006f, 15f), yellow, false);
            Box("LaneLineCross", room, new Vector3(ox - 0.28f, 0.004f, -1.6f), new Vector3(1.25f, 0.006f, 0.12f), yellow, false);

            // ---- textile machines (calenders with rollers and cloth rolls, like the client's photo) down the left wall and further back
            if (Sc04UsePhotoBackdrop)
                Sc04TextileMachines(room, ox, x1);
            else
                Sc04MachineYard(room, ox, x1, h);
            Sc04OverheadPipes(room, ox, h);

            // ---- life in the air: dust motes drifting in the window light, steam wisps rising in front of the machines of the photo
            var glowTex = art != null && art.radialGlow != null ? art.radialGlow.texture : null;
            var dustMat = ParticleMat("Sc04Dust", glowTex, true);
            BuildDust(room, new Vector3(ox - 0.3f, 2.1f, -0.4f), new Vector3(6.4f, 3.2f, 5.5f), dustMat);
            var wisp = ParticleMat("Sc05SteamWisp", Sc05Tex("Steam_Wisp"), false);
            Steam(room, "FrontSteamL", new Vector3(ox + 2.0f, 0.3f, -0.6f), 1.1f, 2.4f, 0.12f, wisp, 0.14f, 6f);
            Steam(room, "FrontSteamR", new Vector3(ox - 1.8f, 0.3f, -0.8f), 1.1f, 2.4f, 0.12f, wisp, 0.12f, 6f);

            // ---- a bit of steam from the machines, drums at the left wall
            var steamMat = ParticleMat("Sc05SteamPuff", Sc05Tex("Steam_Puff"), false);
            Steam(room, "Steam0", new Vector3(ox + 3.0f, 1.9f, -4.0f), 0.7f, 3f, 0.35f, steamMat, 0.3f);
            Steam(room, "Steam1", new Vector3(ox + 3.0f, 1.9f, -9.0f), 0.7f, 3f, 0.35f, steamMat, 0.3f);
            var barrelBlue = Mat("Sc04FacBarrelBlue", new Color(0.12f, 0.24f, 0.45f), 0.4f, 0.3f);
            var barrelGrey = Mat("Sc04FacBarrelGrey", new Color(0.35f, 0.37f, 0.38f), 0.35f, 0.4f);
            for (var i = 0; i < 4; i++)
            {
                var bx = x1 - 0.55f - (i % 2) * 0.62f;
                var bz = -2.4f - (i / 2) * 0.7f;
                Cyl("Barrel" + i, room, new Vector3(bx, 0f, bz), 0.88f, 0.29f, i % 3 == 0 ? barrelBlue : barrelGrey);
                Cyl("BarrelLid" + i, room, new Vector3(bx, 0.88f, bz), 0.02f, 0.3f, steel);
            }
        }

        // ------------------------------------------------------------------ the client's photo as the far field

        private const float Sc04PhotoHFov = 100f;      // horizontal field of view the photo is mapped with
        private const float Sc04DomeRadius = 3.6f;    // the photo hangs on a cylinder this far from the learner
        private const float Sc04EyeAboveTable = 0.45f;

        private static Texture2D Sc04PhotoTex(string name)
        {
            var path = ArtRoot + "/2D/Sc04/" + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            // the backdrop must not repeat (clamp); the steel tile and the cloth must (tiled on the table)
            var wrap = name.Contains("Backdrop") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (ti != null && (ti.maxTextureSize < 4096 || ti.mipmapEnabled == false || !ti.alphaIsTransparency || ti.wrapMode != wrap))
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = wrap;
                ti.maxTextureSize = 4096;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The earlier lab bench steel (brushed), kept bright and only a little metallic: a very metallic top reads dark / black in a
        // headset when nothing is there to reflect.
        private static Material Sc04BrightSteel(Vector3 tableSize)
        {
            var m = Sc04Pbr("Sc04TableSteelBright", "Steel", new Color(0.76f, 0.78f, 0.82f), new Vector2(Mathf.Max(1f, tableSize.x / 1.3f), Mathf.Max(1f, tableSize.z / 0.9f)),
                1f, 0.5f, false);
            m.SetFloat("_Smoothness", 0.6f);
            m.SetFloat("_Metallic", 0.22f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The photo's steel as the table top (tile about 1.0 x 0.5 m).
        private static Material Sc04PhotoSteel(Vector2 tiling)
        {
            var tex = Sc04PhotoTex("Photo_Steel");
            // unlit on purpose: the photo's steel already carries its scratches, streaks and the hall's reflections, so it keeps that
            // brightness whatever the lights do (a lit material read as flat blue-grey)
            var m = Mat("Sc04PhotoSteelUnlit", new Color(0.97f, 0.98f, 1f), 0f, 0f, tex, null, true);
            m.mainTextureScale = tiling;
            EditorUtility.SetDirty(m);
            return m;
        }

        // The hall behind the table as one curved screen (client photo, bottle painted out, table cut away). Every vertex sits on the
        // ray the photo's pixel came from, so from the learner's spot the photo is exactly what the camera saw. A child of the bench:
        // it rises and sinks with the head height like the table, so the table's back edge and the photo's stay together.
        private static void Sc04PhotoDome(Transform bench, Vector3 stand, float tableTop, float lipZ)
        {
            var tex = Sc04PhotoTex("Photo_Backdrop");
            if (tex == null)
            {
                Debug.LogWarning("[Presentable] Photo_Backdrop.png missing: run Tools/make_sc04_photo_textures.py");
                return;
            }

            const float W = 1678f, H = 937f;
            var f = (W * 0.5f) / Mathf.Tan(Sc04PhotoHFov * 0.5f * Mathf.Deg2Rad);
            var eye = new Vector3(stand.x, tableTop + Sc04EyeAboveTable, stand.z);
            // pitch so that the photo's table back edge (row 378) lands on the real table's back edge
            var lipElev = Mathf.Atan2(tableTop + 0.12f - eye.y, stand.z - lipZ);
            var pitch = Mathf.Atan((H * 0.5f - 378f) / f) - lipElev;
            var fwd = new Vector3(0f, -Mathf.Sin(pitch), -Mathf.Cos(pitch));
            var right = new Vector3(-1f, 0f, 0f);
            var up = new Vector3(0f, Mathf.Cos(pitch), -Mathf.Sin(pitch));
            const int nx = 48, ny = 28;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var j = 0; j <= ny; j++)
            {
                var py = j / (float)ny * H;
                for (var i = 0; i <= nx; i++)
                {
                    var px = i / (float)nx * W;
                    var dir = fwd + right * ((px - W * 0.5f) / f) + up * ((H * 0.5f - py) / f);
                    var hlen = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                    verts.Add(eye + dir * (Sc04DomeRadius / hlen));
                    uvs.Add(new Vector2(px / W, 1f - py / H));
                }
            }

            for (var j = 0; j < ny; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var a = j * (nx + 1) + i;
                    var b = a + 1;
                    var c = a + (nx + 1);
                    var d = c + 1;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            }

            var mesh = new Mesh { name = "Sc04PhotoDome" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("PhotoDome");
            go.transform.SetParent(bench, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat("Sc04PhotoDome", Color.white, 0f, 0f, tex, null, true);
            var dm = mr.sharedMaterial;
            dm.SetFloat("_Surface", 1f);
            dm.SetFloat("_Cull", 0f);
            dm.SetOverrideTag("RenderType", "Transparent");
            dm.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            dm.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            dm.SetInt("_ZWrite", 0);
            dm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dm.renderQueue = (int)RenderQueue.Transparent - 50; // before the glass of the bottle and the tray
            EditorUtility.SetDirty(dm);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Debug.Log("[SC04] photo dome: eye " + eye + " pitch " + (pitch * Mathf.Rad2Deg).ToString("0.0") + " deg, lip elev " +
                      (lipElev * Mathf.Rad2Deg).ToString("0.0") + " deg");
        }

        // ------------------------------------------------------------------ several kinds of real machines (models of other scenarios)

        // Puts a model on the floor: yaw, optional fit to a height, centre at (x, z).
        private static void Sc04PlaceOnFloor(GameObject go, float x, float z, float yaw, float targetHeight)
        {
            if (yaw != 0f)
                go.transform.RotateAround(BoundsOfTree(go.transform).center, Vector3.up, yaw);
            var b = BoundsOfTree(go.transform);
            if (targetHeight > 0f && b.size.y > 0.01f)
            {
                go.transform.localScale *= targetHeight / b.size.y;
                b = BoundsOfTree(go.transform);
            }

            go.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z);
        }

        private static void Sc04NoColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // Left wall: the client's Hydro-Press line (injection machine, two presses, hydraulic station, tower); right side: the dye machine
        // row of Scenario 5; middle distance: polished vessels with their pipe stands and a control panel (Scenario 12).
        private static void Sc04MachineYard(Transform room, float ox, float x1, float h)
        {
            var yard = new GameObject("MachineYard").transform;
            yard.SetParent(room, false);

            // 1. Hydro-Press line along the left wall (each machine on its own, as in the supervisor-check wing)
            var press = AssetDatabase.LoadAssetAtPath<GameObject>(ScHydroPress);
            if (press != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(press);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "HydroPressLine";
                go.transform.SetParent(yard, false);
                Sc04NoColliders(go);
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.3f;
                var groups = new Dictionary<string, List<Renderer>>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var key = r.name.StartsWith("empty_2") ? "injection" : r.name.StartsWith("Cube.001") ? "pressA"
                        : r.name.StartsWith("Cube.002") ? "pressB" : r.name.StartsWith("fillister") ? "assembly" : "tower";
                    if (!groups.TryGetValue(key, out var list))
                        groups[key] = list = new List<Renderer>();
                    list.Add(r);
                }

                var spots = new Dictionary<string, Vector2>
                {
                    { "injection", new Vector2(x1 - 1.45f, -4.6f) },
                    { "pressA", new Vector2(x1 - 1.3f, -8.4f) },
                    { "pressB", new Vector2(x1 - 1.3f, -11.4f) },
                    { "assembly", new Vector2(x1 - 1.4f, -14.2f) },
                    { "tower", new Vector2(ox + 0.9f, -14.8f) },
                };
                foreach (var kv in groups)
                {
                    var all = kv.Value[0].bounds;
                    foreach (var r in kv.Value)
                        all.Encapsulate(r.bounds);
                    var at = spots[kv.Key];
                    var delta = new Vector3(at.x - all.center.x, -all.min.y, at.y - all.center.z);
                    foreach (var r in kv.Value)
                        r.transform.position += delta;
                    Debug.Log("[SC04] press " + kv.Key + " size " + all.size.ToString("F2"));
                }
            }

            // 2a. textile calenders (like the photo) on the right of the aisle, panels toward the aisle
            Sc04Calender(yard, "CalenderA", new Vector3(ox - 1.75f, 0f, -4.6f), 3.0f, 1.15f, 2.05f, 1f, false);
            Sc04Calender(yard, "CalenderB", new Vector3(ox - 1.75f, 0f, -8.0f), 3.0f, 1.15f, 1.9f, 1f, true);
            // 2. Scenario 5 dye machines (a row of vats with control panels and cloth) down the right side
            var dye = SpawnSc05("Factory.glb", yard);
            if (dye != null)
            {
                dye.name = "DyeMachineRow";
                dye.transform.localScale = Vector3.one * FactoryScale;
                dye.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var fb = BoundsOfTree(dye.transform);
                dye.transform.position = new Vector3(ox + 1.7f, -fb.min.y, -0.2f);
                var vat = Mat("Sc04YardVat", new Color(0.52f, 0.56f, 0.57f), 0.35f, 0f);
                var panel = Mat("Sc04YardPanel", new Color(0.36f, 0.48f, 0.58f), 0.3f, 0f);
                foreach (var r in dye.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    var inside = b.center.z < 0.4f && b.center.z > -17f && b.center.x > ox + 0.3f && b.center.x < ox + 8f && b.max.y < 5f;
                    if (!inside)
                    {
                        r.gameObject.SetActive(false);
                        continue;
                    }

                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (r.sharedMaterial == null)
                        r.sharedMaterial = vat;
                    else if (mn.Contains("ControlPanel") || mn.Contains("Default"))
                        r.sharedMaterial = panel;
                    if (mn.Contains("Brushed") || mn.Contains("Metal"))
                        r.sharedMaterial = vat;
                }

                // the row (about 15 m long) stands on the right of the aisle, starting well behind the table
                var active = new List<Renderer>();
                foreach (var r in dye.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.activeInHierarchy)
                        active.Add(r);
                if (active.Count > 0)
                {
                    var ab = active[0].bounds;
                    foreach (var r in active)
                        ab.Encapsulate(r.bounds);
                    dye.transform.position += new Vector3((ox - 1.75f) - ab.center.x, 0f, -11.0f - ab.max.z);
                    foreach (var r in active)
                        if (r.bounds.center.z < -15.6f)
                            r.gameObject.SetActive(false);
                    Debug.Log("[SC04] dye row size " + ab.size.ToString("F2"));
                }

                Sc04NoColliders(dye);
            }

            // 3. Scenario 12 polished vessel with its pipe stand, far end of the right side (kept small: the models are hall sized)
            var tank = Sc12Part("Sc12Plant.glb", "TankBig", yard);
            tank.name = "Vessel";
            Sc04NoColliders(tank);
            Sc04PlaceOnFloor(tank, ox - 1.6f, -14.2f, 90f, 2.0f);
            Debug.Log("[SC04] vessel size " + BoundsOfTree(tank.transform).size.ToString("F2"));
        }

        private static Texture Sc04WindowCookie()
        {
            var path = ArtRoot + "/2D/Sc04/WindowCookie.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Cookie)
            {
                ti.textureType = TextureImporterType.Cookie;
                ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // A roller lying along Z (cylinder primitives stand along Y).
        private static GameObject Sc04RollerZ(string name, Transform parent, Vector3 centre, float length, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Sc04Part(string name, Transform parent, Vector3 centre, Vector3 size, Material mat)
        {
            var go = Box(name, parent, centre, size, mat, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // One textile calender, detailed: green cast end frames with bolts, brushed-steel PBR rollers with end caps and bearings, a cloth
        // roll, fabric running between the rollers, a guard rail, a control cabinet with screen and lamps, hazard strip, riser pipe to the
        // ceiling. The rollers lie along Z (like the machines of the photo, running away from the viewer).
        private static void Sc04Calender(Transform parent, string name, Vector3 floorCentre, float length, float width, float height,
            float facing, bool blueRolls)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var green = Mat("Sc04MachGreen", new Color(0.20f, 0.42f, 0.31f), 0.5f, 0.2f);
            var greenDark = Mat("Sc04MachGreenDark", new Color(0.13f, 0.28f, 0.21f), 0.45f, 0.2f);
            var darkGrey = Mat("Sc04MachGrey", new Color(0.24f, 0.26f, 0.28f), 0.45f, 0.5f);
            var steelR = Sc04Pbr("Sc04MachRollerPbr", "Steel", new Color(0.86f, 0.88f, 0.9f), new Vector2(4f, 1.2f), 1f, 0.5f);
            var cloth = Mat("Sc04MachCloth", new Color(0.80f, 0.74f, 0.60f), 0.08f, 0f);
            var blue = Mat("Sc04MachBlue", new Color(0.10f, 0.30f, 0.62f), 0.6f, 0.35f);
            var panelMat = Mat("Sc04MachPanel", new Color(0.58f, 0.60f, 0.58f), 0.4f, 0.3f);
            var yellow = Mat("Sc04MachYellow", new Color(0.92f, 0.72f, 0.1f), 0.3f, 0f);
            var black = Mat("Sc04MachBlack", new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.1f);
            var redLamp = Mat("Sc04MachRed", new Color(1f, 0.15f, 0.1f), 0.2f, 0f, null, new Color(2f, 0.2f, 0.1f), false);
            var greenLamp = Mat("Sc04MachLampGreen", new Color(0.2f, 1f, 0.3f), 0.2f, 0f, null, new Color(0.3f, 2f, 0.4f), false);
            var screen = Mat("Sc04MachScreen", new Color(0.2f, 0.6f, 1f), 0.2f, 0f, null, new Color(0.3f, 1.1f, 2f), true);
            var x = floorCentre.x;
            var z = floorCentre.z;
            var fr = 0.18f;

            // base: plinth with a lip, feet, hazard strip
            Sc04Part("Plinth", root, new Vector3(x, 0.17f, z), new Vector3(width, 0.34f, length), darkGrey);
            Sc04Part("PlinthLip", root, new Vector3(x, 0.355f, z), new Vector3(width * 1.04f, 0.03f, length * 1.02f), black);
            for (var k = 0; k < 6; k++)
            {
                var hz = z - length * 0.5f + (k + 0.5f) * length / 6f;
                Sc04Part("HazardStripe" + k, root, new Vector3(x + facing * (width * 0.5f + 0.003f), 0.17f, hz),
                    new Vector3(0.006f, 0.13f, length / 12f), k % 2 == 0 ? yellow : black);
            }

            foreach (var sz in new[] { -1f, 1f })
            {
                var fz = z + sz * (length * 0.5f - fr * 0.5f);
                Sc04Part("Frame", root, new Vector3(x, 0.37f + (height - 0.37f) * 0.5f, fz), new Vector3(width, height - 0.37f, fr), green);
                Sc04Part("FrameCap", root, new Vector3(x, height + 0.035f, fz), new Vector3(width * 1.05f, 0.07f, fr * 1.25f), greenDark);
                Sc04Part("FrameRib", root, new Vector3(x + facing * (width * 0.5f + 0.01f), height * 0.55f, fz), new Vector3(0.02f, height * 0.7f, fr * 0.5f), greenDark);
                // bolts on the frame faces
                for (var bx = -1; bx <= 1; bx += 2)
                    for (var by = 0; by < 4; by++)
                    {
                        var bolt = Sc04RollerZ("Bolt", root, new Vector3(x + bx * width * 0.38f, 0.6f + by * (height - 1.0f) / 3f, fz + sz * (fr * 0.5f + 0.008f)),
                            0.014f, 0.022f, darkGrey);
                        bolt.name = "Bolt";
                    }
            }

            var rl = length - fr * 2f;
            // rollers (PBR steel) with end caps and bearing blocks
            var rollers = new[]
            {
                new Vector4(0f, height - 0.38f, 0.21f, 0f),
                new Vector4(0.02f, height - 0.82f, 0.15f, 0f),
                new Vector4(width * 0.22f, height - 1.15f, 0.115f, 0f)
            };
            for (var i = 0; i < rollers.Length; i++)
            {
                var rv = rollers[i];
                Sc04RollerZ("Roller" + i, root, new Vector3(x + rv.x, rv.y, z), rl, rv.z, steelR);
                foreach (var sz in new[] { -1f, 1f })
                {
                    Sc04RollerZ("RollerCap" + i, root, new Vector3(x + rv.x, rv.y, z + sz * (rl * 0.5f + 0.012f)), 0.03f, rv.z * 0.8f, darkGrey);
                    Sc04Part("Bearing", root, new Vector3(x + rv.x, rv.y, z + sz * (rl * 0.5f + 0.045f)), new Vector3(rv.z * 1.7f, rv.z * 1.7f, 0.07f), black);
                }
            }

            Sc04RollerZ(blueRolls ? "BlueRoll" : "ClothRoll", root, new Vector3(x - width * 0.2f, 0.37f + 0.21f, z), rl * 0.92f, 0.21f,
                blueRolls ? blue : cloth);
            // fabric running between the rollers (a few slightly offset sheets so it reads as cloth, not one board)
            Sc04Part("Fabric", root, new Vector3(x + width * 0.06f, height - 0.8f, z), new Vector3(0.012f, 0.66f, rl * 0.9f), cloth);
            Sc04Part("FabricB", root, new Vector3(x + width * 0.075f, height - 0.78f, z), new Vector3(0.008f, 0.6f, rl * 0.86f), cloth);
            Sc04Part("FabricLow", root, new Vector3(x + width * 0.12f, height - 1.55f, z), new Vector3(0.012f, 0.55f, rl * 0.85f), cloth);

            // pipes on top and a riser to the ceiling
            Sc04RollerZ("TopPipe", root, new Vector3(x - width * 0.3f, height + 0.2f, z), rl, 0.045f, darkGrey);
            Sc04RollerZ("TopPipeB", root, new Vector3(x + width * 0.25f, height + 0.17f, z), rl, 0.032f, blue);
            var riser = Cyl("Riser", root, new Vector3(x - width * 0.3f, height + 0.2f, z + length * 0.35f), Sc04HallH - height - 0.45f, 0.04f, darkGrey);
            GameObjectUtility.SetStaticEditorFlags(riser, StaticEditorFlags.BatchingStatic);
            for (var f = 0; f < 3; f++)
                Cyl("RiserFlange", root, new Vector3(x - width * 0.3f, height + 0.9f + f * 0.7f, z + length * 0.35f), 0.03f, 0.065f, blue);

            // guard rail along the aisle side (yellow posts and rails)
            for (var k = 0; k <= 4; k++)
            {
                var gz = z - length * 0.5f + k * length / 4f;
                Cyl("GuardPost", root, new Vector3(x + facing * (width * 0.5f + 0.34f), 0f, gz), 0.95f, 0.022f, yellow);
            }

            Sc04RollerZ("GuardRailTop", root, new Vector3(x + facing * (width * 0.5f + 0.34f), 0.95f, z), length, 0.02f, yellow);
            Sc04RollerZ("GuardRailMid", root, new Vector3(x + facing * (width * 0.5f + 0.34f), 0.5f, z), length, 0.016f, yellow);

            // control cabinet on the aisle side at the near end: screen, lamps, buttons, warning label
            var cz = z - length * 0.5f + 0.42f;
            var cxp = x + facing * (width * 0.5f + 0.1f);
            Sc04Part("Cabinet", root, new Vector3(cxp, 1.05f, cz), new Vector3(0.2f, 1.5f, 0.62f), panelMat);
            Sc04Part("CabinetDoor", root, new Vector3(cxp + facing * 0.103f, 1.05f, cz), new Vector3(0.008f, 1.4f, 0.56f), darkGrey);
            Sc04Part("CabinetScreen", root, new Vector3(cxp + facing * 0.11f, 1.45f, cz), new Vector3(0.008f, 0.26f, 0.4f), screen);
            Sc04Part("LampR", root, new Vector3(cxp + facing * 0.11f, 1.18f, cz - 0.14f), new Vector3(0.01f, 0.05f, 0.05f), redLamp);
            Sc04Part("LampG", root, new Vector3(cxp + facing * 0.11f, 1.18f, cz), new Vector3(0.01f, 0.05f, 0.05f), greenLamp);
            Sc04Part("ButtonA", root, new Vector3(cxp + facing * 0.11f, 1.0f, cz - 0.1f), new Vector3(0.012f, 0.07f, 0.07f), redLamp);
            Sc04Part("ButtonB", root, new Vector3(cxp + facing * 0.11f, 1.0f, cz + 0.1f), new Vector3(0.012f, 0.07f, 0.07f), black);
            Sc04Part("WarnLabel", root, new Vector3(cxp + facing * 0.11f, 0.75f, cz), new Vector3(0.006f, 0.16f, 0.3f), yellow);
        }

        private static void Sc04TextileMachines(Transform room, float ox, float x1)
        {
            // along the left wall (viewer's left, +X): a row of calenders, panels toward the aisle
            var zs = new[] { -6.2f, -9.6f, -13.0f };
            for (var i = 0; i < zs.Length; i++)
                Sc04Calender(room, "Calender" + i, new Vector3(x1 - 0.75f, 0f, zs[i]), 3.0f, 1.1f, 2.0f + (i % 2) * 0.1f, -1f, i == 1);
            // a second, lower row in the middle distance (blue cylinders) so the hall behind the table is not empty
            var zs2 = new[] { -7.0f, -10.6f };
            for (var i = 0; i < zs2.Length; i++)
                Sc04Calender(room, "CalenderB" + i, new Vector3(ox + 0.9f, 0f, zs2[i]), 3.2f, 1.2f, 1.7f, 1f, i == 0);
            // a roll trolley and stacked cloth rolls near the first machine
            var cloth = Mat("Sc04MachCloth", new Color(0.80f, 0.74f, 0.60f), 0.08f, 0f);
            var blue = Mat("Sc04MachBlue", new Color(0.10f, 0.30f, 0.62f), 0.55f, 0.35f);
            for (var i = 0; i < 3; i++)
                Sc04RollerZ("LooseRoll" + i, room, new Vector3(ox + 1.5f + i * 0.45f, 0.2f, -15.0f), 1.0f, 0.2f, i == 1 ? blue : cloth);
        }

        // Pipes along the ceiling and vertical risers with valve wheels (grey and blue), as in the photo.
        private static void Sc04OverheadPipes(Transform room, float ox, float h)
        {
            var grey = Mat("Sc04PipeGrey", new Color(0.55f, 0.57f, 0.58f), 0.5f, 0.5f);
            var blue = Mat("Sc04PipeBlue", new Color(0.12f, 0.28f, 0.58f), 0.5f, 0.4f);
            var red = Mat("Sc04PipeWheel", new Color(0.8f, 0.12f, 0.1f), 0.4f, 0.2f);
            for (var i = 0; i < 4; i++)
            {
                var px = ox - 0.4f + i * 0.45f;
                var py = h - 0.45f - (i % 2) * 0.18f;
                var run = Sc04RollerZ("PipeRun" + i, room, new Vector3(px, py, -7f), 22f, 0.075f - 0.01f * (i % 2), i % 3 == 1 ? blue : grey);
                run.name = "PipeRun" + i;
            }

            // vertical risers with a blue valve block and a red wheel (left and centre of the far hall)
            var risers = new[] { new Vector2(ox + 0.1f, -3.2f), new Vector2(ox - 0.9f, -5.5f), new Vector2(ox + 0.2f, -9.5f) };
            for (var i = 0; i < risers.Length; i++)
            {
                var r = risers[i];
                var pipe = Cyl("Riser" + i, room, new Vector3(r.x, 0f, r.y), h - 0.4f, 0.07f, grey);
                GameObjectUtility.SetStaticEditorFlags(pipe, StaticEditorFlags.BatchingStatic);
                Cyl("RiserValve" + i, room, new Vector3(r.x, 2.1f, r.y), 0.32f, 0.11f, blue);
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "ValveWheel" + i;
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(room, true);
                wheel.transform.position = new Vector3(r.x + 0.18f, 2.25f, r.y);
                wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.22f, 0.015f, 0.22f);
                wheel.GetComponent<MeshRenderer>().sharedMaterial = red;
            }
        }

        private static Material Sc04ClearGlass(string name, Color c)
        {
            var m = Mat(name, c, 1f, 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // section of the jug at height fraction: rounded square with exponent n (4 = squarish, 2 = round), half width hw
        private static float JugR(float theta, float hw, float n)
        {
            var c = Mathf.Abs(Mathf.Cos(theta));
            var sn = Mathf.Abs(Mathf.Sin(theta));
            return hw / Mathf.Pow(Mathf.Pow(c, n) + Mathf.Pow(sn, n), 1f / n);
        }

        // profile: (height metres, scale 0..1 of the body half width). A rib / the water use a prefix of it.
        private static readonly Vector2[] JugProfile =
        {
            new Vector2(0.000f, 0.84f), new Vector2(0.008f, 0.94f), new Vector2(0.022f, 0.99f), new Vector2(0.040f, 1.00f),
            new Vector2(0.200f, 1.00f), new Vector2(0.236f, 0.985f), new Vector2(0.262f, 0.93f), new Vector2(0.286f, 0.80f),
            new Vector2(0.308f, 0.60f), new Vector2(0.326f, 0.40f), new Vector2(0.340f, 0.30f), new Vector2(0.352f, 0.27f),
            new Vector2(0.366f, 0.27f), new Vector2(0.372f, 0.31f), new Vector2(0.388f, 0.31f), new Vector2(0.395f, 0.29f)
        };

        // Lathe mesh of the jug body up to 'height'. ribOnly (height small) makes a thin band at the placed height.
        private static GameObject Sc04JugMesh(string name, Transform parent, Vector3 origin, float hw, float widthScale, float height,
            bool closeTop, Material mat)
        {
            const int seg = 56;
            var rows = new List<Vector2>();
            if (height > 0.05f)
            {
                foreach (var pnt in JugProfile)
                {
                    if (pnt.x <= height + 0.0001f)
                        rows.Add(pnt);
                }

                if (rows.Count > 0 && rows[rows.Count - 1].x < height)
                {
                    // cut inside a segment: interpolate
                    var a = rows[rows.Count - 1];
                    Vector2 b = a;
                    foreach (var pnt in JugProfile)
                    {
                        if (pnt.x > height)
                        {
                            b = pnt;
                            break;
                        }
                    }

                    var k = (height - a.x) / Mathf.Max(0.0001f, b.x - a.x);
                    rows.Add(new Vector2(height, Mathf.Lerp(a.y, b.y, k)));
                }
            }
            else
            {
                // a thin ring band around the straight body
                rows.Add(new Vector2(0f, 1f));
                rows.Add(new Vector2(height, 1f));
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var yOff = 0f;
            for (var r = 0; r < rows.Count; r++)
            {
                var sc = rows[r].y;
                var n = Mathf.Lerp(2f, 4f, Mathf.SmoothStep(0.30f, 0.90f, sc));
                for (var i = 0; i <= seg; i++)
                {
                    var th = i / (float)seg * Mathf.PI * 2f;
                    var rad = JugR(th, hw * widthScale, n) * sc;
                    verts.Add(new Vector3(Mathf.Sin(th) * rad, rows[r].x + yOff, Mathf.Cos(th) * rad));
                    uvs.Add(new Vector2(i / (float)seg, rows[r].x));
                }
            }

            for (var r = 0; r < rows.Count - 1; r++)
            {
                for (var i = 0; i < seg; i++)
                {
                    var a = r * (seg + 1) + i;
                    var b = a + 1;
                    var c = a + (seg + 1);
                    var d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            if (closeTop)
            {
                var top = rows[rows.Count - 1];
                var centre = verts.Count;
                verts.Add(new Vector3(0f, top.x, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                var lastRow = (rows.Count - 1) * (seg + 1);
                for (var i = 0; i < seg; i++)
                    tris.AddRange(new[] { lastRow + i, lastRow + i + 1, centre });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // Custom reflection probe: the table, the jug glass and the rollers reflect the hall of the client's photo.
        private static void Sc04ReflectionProbe(Transform room, Vector3 at)
        {
            var path = ArtRoot + "/2D/Sc04/Photo_ReflectionCross.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureShape != TextureImporterShape.TextureCube)
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }

            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null)
            {
                Debug.LogWarning("[Presentable] Photo_ReflectionCross.png is not a cubemap: run Tools/make_sc04_photo_textures.py");
                return;
            }

            var go = new GameObject("Sc04ReflectionProbe");
            go.transform.SetParent(room, false);
            go.transform.position = at;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Custom;
            rp.customBakedTexture = cube;
            rp.size = new Vector3(12f, 6f, 24f);
            rp.boxProjection = false;
            rp.intensity = 1.35f;
            rp.importance = 10;
        }

        // The jug handle: a flat moulded "D" loop (extruded ring) beside the neck, joined to the neck collar and the shoulder.
        private static void Sc04JugHandleRing(Transform parent, Vector3 origin, Material mat)
        {
            const int n = 40;
            var cxr = 0.082f;
            var cyr = 0.352f;
            float ox = 0.062f, oy = 0.056f, ix = 0.040f, iy = 0.034f, d = 0.016f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (var i = 0; i <= n; i++)
            {
                var a = i / (float)n * Mathf.PI * 2f;
                var c = Mathf.Cos(a);
                var s2 = Mathf.Sin(a);
                var o = new Vector2(cxr + ox * c, cyr + oy * s2);
                var inn = new Vector2(cxr + ix * c, cyr + iy * s2);
                verts.Add(new Vector3(o.x, o.y, d));     // 0 outer front
                verts.Add(new Vector3(inn.x, inn.y, d)); // 1 inner front
                verts.Add(new Vector3(o.x, o.y, -d));    // 2 outer back
                verts.Add(new Vector3(inn.x, inn.y, -d)); // 3 inner back
            }

            for (var i = 0; i < n; i++)
            {
                var k = i * 4;
                var m = k + 4;
                // front face, back face, outer wall, inner wall
                tris.AddRange(new[] { k, m, k + 1, m, m + 1, k + 1 });
                tris.AddRange(new[] { k + 2, k + 3, m + 2, m + 2, k + 3, m + 3 });
                tris.AddRange(new[] { k, k + 2, m, m, k + 2, m + 2 });
                tris.AddRange(new[] { k + 1, m + 1, k + 3, m + 1, m + 3, k + 3 });
            }

            var mesh = new Mesh { name = "JugHandleRing" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("JugHandleRing");
            go.transform.SetParent(parent, true);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // (old open bezier handle, no longer used) moulded handle: a bezier loop from the neck over the shoulder, built from short boxes
        private static void Sc04JugHandle(Transform parent, Vector3 origin, Material mat)
        {
            var p0 = new Vector3(0.040f, 0.372f, 0f);
            var p1 = new Vector3(0.075f, 0.455f, 0f);
            var p2 = new Vector3(0.145f, 0.44f, 0f);
            var p3 = new Vector3(0.132f, 0.300f, 0f);
            const int n = 14;
            var prev = p0;
            for (var i = 1; i <= n; i++)
            {
                var t = i / (float)n;
                var u = 1f - t;
                var pt = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                var mid = (prev + pt) * 0.5f;
                var dir = pt - prev;
                var seg = Box("JugHandle" + i, parent, origin + mid, new Vector3(0.016f, dir.magnitude + 0.004f, 0.030f), mat, false);
                seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                prev = pt;
            }
        }

        // label that hugs the squarish front face of the jug (same section function, a hair outside)
        private static void Sc04JugLabel(Transform parent, Vector3 origin, float hw, float y0, float y1, Material mat)
        {
            const int seg = 28;
            var half = 52f * Mathf.Deg2Rad; // the wide HCl label (3:2) wraps the squarish front of the jug
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var t = i / (float)seg;
                var th = Mathf.Lerp(-half, half, t);
                var rad = JugR(th, hw, 4f) + 0.0015f;
                var pos = new Vector3(Mathf.Sin(th) * rad, 0f, Mathf.Cos(th) * rad);
                verts.Add(origin + pos + Vector3.up * y0);
                verts.Add(origin + pos + Vector3.up * y1);
                uvs.Add(new Vector2(1f - t, 0f));
                uvs.Add(new Vector2(1f - t, 1f));
            }

            for (var i = 0; i < seg; i++)
            {
                var k = i * 2;
                tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }

            var mesh = new Mesh { name = "BottleLabel" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("BottleLabel");
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // Table dressing: the raised back edge, a clear tray and the HCl jug behind the gloves.
        private static void BuildSc04FactoryProps(Transform bench, float tableTop, Bounds table, Vector3 clothAt)
        {
            var steel = Mat("Sc06SteelPlain", new Color(0.76f, 0.78f, 0.81f), 0.72f, 0.15f);
            var backZ = table.min.z;
            // raised back edge, like the lip of the client's table
            Box("TableBackLip", bench, new Vector3(table.center.x, tableTop + 0.06f, backZ + 0.02f), new Vector3(table.size.x, 0.12f, 0.035f), steel, false);
            Box("TableSideLipL", bench, new Vector3(table.max.x - 0.02f, tableTop + 0.05f, table.center.z), new Vector3(0.035f, 0.1f, table.size.z), steel, false);

            if (Sc04UsePhotoBackdrop)
                Sc04PhotoDome(bench, new Vector3(clothAt.x, 0f, clothAt.z + 0.85f), tableTop, table.min.z);
            Sc04ReflectionProbe(bench.parent, new Vector3(clothAt.x, 1.6f, clothAt.z));
            // rolled front edge and side trim: catches the light like real stainless
            var edgeMat = Mat("Sc04TableEdge", new Color(0.82f, 0.84f, 0.87f), 0.9f, 0.8f);
            Box("TableFrontRoll", bench, new Vector3(table.center.x, tableTop - 0.005f, table.max.z), new Vector3(table.size.x, 0.034f, 0.034f), edgeMat, false);
            Box("TableSideRoll", bench, new Vector3(table.min.x, tableTop - 0.005f, table.center.z), new Vector3(0.034f, 0.034f, table.size.z), edgeMat, false);
            var glass = LabGlass();
            var cx = clothAt.x;
            var cz = backZ + 0.36f;

            // clear plastic tray
            var tray = new GameObject("AcidTray").transform;
            tray.SetParent(bench, false);
            tray.position = new Vector3(cx, tableTop, cz);
            const float tw = 0.46f, td = 0.32f, th = 0.07f, tt = 0.006f;
            Box("TrayBase", tray, new Vector3(cx, tableTop + tt * 0.5f, cz), new Vector3(tw, tt, td), glass, false);
            Box("TrayWallF", tray, new Vector3(cx, tableTop + th * 0.5f, cz + td * 0.5f), new Vector3(tw, th, tt), glass, false);
            Box("TrayWallB", tray, new Vector3(cx, tableTop + th * 0.5f, cz - td * 0.5f), new Vector3(tw, th, tt), glass, false);
            Box("TrayWallL", tray, new Vector3(cx + tw * 0.5f, tableTop + th * 0.5f, cz), new Vector3(tt, th, td), glass, false);
            Box("TrayWallR", tray, new Vector3(cx - tw * 0.5f, tableTop + th * 0.5f, cz), new Vector3(tt, th, td), glass, false);
            tray.localScale = Vector3.one * 1.7f;

            // the 5 litre jug: a lathe-turned crystal-clear body (rounded square section, soft shoulders, neck), clear water inside,
            // ribbed blue cap with a moulded handle, label that follows the curved front (+Z, toward the learner)
            var jug = new GameObject("AcidBottle").transform;
            jug.SetParent(bench, false);
            var baseY = tableTop + tt;
            jug.position = new Vector3(cx, baseY, cz); // scaled about its base at the end
            var blueCap = Mat("Sc04AcidCap", new Color(0.05f, 0.20f, 0.85f), 0.7f, 0.05f);
            var crystal = Sc04ClearGlass("Sc04JugCrystal", new Color(0.92f, 0.97f, 1f, 0.055f));
            var water = Sc04ClearGlass("Sc04JugWater", new Color(0.82f, 0.93f, 1f, 0.07f));
            var hw = 0.108f;
            var outer = Sc04JugMesh("JugShell", jug, new Vector3(cx, baseY, cz), hw, 1f, 0.395f, false, crystal);
            var inner = Sc04JugMesh("JugWater", jug, new Vector3(cx, baseY + 0.004f, cz), hw, 0.93f, 0.292f, true, water);
            // ribs on the body (thin clear rings, catch the light)
            for (var rb = 0; rb < 3; rb++)
            {
                var rib = Sc04JugMesh("JugRib" + rb, jug, new Vector3(cx, baseY + 0.07f + rb * 0.07f, cz), hw, 1.012f, 0.012f, false, crystal);
                rib.name = "JugRib" + rb;
            }

            var capY = baseY + 0.372f;
            Cyl("JugCap", jug, new Vector3(cx, capY, cz), 0.034f, 0.040f, blueCap);
            for (var rg = 0; rg < 5; rg++)
                Cyl("JugCapRidge" + rg, jug, new Vector3(cx, capY + 0.004f + rg * 0.0065f, cz), 0.003f, 0.0425f, blueCap);
            Cyl("JugCapRing", jug, new Vector3(cx, capY - 0.012f, cz), 0.012f, 0.047f, blueCap);
            Sc04JugHandleRing(jug, new Vector3(cx, baseY, cz), blueCap);

            var labelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/BottleLabelWide.png");
            var labelMat = Mat("Sc04BottleLabelUnlit", new Color(0.97f, 0.97f, 0.98f), 0f, 0f, labelTex, null, true, true);
            Sc04JugLabel(jug, new Vector3(cx, baseY, cz), hw, 0.05f, 0.195f, labelMat);
            jug.localScale = Vector3.one * 1.7f; // reads big from the stand, as in the client's photo
        }

        // A curved label (part of a cylinder) centred on the +Z side, UV 0..1.
        private static void ArcLabel(string name, Transform parent, Vector3 centre, float radius, float height, float arcDeg, Material mat)
        {
            const int seg = 24;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var half = arcDeg * 0.5f * Mathf.Deg2Rad;
            for (var i = 0; i <= seg; i++)
            {
                var t = i / (float)seg;
                var a = Mathf.Lerp(-half, half, t);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // viewed from +Z the left of the image is +X, so u = 1 - t
                verts.Add(centre + dir * radius + Vector3.down * (height * 0.5f));
                verts.Add(centre + dir * radius + Vector3.up * (height * 0.5f));
                uvs.Add(new Vector2(1f - t, 0f));
                uvs.Add(new Vector2(1f - t, 1f));
            }

            for (var i = 0; i < seg; i++)
            {
                var k = i * 2;
                tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
#endif

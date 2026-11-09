#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>Sprite borders of the Supervisor Check UI pictures (Resources/ScCheck) and tiling of its textures.</summary>
    public sealed class ScCheckImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Resources/ScCheck/"))
            {
                var n = Path.GetFileNameWithoutExtension(assetPath);
                if (n.StartsWith("ui_") || n.StartsWith("sds"))
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.maxTextureSize = 2048;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.textureCompression = n.StartsWith("sds") ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ; // the SDS sheets are small text pictures: no block artefacts
                    ti.spriteBorder = n == "ui_screen" ? new Vector4(70f, 70f, 70f, 70f)
                        : n == "ui_popup" ? new Vector4(80f, 80f, 80f, 80f)
                        : n == "ui_sds_frame" ? new Vector4(50f, 50f, 50f, 50f)
                        : Vector4.zero;
                }
                else
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.maxTextureSize = 2048;
                    ti.mipmapEnabled = true;
                }
            }
            else if (assetPath.StartsWith("Assets/Art/Presentable/2D/ScCheckTex/"))
            {
                if (assetPath.EndsWith("_Normal.png"))
                    ti.textureType = TextureImporterType.NormalMap;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.maxTextureSize = 1024;
                ti.mipmapEnabled = true;
            }
        }
    }

    /// <summary>
    /// "Supervisor Pre-Task Safety Check" hall, shared by scenarios 13-18 (origin x = 540). An L-shaped brick hall: the main bay with the
    /// brick back and side walls (SDS poster, control box, tablet), a steel table with the chemical jug, work order, clipboard and glassware,
    /// the lab supervisor beside it; the left wing holds the Hydro-Press machines, cabinets, drums and a window bank. Authored in
    /// player space (r = right, f = forward from the spawn): the player looks along world -Z, so r maps to -X.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const float ScOx = 540f;
        private const float ScZ0 = 3.2f;
        private const string ScTexDir = ArtRoot + "/2D/ScCheckTex";
        private const string ScHydroPress = ArtRoot + "/3D/ScCheck/HydroPress.glb";

        private sealed class ScCheckRefs
        {
            public Transform spawn;
            public Transform stand;
            public ScCheckRunner runner;
        }

        // player space -> world
        private static Vector3 ScP(float r, float y, float f) => new Vector3(ScOx - r, y, ScZ0 - f);

        private static Texture2D ScTex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ScTexDir + "/" + name + ".png");

        private static Texture2D ScResTex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ScCheck/" + name + ".png");

        // a box given in player space by its centre and (width along r, height, depth along f)
        private static GameObject ScBox(string name, Transform parent, float r, float y, float f, float w, float h, float d, Material mat, bool collider = false)
        {
            var go = Box(name, parent, ScP(r, y, f), new Vector3(w, h, d), mat, collider);
            return go;
        }

        // flat picture facing the player (towards +Z in the world)
        private static GameObject ScPicture(string name, Transform parent, Vector3 pos, Vector2 size, Material mat, float yaw = 180f)
        {
            var q = QuadObj(name, parent, pos, new Vector3(0f, yaw, 0f), size, mat);
            q.transform.SetParent(parent, true);
            return q;
        }

        private static Material ScEmissive(string name, Color color, float strength)
        {
            return Mat(name, color * 0.3f, 0.2f, 0f, null, color * strength);
        }

        private static ScCheckRefs BuildScCheckRoom(Transform env, PresentableArt art)
        {
            if (ScCheckUseFactory && _scCheckHall != null)
                return BuildScCheckFactory(env, art); // the client's factory room (copy of the Scenario 6 room); the old brick hall below stays restorable
            var refs = new ScCheckRefs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);

            // mood: bright dusty grey like the client's factory photo, haze only far away
            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(1.0f, 0.99f, 0.96f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.84f, 0.83f, 0.80f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.40f, 0.39f, 0.37f);
            mso.FindProperty("fogColor").colorValue = new Color(0.74f, 0.75f, 0.74f);
            mso.FindProperty("fogStart").floatValue = 9f;
            mso.FindProperty("fogEnd").floatValue = 46f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.5f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // factory materials: dirty plaster walls, concrete floor, pale ceiling (same texture set as SC06 factory)
            var wallMat = Sc01Pbr("ScCheckFacWall", "Wall", new Color(1.55f, 1.55f, 1.52f), 0.12f, 1f);
            var floorMat = Sc01Pbr("ScCheckFacFloor", "Wall", new Color(0.80f, 0.77f, 0.70f), 0.25f, 1f);
            var ceilMat = Sc01Pbr("ScCheckFacCeil", "Wall", new Color(1.05f, 1.04f, 1.0f), 0.1f, 0.9f);
            var bandMat = Sc01Pbr("ScCheckFacBand", "Wall", new Color(0.60f, 0.70f, 0.80f), 0.2f, 1f);
            var steel = Mat("ScCheckSteel", new Color(0.82f, 0.84f, 0.87f), 0.65f, 0.55f);
            var darkSteel = Mat("ScCheckDarkSteel", new Color(0.24f, 0.26f, 0.29f), 0.5f, 0.7f);
            var yellow = Mat("ScCheckYellow", new Color(0.95f, 0.66f, 0.08f), 0.45f, 0.2f);
            var blue = Mat("ScCheckBlueCab", new Color(0.12f, 0.32f, 0.62f), 0.45f, 0.1f);
            var white = Mat("ScCheckWhite", new Color(0.92f, 0.93f, 0.94f), 0.65f, 0f);
            var skyGlow = Mat("ScCheckSky", new Color(0.80f, 0.88f, 0.96f), 0.1f, 0f, null, new Color(1.05f, 1.15f, 1.3f));
            var solidLayer = SolidLayer;

            const float H = 7.0f;
            // the table alcove (brick wall at f 4.6, r 0.2..5) and a hall that goes on to the left and far back, like the picture
            const float rL = -10f, rR = 5.0f, fNear = -3.0f, fFar = 4.6f, fBack = 14.5f;

            // ---------------------------------------------------------------- floor, ceiling
            var floor = ScBox("Floor_Main", room, (rL + rR) * 0.5f, -0.05f, (fNear + fBack) * 0.5f, rR - rL, 0.1f, fBack - fNear, floorMat, true);
            floor.AddComponent<TeleportationArea>();
            if (solidLayer >= 0)
                floor.layer = solidLayer;
            ScBox("Ceiling_Main", room, (rL + rR) * 0.5f, H + 0.05f, (fNear + fBack) * 0.5f, rR - rL, 0.1f, fBack - fNear, ceilMat);

            // ---------------------------------------------------------------- walls (dirty plaster, factory style)
            var walls = new List<GameObject>
            {
                ScBox("Wall_FarBrick", room, (0.9f + rR) * 0.5f, H * 0.5f, fFar + 0.1f, rR - 0.9f + 0.2f, H, 0.2f, wallMat),
                ScBox("Wall_Return", room, 1.0f, H * 0.5f, (fFar + fBack) * 0.5f, 0.2f, H, fBack - fFar, wallMat),
                ScBox("Wall_RightBrick", room, rR + 0.1f, H * 0.5f, (fNear + fFar) * 0.5f, 0.2f, H, fFar - fNear + 0.4f, wallMat),
                ScBox("Wall_Behind", room, (rL + rR) * 0.5f, H * 0.5f, fNear - 0.1f, rR - rL + 0.4f, H, 0.2f, wallMat),
                ScBox("Wall_Left", room, rL - 0.1f, H * 0.5f, (fNear + fBack) * 0.5f, 0.2f, H, fBack - fNear + 0.4f, wallMat),
                ScBox("Wall_Back", room, (rL + 0.4f) * 0.5f, H * 0.5f, fBack + 0.1f, 0.4f - rL + 0.2f, H, 0.2f, wallMat)
            };
            foreach (var w in walls)
            {
                w.isStatic = true;
                if (solidLayer >= 0)
                    w.layer = solidLayer;
            }

            // pale-blue band at the bottom of every wall (like the factory photo)
            var bandEdge = Mat("ScCheckFacBandEdge", new Color(0.30f, 0.34f, 0.38f), 0.2f, 0f);
            ScBox("BandFar", room, (0.9f + rR) * 0.5f, 0.55f, fFar + 0.006f, rR - 0.9f, 1.1f, 0.012f, bandMat);
            ScBox("BandRight", room, rR + 0.006f, 0.55f, (fNear + fFar) * 0.5f, 0.012f, 1.1f, fFar - fNear, bandMat);
            ScBox("BandBehind", room, (rL + rR) * 0.5f, 0.55f, fNear - 0.006f, rR - rL, 1.1f, 0.012f, bandMat);
            ScBox("BandLeft", room, rL - 0.006f, 0.55f, (fNear + fBack) * 0.5f, 0.012f, 1.1f, fBack - fNear, bandMat);
            ScBox("BandEdgeFar", room, (0.9f + rR) * 0.5f, 1.105f, fFar + 0.008f, rR - 0.9f, 0.02f, 0.016f, bandEdge);
            ScBox("BandEdgeRight", room, rR + 0.008f, 1.105f, (fNear + fFar) * 0.5f, 0.016f, 0.02f, fFar - fNear, bandEdge);
            ScBox("BandEdgeBehind", room, (rL + rR) * 0.5f, 1.105f, fNear - 0.008f, rR - rL, 0.02f, 0.016f, bandEdge);
            ScBox("BandEdgeLeft", room, rL - 0.008f, 1.105f, (fNear + fBack) * 0.5f, 0.016f, 0.02f, fBack - fNear, bandEdge);

            // the far end of the hall is the client's own picture (machines, windows, crane, roof); the real 3D hall stands in front of it
            var backTex = Resources.Load<Texture2D>("ScCheck/backdrop");
            if (backTex == null)
                backTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ScCheck/backdrop.png");
            if (backTex != null)
            {
                var backMat = Mat("ScCheckBackdrop", Color.white, 0f, 0f, backTex, null, true);
                var back = ScPicture("Backdrop", room, ScP(-4.8f, 2.3f, fBack - 0.2f), new Vector2(9.8f, 8.06f), backMat);
                back.transform.SetParent(room, true);
            }

            // tall window banks on the left wall and high windows on the back wall (the light comes from the left, like the picture)
            for (var i = 0; i < 6; i++)
                ScWindow(room, rL + 0.2f, 3.1f, 0.8f + i * 2.5f, 1.7f, 3.6f, steel, skyGlow, 270f, true);

            // ---------------------------------------------------------------- roof trusses, yellow crane beams, columns, lamps
            for (var i = 0; i < 6; i++)
            {
                var f = fNear + 0.8f + i * 3.1f;
                ScBox("Truss_Top" + i, room, (rL + rR) * 0.5f, H - 0.55f, f, rR - rL, 0.16f, 0.16f, darkSteel);
                for (var k = 0; k < 7; k++)
                {
                    var rr = rL + 1.0f + k * 2.2f;
                    var diag = ScBox("Truss_Brace" + i + "_" + k, room, rr, H - 0.9f, f, 0.08f, 0.9f, 0.08f, darkSteel);
                    diag.transform.rotation = Quaternion.Euler(0f, 0f, k % 2 == 0 ? 38f : -38f);
                }
            }

            foreach (var cf in new[] { 3.2f, 9.6f })
            {
                ScBox("CraneBeam", room, (rL + rR) * 0.5f, 5.5f, cf, rR - rL - 0.4f, 0.55f, 0.5f, yellow);
                ScBox("CraneBeamEdge", room, (rL + rR) * 0.5f, 5.2f, cf, rR - rL - 0.4f, 0.06f, 0.62f, yellow);
                ScBox("CraneRail", room, -2.5f, 5.95f, cf, 0.7f, 0.22f, 0.8f, darkSteel);
                ScBox("CraneHook", room, -2.5f, 5.0f, cf, 0.06f, 0.8f, 0.06f, darkSteel);
            }

            var columnMat = Mat("ScCheckColumn", new Color(0.30f, 0.38f, 0.48f), 0.45f, 0.5f);
            for (var i = 0; i < 4; i++)
                ScBox("Column" + i, room, rL + 0.3f, H * 0.5f, 2.2f + i * 3.6f, 0.4f, H, 0.4f, columnMat);

            // fluorescent tube fixtures hanging from chains (like the factory photo)
            var tubeMat = Mat("ScCheckFacTube", new Color(0.95f, 0.97f, 0.92f), 0.2f, 0f, null, new Color(2.4f, 2.4f, 2.3f), false);
            var housing = Mat("ScCheckFacHousing", new Color(0.56f, 0.57f, 0.57f), 0.4f, 0.3f);
            var tubePositions = new[]
            {
                new Vector2(-7.5f, 1f), new Vector2(-4f, 1f), new Vector2(-0.5f, 1f), new Vector2(3f, 1f),
                new Vector2(-7.5f, 4.5f), new Vector2(-4f, 4.5f), new Vector2(-0.5f, 4.5f), new Vector2(3f, 4.5f),
                new Vector2(-7.5f, 8f), new Vector2(-4f, 8f), new Vector2(-0.5f, 8f),
                new Vector2(-7.5f, 11.5f), new Vector2(-4f, 11.5f)
            };
            for (var i = 0; i < tubePositions.Length; i++)
            {
                var p = ScP(tubePositions[i].x, H - 0.9f, tubePositions[i].y);
                ScBox("TubeChainA" + i, room, tubePositions[i].x, H - 0.5f, tubePositions[i].y, 0.012f, 0.8f, 0.012f, darkSteel);
                var hg = Box("TubeHousing" + i, room, p + Vector3.up * 0.04f, new Vector3(2.2f, 0.05f, 0.18f), housing, false);
                Box("TubeA" + i, room, p - new Vector3(0f, 0f, 0.04f), new Vector3(2.1f, 0.04f, 0.04f), tubeMat, false);
                Box("TubeB" + i, room, p + new Vector3(0f, 0f, 0.04f), new Vector3(2.1f, 0.04f, 0.04f), tubeMat, false);
                var angle = i % 2 == 0 ? 20f : -14f;
                hg.transform.RotateAround(p, Vector3.up, angle);
            }

            ScLight(room, "Light_Table", ScP(1.4f, 4.2f, 3.4f), 9f, 4.2f, new Color(0.94f, 0.96f, 0.92f));
            ScLight(room, "Light_Sup", ScP(-1.4f, 4.0f, 2.6f), 8f, 3.2f, new Color(0.94f, 0.96f, 0.92f));
            ScLight(room, "Light_Deep", ScP(-4f, 5.0f, 9f), 12f, 3.4f, new Color(0.92f, 0.96f, 1f));
            ScLight(room, "Light_Player", ScP(0f, 3.4f, -0.8f), 7f, 2.2f, new Color(0.94f, 0.96f, 0.92f));

            // floor markings: the yellow safety line runs in from the left foreground, a keep-out frame around the table
            var line = ScBox("SafetyLine", room, -2.7f, 0.006f, 1.6f, 0.14f, 0.012f, 12f, yellow);
            line.transform.rotation = Quaternion.Euler(0f, 62f, 0f);
            line.transform.position = ScP(-3.2f, 0.006f, 1.2f);
            ScBox("TableFrameFront", room, 1.8f, 0.006f, 2.9f, 2.6f, 0.012f, 0.1f, yellow);
            ScBox("TableFrameLeft", room, 0.5f, 0.006f, 3.75f, 0.1f, 0.012f, 1.7f, yellow);

            // ---------------------------------------------------------------- props (real 3D) and the machines
            ScBuildProps(room, fFar, white, steel, darkSteel, yellow, blue, out var labelRenderer, out var posterRenderer, out var benchHeight, out var posterT);
            // the client's Hydro-Press.glb (Assets/Art/Presentable/3D/ScCheck/HydroPress.glb, made by Tools/prepare_sccheck_machines.py) is kept, but the
            // machines in the hall come from the workflow picture backdrop: set this to true to place the 3D model in the left foreground as well
            const bool placeHydroPress3D = true;
            if (placeHydroPress3D)
                ScMachines(room, steel, darkSteel, yellow, blue, white);

            // Supervisor removed from SC13-18 (client request 2026-11-09)
            LabSupervisor supervisor = null;

            // ---------------------------------------------------------------- markers + runner
            var supStart = Marker("SupervisorStart", room, ScP(-0.35f, 0f, 9.5f), 0f);
            var supStand = Marker("SupervisorStand", room, ScP(-0.35f, 0f, 3.2f), 0f);
            var supSide = Marker("SupervisorSide", room, ScP(-1.1f, 0f, 3.6f), 0f); // just left of the question panel (which sits a little right of the player), in view
            refs.spawn = Marker("Spawn_SCCHECK", room, ScP(0.6f, 0f, 2.3f), 180f);
            refs.stand = Marker("Stand_SCCHECK", room, ScP(0.6f, 0f, 2.3f), 180f);
            var host = new GameObject("ScCheckHost");
            host.transform.SetParent(room, false);
            host.transform.position = ScP(0f, 0f, 0f);
            refs.runner = host.AddComponent<ScCheckRunner>();
            var rso = new SerializedObject(refs.runner);
            rso.FindProperty("art").objectReferenceValue = art;
            rso.FindProperty("supervisor").objectReferenceValue = supervisor;
            rso.FindProperty("labelRenderer").objectReferenceValue = labelRenderer;
            rso.FindProperty("posterRenderer").objectReferenceValue = posterRenderer;
            rso.FindProperty("pointTarget").objectReferenceValue = posterT;
            rso.FindProperty("bench").objectReferenceValue = benchHeight;
            rso.FindProperty("supStart").objectReferenceValue = supStart;
            rso.FindProperty("supStand").objectReferenceValue = supStand;
            rso.FindProperty("supSide").objectReferenceValue = supSide;
            rso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }

        private const string ScCheckOnlyKey = "ChemSafetyScCheckOnly";

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 13 (check)")]
        private static void ToggleScCheckOnly()
        {
            EditorPrefs.SetBool(ScCheckOnlyKey, !EditorPrefs.GetBool(ScCheckOnlyKey, false));
        }

        [MenuItem("Chemical Safety/Presentable/Dev: start at Scenario 13 (check)", true)]
        private static bool ToggleScCheckOnlyCheck()
        {
            Menu.SetChecked("Chemical Safety/Presentable/Dev: start at Scenario 13 (check)", EditorPrefs.GetBool(ScCheckOnlyKey, false));
            return true;
        }

        private static void ScLight(Transform parent, string name, Vector3 pos, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
        }

        private static void ScWindow(Transform room, float r, float y, float f, float w, float h, Material frame, Material glow, float yaw, bool onSideWall)
        {
            // frame bars around an emissive glass pane; side-wall windows (the pane faces +X) are given by their far edge r
            var pos = ScP(r, y, f);
            var pane = QuadObj("WindowPane", room, pos, new Vector3(0f, yaw, 0f), onSideWall ? new Vector2(w, h) : new Vector2(w, h), glow);
            pane.transform.SetParent(room, true);
            var t = 0.07f;
            if (onSideWall)
            {
                ScBox("WinTop", room, r, y + h * 0.5f, f, 0.1f, t, w, frame);
                ScBox("WinBottom", room, r, y - h * 0.5f, f, 0.1f, t, w, frame);
                ScBox("WinMid", room, r, y, f, 0.1f, t, w, frame);
                ScBox("WinL", room, r, y, f - w * 0.5f, 0.1f, h, t, frame);
                ScBox("WinR", room, r, y, f + w * 0.5f, 0.1f, h, t, frame);
            }
            else
            {
                ScBox("WinTop", room, r, y + h * 0.5f, f, w, t, 0.1f, frame);
                ScBox("WinBottom", room, r, y - h * 0.5f, f, w, t, 0.1f, frame);
                ScBox("WinMid", room, r, y, f, w, t, 0.1f, frame);
                ScBox("WinL", room, r - w * 0.5f, y, f, t, h, 0.1f, frame);
                ScBox("WinR", room, r + w * 0.5f, y, f, t, h, 0.1f, frame);
                ScBox("WinCenter", room, r, y, f, t, h, 0.1f, frame);
            }
        }

        private static void ScMachines(Transform room, Material steel, Material darkSteel, Material yellow, Material blue, Material white)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ScHydroPress);
            if (asset == null)
            {
                Debug.LogWarning("[ScCheck] Hydro-Press model missing (" + ScHydroPress + "); the wing stays empty");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "HydroPress";
            go.transform.SetParent(room, false);
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            // the client's model keeps its own meshes, materials and texture mapping. Its machines were authored far apart in one long line:
            // here each machine stands on its own on the floor of the wing, front towards the player (the pink control panel then faces the player)
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.3f;
            var groups = new Dictionary<string, List<Renderer>>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var key = r.name.StartsWith("empty_2") ? "injection" : r.name.StartsWith("Cube.001") ? "pressA" : r.name.StartsWith("Cube.002") ? "pressB"
                    : r.name.StartsWith("fillister") ? "assembly" : "tower";
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<Renderer>();
                list.Add(r);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            var spots = new Dictionary<string, Vector2>
            {
                { "injection", new Vector2(-3.7f, 7.6f) },  // the long injection machine with the control panel
                { "pressA", new Vector2(-6.6f, 5.6f) },
                { "pressB", new Vector2(-6.6f, 9.4f) },
                { "assembly", new Vector2(-8.4f, 7.6f) },   // hydraulic station assembly
                { "tower", new Vector2(-5.6f, 12.2f) },     // hydraulic tower
            };
            foreach (var kv in groups)
            {
                var all = kv.Value[0].bounds;
                foreach (var r in kv.Value)
                    all.Encapsulate(r.bounds);
                var centre = all.center;
                foreach (var r in kv.Value)
                    r.transform.RotateAround(centre, Vector3.up, 0f);
                var nb = kv.Value[0].bounds;
                foreach (var r in kv.Value)
                    nb.Encapsulate(r.bounds);
                var at = ScP(spots[kv.Key].x, 0f, spots[kv.Key].y);
                var delta = new Vector3(at.x - nb.center.x, -nb.min.y, at.z - nb.center.z);
                foreach (var r in kv.Value)
                    r.transform.position += delta;
            }
        }

        private static void ScClipboard(Transform supRoot, float s)
        {
            var anchor = new GameObject("ClipboardAnchor").transform;
            anchor.SetParent(supRoot, false);
            anchor.localScale = Vector3.one / s;
            var board = Mat("ScCheckClipboard", new Color(0.62f, 0.44f, 0.26f), 0.4f, 0f);
            var sheet = Mat("ScCheckFormPaper", Color.white, 0.15f, 0f, ScTex("ScCheck_FormPaper"));
            var metal = Mat("ScCheckClip", new Color(0.62f, 0.64f, 0.68f), 0.6f, 0.9f);
            ScLocalBox("Board", anchor, new Vector3(0f, 0.02f, -0.008f), new Vector3(0.24f, 0.32f, 0.012f), board);
            ScLocalBox("Paper", anchor, new Vector3(0f, 0.012f, 0.0015f), new Vector3(0.21f, 0.28f, 0.003f), sheet);
            ScLocalBox("ClipBody", anchor, new Vector3(0f, 0.178f, 0.003f), new Vector3(0.085f, 0.03f, 0.012f), metal);
            ScLocalBox("ClipLever", anchor, new Vector3(0f, 0.158f, 0.008f), new Vector3(0.06f, 0.014f, 0.008f), metal);
            ScLocalBox("ClipRing", anchor, new Vector3(0f, 0.196f, 0.0f), new Vector3(0.03f, 0.012f, 0.01f), metal);

            var inkGo = new GameObject("Ink");
            inkGo.transform.SetParent(anchor, false);
            var lr = inkGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = 0.0011f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.positionCount = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = Mat("ScCheckInkLine", new Color(0.06f, 0.1f, 0.35f), 0f, 0f, null, null, true);

            var pen = new GameObject("Pen").transform;
            var rightSocket = FindDeep(supRoot, "HandR_Socket");
            pen.SetParent(rightSocket != null ? rightSocket : supRoot, false);
            pen.localScale = Vector3.one / s;
            // the pen lies in the curled right hand, its tip pointing along the fingers
            var toward = new Vector3(0.40f, -0.90f, 0.18f).normalized;
            pen.localRotation = Quaternion.FromToRotation(Vector3.down, toward);
            pen.localPosition = -toward * 0.05f;
            var pm = Mat("ScCheckPen", new Color(0.12f, 0.26f, 0.8f), 0.6f, 0.3f);
            ScLocalCyl("PenBody", pen, new Vector3(0f, 0.006f, 0f), 0.0072f, 0.128f, pm);
            ScLocalCyl("PenGrip", pen, new Vector3(0f, -0.045f, 0f), 0.0086f, 0.03f, Mat("ScCheckPenGrip", new Color(0.04f, 0.04f, 0.05f), 0.3f, 0f));
            ScLocalCyl("PenTip", pen, new Vector3(0f, -0.064f, 0f), 0.0024f, 0.012f, metal);
            ScLocalBox("PenClip", pen, new Vector3(0.0062f, 0.035f, 0f), new Vector3(0.0018f, 0.05f, 0.003f), metal);
        }

        private static void ScLocalBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        private static void ScLocalCyl(string name, Transform parent, Vector3 pos, float radius, float length, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        private static void ScCheckBakeProbe(Transform env1, Transform envSc)
        {
            try
            {
                var room = envSc.Find("Room");
                var go = new GameObject("ScCheckReflectionProbe");
                go.transform.SetParent(room, false);
                go.transform.position = ScP(-4f, 3.2f, 2f);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = 256;
                probe.size = new Vector3(22f, 7f, 9f);
                probe.boxProjection = true;
                probe.intensity = 1.2f;
                probe.importance = 2;
                var path = GenDir + "/ScCheckProbe.exr";
                var envWasActive = env1.gameObject.activeSelf;
                env1.gameObject.SetActive(false);
                envSc.gameObject.SetActive(true);
                var ok = Lightmapping.BakeReflectionProbe(probe, path);
                env1.gameObject.SetActive(envWasActive);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else
                {
                    Object.DestroyImmediate(go);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ScCheck] reflection probe skipped: " + e.Message);
            }
        }
    }
}
#endif

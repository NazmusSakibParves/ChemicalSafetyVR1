#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ChemicalSafety.Presentable;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/ChemicalSafety_Presentable.unity from scratch (client demo).
    /// Never opens or saves the learning scene (ChemicalSafety_Demo.unity).
    /// </summary>
    public static class PresentableSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ChemicalSafety_Presentable.unity";

        private const string ArtRoot = "Assets/Art/Presentable";
        private const string GenDir = ArtRoot + "/Generated";
        private const string XrOriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string HandsDir = "Assets/Samples/XR Hands/1.9.0/HandVisualizer/Models";
        private const float ModelScale = 0.337f;

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        [MenuItem("Chemical Safety/Presentable/Build Presentable Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Presentable] Exit Play mode first.");
                return;
            }

            Directory.CreateDirectory(GenDir);
            ConfigureImports();
            var artPath = AssetDatabase.GetAssetPath(BuildArtAsset());
            Mats.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var art = AssetDatabase.LoadAssetAtPath<PresentableArt>(artPath);

            SetupLighting();
            var env = new GameObject("Environment").transform;
            BuildShell(env);
            BuildFloorMarkings(env);
            BuildSideRacks(env);
            BuildSigns(env, art);

            var drums = BuildHeroRack(env, art, out var rackFocus);
            BuildSc02Zone(env);

            var rig = BuildRig(art, out var leftController);
            var systems = new GameObject("Systems").transform;
            var im = new GameObject("XR Interaction Manager");
            im.transform.SetParent(systems);
            im.AddComponent<XRInteractionManager>();
            var es = new GameObject("EventSystem");
            es.transform.SetParent(systems);
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();

            var spawns = new GameObject("Spawns").transform;
            var spawnStart = Marker("Spawn_Start", spawns, new Vector3(0f, 0f, -4.2f), 0f);
            var spawnDrumView = Marker("Spawn_DrumView", spawns, new Vector3(0.5f, 0f, 2.9f), 0f);
            var spawnSc02 = Marker("Spawn_SC02", spawns, new Vector3(0f, 0f, -7.6f), 180f);
            rig.transform.SetPositionAndRotation(spawnStart.position, spawnStart.rotation);

            var flow = new GameObject("PresentableFlow").transform;
            var hotspot1 = Hotspot("Hotspot_SC01", flow, art, new Vector3(2.0f, 0f, 4.5f));
            var hotspot2 = Hotspot("Hotspot_SC02", flow, art, new Vector3(0f, 0f, -9.3f));

            var instruction = new GameObject("InstructionPanel");
            instruction.transform.SetParent(flow);
            instruction.transform.position = new Vector3(0f, 1.72f, -2.3f);
            SetRef(instruction.AddComponent<InstructionPanelView>(), "art", art);

            var mcq1 = Child("McqPanel_SC01", flow).AddComponent<PresentableMcqPanel>();
            SetRef(mcq1, "art", art);
            var mcq2 = Child("McqPanel_SC02", flow).AddComponent<PresentableMcqPanel>();
            SetRef(mcq2, "art", art);

            var fader = Child("ScreenFader", flow).AddComponent<ScreenFader>();
            var subtitle = Child("Subtitle", flow).AddComponent<WorldSubtitle>();
            SetRef(subtitle, "art", art);

            var clipboard = Child("SdsClipboard", flow).AddComponent<SdsClipboardController>();
            SetRef(clipboard, "art", art);
            SetRef(clipboard, "leftHand", leftController);

            var directorGo = Child("PresentableDirector", flow);
            var voice = directorGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 1f;
            var zoom = directorGo.AddComponent<RigZoomController>();
            var director = directorGo.AddComponent<PresentableSequenceDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("rig").objectReferenceValue = rig.transform;
            so.FindProperty("voice").objectReferenceValue = voice;
            so.FindProperty("fader").objectReferenceValue = fader;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("instructionPanel").objectReferenceValue = instruction.GetComponent<InstructionPanelView>();
            so.FindProperty("clipboard").objectReferenceValue = clipboard;
            so.FindProperty("zoom").objectReferenceValue = zoom;
            so.FindProperty("spawnStart").objectReferenceValue = spawnStart;
            so.FindProperty("spawnDrumView").objectReferenceValue = spawnDrumView;
            so.FindProperty("rackFocus").objectReferenceValue = rackFocus;
            so.FindProperty("hotspotSc01").objectReferenceValue = hotspot1;
            so.FindProperty("mcqSc01").objectReferenceValue = mcq1;
            so.FindProperty("spawnSc02").objectReferenceValue = spawnSc02;
            so.FindProperty("hotspotSc02").objectReferenceValue = hotspot2;
            so.FindProperty("mcqSc02").objectReferenceValue = mcq2;
            var drumsProp = so.FindProperty("drums");
            drumsProp.arraySize = drums.Length;
            for (var i = 0; i < drums.Length; i++)
                drumsProp.GetArrayElementAtIndex(i).objectReferenceValue = drums[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Presentable] Built " + ScenePath);
        }

        // ---------------------------------------------------------------- imports / art

        private static void ConfigureImports()
        {
            foreach (var dir in new[] { "2D/Generated", "2D/BanglaText", "2D/UI", "2D/Labels" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot + "/" + dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null)
                        continue;
                    var sprite = !dir.EndsWith("Labels");
                    var changed = false;
                    if (sprite && ti.textureType != TextureImporterType.Sprite)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    if (!ti.alphaIsTransparency || !ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp)
                    {
                        ti.alphaIsTransparency = true;
                        ti.mipmapEnabled = true;
                        ti.wrapMode = TextureWrapMode.Clamp;
                        changed = true;
                    }

                    if (changed)
                        ti.SaveAndReimport();
                }
            }
        }

        private static void ConfigureVoiceImport(string path)
        {
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null)
                return;
            var s = ai.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.DecompressOnLoad && s.preloadAudioData && !ai.loadInBackground)
                return;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = false;
            ai.SaveAndReimport();
        }

        private static PresentableArt BuildArtAsset()
        {
            var path = ArtRoot + "/PresentableArt.asset";
            var art = AssetDatabase.LoadAssetAtPath<PresentableArt>(path);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<PresentableArt>();
                AssetDatabase.CreateAsset(art, path);
            }

            Sprite G(string n) => LoadSprite("2D/Generated/" + n);
            Sprite B(string n) => LoadSprite("2D/BanglaText/" + n);
            AudioClip V(string n)
            {
                var p = "Assets/Art/Audio/VO/" + n;
                if (n != "VO_AssalamuAlaikum_bn.mp3")
                    ConfigureVoiceImport(p);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }

            art.ring = G("FX_Ring");
            art.ringDashed = G("FX_RingDashed");
            art.radialGlow = G("FX_RadialGlow");
            art.beam = G("FX_Beam");
            art.arrowDown = G("FX_ArrowDown");
            art.speaker = G("ICON_Speaker");
            art.check = G("ICON_Check");
            art.cross = G("ICON_Cross");
            art.circle = G("ICON_Circle");
            art.dot = G("ICON_Dot");
            art.iconGhs = G("ICON_GHS");
            art.iconSds = G("ICON_SDS");
            art.roundedPanel = G("UI_RoundedPanel");
            art.roundedSolid = G("UI_RoundedSolid");
            art.softGlow = G("UI_SoftGlow");
            art.popupFrame = G("UI_PopupScreen_Tint");
            art.answerBox = G("UI_AnswerBox_Tint");
            art.pop02 = G("UI_Pop02_Tint");
            art.thumbFaded = G("THUMB_Drum_Faded");
            art.thumbNoLabel = G("THUMB_Drum_NoLabel");
            art.thumbMismatch = G("THUMB_Drum_Mismatch");
            art.thumbCorrect = G("THUMB_Drum_Correct");
            art.sdsClipboard = LoadSprite("2D/UI/SDS_Clipboard");
            art.bnSalam = B("BN_Salam");
            art.bnWelcomeSub = B("BN_WelcomeSub");
            art.bnInstructionSub = B("BN_InstructionSub");
            art.bnInstructionPanel = B("BN_InstructionPanel");
            art.bnStandHere = B("BN_StandHere");
            art.bnCorrectSub = B("BN_CorrectSub");
            art.bnWrongSub = B("BN_WrongSub");
            art.bnClipboardHint = B("BN_ClipboardHint");
            art.bnZoomHint = B("BN_ZoomHint");
            art.bnNextZone = B("BN_NextZone");
            art.voSalam = V("VO_AssalamuAlaikum_bn.mp3");
            art.voInstruction = V("VO_SC01_Instruction_bn.mp3");
            art.voCorrect = V("VO_Correct_bn.mp3");
            art.voWrong = V("VO_Wrong_bn.mp3");

            foreach (var guid in AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null && f.name == "LiberationSans SDF")
                    art.englishFont = f;
            }

            if (art.englishFont == null)
                art.englishFont = TMP_Settings.defaultFontAsset;

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        private static Sprite LoadSprite(string rel)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + rel + ".png");
            if (s == null)
                Debug.LogWarning("[Presentable] Missing sprite " + rel);
            return s;
        }

        private static Material Mat(string name, Color color, float smooth = 0.3f, float metal = 0f,
            Texture tex = null, Color? emission = null, bool unlit = false, bool alphaClip = false)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;

            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }

            m.SetColor("_BaseColor", color);
            if (tex != null)
                m.SetTexture("_BaseMap", tex);
            if (!unlit)
            {
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
            }

            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.35f);
                m.EnableKeyword("_ALPHATEST_ON");
            }

            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // ---------------------------------------------------------------- environment

        private static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.55f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.74f, 0.78f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 48f;

            var sun = new GameObject("Directional Light");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(58f, -28f, 0f);

            var fill = new GameObject("Fill Light");
            var f = fill.AddComponent<Light>();
            f.type = LightType.Directional;
            f.color = new Color(0.75f, 0.85f, 1f);
            f.intensity = 0.35f;
            f.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(30f, 160f, 0f);
        }

        private static void BuildShell(Transform env)
        {
            var concrete = Mat("Concrete", new Color(0.62f, 0.63f, 0.64f), 0.28f);
            var wall = Mat("WallPanel", new Color(0.46f, 0.5f, 0.55f), 0.25f, 0.3f);
            var roof = Mat("Roof", new Color(0.28f, 0.3f, 0.33f), 0.2f, 0.4f);
            var skylight = Mat("Skylight", Color.white, 0f, 0f, null, new Color(2.2f, 2.2f, 2.1f));
            var steel = Mat("SteelBeam", new Color(0.35f, 0.38f, 0.42f), 0.45f, 0.7f);

            var floor = Box("Floor", env, new Vector3(0f, -0.05f, 1f), new Vector3(14f, 0.1f, 30f), concrete);
            floor.AddComponent<TeleportationArea>();

            Box("Wall_Left", env, new Vector3(-7.05f, 3.75f, 1f), new Vector3(0.1f, 7.5f, 30f), wall);
            Box("Wall_Right", env, new Vector3(7.05f, 3.75f, 1f), new Vector3(0.1f, 7.5f, 30f), wall);
            Box("Wall_Back", env, new Vector3(0f, 3.75f, -14.05f), new Vector3(14f, 7.5f, 0.1f), wall);
            Box("Wall_Far", env, new Vector3(0f, 3.75f, 16.05f), new Vector3(14f, 7.5f, 0.1f), wall);
            Box("Roof", env, new Vector3(0f, 7.55f, 1f), new Vector3(14.2f, 0.1f, 30.2f), roof);

            for (var z = -12f; z <= 14f; z += 4f)
            {
                Box("Skylight", env, new Vector3(-3f, 7.48f, z), new Vector3(2.2f, 0.04f, 2.6f), skylight);
                Box("Skylight", env, new Vector3(3f, 7.48f, z), new Vector3(2.2f, 0.04f, 2.6f), skylight);
                Box("RoofBeam", env, new Vector3(0f, 7.2f, z + 2f), new Vector3(14f, 0.25f, 0.18f), steel);
            }

            var muralTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Env_Background.png");
            var mural = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mural.name = "Mural_Warehouse";
            mural.transform.SetParent(env);
            mural.transform.position = new Vector3(0f, 3.75f, 15.98f);
            mural.transform.localScale = new Vector3(13.3f, 7.5f, 1f);
            Object.DestroyImmediate(mural.GetComponent<Collider>());
            mural.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Mural", new Color(0.92f, 0.92f, 0.92f), 0f, 0f, muralTex, null, true);
        }

        private static void BuildFloorMarkings(Transform env)
        {
            var yellow = Mat("FloorYellow", new Color(1f, 0.78f, 0.08f), 0.5f, 0f, null,
                new Color(0.25f, 0.18f, 0f));
            const float y = 0.006f;
            const float h = 0.012f;
            const float w = 0.12f;

            Box("Line_Right", env, new Vector3(2.0f, y, -4.4f), new Vector3(w, h, 19.4f), yellow, false);
            Box("Line_Left", env, new Vector3(-2.0f, y, -4.4f), new Vector3(w, h, 19.4f), yellow, false);
            Box("Line_RackFront", env, new Vector3(0f, y, 5.3f), new Vector3(4.12f, h, w), yellow, false);
            Box("Line_ZoneSC02", env, new Vector3(0f, y, -10.6f), new Vector3(4.12f, h, w), yellow, false);

            for (var i = 0; i < 6; i++)
            {
                var chevron = Box("Chevron", env, new Vector3(3.1f + i * 0.45f, y, 0.4f),
                    new Vector3(0.12f, h, 0.9f), yellow, false);
                chevron.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            }
        }

        private static void BuildSideRacks(Transform env)
        {
            PlaceModel("3D/ShelfWithDrums", "Rack_Left", env, new Vector3(-4.6f, 0f, 6.2f), 90f);
            PlaceModel("3D/ShelfWithDrums", "Rack_Right", env, new Vector3(4.6f, 0f, 6.2f), -90f);

            var pallet = Mat("PalletWood", new Color(0.62f, 0.45f, 0.26f), 0.15f);
            var spots = new[]
            {
                new Vector3(-5.6f, 0f, 1.4f), new Vector3(-5.0f, 0f, 1.4f), new Vector3(-5.3f, 0f, 0.85f),
                new Vector3(5.6f, 0f, 10.6f), new Vector3(5.0f, 0f, 10.6f), new Vector3(-5.6f, 0f, 10.9f)
            };
            Box("Pallet_A", env, new Vector3(-5.3f, 0.06f, 1.15f), new Vector3(1.3f, 0.12f, 1.2f), pallet);
            for (var i = 0; i < spots.Length; i++)
            {
                var p = spots[i];
                if (i < 3)
                    p.y = 0.12f;
                PlaceModel("3D/Drum", "FloorDrum_" + i, env, p, i * 47f);
            }

            var red = Mat("ExtinguisherRed", new Color(0.8f, 0.07f, 0.05f), 0.6f);
            foreach (var pos in new[] { new Vector3(-6.85f, 0.35f, -1.5f), new Vector3(6.85f, 0.35f, 3.2f) })
            {
                var ext = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ext.name = "FireExtinguisher";
                ext.transform.SetParent(env);
                ext.transform.position = pos;
                ext.transform.localScale = new Vector3(0.18f, 0.32f, 0.18f);
                ext.GetComponent<MeshRenderer>().sharedMaterial = red;
            }
        }

        private static DrumCallout[] BuildHeroRack(Transform env, PresentableArt art, out Transform focus)
        {
            const float rackZ = 6.2f;
            var rack = PlaceModel("3D/Shelf", "HeroRack", env, new Vector3(0f, 0f, rackZ), 0f);
            var surface = FindBottomSurface(rack, rackZ);

            var pallet = Mat("PalletWood", new Color(0.62f, 0.45f, 0.26f), 0.15f);
            const float palletH = 0.12f;
            Box("HeroPallet", rack.transform, new Vector3(0f, surface + palletH * 0.5f, rackZ),
                new Vector3(2.75f, palletH, 0.7f), pallet);

            var labelMesh = CurvedLabelMesh(0.3f, 0.36f);
            var labels = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Labels/Label_Acetone_Faded.png"),
                null,
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Labels/Label_Methanol.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Labels/Label_Isopropanol.png")
            };
            var captions = new[] { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

            var callouts = new DrumCallout[4];
            for (var i = 0; i < 4; i++)
            {
                var x = -1.02f + i * 0.68f;
                var drum = PlaceModel("3D/Drum", "HeroDrum_" + (i + 1), rack.transform,
                    new Vector3(x, surface + palletH, rackZ), 180f + (i - 1.5f) * 6f, out var radius, out var height);

                if (labels[i] != null)
                {
                    var label = new GameObject("Label");
                    label.transform.SetParent(drum.transform, false);
                    label.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                    label.transform.rotation = Quaternion.identity;
                    label.transform.localScale = Vector3.one * ((radius + 0.004f) / 0.3f);
                    label.AddComponent<MeshFilter>().sharedMesh = labelMesh;
                    label.AddComponent<MeshRenderer>().sharedMaterial =
                        Mat("Label_" + (i + 1), Color.white, 0.35f, 0f, labels[i], null, false, true);
                }

                var callout = drum.AddComponent<DrumCallout>();
                var so = new SerializedObject(callout);
                so.FindProperty("art").objectReferenceValue = art;
                so.FindProperty("number").intValue = i + 1;
                so.FindProperty("caption").stringValue = captions[i];
                so.FindProperty("bubbleHeight").floatValue = height + 0.1f;
                so.FindProperty("bubbleOffset").vector3Value = new Vector3(0f, 0f, -0.42f);
                so.ApplyModifiedPropertiesWithoutUndo();
                callouts[i] = callout;
            }

            focus = Marker("RackFocus", rack.transform, new Vector3(0f, 1f, rackZ), 0f);
            return callouts;
        }

        private static void BuildSc02Zone(Transform env)
        {
            var zone = new GameObject("Zone_SC02_Storage").transform;
            zone.SetParent(env);

            var cabinets = new[]
            {
                ("OXIDIZERS", new Color(0.95f, 0.55f, 0.1f), -1.6f),
                ("FLAMMABLE\nKEEP FIRE AWAY", new Color(0.97f, 0.8f, 0.08f), 0f),
                ("ACIDS /\nCORROSIVES", new Color(0.16f, 0.36f, 0.75f), 1.6f)
            };

            foreach (var (title, color, x) in cabinets)
            {
                var name = title.Split('\n')[0].Replace(" /", "");
                Box("Cabinet_" + name, zone, new Vector3(x, 0.95f, -12.8f), new Vector3(1.25f, 1.9f, 0.65f),
                    Mat("Cabinet_" + name, color, 0.5f, 0.2f));
                Box("Door_Seam", zone, new Vector3(x, 0.95f, -12.47f), new Vector3(0.015f, 1.7f, 0.01f),
                    Mat("CabinetSeam", new Color(0.1f, 0.1f, 0.1f)), false);

                var canvas = PresentableUi.WorldCanvas("Sign", zone, new Vector2(600f, 300f), 0.0017f, false, 5);
                canvas.transform.position = new Vector3(x, 1.45f, -12.46f);
                canvas.transform.rotation = Quaternion.LookRotation(Vector3.back);
                var bg = PresentableUi.Rect("Bg", canvas.transform, Vector2.zero, Vector2.one);
                PresentableUi.Img(bg, null, new Color(1f, 1f, 1f, 0.95f));
                PresentableUi.Text("Title", canvas.transform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f),
                    title, 64f, name == "FLAMMABLE" ? new Color(0.8f, 0.05f, 0.05f) : new Color(0.1f, 0.1f, 0.1f),
                    bold: true, font: TMP_Settings.defaultFontAsset);
            }

            PlaceModel("3D/Drum", "Sc02_SolventDrum", zone, new Vector3(-2.9f, 0f, -12.4f), 20f);
        }

        private static void BuildSigns(Transform env, PresentableArt art)
        {
            var signs = new[]
            {
                ("2D/BanglaText/BN_Sign_Danger", new Vector3(-3.6f, 2.3f, 4.6f), 0f, 0.0018f,
                    new Color(0.95f, 0.72f, 0.05f)),
                ("2D/BanglaText/BN_Sign_SafetyFirst", new Vector3(-6.95f, 2.4f, -1.0f), 90f, 0.0022f,
                    new Color(0.05f, 0.5f, 0.25f)),
                ("2D/BanglaText/BN_Sign_Equipment", new Vector3(3.6f, 2.3f, 4.6f), 0f, 0.0018f,
                    new Color(0.05f, 0.5f, 0.25f))
            };

            foreach (var (path, pos, yaw, scale, board) in signs)
            {
                var sprite = LoadSprite(path);
                if (sprite == null)
                    continue;
                var size = new Vector2(sprite.rect.width + 80f, sprite.rect.height + 60f);
                var canvas = PresentableUi.WorldCanvas("Sign_" + Path.GetFileName(path), env, size, scale, false, 2);
                canvas.transform.position = pos;
                canvas.transform.rotation = Quaternion.Euler(0f, -yaw, 0f);
                PresentableUi.Img(PresentableUi.Rect("Board", canvas.transform, Vector2.zero, Vector2.one),
                    art.roundedPanel, board);
                PresentableUi.Img(PresentableUi.Rect("Img", canvas.transform, Vector2.zero, Vector2.one,
                    new Vector2(40f, 30f), new Vector2(-40f, -30f)), sprite, Color.white);
            }

            var exit = PresentableUi.WorldCanvas("Sign_ProductionFloor", env, new Vector2(900f, 220f), 0.0022f, false, 2);
            exit.transform.position = new Vector3(5.2f, 3.1f, 12f);
            var bgRt = PresentableUi.Rect("Bg", exit.transform, Vector2.zero, Vector2.one);
            PresentableUi.Img(bgRt, art.roundedPanel, new Color(0.98f, 0.8f, 0.1f));
            PresentableUi.Text("Text", exit.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                "Production Floor A1  \u2192", 96f, new Color(0.08f, 0.08f, 0.08f), bold: true,
                font: art.englishFont);
        }

        // ---------------------------------------------------------------- rig / hands

        private static GameObject BuildRig(PresentableArt art, out Transform leftController)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "XR Origin (XR Rig)";

            var cam = rig.GetComponentInChildren<Camera>(true);
            cam.nearClipPlane = 0.03f;
            cam.gameObject.tag = "MainCamera";
            cam.gameObject.AddComponent<DeskCameraFallback>();

            var glove = Mat("NitrileGlove", new Color(0.07f, 0.075f, 0.085f), 0.62f);
            leftController = FindDeep(rig.transform, "Left Controller");
            var right = FindDeep(rig.transform, "Right Controller");

            AddGlove(right, true, GlovedHandPose.Pose.Point, glove, "RightHand.fbx");
            AddGlove(leftController, false, GlovedHandPose.Pose.Hold, glove, "LeftHand.fbx");
            return rig;
        }

        private static void AddGlove(Transform controller, bool isRight, GlovedHandPose.Pose pose, Material glove,
            string model)
        {
            if (controller == null)
                return;

            var hand = controller.gameObject.AddComponent<GlovedHandPose>();
            var so = new SerializedObject(hand);
            so.FindProperty("handModel").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(HandsDir + "/" + model);
            so.FindProperty("gloveMaterial").objectReferenceValue = glove;
            so.FindProperty("isRight").boolValue = isRight;
            so.FindProperty("pose").enumValueIndex = (int)pose;
            var poke = FindDeep(controller, "Poke Point");
            so.FindProperty("pokePoint").objectReferenceValue = isRight ? poke : null;
            var visual = FindDeep(controller, (isRight ? "Right" : "Left") + " Controller Visual");
            so.FindProperty("controllerVisualToHide").objectReferenceValue = visual != null ? visual.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- helpers

        private static FloorHotspot Hotspot(string name, Transform parent, PresentableArt art, Vector3 pos)
        {
            var go = Child(name, parent);
            go.transform.position = pos;
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.45f;
            col.height = 2.2f;
            col.center = new Vector3(0f, 1.1f, 0f);
            go.AddComponent<XRSimpleInteractable>();
            var hs = go.AddComponent<FloorHotspot>();
            SetRef(hs, "art", art);
            return hs;
        }

        private static GameObject PlaceModel(string rel, string name, Transform parent, Vector3 floorPos, float yaw)
        {
            return PlaceModel(rel, name, parent, floorPos, yaw, out _, out _);
        }

        /// <summary>Instantiates a .glb at real-world scale with its bottom-centre on floorPos.</summary>
        private static GameObject PlaceModel(string rel, string name, Transform parent, Vector3 floorPos, float yaw,
            out float radius, out float height)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + rel + ".glb");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(root.transform, false);
            model.transform.localScale *= ModelScale;

            var b = WorldBounds(model);
            var offset = floorPos - new Vector3(b.center.x, b.min.y, b.center.z);
            model.transform.position += offset;
            radius = Mathf.Min(b.extents.x, b.extents.z);
            height = b.size.y;
            return root;
        }

        private static float FindBottomSurface(GameObject rack, float rackZ)
        {
            var temp = new List<MeshCollider>();
            foreach (var mf in rack.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }

            Physics.SyncTransforms();
            var best = 0f;
            foreach (var x in new[] { -0.9f, -0.3f, 0.3f, 0.9f })
            {
                foreach (var dz in new[] { -0.15f, 0f, 0.15f })
                {
                    if (Physics.Raycast(new Vector3(x, 0.8f, rackZ + dz), Vector3.down, out var hit, 0.8f) &&
                        hit.point.y < 0.45f)
                        best = Mathf.Max(best, hit.point.y);
                }
            }

            foreach (var mc in temp)
                Object.DestroyImmediate(mc);
            return best;
        }

        private static Mesh CurvedLabelMesh(float radius, float width)
        {
            var path = GenDir + "/Mesh_DrumLabel.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "DrumLabel" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            const int seg = 12;
            var arc = width / radius;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var u = i / (float)seg;
                var a = (u - 0.5f) * arc;
                var n = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
                for (var j = 0; j <= 1; j++)
                {
                    verts.Add(n * radius + Vector3.up * ((j - 0.5f) * width));
                    uvs.Add(new Vector2(u, j));
                    normals.Add(n);
                }
            }

            for (var i = 0; i < seg; i++)
            {
                var v00 = i * 2;
                var v01 = v00 + 1;
                var v10 = v00 + 2;
                var v11 = v00 + 3;
                tris.AddRange(new[] { v00, v01, v11, v00, v11, v10 });
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs)
                b.Encapsulate(r.bounds);
            return b;
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat,
            bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Transform Marker(string name, Transform parent, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }
    }
}
#endif

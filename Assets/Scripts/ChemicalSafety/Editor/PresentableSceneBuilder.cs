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
            GenerateRedVariants();
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
            BuildDressing(env);

            var drums = BuildHeroRack(env, art, out var rackFocus, out var drumPokes);
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
            // Centre of the yellow floor box in front of the hero rack.
            var hotspot1 = Hotspot("Hotspot_SC01", flow, art, new Vector3(0f, 0f, 4.5f));
            var hotspot2 = Hotspot("Hotspot_SC02", flow, art, new Vector3(0f, 0f, -9.3f));

            var trolleyPos = new Vector3(1.95f, 0f, 5.7f);
            var trolley = BuildTrolley(env, trolleyPos);
            var hotspotTrolley = Hotspot("Hotspot_Trolley", flow, art, trolleyPos);
            var htSo = new SerializedObject(hotspotTrolley);
            htSo.FindProperty("activateOnStand").boolValue = false;
            htSo.FindProperty("showBanglaLabel").boolValue = false;
            htSo.FindProperty("englishLabel").stringValue = "Poke the trolley";
            htSo.ApplyModifiedPropertiesWithoutUndo();
            var drumPokeStand = Marker("DrumPokeStand", spawns, new Vector3(0f, 0f, 5.2f), 0f);
            var trolleyStand = Marker("TrolleyStand", spawns, new Vector3(1.35f, 0f, 5.1f), 0f);

            var labelPopup = Child("LabelPickPopup", flow).AddComponent<LabelPickPopup>();
            SetRef(labelPopup, "art", art);

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
            directorGo.AddComponent<RayVisualSuppressor>();
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
            so.FindProperty("labelPopup").objectReferenceValue = labelPopup;
            so.FindProperty("drumPokeStand").objectReferenceValue = drumPokeStand;
            so.FindProperty("trolley").objectReferenceValue = trolley;
            so.FindProperty("trolleyPoke").objectReferenceValue = trolley.GetComponent<PokeTarget>();
            so.FindProperty("hotspotTrolley").objectReferenceValue = hotspotTrolley;
            so.FindProperty("trolleyStand").objectReferenceValue = trolleyStand;
            var pokesProp = so.FindProperty("drumPokes");
            pokesProp.arraySize = drumPokes.Length;
            for (var i = 0; i < drumPokes.Length; i++)
                pokesProp.GetArrayElementAtIndex(i).objectReferenceValue = drumPokes[i];
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

        private const string DesignerDir = ArtRoot + "/2D/Designer";

        // Designer frames are cyan; a red twin (R and B swapped) is generated for wrong states because
        // tinting a cyan sprite red just makes it dark.
        private static void GenerateRedVariants()
        {
            MakeRed("pop up screen", "UI_Red_PopupScreen");
            MakeRed("answer box", "UI_Red_AnswerBox");
            MakeRed("button", "UI_Red_Button");
        }

        private static void MakeRed(string srcName, string outName)
        {
            var src = DesignerDir + "/" + srcName + ".png";
            var dst = ArtRoot + "/2D/Generated/" + outName + ".png";
            if (!File.Exists(src))
            {
                Debug.LogWarning("[Presentable] Missing " + src);
                return;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(src));
            var px = tex.GetPixels32();
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color32(c.b, (byte)(c.g * 0.2f), (byte)(c.r * 0.6f), c.a);
            }

            tex.SetPixels32(px);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (File.Exists(dst) && File.ReadAllBytes(dst).Length == bytes.Length)
                return;
            File.WriteAllBytes(dst, bytes);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureImports()
        {
            foreach (var dir in new[] { "2D/Generated", "2D/BanglaText", "2D/Designer", "2D/Env" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot + "/" + dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null)
                        continue;
                    var changed = false;
                    if (ti.textureType != TextureImporterType.Sprite)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    var maxSize = path.EndsWith("FLOOR_Baked.png") ? 4096 : path.EndsWith("Background.png") ? 2048 : 1024;
                    if (ti.maxTextureSize != maxSize)
                    {
                        ti.maxTextureSize = maxSize;
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
            Sprite D(string n) => LoadSprite("2D/Designer/" + n);
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
            art.check = G("ICON_Check");
            art.cross = G("ICON_Cross");
            art.circle = G("ICON_Circle");
            art.dot = G("ICON_Dot");
            art.iconGhs = G("ICON_GHS");
            art.iconSds = G("ICON_SDS");
            art.roundedPanel = G("UI_RoundedPanel");
            art.roundedSolid = G("UI_RoundedSolid");
            art.softGlow = G("UI_SoftGlow");
            art.popupFrame = D("pop up screen");
            art.answerBox = D("answer box");
            art.pop02 = D("pop02");
            art.button = D("button");
            art.popupFrameRed = G("UI_Red_PopupScreen");
            art.answerBoxRed = G("UI_Red_AnswerBox");
            art.buttonRed = G("UI_Red_Button");
            art.speaker = D("speaker button");
            art.thumbNoLabel = G("THUMB_Drum_NoLabel");
            art.labelFaded = D("Drum label/faded");
            art.labelMethanol = D("Drum label/Methanol");
            art.labelIsopropanol = D("Drum label/Isopropanol");
            art.sdsClipboard = D("SDS in Clipboard");
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
            art.bnPickLabel = B("BN_PickLabel");
            art.bnWrongLabel = B("BN_WrongLabel");
            art.bnContinueHint = B("BN_ContinueHint");
            art.voSalam = V("VO_AssalamuAlaikum_bn.mp3");
            art.voInstruction = V("VO_SC01_Instruction_bn.mp3");
            art.voCorrect = V("VO_Correct_bn.mp3");
            art.voWrong = V("VO_Wrong_bn.mp3");
            // Not recorded yet: keep whatever is already in the slot (null) until the mp3 is dropped in.
            var pick = V("VO_PickLabel_bn.mp3");
            if (pick != null)
                art.voPickLabel = pick;
            var wrongLabel = V("VO_WrongLabel_bn.mp3");
            if (wrongLabel != null)
                art.voWrongLabel = wrongLabel;
            var cont = V("VO_ContinueHint_bn.mp3");
            if (cont != null)
                art.voContinueHint = cont;
            if (art.voPickLabel == null)
                Debug.LogWarning("[Presentable] VO_PickLabel_bn.mp3 is missing - AudioClip slot left empty.");
            if (art.voWrongLabel == null)
                Debug.LogWarning("[Presentable] VO_WrongLabel_bn.mp3 is missing - AudioClip slot left empty.");

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
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.65f, 0.68f);
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
            l.shadows = LightShadows.Hard;
            l.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(58f, -28f, 0f);
        }

        private static void BuildShell(Transform env)
        {
            var concrete = Mat("Concrete", new Color(0.62f, 0.63f, 0.64f), 0.28f);
            var wall = Mat("WallPanel", new Color(0.46f, 0.5f, 0.55f), 0.25f, 0.3f);
            var roof = Mat("Roof", new Color(0.28f, 0.3f, 0.33f), 0.2f, 0.4f);
            var skylight = Mat("Skylight", new Color(1f, 1f, 0.95f), 0f, 0f, null, null, true);
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

            BuildCeilingFans(env);

            var muralTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DesignerDir + "/Background.png");
            var mural = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mural.name = "Mural_Warehouse";
            mural.transform.SetParent(env);
            mural.transform.position = new Vector3(0f, 3.75f, 15.98f);
            mural.transform.localScale = new Vector3(13.3f, 7.5f, 1f);
            Object.DestroyImmediate(mural.GetComponent<Collider>());
            mural.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Mural", new Color(0.92f, 0.92f, 0.92f), 0f, 0f, muralTex, null, true);
        }

        // The whole floor (polished concrete, joints, scuffs, light reflections and the worn yellow safety
        // paint) is baked into one texture by Tools/generate_floor.py and laid over the collision floor.
        private static void BuildFloorMarkings(Transform env)
        {
            var mat = Mat("FloorBaked", Color.white, 0.6f, 0f, EnvTex("FLOOR_Baked"));
            QuadObj("Floor_Baked", env, new Vector3(0f, 0.004f, 1f), new Vector3(90f, 0f, 0f), new Vector2(14f, 30f),
                mat);
        }

        // ---------------------------------------------------------------- racks / dressing

        private const float RackX = 4.3f;
        private const float RackStartZ = 1.8f;
        private const float BayLen = 2.6f;
        private const int RackBays = 2;

        private static void MarkStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // Light 12-sided drum (side + lid): ~36 triangles, so hundreds of rack drums stay cheap on Quest.
        private static Mesh LowPolyDrumMesh()
        {
            var path = GenDir + "/Mesh_LowPolyDrum.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "LowPolyDrum" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            const int seg = 12;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var a = i / (float)seg * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(n * 0.5f + Vector3.down * 0.5f);
                verts.Add(n * 0.5f + Vector3.up * 0.5f);
                norms.Add(n);
                norms.Add(n);
                uvs.Add(new Vector2(i / (float)seg, 0f));
                uvs.Add(new Vector2(i / (float)seg, 1f));
            }

            for (var i = 0; i < seg; i++)
            {
                var b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }

            var capStart = verts.Count;
            verts.Add(Vector3.up * 0.5f);
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (var i = 0; i <= seg; i++)
            {
                var a = i / (float)seg * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0.5f, Mathf.Sin(a) * 0.5f));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f));
            }

            for (var i = 0; i < seg; i++)
                tris.AddRange(new[] { capStart, capStart + 2 + i, capStart + 1 + i });

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject MeshObj(string name, Transform parent, Mesh mesh, Vector3 pos, Vector3 scale,
            Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static Material MatAlpha(string name, Texture tex, Color color, bool additive)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;

            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
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

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            if (tex != null)
                m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        private static GameObject QuadObj(string name, Transform parent, Vector3 pos, Vector3 euler, Vector2 size,
            Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(euler);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }

        private static Texture2D EnvTex(string n) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Env/" + n + ".png");

        private static Mesh LoadObjMesh(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Mesh m)
                    return m;
            }

            Debug.LogError("[Presentable] No mesh in " + path);
            return null;
        }

        // Two aisles built from the designer shelf model (shelve.glb) stretched along the aisle, every shelf
        // loaded with 8 drums (~1 in 4 red). Drums are 12-sided meshes so the extra rows stay cheap.
        private static void BuildSideRacks(Transform env)
        {
            var racks = new GameObject("Racks").transform;
            racks.SetParent(env, false);
            var rackBlue = Mat("RackUpright", new Color(0.08f, 0.26f, 0.7f), 0.4f, 0.4f);
            var rackOrange = Mat("RackBeam", new Color(0.95f, 0.4f, 0.05f), 0.4f, 0.3f);
            var wood = Mat("PalletWood", new Color(0.62f, 0.45f, 0.26f), 0.15f);
            var blue = Mat("DrumBlue", new Color(0.05f, 0.23f, 0.66f), 0.5f, 0.4f);
            var red = Mat("DrumRed", new Color(0.8f, 0.09f, 0.06f), 0.55f, 0.35f);
            var drum = LowPolyDrumMesh();
            // Same rack as Shelf.glb, decimated from 88k to ~13k triangles (Tools: see Docs); sized in metres.
            var uprightMesh = LoadObjMesh(ArtRoot + "/3D/ShelfLite_Uprights.obj");
            var beamMesh = LoadObjMesh(ArtRoot + "/3D/ShelfLite_Beams.obj");

            var labelMats = new[]
            {
                LabelMat("RackLabel_Faded", "faded"), LabelMat("RackLabel_Methanol", "Methanol"),
                LabelMat("RackLabel_Isopropanol", "Isopropanol"), LabelMat("RackLabel_Methanol2", "Methanol"),
                LabelMat("RackLabel_Isopropanol2", "Isopropanol")
            };

            const float stretch = 1.75f;
            const float modelLength = 2.92f;
            var rackLen = modelLength * stretch;
            const float surface = 0.135f; // top of the shelf's bottom beam (measured on the hero rack)

            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * RackX;
                var rnd = new System.Random(side < 0f ? 7 : 19);
                for (var n = 0; n < RackBays; n++)
                {
                    var zc = RackStartZ + rackLen * (n + 0.5f);
                    var rack = new GameObject("SideRack").transform;
                    rack.SetParent(racks, false);
                    rack.SetPositionAndRotation(new Vector3(x, 0f, zc), Quaternion.Euler(0f, 90f, 0f));
                    rack.localScale = new Vector3(stretch, 1f, 1f);
                    MeshObj("Uprights", rack, uprightMesh, Vector3.zero, Vector3.one, rackBlue);
                    MeshObj("Beams", rack, beamMesh, Vector3.zero, Vector3.one, rackOrange);

                    for (var level = 0; level < 3; level++)
                    {
                        var y = surface + level * 1.4f;
                        Prim("Pallet", PrimitiveType.Cube, racks, new Vector3(x, y + 0.06f, zc),
                            new Vector3(0.7f, 0.12f, rackLen - 0.2f), wood);
                        var count = 8;
                        for (var k = 0; k < count; k++)
                        {
                            if (rnd.NextDouble() < 0.04)
                                continue;
                            var along = (k - (count - 1) * 0.5f) * ((rackLen - 0.5f) / count);
                            var dpos = new Vector3(x, y + 0.12f + 0.44f, zc + along);
                            MeshObj("Drum", racks, drum, dpos, new Vector3(0.58f, 0.88f, 0.58f),
                                rnd.NextDouble() < 0.27 ? red : blue);
                            if (rnd.NextDouble() < 0.38)
                                AddDrumLabel(racks, dpos, side, labelMats[rnd.Next(labelMats.Length)], rnd);
                        }
                    }
                }

                // Yellow end guards at the aisle end of each run.
                var yellow = Mat("GuardYellow", new Color(0.98f, 0.8f, 0.05f), 0.4f);
                Prim("Guard", PrimitiveType.Cube, racks, new Vector3(x, 0.3f, RackStartZ - 0.55f),
                    new Vector3(1.5f, 0.12f, 0.12f), yellow);
                foreach (var sx in new[] { -0.75f, 0.75f })
                    Prim("GuardPost", PrimitiveType.Cube, racks, new Vector3(x + sx, 0.3f, RackStartZ - 0.55f),
                        new Vector3(0.12f, 0.6f, 0.12f), yellow);

                // Floor pallets with drums beside the aisle.
                foreach (var z in new[] { -2.0f, -6.5f })
                {
                    var px = side * 2.95f;
                    Prim("FloorPallet", PrimitiveType.Cube, racks, new Vector3(px, 0.07f, z),
                        new Vector3(1.2f, 0.14f, 1.2f), wood);
                    for (var i = 0; i < 4; i++)
                        MeshObj("FloorDrum", racks, drum,
                            new Vector3(px + (i % 2 - 0.5f) * 0.58f, 0.14f + 0.44f, z + (i / 2 - 0.5f) * 0.58f),
                            new Vector3(0.56f, 0.88f, 0.56f), rnd.NextDouble() < 0.3 ? red : blue);
                    for (var i = 0; i < 4; i++)
                    {
                        if (rnd.NextDouble() < 0.55)
                            AddDrumLabel(racks, new Vector3(px + (i % 2 - 0.5f) * 0.58f, 0.58f, z + (i / 2 - 0.5f) * 0.58f),
                                side, labelMats[rnd.Next(labelMats.Length)], rnd);
                    }
                }
            }

            // Safety boards on the near end of each run, like Background.png.
            BuildRackSign(racks, "SIGN_SafetyEquipment", new Vector3(-RackX - 0.4f, 2.55f, RackStartZ - 0.2f), new Vector2(0.72f, 0.98f));
            BuildRackSign(racks, "SIGN_SafetyFirst", new Vector3(-RackX + 0.42f, 2.55f, RackStartZ - 0.2f), new Vector2(0.72f, 0.98f));
            BuildRackSign(racks, "SIGN_WarningHazardous", new Vector3(-RackX - 0.4f, 1.2f, RackStartZ - 0.2f), new Vector2(0.78f, 0.34f));
            BuildRackSign(racks, "SIGN_CautionHazardous", new Vector3(-RackX + 0.42f, 1.2f, RackStartZ - 0.2f), new Vector2(0.78f, 0.34f));
            BuildRackSign(racks, "SIGN_DangerHazard", new Vector3(RackX, 2.55f, RackStartZ - 0.2f), new Vector2(0.8f, 1.1f));
            BuildRackSign(racks, "SIGN_SafetyStart", new Vector3(RackX, 1.2f, RackStartZ - 0.2f), new Vector2(0.98f, 0.42f));
            MarkStatic(racks);
        }

        private static Material LabelMat(string name, string file)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DesignerDir + "/Drum label/" + file + ".png");
            return Mat(name, Color.white, 0.35f, 0f, tex, null, false, true);
        }

        // Small GHS sticker on the aisle-facing side of a rack drum.
        private static void AddDrumLabel(Transform parent, Vector3 drumCentre, float side, Material mat,
            System.Random rnd)
        {
            var pos = drumCentre + new Vector3(-side * 0.287f, (float)(rnd.NextDouble() - 0.5) * 0.12f,
                (float)(rnd.NextDouble() - 0.5) * 0.08f);
            QuadObj("Label", parent, pos, new Vector3(0f, 90f * side, (float)(rnd.NextDouble() - 0.5) * 8f),
                new Vector2(0.34f, 0.37f), mat);
        }

        private static void BuildRackSign(Transform parent, string tex, Vector3 pos, Vector2 size)
        {
            var mat = Mat(tex, Color.white, 0f, 0f, EnvTex(tex), null, true, true);
            QuadObj(tex, parent, pos, Vector3.zero, size, mat);
        }

        // Floor stains, hanging lamps, sky-light shafts, wall extinguishers.
        private static void BuildDressing(Transform env)
        {
            var dress = new GameObject("Dressing").transform;
            dress.SetParent(env, false);

            var stains = new[]
            {
                ("STAIN_Blood", new Vector3(2.7f, 0.016f, 1.0f), 1.3f, 20f),
                ("STAIN_Rust", new Vector3(-0.9f, 0.016f, 2.9f), 1.9f, 80f),
                ("STAIN_Oil", new Vector3(-2.6f, 0.016f, -5.4f), 1.5f, 140f),
                ("STAIN_Rust", new Vector3(1.4f, 0.016f, -8.2f), 1.2f, 30f)
            };
            var i = 0;
            foreach (var (tex, pos, size, yaw) in stains)
            {
                var m = MatAlpha("Stain_" + tex, EnvTex(tex), Color.white, false);
                QuadObj("Stain" + i++, dress, pos, new Vector3(90f, yaw, 0f), new Vector2(size, size), m);
            }

            // Hanging industrial lamps on the roof beams.
            var shade = Mat("LampShade", new Color(0.16f, 0.17f, 0.19f), 0.4f, 0.7f);
            var tube = Mat("LampTube", new Color(1f, 0.98f, 0.9f), 0f, 0f, null, new Color(2.6f, 2.5f, 2.3f), true);
            var cable = Mat("LampCable", new Color(0.1f, 0.1f, 0.1f), 0.2f);
            foreach (var z in new[] { -10f, -6f, -2f, 2f, 6f, 10f, 14f })
            {
                foreach (var x in new[] { -3.2f, 3.2f })
                {
                    Prim("LampCable", PrimitiveType.Cylinder, dress, new Vector3(x, 6.7f, z),
                        new Vector3(0.015f, 0.38f, 0.015f), cable);
                    Prim("LampShade", PrimitiveType.Cube, dress, new Vector3(x, 6.32f, z),
                        new Vector3(0.34f, 0.08f, 1.3f), shade);
                    Prim("LampTube", PrimitiveType.Cube, dress, new Vector3(x, 6.26f, z),
                        new Vector3(0.18f, 0.04f, 1.1f), tube);
                }
            }

            // Soft shafts of daylight under some sky-lights (two crossed quads each, additive).
            var shaftTex = EnvTex("FX_Shaft");
            var shaft = MatAlpha("Shaft", shaftTex, new Color(1f, 0.97f, 0.88f, 0.16f), true);
            foreach (var z in new[] { -4f, 4f, 12f })
            {
                foreach (var x in new[] { -3f, 3f })
                {
                    QuadObj("Shaft", dress, new Vector3(x, 3.9f, z), Vector3.zero, new Vector2(2.0f, 7.2f), shaft);
                    QuadObj("Shaft", dress, new Vector3(x, 3.9f, z), new Vector3(0f, 90f, 0f), new Vector2(2.4f, 7.2f), shaft);
                }
            }

            // Wall extinguishers (+ yellow pillars at the far wall).
            var pillar = Mat("PillarYellow", new Color(0.98f, 0.8f, 0.08f), 0.35f);
            foreach (var px in new[] { -2.4f, 2.4f })
                Box("Pillar", dress, new Vector3(px, 1.6f, 15.65f), new Vector3(0.45f, 3.2f, 0.45f), pillar);
            BuildExtinguisher(dress, new Vector3(-2.4f, 0f, 15.38f), 180f);
            BuildExtinguisher(dress, new Vector3(2.4f, 0f, 15.38f), 180f);
            BuildExtinguisher(dress, new Vector3(-6.97f, 0f, 0.4f), 90f);
            BuildExtinguisher(dress, new Vector3(6.97f, 0f, 0.4f), -90f);
            BuildExtinguisher(dress, new Vector3(-6.97f, 0f, 14.6f), 90f);
            BuildExtinguisher(dress, new Vector3(6.97f, 0f, 14.6f), -90f);
            BuildExtinguisher(dress, new Vector3(-6.97f, 0f, -7.0f), 90f);
            BuildExtinguisher(dress, new Vector3(6.97f, 0f, -7.0f), -90f);
            MarkStatic(dress);
        }

        // yaw = direction the extinguisher faces (0 = +z).
        private static void BuildExtinguisher(Transform parent, Vector3 pos, float yaw)
        {
            var red = Mat("ExtinguisherRed", new Color(0.8f, 0.07f, 0.05f), 0.6f);
            var black = Mat("ExtinguisherBlack", new Color(0.06f, 0.06f, 0.06f), 0.3f);
            var steel = Mat("TrolleySteel", new Color(0.6f, 0.62f, 0.65f), 0.5f, 0.8f);
            var white = Mat("SignWhite", Color.white, 0.2f);
            var holder = new GameObject("Extinguisher").transform;
            holder.SetParent(parent, false);
            holder.position = pos;
            holder.rotation = Quaternion.Euler(0f, yaw, 0f);

            Prim("Body", PrimitiveType.Cylinder, holder, new Vector3(0f, 1.1f, 0.12f), new Vector3(0.17f, 0.26f, 0.17f), red);
            Prim("Neck", PrimitiveType.Cylinder, holder, new Vector3(0f, 1.4f, 0.12f), new Vector3(0.07f, 0.05f, 0.07f), black);
            Prim("Handle", PrimitiveType.Cube, holder, new Vector3(0f, 1.47f, 0.12f), new Vector3(0.05f, 0.03f, 0.16f), black);
            Prim("Hose", PrimitiveType.Cube, holder, new Vector3(0.09f, 1.22f, 0.04f), new Vector3(0.025f, 0.28f, 0.025f), black);
            Prim("Strap", PrimitiveType.Cylinder, holder, new Vector3(0f, 1.2f, 0.12f), new Vector3(0.19f, 0.015f, 0.19f), steel);
            Prim("SignBack", PrimitiveType.Cube, holder, new Vector3(0f, 1.85f, 0.01f), new Vector3(0.34f, 0.34f, 0.015f), red);
            Prim("SignMark", PrimitiveType.Cube, holder, new Vector3(0f, 1.85f, 0.002f), new Vector3(0.09f, 0.22f, 0.015f), white);
        }

        private static DrumCallout[] BuildHeroRack(Transform env, PresentableArt art, out Transform focus,
            out PokeTarget[] pokes)
        {
            const float rackZ = 6.2f;
            var rack = PlaceModel("3D/Shelf", "HeroRack", env, new Vector3(0f, 0f, rackZ), 0f);
            var surface = FindBottomSurface(rack, rackZ);
            var rackBlue = Mat("RackUpright", new Color(0.08f, 0.26f, 0.7f), 0.4f, 0.4f);
            var rackOrange = Mat("RackBeam", new Color(0.95f, 0.4f, 0.05f), 0.4f, 0.3f);
            foreach (var r in rack.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name.StartsWith("Shelves_Brushed metal_0.001"))
                    r.sharedMaterial = rackOrange;
                else if (r.name.StartsWith("Shelves_Brushed metal_0"))
                    r.sharedMaterial = rackBlue;
            }


            var pallet = Mat("PalletWood", new Color(0.62f, 0.45f, 0.26f), 0.15f);
            const float palletH = 0.12f;
            Box("HeroPallet", rack.transform, new Vector3(0f, surface + palletH * 0.5f, rackZ),
                new Vector3(2.75f, palletH, 0.7f), pallet);

            var labelMesh = CurvedLabelMesh(0.3f, 0.36f);
            var labels = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(DesignerDir + "/Drum label/faded.png"),
                null,
                AssetDatabase.LoadAssetAtPath<Texture2D>(DesignerDir + "/Drum label/Methanol.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(DesignerDir + "/Drum label/Isopropanol.png")
            };
            var captions = new[] { "Faded Label", "No Label", "Mismatched GHS vs SDS", "Correctly Labelled" };

            var callouts = new DrumCallout[4];
            pokes = new PokeTarget[4];
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

                // Trigger volume a little fatter than the drum so a gloved fingertip cannot slip past.
                var cap = drum.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.radius = radius + 0.05f;
                cap.height = height + 0.04f;
                cap.center = new Vector3(0f, height * 0.5f, 0f);
                pokes[i] = drum.AddComponent<PokeTarget>();

                var callout = drum.AddComponent<DrumCallout>();
                var so = new SerializedObject(callout);
                so.FindProperty("art").objectReferenceValue = art;
                so.FindProperty("number").intValue = i + 1;
                so.FindProperty("caption").stringValue = captions[i];
                so.FindProperty("bubbleHeight").floatValue = height + 0.1f;
                so.FindProperty("bubbleOffset").vector3Value = new Vector3(0f, 0f, -0.42f);
                so.FindProperty("numberColor").colorValue = i == 3 ? PresentableUi.Green : Color.white;
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
            var exit = PresentableUi.WorldCanvas("Sign_ProductionFloor", env, new Vector2(900f, 220f), 0.0022f, false, 2);
            exit.transform.position = new Vector3(4.4f, 3.5f, 15.9f);
            var bgRt = PresentableUi.Rect("Bg", exit.transform, Vector2.zero, Vector2.one);
            PresentableUi.Img(bgRt, art.roundedPanel, new Color(0.98f, 0.8f, 0.1f));
            PresentableUi.Text("Text", exit.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f),
                "Production Floor A1  \u2192", 96f, new Color(0.08f, 0.08f, 0.08f), bold: true,
                font: art.englishFont);
        }

        // ---------------------------------------------------------------- fans / trolley

        // Large 3-blade industrial fans hung from the roof beams. Big, low enough to be seen from the
        // aisle, with one painted blade tip so the slow spin reads at a glance.
        private static void BuildCeilingFans(Transform env)
        {
            var hubMat = Mat("FanHub", new Color(0.16f, 0.17f, 0.19f), 0.4f, 0.8f);
            var rodMat = Mat("FanRod", new Color(0.22f, 0.23f, 0.25f), 0.35f, 0.8f);
            var tipMat = Mat("FanTip", new Color(0.9f, 0.35f, 0.05f), 0.3f, 0.3f);
            var blades = new[]
            {
                Mat("FanBlade_A", new Color(0.55f, 0.57f, 0.6f), 0.32f, 0.85f),
                Mat("FanBlade_B", new Color(0.46f, 0.48f, 0.5f), 0.28f, 0.85f),
                Mat("FanBlade_C", new Color(0.6f, 0.58f, 0.55f), 0.36f, 0.8f)
            };

            const float beamBottom = 7.075f;
            const float hubY = 5.4f;
            var rodLength = beamBottom - hubY - 0.22f;
            foreach (var z in new[] { -10f, -2f, 6f, 14f })
            {
                var fan = new GameObject("CeilingFan").transform;
                fan.SetParent(env, false);
                fan.position = new Vector3(0f, beamBottom, z);

                Prim("MountPlate", PrimitiveType.Cylinder, fan, new Vector3(0f, -0.02f, 0f),
                    new Vector3(0.3f, 0.02f, 0.3f), hubMat);
                Prim("DownRod", PrimitiveType.Cylinder, fan, new Vector3(0f, -rodLength * 0.5f, 0f),
                    new Vector3(0.07f, rodLength * 0.5f, 0.07f), rodMat);
                Prim("Canopy", PrimitiveType.Cylinder, fan, new Vector3(0f, -rodLength - 0.05f, 0f),
                    new Vector3(0.36f, 0.07f, 0.36f), hubMat);

                var rotor = new GameObject("Rotor").transform;
                rotor.SetParent(fan, false);
                rotor.localPosition = new Vector3(0f, -rodLength - 0.22f, 0f);
                Prim("Motor", PrimitiveType.Cylinder, rotor, Vector3.zero, new Vector3(0.44f, 0.12f, 0.44f), hubMat);
                for (var k = 0; k < 3; k++)
                {
                    var pivot = new GameObject("Blade" + k).transform;
                    pivot.SetParent(rotor, false);
                    pivot.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
                    Prim("Arm", PrimitiveType.Cube, pivot, new Vector3(0.45f, -0.01f, 0f),
                        new Vector3(0.5f, 0.035f, 0.11f), hubMat);
                    var blade = Prim("Paddle", PrimitiveType.Cube, pivot, new Vector3(1.2f, -0.02f, 0f),
                        new Vector3(1.4f, 0.014f, 0.3f), blades[k]);
                    blade.transform.localRotation = Quaternion.Euler(11f, 0f, 0f);
                    if (k == 0)
                    {
                        var tip = Prim("Tip", PrimitiveType.Cube, pivot, new Vector3(1.8f, -0.02f, 0f),
                            new Vector3(0.22f, 0.016f, 0.3f), tipMat);
                        tip.transform.localRotation = Quaternion.Euler(11f, 0f, 0f);
                    }
                }

                var spin = rotor.gameObject.AddComponent<CeilingFanSpin>();
                var so = new SerializedObject(spin);
                so.FindProperty("degreesPerSecond").floatValue = 140f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Simple two-wheel hand trolley; starts hidden and is revealed by the director.
        private static GameObject BuildTrolley(Transform env, Vector3 floorPos)
        {
            var paint = Mat("TrolleyBlue", new Color(0.1f, 0.3f, 0.78f), 0.5f, 0.3f);
            var rubber = Mat("TrolleyRubber", new Color(0.05f, 0.05f, 0.05f), 0.2f);
            var steel = Mat("TrolleySteel", new Color(0.6f, 0.62f, 0.65f), 0.5f, 0.8f);

            var root = new GameObject("HandTrolley").transform;
            root.SetParent(env, false);
            root.position = floorPos;

            var frame = new GameObject("Frame").transform;
            frame.SetParent(root, false);
            frame.localPosition = new Vector3(0f, 0.14f, -0.08f);
            frame.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            foreach (var x in new[] { -0.2f, 0.2f })
                Prim("Rail", PrimitiveType.Cube, frame, new Vector3(x, 0.5f, 0f), new Vector3(0.035f, 1.0f, 0.035f), paint);
            foreach (var y in new[] { 0.12f, 0.45f, 0.75f })
                Prim("Cross", PrimitiveType.Cube, frame, new Vector3(0f, y, 0f), new Vector3(0.4f, 0.025f, 0.025f), paint);
            var handle = Prim("Handle", PrimitiveType.Cylinder, frame, new Vector3(0f, 1.02f, -0.02f),
                new Vector3(0.03f, 0.24f, 0.03f), rubber);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Prim("NosePlate", PrimitiveType.Cube, root, new Vector3(0f, 0.06f, 0.2f), new Vector3(0.44f, 0.02f, 0.38f),
                steel);
            foreach (var x in new[] { -0.26f, 0.26f })
            {
                var wheel = Prim("Wheel", PrimitiveType.Cylinder, root, new Vector3(x, 0.11f, -0.05f),
                    new Vector3(0.22f, 0.035f, 0.22f), rubber);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.65f, 0.05f);
            col.size = new Vector3(0.95f, 1.3f, 0.95f);
            root.gameObject.AddComponent<PokeTarget>();
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
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

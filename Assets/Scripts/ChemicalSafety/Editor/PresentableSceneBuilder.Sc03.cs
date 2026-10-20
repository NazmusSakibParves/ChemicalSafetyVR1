#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    // Scenario 3: old chemical storage room modelled on Scene03 workflow step01 (dark concrete, rusty shelving,
    // old drums, one crystallised blue Organic Peroxide drum on a middle shelf). Sits 80 m to the side and is hidden
    // until the SC02 question is finished.
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc03Refs
        {
            public Sc03Inspect inspect;
            public Sc03Detonation detonation;
            public Transform spawn;
            public Transform stand;
        }

        private const float Sc03Ox = 80f;

        // viewer-right is -x when facing -z (the spawn direction); the hero shelf is on the right
        private static Sc03Refs BuildSc03Room(Transform env, PresentableArt art)
        {
            const float ox = Sc03Ox;
            const float half = 2.55f;      // inner half width
            const float zFront = 3.0f;
            const float zBack = -4.2f;
            const float ceil = 3.3f;
            const float heroZ = -1.2f;
            var refs = new Sc03Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            env.gameObject.AddComponent<Sc03Mood>();
            var solid = LayerMask.NameToLayer("SolidProps");

            var floorMat = TiledMat("Sc03Floor", "SC03_Floor", new Vector2(2.6f, 3.7f), 0.3f, 0f, Color.white);
            var wallBack = TiledMat("Sc03WallBack", "SC03_Wall", new Vector2(2.6f, 1.65f), 0.1f, 0f, Color.white);
            var wallSide = TiledMat("Sc03WallSide", "SC03_Wall", new Vector2(3.7f, 1.65f), 0.1f, 0f, Color.white);
            var ceilMat = Mat("Sc03Ceiling", new Color(0.2f, 0.2f, 0.21f), 0.1f, 0.1f);
            var pipeMat = Mat("Sc03Pipe", new Color(0.28f, 0.27f, 0.26f), 0.35f, 0.7f);
            var tube = Mat("RoomTube", Color.white, 0f, 0f, null, new Color(1.6f, 1.65f, 1.7f), true);
            var housing = Mat("RoomTubeHousing", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.6f);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solid >= 0)
                    go.layer = solid;
            }

            var depth = zFront - zBack;
            var cz = (zFront + zBack) * 0.5f;
            var floor = Box("Floor", room, new Vector3(ox, -0.05f, cz), new Vector3(half * 2f + 0.2f, 0.1f, depth + 0.2f), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(ox, ceil * 0.5f, zBack - 0.05f), new Vector3(half * 2f + 0.2f, ceil, 0.1f), wallBack);
            Shell("Wall_Front", new Vector3(ox, ceil * 0.5f, zFront + 0.05f), new Vector3(half * 2f + 0.2f, ceil, 0.1f), wallBack);
            Shell("Wall_Left", new Vector3(ox + half + 0.05f, ceil * 0.5f, cz), new Vector3(0.1f, ceil, depth), wallSide);
            Shell("Wall_Right", new Vector3(ox - half - 0.05f, ceil * 0.5f, cz), new Vector3(0.1f, ceil, depth), wallSide);
            Shell("Ceiling", new Vector3(ox, ceil + 0.04f, cz), new Vector3(half * 2f + 0.2f, 0.08f, depth + 0.2f), ceilMat);

            // pipes and conduit along the ceiling, a fluorescent tube like step01
            foreach (var px in new[] { -1.7f, 1.2f })
            {
                var pipe = Prim("CeilingPipe", PrimitiveType.Cylinder, room, new Vector3(ox + px, ceil - 0.12f, cz),
                    new Vector3(0.12f, depth * 0.5f, 0.12f), pipeMat);
                pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            foreach (var t in new[] { new Vector3(-0.9f, 0f, -1.0f), new Vector3(0.8f, 0f, 1.4f), new Vector3(-0.3f, 0f, -3.1f) })
            {
                Box("TubeHousing", room, new Vector3(ox + t.x, ceil - 0.05f, t.z), new Vector3(0.26f, 0.05f, 1.5f), housing, false);
                Box("Tube", room, new Vector3(ox + t.x, ceil - 0.09f, t.z), new Vector3(0.14f, 0.03f, 1.3f), tube, false);
            }

            MakeLight(room, "RoomLightA", new Vector3(ox - 0.9f, 2.85f, -1.0f), new Color(1f, 0.93f, 0.8f), 2.4f, 7f);
            MakeLight(room, "RoomLightB", new Vector3(ox + 0.8f, 2.85f, 1.4f), new Color(1f, 0.93f, 0.8f), 1.5f, 6f);
            MakeLight(room, "HeroLight", new Vector3(ox - 1.1f, 2.3f, heroZ + 0.4f), new Color(1f, 0.95f, 0.88f), 1.6f, 3.2f);

            // --- shelving
            var shelfMesh = LoadObjMesh(ArtRoot + "/3D/Sc03Shelf.obj");
            var shelfMat = Mat("Sc03Shelf", new Color(0.4f, 0.33f, 0.27f), 0.3f, 0.6f);
            var deckMat = Mat("Sc03Deck", new Color(0.3f, 0.28f, 0.26f), 0.25f, 0.5f);
            var detail = EnvTex("DRUM_Detail");
            var drumMats = new[]
            {
                Mat("Sc03DrumA", new Color(0.1f, 0.15f, 0.28f), 0.3f, 0.5f, detail),
                Mat("Sc03DrumB", new Color(0.17f, 0.18f, 0.19f), 0.3f, 0.5f, detail),
                Mat("Sc03DrumC", new Color(0.27f, 0.2f, 0.1f), 0.3f, 0.45f, detail),
                Mat("Sc03DrumD", new Color(0.2f, 0.25f, 0.3f), 0.3f, 0.5f, detail)
            };
            var drumMesh = RealisticDrumMesh();
            var fadedLabel = LabelMat("Sc03Label_Faded", "faded");
            var residueMat = MatAlpha("Sc03Residue", EnvTex("SC03_Residue"), new Color(0.78f, 0.78f, 0.75f, 0.85f), false);
            var rnd = new System.Random(303);
            float[] decks = { 0.12f, 1.36f, 2.62f };

            // (centre, yaw, side towards the aisle, depth axis is z for the back shelf)
            var bays = new[]
            {
                new { pos = new Vector3(ox - half + 0.37f, 0f, heroZ), yaw = 90f, side = -1f, hero = true },
                new { pos = new Vector3(ox - half + 0.37f, 0f, heroZ + 2.7f), yaw = 90f, side = -1f, hero = false },
                new { pos = new Vector3(ox + half - 0.37f, 0f, heroZ), yaw = 90f, side = 1f, hero = false },
                new { pos = new Vector3(ox + half - 0.37f, 0f, heroZ + 2.7f), yaw = 90f, side = 1f, hero = false },
                new { pos = new Vector3(ox, 0f, zBack + 0.37f), yaw = 0f, side = 0f, hero = false }
            };

            foreach (var bay in bays)
            {
                var holder = new GameObject("ShelfBay").transform;
                holder.SetParent(room, false);
                holder.SetPositionAndRotation(bay.pos, Quaternion.Euler(0f, bay.yaw, 0f));
                MeshObj("Shelf", holder, shelfMesh, Vector3.zero, Vector3.one, shelfMat);
                foreach (var dy in decks)
                    Box("Deck", holder, bay.pos + Vector3.up * (dy - 0.02f), new Vector3(2.5f, 0.04f, 0.66f), deckMat, false)
                        .transform.rotation = Quaternion.Euler(0f, bay.yaw, 0f);
                var sx = Mathf.Abs(Mathf.Sin(bay.yaw * Mathf.Deg2Rad)) > 0.5f;
                AddSolidBox(room, "ShelfSolid", bay.pos + Vector3.up * 1.8f,
                    sx ? new Vector3(0.78f, 3.6f, 2.7f) : new Vector3(2.7f, 3.6f, 0.78f));

                for (var tier = 0; tier < decks.Length; tier++)
                {
                    var slots = new[] { -0.95f, -0.32f, 0.32f, 0.95f };
                    foreach (var along in slots)
                    {
                        if (rnd.NextDouble() < 0.12)
                            continue;
                        // the hero bay keeps the middle shelf open around the crystallised drum
                        if (bay.hero && tier == 1 && Mathf.Abs(along) < 0.7f)
                            continue;
                        var local = new Vector3(along, 0f, 0f);
                        var wp = bay.pos + Quaternion.Euler(0f, bay.yaw, 0f) * local + Vector3.up * (decks[tier] + 0.44f);
                        var d = MeshObj("Drum", room, drumMesh, wp, new Vector3(0.57f, 0.88f, 0.57f),
                            drumMats[rnd.Next(drumMats.Length)]);
                        d.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                        if (bay.side != 0f && rnd.NextDouble() < 0.5)
                            AddDrumLabel(room, wp, bay.side, fadedLabel, rnd);
                    }
                }
            }

            // crystal residue on the decks and floor
            QuadObj("ResidueDeck", room, new Vector3(ox - half + 0.37f, decks[1] + 0.004f, heroZ), new Vector3(90f, 0f, 0f),
                new Vector2(1.0f, 0.8f), residueMat);
            QuadObj("ResidueDeckLow", room, new Vector3(ox - half + 0.37f, decks[0] + 0.004f, heroZ + 0.5f), new Vector3(90f, 30f, 0f),
                new Vector2(0.8f, 0.6f), residueMat);
            QuadObj("ResidueFloorA", room, new Vector3(ox - 1.5f, 0.006f, heroZ - 0.2f), new Vector3(90f, 10f, 0f),
                new Vector2(1.0f, 0.75f), residueMat);

            // two real old drums on the floor (designer model, 10k triangles: only one pair)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc03OldDrums.glb") != null)
            {
                var pair = PlaceSizedModel("3D/Sc03OldDrums", "OldDrums", room, new Vector3(ox + 1.55f, 0f, 0.9f), 35f, 0.9f);
                AddSolidBox(room, "OldDrumsSolid", new Vector3(ox + 1.55f, 0.5f, 0.9f), new Vector3(1.2f, 1f, 1.2f));
            }

            // --- hero drum: crystallised Organic Peroxide, label facing the aisle (+x)
            var heroPos = new Vector3(ox - half + 0.37f, decks[1], heroZ);
            var hero = PlaceSizedModel("3D/Sc03Drum", "HeroDrum", room, heroPos, 0f, 0.9f);
            var anchor = new GameObject("LabelAnchor").transform;
            anchor.SetParent(hero.transform, true);
            // the label is the sub-mesh with the "Material.003" material (all three parts share one renderer)
            var haveLabel = false;
            var labelWorld = Vector3.zero;
            foreach (var mf in hero.GetComponentsInChildren<MeshFilter>())
            {
                var mats = mf.GetComponent<Renderer>().sharedMaterials;
                for (var si = 0; si < mats.Length && si < mf.sharedMesh.subMeshCount; si++)
                {
                    if (mats[si] == null || !mats[si].name.Contains("Material.003"))
                        continue;
                    labelWorld = mf.transform.TransformPoint(mf.sharedMesh.GetSubMesh(si).bounds.center);
                    haveLabel = true;
                }
            }

            var body = WorldBounds(hero);
            var dir = Vector3.right;
            if (haveLabel)
            {
                var d = labelWorld - body.center;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0004f)
                    dir = d.normalized;
            }
            else
            {
                Debug.LogWarning("[Presentable] SC03 hero drum: label sub-mesh not found, label direction guessed.");
            }

            // turn the drum so the label faces the aisle (+x), pivot = drum centre
            hero.transform.Rotate(0f, Vector3.SignedAngle(dir, Vector3.right, Vector3.up), 0f, Space.World);
            body = WorldBounds(hero);
            var labelY = body.center.y + 0.05f;
            if (haveLabel)
            {
                foreach (var mf in hero.GetComponentsInChildren<MeshFilter>())
                {
                    var mats = mf.GetComponent<Renderer>().sharedMaterials;
                    for (var si = 0; si < mats.Length && si < mf.sharedMesh.subMeshCount; si++)
                    {
                        if (mats[si] != null && mats[si].name.Contains("Material.003"))
                            labelY = mf.transform.TransformPoint(mf.sharedMesh.GetSubMesh(si).bounds.center).y;
                    }
                }
            }

            anchor.position = new Vector3(body.center.x + 0.29f, labelY, body.center.z);
            anchor.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            AddSolidBox(room, "HeroDrumSolid", body.center, new Vector3(0.62f, body.size.y, 0.62f));

            // floating arrow + inspection
            var touchGo = new GameObject("TouchButton_SC03");
            touchGo.transform.SetParent(room, false);
            touchGo.transform.position = new Vector3(ox - half + 0.37f + 0.29f + 0.42f, 1.55f, heroZ);
            var touch = touchGo.AddComponent<FloatingTouchButton>();
            SetRef(touch, "art", art);
            var tso = new SerializedObject(touch);
            tso.FindProperty("pressRadius").floatValue = 0.12f;
            tso.FindProperty("hoverRadius").floatValue = 0.22f;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var inspectGo = new GameObject("Sc03Inspect");
            inspectGo.transform.SetParent(room, false);
            refs.inspect = inspectGo.AddComponent<Sc03Inspect>();
            SetRef(refs.inspect, "art", art);
            SetRef(refs.inspect, "touchButton", touch);
            SetRef(refs.inspect, "labelAnchor", anchor);

            // --- detonation (wrong answer): siren lamps above the hero bay, particle materials are scene assets
            var lampOff = Mat("Sc03LampOff", new Color(0.4f, 0.03f, 0.03f), 0.5f, 0.1f);
            var lampOn = Mat("Sc03LampOn", new Color(1f, 0.12f, 0.06f), 0f, 0f, null, new Color(2.2f, 0.1f, 0.05f), true);
            var lamps = new Renderer[3];
            var lights = new UnityEngine.Light[3];
            for (var i = 0; i < 3; i++)
            {
                var lp = new Vector3(ox - half + 0.5f, 3.05f, heroZ + (i - 1) * 0.6f);
                lamps[i] = Prim("SirenLamp" + i, PrimitiveType.Cylinder, room, lp, new Vector3(0.14f, 0.09f, 0.14f), lampOff)
                    .GetComponent<Renderer>();
                lights[i] = MakeLight(room, "SirenLight" + i, lp + Vector3.down * 0.2f, new Color(1f, 0.1f, 0.05f), 0f, 5f);
            }

            var glow = art.radialGlow != null ? art.radialGlow.texture : null;
            var flame = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Generated/FX_Flame.png");
            var detGo = new GameObject("Sc03Detonation");
            detGo.transform.SetParent(room, false);
            detGo.transform.position = new Vector3(body.center.x, body.max.y - 0.05f, body.center.z);
            refs.detonation = detGo.AddComponent<Sc03Detonation>();
            var dso = new SerializedObject(refs.detonation);
            dso.FindProperty("origin").objectReferenceValue = detGo.transform;
            dso.FindProperty("drum").objectReferenceValue = hero.transform;
            dso.FindProperty("fireMaterial").objectReferenceValue = ParticleMat("Sc03Fire", flame != null ? flame : glow, true);
            dso.FindProperty("smokeMaterial").objectReferenceValue = ParticleMat("Sc03Smoke", glow, false);
            dso.FindProperty("sparkMaterial").objectReferenceValue = ParticleMat("Sc03Spark", glow, true);
            dso.FindProperty("lampOn").objectReferenceValue = lampOn;
            dso.FindProperty("lampOff").objectReferenceValue = lampOff;
            var lampProp = dso.FindProperty("sirenLamps");
            lampProp.arraySize = lamps.Length;
            var lightProp = dso.FindProperty("sirenLights");
            lightProp.arraySize = lights.Length;
            for (var i = 0; i < lamps.Length; i++)
            {
                lampProp.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
                lightProp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }

            dso.ApplyModifiedPropertiesWithoutUndo();

            refs.spawn = Marker("Spawn_SC03", env, new Vector3(ox, 0f, 2.4f), 180f);
            // about 0.9 m from the drum surface, facing it (-x)
            refs.stand = Marker("Stand_SC03", env, new Vector3(ox - half + 0.37f + 0.29f + 0.95f, 0f, heroZ), 270f);
            return refs;
        }

        private static UnityEngine.Light MakeLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // Particle material (URP Particles/Unlit multiplies the particle colour, plain Unlit would not).
        private static Material ParticleMat(string name, Texture tex, bool additive)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;
            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
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
            m.SetColor("_BaseColor", Color.white);
            if (tex != null)
                m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // Instantiates a glb scaled so its tallest part is targetHeight, bottom centre on floorPos, yaw about the pivot.
        private static GameObject PlaceSizedModel(string rel, string name, Transform parent, Vector3 floorPos, float yaw,
            float targetHeight)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(floorPos, Quaternion.identity);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + rel + ".glb");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(root.transform, false);

            var b = WorldBounds(model);
            var k = targetHeight / Mathf.Max(0.01f, b.size.y);
            model.transform.localScale *= k;
            b = WorldBounds(model);
            model.transform.position += floorPos - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return root;
        }
    }
}
#endif

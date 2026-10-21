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
            const float ceil = 3.7f;
            const float heroZ = -1.2f;
            var refs = new Sc03Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            env.gameObject.AddComponent<Sc03Mood>();
            var solid = LayerMask.NameToLayer("SolidProps");

            ConfigureSc03Textures();
            var floorMat = PbrMat("Sc03FloorPbr", "Floor_Albedo", "Floor_Normal", "Floor_MS", new Color(0.82f, 0.8f, 0.76f),
                new Vector2(2.2f, 3.0f), 0.7f);
            var wallBack = PbrMat("Sc03WallBackPbr", "Wall_Albedo", "Wall_Normal", "Wall_MS", new Color(0.78f, 0.76f, 0.72f),
                new Vector2(2.2f, 1.5f));
            var wallSide = PbrMat("Sc03WallSidePbr", "Wall_Albedo", "Wall_Normal", "Wall_MS", new Color(0.78f, 0.76f, 0.72f),
                new Vector2(3.0f, 1.5f));
            var ceilMat = PbrMat("Sc03CeilPbr", "Wall_Albedo", "Wall_Normal", "Wall_MS", new Color(0.4f, 0.39f, 0.38f),
                new Vector2(2.2f, 3.0f));
            var moteGlow = art.radialGlow != null ? art.radialGlow.texture : null;
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

            var lightA = MakeLight(room, "RoomLightA", new Vector3(ox - 0.9f, ceil - 0.45f, -1.0f), new Color(1f, 0.93f, 0.8f), 2.4f, 7f);
            var lightB = MakeLight(room, "RoomLightB", new Vector3(ox + 0.8f, ceil - 0.45f, 1.4f), new Color(1f, 0.93f, 0.8f), 1.5f, 6f);
            MakeLight(room, "HeroLight", new Vector3(ox - 1.1f, 2.3f, heroZ + 0.4f), new Color(1f, 0.95f, 0.88f), 1.6f, 3.2f);

            // --- shelving: rusty steel racks (designer rust textures) and old barrels (designer barrel mesh)
            var props = new GameObject("StaticProps").transform;
            props.SetParent(room, false);
            const float rackH = 3.5f;
            float[] decks = { 0.12f, 1.36f, 2.5f };
            var rackMesh = Sc03RackMesh(decks, rackH);
            var rackMat = PbrMat("Sc03RackPbr", "Rust_Albedo", "Rust_Normal", "Rust_MS", new Color(0.8f, 0.72f, 0.66f),
                Vector2.one, 0.9f);
            var barrelMesh = Sc03BarrelMesh();
            Material Barrel(string n, Color tint) =>
                PbrMat(n, "Barrel_Albedo", "Barrel_Normal", "Barrel_MS", tint, Vector2.one, 0.85f);
            var barrelMats = new[]
            {
                Barrel("Sc03BarrelA", new Color(0.78f, 0.72f, 0.68f)),   // bare rust
                Barrel("Sc03BarrelB", new Color(0.5f, 0.58f, 0.78f)),    // blue-grey paint
                Barrel("Sc03BarrelC", new Color(0.55f, 0.68f, 0.5f)),    // faded green
                Barrel("Sc03BarrelD", new Color(0.4f, 0.37f, 0.35f)),    // soot
                Barrel("Sc03BarrelE", new Color(0.78f, 0.45f, 0.4f))     // red-brown
            };
            var fadedLabel = LabelMat("Sc03Label_Faded", "faded");
            var residueMat = MatAlpha("Sc03Residue", EnvTex("SC03_Residue"), new Color(0.62f, 0.62f, 0.58f, 0.7f), false);
            var rnd = new System.Random(303);

            // (centre, yaw so that local -z is the wall side, side towards the aisle)
            var bays = new[]
            {
                new { pos = new Vector3(ox - half + 0.37f, 0f, heroZ), yaw = 90f, side = -1f, hero = true },
                new { pos = new Vector3(ox - half + 0.37f, 0f, heroZ + 2.7f), yaw = 90f, side = -1f, hero = false },
                new { pos = new Vector3(ox + half - 0.37f, 0f, heroZ), yaw = 270f, side = 1f, hero = false },
                new { pos = new Vector3(ox + half - 0.37f, 0f, heroZ + 2.7f), yaw = 270f, side = 1f, hero = false },
                new { pos = new Vector3(ox, 0f, zBack + 0.37f), yaw = 0f, side = 0f, hero = false }
            };

            foreach (var bay in bays)
            {
                var rot = Quaternion.Euler(0f, bay.yaw, 0f);
                var rack = MeshObj("Rack", props, rackMesh, bay.pos, Vector3.one, rackMat);
                rack.transform.rotation = rot;
                var sx = Mathf.Abs(Mathf.Sin(bay.yaw * Mathf.Deg2Rad)) > 0.5f;
                AddSolidBox(room, "ShelfSolid", bay.pos + Vector3.up * 1.8f,
                    sx ? new Vector3(0.78f, 3.6f, 2.7f) : new Vector3(2.7f, 3.6f, 0.78f));

                for (var tier = 0; tier < decks.Length; tier++)
                {
                    foreach (var along in new[] { -0.95f, -0.32f, 0.32f, 0.95f })
                    {
                        if (rnd.NextDouble() < 0.1)
                            continue;
                        // the hero bay keeps the middle shelf open around the crystallised drum
                        if (bay.hero && tier == 1 && Mathf.Abs(along) < 0.7f)
                            continue;
                        var wp = bay.pos + rot * new Vector3(along + ((float)rnd.NextDouble() - 0.5f) * 0.05f, 0f,
                            ((float)rnd.NextDouble() - 0.5f) * 0.05f) + Vector3.up * decks[tier];
                        var jitter = 0.97f + (float)rnd.NextDouble() * 0.06f;
                        var d = MeshObj("Barrel", props, barrelMesh, wp, new Vector3(jitter, 0.97f + (float)rnd.NextDouble() * 0.06f, jitter),
                            barrelMats[rnd.Next(barrelMats.Length)]);
                        d.transform.rotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 2.4f,
                            (float)rnd.NextDouble() * 360f, ((float)rnd.NextDouble() - 0.5f) * 2.4f);
                        if (bay.side != 0f && rnd.NextDouble() < 0.4)
                            AddDrumLabel(props, wp + Vector3.up * 0.45f, bay.side, fadedLabel, rnd);
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

            // --- floor clutter and dirt (kept out of the walking line x = ox and the inspection stand)
            var woodMat = Mat("PalletWood", new Color(0.62f, 0.45f, 0.26f), 0.15f);
            var boxMat = Mat("Sc03Cardboard", new Color(0.36f, 0.28f, 0.19f), 0.1f);
            var tapeMat = Mat("Sc03Tape", new Color(0.62f, 0.5f, 0.3f), 0.2f);
            void FloorBarrel(Vector3 p, bool lying, float yaw, int mat)
            {
                var go = MeshObj("FloorBarrel", props, barrelMesh, p, Vector3.one, barrelMats[mat % barrelMats.Length]);
                if (lying)
                {
                    var q = Quaternion.Euler(90f, yaw, 0f);
                    go.transform.rotation = q;
                    go.transform.position = new Vector3(p.x, 0.285f, p.z) - q * new Vector3(0f, 0.45f, 0f);
                }
                else
                {
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }

            // pallet with upright barrels near the right wall, front half
            Box("Pallet", props, new Vector3(ox - 1.55f, 0.07f, 2.05f), new Vector3(1.15f, 0.14f, 1.15f), woodMat, false);
            FloorBarrel(new Vector3(ox - 1.8f, 0.14f, 1.8f), false, 30f, 0);
            FloorBarrel(new Vector3(ox - 1.3f, 0.14f, 2.25f), false, 140f, 3);
            FloorBarrel(new Vector3(ox - 1.8f, 0.14f, 2.3f), false, 80f, 1);
            AddSolidBox(room, "PalletSolid", new Vector3(ox - 1.55f, 0.55f, 2.05f), new Vector3(1.2f, 1.1f, 1.2f));
            // fallen barrels left behind the rack ends
            FloorBarrel(new Vector3(ox + 1.1f, 0f, 0.55f), true, 70f, 4);
            FloorBarrel(new Vector3(ox + 1.45f, 0f, -2.95f), true, 20f, 0);
            FloorBarrel(new Vector3(ox - 1.2f, 0f, -3.05f), false, 0f, 2);
            // cardboard boxes in the front left corner
            foreach (var cb in new[]
                     {
                         new Vector4(ox + 1.6f, 0.22f, 2.35f, 12f), new Vector4(ox + 1.1f, 0.18f, 2.55f, -20f),
                         new Vector4(ox + 1.55f, 0.6f, 2.4f, 35f)
                     })
            {
                var size = new Vector3(0.55f, cb.y * 2f, 0.45f);
                var bx = Box("CardboardBox", props, new Vector3(cb.x, cb.y, cb.z), size, boxMat, false);
                bx.transform.rotation = Quaternion.Euler(0f, cb.w, 0f);
                var tape = Box("Tape", props, new Vector3(cb.x, cb.y * 2f + 0.002f, cb.z), new Vector3(0.1f, 0.004f, 0.46f), tapeMat, false);
                tape.transform.rotation = Quaternion.Euler(0f, cb.w, 0f);
            }

            AddSolidBox(room, "BoxesSolid", new Vector3(ox + 1.45f, 0.4f, 2.45f), new Vector3(1.1f, 0.9f, 0.7f));

            // dirt: dark blots and drips, white crystal residue
            var blotMat = DecalMat("Sc03Blot", "Stain_Blot", new Color(1f, 1f, 1f, 0.7f));
            var dripMat = DecalMat("Sc03Drip", "Grime_Drip", new Color(1f, 1f, 1f, 0.85f));
            foreach (var bl in new[]
                     {
                         new Vector4(ox - 0.3f, -2.2f, 1.6f, 20f), new Vector4(ox + 0.6f, -0.2f, 1.2f, 80f),
                         new Vector4(ox - 0.9f, 1.3f, 1.8f, 150f), new Vector4(ox + 0.5f, 2.0f, 1.4f, 40f),
                         new Vector4(ox + 0.2f, -3.4f, 1.7f, 110f), new Vector4(ox + 1.3f, -1.4f, 1.0f, 10f),
                         new Vector4(ox - 1.5f, -2.6f, 1.2f, 60f)
                     })
                DecalQuad("FloorBlot", props, new Vector3(bl.x, 0.005f, bl.y), Vector3.up, Quaternion.Euler(0f, bl.w, 0f) * Vector3.forward,
                    new Vector2(bl.z, bl.z * 0.75f), blotMat);
            var dripXs = new[] { -2.1f, -0.9f, 0.7f, 1.9f };
            foreach (var dx in dripXs)
                DecalQuad("WallDrip", props, new Vector3(ox + dx, ceil - 1.2f, zBack + 0.012f), Vector3.forward, Vector3.up,
                    new Vector2(0.7f, 2.4f), dripMat);
            foreach (var dz in new[] { -3.0f, -1.2f, 0.9f, 2.1f })
            {
                DecalQuad("WallDrip", props, new Vector3(ox - half + 0.012f, ceil - 1.2f, dz), Vector3.right, Vector3.up,
                    new Vector2(0.7f, 2.4f), dripMat);
                DecalQuad("WallDrip", props, new Vector3(ox + half - 0.012f, ceil - 1.2f, dz + 0.4f), Vector3.left, Vector3.up,
                    new Vector2(0.7f, 2.4f), dripMat);
            }

            // cobwebs: every ceiling corner and the top corners of every rack bay
            var webCorner = DecalMat("Sc03WebCorner", "Web_Corner", new Color(1f, 1f, 1f, 0.8f));
            var webFull = DecalMat("Sc03WebFull", "Web_Full", new Color(1f, 1f, 1f, 0.7f));
            foreach (var sxc in new[] { -1f, 1f })
            {
                var cx = ox + sxc * half;
                WebCorner(props, new Vector3(cx, ceil, zBack), Vector3.forward, new Vector3(-sxc, 0f, 0f), 1.0f, webCorner);
                WebCorner(props, new Vector3(cx, ceil, zFront), Vector3.back, new Vector3(-sxc, 0f, 0f), 1.0f, webCorner);
                WebCorner(props, new Vector3(cx, ceil, zBack), new Vector3(-sxc, 0f, 0f), Vector3.forward, 1.0f, webCorner);
                WebCorner(props, new Vector3(cx, ceil, zFront), new Vector3(-sxc, 0f, 0f), Vector3.back, 1.0f, webCorner);
            }

            foreach (var bay in bays)
            {
                var rot = Quaternion.Euler(0f, bay.yaw, 0f);
                var frontN = rot * Vector3.forward;
                foreach (var sxc in new[] { -1f, 1f })
                {
                    var anchor3 = bay.pos + rot * new Vector3(sxc * 1.27f, rackH - 0.05f, 0.34f);
                    WebCorner(props, anchor3, frontN, rot * new Vector3(-sxc, 0f, 0f), 0.75f, webCorner);
                }

                // a round web hanging in the front plane of the top tier
                DecalQuad("WebFull", props, bay.pos + rot * new Vector3((float)(rnd.NextDouble() - 0.5) * 1.4f, 2.35f, 0.345f),
                    frontN, Vector3.up, new Vector2(0.55f, 0.55f), webFull);
            }

            // dust motes, light shafts under the tubes, an old tube that dims now and then
            BuildDust(room, new Vector3(ox, 1.7f, cz), new Vector3(4.6f, 2.8f, 6.6f), ParticleMat("Sc03Mote", moteGlow, true));
            var shaftMat = DecalMat("Sc03Shaft", "Shaft", new Color(1f, 0.95f, 0.8f, 0.24f), true);
            foreach (var tl in new[] { new Vector2(-0.9f, -1.0f), new Vector2(0.8f, 1.4f), new Vector2(-0.3f, -3.1f) })
                LightShaft(props, new Vector3(ox + tl.x, ceil - 0.1f, tl.y), 2.4f, 1.1f, shaftMat);
            var flicker = room.gameObject.AddComponent<Sc03Flicker>();
            var fso = new SerializedObject(flicker);
            var fl = fso.FindProperty("lights");
            fl.arraySize = 2;
            fl.GetArrayElementAtIndex(0).objectReferenceValue = lightA;
            fl.GetArrayElementAtIndex(1).objectReferenceValue = lightB;
            fso.ApplyModifiedPropertiesWithoutUndo();

            foreach (var tr in props.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);

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
                var lp = new Vector3(ox - 1.45f, ceil - 0.18f, heroZ + (i - 1) * 0.6f);
                lamps[i] = Prim("SirenLamp" + i, PrimitiveType.Cylinder, room, lp, new Vector3(0.14f, 0.09f, 0.14f), lampOff)
                    .GetComponent<Renderer>();
                lights[i] = MakeLight(room, "SirenLight" + i, lp + Vector3.down * 0.2f, new Color(1f, 0.1f, 0.05f), 0f, 5f);
            }

            Texture2D Gen(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Generated/" + file);
            var glow = art.radialGlow != null ? art.radialGlow.texture : null;
            var ringTex = art.ring != null ? art.ring.texture : null;

            // burst drum (top peeled open, charred), blown-off lid, scorch marks
            var burst = BuildBurstDrum(room, hero, heroPos);
            var lidGo = Prim("DrumLid", PrimitiveType.Cylinder, room, heroPos, new Vector3(0.58f, 0.025f, 0.58f),
                Mat("Sc03Lid", new Color(0.05f, 0.22f, 0.72f), 0.55f, 0.15f));
            lidGo.SetActive(false);
            var sootTex = Gen("FX_Soot.png");
            var sootMat = MatAlpha("Sc03Soot", sootTex, new Color(1f, 1f, 1f, 0f), false);
            var wallX = ox - half + 0.012f;
            var soot = new[]
            {
                QuadObj("SootWall", room, new Vector3(wallX, body.center.y + 0.35f, heroZ), new Vector3(0f, -90f, 0f),
                    new Vector2(2.1f, 2.1f), sootMat).GetComponent<Renderer>(),
                QuadObj("SootDeckTop", room, new Vector3(ox - half + 0.37f, decks[2] - 0.045f, heroZ), new Vector3(-90f, 0f, 0f),
                    new Vector2(1.3f, 0.9f), sootMat).GetComponent<Renderer>(),
                QuadObj("SootDeck", room, new Vector3(ox - half + 0.37f, decks[1] + 0.006f, heroZ), new Vector3(90f, 0f, 0f),
                    new Vector2(1.1f, 0.8f), sootMat).GetComponent<Renderer>()
            };

            var detGo = new GameObject("Sc03Detonation");
            detGo.transform.SetParent(room, false);
            detGo.transform.position = new Vector3(body.center.x, body.max.y - 0.05f, body.center.z);
            refs.detonation = detGo.AddComponent<Sc03Detonation>();
            var dso = new SerializedObject(refs.detonation);
            dso.FindProperty("origin").objectReferenceValue = detGo.transform;
            dso.FindProperty("drum").objectReferenceValue = hero.transform;
            dso.FindProperty("burstDrum").objectReferenceValue = burst;
            dso.FindProperty("lid").objectReferenceValue = lidGo.transform;
            dso.FindProperty("fireSheet").objectReferenceValue = ParticleMat("Sc03FireSheet", Gen("FX_FireSheet.png"), true);
            dso.FindProperty("smokeSheet").objectReferenceValue = ParticleMat("Sc03SmokeSheet", Gen("FX_SmokeSheet.png"), false);
            dso.FindProperty("glowAdd").objectReferenceValue = ParticleMat("Sc03Spark", glow, true);
            dso.FindProperty("dustMaterial").objectReferenceValue = ParticleMat("Sc03Dust", glow, false);
            dso.FindProperty("ringMaterial").objectReferenceValue = ParticleMat("Sc03Ring", ringTex, true);
            dso.FindProperty("shardMaterial").objectReferenceValue = ShardMat();
            dso.FindProperty("shardMesh").objectReferenceValue = ShardMesh();
            dso.FindProperty("lampOn").objectReferenceValue = lampOn;
            dso.FindProperty("lampOff").objectReferenceValue = lampOff;
            dso.FindProperty("vignette").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/2D/Generated/FX_Vignette.png");
            var sootProp = dso.FindProperty("scorch");
            sootProp.arraySize = soot.Length;
            for (var i = 0; i < soot.Length; i++)
                sootProp.GetArrayElementAtIndex(i).objectReferenceValue = soot[i];
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

        // Plastic-blue lit particle material for the flying drum shards.
        private static Material ShardMat()
        {
            const string name = "Sc03Shard";
            if (Mats.TryGetValue(name, out var cached))
                return cached;
            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
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

            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_Smoothness", 0.55f);
            m.SetColor("_BaseColor", new Color(0.05f, 0.24f, 0.78f));
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // A thin, slightly bent triangular plastic chip (both faces), used as a mesh particle.
        private static Mesh ShardMesh()
        {
            var path = GenDir + "/Mesh_Sc03Shard.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Sc03Shard" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var v = new[]
            {
                new Vector3(0f, 0.55f, 0f), new Vector3(-0.32f, -0.4f, 0.05f), new Vector3(0.4f, -0.3f, -0.04f),
                new Vector3(0.05f, -0.05f, 0.09f)
            };
            var verts = new System.Collections.Generic.List<Vector3>(v);
            var tris = new System.Collections.Generic.List<int>
            {
                0, 1, 3, 1, 2, 3, 2, 0, 3,   // front fan
                0, 3, 1, 1, 3, 2, 2, 3, 0    // back faces
            };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // The hero drum after the blast: a copy of its mesh with the top peeled open into jagged petals and the lid
        // sunk into a hole, charred and glowing. Hidden until Sc03Detonation swaps it in.
        private static GameObject BuildBurstDrum(Transform room, GameObject hero, Vector3 floorPos)
        {
            var burst = PlaceSizedModel("3D/Sc03Drum", "HeroDrumBurst", room, floorPos, 0f, 0.9f);
            burst.transform.rotation = hero.transform.rotation;
            var mf = burst.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("[Presentable] SC03 burst drum: no mesh found.");
                burst.SetActive(false);
                return burst;
            }

            var src = mf.sharedMesh;
            var verts = src.vertices;
            var local = new Vector3[verts.Length];
            var top = float.MinValue;
            var rMax = 0f;
            for (var i = 0; i < verts.Length; i++)
            {
                local[i] = burst.transform.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));
                top = Mathf.Max(top, local[i].y);
                rMax = Mathf.Max(rMax, new Vector2(local[i].x, local[i].z).magnitude);
            }

            for (var i = 0; i < local.Length; i++)
            {
                var p = local[i];
                var r = new Vector2(p.x, p.z).magnitude;
                var ang = Mathf.Atan2(p.z, p.x);
                var n1 = 0.5f + 0.5f * Mathf.Sin(3f * ang + 1.3f) * Mathf.Cos(5f * ang - 0.4f);
                var n2 = 0.5f + 0.5f * Mathf.Sin(7f * ang + 0.7f);
                var n3 = 0.5f + 0.5f * Mathf.Sin(11f * ang - 1.1f) * Mathf.Cos(4f * ang);
                var cutY = top * (0.74f + 0.1f * n1);
                if (p.y > cutY)
                {
                    if (p.y > top - 0.03f && r < rMax * 0.8f)
                    {
                        // the lid cap sinks into a dark hole
                        p.y = cutY - 0.05f - 0.04f * n2;
                        var k = r > 1e-4f ? 0.82f : 1f;
                        p.x *= k;
                        p.z *= k;
                    }
                    else
                    {
                        var s = (p.y - cutY) * (0.6f + 0.8f * n3);
                        var phi = (65f + 55f * n2) * Mathf.Deg2Rad;
                        var dir = r > 1e-4f ? new Vector2(p.x, p.z) / r : Vector2.right;
                        var rr = r + s * Mathf.Sin(phi) * 1.25f;
                        p.x = dir.x * rr;
                        p.z = dir.y * rr;
                        p.y = cutY + s * Mathf.Cos(phi) * 0.6f - s * 0.35f;
                    }
                }

                local[i] = p;
            }

            for (var i = 0; i < verts.Length; i++)
                verts[i] = mf.transform.InverseTransformPoint(burst.transform.TransformPoint(local[i]));

            var path = GenDir + "/Mesh_Sc03BurstDrum.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(src);
                mesh.name = "Sc03BurstDrum";
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.indexFormat = src.indexFormat;
            mesh.vertices = verts;
            mesh.uv = src.uv;
            mesh.subMeshCount = src.subMeshCount;
            for (var si = 0; si < src.subMeshCount; si++)
                mesh.SetTriangles(src.GetTriangles(si), si);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            mf.sharedMesh = mesh;

            // charred copies of the glTF materials (body glows like hot metal)
            var rend = mf.GetComponent<Renderer>();
            var mats = rend.sharedMaterials;
            var burnt = new Material[mats.Length];
            for (var i = 0; i < mats.Length; i++)
            {
                var mp = GenDir + "/M_Sc03Burnt_" + i + ".mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (copy == null)
                {
                    copy = new Material(mats[i]);
                    AssetDatabase.CreateAsset(copy, mp);
                }
                else
                {
                    copy.CopyPropertiesFromMaterial(mats[i]);
                }

                if (copy.HasProperty("baseColorFactor"))
                {
                    var c = mats[i].GetColor("baseColorFactor");
                    var dark = i == 0 ? 0.22f : 0.4f;
                    copy.SetColor("baseColorFactor", new Color(c.r * dark, c.g * dark, c.b * dark, c.a));
                }

                if (i == 0 && copy.HasProperty("emissiveFactor"))
                    copy.SetColor("emissiveFactor", new Color(0.7f, 0.17f, 0.03f));
                EditorUtility.SetDirty(copy);
                burnt[i] = copy;
            }

            rend.sharedMaterials = burnt;
            burst.SetActive(false);
            return burst;
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

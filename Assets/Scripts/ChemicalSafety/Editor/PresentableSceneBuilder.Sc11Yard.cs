#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 11 on the controller ray: the client's chlorine cylinder yard (outdoor, hazy daylight). The learner stands in front of a
    /// concrete wall with a tin roof, the chained cylinders (grey / green, CHLORINE stencil) against it, a leaking one in front, the toxic
    /// gas sign above, a fire-alarm box, a green chain-link fence and a pipe rack along the street on the left, a wind sock, the plant in
    /// the haze. User faces -Z (viewer's left = +X). Env origin x = 400.
    /// ONE switch: <see cref="Sc11UseYard"/>. false + Build Presentable Scene = the previous Scenario 11 yard with the finger-touch flow.
    /// Shared tail (workers, radio, host, gate card) lives here so the old and the new yard use the same code.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc11UseYard = true;

        private sealed class Sc11Parts
        {
            public ParticleSystem gas, purple;
            public Renderer[] frost;
            public Sc07Beacon[] beacons;
            public GameObject exitSign, outside;
            public Sc07Human[] workers;
            public Transform[] starts, path;
            public Transform radio, radioScreen, valveAt, sirenAt;
        }

        private static Texture2D Sc11YardTex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11Yard/" + n + ".png");

        // ---------------------------------------------------------------- shared helpers (old and new yard)

        private static void Sc11MakeWorkers(Transform crowd, Vector3[] starts, float yaw, out Sc07Human[] workers, out Transform[] startMarks)
        {
            // Reuse the Scenario 7 prefab: saving it again gives the Head renderer a new id and the Scenario 7 crowd loses its Head material (magenta faces).
            var prefabLo = AssetDatabase.LoadAssetAtPath<GameObject>(Sc07GenDir + "/Worker_Lo.prefab") ?? Sc07MakeHuman("Lo");
            var walkClips = Sc07Clips("Step_Walk_", 6);
            var colors = new[] { new Color(0.1f, 0.45f, 0.85f), new Color(1f, 0.74f, 0.04f), new Color(0.1f, 0.45f, 0.85f), new Color(1f, 0.74f, 0.04f), new Color(0.1f, 0.45f, 0.85f) };
            workers = new Sc07Human[starts.Length];
            startMarks = new Transform[starts.Length];
            for (var i = 0; i < starts.Length; i++)
            {
                var person = Sc07People[(i % (Sc07People.Length - 1)) + 1];
                person = new Sc07Person { name = "Y" + i, skin = person.skin, height = person.height, girth = person.girth, helmet = colors[i % colors.Length] };
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabLo);
                inst.name = "Worker_" + i;
                inst.transform.SetParent(crowd, false);
                inst.transform.localScale = Vector3.one * (person.height / 3.64f);
                inst.transform.position = starts[i];
                inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Sc07Dress(inst, person);
                // hi-vis vest: the uniform takes the neon yellow-green / orange of the picture
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.name == "Uniform")
                        smr.sharedMaterial = Mat("Sc11HiVis" + (i % 2), i % 2 == 0 ? new Color(0.62f, 0.70f, 0.14f) : new Color(0.80f, 0.40f, 0.12f), 0.2f, 0f, Sc07WorkerTex("Worker_Uniform"));
                var human = inst.AddComponent<Sc07Human>();
                var hso = new SerializedObject(human);
                FillArray(hso.FindProperty("stepWalk"), walkClips.Length, (pp, k) => pp.objectReferenceValue = walkClips[k]);
                hso.FindProperty("stepVolume").floatValue = 0.35f;
                hso.ApplyModifiedPropertiesWithoutUndo();
                human.Evaluate(0f, 0f);
                var shadowQuad = QuadObj("ContactShadow", inst.transform, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector2(0.7f, 0.7f) / (person.height / 3.64f),
                    MatAlpha("Sc07WorkerShadow", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/ContactShadow.png"), new Color(0f, 0f, 0f, 0.5f), false));
                shadowQuad.transform.localPosition = new Vector3(0f, 0.012f / (person.height / 3.64f), 0f);
                workers[i] = human;
                startMarks[i] = Marker("WorkerStart_" + i, crowd, starts[i], yaw);
            }
        }

        private static void Sc11MakeRadio(Transform room, out Transform radioT, out Transform screenT)
        {
            var radioGo = Sc11Spawn("Wireless.glb", room);
            radioT = null;
            screenT = null;
            if (radioGo == null)
                return;
            radioGo.name = "Walkie";
            FitHeight(radioGo, 0.21f);
            radioT = new GameObject("RadioRig").transform;
            radioT.SetParent(room, false);
            radioGo.transform.SetParent(radioT, false);
            var rb = BoundsOfTree(radioGo.transform);
            radioGo.transform.position += radioT.position - rb.center; // model centred on the rig
            screenT = new GameObject("RadioScreen").transform;
            screenT.SetParent(radioT, false);
            screenT.localPosition = new Vector3(-0.0218f, 0.0105f, 0f);
            screenT.localRotation = Quaternion.identity;
            radioT.gameObject.SetActive(false);
        }

        /// <summary>The host component with its sounds, the guide arrow, the gate card and the stand markers (positions are floor points, yaw 180 = facing -Z).</summary>
        private static Sc11Refs Sc11Finish(Transform room, PresentableArt art, Sc11Parts p, Vector3 spawn, Vector3 yardStand, Vector3 gateStand, Vector3 outsideStand, Vector3 exitTarget)
        {
            var refs = new Sc11Refs();
            var hostGo = new GameObject("Sc11YardHost");
            hostGo.transform.SetParent(room, false);
            refs.yard = hostGo.AddComponent<Sc11Yard>();
            var yso = new SerializedObject(refs.yard);
            yso.FindProperty("art").objectReferenceValue = art;
            yso.FindProperty("leakGas").objectReferenceValue = p.gas;
            yso.FindProperty("leakGasPurple").objectReferenceValue = p.purple;
            FillArray(yso.FindProperty("frost"), p.frost.Length, (pp, k) => pp.objectReferenceValue = p.frost[k]);
            FillArray(yso.FindProperty("beacons"), p.beacons.Length, (pp, k) => pp.objectReferenceValue = p.beacons[k]);
            yso.FindProperty("exitSign").objectReferenceValue = p.exitSign;
            FillArray(yso.FindProperty("workers"), p.workers.Length, (pp, k) => pp.objectReferenceValue = p.workers[k]);
            FillArray(yso.FindProperty("workerStarts"), p.starts.Length, (pp, k) => pp.objectReferenceValue = p.starts[k]);
            FillArray(yso.FindProperty("exitPath"), p.path.Length, (pp, k) => pp.objectReferenceValue = p.path[k]);
            yso.FindProperty("radio").objectReferenceValue = p.radio;
            yso.FindProperty("radioScreen").objectReferenceValue = p.radioScreen;
            yso.FindProperty("outsideRoot").objectReferenceValue = p.outside;
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc11/" + n + ".wav");
            yso.FindProperty("windLoop").objectReferenceValue = Clip("Wind_Loop");
            yso.FindProperty("murmurLoop").objectReferenceValue = Clip("Crowd_Murmur_Loop");
            for (var lang = 0; lang < 2; lang++)
            {
                var pre = lang == 0 ? "bn_" : "en_";
                var clips = new System.Collections.Generic.List<AudioClip>();
                for (var ci = 0; ci < 6; ci++)
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc11Crowd/" + pre + ci + ".mp3"));
                FillArray(yso.FindProperty(lang == 0 ? "crowdBn" : "crowdEn"), clips.Count, (pp, k) => pp.objectReferenceValue = clips[k]);
            }

            yso.FindProperty("hissLoop").objectReferenceValue = Clip("Gas_Hiss_Loop");
            yso.FindProperty("sirenLoop").objectReferenceValue = Clip("Siren_Loop");
            yso.FindProperty("staticLoop").objectReferenceValue = Clip("Radio_Static_Loop");
            yso.FindProperty("squelch").objectReferenceValue = Clip("Radio_Squelch");
            yso.FindProperty("callTone").objectReferenceValue = Clip("Radio_Call");
            yso.FindProperty("roger").objectReferenceValue = Clip("Radio_Roger");
            yso.FindProperty("click").objectReferenceValue = Clip("Radio_Click");
            yso.FindProperty("hissAt").objectReferenceValue = p.valveAt;
            yso.FindProperty("sirenAt").objectReferenceValue = p.sirenAt;
            yso.ApplyModifiedPropertiesWithoutUndo();
            p.outside.SetActive(false);

            var gateGo = new GameObject("Sc11GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc11Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc11Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc11Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var guideGo = new GameObject("Sc11Guide");
            guideGo.transform.SetParent(room, false);
            guideGo.transform.localScale = Vector3.one * 0.55f;
            refs.guide = guideGo.AddComponent<Sc09Guide>();
            SetRef(refs.guide, "art", art);

            refs.exitTarget = Marker("ExitTarget", room, exitTarget, 0f);
            refs.spawn = Marker("Spawn_SC11", room, spawn, 180f);
            refs.yardStand = Marker("Stand_SC11_Yard", room, yardStand, 180f);
            refs.gateStand = Marker("Stand_SC11_Gate", room, gateStand, 180f);
            refs.outsideStand = Marker("Stand_SC11_Outside", room, outsideStand, 0f);
            return refs;
        }

        // ---------------------------------------------------------------- the new yard

        private static Sc11Refs BuildSc11YardRoom(Transform env, PresentableArt art)
        {
            const float ox = Sc11Ox;
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            Sc11FixTextures();
            var solidLayer = SolidLayer;

            // hazy overcast daylight, soft shadows, light fog into the plant
            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.74f, 0.78f, 0.84f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.64f, 0.66f, 0.68f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.42f, 0.40f, 0.38f);
            mso.FindProperty("fogColor").colorValue = new Color(0.76f, 0.78f, 0.77f);
            mso.FindProperty("fogStart").floatValue = 10f;
            mso.FindProperty("fogEnd").floatValue = 120f;
            mso.FindProperty("sunIntensity").floatValue = 0.85f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.7f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var concreteTex = Sc11YardTex("Yard_Concrete");
            var whiteTex = Sc11YardTex("Yard_WallWhite"); // old whitewashed plaster: damp streaks, soot near the floor, peeled patches
            var tinTex = Sc11YardTex("Yard_Tin");
            var steel = Mat("Sc11YSteel", new Color(0.52f, 0.54f, 0.56f), 0.45f, 0.7f);
            var darkSteel = Mat("Sc11YDarkSteel", new Color(0.18f, 0.20f, 0.22f), 0.4f, 0.6f);
            var green = Mat("Sc11YGreen", new Color(0.13f, 0.40f, 0.20f), 0.35f, 0.5f);

            // ---------------------------------------------------------------- ground and sky
            var floorMat = Mat("Sc11YFloor", new Color(1f, 1f, 1f), 0.18f, 0f, Sc11YardTex("Yard_Floor"));
            floorMat.SetTextureScale("_BaseMap", new Vector2(18f, 34f));
            var ground = Box("Ground", room, new Vector3(ox, -0.05f, -30f), new Vector3(60f, 0.1f, 100f), floorMat);
            ground.AddComponent<TeleportationArea>();
            if (solidLayer >= 0)
                ground.layer = solidLayer;

            var skyMat = Mat("Sc11YSky", Color.white, 0f, 0f, Sc11YardTex("Yard_Sky"), null, true);
            skyMat.SetFloat("_Cull", 0f);
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "SkyDome";
            sky.transform.SetParent(room, false);
            sky.transform.position = new Vector3(ox, -5f, -20f);
            sky.transform.localScale = new Vector3(-220f, 220f, 220f); // seen from inside
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            sky.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(sky.GetComponent<Collider>());

            // ---------------------------------------------------------------- concrete wall, tin roof, pillar
            var wallMat = Mat("Sc11YWall", Color.white, 0.1f, 0f, whiteTex);
            wallMat.SetTextureScale("_BaseMap", new Vector2(5.2f, 1f));
            Box("YardWall", room, new Vector3(ox - 10.5f, 1.85f, -3.62f), new Vector3(21f, 3.7f, 0.3f), wallMat);
            // the yard is closed on the right: a return wall toward the learner and a tall ribbed hall rising behind the wall (client picture)
            var endMat = Mat("Sc11YWallEnd", Color.white, 0.1f, 0f, whiteTex);
            endMat.SetTextureScale("_BaseMap", new Vector2(2.9f, 1f));
            Box("YardWallEnd", room, new Vector3(ox - 20.9f, 1.85f, 2.2f), new Vector3(0.3f, 3.7f, 11.6f), endMat);
            var hallMat = Mat("Sc11YBackHall", new Color(0.84f, 0.86f, 0.83f), 0.35f, 0.45f, tinTex);
            hallMat.SetTextureScale("_BaseMap", new Vector2(28f, 2.6f));
            Box("BackHall", room, new Vector3(ox - 10.5f, 5.6f, -8.5f), new Vector3(24f, 11.2f, 0.8f), hallMat, false);
            var hallRoof = Box("BackHallRoof", room, new Vector3(ox - 10.5f, 11.5f, -6.6f), new Vector3(24.4f, 0.14f, 4.4f), Mat("Sc11YBackHallRoof", new Color(0.55f, 0.57f, 0.56f), 0.3f, 0.4f, tinTex), false);
            hallRoof.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Box("HallBand", room, new Vector3(ox - 10.5f, 9.2f, -8.05f), new Vector3(24f, 0.9f, 0.1f), Mat("Sc11YHallBand", new Color(0.18f, 0.30f, 0.45f), 0.3f), false);
            var pillarMat = Mat("Sc11YPillar", new Color(0.95f, 0.94f, 0.92f), 0.1f, 0f, whiteTex);
            pillarMat.SetTextureScale("_BaseMap", new Vector2(0.15f, 1f));
            Box("Pillar", room, new Vector3(ox - 0.1f, 1.9f, -3.55f), new Vector3(0.5f, 3.8f, 0.6f), pillarMat);
            var lowMat = Mat("Sc11YLowWall", new Color(0.97f, 0.96f, 0.94f), 0.1f, 0f, whiteTex);
            lowMat.SetTextureScale("_BaseMap", new Vector2(0.7f, 0.55f));
            Box("LowWall", room, new Vector3(ox + 0.35f, 1.1f, -5.0f), new Vector3(0.24f, 2.2f, 2.8f), lowMat);

            var tinMat = Mat("Sc11YTin", new Color(0.95f, 0.92f, 0.88f), 0.45f, 0.5f, tinTex);
            tinMat.SetTextureScale("_BaseMap", new Vector2(12f, 1f));
            var roof = Box("ShedRoof", room, new Vector3(ox - 10.5f, 3.78f, -2.55f), new Vector3(21.4f, 0.07f, 2.3f), tinMat, false);
            roof.transform.rotation = Quaternion.Euler(7f, 0f, 0f);
            var roofEdge = Box("ShedRoofEdge", room, new Vector3(ox - 10.5f, 3.50f, -1.43f), new Vector3(21.4f, 0.16f, 0.07f), darkSteel, false);
            roofEdge.transform.rotation = Quaternion.Euler(7f, 0f, 0f);
            foreach (var px in new[] { ox - 7.5f, ox - 0.3f, ox - 14f, ox - 20.5f })
                Sc11Cyl("RoofPost", room, new Vector3(px, 1.7f, -1.45f), 0.06f, 3.4f, darkSteel);

            // ---------------------------------------------------------------- signs, fire alarm, beacons on the wall
            var signMat = Mat("Sc11YSign", Color.white, 0.35f, 0.1f, Sc11YardTex("Sign_ToxicGas"), null, false, false);
            var sign = QuadObj("Sign_ToxicGas", room, new Vector3(ox - 2.45f, 2.62f, -3.44f), new Vector3(0f, 180f, 0f), new Vector2(2.25f, 1.45f), signMat);
            sign.transform.SetParent(room, true);
            Box("SignBack", room, new Vector3(ox - 2.45f, 2.62f, -3.46f), new Vector3(2.32f, 1.52f, 0.03f), darkSteel, false);
            var sign2 = QuadObj("Sign_ToxicGas_2", room, new Vector3(ox - 12.5f, 2.3f, -3.44f), new Vector3(0f, 180f, 0f), new Vector2(1.5f, 0.97f), signMat);
            sign2.transform.SetParent(room, true);
            var alarmRed = Mat("Sc11YAlarmRed", new Color(0.78f, 0.06f, 0.05f), 0.5f, 0f, null, new Color(0.12f, 0.01f, 0.01f));
            Box("FireAlarm", room, new Vector3(ox - 6.3f, 1.4f, -3.42f), new Vector3(0.2f, 0.2f, 0.08f), alarmRed, false);
            Box("FireAlarmPlate", room, new Vector3(ox - 6.3f, 1.4f, -3.465f), new Vector3(0.26f, 0.26f, 0.03f), Mat("Sc11YAlarmPlate", new Color(0.88f, 0.88f, 0.86f), 0.3f), false);
            Box("FireAlarmGlass", room, new Vector3(ox - 6.3f, 1.4f, -3.37f), new Vector3(0.1f, 0.1f, 0.01f), Mat("Sc11YAlarmGlass", new Color(0.9f, 0.9f, 0.9f), 0.8f), false);

            var lampMat = Mat("Sc11BeaconLamp", new Color(0.5f, 0.05f, 0.04f), 0.7f, 0f, null, new Color(0.3f, 0.02f, 0.01f));
            Sc07Beacon Lamp(string n, Vector3 at)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                l.name = n;
                l.transform.SetParent(room, true);
                l.transform.position = at;
                l.transform.localScale = Vector3.one * 0.26f;
                l.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
                Object.DestroyImmediate(l.GetComponent<Collider>());
                Box(n + "Base", room, at + new Vector3(0f, -0.15f, 0.03f), new Vector3(0.2f, 0.06f, 0.2f), darkSteel, false);
                return Sc07BuildBeacon(room, l.transform, at, art);
            }

            var beacon1 = Lamp("BeaconLampA", new Vector3(ox - 6.3f, 2.3f, -3.35f));
            var beacon2 = Lamp("BeaconLampB", new Vector3(ox - 1.05f, 3.15f, -3.35f));

            // ---------------------------------------------------------------- the cylinders on their rack, the leak
            var rackRoot = new GameObject("CylinderRack").transform;
            rackRoot.SetParent(room, false);
            Sc11BuildRack(rackRoot, ox, art, out var valveAt, out var frostRenderers, out var gasGo, out var purpleGo, true);

            // ---------------------------------------------------------------- green chain-link fence along the street (left)
            var fenceMat = Mat("Sc11FenceMat", Color.white, 0.75f, 0.55f, Sc06Tex("Label_Sc11Fence"), null, false, true);
            const float fx = 3.9f;
            const float fh = 2.3f;
            const float z0 = 3f, z1 = -16f;
            var len = z0 - z1;
            var cz = (z0 + z1) * 0.5f;
            for (var side = 0; side < 2; side++)
            {
                var q = QuadObj(side == 0 ? "Mesh_F" : "Mesh_B", room, new Vector3(ox + fx + (side == 0 ? -0.01f : 0.01f), fh * 0.5f, cz), new Vector3(0f, side == 0 ? 90f : -90f, 0f),
                    new Vector2(len, fh), fenceMat);
                q.transform.SetParent(room, true);
                var r = q.GetComponent<MeshRenderer>();
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector("_BaseMap_ST", new Vector4(len / 0.6f, fh / 0.6f, 0f, 0f));
                r.SetPropertyBlock(mpb);
            }

            for (var z = z0; z >= z1 - 0.01f; z -= 3.2f)
                Sc11Cyl("FencePost", room, new Vector3(ox + fx, fh * 0.5f + 0.1f, z), 0.05f, fh + 0.2f, green);
            foreach (var y in new[] { 0.06f, fh * 0.5f, fh - 0.02f })
                Sc11Cyl("FenceRail", room, new Vector3(ox + fx, y, cz), 0.025f, len, green, new Vector3(90f, 0f, 0f));
            var wireMat = Mat("Sc11WireMat", new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.85f);
            for (var s = 0; s < 3; s++)
                Sc11Cyl("Barb", room, new Vector3(ox + fx, fh + 0.28f + s * 0.07f, cz), 0.008f, len, wireMat, new Vector3(90f, 0f, 0f));
            // gate frame with a diagonal brace, standing in the fence like the client's picture
            Sc11Cyl("GateBrace", room, new Vector3(ox + fx, fh * 0.5f, 0.8f), 0.03f, 3.3f, green, new Vector3(36f, 0f, 0f));
            Sc11Cyl("GatePostA", room, new Vector3(ox + fx, fh * 0.5f + 0.2f, 2.0f), 0.08f, fh + 0.4f, green);

            // ---------------------------------------------------------------- the industrial area: corrugated sheds, pipe rack, tanks, column, chimney
            Sc11IndustryBackdrop(room, ox, concreteTex, tinTex, steel, darkSteel);

            // ---------------------------------------------------------------- the wind sock above the street (the wind blows to the left of the view)
            var pole = new GameObject("WindsockPole").transform;
            pole.SetParent(room, false);
            pole.position = new Vector3(ox + 0.9f, 0f, -9.5f);
            Sc11Cyl("Pole", pole, pole.position + Vector3.up * 2.7f, 0.045f, 5.4f, steel);
            var sock = new GameObject("Windsock");
            sock.transform.SetParent(pole, false);
            sock.transform.position = pole.position + Vector3.up * 5.25f;
            sock.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            var meshPath = GenDir + "/Mesh_Sc11Windsock.asset";
            var sockMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (sockMesh == null)
            {
                sockMesh = new Mesh();
                AssetDatabase.CreateAsset(sockMesh, meshPath);
            }

            Sc11FillSock(sockMesh);
            sock.AddComponent<MeshFilter>().sharedMesh = sockMesh;
            var sockMat = Mat("Sc11YWindsock", Color.white, 0.2f, 0f, Sc11YardTex("Windsock"));
            sockMat.SetFloat("_Cull", 0f);
            var sockR = sock.AddComponent<MeshRenderer>();
            sockR.sharedMaterial = sockMat;
            sockR.shadowCastingMode = ShadowCastingMode.Off;
            sock.AddComponent<Sc11Windsock>();

            // ---------------------------------------------------------------- litter and stains on the slab floor
            var stainMat = MatAlpha("Sc11YStain", art.radialGlow != null ? art.radialGlow.texture : null, new Color(0.10f, 0.09f, 0.08f, 0.45f), false);
            foreach (var st in new[] { new Vector4(0.9f, -1.0f, 1.1f, 0.7f), new Vector4(-1.6f, -2.2f, 0.9f, 0.6f), new Vector4(1.8f, -4.6f, 1.4f, 0.9f), new Vector4(-4.5f, -2.4f, 1.0f, 0.7f) })
            {
                var q = QuadObj("Stain", room, new Vector3(ox + st.x, 0.015f, st.y), new Vector3(90f, 0f, 0f), new Vector2(st.z, st.w), stainMat);
                q.transform.SetParent(room, true);
            }

            var plank = Mat("Sc11YPlank", new Color(0.46f, 0.36f, 0.26f), 0.1f);
            Box("Plank", room, new Vector3(ox + 1.3f, 0.025f, -1.8f), new Vector3(0.9f, 0.05f, 0.12f), plank, false).transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            Box("Plank2", room, new Vector3(ox - 5.2f, 0.025f, -1.6f), new Vector3(0.7f, 0.05f, 0.1f), plank, false).transform.rotation = Quaternion.Euler(0f, -40f, 0f);

            // ---------------------------------------------------------------- the people (light clothes, helmets) in the street, the way out
            var crowd = new GameObject("Crowd").transform;
            crowd.SetParent(room, false);
            var starts = new[]
            {
                new Vector3(ox + 1.2f, 0f, -7.5f), new Vector3(ox + 2.2f, 0f, -8.4f), new Vector3(ox + 1.7f, 0f, -9.6f),
                new Vector3(ox + 2.6f, 0f, -6.8f), new Vector3(ox + 1.0f, 0f, -11.0f)
            };
            Sc11MakeWorkers(crowd, starts, 20f, out var workers, out var startMarks);
            var pathPts = new[] { new Vector3(ox + 1.9f, 0f, -13f), new Vector3(ox + 1.7f, 0f, -24f) };
            var path = new Transform[pathPts.Length];
            for (var i = 0; i < pathPts.Length; i++)
                path[i] = Marker("ExitPath_" + i, crowd, pathPts[i], 180f);

            var outside = new GameObject("Outside");
            outside.transform.SetParent(room, false);
            Sc11MakeRadio(room, out var radioT, out var screenT);

            var parts = new Sc11Parts
            {
                gas = gasGo, purple = purpleGo, frost = frostRenderers, beacons = new[] { beacon1, beacon2 }, exitSign = null, outside = outside,
                workers = workers, starts = startMarks, path = path, radio = radioT, radioScreen = screenT, valveAt = valveAt, sirenAt = beacon2.transform
            };
            // the learner stands at the rack in the reference angle; after the alarm the learner steps back, away from the plume
            return Sc11Finish(room, art, parts, new Vector3(ox - 0.2f, 0f, -0.2f), new Vector3(ox - 0.85f, 0f, -1.25f), new Vector3(ox + 0.3f, 0f, 1.9f),
                new Vector3(ox, 0f, 2f), new Vector3(ox + 1.9f, 0f, -14f));
        }

        /// <summary>Everything behind the fence and down the street: ribbed metal sheds with roller doors, a heavy pipe rack on steel frames,
        /// white tanks, a distillation column, a sphere tank, a chimney, oil drums by the wall. Plain boxes and cylinders (Quest friendly).</summary>
        private static void Sc11IndustryBackdrop(Transform room, float ox, Texture2D concreteTex, Texture2D tinTex, Material steel, Material darkSteel)
        {
            var root = new GameObject("Industry").transform;
            root.SetParent(room, false);

            // ribbed metal sheds (the ribs of Yard_Tin run along the texture's u, so they stand vertical on these side walls)
            Material Ribbed(string n, Color c, float lenRepeat, float hRepeat)
            {
                var m = Mat(n, c, 0.35f, 0.45f, tinTex);
                m.SetTextureScale("_BaseMap", new Vector2(lenRepeat, hRepeat));
                return m;
            }

            var shedMat = Ribbed("Sc11YShed", new Color(0.86f, 0.88f, 0.84f), 26f, 2.4f);
            Box("NearShed", root, new Vector3(ox + 6.7f, 2.9f, -11f), new Vector3(0.5f, 5.8f, 31f), shedMat, false);
            var roofMat = Ribbed("Sc11YShedRoof", new Color(0.62f, 0.64f, 0.62f), 26f, 1f);
            var roof = Box("NearShedRoof", root, new Vector3(ox + 5.7f, 5.95f, -11f), new Vector3(2.6f, 0.12f, 31.4f), roofMat, false);
            roof.transform.rotation = Quaternion.Euler(0f, 0f, 6f);
            Box("Gutter", root, new Vector3(ox + 4.45f, 5.75f, -11f), new Vector3(0.16f, 0.14f, 31.4f), darkSteel, false);
            var doorMat = Mat("Sc11YRollerDoor", new Color(0.30f, 0.34f, 0.33f), 0.3f, 0.4f);
            var winMat = Mat("Sc11YShedWin", new Color(0.20f, 0.26f, 0.30f), 0.85f, 0.2f);
            var yellow = Mat("Sc11YYellow", new Color(0.92f, 0.72f, 0.08f), 0.3f);
            foreach (var z in new[] { -3.5f, -15f, -24f })
            {
                Box("RollerDoor", root, new Vector3(ox + 6.42f, 1.7f, z), new Vector3(0.1f, 3.4f, 3.8f), doorMat, false);
                Box("DoorFrame", root, new Vector3(ox + 6.40f, 3.5f, z), new Vector3(0.14f, 0.2f, 4.2f), yellow, false);
                Sc11Cyl("Bollard", root, new Vector3(ox + 5.7f, 0.5f, z - 2.4f), 0.09f, 1.0f, yellow);
                Sc11Cyl("Bollard", root, new Vector3(ox + 5.7f, 0.5f, z + 2.4f), 0.09f, 1.0f, yellow);
            }

            Box("ShedWindows", root, new Vector3(ox + 6.44f, 4.6f, -11f), new Vector3(0.06f, 0.7f, 28f), winMat, false);
            foreach (var z in new[] { 2.5f, -8f, -19f })
                Sc11Cyl("Downpipe", root, new Vector3(ox + 6.35f, 2.9f, z), 0.07f, 5.8f, steel);

            // far block across the street end and a second tall hall
            var farMat = Ribbed("Sc11YFarShed", new Color(0.80f, 0.82f, 0.80f), 18f, 2f);
            Box("FarShed", root, new Vector3(ox + 16f, 5.5f, -42f), new Vector3(0.6f, 11f, 40f), farMat, false);
            Box("FarShedRoof", root, new Vector3(ox + 15.2f, 11.3f, -42f), new Vector3(2.4f, 0.14f, 40.4f), roofMat, false);
            Box("HallEnd", root, new Vector3(ox + 5f, 5f, -53f), new Vector3(24f, 10f, 0.8f), Ribbed("Sc11YHallEnd", new Color(0.78f, 0.80f, 0.78f), 20f, 2f), false);
            var endWall = Mat("Sc11YEndWall", new Color(0.78f, 0.78f, 0.77f), 0.1f, 0f, concreteTex);
            endWall.SetTextureScale("_BaseMap", new Vector2(10f, 2.4f));
            Box("StreetEnd", root, new Vector3(ox - 14f, 4.2f, -48f), new Vector3(18f, 8.4f, 1f), endWall, false);

            // heavy pipe rack above the street: five pipes on two levels, flanges, steel frames with bracing
            var pipeGrey = Mat("Sc11YPipe", new Color(0.50f, 0.52f, 0.53f), 0.35f, 0.75f);
            var pipeBlue = Mat("Sc11YPipeBlue", new Color(0.46f, 0.52f, 0.58f), 0.35f, 0.6f);
            var pipeRed = Mat("Sc11YPipeRed", new Color(0.66f, 0.16f, 0.12f), 0.35f, 0.4f);
            const float pz0 = -5.0f, pz1 = -50f;
            var plen = pz0 - pz1;
            var pcz = (pz0 + pz1) * 0.5f;
            var levels = new[]
            {
                new Vector4(0.9f, 4.6f, 0.22f, 0), new Vector4(1.5f, 4.6f, 0.16f, 1), new Vector4(2.1f, 4.6f, 0.12f, 2),
                new Vector4(1.1f, 3.9f, 0.14f, 0), new Vector4(1.9f, 3.9f, 0.10f, 1)
            };
            foreach (var v in levels)
            {
                var m = v.w == 0 ? pipeGrey : v.w == 1 ? pipeBlue : pipeRed;
                Sc11Cyl("RackPipe", root, new Vector3(ox + v.x, v.y, pcz), v.z, plen, m, new Vector3(90f, 0f, 0f));
                for (var z = -8f; z > pz1; z -= 6f)
                    Sc11Cyl("Flange", root, new Vector3(ox + v.x, v.y, z), v.z * 1.35f, 0.1f, darkSteel, new Vector3(90f, 0f, 0f));
            }

            for (var z = -7f; z > pz1; z -= 6f)
            {
                foreach (var cx in new[] { 0.5f, 2.5f })
                    Sc11Cyl("RackColumn", root, new Vector3(ox + cx, 2.4f, z), 0.07f, 4.8f, darkSteel);
                Box("RackBeamTop", root, new Vector3(ox + 1.5f, 4.85f, z), new Vector3(2.2f, 0.1f, 0.1f), darkSteel, false);
                Box("RackBeamMid", root, new Vector3(ox + 1.5f, 3.6f, z), new Vector3(2.2f, 0.08f, 0.08f), darkSteel, false);
                var br = Box("RackBrace", root, new Vector3(ox + 1.5f, 4.2f, z), new Vector3(0.05f, 0.05f, 2.5f), darkSteel, false);
                br.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // tanks, column, sphere, chimney in the haze behind the sheds' end
            var tankMat = Mat("Sc11YTank", new Color(0.86f, 0.86f, 0.84f), 0.3f, 0.2f);
            foreach (var t in new[] { new Vector3(10.5f, 3.5f, -34f), new Vector3(10.5f, 3.5f, -40f) })
            {
                Sc11Cyl("Tank", root, new Vector3(ox + t.x, t.y, t.z), 2.2f, 7f, tankMat);
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = "TankDome";
                dome.transform.SetParent(root, true);
                dome.transform.position = new Vector3(ox + t.x, 7f, t.z);
                dome.transform.localScale = new Vector3(4.4f, 1.6f, 4.4f);
                dome.GetComponent<MeshRenderer>().sharedMaterial = tankMat;
                Object.DestroyImmediate(dome.GetComponent<Collider>());
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SphereTank";
            sphere.transform.SetParent(root, true);
            sphere.transform.position = new Vector3(ox - 2f, 6.2f, -40f);
            sphere.transform.localScale = Vector3.one * 6f;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = tankMat;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            foreach (var ang in new[] { 0f, 120f, 240f })
            {
                var o = Quaternion.Euler(0f, ang, 0f) * new Vector3(2.2f, 0f, 0f);
                Sc11Cyl("SphereLeg", root, new Vector3(ox - 2f + o.x, 1.7f, -40f + o.z), 0.12f, 3.4f, darkSteel);
            }

            Sc11Cyl("Column", root, new Vector3(ox - 7.5f, 9f, -44f), 1.3f, 18f, Mat("Sc11YColumn", new Color(0.62f, 0.64f, 0.64f), 0.35f, 0.6f));
            for (var y = 4f; y < 17f; y += 3.6f)
                Sc11Cyl("ColumnPlatform", root, new Vector3(ox - 7.5f, y, -44f), 1.9f, 0.08f, darkSteel);
            Sc11Cyl("ColumnPipe", root, new Vector3(ox - 5.9f, 8f, -44f), 0.14f, 16f, pipeGrey);
            Sc11Cyl("Chimney", root, new Vector3(ox + 13f, 12f, -52f), 1.1f, 24f, Mat("Sc11YChimney", new Color(0.50f, 0.46f, 0.42f), 0.15f));
            Sc11Cyl("ChimneyBand", root, new Vector3(ox + 13f, 21f, -52f), 1.13f, 1.4f, Mat("Sc11YChimneyBand", new Color(0.72f, 0.20f, 0.16f), 0.15f));

            // the wall behind the rack: a vertical pipe, a cable tray, oil drums
            Sc11Cyl("WallPipe", root, new Vector3(ox - 0.75f, 1.8f, -3.38f), 0.07f, 3.6f, pipeGrey);
            Box("CableTray", root, new Vector3(ox - 4.3f, 3.3f, -3.40f), new Vector3(6.4f, 0.12f, 0.2f), darkSteel, false);
            var drumBlue = Mat("Sc11YDrumBlue", new Color(0.12f, 0.30f, 0.55f), 0.3f, 0.5f);
            var drumRed = Mat("Sc11YDrumRed", new Color(0.62f, 0.12f, 0.10f), 0.3f, 0.5f);
            for (var i = 0; i < 4; i++)
                Sc11Cyl("Drum", root, new Vector3(ox - 6.9f + (i % 2) * 0.62f, 0.45f, -2.6f + (i / 2) * 0.62f), 0.29f, 0.9f, i % 2 == 0 ? drumBlue : drumRed);

            // yellow walking line along the street edge
            var lineMat = Mat("Sc11YLine", new Color(0.88f, 0.70f, 0.08f), 0.2f);
            Box("WalkLine", root, new Vector3(ox + 3.3f, 0.012f, -22f), new Vector3(0.12f, 0.02f, 50f), lineMat, false);
            Box("WalkLine2", root, new Vector3(ox + 0.6f, 0.012f, -22f), new Vector3(0.12f, 0.02f, 50f), lineMat, false);
        }

        /// <summary>Open cone-shaped sock along +X (wide mouth at the pole, narrow end), uv.y along the length for the stripes.</summary>
        private static void Sc11FillSock(Mesh m)
        {
            const int seg = 20;
            const float length = 1.5f;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new System.Collections.Generic.List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var a = i / (float)seg * Mathf.PI * 2f;
                var c = Mathf.Cos(a);
                var s = Mathf.Sin(a);
                v[i] = new Vector3(0f, c * 0.24f, s * 0.24f);
                v[seg + 1 + i] = new Vector3(length, c * 0.10f, s * 0.10f);
                uv[i] = new Vector2(i / (float)seg, 0f);
                uv[seg + 1 + i] = new Vector2(i / (float)seg, 1f);
            }

            for (var i = 0; i < seg; i++)
            {
                tri.AddRange(new[] { i, i + 1, seg + 1 + i, i + 1, seg + 2 + i, seg + 1 + i });
            }

            m.Clear();
            m.vertices = v;
            m.uv = uv;
            m.triangles = tri.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
        }
    }
}
#endif

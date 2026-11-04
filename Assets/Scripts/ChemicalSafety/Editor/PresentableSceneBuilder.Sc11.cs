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
    /// Scenario 11: gas cylinder leak in an outdoor yard, modelled on the Scene 11 workflow. The user faces -Z (viewer's left = +X):
    /// the chain-link fence with barbed wire runs along z = -4, the twelve chained cylinders stand on a steel rack in front of it
    /// (the leaking one at the right end of the rack's left block), the rusty bilingual No-Smoking sign is on the far left of the
    /// fence, the procedure poster hangs right of the rack, the exit gate and the brick gas-detector hut are on the right. Storm sky
    /// dome, gravel and mud ground, a meadow behind the fence for the radio call. Env origin x = 400.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc11Refs
        {
            public Transform spawn;
            public Transform yardStand;
            public Transform gateStand;
            public Transform outsideStand;
            public Transform exitTarget;
            public Sc11Yard yard;
            public Sc09Guide guide;
            public Sc06Gate gate;
        }

        private const float Sc11Ox = 400f;
        private const float Sc11FenceZ = -4.0f;
        private const float Sc11GateX = -4.5f; // offset from the origin

        private static void Sc11FixTextures()
        {
            foreach (var n in new[] { "Label_Sc11Ground", "Label_Sc11Meadow", "Label_Sc11Brick", "Label_Sc11Fence", "Label_Sc11Sky", "Label_Sc11Bark", "Label_Sc11Frond", "Label_Sc11LeafCluster", "Label_Sc11Tuft" })
            {
                var path = Sc06TexDir + "/" + n + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var wrap = n == "Label_Sc11Sky" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                var alpha = n == "Label_Sc11Fence" || n == "Label_Sc11Frond" || n == "Label_Sc11LeafCluster" || n == "Label_Sc11Tuft";
                if (ti.wrapMode != wrap || ti.alphaIsTransparency != alpha || ti.anisoLevel < 8 || ti.maxTextureSize < 2048 || (alpha && !ti.mipMapsPreserveCoverage))
                {
                    ti.wrapMode = wrap;
                    ti.alphaIsTransparency = alpha;
                    ti.mipMapsPreserveCoverage = alpha;
                    ti.alphaTestReferenceValue = 0.5f;
                    ti.anisoLevel = 8;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
            }
        }

        private static GameObject Sc11Spawn(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc11/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc11 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        private static GameObject Sc11Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            if (euler.HasValue)
                go.transform.rotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Sc11Refs BuildSc11Room(Transform env, PresentableArt art)
        {
            const float ox = Sc11Ox;
            var refs = new Sc11Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            Sc11FixTextures();
            var solidLayer = SolidLayer;

            // overcast storm light: strong sky ambient, weak sun, haze that blends into the sky dome
            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.62f, 0.67f, 0.74f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.52f, 0.55f, 0.58f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.30f, 0.27f, 0.24f);
            mso.FindProperty("fogColor").colorValue = new Color(0.50f, 0.54f, 0.57f);
            mso.FindProperty("fogStart").floatValue = 16f;
            mso.FindProperty("fogEnd").floatValue = 260f;
            mso.FindProperty("sunIntensity").floatValue = 0.55f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.55f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- ground, road, sky
            var groundMat = Mat("Sc11GroundMat", new Color(1.1f, 1.05f, 1f), 0.12f, 0f, Sc06Tex("Label_Sc11Ground"));
            groundMat.SetTextureScale("_BaseMap", new Vector2(14f, 8f));
            var ground = Box("Ground", room, new Vector3(ox, -0.05f, 0f), new Vector3(60f, 0.1f, 28f), groundMat);
            ground.AddComponent<TeleportationArea>();
            if (solidLayer >= 0)
                ground.layer = solidLayer;
            var farMat = Mat("Sc11FarGroundMat", new Color(0.85f, 0.8f, 0.72f), 0.1f, 0f, Sc06Tex("Label_Sc11Ground"));
            farMat.SetTextureScale("_BaseMap", new Vector2(120f, 120f));
            Box("FarGround", room, new Vector3(ox, -0.07f, -60f), new Vector3(300f, 0.1f, 120f), farMat, true).layer = Mathf.Max(0, solidLayer);
            Box("FarGroundFront", room, new Vector3(ox, -0.07f, 60f), new Vector3(300f, 0.1f, 80f), farMat, false);

            var skyMat = Mat("Sc11SkyMat", Color.white, 0f, 0f, Sc06Tex("Label_Sc11Sky"), null, true);
            skyMat.SetFloat("_Cull", 0f);
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "SkyDome";
            sky.transform.SetParent(room, false);
            sky.transform.position = new Vector3(ox, -5f, 0f);
            sky.transform.localScale = new Vector3(-200f, 200f, 200f); // seen from inside
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            sky.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(sky.GetComponent<Collider>());

            // dirt road from the gate to the horizon, darker and a little wet
            var roadMat = Mat("Sc11RoadMat", new Color(0.55f, 0.50f, 0.45f), 0.3f, 0f, Sc06Tex("Label_Sc11Ground"));
            roadMat.SetTextureScale("_BaseMap", new Vector2(2f, 20f));
            Box("Road", room, new Vector3(ox + Sc11GateX, 0.012f, -22f), new Vector3(3.6f, 0.02f, 40f), roadMat, false);

            // ---------------------------------------------------------------- fence along z = FenceZ (chain-link, posts, barbed wire)
            var fenceMat = Mat("Sc11FenceMat", Color.white, 0.75f, 0.55f, Sc06Tex("Label_Sc11Fence"), null, false, true);
            var postMat = Mat("Sc11PostMat", new Color(0.66f, 0.68f, 0.70f), 0.6f, 0.85f);
            var wireMat = Mat("Sc11WireMat", new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.85f);
            const float fh = 2.5f;
            void FenceRun(float xa, float xb)
            {
                var len = xb - xa;
                var cx = (xa + xb) * 0.5f;
                for (var side = 0; side < 2; side++)
                {
                    var q = QuadObj(side == 0 ? "Mesh_F" : "Mesh_B", room, new Vector3(cx, fh * 0.5f, Sc11FenceZ + (side == 0 ? 0.01f : -0.01f)),
                        new Vector3(0f, side == 0 ? 0f : 180f, 0f), new Vector2(len, fh), fenceMat);
                    q.transform.SetParent(room, true);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    var r = q.GetComponent<MeshRenderer>();
                    r.sharedMaterial = fenceMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetVector("_BaseMap_ST", new Vector4(len / 0.6f, fh / 0.6f, 0f, 0f));
                    r.SetPropertyBlock(mpb);
                }

                var posts = Mathf.CeilToInt(len / 3f);
                for (var i = 0; i <= posts; i++)
                {
                    var x = xa + len * i / posts;
                    Sc11Cyl("Post", room, new Vector3(x, fh * 0.5f + 0.1f, Sc11FenceZ), 0.045f, fh + 0.2f, postMat);
                    // slanted arm that carries the barbed wire
                    Sc11Cyl("Arm", room, new Vector3(x, fh + 0.2f, Sc11FenceZ + 0.1f), 0.025f, 0.45f, postMat, new Vector3(-40f, 0f, 0f));
                }

                foreach (var y in new[] { 0.05f, fh * 0.5f, fh - 0.03f })
                    Sc11Cyl("Rail", room, new Vector3(cx, y, Sc11FenceZ), 0.022f, len, postMat, new Vector3(0f, 0f, 90f));
                foreach (var dz in new[] { -0.2f, 0.1f })
                    for (var s = 0; s < 3; s++)
                        Sc11Cyl("Barb", room, new Vector3(cx, fh + 0.28f + s * 0.07f, Sc11FenceZ + dz), 0.008f, len, wireMat, new Vector3(0f, 0f, 90f));
            }

            const float gateHalf = 1.25f;
            FenceRun(ox - 14f, ox + Sc11GateX - gateHalf);
            FenceRun(ox + Sc11GateX + gateHalf, ox + 14f);
            // the gate: two leaves swung open to the sides, a lintel, and the EXIT sign over it
            var gateMat = Mat("Sc11GateMat", new Color(0.44f, 0.46f, 0.47f), 0.4f, 0.7f);
            foreach (var s in new[] { -1f, 1f })
            {
                var px = ox + Sc11GateX + s * gateHalf;
                Sc11Cyl("GatePost", room, new Vector3(px, 1.45f, Sc11FenceZ), 0.075f, 2.9f, gateMat);
                var leaf = QuadObj("GateLeaf", room, new Vector3(px + s * 0.55f, 1.2f, Sc11FenceZ - 0.9f), new Vector3(0f, s * 90f, 0f), new Vector2(1.1f, 2.3f), fenceMat);
                leaf.transform.SetParent(room, true);
                Object.DestroyImmediate(leaf.GetComponent<Collider>());
            }

            Box("GateLintel", room, new Vector3(ox + Sc11GateX, 2.8f, Sc11FenceZ), new Vector3(gateHalf * 2f + 0.15f, 0.12f, 0.12f), gateMat, false);

            // EXIT sign (green, white running man arrow) above the gate, shown after the SDS
            var exitGo = new GameObject("ExitSign");
            exitGo.transform.SetParent(room, false);
            exitGo.transform.position = new Vector3(ox + Sc11GateX, 3.15f, Sc11FenceZ + 0.15f);
            Box("Body", exitGo.transform, exitGo.transform.position, new Vector3(0.95f, 0.34f, 0.08f),
                Mat("Sc11ExitGreen", new Color(0.04f, 0.45f, 0.18f), 0.4f, 0f, null, new Color(0.0f, 0.55f, 0.15f)), false);
            var exitText = new GameObject("Text").AddComponent<TextMesh>();
            exitText.transform.SetParent(exitGo.transform, false);
            exitText.transform.position = exitGo.transform.position + new Vector3(0f, 0f, 0.05f);
            exitText.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            exitText.text = "EXIT >";
            exitText.fontSize = 60;
            exitText.characterSize = 0.045f;
            exitText.anchor = TextAnchor.MiddleCenter;
            exitText.alignment = TextAlignment.Center;
            exitText.color = Color.white;
            exitText.fontStyle = FontStyle.Bold;
            exitGo.SetActive(false);

            // ---------------------------------------------------------------- the signs on the fence
            var signTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11/Sign_NoSmoking_Cylinders.png");
            var signMat = Mat("Sc11SignMat", new Color(1.25f, 1.25f, 1.25f), 0.4f, 0.25f, signTex, null, false, true);
            for (var side = 0; side < 2; side++)
            {
                var z = Sc11FenceZ + (side == 0 ? 0.12f : -0.12f);
                var sq = QuadObj("Sign_NoSmokingCylinders", room, new Vector3(ox + 5.3f, 1.85f, z), new Vector3(0f, side == 0 ? 0f : 180f, 0f), new Vector2(3.3f, 1.87f), signMat);
                sq.transform.SetParent(room, true);
                Object.DestroyImmediate(sq.GetComponent<Collider>());
            }

            var posterTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11/Poster_GasLeak.png");
            var posterMat = Mat("Sc11PosterMat", Color.white, 0.4f, 0f, posterTex);
            var pq = QuadObj("Poster_GasLeak", room, new Vector3(ox - 2.2f, 1.55f, Sc11FenceZ + 0.05f), new Vector3(0f, 180f, 0f), new Vector2(1.14f, 0.76f), posterMat);
            pq.transform.SetParent(room, true);
            Object.DestroyImmediate(pq.GetComponent<Collider>());
            Box("PosterBack", room, new Vector3(ox - 2.2f, 1.55f, Sc11FenceZ + 0.02f), new Vector3(1.2f, 0.82f, 0.015f), Mat("Sc11PosterFrame", new Color(0.1f, 0.15f, 0.3f), 0.4f, 0.4f), false);

            // ---------------------------------------------------------------- the brick gas-detector hut on the right
            var brickMat = Mat("Sc11BrickMat", new Color(1f, 0.98f, 0.95f), 0.1f, 0f, Sc06Tex("Label_Sc11Brick"));
            brickMat.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
            var hutX = ox - 8.2f;
            Box("Hut", room, new Vector3(hutX, 1.6f, -1.2f), new Vector3(3.0f, 3.2f, 3.4f), brickMat);
            Box("HutRoof", room, new Vector3(hutX, 3.3f, -1.2f), new Vector3(3.3f, 0.22f, 3.7f), Mat("Sc11HutRoof", new Color(0.30f, 0.31f, 0.32f), 0.3f, 0.2f), false);
            Box("HutDoor", room, new Vector3(hutX + 1.51f, 1.1f, -0.5f), new Vector3(0.05f, 2.2f, 1.1f), Mat("Sc11HutDoor", new Color(0.34f, 0.37f, 0.38f), 0.35f, 0.6f), false);
            // the green gas-detector panel and the red alarm unit, on the hut wall that faces the yard (+X face)
            var panelX = hutX + 1.53f;
            Box("DetectorBox", room, new Vector3(panelX, 1.75f, 0.75f), new Vector3(0.06f, 0.48f, 0.34f), Mat("Sc11DetectorBox", new Color(0.18f, 0.20f, 0.21f), 0.4f, 0.5f), false);
            Box("DetectorLed", room, new Vector3(panelX + 0.04f, 1.8f, 0.75f), new Vector3(0.02f, 0.12f, 0.2f), Mat("Sc11DetectorLed", new Color(0.1f, 0.9f, 0.25f), 0.3f, 0f, null, new Color(0.1f, 1.2f, 0.3f)), false);
            var nsTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11/NoSmoking_Pictogram.png");
            var ns = QuadObj("NoSmoking", room, new Vector3(panelX + 0.02f, 2.55f, 0.1f), new Vector3(0f, 90f, 0f), new Vector2(0.5f, 0.5f), Mat("Sc11NoSmokingMat", Color.white, 0.4f, 0f, nsTex));
            ns.transform.SetParent(room, true);
            Object.DestroyImmediate(ns.GetComponent<Collider>());

            // ---------------------------------------------------------------- alarm: a pole beacon (FIRE) and a red unit on the hut
            var lampMat = Mat("Sc11BeaconLamp", new Color(0.5f, 0.05f, 0.04f), 0.7f, 0f, null, new Color(0.3f, 0.02f, 0.01f));
            var polePos = new Vector3(ox + Sc11GateX + 1.9f, 0f, Sc11FenceZ + 1.1f);
            Sc11Cyl("AlarmPole", room, polePos + Vector3.up * 1.75f, 0.04f, 3.5f, postMat);
            Box("FireLabel", room, polePos + new Vector3(0f, 2.78f, -0.0f), new Vector3(0.34f, 0.14f, 0.04f), Mat("Sc11FireLabel", new Color(0.82f, 0.08f, 0.06f), 0.4f), false);
            var pl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pl.name = "PoleLamp";
            pl.transform.SetParent(room, true);
            pl.transform.position = polePos + new Vector3(0f, 3.05f, 0f);
            pl.transform.localScale = Vector3.one * 0.34f;
            pl.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
            Object.DestroyImmediate(pl.GetComponent<Collider>());
            var beacon1 = Sc07BuildBeacon(room, pl.transform, pl.transform.position, art);
            var hl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hl.name = "HutLamp";
            hl.transform.SetParent(room, true);
            hl.transform.position = new Vector3(panelX + 0.16f, 2.05f, 0.75f);
            hl.transform.localScale = Vector3.one * 0.2f;
            hl.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
            Object.DestroyImmediate(hl.GetComponent<Collider>());
            var beacon2 = Sc07BuildBeacon(room, hl.transform, hl.transform.position, art);

            // ---------------------------------------------------------------- the cylinders on their rack, the leak
            var rackRoot = new GameObject("CylinderRack").transform;
            rackRoot.SetParent(room, false);
            Sc11BuildRack(rackRoot, ox, art, out var valveAt, out var frostRenderers, out var gasGo, out var purpleGo);

            // ---------------------------------------------------------------- the people, the path out
            var crowd = new GameObject("Crowd").transform;
            crowd.SetParent(room, false);
            // Reuse the Scenario 7 prefab: saving it again gives the Head renderer a new id and the Scenario 7 crowd loses its Head material (magenta faces).
            var prefabLo = AssetDatabase.LoadAssetAtPath<GameObject>(Sc07GenDir + "/Worker_Lo.prefab") ?? Sc07MakeHuman("Lo");
            var walkClips = Sc07Clips("Step_Walk_", 6);
            var colors = new[] { new Color(0.1f, 0.45f, 0.85f), new Color(1f, 0.74f, 0.04f), new Color(0.1f, 0.45f, 0.85f), new Color(1f, 0.74f, 0.04f), new Color(0.1f, 0.45f, 0.85f) };
            var starts = new[] { new Vector3(ox - 0.2f, 0f, -1.4f), new Vector3(ox - 0.9f, 0f, -2.2f), new Vector3(ox - 1.6f, 0f, -1.6f), new Vector3(ox - 2.3f, 0f, -2.4f), new Vector3(ox - 2.9f, 0f, -1.7f) };
            var workers = new Sc07Human[5];
            var startMarks = new Transform[5];
            for (var i = 0; i < 5; i++)
            {
                var person = Sc07People[(i % (Sc07People.Length - 1)) + 1];
                person = new Sc07Person { name = "Y" + i, skin = person.skin, height = person.height, girth = person.girth, helmet = colors[i] };
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabLo);
                inst.name = "Worker_" + i;
                inst.transform.SetParent(crowd, false);
                inst.transform.localScale = Vector3.one * (person.height / 3.64f);
                inst.transform.position = starts[i];
                inst.transform.rotation = Quaternion.Euler(0f, 215f, 0f);
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
                startMarks[i] = Marker("WorkerStart_" + i, crowd, starts[i], 215f);
            }

            var pathPts = new[]
            {
                new Vector3(ox + Sc11GateX + 0.2f, 0f, -2.4f),
                new Vector3(ox + Sc11GateX, 0f, Sc11FenceZ + 0.6f),
                new Vector3(ox + Sc11GateX - 0.2f, 0f, Sc11FenceZ - 3f),
                new Vector3(ox + Sc11GateX - 0.5f, 0f, -16f),
            };
            var path = new Transform[pathPts.Length];
            for (var i = 0; i < pathPts.Length; i++)
                path[i] = Marker("ExitPath_" + i, crowd, pathPts[i], 180f);

            // ---------------------------------------------------------------- outside: the meadow and the walkie-talkie
            var outside = new GameObject("Outside").transform;
            outside.SetParent(room, false);
            var meadowMat = Mat("Sc11MeadowMat", new Color(1.05f, 1.05f, 1f), 0.1f, 0f, Sc06Tex("Label_Sc11Meadow"));
            meadowMat.SetTextureScale("_BaseMap", new Vector2(70f, 20f));
            var meadow = Box("Meadow", outside, new Vector3(ox, -0.02f, -42f), new Vector3(150f, 0.1f, 66f), meadowMat, true);
            if (solidLayer >= 0)
                meadow.layer = solidLayer;
            Sc11Tufts(outside, 11, 90, ox - 14f, ox + 14f, -34f, -10f, 0.5f, 1.1f);
            Sc11Scenery(room, ox);
            Sc11Tufts(room, 12, 46, ox - 13f, ox + 12f, -3.85f, -3.0f, 0.45f, 0.95f);

            var radioGo = Sc11Spawn("Wireless.glb", room);
            Transform radioT = null;
            Transform screenT = null;
            if (radioGo != null)
            {
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

            // ---------------------------------------------------------------- the host, sounds, guide arrow, gate card
            var hostGo = new GameObject("Sc11YardHost");
            hostGo.transform.SetParent(room, false);
            refs.yard = hostGo.AddComponent<Sc11Yard>();
            var yso = new SerializedObject(refs.yard);
            yso.FindProperty("art").objectReferenceValue = art;
            yso.FindProperty("leakGas").objectReferenceValue = gasGo;
            yso.FindProperty("leakGasPurple").objectReferenceValue = purpleGo;
            FillArray(yso.FindProperty("frost"), frostRenderers.Length, (pp, k) => pp.objectReferenceValue = frostRenderers[k]);
            FillArray(yso.FindProperty("beacons"), 2, (pp, k) => pp.objectReferenceValue = k == 0 ? beacon1 : beacon2);
            yso.FindProperty("exitSign").objectReferenceValue = exitGo;
            FillArray(yso.FindProperty("workers"), workers.Length, (pp, k) => pp.objectReferenceValue = workers[k]);
            FillArray(yso.FindProperty("workerStarts"), startMarks.Length, (pp, k) => pp.objectReferenceValue = startMarks[k]);
            FillArray(yso.FindProperty("exitPath"), path.Length, (pp, k) => pp.objectReferenceValue = path[k]);
            yso.FindProperty("radio").objectReferenceValue = radioT;
            yso.FindProperty("radioScreen").objectReferenceValue = screenT;
            yso.FindProperty("outsideRoot").objectReferenceValue = outside.gameObject;
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
            yso.FindProperty("hissAt").objectReferenceValue = valveAt;
            yso.FindProperty("sirenAt").objectReferenceValue = pl.transform;
            yso.ApplyModifiedPropertiesWithoutUndo();
            outside.gameObject.SetActive(false);

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

            refs.exitTarget = Marker("ExitTarget", room, new Vector3(ox + Sc11GateX, 0f, Sc11FenceZ + 0.3f), 0f);
            refs.spawn = Marker("Spawn_SC11", room, new Vector3(ox + 1.2f, 0f, 1.4f), 180f);
            refs.yardStand = Marker("Stand_SC11_Yard", room, new Vector3(ox + 1.0f, 0f, 0.9f), 180f);
            refs.gateStand = Marker("Stand_SC11_Gate", room, new Vector3(ox + Sc11GateX + 0.9f, 0f, -0.2f), 180f);
            refs.outsideStand = Marker("Stand_SC11_Outside", room, new Vector3(ox - 2.2f, 0f, -32f), 0f);
            return refs;
        }
    }
}
#endif

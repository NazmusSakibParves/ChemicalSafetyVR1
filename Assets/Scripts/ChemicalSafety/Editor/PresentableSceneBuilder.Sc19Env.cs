#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 19: confined-space entry of a chemical tank. The scene, the four characters, the props and the film camera are the client
    /// animator's own work (Tank pipe24.blend, exported by Blender to Assets/Art/Presentable/3D/Sc19/Sc19Film.glb with the finished 38.75 s timeline
    /// baked into 17 clips). The scene is scaled 0.53 so the characters (3.4 units tall in Blender) are human size. Only the colours of the roof,
    /// wall, ground, hatch and equipment case are changed to match the client's workflow pictures (textures from Tools/generate_sc19_textures.py).
    /// Nothing of the other scenarios is touched. <c>Sc19Enabled = false</c> + Build Presentable Scene leaves the scenario out of the scene.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc19Enabled = true;
        private const float Sc19Ox = 580f;
        private const float Sc19Scale = 0.53f;
        /// <summary>false (default) = the learner's own camera follows the animator's film camera (the rig is carried along it); true = the film is shown on a
        /// big screen in front of the learner with a second camera (the earlier version). Build Presentable Scene after changing it.</summary>
        private const bool Sc19FilmOnScreen = false;
        private const string Sc19Dir = ArtRoot + "/3D/Sc19";
        private const string Sc19AudioDir = "Assets/Art/Audio/Sc19/";

        private sealed class Sc19Refs
        {
            public Transform room;
            public Sc06Gate gate;
            public Sc19Cinematic cine;
            public Transform viewing;
        }

        private static Texture2D Sc19Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc19Dir + "/Tex/" + n + ".png");

        private static AudioClip Sc19Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sc19AudioDir + n + ".wav");

        private static Sc19Refs BuildSc19Room(Transform env, PresentableArt art)
        {
            var refs = new Sc19Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            refs.room = room;
            if (!Sc19Enabled)
                return refs;

            // bright hazy daylight over the plant (workflow pictures: soft sun, light haze far away)
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.86f, 0.87f, 0.90f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.78f, 0.74f, 0.68f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.46f, 0.40f, 0.34f);
            mso.FindProperty("fogColor").colorValue = new Color(0.80f, 0.83f, 0.86f);
            mso.FindProperty("fogStart").floatValue = 30f;
            mso.FindProperty("fogEnd").floatValue = 170f;
            mso.FindProperty("sunIntensity").floatValue = 1.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.8f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var roofMat = Mat("Sc19Roof", Color.white, 0.22f, 0f, Sc19Tex("Sc19_RoofRust"));
            roofMat.SetTextureScale("_BaseMap", new Vector2(2.2f, 2.2f));
            var wallMat = Mat("Sc19Wall", Color.white, 0.12f, 0f, Sc19Tex("Sc19_WallConcrete"));
            wallMat.SetTextureScale("_BaseMap", new Vector2(2.5f, 1.6f));
            var groundMat = Mat("Sc19Ground", Color.white, 0.1f, 0f, Sc19Tex("Sc19_GroundConcrete"));
            groundMat.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
            var coverMat = Mat("Sc19Cover", Color.white, 0.25f, 0.1f, Sc19Tex("Sc19_CoverRust"));
            var caseMat = Mat("Sc19PpeCase", new Color(0.56f, 0.58f, 0.59f), 0.42f, 0.1f); // the grey equipment box of the workflow pictures
            // more of the workflow pictures' colours: orange ventilator duct and tripod, silver galvanized railing, grey equipment box
            var ductMat = Mat("Sc19Duct", new Color(0.86f, 0.44f, 0.09f), 0.30f, 0f);
            var tripodMat = Mat("Sc19Tripod", new Color(0.88f, 0.40f, 0.07f), 0.38f, 0.45f);
            var railMat = Mat("Sc19Railing", new Color(0.80f, 0.82f, 0.84f), 0.55f, 0.65f);
            var boxMat = Mat("Sc19PpeBox", new Color(0.60f, 0.62f, 0.63f), 0.40f, 0.15f);
            var permitMat = Mat("Sc19Permit", Color.white, 0.02f, 0f, AssetDatabase.LoadAssetAtPath<Texture2D>(Sc19Dir + "/Tex/Sc19_Permit.png"));
            // the picture is a photo of the whole clipboard on a blurred background (1510 x 1041); the paper quad (UV 0..1) must show only the white page, and
            // the glb's UVs run the other way up (the page was upside down): page = pixels x 380..1130, y 137..963 of the picture
            permitMat.SetTextureScale("_BaseMap", new Vector2(0.4967f, -0.7935f));
            permitMat.SetTextureOffset("_BaseMap", new Vector2(0.2517f, 0.8684f));
            // the glb has no colour picture for the entrant's overall, face, vest and harness (Blender node groups the exporter cannot read): the client's own
            // pictures are put back by material name
            Texture2D Pic(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc19Dir + "/Tex/" + n + ".jpg");
            var workerMat = Mat("Sc19Worker", Color.white, 0.22f, 0f, Pic("Sc19_WorkerBody"));
            var headMat = Mat("Sc19Head", Color.white, 0.35f, 0f, Pic("Sc19_WorkerHead"));
            var vestMat = Mat("Sc19Vest", Color.white, 0.30f, 0f, Pic("Sc19_Vest"));
            var strapsMat = Mat("Sc19Straps", Color.white, 0.30f, 0f, Pic("Sc19_Straps"));
            var jacketMat = Mat("Sc19Jacket", Color.white, 0.25f, 0f, Pic("Sc19_Jacket"));
            var shirtMat = Mat("Sc19Shirt", Color.white, 0.25f, 0f, Pic("Sc19_Shirt"));
            var bootMat = Mat("Sc19Boot", Color.white, 0.25f, 0f, Pic("Sc19_Boot"));
            var armsMat = Mat("Sc19Arms", Color.white, 0.30f, 0f, Pic("Sc19_Arms"));
            var faceMat = Mat("Sc19Face", Color.white, 0.35f, 0f, Pic("Sc19_Face"));
            var hairMat = Mat("Sc19Hair", Color.white, 0.30f, 0f, Pic("Sc19_Hair"));

            // ---- the animator's film scene
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Sc19Dir + "/Sc19Film.glb");
            if (asset == null)
            {
                Debug.LogError("[Sc19] Sc19Film.glb missing in " + Sc19Dir);
                return refs;
            }

            var film = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(film, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            film.name = "Sc19Film";
            film.transform.SetParent(room, false);
            film.transform.localPosition = new Vector3(Sc19Ox, 0f, 0f);
            film.transform.localRotation = Quaternion.identity;
            film.transform.localScale = Vector3.one * Sc19Scale;
            foreach (var a in film.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
            Sc04NoColliders(film);
            foreach (var sk in film.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sk.updateWhenOffscreen = true; // the bones are moved by code each frame: the bind-pose bounds would cull the characters
            // the animator's own tank wall picture (white metal with long rust streaks): the outside keeps the workflow concrete, the INSIDE of the tank,
            // the old rusted lid and nothing else use this one
            Texture streakTex = null;
            foreach (var r0 in film.GetComponentsInChildren<Renderer>(true))
                foreach (var m0 in r0.sharedMaterials)
                    if (m0 != null && m0.name == "Material.004" && r0.name == "Cube.001" && streakTex == null)
                        streakTex = m0.HasProperty("baseColorTexture") ? m0.GetTexture("baseColorTexture") : m0.mainTexture;
            // the lid's own UVs are tiny: a rust-brown noise picture tinted grey reads as an old, rusted dome from any distance
            var lidMat = Mat("Sc19Lid", new Color(0.74f, 0.60f, 0.50f), 0.08f, 0.2f, Sc19Tex("Sc19_CoverRust"));
            lidMat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            var domeMat = Mat("Sc19Dome", new Color(0.90f, 0.78f, 0.68f), 0.12f, 0.1f, streakTex);
            domeMat.SetTextureScale("_BaseMap", new Vector2(1.3f, 1.3f));
            var innerMat = Mat("Sc19TankInner", new Color(0.92f, 0.88f, 0.84f), 0.10f, 0.05f, streakTex);
            innerMat.SetFloat("_Cull", 2f); // back faces culled: the wall is a one-way inside skin
            innerMat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));

            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (m == null)
                        continue;
                    Material swap = null;
                    if (m.name == "Corroded Painted Steel")
                        swap = roofMat;
                    else if (m.name == "Material.004")
                        swap = r.name == "Cube.004" ? domeMat : wallMat; // Cube.004 is the round lid on the roof (the animator: white metal, rust blotches)
                    else if ((m.name == "Material" || m.name == "Brass") && r.name == "_gltfNode_4")
                        swap = lidMat; // the dome lid on the roof: old, rusty (the toolbox "Material_0.001" keeps its own blue picture)
                    else if (m.name == "GrayTile")
                        swap = groundMat;
                    else if (m.name == "Material.006")
                        swap = ductMat;
                    else if (m.name == "main.001" || m.name == "beam.001")
                        swap = tripodMat;
                    else if (m.name == "Material.002")
                        swap = railMat;
                    else if (m.name == "Material.001")
                        swap = boxMat;
                    else if (m.name == "Plane.004__0")
                        swap = permitMat; // the clipboard picture at full resolution (the glb copy was cut to 1024 px and blurred)
                    else if (m.name == "Material #414.001")
                        swap = workerMat;
                    else if (m.name == "worker head material.001" || m.name == "worker head material.002")
                        swap = headMat;
                    else if (m.name == "DBI_SALA_Harness_Accessories.002")
                        swap = vestMat;
                    else if (m.name == "Material.015")
                        swap = strapsMat;
                    else if (m.name == "Material.011")
                        swap = jacketMat;
                    else if (m.name == "pants")
                        swap = shirtMat;
                    else if (m.name == "boot")
                        swap = bootMat;
                    else if (m.name == "arms")
                        swap = armsMat;
                    else if (m.name == "face")
                        swap = faceMat;
                    else if (m.name == "Material.012")
                        swap = hairMat;
                    if (swap != null)
                    {
                        ms[i] = swap;
                        changed = true;
                    }
                }

                if (changed)
                    r.sharedMaterials = ms;
            }

            // the entrant's red vest mesh of the glb is not bound to the body (it is stretched over the whole yard as a red scrap): never drawn
            foreach (var sk in film.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sk.name == "_gltfNode_6.002")
                    sk.enabled = false;

            // ---- the inside of the tank: the glb tank is a one-sided shell, from inside / through the manhole the yard floor showed. A rusted wall and floor
            // (the animator's own streaked picture) close it. Film units: wall radius 3.8, centre (0, -0.3), roof underside 7.14.
            {
                var inside = new GameObject("Sc19TankInside").transform;
                inside.SetParent(film.transform, false);
                inside.localPosition = new Vector3(0f, 0f, -0.3f);
                const int seg = 48;
                const float rad = 3.70f, y0 = 0.05f, y1 = 7.25f;
                var vs = new Vector3[(seg + 1) * 2];
                var uv = new Vector2[vs.Length];
                var nm = new Vector3[vs.Length];
                for (var i = 0; i <= seg; i++)
                {
                    var a2 = Mathf.PI * 2f * i / seg;
                    var dir = new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2));
                    vs[i * 2] = dir * rad + Vector3.up * y0;
                    vs[i * 2 + 1] = dir * rad + Vector3.up * y1;
                    uv[i * 2] = new Vector2(i * 4.4f / seg, 0f);
                    uv[i * 2 + 1] = new Vector2(i * 4.4f / seg, 1.6f);
                    nm[i * 2] = nm[i * 2 + 1] = -dir;
                }

                var tris = new int[seg * 6];
                for (var i = 0; i < seg; i++)
                {
                    var q = i * 2;
                    tris[i * 6] = q; tris[i * 6 + 1] = q + 2; tris[i * 6 + 2] = q + 1; // front faces look to the axis: drawn from inside only, never over the outside of the tank
                    tris[i * 6 + 3] = q + 1; tris[i * 6 + 4] = q + 2; tris[i * 6 + 5] = q + 3;
                }

                var tube = new Mesh { name = "Sc19TankInsideWall", vertices = vs, uv = uv, normals = nm, triangles = tris };
                tube.RecalculateBounds();
                var wallGo = new GameObject("Wall");
                wallGo.transform.SetParent(inside, false);
                wallGo.AddComponent<MeshFilter>().sharedMesh = tube;
                var wr = wallGo.AddComponent<MeshRenderer>();
                wr.sharedMaterial = innerMat;
                wr.shadowCastingMode = ShadowCastingMode.Off;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "Floor";
                floor.transform.SetParent(inside, false);
                floor.transform.localPosition = new Vector3(0f, y0 + 0.01f, 0f);
                floor.transform.localScale = new Vector3(rad * 2f, 0.01f, rad * 2f);
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                var fr = floor.GetComponent<MeshRenderer>();
                fr.sharedMaterial = innerMat;
                fr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // ---- DANGER signs on the railing (workflow pictures: "Danger - Confined space - entry permit required")
            var signMat = Mat("Sc19SignDanger", Color.white, 0.2f, 0f, Sc19Tex("Sc19_SignDanger"));
            var centre = new Vector3(0f, 0f, -0.3f);
            var baseAng = Mathf.Atan2(7.59f + 0.3f, -7.47f);
            foreach (var off in new[] { 0f, -0.95f })
            {
                var ang = baseAng + off;
                var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var sign = QuadObj("Sc19Sign", film.transform, centre + radial * 4.55f + Vector3.up * 8.6f, Vector3.zero, new Vector2(2.3f, 1.3f), signMat);
                sign.transform.localRotation = Quaternion.LookRotation(-radial, Vector3.up); // the quad shows its picture to -Z: it looks away from the viewer's side
            }

            Sc19SmoothTextures();
            Sc19BuildSurroundings(room, wallMat);

            // ---- sky
            var skyTex = Sc11YardTex("Yard_Sky");
            var skyMat = Mat("Sc19Sky", Color.white, 0f, 0f, skyTex, null, true);
            skyMat.SetFloat("_Cull", 0f);
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "SkyDome";
            sky.transform.SetParent(room, false);
            sky.transform.position = new Vector3(Sc19Ox, -5f, 0f);
            sky.transform.localScale = new Vector3(-300f, 300f, 300f);
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            sky.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(sky.GetComponent<Collider>());

            // ---- the animator's plant only stands behind the tank: copies of its pipe racks and tanks fill the other sides, so wherever the learner looks
            // it is an industrial yard (film units: the film root is scaled, the copies are its children)
            Transform Find(string n)
            {
                foreach (var t in film.GetComponentsInChildren<Transform>(true))
                    if (t.name == n)
                        return t;
                return null;
            }

            void Copy(string n, Vector3 at, float yaw)
            {
                var src = Find(n);
                if (src == null)
                    return;
                var c = Object.Instantiate(src.gameObject, film.transform);
                c.name = n + "_Copy";
                c.transform.localPosition = at;
                c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            Copy("_Pipe_.001", new Vector3(5.63f, -0.04f, 23.47f), 180f);   // rack across the back of the learner's spot
            Copy("_Pipe_", new Vector3(-28f, -0.04f, 11.06f), 180f);         // rack down the left
            Copy("_Pipe_", new Vector3(-28f, -0.04f, 34f), 0f);
            Copy("_Pipe_.001", new Vector3(-34f, -0.04f, 4f), 90f);
            Copy("_Fuel_Tank_1", new Vector3(-9f, 0.02f, 14.5f), 0f);
            Copy("Fuel_tank_3_", new Vector3(-22f, 0.02f, 18f), 90f);
            Copy("Fuel_tank_3_", new Vector3(24f, 0.02f, 18f), 0f);
            Copy("_Fuel_Tank_1", new Vector3(-30f, 0.02f, -6f), 0f);

            // ---- where the learner stands: on the ground in front of the tank, looking at it (the film screen floats in front of the learner)
            var viewPos = new Vector3(Sc19Ox - 2.5f, 0f, 9.5f);
            var look = new Vector3(Sc19Ox, 0f, -0.2f) - viewPos;
            refs.viewing = Marker("Sc19Viewing", room, viewPos, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
            var ground = Box("Sc19ViewingGround", room, new Vector3(Sc19Ox - 2.5f, -0.1f, 9.5f), new Vector3(8f, 0.2f, 8f), groundMat);
            ground.GetComponent<MeshRenderer>().enabled = false;
            ground.AddComponent<TeleportationArea>();
            if (SolidLayer >= 0)
                ground.layer = SolidLayer;

            refs.cine = Sc19BuildFilmRig(room, film.transform);

            // ---- "inspection complete" card
            var gateGo = new GameObject("Sc19GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc19Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc19Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc19Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }

        /// <summary>Sharper, steadier textures in the headset: mip maps and 8x anisotropic filtering on every Scenario 19 picture (ground joints shimmer without).</summary>
        private static void Sc19SmoothTextures()
        {
            // the data sheet card (a Resources sprite): mip maps + trilinear + 8x aniso, or it shimmers when the head moves
            var sds = AssetImporter.GetAtPath("Assets/Resources/Sc19/Sc19_Sds.png") as TextureImporter;
            if (sds != null && (!sds.mipmapEnabled || sds.filterMode != FilterMode.Trilinear || sds.anisoLevel != 8 || sds.maxTextureSize != 2048))
            {
                sds.textureType = TextureImporterType.Sprite;
                sds.spriteImportMode = SpriteImportMode.Single;
                sds.mipmapEnabled = true;
                sds.filterMode = FilterMode.Trilinear;
                sds.anisoLevel = 8;
                sds.maxTextureSize = 2048;
                sds.textureCompression = TextureImporterCompression.CompressedHQ;
                sds.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc19Dir + "/Tex" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null)
                    continue;
                if (imp.anisoLevel != 8 || !imp.mipmapEnabled || imp.filterMode != FilterMode.Trilinear)
                {
                    imp.anisoLevel = 8;
                    imp.mipmapEnabled = true;
                    imp.filterMode = FilterMode.Trilinear;
                    imp.maxTextureSize = 2048;
                    imp.SaveAndReimport();
                }
            }

            // the entry permit (1510 x 1041, the animator's own picture): "To Nearest" squeezed it to 1024 x 1024 and plain compression blurred the small print.
            // Keep its real size and use the best ASTC on the Quest (this one texture only).
            var permit = AssetImporter.GetAtPath(Sc19Dir + "/Tex/Sc19_Permit.png") as TextureImporter;
            if (permit != null)
            {
                var android = permit.GetPlatformTextureSettings("Android");
                if (permit.npotScale != TextureImporterNPOTScale.None || !android.overridden || android.format != TextureImporterFormat.ASTC_4x4 ||
                    android.compressionQuality != 100)
                {
                    permit.npotScale = TextureImporterNPOTScale.None;
                    permit.maxTextureSize = 2048;
                    permit.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android",
                        overridden = true,
                        maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_4x4,
                        textureCompression = TextureImporterCompression.CompressedHQ,
                        compressionQuality = 100,
                    });
                    permit.SaveAndReimport();
                }
            }
        }

        /// <summary>The animator's plant only stands behind the tank. Halls, warehouses, silos and chimneys all round it, far enough not to touch the film,
        /// so wherever the learner looks (or the film camera sees from above) it is an industrial site, not empty ground. World units, relative to the origin.</summary>
        private static void Sc19BuildSurroundings(Transform room, Material wallMat)
        {
            var g = new GameObject("Sc19Surroundings").transform;
            g.SetParent(room, false);
            var roofMat = Mat("Sc19BldRoof", new Color(0.30f, 0.31f, 0.32f), 0.25f, 0.3f);
            var glassMat = Mat("Sc19BldGlass", new Color(0.13f, 0.21f, 0.28f), 0.85f, 0.25f);
            var doorMat = Mat("Sc19BldDoor", new Color(0.22f, 0.24f, 0.26f), 0.3f, 0.4f);
            var tinTex = Sc11YardTex("Yard_Tin");
            var stackMat = Mat("Sc19Stack", new Color(0.78f, 0.78f, 0.76f), 0.3f, 0.2f);
            var stackRed = Mat("Sc19StackRed", new Color(0.72f, 0.16f, 0.12f), 0.3f, 0.2f);
            var siloMat = Mat("Sc19Silo", new Color(0.62f, 0.64f, 0.66f), 0.5f, 0.6f);
            var n = 0;

            void Building(float cx, float cz, float w, float d, float h, bool tin)
            {
                n++;
                var wall = tin
                    ? Mat("Sc19Bld" + n, new Color(0.88f, 0.89f, 0.88f), 0.35f, 0.45f, tinTex)
                    : Mat("Sc19Bld" + n, Color.white, 0.1f, 0f, Sc19Tex("Sc19_WallConcrete"));
                wall.SetTextureScale("_BaseMap", tin ? new Vector2(w * 0.5f, 2f) : new Vector2(w * 0.18f, h * 0.18f));
                var x = Sc19Ox + cx;
                Box("Bld" + n, g, new Vector3(x, h * 0.5f, cz), new Vector3(w, h, d), wall, false);
                Box("BldRoof" + n, g, new Vector3(x, h + 0.25f, cz), new Vector3(w + 0.8f, 0.5f, d + 0.8f), roofMat, false);
                // the faces that look at the tank: a window band and a loading door
                var toTank = Mathf.Abs(cx) > Mathf.Abs(cz) ? new Vector3(-Mathf.Sign(cx), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(cz));
                var alongX = Mathf.Abs(toTank.z) > 0.5f; // the face is parallel to X
                var face = new Vector3(x, 0f, cz) + Vector3.Scale(toTank, new Vector3(w * 0.5f, 0f, d * 0.5f));
                var band = new Vector3(alongX ? w * 0.86f : 0.12f, 1.5f, alongX ? 0.12f : d * 0.86f);
                Box("BldWin" + n, g, face + Vector3.up * (h * 0.64f) + toTank * 0.03f, band, glassMat, false);
                var door = new Vector3(alongX ? 3.4f : 0.14f, 4.2f, alongX ? 0.14f : 3.4f);
                Box("BldDoor" + n, g, face + Vector3.up * 2.1f + toTank * 0.04f + (alongX ? Vector3.right : Vector3.forward) * (w * 0.2f), door, doorMat, false);
                for (var v = 0; v < 3; v++)
                    Box("BldVent" + n + "_" + v, g, new Vector3(x - w * 0.3f + v * w * 0.3f, h + 0.9f, cz), new Vector3(1.6f, 0.9f, 1.6f), doorMat, false);
            }

            void Stack(float cx, float cz, float r, float h, bool banded)
            {
                var x = Sc19Ox + cx;
                Cyl("Stack", g, new Vector3(x, 0f, cz), h, r, stackMat);
                if (banded)
                    for (var b = 0; b < 3; b++)
                        Cyl("StackBand", g, new Vector3(x, h * (0.50f + b * 0.17f), cz), h * 0.085f, r * 1.02f, stackRed);
            }

            void Silo(float cx, float cz, float r, float h)
            {
                var x = Sc19Ox + cx;
                Cyl("Silo", g, new Vector3(x, 0f, cz), h, r, siloMat);
                Cyl("SiloTop", g, new Vector3(x, h, cz), 0.5f, r * 1.04f, roofMat);
            }

            Building(-12f, -44f, 38f, 12f, 11f, true);   // back, long warehouse
            Building(28f, -38f, 22f, 14f, 9f, false);
            Building(-46f, -8f, 14f, 32f, 12f, true);    // left hall
            Building(44f, 4f, 14f, 28f, 10f, false);     // right hall
            Building(-18f, 40f, 32f, 10f, 9f, false);    // behind the learner
            Building(24f, 44f, 26f, 12f, 12f, true);
            Building(-58f, 34f, 18f, 14f, 8f, false);
            Building(58f, -30f, 16f, 18f, 11f, true);
            Stack(-32f, -32f, 1.1f, 34f, true);
            Stack(38f, -24f, 0.9f, 28f, true);
            Stack(-40f, 20f, 0.8f, 22f, false);
            Silo(-32f, 18f, 3.2f, 12f);
            Silo(-27f, 24f, 2.6f, 10f);
            Silo(32f, 22f, 2.8f, 11f);
            Silo(38f, 28f, 2.4f, 9f);
        }

        private static ParticleSystem Sc19Haze(string name, Transform parent, Vector3 localAt, float radius, float rate, Color col, float size, float life, float speed)
        {
            var sheet = ParticleMat("Sc19HazePuff", Sc05Tex("Steam_Puff"), false); // soft light puffs: tinted yellow-grey they read as toxic haze, not black smoke
            var ps = Sc07Ps(name, parent, parent.TransformPoint(localAt), new Vector3(-90f, 0f, 0f), sheet, 0.2f);
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life * 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed * 1.3f);
            m.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.gravityModifier = -0.03f;
            m.startColor = col;
            m.maxParticles = 90;
            var e = ps.emission;
            e.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = radius;
            Sc07FadeCol(ps, 0.2f, 0.6f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            return ps;
        }

        /// <summary>The player of the film: clips, haze, light in the shaft, sounds. Positions are local to the (scaled) film root.</summary>
        private static Sc19Cinematic Sc19BuildFilmRig(Transform room, Transform film)
        {
            var cineGo = new GameObject("Sc19Cinematic");
            cineGo.transform.SetParent(room, false);
            var cine = cineGo.AddComponent<Sc19Cinematic>();
            var cso = new SerializedObject(cine);
            cso.FindProperty("filmRoot").objectReferenceValue = film;

            // the film camera (renders to a texture while the screen is visible) and the screen that shows it
            var camGo = new GameObject("Sc19FilmCamera");
            camGo.transform.SetParent(cineGo.transform, false);
            var fcam = camGo.AddComponent<Camera>();
            fcam.fieldOfView = 32.2f; // Blender: 35 mm lens, 16:9
            fcam.nearClipPlane = 0.05f;
            fcam.farClipPlane = 300f;
            fcam.clearFlags = CameraClearFlags.SolidColor;
            fcam.backgroundColor = new Color(0.62f, 0.72f, 0.85f);
            fcam.enabled = false;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            var screenRoot = new GameObject("Sc19FilmScreen").transform;
            screenRoot.SetParent(room, false);
            var screenMat = Mat("Sc19FilmScreen", Color.white, 0f, 0f, null, null, true);
            var frameMat = Mat("Sc19FilmScreenFrame", new Color(0.04f, 0.10f, 0.16f), 0.4f, 0f, null, null, true);
            var face = QuadObj("Picture", screenRoot, Vector3.zero, Vector3.zero, new Vector2(3.2f, 1.8f), screenMat);
            QuadObj("Frame", screenRoot, new Vector3(0f, 0f, 0.012f), Vector3.zero, new Vector2(3.32f, 1.92f), frameMat);
            screenRoot.gameObject.SetActive(false);
            cso.FindProperty("useScreen").boolValue = Sc19FilmOnScreen;
            cso.FindProperty("filmCam").objectReferenceValue = fcam;
            cso.FindProperty("screen").objectReferenceValue = screenRoot;
            cso.FindProperty("screenRenderer").objectReferenceValue = face.GetComponent<MeshRenderer>();

            // the 17 clips of the glb (each runs the whole 38.75 s)
            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Sc19Dir + "/Sc19Film.glb"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    clips.Add(c);
            var cp = cso.FindProperty("clips");
            cp.arraySize = clips.Count;
            for (var i = 0; i < clips.Count; i++)
                cp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            Debug.Log("[Sc19] film clips = " + clips.Count);

            // toxic haze: out of the tank opening and a denser one in the shaft (client: smoke VFX inside the tank)
            var smokeRoot = new GameObject("Haze").transform;
            smokeRoot.SetParent(film, false);
            var s1 = Sc19Haze("HazeAtOpening", smokeRoot, new Vector3(0f, 7.75f, -0.3f), 0.8f, 9f, new Color(0.66f, 0.64f, 0.46f, 0.42f), 1.6f, 2.8f, 1.0f);
            var s2 = Sc19Haze("HazeInShaft", smokeRoot, new Vector3(0f, 0.4f, -0.3f), 1.6f, 22f, new Color(0.60f, 0.57f, 0.38f, 0.5f), 2.4f, 4.5f, 1.0f);
            var sp = cso.FindProperty("smoke");
            sp.arraySize = 2;
            sp.GetArrayElementAtIndex(0).objectReferenceValue = s1;
            sp.GetArrayElementAtIndex(1).objectReferenceValue = s2;

            // light down the shaft: the floor with the worker must read from above
            var lg = new GameObject("ShaftLight");
            lg.transform.SetParent(film, false);
            lg.transform.localPosition = new Vector3(0f, 4.8f, -0.3f);
            var shaft = lg.AddComponent<Light>();
            shaft.type = LightType.Point;
            shaft.range = 6.5f;
            shaft.intensity = 4.5f;
            shaft.color = new Color(1f, 0.94f, 0.82f);
            shaft.shadows = LightShadows.None;

            // sounds
            AudioSource Src(string n, bool loop, float vol)
            {
                var g = new GameObject(n);
                g.transform.SetParent(cineGo.transform, false);
                var s = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = loop;
                s.volume = vol;
                s.spatialBlend = 0.85f;
                s.minDistance = 2.5f;
                s.maxDistance = 40f;
                return s;
            }

            cso.FindProperty("fxSource").objectReferenceValue = Src("FxSource", false, 1f);
            cso.FindProperty("loopSource").objectReferenceValue = Src("BlowerSource", true, 0.55f);
            var coughs = new System.Collections.Generic.List<AudioClip>();
            for (var i = 0; i < 2; i++)
            {
                var c = Sc19Clip("Cough_" + i) ?? Sc19Clip("Cough_Synth_" + i);
                if (c != null)
                    coughs.Add(c);
            }

            var cgp = cso.FindProperty("coughs");
            cgp.arraySize = coughs.Count;
            for (var i = 0; i < coughs.Count; i++)
                cgp.GetArrayElementAtIndex(i).objectReferenceValue = coughs[i];
            cso.FindProperty("gasp").objectReferenceValue = Sc19Clip("Gasp");
            cso.FindProperty("thud").objectReferenceValue = Sc19Clip("Thud");
            cso.FindProperty("blowerLoop").objectReferenceValue = Sc19Clip("Blower_Loop");
            cso.FindProperty("beep").objectReferenceValue = Sc19Clip("Beep");
            cso.FindProperty("clank").objectReferenceValue = Sc19Clip("Clank");
            cso.FindProperty("pen").objectReferenceValue = Sc19Clip("Pen");
            cso.ApplyModifiedPropertiesWithoutUndo();

            // the distant plant hums while the room is on
            var amb = new GameObject("PlantAmbience");
            amb.transform.SetParent(room, false);
            var asrc = amb.AddComponent<AudioSource>();
            asrc.clip = Sc19Clip("Plant_Loop");
            asrc.loop = true;
            asrc.playOnAwake = true;
            asrc.spatialBlend = 0f;
            asrc.volume = 0.35f;
            return cine;
        }

        private static void Sc19BakeProbe(Transform env1, Transform envSc)
        {
            if (!Sc19Enabled)
                return;
            try
            {
                var room = envSc.Find("Room");
                var go = new GameObject("Sc19ReflectionProbe");
                go.transform.SetParent(room, false);
                go.transform.position = new Vector3(Sc19Ox, 6f, 0f);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = 256;
                probe.size = new Vector3(40f, 24f, 40f);
                probe.boxProjection = false;
                probe.intensity = 1.0f;
                probe.importance = 2;
                var path = GenDir + "/Sc19Probe.exr";
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
                Debug.LogWarning("[Sc19] reflection probe skipped: " + e.Message);
            }
        }
    }
}
#endif

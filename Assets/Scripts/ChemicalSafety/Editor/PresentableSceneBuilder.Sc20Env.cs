#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 20: chemical splash on a worker, the emergency shower, the report to the supervisor. The room is a copy of the Scenario 1 hall (same
    /// builder, racks only along the two side walls so the middle stays free); the film is the client animator's own work (Scene 20.blend, exported by
    /// Blender to Assets/Art/Presentable/3D/Sc20/Sc20Film.glb, baked to Resources/Sc20/Sc20FilmBake.bytes). The film is scaled 0.5 (the characters are
    /// 3.4 units tall in Blender) and turned 90 degrees so that the walk with the drum crosses the free middle of the hall and the shower stands against
    /// the right wall, 10 m further on. Added to the animator's film: the chemical splash and the stains on the clothes, the wet look, the shower
    /// water, the taken-off clothes in the yellow bins, sounds. Nothing of the other scenarios is touched. <c>Sc20Enabled = false</c> + Build
    /// Presentable Scene leaves the scenario out of the scene.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc20Enabled = true;
        private const float Sc20Scale = 0.5f;
        // film (x, z) -> hall (X, Z): X = Sc20Origin.x + 0.5 * zf, Z = Sc20Origin.z - 0.5 * xf (the film is turned 90 degrees about Y)
        private static readonly Vector3 Sc20Origin = new Vector3(-2.5f, 0f, 7.85f);
        private const string Sc20Dir = ArtRoot + "/3D/Sc20";
        private const string Sc20AudioDir = "Assets/Art/Audio/Sc20/";

        private sealed class Sc20Refs
        {
            public Transform room;
            public Sc06Gate gate;
            public Sc20Cinematic cine;
            public Transform viewing;
        }

        private static AudioClip Sc20Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sc20AudioDir + n + ".wav");

        private static Vector3 Sc20Hall(float xf, float yf, float zf) =>
            new Vector3(Sc20Origin.x + Sc20Scale * zf, yf * Sc20Scale, Sc20Origin.z - Sc20Scale * xf);

        private static Sc20Refs BuildSc20Room(Transform env, PresentableArt art)
        {
            var refs = new Sc20Refs();
            if (!Sc20Enabled)
            {
                refs.room = new GameObject("Room").transform;
                refs.room.SetParent(env, false);
                return refs;
            }

            // ---- the hall of Scenario 1 (same builder) with the middle kept free
            BuildSc01Hall(env, art);
            var hall = env.Find("Hall");
            var racks = hall != null ? hall.Find("Racks") : null;
            if (racks != null && racks.childCount >= 4)
            {
                // [0] along the back wall and [3] the free-standing one would stand in the learner's way / in the walk: only the two side runs stay
                Object.DestroyImmediate(racks.GetChild(3).gameObject);
                Object.DestroyImmediate(racks.GetChild(0).gameObject);
            }

            // the walk crosses the middle of the hall: the half wall at z 7.5 and the three drums at the left wall (Scenario 1 props) are not in this copy
            if (hall != null)
                foreach (var n in new[] { "HalfWall", "LeftDrum1", "LeftDrum2", "MidDrum", "LeftDrum1Solid", "LeftDrum2Solid", "MidDrumSolid", "LeftDrumLabel", "FloorCable", "BackBlock", "VentGrille" })
                {
                    Transform victim = null;
                    foreach (var t in hall.GetComponentsInChildren<Transform>(true))
                        if (t.name == n)
                        {
                            victim = t;
                            break;
                        }

                    if (victim != null)
                        Object.DestroyImmediate(victim.gameObject);
                }

            if (hall != null)
                foreach (var t in new List<Transform>(hall.GetComponentsInChildren<Transform>(true)))
                    if (t != null && t.name == "VentSlat")
                        Object.DestroyImmediate(t.gameObject);

            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            refs.room = room;

            // light on the walk and on the shower (the hall lamps are tuned for the drum bay)
            void Lamp(string n, Vector3 at, float range, float intensity, Color col)
            {
                var g = new GameObject(n);
                g.transform.SetParent(room, false);
                g.transform.position = at;
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.intensity = intensity;
                l.color = col;
                l.shadows = LightShadows.None;
            }

            Lamp("Sc20LampStart", new Vector3(-0.6f, 3.6f, 6.5f), 9f, 5.5f, new Color(1f, 0.96f, 0.88f));
            Lamp("Sc20LampMid", new Vector3(0.2f, 3.6f, 1.5f), 9f, 5f, new Color(1f, 0.96f, 0.88f));
            Lamp("Sc20LampShower", new Vector3(2.2f, 3.4f, -2.5f), 8f, 6f, new Color(1f, 0.97f, 0.9f));
            Lamp("Sc20LampBack", new Vector3(0f, 3.4f, 12.5f), 7f, 3.5f, new Color(1f, 0.96f, 0.88f));

            // the baked reflection probe of the Scenario 1 hall is the same hall: reuse its picture (nothing is baked here)
            try
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(GenDir + "/Sc01Probe.exr");
                if (tex != null)
                {
                    var pg = new GameObject("Sc20ReflectionProbe");
                    pg.transform.SetParent(room, false);
                    pg.transform.position = new Vector3(0f, 2.2f, 2.5f);
                    var probe = pg.AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Baked;
                    probe.resolution = 256;
                    probe.size = new Vector3(8.6f, 7.4f, 24.8f);
                    probe.center = new Vector3(0f, 1.3f, 1f);
                    probe.boxProjection = true;
                    probe.importance = 1;
                    probe.bakedTexture = tex;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Sc20] reflection probe skipped: " + e.Message);
            }

            // ---- the animator's film scene
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Sc20Dir + "/Sc20Film.glb");
            if (asset == null)
            {
                Debug.LogError("[Sc20] Sc20Film.glb missing in " + Sc20Dir);
                return refs;
            }

            var film = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(film, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            film.name = "Sc20Film";
            film.transform.SetParent(room, false);
            film.transform.position = Sc20Origin;
            film.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            film.transform.localScale = Vector3.one * Sc20Scale;
            foreach (var a in film.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
            Sc04NoColliders(film);
            foreach (var sk in film.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sk.updateWhenOffscreen = true; // the bones are moved by code each frame: the bind-pose bounds would cull the characters

            // rig control shapes, the animator's 54 m floor plane and the empty marker are not part of the picture (the hall has its own floor)
            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("cs_") || r.name == "Plane" || r.name == "empty_17")
                    r.enabled = false;

            Sc20Recolour(film);

            // the shower (with its pull handle and eye-wash bowl) stands clear of the hall wall: the film shower scene is shifted by ShowerShift (Sc20Cinematic)
            foreach (var t in film.GetComponentsInChildren<Transform>(true))
                if (t.name == "EmergencyShower_Shower_MAT_0")
                {
                    t.localPosition += Sc20Cinematic.ShowerShift;
                    break;
                }

            var dz = Sc20Cinematic.ShowerShift.z;

            Transform FindT(string n, Transform under = null)
            {
                foreach (var t in (under != null ? under : film.transform).GetComponentsInChildren<Transform>(true))
                    if (t.name == n)
                        return t;
                return null;
            }

            // ---- the chemical on the worker: copies of his clothes (same mesh, same bones) in a wet-chemical overlay, switched on by the film at the trip
            var worker = FindT("rig");
            var stainMat = Sc20StainMaterial();
            var stainList = new List<Renderer>();
            var wetList = new List<Renderer>();
            Renderer vest = null, vestStain = null;
            foreach (var sk in worker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sk.name.StartsWith("cs_"))
                    continue;
                // skin, face and cap are never darkened or stained (clothes only)
                if (sk.name != "Object_3" && !sk.name.StartsWith("Helmet"))
                    wetList.Add(sk);
                if (sk.name == "_gltfNode_6")
                    vest = sk;
                if (sk.name.StartsWith("Helmet") || sk.name == "Object_3")
                    continue;
                var go = new GameObject(sk.name + "_Chem");
                go.transform.SetParent(sk.transform.parent, false);
                go.transform.localPosition = sk.transform.localPosition;
                go.transform.localRotation = sk.transform.localRotation;
                go.transform.localScale = sk.transform.localScale;
                var dup = go.AddComponent<SkinnedMeshRenderer>();
                dup.sharedMesh = sk.sharedMesh;
                dup.bones = sk.bones;
                dup.rootBone = sk.rootBone;
                dup.sharedMaterial = stainMat;
                dup.updateWhenOffscreen = true;
                dup.shadowCastingMode = ShadowCastingMode.Off;
                dup.receiveShadows = false;
                dup.enabled = false;
                stainList.Add(dup);
                if (sk.name == "_gltfNode_6")
                    vestStain = dup;
            }

            // the man who stands under the shower after the swap (second rig) gets the wet look as well
            var second = FindT("rig.001");
            if (second != null)
                foreach (var sk in second.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!sk.name.StartsWith("cs_") && sk.name != "Object_3.001")
                        wetList.Add(sk); // the shirtless man: wet trousers only, his skin and face keep their own colour

            // ---- the cinematic (player of the film)
            var cineGo = new GameObject("Sc20Cinematic");
            cineGo.transform.SetParent(room, false);
            var cine = cineGo.AddComponent<Sc20Cinematic>();
            var cso = new SerializedObject(cine);
            cso.FindProperty("filmRoot").objectReferenceValue = film.transform;

            var clips = new List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Sc20Dir + "/Sc20Film.glb"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    clips.Add(c);
            var cp = cso.FindProperty("clips");
            cp.arraySize = clips.Count;
            for (var i = 0; i < clips.Count; i++)
                cp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

            void SetArray(string prop, List<Renderer> list)
            {
                var ap = cso.FindProperty(prop);
                ap.arraySize = list.Count;
                for (var i = 0; i < list.Count; i++)
                    ap.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }

            SetArray("stainRenderers", stainList);
            SetArray("wetRenderers", wetList);
            cso.FindProperty("vestRenderer").objectReferenceValue = vest;
            cso.FindProperty("vestStainRenderer").objectReferenceValue = vestStain;

            // ---- effects
            var fx = new GameObject("Sc20Fx").transform;
            fx.SetParent(room, false);
            var dropMat = ParticleMat("Sc20Drop", AssetDatabase.LoadAssetAtPath<Texture2D>(Sc20Dir + "/Tex/Sc20_Drop.png"), false);
            var puffMat = ParticleMat("Sc20Fume", Sc05Tex("Steam_Puff"), false);
            var waterMat = ParticleMat("Sc20Water", AssetDatabase.LoadAssetAtPath<Texture2D>(Sc20Dir + "/Tex/Sc20_WaterDrop.png"), false);

            ParticleSystem Fx(string n, Vector3 at, Vector3 euler, Material mat)
            {
                var ps = Sc07Ps(n, fx, at, euler, mat, 0.2f);
                var m = ps.main;
                m.playOnAwake = false;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                return ps;
            }

            // chemical impact on the worker: a short spray of small, almost clear drops that splatter off the chest and arms and fall close by
            var splash = Fx("ChemSplash", Sc20Hall(0f, 3f, 3f), new Vector3(-90f, 0f, 0f), dropMat);
            {
                var m = splash.main;
                m.loop = false;
                m.duration = 0.5f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.04f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                m.gravityModifier = 1.9f;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.96f, 0.90f, 0.55f), new Color(0.88f, 0.93f, 0.80f, 0.45f));
                m.maxParticles = 140;
                var e = splash.emission;
                e.rateOverTime = 0f;
                e.SetBursts(new[] { new ParticleSystem.Burst(0f, 40), new ParticleSystem.Burst(0.08f, 28), new ParticleSystem.Burst(0.2f, 16) });
                var sh = splash.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.16f;
                var rr = splash.GetComponent<ParticleSystemRenderer>();
                rr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            // the soaked worker keeps dripping, and the corrosive liquid fumes a little (pale vapour)
            var drips = Fx("ChemDrips", Sc20Hall(0f, 2f, 3f), new Vector3(90f, 0f, 0f), dropMat);
            {
                var m = drips.main;
                m.loop = true;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
                m.startSpeed = 0.05f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                m.gravityModifier = 1.1f;
                m.startColor = new Color(0.90f, 0.94f, 0.84f, 0.60f);
                m.maxParticles = 80;
                var e = drips.emission;
                e.rateOverTime = 34f;
                var sh = drips.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.7f, 0.3f, 0.4f);
                var rr = drips.GetComponent<ParticleSystemRenderer>();
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.lengthScale = 1.6f;
            }

            var fumes = Fx("ChemFumes", Sc20Hall(0f, 3f, 3f), new Vector3(-90f, 0f, 0f), puffMat);
            {
                var m = fumes.main;
                m.loop = true;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                m.gravityModifier = -0.02f;
                m.startColor = new Color(0.85f, 0.88f, 0.62f, 0.24f);
                m.maxParticles = 50;
                var e = fumes.emission;
                e.rateOverTime = 8f;
                var sh = fumes.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 25f;
                sh.radius = 0.25f;
                Sc07FadeCol(fumes, 0.2f, 0.55f);
            }

            // shower water: from the head of the animator's shower, straight down (head centre in film units 21.0 / 5.3 / 11.7)
            var headT = new GameObject("Sc20ShowerHead").transform;
            headT.SetParent(film.transform, false);
            headT.localPosition = new Vector3(21.0f, 5.3f, 11.7f + dz);
            var water = Fx("ShowerWater", headT.position, new Vector3(90f, 0f, 0f), waterMat);
            {
                var m = water.main;
                m.loop = true;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 0.85f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.024f);
                m.gravityModifier = 0.5f;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.80f, 0.90f, 1f, 0.50f), new Color(0.95f, 0.98f, 1f, 0.70f));
                m.maxParticles = 1000;
                var e = water.emission;
                e.rateOverTime = 650f;
                var sh = water.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 11f;
                sh.radius = 0.2f;
                var rr = water.GetComponent<ParticleSystemRenderer>();
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.lengthScale = 1.3f;
                rr.velocityScale = 0.03f;
            }

            var mist = Fx("ShowerMist", Sc20Hall(21.0f, 0.5f, 11.8f + dz), new Vector3(-90f, 0f, 0f), puffMat);
            {
                var m = mist.main;
                m.loop = true;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                m.gravityModifier = -0.03f;
                m.startColor = new Color(0.92f, 0.95f, 1f, 0.14f);
                m.maxParticles = 40;
                var e = mist.emission;
                e.rateOverTime = 7f;
                var sh = mist.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 20f;
                sh.radius = 0.5f;
                Sc07FadeCol(mist, 0.25f, 0.5f);
            }

            cso.FindProperty("splash").objectReferenceValue = splash;
            cso.FindProperty("drips").objectReferenceValue = drips;
            cso.FindProperty("fumes").objectReferenceValue = fumes;
            cso.FindProperty("shower").objectReferenceValue = water;
            cso.FindProperty("showerMist").objectReferenceValue = mist;
            cso.FindProperty("showerHead").objectReferenceValue = headT;

            // the puddle that spreads on the floor under the worker after the trip (switched on by the film)
            var puddleMat = MatAlpha("Sc20PuddleMat", AssetDatabase.LoadAssetAtPath<Texture2D>(Sc20Dir + "/Tex/Sc20_Puddle.png"), Color.white, false);
            var puddle = QuadObj("ChemPuddle", fx, Sc20Hall(0.1f, 0.02f, 3.1f), new Vector3(90f, 20f, 0f), new Vector2(1.5f, 1.5f), puddleMat);
            puddle.SetActive(false);
            cso.FindProperty("puddle").objectReferenceValue = puddle;

            // the floor drain under the safety shower (a round steel grate) and the wet floor around it (grows when the water starts)
            {
                var steel = Mat("Sc20DrainSteel", new Color(0.30f, 0.31f, 0.33f), 0.55f, 0.7f);
                var dark = Mat("Sc20DrainDark", new Color(0.02f, 0.02f, 0.025f), 0.1f, 0f);
                var at = Sc20Hall(20.7f, 0.03f, 11.8f + dz);
                var drain = new GameObject("Sc20Drain").transform;
                drain.SetParent(room, false);
                drain.position = at;
                Prim("Hole", PrimitiveType.Cylinder, drain, new Vector3(0f, 0.002f, 0f), new Vector3(0.46f, 0.001f, 0.46f), dark);
                Prim("Ring", PrimitiveType.Cylinder, drain, new Vector3(0f, 0.006f, 0f), new Vector3(0.56f, 0.004f, 0.56f), steel);
                Prim("RingInner", PrimitiveType.Cylinder, drain, new Vector3(0f, 0.009f, 0f), new Vector3(0.44f, 0.005f, 0.44f), dark);
                for (var i = -3; i <= 3; i++)
                    Prim("Bar" + i, PrimitiveType.Cube, drain, new Vector3(i * 0.052f, 0.014f, 0f), new Vector3(0.018f, 0.014f, 0.42f - Mathf.Abs(i) * 0.035f), steel);
                var wetMat = MatAlpha("Sc20ShowerPuddleMat", AssetDatabase.LoadAssetAtPath<Texture2D>(Sc20Dir + "/Tex/Sc20_Puddle.png"), new Color(0.40f, 0.46f, 0.52f, 0.62f), false);
                var wet = QuadObj("ShowerPuddle", fx, at + Vector3.up * 0.004f, new Vector3(90f, 0f, 0f), new Vector2(1.5f, 1.5f), wetMat);
                wet.SetActive(false);
                cso.FindProperty("showerPuddle").objectReferenceValue = wet;
            }

            // the jet of chemical out of the drum mouth towards the worker (placed and started by the film at the trip)
            var stream = Fx("ChemStream", Sc20Hall(0f, 3f, 3f), new Vector3(0f, 0f, 0f), dropMat);
            {
                var m = stream.main;
                m.loop = false;
                m.duration = 0.5f;
                m.startLifetime = 0.28f;
                m.startSpeed = 3f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.04f);
                m.gravityModifier = Sc20Cinematic.StreamGravity;
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.96f, 0.90f, 0.60f), new Color(0.88f, 0.93f, 0.80f, 0.50f));
                m.maxParticles = 260;
                var e = stream.emission;
                e.rateOverTime = 520f;
                var sh = stream.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 2.5f;
                sh.radius = 0.025f;
                var rr = stream.GetComponent<ParticleSystemRenderer>();
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.lengthScale = 1.35f;
                rr.velocityScale = 0.015f;
            }

            foreach (var liquid in new[] { stream, splash })
            {
                // drops that reach the floor bounce a little and die there
                var col = liquid.collision;
                col.enabled = true;
                col.type = ParticleSystemCollisionType.World;
                col.mode = ParticleSystemCollisionMode.Collision3D;
                col.dampen = 0.4f;
                col.bounce = 0.12f;
                col.lifetimeLoss = 0.4f;
                col.radiusScale = 0.5f;
                col.quality = ParticleSystemCollisionQuality.Low;
            }

            cso.FindProperty("stream").objectReferenceValue = stream;
            foreach (var r0 in film.GetComponentsInChildren<Renderer>(true))
                if (r0.name == "defaultMaterial.002")
                {
                    cso.FindProperty("drumRenderer").objectReferenceValue = r0;
                    break;
                }

            // ---- the yellow bins beside the shower (workflow picture) and the taken-off clothes that land in them
            var binMat = Mat("Sc20Bin", new Color(0.95f, 0.74f, 0.06f), 0.35f, 0.05f);
            var vestPileMat = Mat("Sc20PileRed", new Color(0.72f, 0.12f, 0.10f), 0.3f);
            var darkPileMat = Mat("Sc20PileNavy", new Color(0.09f, 0.11f, 0.19f), 0.3f);
            var binA = Sc20Bin(room, "Sc20BinA", Sc20Hall(19.9f, 0f, 9.3f + dz), binMat, vestPileMat, out var pileA);
            var binB = Sc20Bin(room, "Sc20BinB", Sc20Hall(21.0f, 0f, 9.0f + dz), binMat, darkPileMat, out var pileB);
            pileA.SetActive(false);
            pileB.SetActive(false);
            var pp = cso.FindProperty("clothesPile");
            pp.arraySize = 2;
            pp.GetArrayElementAtIndex(0).objectReferenceValue = pileA;
            pp.GetArrayElementAtIndex(1).objectReferenceValue = pileB;

            // ---- sounds
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
            cso.FindProperty("loopSource").objectReferenceValue = Src("ShowerSource", true, 0.5f);
            cso.FindProperty("slosh").objectReferenceValue = Sc20Clip("Slosh");
            cso.FindProperty("splashClip").objectReferenceValue = Sc20Clip("Splash");
            cso.FindProperty("sizzle").objectReferenceValue = Sc20Clip("Sizzle");
            cso.FindProperty("pull").objectReferenceValue = Sc20Clip("Pull");
            cso.FindProperty("showerLoop").objectReferenceValue = Sc20Clip("Shower_Loop");
            cso.FindProperty("stepClip").objectReferenceValue = Sc20Clip("Step");
            cso.FindProperty("dripClip").objectReferenceValue = Sc20Clip("Drip");
            cso.ApplyModifiedPropertiesWithoutUndo();
            refs.cine = cine;

            var amb = new GameObject("HallAmbience");
            amb.transform.SetParent(room, false);
            var asrc = amb.AddComponent<AudioSource>();
            asrc.clip = Sc20Clip("Hall_Loop");
            asrc.loop = true;
            asrc.playOnAwake = true;
            asrc.spatialBlend = 0f;
            asrc.volume = 0.3f;

            // ---- where the learner stands before / after the film: at the film camera of the first shot, at the back of the hall, looking along the walk
            refs.viewing = Marker("Sc20Viewing", room, new Vector3(-0.6f, 0f, 14.8f), 180f);

            // ---- "decontamination complete" card
            var gateGo = new GameObject("Sc20GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc20Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc20Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc20Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();
            Sc20SmoothTextures();
            return refs;
        }

        /// <summary>A yellow plastic bin (open top) with a heap of cloth inside (the heap is returned, switched on by the film).</summary>
        private static GameObject Sc20Bin(Transform parent, string name, Vector3 floorAt, Material bin, Material cloth, out GameObject heap)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = floorAt;
            root.rotation = Quaternion.Euler(0f, 90f, 0f);
            const float w = 0.5f, d = 0.42f, h = 0.62f, t = 0.025f;
            Prim("Floor", PrimitiveType.Cube, root, new Vector3(0f, 0.04f, 0f), new Vector3(w, 0.05f, d), bin);
            Prim("WallA", PrimitiveType.Cube, root, new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), bin);
            Prim("WallB", PrimitiveType.Cube, root, new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, t), bin);
            Prim("WallC", PrimitiveType.Cube, root, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), bin);
            Prim("WallD", PrimitiveType.Cube, root, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), bin);
            var heapGo = new GameObject("ClothesHeap").transform;
            heapGo.SetParent(root, false);
            var rnd = new System.Random(name.GetHashCode());
            for (var i = 0; i < 5; i++)
            {
                var s = Prim("Fold" + i, PrimitiveType.Sphere, heapGo,
                    new Vector3((float)(rnd.NextDouble() - 0.5) * 0.28f, h - 0.12f + (float)rnd.NextDouble() * 0.1f, (float)(rnd.NextDouble() - 0.5) * 0.22f),
                    new Vector3(0.32f, 0.16f, 0.26f), cloth);
                s.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, (float)(rnd.NextDouble() - 0.5) * 40f);
            }

            heap = heapGo.gameObject;
            return root.gameObject;
        }

        /// <summary>
        /// The drum label patch (sub mesh 3 of the drum) has UVs for a different picture layout (the Blender mapping node is lost in the export). A copy of the
        /// mesh gets cylindrical UVs on that patch: u round the drum, v up the drum, so the whole label picture lies on the label, upright.
        /// </summary>
        private static void Sc20DrumLabelUvs(Renderer drum)
        {
            var mf = drum.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 4)
                return;
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "Sc20DrumMesh";
            var vs = mesh.vertices;
            var uv = mesh.uv;
            var idx = mesh.GetIndices(3);
            var set = new HashSet<int>(idx);
            var b = mesh.bounds;
            var cx = b.center.x;
            var cy = b.center.y;
            float zmin = float.MaxValue, zmax = float.MinValue, mean = 0f;
            foreach (var i in set)
            {
                zmin = Mathf.Min(zmin, vs[i].z);
                zmax = Mathf.Max(zmax, vs[i].z);
                mean += Mathf.Atan2(vs[i].y - cy, vs[i].x - cx) * Mathf.Rad2Deg;
            }

            mean /= Mathf.Max(1, set.Count);
            float amin = float.MaxValue, amax = float.MinValue;
            foreach (var i in set)
            {
                var a = Mathf.DeltaAngle(mean, Mathf.Atan2(vs[i].y - cy, vs[i].x - cx) * Mathf.Rad2Deg);
                amin = Mathf.Min(amin, a);
                amax = Mathf.Max(amax, a);
            }

            foreach (var i in set)
            {
                var a = Mathf.DeltaAngle(mean, Mathf.Atan2(vs[i].y - cy, vs[i].x - cx) * Mathf.Rad2Deg);
                uv[i] = new Vector2(1f - (a - amin) / Mathf.Max(0.001f, amax - amin), (vs[i].z - zmin) / Mathf.Max(0.001f, zmax - zmin));
            }

            mesh.uv = uv;
            var path = GenDir + "/Sc20DrumMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            mf.sharedMesh = mesh;
        }

        /// <summary>Lit, transparent, glossy chemical film for the stain overlay (the colour / alpha is driven per renderer by the film).</summary>
        private static Material Sc20StainMaterial()
        {
            // unlit: a lit transparent copy of the clothes collected light and made the wet cloth look white / grey; the stain is a dark brown-olive film
            // (the clothes themselves get the wet look from the film: darker, a little glossier)
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Sc20Dir + "/Tex/Sc20_Stain.png");
            var m = MatAlpha("Sc20StainUnlit", tex, Color.white, false);
            m.SetTextureScale("_BaseMap", new Vector2(1.2f, 1.2f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Workflow colours (Scene flow.jpg): orange overall and hi-vis vest, light hard hat, yellow shower with a green sign. Materials are swapped by name.</summary>
        private static void Sc20Recolour(GameObject film)
        {
            // The worker keeps the animator's own colours (red vest, red cap, dark jacket, jeans). Only what the glb export dropped is put back: the blue
            // drum (a Blender node colour) and its label picture.
            Material drumBlue = null, drumLabel = null;
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
                    if (m.name == "Plastic Blue 16")
                    {
                        // the animator drum is blue (the Blender node colour is dropped by the exporter); the workflow drum is this blue (drum level.jpeg)
                        if (drumBlue == null)
                            drumBlue = Mat("Sc20DrumBlue", new Color(0.09f, 0.36f, 0.80f), 0.5f, 0.05f);
                        swap = drumBlue;
                    }
                    else if (m.name == "Material.004" && r.name == "defaultMaterial.002")
                    {
                        // the label picture (05_hydrochloric_acid) sits in a small part of the drum UV sheet: in Blender a mapping node stretched it over the
                        // label patch; the exporter drops that node, so the patch gets cylindrical UVs of its own (Sc20DrumLabelUvs)
                        if (drumLabel == null)
                        {
                            var ltex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
                            drumLabel = Mat("Sc20DrumLabel", Color.white, 0.3f, 0f, ltex);
                            drumLabel.SetTextureScale("_BaseMap", Vector2.one);
                            drumLabel.SetTextureOffset("_BaseMap", Vector2.zero);
                            Sc20DrumLabelUvs(r);
                        }

                        swap = drumLabel;
                    }

                    if (swap != null)
                    {
                        ms[i] = swap;
                        changed = true;
                    }
                }

                if (changed)
                    r.sharedMaterials = ms;
            }
        }

        private static void Sc20SmoothTextures()
        {
            // the data sheet card (a Resources sprite): mip maps + trilinear + 8x aniso, or it shimmers when the head moves
            var sds = AssetImporter.GetAtPath("Assets/Resources/Sc20/Sc20_Sds.png") as TextureImporter;
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
        }
    }
}
#endif

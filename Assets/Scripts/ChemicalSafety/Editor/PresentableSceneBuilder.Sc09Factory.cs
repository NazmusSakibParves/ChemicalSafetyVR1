#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 9 on the controller ray: it plays in the Scenario 6/8 garment-factory room, so nothing of that room is rebuilt. This adds
    /// ONLY Scenario 9's own pieces to it: the emergency shower with its eye wash and floor drain (the designer's shower mesh taken out of the
    /// old Scenario 9 lab model) on the FRONT wall behind the learner (hidden outside Scenario 9, so Scenarios 6, 8 and the others never show
    /// it), the accident props (broken jar, shards, yellow acid puddles, drip) hidden until the accident, the acid visor, the "practice
    /// complete" card and the <see cref="Sc09AutoSplash"/> component that drives the existing table containers. Scenario 6's and 8's
    /// objects and scripts are not changed. <c>Sc09UseFactory = false</c> + Build Presentable Scene gives the old finger-touch Scenario 9
    /// lab back (nothing of it is deleted).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc09UseFactory = true;

        private sealed class Sc09RayRefs
        {
            public Sc09AutoSplash splash;
            public Sc09Shower shower;
            public Sc09Visor visor;
            public Sc06Gate gate;
            public Transform nearStand;
            public Transform showerStand;
            public GameObject showerMount;
        }

        // where the shower stands along the front wall (x relative to the room origin, to the learner's right of the table)
        private const float Sc09FacShowerX = -1.3f;

        private static Sc09RayRefs Sc09BuildRayStation(Transform room, Sc06Refs sc6, PresentableArt art)
        {
            var refs = new Sc09RayRefs();
            const float ox = Sc06Ox;
            const float wallZ = 3.4f; // inner face of the front wall (behind the learner at the start)
            var acidColor = new Color(0.62f, 0.56f, 0.10f, 0.92f);

            // ---- the table: top height and front edge (measured from the real table parts)
            var topY = 0.92f;
            var frontZ = 0.93f;
            var tableRoot = room.Find("TableRoot");
            if (tableRoot != null)
            {
                var tb = new Bounds();
                var has = false;
                foreach (Transform kid in tableRoot)
                {
                    if (kid.name != "TableTopA" && kid.name != "TableTopB")
                        continue;
                    var r = kid.GetComponent<Renderer>();
                    if (r == null)
                        continue;
                    if (!has)
                    {
                        tb = r.bounds;
                        has = true;
                    }
                    else
                    {
                        tb.Encapsulate(r.bounds);
                    }
                }

                if (has)
                {
                    topY = tb.max.y;
                    frontZ = tb.max.z;
                }
            }

            var so6 = new SerializedObject(sc6.auto);
            var jarT = (Transform)so6.FindProperty("jar").objectReferenceValue;
            var acidT = (Transform)so6.FindProperty("acid").objectReferenceValue;
            var jarPos = jarT.position;
            var acidPos = acidT.position;

            // ================================================================== the emergency shower (hidden outside Scenario 9)
            var mount = new GameObject("Sc09ShowerMount").transform;
            mount.SetParent(room, false);
            refs.showerMount = mount.gameObject;
            var lab = new Sc09Layout();
            var model = SpawnSc09("ChemLab02.glb", mount);
            MeshFilter showerMf = null;
            if (model != null)
            {
                model.transform.localScale = Vector3.one * Sc09ModelScale;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name.StartsWith("EmergencyShower"))
                        showerMf = mf;
            }

            if (showerMf != null)
            {
                var shower = showerMf.transform;
                shower.SetParent(mount, true); // keeps its world scale and orientation; the rest of the lab model goes away
                Object.DestroyImmediate(model);
                shower.name = "Sc09EmergencyShower";
                // the model faces +Z (the old lab); here the shower stands on the front wall and faces the room (-Z)
                var b0 = BoundsOfTree(shower);
                shower.RotateAround(b0.center, Vector3.up, 180f);
                var b1 = BoundsOfTree(shower);
                shower.position += new Vector3(ox + Sc09FacShowerX - b1.center.x, -b1.min.y, wallZ - 0.02f - b1.max.z);

                // measure the parts by their glb material names (before the materials are replaced)
                var mr = showerMf.GetComponent<MeshRenderer>();
                var poleX = ox + Sc09FacShowerX;
                if (Sc06SubBounds(showerMf, mr, "ShowerSign_MAT", out var signB))
                    poleX = signB.center.x;
                var headMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var headMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var vtx in showerMf.sharedMesh.vertices)
                {
                    var wp = showerMf.transform.TransformPoint(vtx);
                    if (wp.y < 2.15f || Mathf.Abs(wp.x - poleX) < 0.22f)
                        continue;
                    headMin = Vector3.Min(headMin, wp);
                    headMax = Vector3.Max(headMax, wp);
                }

                if (headMax.x > headMin.x)
                {
                    lab.showerHead = new Vector3((headMin.x + headMax.x) * 0.5f, headMin.y - 0.02f, (headMin.z + headMax.z) * 0.5f);
                    lab.headFound = true;
                }

                if (Sc06SubBounds(showerMf, mr, "HandSign_MAt", out var plateB))
                    lab.showerPlate = plateB.center;
                if (Sc06SubBounds(showerMf, mr, "Material", out var bowlB))
                    lab.bowlCentre = new Vector3(bowlB.center.x, bowlB.max.y, bowlB.center.z);
                var sb = BoundsOfTree(shower);
                if (lab.showerPlate == Vector3.zero)
                    lab.showerPlate = new Vector3(sb.center.x - 0.28f, 1.13f, sb.min.z + 0.2f);
                if (lab.bowlCentre == Vector3.zero)
                    lab.bowlCentre = new Vector3(sb.center.x, 1.19f, sb.min.z + 0.25f);
                if (!lab.headFound)
                    lab.showerHead = new Vector3(lab.bowlCentre.x, 2.40f, lab.bowlCentre.z - 0.05f);
                Debug.Log("[SC09 ray] shower bounds " + sb.min.ToString("F2") + " " + sb.max.ToString("F2") + " plate " + lab.showerPlate.ToString("F2") +
                          " bowl " + lab.bowlCentre.ToString("F2") + " head " + lab.showerHead.ToString("F2"));

                foreach (var r in shower.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (var i = 0; i < ms.Length; i++)
                        ms[i] = Sc09Remat(ms[i]);
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            else
            {
                if (model != null)
                    Object.DestroyImmediate(model);
                Debug.LogWarning("[SC09 ray] the shower mesh was not found in ChemLab02.glb: using a plain placeholder position");
                lab.showerPlate = new Vector3(ox + Sc09FacShowerX - 0.28f, 1.13f, wallZ - 0.8f);
                lab.bowlCentre = new Vector3(ox + Sc09FacShowerX, 1.19f, wallZ - 0.75f);
                lab.showerHead = new Vector3(ox + Sc09FacShowerX, 2.40f, wallZ - 0.7f);
            }

            // the stand mark and the learner's spot: 0.88 m in front of the bowl, looking at the shower (+Z)
            lab.showerStand = new Vector3(lab.bowlCentre.x, 0f, lab.bowlCentre.z - 0.88f);
            // the push plate, the drain, the water jets and the STAND HERE mark (absolute coordinates: ox = 0)
            Sc09Shower(mount, 0f, art, lab, new Sc09Refs(), -1f);
            // the plate is pressed automatically in this flow: no touch volume or glow (and no stray collider)
            foreach (var n in new[] { "Sc09PushPoke", "Sc09PushGlow" })
            {
                var t = mount.Find(n);
                if (t != null)
                    Object.DestroyImmediate(t.gameObject);
            }

            var sfx = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc09/";
            var visorGo = new GameObject("Sc09VisorRay");
            visorGo.transform.SetParent(room, false);
            refs.visor = visorGo.AddComponent<Sc09Visor>();
            SetRef(refs.visor, "shader", Shader.Find("ChemicalSafety/Sc09Visor"));
            var vso = new SerializedObject(refs.visor);
            vso.FindProperty("tint").colorValue = new Color(0.50f, 0.46f, 0.07f, 1f); // yellow acid, not the old purple sample
            vso.ApplyModifiedPropertiesWithoutUndo();

            var showerGo = new GameObject("Sc09ShowerLogic");
            showerGo.transform.SetParent(mount, false); // Sc09Shower finds the water jets and plate under its parent
            refs.shower = showerGo.AddComponent<Sc09Shower>();
            var sso = new SerializedObject(refs.shower);
            sso.FindProperty("visor").objectReferenceValue = refs.visor;
            sso.FindProperty("holoSprite").objectReferenceValue = art.holoSc09;
            sso.FindProperty("valveClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Valve.wav");
            sso.FindProperty("waterLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Water_Loop.wav");
            sso.ApplyModifiedPropertiesWithoutUndo();

            var showerStand = Marker("Stand_SC09_Shower", room, new Vector3(lab.showerStand.x, 0f, lab.showerStand.z), 0f);
            refs.showerStand = showerStand;
            refs.nearStand = Marker("Stand_SC09_Table", room, new Vector3((jarPos.x + acidPos.x) * 0.5f, 0f, frontZ + 0.62f), 180f);
            mount.gameObject.SetActive(false);

            // ================================================================== the accident props (hidden until the accident)
            var glass = Sc06GlassMat("Sc06FacJarGlass", new Color(0.88f, 0.94f, 0.97f), 0.12f, 0.9f);
            var jb = Sc06Bounds(jarT);
            var jarR = Mathf.Max(0.05f, Mathf.Min(jb.extents.x, jb.extents.z));
            var jarH = Mathf.Max(0.12f, jb.size.y);
            var broken = new GameObject("Sc09FacBroken").transform;
            broken.SetParent(room, false);
            broken.position = Vector3.zero;
            var baseMesh = Sc09JaggedShell("Sc09FacBrokenBase", new[]
            {
                new Vector2(0f, 0f), new Vector2(jarR * 0.97f, 0f), new Vector2(jarR, 0.010f), new Vector2(jarR * 0.99f, jarH * 0.22f),
                new Vector2(jarR * 0.96f, jarH * 0.40f)
            }, 24, 0.03f, true);
            Sc06Mesh("Sc09FacBrokenBase", broken, baseMesh, glass, Vector3.zero + Vector3.up * 0.002f, Quaternion.Euler(0f, 205f, 0f), Vector3.one);
            for (var i = 0; i < 12; i++)
            {
                var sz = i < 4 ? 0.034f : 0.020f;
                var m = Sc09ShardMesh("Sc09FacShardMesh" + i, 31 + i, sz);
                var a = i / 12f * Mathf.PI * 2f;
                Sc06Mesh("Sc09FacShard" + i, broken, m, glass, new Vector3(Mathf.Cos(a) * 0.05f, 0.02f + (i % 3) * 0.012f, Mathf.Sin(a) * 0.05f),
                    Quaternion.Euler(i * 31f, i * 57f, i * 13f), Vector3.one);
            }

            broken.gameObject.SetActive(false);

            var puddleMat = Mat("Sc09FacAcidPuddle", new Color(0.58f, 0.52f, 0.08f, 0.93f), 0.95f, 0f, Sc06Tex("Label_Sc09Puddle"));
            MakeTransparent(puddleMat);
            var splatMat = Mat("Sc09FacAcidSplat", new Color(0.55f, 0.49f, 0.07f, 0.95f), 0.92f, 0f, Sc06Tex("Label_Sc09Splat"));
            MakeTransparent(splatMat);
            var zNear = jarPos.z - 0.10f;
            var zFar = frontZ - 0.03f;
            var tableCentre = new Vector3(jarPos.x - 0.05f, topY + 0.0035f, (zNear + zFar) * 0.5f);
            var tableSize = new Vector2(0.80f, zFar - zNear);
            var pt = QuadObj("Sc09FacPuddleTable", room, tableCentre, new Vector3(90f, 0f, 0f), new Vector2(1f, 1f), puddleMat);
            Object.DestroyImmediate(pt.GetComponent<Collider>());
            pt.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            pt.SetActive(false);
            var floorCentre = new Vector3(jarPos.x - 0.05f, 0.016f, frontZ + 0.16f);
            var floorSize = new Vector2(0.90f, 0.66f);
            var pf = QuadObj("Sc09FacPuddleFloor", room, floorCentre, new Vector3(90f, 12f, 0f), new Vector2(1f, 1f), puddleMat);
            Object.DestroyImmediate(pf.GetComponent<Collider>());
            pf.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            pf.SetActive(false);

            var spills = new GameObject("Sc09FacSpills").transform;
            spills.SetParent(room, false);
            var rnd = new System.Random(17);
            for (var i = 0; i < 7; i++)
            {
                var size = 0.10f + (float)rnd.NextDouble() * 0.12f;
                var p = new Vector3(tableCentre.x + ((float)rnd.NextDouble() - 0.5f) * 0.9f, topY + 0.0028f + i * 0.0002f,
                    Mathf.Lerp(zNear + 0.05f, zFar - 0.05f, (float)rnd.NextDouble()));
                var sp = QuadObj("Sc09FacSplatTable" + i, spills, p, new Vector3(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector2(size, size), splatMat);
                Object.DestroyImmediate(sp.GetComponent<Collider>());
            }

            for (var i = 0; i < 6; i++)
            {
                var size = 0.12f + (float)rnd.NextDouble() * 0.16f;
                var p = new Vector3(floorCentre.x + ((float)rnd.NextDouble() - 0.5f) * 1.1f, 0.0185f + i * 0.0002f,
                    floorCentre.z + ((float)rnd.NextDouble() - 0.35f) * 0.7f);
                var sp = QuadObj("Sc09FacSplatFloor" + i, spills, p, new Vector3(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector2(size, size), splatMat);
                Object.DestroyImmediate(sp.GetComponent<Collider>());
            }

            spills.gameObject.SetActive(false);

            // acid drips off the front edge of the table onto the floor puddle
            var dripGo = new GameObject("Sc09FacDrip");
            dripGo.transform.SetParent(room, false);
            dripGo.transform.SetPositionAndRotation(new Vector3(floorCentre.x, topY - 0.01f, frontZ + 0.01f), Quaternion.Euler(90f, 0f, 0f));
            var dripPs = dripGo.AddComponent<ParticleSystem>();
            dripPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = dripPs.main;
            dm.loop = true;
            dm.playOnAwake = false;
            dm.duration = 2f;
            dm.startLifetime = 0.8f;
            dm.startSpeed = 0.05f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.018f);
            dm.startColor = acidColor;
            dm.gravityModifier = 1.0f;
            dm.maxParticles = 40;
            dm.simulationSpace = ParticleSystemSimulationSpace.World;
            var dem = dripPs.emission;
            dem.rateOverTime = 14f;
            var dsh = dripPs.shape;
            dsh.shapeType = ParticleSystemShapeType.Cone;
            dsh.angle = 4f;
            dsh.radius = 0.04f;
            var dr = dripGo.GetComponent<ParticleSystemRenderer>();
            dr.sharedMaterial = ParticleMat("Sc09AcidDrip", Sc06Tex("Fx_Drop"), false);
            dr.shadowCastingMode = ShadowCastingMode.Off;

            // ================================================================== the automatic accident (the SAME containers, anchors and liquid as Scenario 6/8)
            var host = new GameObject("Sc09Splash");
            host.transform.SetParent(room, false);
            refs.splash = host.AddComponent<Sc09AutoSplash>();
            var so = new SerializedObject(refs.splash);
            so.FindProperty("jar").objectReferenceValue = jarT;
            so.FindProperty("acid").objectReferenceValue = acidT;
            so.FindProperty("jarMouth").objectReferenceValue = so6.FindProperty("jarMouth").objectReferenceValue;
            so.FindProperty("acidSpout").objectReferenceValue = so6.FindProperty("acidSpout").objectReferenceValue;
            so.FindProperty("acidCap").objectReferenceValue = so6.FindProperty("acidCap").objectReferenceValue;
            so.FindProperty("liquid").objectReferenceValue = so6.FindProperty("liquid").objectReferenceValue;
            so.FindProperty("liquidRenderer").objectReferenceValue = so6.FindProperty("liquidRenderer").objectReferenceValue;
            so.FindProperty("liquidFullHeight").floatValue = so6.FindProperty("liquidFullHeight").floatValue;
            var topT = Sc06FindUnder(jarT, "JarLiquidTop");
            so.FindProperty("liquidTop").objectReferenceValue = topT != null ? topT.GetComponent<Renderer>() : null;
            so.FindProperty("broken").objectReferenceValue = broken;
            so.FindProperty("puddleTable").objectReferenceValue = pt.transform;
            so.FindProperty("puddleFloor").objectReferenceValue = pf.transform;
            so.FindProperty("spills").objectReferenceValue = spills;
            so.FindProperty("drip").objectReferenceValue = dripPs;
            so.FindProperty("puddleTableSize").vector2Value = tableSize;
            so.FindProperty("puddleFloorSize").vector2Value = floorSize;
            so.FindProperty("tableTopY").floatValue = topY;
            so.FindProperty("tableFrontZ").floatValue = frontZ;
            so.FindProperty("streamMaterial").objectReferenceValue = MatAlpha("Sc09AcidStream", null, new Color(0.62f, 0.56f, 0.10f, 0.9f), false);
            so.FindProperty("dropMaterial").objectReferenceValue = ParticleMat("Sc09AcidDrop", Sc06Tex("Fx_Drop"), false);
            so.FindProperty("knockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Flask_Knock.wav");
            so.FindProperty("breakClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Glass_Break.wav");
            so.FindProperty("splashClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Liquid_Splash.wav");
            so.FindProperty("visor").objectReferenceValue = refs.visor;
            so.FindProperty("acidColor").colorValue = acidColor;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================== "practice complete" card: NEXT = the question, PREVIEW = the whole animation again
            var gateGo = new GameObject("Sc09GateHostRay");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc09Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc09Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc09Content.RayGateBodyPrev;
            gso.FindProperty("backText").stringValue = "PREVIEW";
            gso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }
    }
}
#endif

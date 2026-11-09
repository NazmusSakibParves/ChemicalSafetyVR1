#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 10 on the controller ray: it plays in the Scenario 6/8/9 garment-factory room, so nothing of that room is rebuilt. This adds
    /// ONLY Scenario 10's own pieces to it, all under one hidden mount (<c>Sc10Mount</c>, switched on by the director only in Scenario 10):
    /// the red spill kit on the left wall next to the Scenario 8 PPE station (a smaller copy of the old kit, same items), the spill on the
    /// floor in front of the table (puddle, broken jar pieces and shards, soda ash / sock / pad / dustpan props), the old waste bin, the CAUTION
    /// sign, and the aftermath of the accident on the table (<see cref="Sc10Accident"/>: the acid can lying on the table, the acid running
    /// over the edge). The old Scenario 10 kit and spill logic (<see cref="Sc10Spill"/>) is reused through the same builder code as the old lab.
    /// <c>Sc10UseFactory = false</c> + Build Presentable Scene gives the old finger-touch Scenario 10 lab back (nothing of it is deleted).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc10UseFactory = true;

        private sealed class Sc10RayRefs
        {
            public GameObject mount;
            public Sc10Spill spill;
            public Sc10Accident accident;
            public Sc06Gate gate;
            public Transform kitStand;
            public Transform spillStand;
        }

        // the spill on the floor, in front of the table (x relative to the room origin)
        private const float Sc10FacSpillX = 0.13f; // also puts the can clear of the water jug
        private const float Sc10FacSpillZ = 1.15f;

        private static Sc10RayRefs Sc10BuildRayStation(Transform room, Sc06Refs sc6, PresentableArt art)
        {
            var refs = new Sc10RayRefs();
            const float ox = Sc06Ox;
            var x1 = ox + 3.7f;
            var acidColor = new Color(0.62f, 0.56f, 0.10f, 0.92f);

            var mount = new GameObject("Sc10Mount").transform;
            mount.SetParent(room, false);
            refs.mount = mount.gameObject;

            // ---- the table: top height and front edge (measured from the real table parts, as Scenario 9 does)
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
            var jb = Sc06Bounds(jarT);
            var jarR = Mathf.Max(0.05f, Mathf.Min(jb.extents.x, jb.extents.z));
            var jarH = Mathf.Max(0.12f, jb.size.y);

            // ================================================================== the spill bay: kit on the left wall, spill, bin, sign
            // the acid: a dull olive-yellow like the client's picture (the same puddle art as Scenario 9, darker), on the table and on the floor
            var acidPuddleMat = Mat("Sc10FacAcidPuddle", new Color(0.20f, 0.17f, 0.035f, 0.97f), 0.95f, 0f, Sc06Tex("Label_Sc09Puddle"));
            MakeTransparent(acidPuddleMat);
            var bayRefs = new Sc10Refs();
            var cfg = new Sc10BayCfg
            {
                kitShiftZ = 2.97f - 0.35f, // the kit is built at z 0.35: its centre goes to z 2.97 (the PPE board ends at z 2.55, the front wall is at 3.4)
                kitScale = 0.66f,
                spillC = new Vector3(ox + Sc10FacSpillX, 0f, Sc10FacSpillZ),
                bin = new Vector2(ox - 1.75f, 2.0f),
                sign = new Vector2(ox + 1.7f, 1.9f),
                drain = false,
                sds = false,
                stains = false,
                jarBroken = true,
                kitDetail = true,
                shards = 34,
                shardMin = 0.02f,
                shardMax = 0.07f,
                shardSpread = 0.62f,
                jarR = jarR,
                jarH = jarH,
                puddleTint = new Color(0.78f, 0.70f, 0.26f, 1f),
                // the puddle stays inside the sock ring (ellipse 1.9 x 1.24 m round the spill centre), a little smaller than it
                puddleSize = new Vector2(1.75f, 1.12f),
                puddleOffsetX = 0f,
                puddleMat = acidPuddleMat
            };
            Sc10Bay(mount, ox, x1, -2.9f, 3.4f, 3.2f, art, bayRefs, cfg);
            refs.spill = bayRefs.spill;
            // a soft light in front of the kit so the cabinet reads as a solid red box with depth (the room lights are dim there)
            var kitLight = new GameObject("Sc10KitLight");
            kitLight.transform.SetParent(mount, false);
            kitLight.transform.position = new Vector3(x1 - 0.9f, 2.0f, 2.97f);
            var kl = kitLight.AddComponent<Light>();
            kl.type = LightType.Point;
            kl.color = new Color(1f, 0.96f, 0.90f);
            kl.range = 2.6f;
            kl.intensity = 0.55f;
            kl.shadows = LightShadows.None;
            refs.kitStand = Marker("Stand_SC10_KitRay", mount, new Vector3(ox + 2.2f, 0f, 2.7f), 90f);
            // the learner watches the spill work from the front corner, turned towards it (further away = the whole floor spill is in view)
            var standPos = new Vector3(ox + 1.9f, 0f, 3.2f);
            // the bin's front (label, pedal) must face the learner (it was built facing -X, away from him)
            foreach (var tr in mount.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "Sc10Bin")
                    continue;
                var toLearner = standPos - tr.position;
                toLearner.y = 0f;
                toLearner.Normalize();
                // local -X must point at the learner: x' = -cos(a), z' = sin(a)
                tr.rotation = Quaternion.Euler(0f, Mathf.Atan2(toLearner.z, -toLearner.x) * Mathf.Rad2Deg, 0f);
            }

            var toSpill = new Vector3(ox + Sc10FacSpillX, 0f, Sc10FacSpillZ) - standPos;
            refs.spillStand = Marker("Stand_SC10_SpillRay", mount, standPos, Mathf.Atan2(toSpill.x, toSpill.z) * Mathf.Rad2Deg);

            // ================================================================== the aftermath on the table
            // on the steel table the acid must read as a dark wet smear (a glossy material only mirrors the lights there)
            var puddleMat = Mat("Sc10FacAcidTable", new Color(0.27f, 0.23f, 0.04f, 0.97f), 0.45f, 0f, Sc06Tex("Label_Sc09Puddle"));
            MakeTransparent(puddleMat);
            var splatMat = Mat("Sc10FacAcidSplat", new Color(0.27f, 0.23f, 0.04f, 0.96f), 0.45f, 0f, Sc06Tex("Label_Sc09Splat"));
            MakeTransparent(splatMat);
            var pt = QuadObj("Sc10TablePuddle", mount, new Vector3(acidT.position.x, topY + 0.0035f, frontZ - 0.3f), new Vector3(90f, 8f, 0f), new Vector2(1f, 1f), puddleMat);
            Object.DestroyImmediate(pt.GetComponent<Collider>());
            pt.transform.localScale = new Vector3(0.06f, 0.06f, 1f);
            pt.SetActive(false);

            var splats = new GameObject("Sc10TableSplats").transform;
            splats.SetParent(mount, false);
            var rnd = new System.Random(23);
            for (var i = 0; i < 26; i++)
            {
                // splashes and smears round the pool, all over the front half of the table top
                var size = 0.06f + (float)rnd.NextDouble() * 0.12f;
                var p = new Vector3(ox + Sc10FacSpillX + ((float)rnd.NextDouble() - 0.5f) * 1.8f, topY + 0.0028f + i * 0.0002f,
                    frontZ - 0.04f - (float)rnd.NextDouble() * 0.78f);
                var sp = QuadObj("Sc10TableSplat" + i, splats, p, new Vector3(90f, (float)rnd.NextDouble() * 360f, 0f), new Vector2(size, size), splatMat);
                Object.DestroyImmediate(sp.GetComponent<Collider>());
            }

            splats.gameObject.SetActive(false);

            // thin drops from the table edge onto the floor puddle (positioned and started by Sc10Accident)
            var dripGo = new GameObject("Sc10EdgeDrip");
            dripGo.transform.SetParent(mount, false);
            dripGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var dripPs = dripGo.AddComponent<ParticleSystem>();
            dripPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = dripPs.main;
            dm.loop = true;
            dm.playOnAwake = false;
            dm.duration = 2f;
            dm.startLifetime = 0.7f;
            dm.startSpeed = 0.04f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.014f);
            dm.startColor = acidColor;
            dm.gravityModifier = 1.0f;
            dm.maxParticles = 30;
            dm.simulationSpace = ParticleSystemSimulationSpace.World;
            var dem = dripPs.emission;
            dem.rateOverTime = 7f;
            var dsh = dripPs.shape;
            dsh.shapeType = ParticleSystemShapeType.Cone;
            dsh.angle = 3f;
            dsh.radius = 0.03f;
            var dr = dripGo.GetComponent<ParticleSystemRenderer>();
            dr.sharedMaterial = ParticleMat("Sc09AcidDrip", Sc06Tex("Fx_Drop"), false);
            dr.shadowCastingMode = ShadowCastingMode.Off;

            var accGo = new GameObject("Sc10Accident");
            accGo.transform.SetParent(mount, false);
            refs.accident = accGo.AddComponent<Sc10Accident>();
            var so = new SerializedObject(refs.accident);
            so.FindProperty("jar").objectReferenceValue = jarT;
            so.FindProperty("acid").objectReferenceValue = acidT;
            so.FindProperty("acidSpout").objectReferenceValue = so6.FindProperty("acidSpout").objectReferenceValue;
            so.FindProperty("acidCap").objectReferenceValue = so6.FindProperty("acidCap").objectReferenceValue;
            so.FindProperty("tablePuddle").objectReferenceValue = pt.transform;
            so.FindProperty("tableSplats").objectReferenceValue = splats;
            so.FindProperty("edgeDrip").objectReferenceValue = dripPs;
            so.FindProperty("runnelMaterial").objectReferenceValue = MatAlpha("Sc10AcidRunnel", null, new Color(0.30f, 0.26f, 0.05f, 0.9f), false);
            so.FindProperty("tableTopY").floatValue = topY;
            so.FindProperty("tableFrontZ").floatValue = frontZ;
            so.FindProperty("floorLanding").vector2Value = new Vector2(ox + Sc10FacSpillX, frontZ + 0.2f);
            so.FindProperty("mouthTargetX").floatValue = ox + Sc10FacSpillX; // the acid runs off the table right above the puddle
            so.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================== "inspection complete" card: NEXT = the question, PREVIEW = the whole animation again
            var gateGo = new GameObject("Sc10GateHostRay");
            gateGo.transform.SetParent(mount, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc10Content.RayGateTitle;
            gso.FindProperty("questionText").stringValue = Sc10Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc10Content.GateBody; // "... click NEXT to proceed, or BACK to review again" (BACK / NEXT as in the other scenarios)
            gso.ApplyModifiedPropertiesWithoutUndo();

            mount.gameObject.SetActive(false); // the shared room shows none of it outside Scenario 10
            return refs;
        }
    }
}
#endif

#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 8 on the controller ray: it plays in the Scenario 6 garment-factory room (the client's picture 8.jpeg shows that room), so
    /// nothing is rebuilt: this adds ONLY Scenario 8's own pieces to that room - the PPE station on the left wall (the old station, the
    /// same models), the glass rod on the table, the "dilution practice complete" card and the <see cref="Sc08AutoDilute"/> component that
    /// drives the existing table containers. Scenario 6's objects and scripts are not changed (the rod is hidden outside Scenario 8).
    /// <c>Sc08UseFactory = false</c> + Build Presentable Scene gives the previous Scenario 8 (old lab, finger touch) back; nothing of it
    /// is deleted. Later scenarios (9, 10 ...) can add their own pieces to the same room the same way.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc08UseFactory = true;

        private sealed class Sc08RayRefs
        {
            public Sc08AutoDilute dilute;
            public Sc08Ppe ppe;
            public Sc06Gate gate;
        }

        private static Sc08RayRefs Sc08BuildRayStation(Transform room, Sc06Refs sc6, PresentableArt art)
        {
            var refs = new Sc08RayRefs();
            const float ox = Sc06Ox;
            var wallX = ox + 3.7f; // inner face of the left wall (the learner looks along -Z, the left hand is +X)

            // ---- the PPE station on the LEFT wall: the old station built at the origin, then the whole mount turned to face the room
            var mount = new GameObject("Sc08PpeMount").transform;
            mount.SetParent(room, false);
            refs.ppe = Sc08BuildPpeStation(mount, 0f, art, out _);
            mount.SetPositionAndRotation(new Vector3(wallX, 0f, 2.0f), Quaternion.Euler(0f, 180f, 0f));
            // the check-box card opens in the air between the board and the table, turned toward the learner
            var anchor = new GameObject("Sc08PpePanelAnchor").transform;
            anchor.SetParent(room, false);
            anchor.position = new Vector3(ox + 2.35f, 1.5f, 1.0f);
            var pso = new SerializedObject(refs.ppe);
            pso.FindProperty("panelAnchor").objectReferenceValue = anchor;
            pso.FindProperty("panelScale").floatValue = 2.0f;
            pso.FindProperty("stationRoot").objectReferenceValue = mount.gameObject;
            pso.ApplyModifiedPropertiesWithoutUndo();

            mount.gameObject.SetActive(false); // Scenario 6 shares this room: the board only exists in Scenario 8
            // ---- the glass rod: lies on the table in front of the containers, hidden outside Scenario 8
            var rodGlass = Sc06GlassMat("Sc08RodGlassRay", new Color(0.86f, 0.94f, 0.97f), 0.1f, 0.9f);
            var rod = new GameObject("Sc08Rod").transform;
            rod.SetParent(room, false);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "RodShaft";
            Object.DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(rod, false);
            shaft.transform.localScale = new Vector3(0.015f, 0.15f, 0.015f);
            var smr = shaft.GetComponent<MeshRenderer>();
            smr.sharedMaterial = rodGlass;
            smr.shadowCastingMode = ShadowCastingMode.Off;
            var rodHome = new GameObject("Sc08RodHome").transform;
            rodHome.SetParent(room, false);
            rodHome.SetPositionAndRotation(new Vector3(ox - 0.1f + 0.22f, 0.92f + 0.010f, 0.5f + 0.27f), Quaternion.Euler(0f, 12f, 90f));
            rod.SetPositionAndRotation(rodHome.position, rodHome.rotation);
            rod.gameObject.SetActive(false);

            // ---- the demonstration component: the SAME containers, anchors, liquid and stream objects as Scenario 6's
            var so6 = new SerializedObject(sc6.auto);
            var host = new GameObject("Sc08Dilute");
            host.transform.SetParent(room, false);
            refs.dilute = host.AddComponent<Sc08AutoDilute>();
            var so = new SerializedObject(refs.dilute);
            foreach (var name in new[]
                     {
                         "jar", "water", "acid", "jarMouth", "waterSpout", "acidSpout", "jarSlot", "waterSlot", "acidSlot", "acidCap", "waterCap",
                         "liquid", "liquidRenderer", "stream", "streamRenderer", "splash", "fumes", "ambience"
                     })
                so.FindProperty(name).objectReferenceValue = so6.FindProperty(name).objectReferenceValue;
            so.FindProperty("liquidFullHeight").floatValue = so6.FindProperty("liquidFullHeight").floatValue;
            var jarT = (Transform)so6.FindProperty("jar").objectReferenceValue;
            var topT = jarT != null ? Sc06FindUnder(jarT, "JarLiquidTop") : null;
            so.FindProperty("liquidTop").objectReferenceValue = topT != null ? topT.GetComponent<Renderer>() : null;
            so.FindProperty("rod").objectReferenceValue = rod;
            so.FindProperty("rodShaft").objectReferenceValue = shaft.transform;
            so.FindProperty("rodHome").objectReferenceValue = rodHome;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- "dilution practice complete" card: NEXT = the question, PREVIEW = the whole animation again
            var gateGo = new GameObject("Sc08GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc08Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc08Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc08Content.GateBodyPrev;
            gso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }
    }
}
#endif

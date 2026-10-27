#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 9 room shell: the designer lab model, re-materialled, closed with our own walls, floor, ceiling and lights.</summary>
    public static partial class PresentableSceneBuilder
    {
        // Where things are in the room (x, z relative to the env origin; world y).
        private struct Sc09Layout
        {
            public Vector3 spawn;
            public float spawnYaw;
            public Vector3 bench;
            public float benchYaw;
            public Vector3 showerStand;
            public float showerYaw;
            public Vector3 flaskBase;     // world position of the middle flask (B-Sample 2)
            public float benchTop;
            public Vector3 showerPlate;   // world position of the eye wash push plate
            public Vector3 bowlCentre;
            public Vector3 showerHead;
            public bool headFound;
            public float backWallZ;
        }

        private const float Sc09ModelScale = 0.45f;
        private const float Sc09H = 3.02f;

        // Replaces the glb shader-graph materials (fully metallic, black without reflection probes) by URP Lit / glass ones.
        private static Material Sc09Remat(Material src)
        {
            var n = src != null ? src.name : "none";
            switch (n)
            {
                case "glass":
                case "glass_Mat":
                case "FLASKS-GLASS.003":
                    return Sc06GlassMat("Sc09Glass", new Color(0.84f, 0.93f, 0.96f), 0.07f, 0.75f);
                case "CABINET-DOOR-GLASS":
                    return Sc06GlassMat("Sc09DoorGlass", new Color(0.78f, 0.88f, 0.92f), 0.16f, 0.7f);
                case "AR3DMat PBR Realistic Mirror":
                    return Mat("Sc09Mirror", new Color(0.78f, 0.80f, 0.82f), 0.92f, 0.6f);
                case "Light red emission":
                    return Mat("Sc09SirenRed", new Color(0.6f, 0.03f, 0.02f), 0.6f, 0f, null, new Color(2.2f, 0.12f, 0.08f));
                case "Plastic black":
                    return Mat("Sc09PlasticBlack", new Color(0.05f, 0.05f, 0.055f), 0.5f, 0f);
                case "Wall":
                    return Sc09WallMat();
                case "TABLE-TOP":
                    return Mat("Sc09BenchTop", new Color(0.60f, 0.62f, 0.64f), 0.55f, 0.5f);
                case "Material.064":
                    return Mat("Sc09CabinetBody", new Color(0.20f, 0.22f, 0.25f), 0.45f, 0.2f);
                case "Stainless Steel":
                case "FAUCET":
                case "LAB-SINK":
                case "CABINET-DOOR-HANDLE":
                    return Mat("Sc09" + n.Replace(" ", "").Replace("-", ""), new Color(0.72f, 0.74f, 0.76f), 0.7f, 0.8f);
            }

            // everything else: keep the designer colour (and picture), but as ordinary lit paint / plastic
            var col = Color.white;
            Texture tex = null;
            if (src != null)
            {
                if (src.HasProperty("baseColorFactor"))
                    col = src.GetColor("baseColorFactor");
                if (src.HasProperty("baseColorTexture"))
                    tex = src.GetTexture("baseColorTexture");
            }

            if (tex != null)
                col = Color.white;
            col.a = 1f;
            return Mat("Sc09_" + n.Replace(" ", "").Replace("-", ""), col, 0.5f, 0.1f, tex);
        }

        private static Material Sc09WallMat() =>
            Sc06Pbr("Sc09WallPbr", "Wall", new Color(0.96f, 0.97f, 0.97f), new Vector2(3f, 1.3f), 0.8f, 0.6f);

        private static void Sc09Shell(Transform room, float ox, PresentableArt art, out Sc09Layout lab)
        {
            lab = new Sc09Layout();
            lab.backWallZ = -3.21f;
            var solidLayer = SolidLayer;
            var model = SpawnSc09("ChemLab02.glb", room);
            if (model != null)
            {
                model.name = "ChemLab02";
                model.transform.localScale = Vector3.one * Sc09ModelScale;
                var b = BoundsOfTree(model.transform);
                model.transform.position += new Vector3(ox - b.center.x, -b.min.y, 0f - b.center.z);
                // measure the shower parts before the materials are replaced (the sub-mesh is found by its glb material name)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null || mf.sharedMesh == null)
                        continue;
                    if (Sc06SubBounds(mf, mr, "Wall", out var wb))
                    {
                        Debug.Log("[SC09] wall part " + mf.name + " " + wb.min.ToString("F3") + " " + wb.max.ToString("F3"));
                        if (wb.size.z < 0.15f && wb.center.z < -2f)
                            lab.backWallZ = wb.max.z;
                    }
                    if (!mf.name.StartsWith("EmergencyShower"))
                        continue;
                    // the shower head hangs on an arm: the high vertices that are far from the pole
                    var headMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    var headMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    var poleX = 0f;
                    if (Sc06SubBounds(mf, mr, "ShowerSign_MAT", out var signB))
                        poleX = signB.center.x;
                    foreach (var vtx in mf.sharedMesh.vertices)
                    {
                        var wp = mf.transform.TransformPoint(vtx);
                        if (wp.y < 2.15f || Mathf.Abs(wp.x - poleX) < 0.22f)
                            continue;
                        headMin = Vector3.Min(headMin, wp);
                        headMax = Vector3.Max(headMax, wp);
                    }

                    if (headMax.x > headMin.x)
                    {
                        Debug.Log("[SC09] shower head " + headMin.ToString("F3") + " " + headMax.ToString("F3"));
                        lab.showerHead = new Vector3((headMin.x + headMax.x) * 0.5f, headMin.y - 0.02f, (headMin.z + headMax.z) * 0.5f);
                        lab.headFound = true;
                    }

                    foreach (var mn in new[] { "HandSign_MAt", "ShowerSign_MAT", "Material", "Stainless Steel" })
                    {
                        if (Sc06SubBounds(mf, mr, mn, out var sb))
                        {
                            Debug.Log("[SC09] shower part " + mn + " " + sb.min.ToString("F3") + " " + sb.max.ToString("F3"));
                            if (mn == "HandSign_MAt")
                                lab.showerPlate = sb.center;
                            else if (mn == "Material")
                                lab.bowlCentre = new Vector3(sb.center.x, sb.max.y, sb.center.z);
                            else if (mn == "ShowerSign_MAT" && !lab.headFound)
                                lab.showerHead = new Vector3(sb.center.x, 2.40f, 0f);
                        }
                    }
                }

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (var i = 0; i < ms.Length; i++)
                        ms[i] = Sc09Remat(ms[i]);
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }

                foreach (Transform t in model.transform)
                    Debug.Log("[SC09] node " + t.name + " " + BoundsOfTree(t).min.ToString("F2") + " " + BoundsOfTree(t).max.ToString("F2"));
            }

            const float x0 = -3.5f, x1 = 3.5f, zBack = -3.5f, zFront = 3.5f;
            var w = x1 - x0;
            var d = zFront - zBack;
            var floorMat = Sc06Pbr("Sc09FloorPbr", "Floor", new Color(0.50f, 0.52f, 0.54f), new Vector2(w / 2.4f, d / 2.4f), 0.85f, 0.6f);
            var wallMat = Sc09WallMat();
            var ceilMat = Sc06Pbr("Sc06CeilPbr", "Ceil", Color.white, new Vector2(w / 2.4f, d / 2.4f), 1f, 0.6f, false);
            var floor = Box("Sc09Floor", room, new Vector3(ox, 0.003f, 0f), new Vector3(w, 0.006f, d), floorMat);
            floor.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solidLayer >= 0)
                    go.layer = solidLayer;
            }

            Shell("Sc09WallFront", new Vector3(ox, Sc09H * 0.5f, zFront + 0.06f), new Vector3(w, Sc09H, 0.12f), wallMat);
            Shell("Sc09WallRight", new Vector3(ox + x0 - 0.06f, Sc09H * 0.5f, 0f), new Vector3(0.12f, Sc09H, d), wallMat);
            Box("Sc09Ceiling", room, new Vector3(ox, Sc09H + 0.05f, 0f), new Vector3(w, 0.1f, d), ceilMat, false);
            var trim = Mat("Sc08Skirting", new Color(0.40f, 0.42f, 0.42f), 0.3f, 0f);
            Box("Sc09SkirtRight", room, new Vector3(ox + x0 + 0.015f, 0.06f, 0f), new Vector3(0.03f, 0.12f, d), trim, false);
            Box("Sc09SkirtFront", room, new Vector3(ox, 0.06f, zFront - 0.015f), new Vector3(w, 0.12f, 0.03f), trim, false);

            // ceiling light panels with soft lights under them
            var panelMat = Mat("Sc06LightPanelGrid", new Color(1.04f, 1.04f, 1.02f), 0f, 0f, Sc06Tex("Label_LightGrid"), null, true);
            var housing = Mat("Sc06PanelFrame", new Color(0.82f, 0.83f, 0.82f), 0.3f, 0f);
            var k = 0;
            for (var ix = -1; ix <= 1; ix++)
            {
                for (var iz = -1; iz <= 1; iz++)
                {
                    var p = new Vector3(ox + ix * 2.2f, 0f, iz * 2.2f);
                    Box("LightFrame" + k, room, new Vector3(p.x, Sc09H - 0.012f, p.z), new Vector3(1.26f, 0.025f, 0.66f), housing, false);
                    Box("LightPanel" + k, room, new Vector3(p.x, Sc09H - 0.026f, p.z), new Vector3(1.16f, 0.01f, 0.58f), panelMat, false);
                    var lg = new GameObject("RoomLight" + k);
                    lg.transform.SetParent(room, false);
                    lg.transform.position = new Vector3(p.x, Sc09H - 0.5f, p.z);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.98f, 0.95f);
                    l.range = 6.5f;
                    l.intensity = 0.8f;
                    l.shadows = LightShadows.None;
                    k++;
                }
            }

            // the whiteboard (designer picture) on the back wall right of the shower, above the small bench
            var boardMat = Sc06Label("Sc09Board", "Label_Sc09Board", false, 0.4f);
            QuadObj("Sc09Whiteboard", room, new Vector3(ox - 2.1f, 1.78f, lab.backWallZ + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(1.7f, 0.93f), boardMat);

            lab.spawn = new Vector3(0f, 0f, 2.7f);
            lab.spawnYaw = 180f;
            // the island table in front of the left counter; the user stands on its -X side and looks along +X
            lab.benchTop = 0.92f;
            lab.flaskBase = new Vector3(ox + 1.45f, lab.benchTop, 0.20f);
            lab.bench = new Vector3(0.80f, 0f, 0.20f);
            lab.benchYaw = 90f;
            if (lab.showerPlate == Vector3.zero)
            {
                lab.showerPlate = new Vector3(ox + 0.70f, 1.13f, -2.77f);
                lab.bowlCentre = new Vector3(ox + 0.98f, 1.19f, -2.77f);
                lab.showerHead = new Vector3(ox + 0.98f, 2.40f, 0f);
            }

            if (!lab.headFound)
                lab.showerHead = new Vector3(lab.bowlCentre.x, lab.showerHead.y, lab.bowlCentre.z + 0.05f);
            lab.showerStand = new Vector3(lab.bowlCentre.x - ox, 0f, lab.bowlCentre.z + 0.88f);
            lab.showerYaw = 180f;

            // bilingual wall signs left of the shower (viewer's left = +X), like the designer picture
            QuadObj("Sc09SignEyewash", room, new Vector3(lab.bowlCentre.x - 0.72f, 1.88f, lab.backWallZ + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.80f, 0.40f),
                Sc06Label("Sc09SignEyewashMat", "Label_Sc09SignEyewash", true, 0.3f));
            QuadObj("Sc09SignShower", room, new Vector3(lab.bowlCentre.x - 0.72f, 1.42f, lab.backWallZ + 0.012f), new Vector3(0f, 180f, 0f), new Vector2(0.80f, 0.40f),
                Sc06Label("Sc09SignShowerMat", "Label_Sc09SignShower", true, 0.3f));
        }

        private static void Sc09Logic(Transform room, float ox, PresentableArt art, Sc09Refs refs)
        {
            var guideGo = new GameObject("Sc09Guide");
            guideGo.transform.SetParent(room, false);
            refs.guide = guideGo.AddComponent<Sc09Guide>();
            SetRef(refs.guide, "art", art);

            var ambGo = new GameObject("Sc09Ambience");
            ambGo.transform.SetParent(room, false);
            refs.ambience = ambGo.AddComponent<Sc06Ambience>();
            var audioDir = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc06/";
            var aso = new SerializedObject(refs.ambience);
            aso.FindProperty("roomLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(audioDir + "Room_Loop.wav");
            aso.ApplyModifiedPropertiesWithoutUndo();

            var visorGo = new GameObject("Sc09Visor");
            visorGo.transform.SetParent(room, false);
            refs.visor = visorGo.AddComponent<Sc09Visor>();
            SetRef(refs.visor, "shader", Shader.Find("ChemicalSafety/Sc09Visor"));

            var gateGo = new GameObject("Sc09GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc09Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc09Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc09Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var sfx = ArtRoot.Replace("Art/Presentable", "Art/Audio") + "/Sc09/";
            var flaskGo = new GameObject("Sc09FlaskLogic");
            flaskGo.transform.SetParent(room, false);
            refs.flask = flaskGo.AddComponent<Sc09Flask>();
            var fso = new SerializedObject(refs.flask);
            fso.FindProperty("streamMaterial").objectReferenceValue = MatAlpha("Sc09Stream", null, new Color(0.50f, 0.08f, 0.68f, 0.9f), false);
            fso.FindProperty("dropMaterial").objectReferenceValue = ParticleMat("Sc09Drop", Sc06Tex("Fx_Drop"), false);
            fso.FindProperty("knockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Flask_Knock.wav");
            fso.FindProperty("breakClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Glass_Break.wav");
            fso.FindProperty("splashClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Liquid_Splash.wav");
            fso.FindProperty("visor").objectReferenceValue = refs.visor;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var showerGo = new GameObject("Sc09ShowerLogic");
            showerGo.transform.SetParent(room, false);
            refs.shower = showerGo.AddComponent<Sc09Shower>();
            var sso = new SerializedObject(refs.shower);
            sso.FindProperty("visor").objectReferenceValue = refs.visor;
            sso.FindProperty("valveClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Valve.wav");
            sso.FindProperty("waterLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sfx + "Water_Loop.wav");
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 7 effects: the cabinet fire (flames, smoke, sparks, embers, light, soot) and the alarm beacon.</summary>
    public static partial class PresentableSceneBuilder
    {
        private static ParticleSystem Sc07Ps(string name, Transform parent, Vector3 at, Vector3 euler, Material mat, float sortFudge)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(euler);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = sortFudge;
            return ps;
        }

        private static void Sc07Sheet(ParticleSystem ps, int tiles)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = tiles;
            tsa.numTilesY = tiles;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.cycleCount = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        }

        private static void Sc07FadeCol(ParticleSystem ps, float inTime, float outStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inTime), new GradientAlphaKey(1f, outStart), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // The fire at the foot of the cabinet. 'at' = floor point in front of the cabinet, 'front' = the cabinet's front (+Z) plane.
        private static Sc07Fire Sc07BuildFire(Transform room, Vector3 at, float cabinetFrontZ, float cabinetWidth, PresentableArt art)
        {
            var fireSheet = ParticleMat("Sc07FireSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Generated/FX_FireSheet.png"), true);
            var smokeSheet = ParticleMat("Sc07SmokeSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Generated/FX_SmokeSheet.png"), false);
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var sparkMat = ParticleMat("Sc07Spark", glowTex, true);
            var puffMat = ParticleMat("Sc07SprayPuff", Sc05Tex("Steam_Puff"), false);

            var go = new GameObject("Fire");
            go.transform.SetParent(room, false);
            go.transform.position = at;
            var fire = go.AddComponent<Sc07Fire>();

            // the flame column: licks up the cabinet door and spreads over the floor in front
            var col = Sc07Ps("FlameColumn", go.transform, at + new Vector3(0f, 0.1f, 0f), new Vector3(-90f, 0f, 0f), fireSheet, 0f);
            var m = col.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            m.gravityModifier = -0.14f;
            m.maxParticles = 70;
            var e = col.emission;
            e.rateOverTime = 22f;
            var sh = col.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.28f;
            Sc07Sheet(col, 6);

            var wide = Sc07Ps("FlameBase", go.transform, at + new Vector3(0f, 0.15f, 0.05f), new Vector3(-90f, 0f, 0f), fireSheet, 0.1f);
            m = wide.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            m.gravityModifier = -0.2f;
            m.maxParticles = 60;
            e = wide.emission;
            e.rateOverTime = 26f;
            sh = wide.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(Mathf.Min(1.1f, cabinetWidth * 0.5f), 0.2f, 0.3f);
            sh.rotation = Vector3.zero;
            Sc07Sheet(wide, 6);

            // black smoke climbing the cabinet front and rolling along the ceiling
            var smoke = Sc07Ps("Smoke", go.transform, at + new Vector3(0f, 0.7f, -0.05f), new Vector3(-90f, 0f, 0f), smokeSheet, 0.2f);
            m = smoke.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.9f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.gravityModifier = -0.04f;
            m.startColor = new Color(0.06f, 0.06f, 0.065f, 0.88f);
            m.maxParticles = 60;
            e = smoke.emission;
            e.rateOverTime = 8f;
            sh = smoke.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 16f;
            sh.radius = 0.3f;
            Sc07Sheet(smoke, 6);
            Sc07FadeCol(smoke, 0.12f, 0.55f);

            var sparks = Sc07Ps("Sparks", go.transform, at + new Vector3(0f, 0.9f, 0.1f), new Vector3(-90f, 0f, 0f), sparkMat, 0.3f);
            m = sparks.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            m.startColor = new Color(1f, 0.82f, 0.45f, 1f);
            m.gravityModifier = 0.7f;
            m.maxParticles = 80;
            e = sparks.emission;
            e.rateOverTime = 16f;
            sh = sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 46f;
            sh.radius = 0.2f;
            Sc07FadeCol(sparks, 0.05f, 0.6f);

            var embers = Sc07Ps("Embers", go.transform, at + new Vector3(0f, 0.4f, 0f), new Vector3(-90f, 0f, 0f), sparkMat, 0.3f);
            m = embers.main;
            m.loop = true;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            m.startColor = new Color(1f, 0.5f, 0.15f, 1f);
            m.maxParticles = 50;
            e = embers.emission;
            e.rateOverTime = 9f;
            sh = embers.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 28f;
            sh.radius = 0.4f;
            var emNoise = embers.noise;
            emNoise.enabled = true;
            emNoise.strength = 0.5f;
            emNoise.frequency = 0.6f;
            Sc07FadeCol(embers, 0.1f, 0.5f);

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.position = at + new Vector3(0f, 0.8f, 0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.18f);
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            // soot on the cabinet front (black blot that darkens as the fire grows)
            var sootTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Generated/FX_Soot.png");
            var sootMat = MatAlpha("Sc07Soot", sootTex, new Color(1f, 1f, 1f, 0.85f), false);
            var soot = QuadObj("Soot", go.transform, new Vector3(at.x, 1.05f, cabinetFrontZ + 0.012f), new Vector3(0f, 180f, 0f),
                new Vector2(Mathf.Min(1.5f, cabinetWidth * 0.8f), 1.5f), sootMat);

            var fso = new SerializedObject(fire);
            var arr = fso.FindProperty("flames");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = col;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = wide;
            fso.FindProperty("smoke").objectReferenceValue = smoke;
            fso.FindProperty("sparks").objectReferenceValue = sparks;
            fso.FindProperty("embers").objectReferenceValue = embers;
            fso.FindProperty("fireLight").objectReferenceValue = light;
            fso.FindProperty("sprayMaterial").objectReferenceValue = puffMat;
            fso.FindProperty("scorch").objectReferenceValue = soot.GetComponent<Renderer>();
            fso.ApplyModifiedPropertiesWithoutUndo();
            return fire;
        }

        // The pulsing alarm beacon: lamp emission, a red wash light and a halo billboard.
        private static Sc07Beacon Sc07BuildBeacon(Transform room, Transform beaconModel, Vector3 at, PresentableArt art)
        {
            var go = new GameObject("BeaconFx");
            go.transform.SetParent(room, false);
            go.transform.position = at;
            var beacon = go.AddComponent<Sc07Beacon>();
            var lightGo = new GameObject("BeaconWash");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.position = at + new Vector3(0f, -0.1f, 0.7f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.1f, 0.06f);
            l.range = 7.5f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            var haloTex = art.radialGlow != null ? art.radialGlow.texture : null;
            var halo = QuadObj("BeaconHalo", go.transform, at + new Vector3(0f, 0f, 0.1f), new Vector3(0f, 180f, 0f), new Vector2(1.3f, 1.3f),
                MatAlpha("Sc07BeaconHalo", haloTex, new Color(1f, 0.12f, 0.08f, 0.75f), true));
            Renderer lamp = null;
            if (beaconModel != null)
            {
                foreach (var r in beaconModel.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterials.Length > 0 && r.sharedMaterial != null && r.sharedMaterial.name.Contains("emission"))
                        lamp = r;
                if (lamp == null)
                    lamp = beaconModel.GetComponentInChildren<Renderer>(true);
            }

            var bso = new SerializedObject(beacon);
            bso.FindProperty("lamp").objectReferenceValue = lamp;
            bso.FindProperty("washLight").objectReferenceValue = l;
            bso.FindProperty("halo").objectReferenceValue = halo.GetComponent<Renderer>();
            bso.ApplyModifiedPropertiesWithoutUndo();
            return beacon;
        }
    }
}
#endif

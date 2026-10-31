#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 11 props: the designer's cylinder rack module (4 cylinders + chained steel frame) three times = 12 cylinders, the leak FX.</summary>
    public static partial class PresentableSceneBuilder
    {
        private static ParticleSystem Sc11Gas(string name, Transform parent, Vector3 at, Vector3 dir, Color tint, Material mat, float size, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = tint;
            main.gravityModifier = -0.012f;
            main.maxParticles = 160;
            var em = ps.emission;
            em.rateOverTime = 24f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.012f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 2.6f)));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.5f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sortingFudge = 2f;
            ps.Play();
            return ps;
        }

        /// <summary>Crossed grass-blade quads (double sided, alpha clipped).</summary>
        private static void Sc11Tufts(Transform parent, int seed, int count, float x0, float x1, float z0, float z1, float sizeMin, float sizeMax)
        {
            var mat = Mat("Sc11TuftMat", Color.white, 0.05f, 0f, Sc06Tex("Label_Sc11Tuft"), null, false, true);
            mat.SetFloat("_Cull", 0f);
            var rng = new System.Random(seed);
            var holder = new GameObject("Tufts").transform;
            holder.SetParent(parent, false);
            for (var i = 0; i < count; i++)
            {
                var x = x0 + (float)rng.NextDouble() * (x1 - x0);
                var z = z0 + (float)rng.NextDouble() * (z1 - z0);
                if (Mathf.Abs(x - (Sc11Ox + Sc11GateX)) < 3.0f)
                    continue; // no grass on the walking road
                var sz = sizeMin + (float)rng.NextDouble() * (sizeMax - sizeMin);
                var yaw = (float)rng.NextDouble() * 180f;
                for (var k = 0; k < 2; k++)
                {
                    var q = QuadObj("Tuft", holder, new Vector3(x, sz * 0.5f, z), new Vector3(0f, yaw + k * 90f, 0f), new Vector2(sz, sz), mat);
                    q.transform.SetParent(holder, true);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private static GameObject Sc11Tree(Transform parent, Vector3 at, float height, int seed, Material trunk, Material leaf)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Tree").transform;
            root.SetParent(parent, true);
            root.position = at;
            Sc11Cyl("Trunk", root, at + Vector3.up * height * 0.3f, height * 0.035f, height * 0.6f, trunk);
            for (var i = 0; i < 5; i++)
            {
                var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sp.name = "Crown";
                sp.transform.SetParent(root, true);
                var r = height * (0.20f + (float)rng.NextDouble() * 0.10f);
                sp.transform.position = at + new Vector3(((float)rng.NextDouble() - 0.5f) * height * 0.38f, height * (0.55f + (float)rng.NextDouble() * 0.38f), ((float)rng.NextDouble() - 0.5f) * height * 0.38f);
                sp.transform.localScale = new Vector3(r * 2f, r * 1.7f, r * 2f);
                sp.GetComponent<MeshRenderer>().sharedMaterial = leaf;
                sp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                Object.DestroyImmediate(sp.GetComponent<Collider>());
            }

            return root.gameObject;
        }

        /// <summary>Tree line behind the fence, far low buildings and a water tower: the horizon of the workflow pictures.</summary>
        private static void Sc11Scenery(Transform room, float ox)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(room, false);
            Sc11PlantTrees(scenery, ox);

            // far industrial sheds (flat boxes with roofs), hazy because of the fog
            var wall = Mat("Sc11FarWall", new Color(0.55f, 0.56f, 0.55f), 0.15f);
            var roof = Mat("Sc11FarRoof", new Color(0.40f, 0.43f, 0.45f), 0.25f, 0.2f);
            var shed = new[]
            {
                new Vector4(-22f, -52f, 16f, 5f), new Vector4(-4f, -58f, 22f, 6f), new Vector4(18f, -50f, 14f, 5.5f),
                new Vector4(34f, -62f, 20f, 7f), new Vector4(-38f, -64f, 18f, 6f), new Vector4(2f, -74f, 30f, 8f),
            };
            for (var i = 0; i < shed.Length; i++)
            {
                var v = shed[i];
                Box("Shed" + i, scenery, new Vector3(ox + v.x, v.w * 0.5f, v.y), new Vector3(v.z, v.w, 9f), wall, false);
                Box("ShedRoof" + i, scenery, new Vector3(ox + v.x, v.w + 0.35f, v.y), new Vector3(v.z + 0.6f, 0.7f, 9.6f), roof, false);
            }

            Sc11Cyl("Chimney", scenery, new Vector3(ox + 14f, 9f, -75f), 0.9f, 18f, wall);
            Sc11Cyl("TowerLeg", scenery, new Vector3(ox - 14f, 5f, -68f), 0.5f, 10f, wall);
            var tank = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tank.name = "WaterTank";
            tank.transform.SetParent(scenery, true);
            tank.transform.position = new Vector3(ox - 14f, 11f, -68f);
            tank.transform.localScale = new Vector3(5f, 3.4f, 5f);
            tank.GetComponent<MeshRenderer>().sharedMaterial = wall;
            Object.DestroyImmediate(tank.GetComponent<Collider>());
        }

        private static void Sc11BuildRack(Transform root, float ox, PresentableArt art, out Transform valveAt, out Renderer[] frostRenderers,
            out ParticleSystem gas, out ParticleSystem purple)
        {
            valveAt = null;
            frostRenderers = new Renderer[0];
            gas = null;
            purple = null;
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            const float z = -3.30f;
            var xs = new[] { ox + 3.6f, ox + 2.67f, ox + 1.74f, ox + 0.81f, ox - 0.12f };
            for (var m = 0; m < xs.Length; m++)
            {
                var module = Sc11Spawn("GasCylinder.glb", root);
                if (module == null)
                    return;
                module.name = "RackModule_" + m;
                foreach (var t in module.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Cube.001")
                        Object.DestroyImmediate(t.gameObject); // the flat label plane of the designer's file
                module.transform.rotation = Quaternion.identity;
                var b = BoundsOfTree(module.transform);
                module.transform.position += new Vector3(xs[m] - b.center.x, -b.min.y, z - b.center.z);
                foreach (var r in module.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Material_Check"))
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        var src = r.sharedMaterial;
                        var blue = Mat("Sc11CylinderBlue", new Color(0.38f, 0.55f, 0.82f), 0.5f, 0.45f, src.GetTexture("_BaseMap"));
                        if (src.GetTexture("_BumpMap") != null)
                        {
                            blue.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                            blue.EnableKeyword("_NORMALMAP");
                        }

                        r.sharedMaterial = blue;
                    }
                }

                if (m == 4)
                {
                    // the leaking cylinder: its valve is the front-left one of the left module
                    Transform leak = null;
                    foreach (var t in module.GetComponentsInChildren<Transform>(true))
                        if (t.name == "SM_GasCylinder_02")
                            leak = t;
                    var lb = leak != null ? BoundsOfTree(leak) : b;
                    var top = new Vector3(lb.center.x, lb.max.y - 0.05f, lb.center.z);
                    valveAt = new GameObject("ValveAt").transform;
                    valveAt.SetParent(root, true);
                    valveAt.position = top;
                    var gasMat = ParticleMat("Sc11GasWhite", glowTex, false);
                    var purpleMat = ParticleMat("Sc11GasPurple", glowTex, false);
                    gas = Sc11Gas("LeakGas", root, top + new Vector3(-0.04f, 0.02f, 0.06f), new Vector3(-0.5f, 0.25f, 0.8f), new Color(0.92f, 0.95f, 1f, 0.42f), gasMat, 0.34f, 3.2f);
                    purple = Sc11Gas("LeakGasPurple", root, top + new Vector3(-0.02f, 0.02f, 0.05f), new Vector3(-0.35f, 0.4f, 0.7f), new Color(0.62f, 0.36f, 0.95f, 0.5f), purpleMat, 0.4f, 3.6f);
                    var em = purple.emission;
                    em.rateOverTime = 0f;
                    // frost crust around the valve: a few flattened white blobs
                    var frostMat = MatAlpha("Sc11Frost", glowTex, new Color(0.88f, 0.95f, 1f, 0.8f), false);
                    var list = new System.Collections.Generic.List<Renderer>();
                    for (var i = 0; i < 4; i++)
                    {
                        var f = QuadObj("Frost_" + i, root, top + new Vector3(((i % 2) - 0.5f) * 0.05f, -0.04f - i * 0.045f, 0.14f - i * 0.02f),
                            new Vector3(0f, 180f, 0f), new Vector2(0.16f - i * 0.02f, 0.12f), frostMat);
                        f.transform.SetParent(root, true);
                        Object.DestroyImmediate(f.GetComponent<Collider>());
                        list.Add(f.GetComponent<Renderer>());
                    }

                    frostRenderers = list.ToArray();
                }
            }

            // ground clutter around the rack: gravel patches and a puddle
            var puddle = MatAlpha("Sc11Puddle", glowTex, new Color(0.18f, 0.20f, 0.22f, 0.55f), false);
            var pq = QuadObj("Puddle", root, new Vector3(ox + 3.5f, 0.02f, -1.2f), new Vector3(90f, 0f, 0f), new Vector2(1.6f, 0.9f), puddle);
            pq.transform.SetParent(root, true);
            Object.DestroyImmediate(pq.GetComponent<Collider>());
        }
    }
}
#endif

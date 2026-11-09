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
            out ParticleSystem gas, out ParticleSystem purple, bool yard = false)
        {
            valveAt = null;
            frostRenderers = new Renderer[0];
            gas = null;
            purple = null;
            var glowTex = art.radialGlow != null ? art.radialGlow.texture : null;
            // yard = the chlorine yard of the controller-ray redo: grey / green cylinders against the concrete wall, the leak in module 1
            var z = yard ? -3.12f : -3.30f;
            var leakModule = yard ? 1 : 4;
            var xs = yard
                ? new[] { ox + 0.0f, ox - 0.93f, ox - 1.86f, ox - 2.79f, ox - 3.72f }
                : new[] { ox + 3.6f, ox + 2.67f, ox + 1.74f, ox + 0.81f, ox - 0.12f };
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
                        var blue = yard
                            ? (m % 2 == 0
                                ? Mat("Sc11CylinderGreen", new Color(0.30f, 0.46f, 0.35f), 0.38f, 0.4f, src.GetTexture("_BaseMap"))
                                : Mat("Sc11CylinderGrey", new Color(0.60f, 0.62f, 0.64f), 0.38f, 0.4f, src.GetTexture("_BaseMap")))
                            : Mat("Sc11CylinderBlue", new Color(0.38f, 0.55f, 0.82f), 0.5f, 0.45f, src.GetTexture("_BaseMap"));
                        if (src.GetTexture("_BumpMap") != null)
                        {
                            blue.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                            blue.EnableKeyword("_NORMALMAP");
                        }

                        r.sharedMaterial = blue;
                    }
                }

                if (yard && m <= 2)
                    Sc11ChlorineLabels(module.transform, root);

                if (m == leakModule)
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
                    if (yard)
                    {
                        // chlorine: pale yellow-green plume drifting to the left of the view (the wind sock points the same way), thicker after the alarm
                        gas = Sc11Gas("LeakGas", root, top + new Vector3(0.06f, 0.003f, 0.0f), new Vector3(1.0f, 0.10f, 0.14f), new Color(0.80f, 0.88f, 0.42f, 0.38f), gasMat, 0.30f, 4.2f);
                        purple = Sc11Gas("LeakGasThick", root, top + new Vector3(0.06f, 0.003f, 0.0f), new Vector3(1.0f, 0.14f, 0.16f), new Color(0.62f, 0.78f, 0.22f, 0.5f), purpleMat, 0.36f, 4.6f);
                    }
                    else
                    {
                        gas = Sc11Gas("LeakGas", root, top + new Vector3(-0.04f, 0.02f, 0.06f), new Vector3(-0.5f, 0.25f, 0.8f), new Color(0.92f, 0.95f, 1f, 0.42f), gasMat, 0.34f, 3.2f);
                        purple = Sc11Gas("LeakGasPurple", root, top + new Vector3(-0.02f, 0.02f, 0.05f), new Vector3(-0.35f, 0.4f, 0.7f), new Color(0.62f, 0.36f, 0.95f, 0.5f), purpleMat, 0.4f, 3.6f);
                    }

                    var em = purple.emission;
                    em.rateOverTime = 0f;
                    if (yard)
                    {
                        // cold white vapour right at the valve (the gas is freezing as it expands): small, soft, lifted a little by the draught
                        var mist = Sc11Gas("ColdMist", root, top + new Vector3(0.06f, 0.003f, 0.0f), new Vector3(1.0f, 0.18f, 0.12f), new Color(0.96f, 0.98f, 1f, 0.42f),
                            ParticleMat("Sc11ColdMist", glowTex, false), 0.16f, 2.0f);
                        var mem = mist.emission;
                        mem.rateOverTime = 14f;
                    }

                    // frost crust around the valve
                    var list = new System.Collections.Generic.List<Renderer>();
                    if (yard)
                    {
                        // ice crust on the valve and the cylinder shoulder: ragged white-blue patches (alpha clipped texture) on squashed spheres
                        var iceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11Yard/Yard_Frost.png");
                        var ice = Mat("Sc11FrostIce", Color.white, 0.2f, 0f, iceTex, new Color(0.10f, 0.12f, 0.15f), false, true);
                        // like the client's picture: a smooth white ice cap over the valve collars (rounded, opaque), a sparse skirt where it thins out on the
                        // shoulder, a clump of ice on the valve mouth and a brass nozzle there
                        var iceMouthTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11Yard/Yard_FrostMouth.png");
                        var iceMouth = Mat("Sc11FrostIceMouth", Color.white, 0.2f, 0f, iceMouthTex, new Color(0.10f, 0.12f, 0.15f), false, true);
                        var iceCap = Mat("Sc11FrostCap", new Color(0.93f, 0.96f, 1f), 0.12f, 0f, iceMouthTex, new Color(0.06f, 0.07f, 0.09f), false, false);
                        var brass = Mat("Sc11Brass", new Color(0.80f, 0.60f, 0.24f), 0.6f, 0.9f);
                        var nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        nozzle.name = "BrassNozzle";
                        nozzle.transform.SetParent(root, true);
                        nozzle.transform.position = top + new Vector3(0.028f, 0.003f, 0f);
                        nozzle.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                        nozzle.transform.localScale = new Vector3(0.038f, 0.034f, 0.038f); // cylinder: y is half the length
                        nozzle.GetComponent<MeshRenderer>().sharedMaterial = brass;
                        Object.DestroyImmediate(nozzle.GetComponent<Collider>());
                        foreach (var spec in new[]
                                 {
                                     new Vector4(0.000f, -0.040f, 0.114f, 0.108f), // the cap on the collar rings (opaque)
                                     new Vector4(0.052f, 0.003f, 0.050f, 0.040f), // the mouth of the valve
                                     new Vector4(0.000f, -0.100f, 0.172f, 0.062f) // the skirt: sparse specks
                                 })
                        {
                            var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sp.name = "FrostCrust";
                            sp.transform.SetParent(root, true);
                            sp.transform.position = top + new Vector3(spec.x, spec.y, 0f);
                            sp.transform.localScale = new Vector3(spec.z, spec.w, spec.z);
                            var rr = sp.GetComponent<MeshRenderer>();
                            rr.sharedMaterial = spec.w > 0.1f ? iceCap : spec.x > 0f ? iceMouth : ice;
                            rr.shadowCastingMode = ShadowCastingMode.Off;
                            Object.DestroyImmediate(sp.GetComponent<Collider>());
                            list.Add(rr);
                        }
                        list.Add(nozzle.GetComponent<MeshRenderer>());
                    }
                    else
                    {
                        var frostMat = MatAlpha("Sc11Frost", glowTex, new Color(0.88f, 0.95f, 1f, 0.8f), false);
                        for (var i = 0; i < 4; i++)
                        {
                            var f = QuadObj("Frost_" + i, root, top + new Vector3(((i % 2) - 0.5f) * 0.05f, -0.04f - i * 0.045f, 0.14f - i * 0.02f),
                                new Vector3(0f, 180f, 0f), new Vector2(0.16f - i * 0.02f, 0.12f), frostMat);
                            f.transform.SetParent(root, true);
                            Object.DestroyImmediate(f.GetComponent<Collider>());
                            list.Add(f.GetComponent<Renderer>());
                        }
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

        /// <summary>The white CHLORINE stencil with the skull and corrosion diamonds, wrapped on the front (+Z side) of every cylinder of a rack
        /// module: a curved strip that follows the real body radius (read from the mesh), so it sits ON the metal instead of floating in front.</summary>
        private static void Sc11ChlorineLabels(Transform module, Transform root)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc11Yard/Label_Chlorine.png");
            if (tex == null)
                return;
            var mat = Mat("Sc11ChlorineLabel", Color.white, 0.25f, 0f, tex, null, false, true);
            mat.SetFloat("_Cull", 0f);
            Mesh mesh = null;
            foreach (var t in module.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("SM_GasCylinder"))
                    continue;
                var b = BoundsOfTree(t);
                if (b.size.y < 0.5f)
                    continue;
                var yc = b.min.y + b.size.y * 0.50f;
                var c = new Vector2(b.center.x, b.center.z);
                if (mesh == null)
                    mesh = Sc11LabelMesh(Mathf.Max(b.size.x, b.size.z) * 0.5f + 0.002f); // the body radius = half the width of the (vertical) cylinder's bounds
                var go = new GameObject("ChlorineLabel");
                go.transform.SetParent(root, true);
                go.transform.position = new Vector3(c.x, yc, c.y);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>A strip of a cylinder (axis through the origin, centred on +Z, about 110 degrees) with uv 0..1; u runs to the viewer's right.</summary>
        private static Mesh Sc11LabelMesh(float radius)
        {
            var path = GenDir + "/Mesh_Sc11CylLabel.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null)
            {
                m = new Mesh();
                AssetDatabase.CreateAsset(m, path);
            }

            const int seg = 14;
            var arc = 1.9f; // radians
            var h = arc * radius * 1.25f;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new System.Collections.Generic.List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var th = (i / (float)seg - 0.5f) * arc;
                var x = Mathf.Sin(th) * radius;
                var z = Mathf.Cos(th) * radius;
                v[i * 2] = new Vector3(x, -h * 0.5f, z);
                v[i * 2 + 1] = new Vector3(x, h * 0.5f, z);
                var u = 0.5f - th / arc; // +X is the viewer's left (the viewer faces -Z)
                uv[i * 2] = new Vector2(u, 0f);
                uv[i * 2 + 1] = new Vector2(u, 1f);
                if (i < seg)
                    tri.AddRange(new[] { i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 1, i * 2 + 3, i * 2 + 2 });
            }

            m.Clear();
            m.vertices = v;
            m.uv = uv;
            m.triangles = tri.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif

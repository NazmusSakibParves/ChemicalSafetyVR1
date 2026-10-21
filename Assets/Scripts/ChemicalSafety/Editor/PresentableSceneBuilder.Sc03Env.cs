#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    // Scenario 3 realism helpers: PBR materials, the rusty rack mesh, the old barrel, cobwebs, grime, light shafts, dust.
    public static partial class PresentableSceneBuilder
    {
        private const string Sc03TexDir = ArtRoot + "/2D/Sc03Tex";

        private static Texture2D Sc03Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Sc03TexDir + "/" + name + ".png");

        // Normal maps need the normal-map importer, metal/smooth maps are linear, decals clamp, surfaces repeat.
        private static void ConfigureSc03Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc03TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var normal = file.EndsWith("_Normal");
                var linear = file.EndsWith("_MS");
                var decal = file.StartsWith("Web_") || file.StartsWith("Grime_") || file.StartsWith("Stain_") ||
                            file.StartsWith("Shaft");
                var wanted = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var changed = false;
                if (ti.textureType != wanted)
                {
                    ti.textureType = wanted;
                    changed = true;
                }

                if (!normal && ti.sRGBTexture == linear)
                {
                    ti.sRGBTexture = !linear;
                    changed = true;
                }

                var wrap = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 ||
                    ti.alphaIsTransparency != decal)
                {
                    ti.wrapMode = wrap;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = decal;
                    changed = true;
                }

                if (ti.anisoLevel < 4 && !decal)
                {
                    ti.anisoLevel = 4;
                    changed = true;
                }

                if (changed)
                    ti.SaveAndReimport();
            }
        }

        // URP Lit with albedo + normal + metal(R)/smooth(A) maps.
        private static Material PbrMat(string name, string albedo, string normal, string ms, Color tint, Vector2 tiling,
            float smoothScale = 1f, float bump = 1f)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;
            var path = GenDir + "/M_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }

            m.SetTexture("_BaseMap", Sc03Tex(albedo));
            m.SetColor("_BaseColor", tint);
            if (!string.IsNullOrEmpty(normal))
            {
                m.SetTexture("_BumpMap", Sc03Tex(normal));
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            if (!string.IsNullOrEmpty(ms))
            {
                m.SetTexture("_MetallicGlossMap", Sc03Tex(ms));
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", smoothScale);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureScale("_BumpMap", tiling);
            m.SetTextureScale("_MetallicGlossMap", tiling);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // Alpha-blended decal material, drawn from both sides (webs, drips).
        private static Material DecalMat(string name, string tex, Color tint, bool additive = false)
        {
            var m = MatAlpha(name, Sc03Tex(tex), tint, additive);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        // ---------------------------------------------------------------- meshes

        private static void AppendBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 centre,
            Vector3 size, Quaternion rot, float uvPerMetre)
        {
            var h = size * 0.5f;
            // each face: normal, u axis, v axis (local), all unit
            var faces = new[]
            {
                new[] { Vector3.right, Vector3.forward, Vector3.up },
                new[] { Vector3.left, Vector3.back, Vector3.up },
                new[] { Vector3.up, Vector3.right, Vector3.forward },
                new[] { Vector3.down, Vector3.left, Vector3.forward },
                new[] { Vector3.forward, Vector3.left, Vector3.up },
                new[] { Vector3.back, Vector3.right, Vector3.up }
            };
            foreach (var f in faces)
            {
                var nrm = f[0];
                var ua = f[1];
                var va = f[2];
                var c = Vector3.Scale(nrm, h);
                var du = Vector3.Scale(ua, h);
                var dv = Vector3.Scale(va, h);
                var b = v.Count;
                foreach (var corner in new[] { c - du - dv, c + du - dv, c + du + dv, c - du + dv })
                {
                    var wp = centre + rot * corner;
                    v.Add(wp);
                    n.Add(rot * nrm);
                    // texture follows the position so neighbouring parts line up
                    var lu = Vector3.Dot(wp, rot * ua);
                    var lv = Vector3.Dot(wp, rot * va);
                    uv.Add(new Vector2(lu, lv) * uvPerMetre);
                }

                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
        }

        // One steel rack bay (local space: length along x, depth along z, back at -z, base at y = 0).
        private static Mesh Sc03RackMesh(float[] decks, float height)
        {
            var path = GenDir + "/Mesh_Sc03Rack.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Sc03Rack" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            const float halfL = 1.27f;
            const float halfD = 0.32f;
            void Box(Vector3 c, Vector3 s, Quaternion? r = null) =>
                AppendBox(v, n, uv, t, c, s, r ?? Quaternion.identity, 1.1f);

            foreach (var sx in new[] { -halfL, halfL })
            {
                foreach (var sz in new[] { -halfD, halfD })
                {
                    Box(new Vector3(sx, height * 0.5f, sz), new Vector3(0.06f, height, 0.06f));
                    Box(new Vector3(sx + Mathf.Sign(sx) * -0.012f, 0.012f, sz), new Vector3(0.12f, 0.024f, 0.12f)); // foot plate
                }
            }

            foreach (var d in decks)
            {
                Box(new Vector3(0f, d - 0.018f, 0f), new Vector3(2.5f, 0.036f, 0.66f));             // deck sheet
                foreach (var sz in new[] { -halfD, halfD })
                    Box(new Vector3(0f, d - 0.09f, sz), new Vector3(2.54f, 0.12f, 0.035f));          // front / back beam
                foreach (var sx in new[] { -halfL, halfL })
                    Box(new Vector3(sx, d - 0.075f, 0f), new Vector3(0.035f, 0.09f, 0.62f));         // side rail
            }

            // top frame
            foreach (var sz in new[] { -halfD, halfD })
                Box(new Vector3(0f, height - 0.03f, sz), new Vector3(2.54f, 0.05f, 0.04f));
            foreach (var sx in new[] { -halfL, halfL })
                Box(new Vector3(sx, height - 0.03f, 0f), new Vector3(0.04f, 0.05f, 0.62f));

            // diagonal braces on the back plane between the decks and above the top one
            var levels = new List<float> { decks[0] };
            levels.AddRange(decks);
            levels.Add(height - 0.05f);
            for (var i = 0; i + 1 < levels.Count; i++)
            {
                if (levels[i + 1] - levels[i] < 0.3f)
                    continue;
                var a = new Vector3(-halfL, levels[i] + 0.1f, -halfD);
                var b = new Vector3(halfL, levels[i + 1] - 0.1f, -halfD);
                if (i % 2 == 1)
                {
                    a.x = halfL;
                    b.x = -halfL;
                }

                var dir = b - a;
                Box((a + b) * 0.5f, new Vector3(0.028f, dir.magnitude, 0.028f),
                    Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }

            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // The designer's low-poly rusty barrel (240 triangles, from Old drams.glb), baked upright to 0.9 m with its
        // base centre at the origin so it can be instanced freely.
        private static Mesh Sc03BarrelMesh()
        {
            var path = GenDir + "/Mesh_Sc03Barrel.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && existing.vertexCount > 0)
                return existing;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc03OldDrums.glb");
            if (asset == null)
                return RealisticDrumMesh();
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            MeshFilter barrel = null;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name.Contains("Barrels"))
                    barrel = mf;
            }

            if (barrel == null)
            {
                Object.DestroyImmediate(inst);
                Debug.LogWarning("[Presentable] SC03 barrel mesh not found, using the procedural drum.");
                return RealisticDrumMesh();
            }

            var src = barrel.sharedMesh;
            var sv = src.vertices;
            var sn = src.normals;
            var st = src.tangents;
            var world = new Vector3[sv.Length];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var i = 0; i < sv.Length; i++)
            {
                world[i] = barrel.transform.TransformPoint(sv[i]);
                min = Vector3.Min(min, world[i]);
                max = Vector3.Max(max, world[i]);
            }

            var k = 0.9f / (max.y - min.y);
            var origin = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
            var verts = new Vector3[sv.Length];
            var norms = new Vector3[sv.Length];
            var tans = new Vector4[sv.Length];
            for (var i = 0; i < sv.Length; i++)
            {
                verts[i] = (world[i] - origin) * k;
                norms[i] = sn.Length > i ? barrel.transform.TransformDirection(sn[i]).normalized : Vector3.up;
                if (st.Length > i)
                {
                    var td = barrel.transform.TransformDirection(new Vector3(st[i].x, st[i].y, st[i].z)).normalized;
                    tans[i] = new Vector4(td.x, td.y, td.z, st[i].w);
                }
            }

            var mesh = new Mesh { name = "Sc03Barrel", indexFormat = src.indexFormat };
            mesh.vertices = verts;
            mesh.normals = norms;
            if (st.Length == sv.Length)
                mesh.tangents = tans;
            mesh.uv = src.uv;
            mesh.subMeshCount = src.subMeshCount;
            for (var si = 0; si < src.subMeshCount; si++)
                mesh.SetTriangles(src.GetTriangles(si), si);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            Object.DestroyImmediate(inst);
            return mesh;
        }

        // ---------------------------------------------------------------- decals

        // Quad placed on a surface. normal = direction the visible face points; up = image up on the surface.
        private static GameObject DecalQuad(string name, Transform parent, Vector3 centre, Vector3 normal, Vector3 up,
            Vector2 size, Material mat, bool flipX = false)
        {
            var q = QuadObj(name, parent, Vector3.zero, Vector3.zero, size, mat);
            q.transform.position = centre;
            q.transform.rotation = Quaternion.LookRotation(-normal, up);
            if (flipX)
                q.transform.localScale = new Vector3(-size.x, size.y, 1f);
            return q;
        }

        // Cobweb in a corner: the texture's top-left is the anchor, the web spreads along 'right' and down.
        private static void WebCorner(Transform parent, Vector3 anchor, Vector3 normal, Vector3 right, float size,
            Material mat)
        {
            var up = Vector3.up;
            var facing = Quaternion.LookRotation(-normal, up);
            var imageRight = facing * Vector3.right;
            var flip = Vector3.Dot(imageRight, right) < 0f;
            var centre = anchor + right.normalized * (size * 0.5f) + Vector3.down * (size * 0.5f) + normal * 0.012f;
            DecalQuad("Web", parent, centre, normal, up, new Vector2(size, size), mat, flip);
        }

        // Soft light shaft under a tube light (two crossed quads, additive).
        private static void LightShaft(Transform parent, Vector3 top, float length, float width, Material mat)
        {
            for (var i = 0; i < 2; i++)
            {
                var q = QuadObj("Shaft", parent, Vector3.zero, Vector3.zero, new Vector2(width, length), mat);
                q.transform.position = top + Vector3.down * (length * 0.5f);
                q.transform.rotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
        }

        // Slow floating dust in the room volume.
        private static void BuildDust(Transform parent, Vector3 centre, Vector3 size, Material mat)
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }
    }
}
#endif

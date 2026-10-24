#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>Scenario 6 helpers: PBR materials, texture import, the solvent jug, bench glassware and hood apparatus.</summary>
    public static partial class PresentableSceneBuilder
    {
        private const string Sc06TexDir = ArtRoot + "/2D/Sc06Tex";

        private static Texture2D Sc06Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Sc06TexDir + "/" + name + ".png");

        private static void ConfigureSc06Textures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc06TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var normal = file.EndsWith("_Normal");
                var linear = file.EndsWith("_MS");
                var decal = file.StartsWith("Fx_") || file.StartsWith("Label_") || file.StartsWith("Poster_");
                var changed = false;
                var wanted = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
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
                if (ti.wrapMode != wrap || !ti.mipmapEnabled || ti.maxTextureSize != 1024 || ti.alphaIsTransparency != decal)
                {
                    ti.wrapMode = wrap;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 1024;
                    ti.alphaIsTransparency = decal;
                    changed = true;
                }

                if (!decal && ti.anisoLevel < 4)
                {
                    ti.anisoLevel = 4;
                    changed = true;
                }

                if (changed)
                    ti.SaveAndReimport();
            }
        }

        private static Material Sc06Pbr(string name, string set, Color tint, Vector2 tiling, float smoothScale, float bump,
            bool hasMs = true)
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

            m.SetTexture("_BaseMap", Sc06Tex(set + "_Albedo"));
            m.SetColor("_BaseColor", tint);
            if (Sc06Tex(set + "_Normal") != null)
            {
                m.SetTexture("_BumpMap", Sc06Tex(set + "_Normal"));
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            if (hasMs && Sc06Tex(set + "_MS") != null)
            {
                m.SetTexture("_MetallicGlossMap", Sc06Tex(set + "_MS"));
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", smoothScale);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                m.SetFloat("_Smoothness", 0.08f);
                m.SetFloat("_Metallic", 0f);
            }

            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureScale("_BumpMap", tiling);
            m.SetTextureScale("_MetallicGlossMap", tiling);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        // A flat lit decal/label material (alpha-clipped when it has transparent edges).
        private static Material Sc06Label(string name, string tex, bool clip = false, float smooth = 0.25f)
        {
            return Mat(name, Color.white, smooth, 0f, Sc06Tex(tex), null, false, clip);
        }

        private static GameObject SpawnSc06(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc06/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc06 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        private static Mesh Sc06Obj(string name) => LoadObjMesh(ArtRoot + "/3D/Sc06/" + name + ".obj");

        // Mesh part at a bench position: base centre at 'baseAt', scaled uniformly to 'height'.
        private static GameObject Sc06Part(string name, Transform parent, Mesh mesh, Vector3 baseAt, float height, Material mat,
            float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var b = mesh.bounds;
            var s = height / Mathf.Max(0.001f, b.size.y);
            go.transform.localScale = Vector3.one * s;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // the meshes are authored with their base at y = 0 and centred on x/z of the bottle body
            go.transform.position = baseAt - go.transform.rotation * new Vector3(0f, b.min.y * s, 0f);
            return go;
        }

        private static GameObject Sc06Disc(string name, Transform room, Vector3 pos, float diameter, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(room, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(diameter, height, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material Sc06Amber() => Mat("Sc06Amber", new Color(0.30f, 0.14f, 0.04f, 1f), 0.82f, 0.05f);

        private static Material Sc06Plastic(string n, Color c) => Mat(n, c, 0.55f, 0f);

        // World-space bounds of the sub-meshes of a merged glTF mesh whose material name matches.
        private static bool Sc06SubBounds(MeshFilter mf, MeshRenderer mr, string matName, out Bounds bounds)
        {
            bounds = new Bounds();
            var mats = mr.sharedMaterials;
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var has = false;
            for (var i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].name != matName)
                    continue;
                foreach (var idx in mesh.GetTriangles(i))
                {
                    var wp = mf.transform.TransformPoint(verts[idx]);
                    if (!has)
                    {
                        bounds = new Bounds(wp, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(wp);
                    }
                }
            }

            return has;
        }

        // Copy of the mesh without the sub-meshes of the named materials (saved as an asset).
        private static void Sc06DropSubmeshes(MeshFilter mf, MeshRenderer mr, string assetName, params string[] drop)
        {
            var mats = mr.sharedMaterials;
            var src = mf.sharedMesh;
            var keep = new List<int>();
            var keepMats = new List<Material>();
            for (var i = 0; i < src.subMeshCount && i < mats.Length; i++)
            {
                if (mats[i] != null && System.Array.IndexOf(drop, mats[i].name) >= 0)
                    continue;
                keep.Add(i);
                keepMats.Add(mats[i]);
            }

            var path = GenDir + "/" + assetName + ".asset";
            var dst = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (dst == null)
            {
                dst = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(dst, path);
            }

            dst.Clear();
            dst.indexFormat = src.indexFormat;
            dst.vertices = src.vertices;
            dst.normals = src.normals;
            dst.uv = src.uv;
            dst.tangents = src.tangents;
            dst.subMeshCount = keep.Count;
            for (var i = 0; i < keep.Count; i++)
                dst.SetTriangles(src.GetTriangles(keep[i]), i);
            dst.RecalculateBounds();
            EditorUtility.SetDirty(dst);
            mf.sharedMesh = dst;
            mr.sharedMaterials = keepMats.ToArray();
        }

        // ---------------------------------------------------------------- the 5 L Volatile Solvent jerrycan (the designer's model)

        // Size, mouth and cap of the converted model (Tools/prepare_sc06_chem.py, Chem/manifest.txt).
        public const float JugW = 0.205f, JugD = 0.107f, JugH = 0.30f;
        private const float JugMouthX = -0.0704f, JugCapBottom = 0.2641f;

        private static Mesh Sc06ChemMesh(string name)
        {
            var m = LoadObjMesh(ArtRoot + "/3D/Sc06/Chem/" + name + ".obj");
            if (m == null)
                Debug.LogError("[SC06] missing mesh " + name + " (run Tools/prepare_sc06_chem.py)");
            return m;
        }

        // Returns the jug root (pivot at the base centre). 'mouth' is the open mouth, 'cap' the red cap (pivot at its bottom centre).
        private static GameObject Sc06BuildJug(Transform parent, Vector3 baseAt, PresentableArt art, out Transform mouth, out GameObject cap)
        {
            var root = new GameObject("SolventJug").transform;
            root.SetParent(parent, true);
            root.position = baseAt;
            var white = Mat("Sc06Hdpe", new Color(0.95f, 0.95f, 0.93f), 0.6f, 0f);
            var red = Mat("Sc06RedCap", new Color(0.72f, 0.07f, 0.08f), 0.6f, 0f);
            var dark = Mat("Sc06Mouth", new Color(0.04f, 0.04f, 0.045f), 0.2f, 0f);
            var body = new GameObject("JugBody");
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = Sc06ChemMesh("Sc06_Jerry");
            var br = body.AddComponent<MeshRenderer>();
            br.sharedMaterial = white;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
            // open neck: a short rim and the dark opening
            var rim = Cyl("NeckRim", root, root.position + new Vector3(JugMouthX, JugCapBottom - 0.002f, 0f), 0.014f, 0.0215f, white);
            rim.transform.SetParent(root, true);
            Sc06Disc("MouthHole", root, root.position + new Vector3(JugMouthX, JugCapBottom + 0.0125f, 0f), 0.036f, 0.0006f, dark)
                .transform.SetParent(root, true);
            mouth = new GameObject("Mouth").transform;
            mouth.SetParent(root, true);
            mouth.position = root.position + new Vector3(JugMouthX, JugCapBottom + 0.016f, 0f);
            // label on the front (+Z)
            var labelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc06/SolventLabel.png");
            var q = QuadObj("JugLabel", root, new Vector3(0f, JugH * 0.46f, JugD * 0.5f + 0.0012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.122f, 0.093f), Mat("Sc06SolventLabel", Color.white, 0.3f, 0f, labelTex));
            q.transform.SetParent(root, false);
            q.transform.localPosition = new Vector3(0f, JugH * 0.46f, JugD * 0.5f + 0.0012f);
            // the big "5L" printed on the jug below the label, as in the designer's picture
            var five = QuadObj("Jug5L", root, new Vector3(0.03f, JugH * 0.17f, JugD * 0.5f + 0.0012f), new Vector3(0f, 180f, 0f),
                new Vector2(0.085f, 0.053f), Mat("Sc06Jug5L", Color.white, 0.3f, 0f, Sc06Tex("Label_5L"), null, false, true));
            five.transform.SetParent(root, false);
            five.transform.localPosition = new Vector3(0.035f, JugH * 0.17f, JugD * 0.5f + 0.0012f);
            Object.DestroyImmediate(five.GetComponent<Collider>());
            // the cap, loose (pivot at its bottom centre so it can sit on the table or screw on the neck)
            cap = new GameObject("JugCap");
            cap.transform.SetParent(parent, true);
            cap.transform.position = baseAt;
            var capMesh = new GameObject("CapMesh");
            capMesh.transform.SetParent(cap.transform, false);
            capMesh.transform.localPosition = new Vector3(-JugMouthX, -JugCapBottom, 0f);
            capMesh.AddComponent<MeshFilter>().sharedMesh = Sc06ChemMesh("Sc06_JerryCap");
            var cr = capMesh.AddComponent<MeshRenderer>();
            cr.sharedMaterial = red;
            cr.shadowCastingMode = ShadowCastingMode.Off;
            cr.receiveShadows = false;
            return root.gameObject;
        }

        // ---------------------------------------------------------------- the designer's bottles (every part with its own material)

        private static Material Sc06ChemMat(string role)
        {
            switch (role)
            {
                case "glass": return Sc06GlassMat("Sc06ChemGlass", new Color(0.84f, 0.93f, 0.95f), 0.06f, 0.7f);
                case "glassdark": return Sc06GlassMat("Sc06ChemGlassDark", new Color(0.30f, 0.17f, 0.08f), 0.5f, 0.85f);
                case "liquid": return Sc06Liquid("Sc06ChemLiquid", new Color(0.86f, 0.93f, 0.97f, 0.34f));
                case "cap": return Mat("Sc06ChemCap", new Color(0.72f, 0.72f, 0.7f), 0.5f, 0f);
                case "capwhite": return Mat("Sc06ChemCapWhite", new Color(0.92f, 0.92f, 0.9f), 0.5f, 0f);
                case "capblue": return Mat("Sc06ChemCapBlue", new Color(0.03f, 0.17f, 0.46f), 0.55f, 0f);
                case "capblack": return Mat("Sc06ChemCapBlack", new Color(0.04f, 0.04f, 0.045f), 0.5f, 0f);
                case "plastic": return Mat("Sc06ChemPlastic", new Color(0.93f, 0.93f, 0.91f), 0.5f, 0f);
                default: return Mat("Sc06ChemLabel", new Color(0.94f, 0.94f, 0.9f), 0.25f, 0f); // "label"
            }
        }

        // Builds one of the designer's bottles at 'baseAt' (base centre). 'labelTex' (optional) is laid on the front of the label band.
        private static GameObject Sc06ChemGroup(Transform parent, string group, string[] roles, Vector3 baseAt, float yaw, string labelTex = null)
        {
            var root = new GameObject("Chem_" + group).transform;
            root.SetParent(parent, true);
            root.position = baseAt;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var role in roles)
            {
                var mesh = Sc06ChemMesh("Sc06C_" + group + "_" + role);
                if (mesh == null)
                    continue;
                var go = new GameObject(role);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Sc06ChemMat(role);
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (role == "label" && !string.IsNullOrEmpty(labelTex))
                {
                    var b = mesh.bounds;
                    var q = QuadObj(group + "LabelArt", root, new Vector3(b.center.x, b.center.y, b.max.z + 0.0012f), new Vector3(0f, 180f, 0f),
                        new Vector2(b.size.x * 0.78f, b.size.y * 0.9f), Sc06Label("Sc06BL_" + labelTex, labelTex, false, 0.3f));
                    q.transform.SetParent(root, false);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                }
            }

            return root.gameObject;
        }

        // ---------------------------------------------------------------- glassware

        private static void Sc06Beaker(Transform parent, string name, Vector3 baseAt, float height, bool liquid, Mesh mesh, Material glass)
        {
            var g = Sc06Part(name, parent, mesh, baseAt, height, glass);
            if (liquid)
            {
                var lq = Cyl(name + "Liquid", parent, baseAt + Vector3.up * 0.004f, height * 0.4f, height * 0.26f,
                    Mat("Sc06Liquid", new Color(0.78f, 0.88f, 0.92f, 0.5f), 0.9f, 0f));
                lq.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                MakeTransparent(lq.GetComponent<MeshRenderer>().sharedMaterial);
            }
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        private static GameObject Sc06Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = (a + b) * 0.5f;
            go.transform.up = (b - a).normalized;
            go.transform.localScale = new Vector3(radius * 2f, (b - a).magnitude * 0.5f, radius * 2f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static GameObject Sc06Ball(string name, Transform parent, Vector3 centre, float radius, Material mat)
        {
            var go = Prim(name, PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * radius * 2f, mat);
            go.transform.position = centre;
            return go;
        }

        // The distillation rig of the designer's hood interior (round flasks, condenser, clamps, stands, tubing, beaker).
        private static void Sc06BuildApparatus(Transform parent, float xCentre, float surfaceY, float backZ, float depth)
        {
            var steel = Mat("Sc06StandSteel", new Color(0.70f, 0.72f, 0.74f), 0.75f, 0.1f);
            var clampMat = Mat("Sc06Clamp", new Color(0.10f, 0.10f, 0.11f), 0.4f, 0f);
            var glass = Sc06GlassMat("Sc06HoodGlass", new Color(0.82f, 0.92f, 0.96f), 0.07f, 0.8f);
            var liquid = Sc06Liquid("Sc06FlaskLiquid", new Color(0.9f, 0.95f, 0.97f, 0.3f));
            var rubber = Mat("Sc06Rubber", new Color(0.05f, 0.05f, 0.055f), 0.15f, 0f);
            var z0 = backZ + depth * 0.34f;
            Box("RubberMat0", parent, new Vector3(xCentre + 0.5f, surfaceY + 0.006f, z0 + 0.05f), new Vector3(0.26f, 0.012f, 0.26f), rubber, false);
            Box("RubberMat1", parent, new Vector3(xCentre + 0.1f, surfaceY + 0.006f, z0 + 0.07f), new Vector3(0.26f, 0.012f, 0.26f), rubber, false);
            Box("RubberMat2", parent, new Vector3(xCentre - 0.32f, surfaceY + 0.006f, z0 + 0.1f), new Vector3(0.24f, 0.012f, 0.24f), rubber, false);
            var stands = new[] { xCentre + 0.5f, xCentre + 0.1f, xCentre - 0.32f };
            for (var i = 0; i < stands.Length; i++)
            {
                var sx = stands[i];
                Box("StandBase" + i, parent, new Vector3(sx, surfaceY + 0.008f, z0 - 0.1f), new Vector3(0.18f, 0.016f, 0.12f), clampMat, false);
                Cyl("StandRod" + i, parent, new Vector3(sx, surfaceY + 0.016f, z0 - 0.12f), 0.72f, 0.006f, steel);
            }

            var flask = Sc06RoundFlaskMesh();
            var fx = stands[0];
            Sc06Mesh("FlaskA", parent, flask, glass, new Vector3(fx - 0.01f, surfaceY + 0.012f, z0 + 0.02f), null, Vector3.one * 1.1f);
            var liq = Sc06LiquidDisc("Sc06FlaskLiqMesh", 0.062f, 0.07f);
            Sc06Mesh("FlaskALiquid", parent, liq, liquid, new Vector3(fx - 0.01f, surfaceY + 0.02f, z0 + 0.02f), null, Vector3.one * 1.1f);
            Box("ClampA", parent, new Vector3(fx - 0.01f, surfaceY + 0.34f, z0 - 0.06f), new Vector3(0.05f, 0.025f, 0.14f), clampMat, false);
            // condenser: long slanted tube with a water jacket from the flask neck down to the beaker
            var a = new Vector3(fx - 0.01f, surfaceY + 0.42f, z0 + 0.02f);
            var b = new Vector3(stands[2] + 0.02f, surfaceY + 0.27f, z0 + 0.08f);
            Sc06Rod("Condenser", parent, a, b, 0.016f, glass);
            Sc06Rod("CondenserJacket", parent, Vector3.Lerp(a, b, 0.18f), Vector3.Lerp(a, b, 0.82f), 0.028f, glass);
            Sc06Rod("TubeIn", parent, Vector3.Lerp(a, b, 0.72f) + new Vector3(0f, 0.03f, 0f), Vector3.Lerp(a, b, 0.72f) + new Vector3(0.08f, 0.12f, -0.1f), 0.006f, rubber);
            Sc06Rod("TubeOut", parent, Vector3.Lerp(a, b, 0.3f) + new Vector3(0f, 0.03f, 0f), Vector3.Lerp(a, b, 0.3f) + new Vector3(-0.08f, 0.14f, -0.1f), 0.006f, rubber);
            // second flask in the middle (empty), a beaker on the right mat
            var mx = stands[1];
            Sc06Mesh("FlaskB", parent, flask, glass, new Vector3(mx, surfaceY + 0.012f, z0 + 0.05f), null, Vector3.one * 0.85f);
            Box("ClampB", parent, new Vector3(mx, surfaceY + 0.3f, z0 - 0.04f), new Vector3(0.045f, 0.022f, 0.11f), clampMat, false);
            var bx = stands[2];
            Sc06ChemGroup(parent, "Beaker", new[] { "glass", "liquid" }, new Vector3(bx, surfaceY + 0.012f, z0 + 0.1f), 0f);
        }
    }
}
#endif

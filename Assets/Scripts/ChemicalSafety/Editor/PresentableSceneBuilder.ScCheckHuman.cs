#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenarios 13-18 lab supervisor. The client's Lab Technician1.glb is a static A-pose mesh (no skeleton). It is converted to body
    /// space (up = +Y, faces +Z, 5.11 units tall), decimated, split per material, skinned to a code-fitted humanoid skeleton (same bone
    /// names as Sc07: Hips, Spine, Chest, Neck, Head, ThighR/L, ShinR/L, FootR/L, UpperArmR/L, ForeArmR/L, HandR/L, R = +X) and saved
    /// as Generated/ScCheck/Supervisor.prefab. LabSupervisor animates the bones procedurally.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const string ScCheckSrcDir = "Assets/Art/ScCheckSrc";
        private const string ScCheckGenDir = GenDir + "/ScCheck";
        private const string ScCheckTexDir = ArtRoot + "/3D/ScCheck/Tex";
        public const string ScCheckSupervisorPrefab = ScCheckGenDir + "/Supervisor.prefab";

        private struct ScJoint
        {
            public string name;
            public string parent;
            public Vector3 pos;
        }

        private static readonly ScJoint[] ScSupJoints =
        {
            new ScJoint { name = "Hips", parent = null, pos = new Vector3(0f, 2.55f, 0.03f) },
            new ScJoint { name = "Spine", parent = "Hips", pos = new Vector3(0f, 3.05f, 0.03f) },
            new ScJoint { name = "Chest", parent = "Spine", pos = new Vector3(0f, 3.60f, 0.03f) },
            new ScJoint { name = "Neck", parent = "Chest", pos = new Vector3(0f, 4.18f, 0.05f) },
            new ScJoint { name = "Head", parent = "Neck", pos = new Vector3(0f, 4.32f, 0.07f) },
            new ScJoint { name = "HeadTop", parent = "Head", pos = new Vector3(0f, 5.11f, 0.07f) },
            new ScJoint { name = "ThighR", parent = "Hips", pos = new Vector3(0.24f, 2.45f, 0.02f) },
            new ScJoint { name = "ShinR", parent = "ThighR", pos = new Vector3(0.25f, 1.38f, 0.05f) },
            new ScJoint { name = "FootR", parent = "ShinR", pos = new Vector3(0.25f, 0.40f, 0.02f) },
            new ScJoint { name = "ToeR", parent = "FootR", pos = new Vector3(0.25f, 0.10f, 0.45f) },
            new ScJoint { name = "ThighL", parent = "Hips", pos = new Vector3(-0.24f, 2.45f, 0.02f) },
            new ScJoint { name = "ShinL", parent = "ThighL", pos = new Vector3(-0.25f, 1.38f, 0.05f) },
            new ScJoint { name = "FootL", parent = "ShinL", pos = new Vector3(-0.25f, 0.40f, 0.02f) },
            new ScJoint { name = "ToeL", parent = "FootL", pos = new Vector3(-0.25f, 0.10f, 0.45f) },
            new ScJoint { name = "UpperArmR", parent = "Chest", pos = new Vector3(0.58f, 4.06f, 0.0f) },
            new ScJoint { name = "ForeArmR", parent = "UpperArmR", pos = new Vector3(1.02f, 3.50f, 0.04f) },
            new ScJoint { name = "HandR", parent = "ForeArmR", pos = new Vector3(1.47f, 2.95f, 0.07f) },
            new ScJoint { name = "FingersR", parent = "HandR", pos = new Vector3(1.70f, 2.50f, 0.12f) },
            new ScJoint { name = "UpperArmL", parent = "Chest", pos = new Vector3(-0.58f, 4.06f, 0.0f) },
            new ScJoint { name = "ForeArmL", parent = "UpperArmL", pos = new Vector3(-1.02f, 3.50f, 0.04f) },
            new ScJoint { name = "HandL", parent = "ForeArmL", pos = new Vector3(-1.47f, 2.95f, 0.07f) },
            new ScJoint { name = "FingersL", parent = "HandL", pos = new Vector3(-1.70f, 2.50f, 0.12f) },
        };

        private static readonly string[][] ScSupSegments =
        {
            new[] { "Hips", "Spine" }, new[] { "Spine", "Chest" }, new[] { "Chest", "Neck" }, new[] { "Neck", "Head" }, new[] { "Head", "HeadTop" },
            new[] { "ThighR", "ShinR" }, new[] { "ShinR", "FootR" }, new[] { "FootR", "ToeR" },
            new[] { "ThighL", "ShinL" }, new[] { "ShinL", "FootL" }, new[] { "FootL", "ToeL" },
            new[] { "UpperArmR", "ForeArmR" }, new[] { "ForeArmR", "HandR" }, new[] { "HandR", "FingersR" },
            new[] { "UpperArmL", "ForeArmL" }, new[] { "ForeArmL", "HandL" }, new[] { "HandL", "FingersL" },
        };

        // submesh -> (part name, triangle budget, base texture size cap, mode 0 opaque / 1 alpha clip / 2 glass)
        private static readonly object[][] ScSupParts =
        {
            new object[] { "Shirt", 3500, 1024, 0 },
            new object[] { "Shoes", 3500, 1024, 0 },
            new object[] { "Pants", 5000, 1024, 0 },
            new object[] { "Coat", 12000, 2048, 0 },
            new object[] { "Face", 14000, 2048, 0 },
            new object[] { "Eyes", 3000, 1024, 0 },
            new object[] { "Brows", 3500, 1024, 1 },
            new object[] { "Hair", 8000, 1024, 0 },
            new object[] { "Goggles", 3500, 1024, 2 },
            new object[] { "Gloves", 9000, 1024, 0 },
            new object[] { "Helmet", 6000, 1024, 0 },
        };

        private static bool ScSupAllowed(string bone, Vector3 v, bool coat)
        {
            var side = bone.EndsWith("R") ? 1f : bone.EndsWith("L") ? -1f : 0f;
            if (bone.StartsWith("Thigh") || bone.StartsWith("Shin") || bone.StartsWith("Foot"))
                return v.y < (coat ? 2.2f : 2.7f) && (Mathf.Abs(v.x) < 0.06f || Mathf.Sign(v.x) == side);
            if (bone.StartsWith("UpperArm") || bone.StartsWith("ForeArm") || bone.StartsWith("Hand"))
                return Mathf.Abs(v.x) > 0.5f && v.y > 2.2f && Mathf.Sign(v.x) == side;
            if (bone == "Head")
                return v.y > 4.22f;
            return true;
        }

        private static BoneWeight[] ScSupWeights(Vector3[] verts, string part, Dictionary<string, Vector3> jp, List<string> order)
        {
            var res = new BoneWeight[verts.Length];
            var headRigid = part == "Eyes" || part == "Brows" || part == "Hair" || part == "Goggles" || part == "Helmet";
            var coat = part == "Coat";
            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (headRigid)
                {
                    res[i] = new BoneWeight { boneIndex0 = order.IndexOf("Head"), weight0 = 1f };
                    continue;
                }

                var cand = new List<KeyValuePair<float, string>>();
                foreach (var seg in ScSupSegments)
                {
                    if (!ScSupAllowed(seg[0], v, coat))
                        continue;
                    var d = DistToSegment(v, jp[seg[0]], jp[seg[1]]);
                    if (coat && (seg[0].StartsWith("Thigh") || seg[0].StartsWith("Shin")))
                        d += 0.35f;
                    cand.Add(new KeyValuePair<float, string>(d, seg[0]));
                }

                cand.Sort((a, b) => a.Key.CompareTo(b.Key));
                var take = Mathf.Min(3, cand.Count);
                var ws = new float[3];
                var sum = 0f;
                for (var k = 0; k < take; k++)
                {
                    ws[k] = 1f / Mathf.Pow(cand[k].Key + (coat ? 0.2f : 0.06f), coat ? 2f : 3f);
                    sum += ws[k];
                }

                var bw = new BoneWeight();
                if (take > 0) { bw.boneIndex0 = order.IndexOf(cand[0].Value); bw.weight0 = ws[0] / sum; }
                if (take > 1) { bw.boneIndex1 = order.IndexOf(cand[1].Value); bw.weight1 = ws[1] / sum; }
                if (take > 2) { bw.boneIndex2 = order.IndexOf(cand[2].Value); bw.weight2 = ws[2] / sum; }
                res[i] = bw;
            }

            return res;
        }

        // Downsizes a (possibly unreadable) texture into a PNG under ScCheckTexDir and returns the imported asset.
        private static Texture2D ScSupTexture(Texture src, int cap, string name, bool alpha)
        {
            if (src == null)
                return null;
            Directory.CreateDirectory(ScCheckTexDir);
            var size = Mathf.Min(cap, Mathf.Max(src.width, src.height));
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            if (name == "Sup_Coat")
            {
                // a clean white lab coat (the source is grey-blue with dark folds): keep a soft fold shading only
                var px = tex.GetPixels32();
                for (var i = 0; i < px.Length; i++)
                {
                    var l = (px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f) / 255f;
                    var v = 0.80f + 0.20f * Mathf.Clamp01(l * 1.25f);
                    px[i] = new Color32((byte)(v * 247f), (byte)(v * 251f), (byte)(v * 255f), 255);
                }

                tex.SetPixels32(px);
            }

            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var path = ScCheckTexDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = alpha;
            ti.maxTextureSize = cap;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material ScSupMaterial(string part, Material srcMat, int cap, int mode)
        {
            Texture baseTex = null;
            foreach (var pn in srcMat.GetTexturePropertyNames())
                if (pn.ToLowerInvariant().Contains("basecolor") && srcMat.GetTexture(pn) != null)
                {
                    baseTex = srcMat.GetTexture(pn);
                    break;
                }

            var tex = ScSupTexture(baseTex, cap, "Sup_" + part, mode != 0);
            var col = Color.white;
            foreach (var pn in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                if (srcMat.HasProperty(pn))
                {
                    col = srcMat.GetColor(pn);
                    break;
                }

            if (baseTex != null && part != "Goggles")
                col = Color.white;
            var smooth = part == "Helmet" ? 0.6f : part == "Shoes" ? 0.45f : part == "Gloves" ? 0.4f : part == "Face" ? 0.3f : part == "Hair" ? 0.35f : 0.18f;
            var name = "ScSup" + part;
            var m = Mat(name, col, smooth, 0f, tex, null, false, mode == 1);
            if (part == "Face")
                m.SetFloat("_Cull", 2f);
            if (mode == 2)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", 5f);
                m.SetFloat("_DstBlend", 10f);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                m.SetColor("_BaseColor", new Color(0.85f, 0.92f, 0.95f, 0.22f));
                m.SetFloat("_Smoothness", 0.96f);
            }

            if (part == "Hair")
                m.SetColor("_BaseColor", new Color(0.06f, 0.05f, 0.05f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("Chemical Safety/Presentable/Check Scenarios/Build Supervisor Prefab")]
        public static void ScCheckBuildSupervisor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ScCheck] Exit Play mode first.");
                return;
            }

            Directory.CreateDirectory(ScCheckGenDir);
            Mats.Clear();
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ScCheckSrcDir + "/Supervisor_Src.glb");
            if (src == null)
            {
                Debug.LogError("[ScCheck] Missing " + ScCheckSrcDir + "/Supervisor_Src.glb (kept out of git; copy the client file there).");
                return;
            }

            var srcMesh = src.GetComponent<MeshFilter>().sharedMesh;
            var srcMats = src.GetComponent<MeshRenderer>().sharedMaterials;
            var verts = srcMesh.vertices;
            var norms = srcMesh.normals;
            var uvs = srcMesh.uv;
            for (var i = 0; i < verts.Length; i++)
            {
                // glb root node is rotated 90 degrees about X: (x,y,z) -> (x,-z,y)
                verts[i] = new Vector3(verts[i].x, -verts[i].z, verts[i].y);
                norms[i] = new Vector3(norms[i].x, -norms[i].z, norms[i].y);
            }

            var root = new GameObject("Supervisor");
            var jp = new Dictionary<string, Vector3>();
            var bones = new Dictionary<string, Transform>();
            var order = new List<string>();
            foreach (var j in ScSupJoints)
            {
                var t = new GameObject(j.name).transform;
                t.SetParent(j.parent == null ? root.transform : bones[j.parent], false);
                t.position = j.pos;
                bones[j.name] = t;
                jp[j.name] = j.pos;
                if (j.name != "HeadTop" && !j.name.StartsWith("Toe") && !j.name.StartsWith("Fingers"))
                    order.Add(j.name);
            }

            var boneArr = new Transform[order.Count];
            var bind = new Matrix4x4[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                boneArr[i] = bones[order[i]];
                bind[i] = boneArr[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            foreach (var s in new[] { "HandR", "HandL" })
            {
                var sock = new GameObject(s + "_Socket").transform;
                sock.SetParent(bones[s], false);
                sock.position = jp[s] + new Vector3(Mathf.Sign(jp[s].x) * 0.12f, -0.25f, 0.06f);
            }

            var total = 0;
            for (var si = 0; si < srcMesh.subMeshCount && si < ScSupParts.Length; si++)
            {
                var part = (string)ScSupParts[si][0];
                if (part == "Hair") // the hard hat covers it and the decimated strands look broken
                    continue;
                var budget = (int)ScSupParts[si][1];
                var cap = (int)ScSupParts[si][2];
                var mode = (int)ScSupParts[si][3];
                var srcIdx = srcMesh.GetIndices(si);
                if (part == "Brows")
                {
                    // keep only what sits at brow height: the lower hair cards drew a black patch at the mouth
                    var keep = new List<int>();
                    for (var k = 0; k + 2 < srcIdx.Length; k += 3)
                    {
                        var cy = (verts[srcIdx[k]].y + verts[srcIdx[k + 1]].y + verts[srcIdx[k + 2]].y) / 3f;
                        if (cy > 4.66f)
                        {
                            keep.Add(srcIdx[k]);
                            keep.Add(srcIdx[k + 1]);
                            keep.Add(srcIdx[k + 2]);
                        }
                    }

                    srcIdx = keep.ToArray();
                }

                // weld vertices that only differ by normal (glb splits them), so the decimator can collapse across them
                var wmap = new Dictionary<(int, int, int, int, int), int>();
                var wv = new List<Vector3>();
                var wn = new List<Vector3>();
                var wu = new List<Vector2>();
                var wt = new int[srcIdx.Length];
                for (var k = 0; k < srcIdx.Length; k++)
                {
                    var s = srcIdx[k];
                    var key = (Mathf.RoundToInt(verts[s].x * 2000f), Mathf.RoundToInt(verts[s].y * 2000f), Mathf.RoundToInt(verts[s].z * 2000f),
                        Mathf.RoundToInt(uvs[s].x * 2000f), Mathf.RoundToInt(uvs[s].y * 2000f));
                    if (!wmap.TryGetValue(key, out var wi))
                    {
                        wi = wv.Count;
                        wmap[key] = wi;
                        wv.Add(verts[s]);
                        wn.Add(norms[s]);
                        wu.Add(uvs[s]);
                    }
                    else
                    {
                        wn[wi] += norms[s];
                    }

                    wt[k] = wi;
                }

                var wva = wv.ToArray();
                var wua = wu.ToArray();
                var tris = ScCheckDecimator.Decimate(wva, wua, wt, budget, !(part == "Hair" || part == "Brows" || part == "Helmet" || part == "Goggles"));

                // compact the vertices used by this part
                var map = new Dictionary<int, int>();
                var pv = new List<Vector3>();
                var pn = new List<Vector3>();
                var pu = new List<Vector2>();
                var nt = new int[tris.Length];
                for (var k = 0; k < tris.Length; k++)
                {
                    if (!map.TryGetValue(tris[k], out var ni))
                    {
                        ni = pv.Count;
                        map[tris[k]] = ni;
                        pv.Add(wva[tris[k]]);
                        pn.Add(wn[tris[k]].normalized);
                        pu.Add(wua[tris[k]]);
                    }

                    nt[k] = ni;
                }

                var mesh = new Mesh { name = "ScSup_" + part };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(pv);
                mesh.SetNormals(pn);
                mesh.SetUVs(0, pu);
                mesh.SetTriangles(nt, 0);
                mesh.RecalculateTangents();
                mesh.boneWeights = ScSupWeights(pv.ToArray(), part, jp, order);
                mesh.bindposes = bind;
                mesh.bounds = new Bounds(new Vector3(0f, 2.6f, 0.1f), new Vector3(4.2f, 5.4f, 2f));
                var meshPath = ScCheckGenDir + "/Mesh_Sup_" + part + ".asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                total += nt.Length / 3;

                var go = new GameObject(part);
                go.transform.SetParent(root.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = boneArr;
                smr.rootBone = bones["Hips"];
                smr.localBounds = mesh.bounds;
                smr.updateWhenOffscreen = false;
                smr.sharedMaterial = ScSupMaterial(part, srcMats[si], cap, mode);
                smr.shadowCastingMode = mode == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(ScCheckSupervisorPrefab);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, ScCheckSupervisorPrefab, out var savedOk);
            if (!savedOk || saved == null)
                Debug.LogError("[ScCheck] Supervisor prefab save failed.");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ScCheckSupervisorPrefab, ImportAssetOptions.ForceUpdate);
            Debug.Log("[ScCheck] Supervisor prefab built: " + total + " triangles.");
        }
    }
}
#endif

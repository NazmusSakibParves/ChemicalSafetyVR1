#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 7 workers. Worker_Hi/Lo.glb hold the static T-pose worker cut into parts (Head, Arms, Uniform, Collar, Boots,
    /// Helmet, Eyes, Lashes). Here a humanoid skeleton is fitted to the mesh (joint positions measured on the model, units of
    /// the glb: 3.64 tall, faces +Z), smooth bone weights are computed from the distance to the bone segments and the result is
    /// saved as a skinned prefab (Generated/Sc07/Worker_Hi.prefab). Sc07Human animates the bones procedurally.
    /// Bone names: Hips, Spine, Chest, Neck, Head, ThighR/L, ShinR/L, FootR/L, UpperArmR/L, ForeArmR/L, HandR/L (R = +X).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const string Sc07GenDir = GenDir + "/Sc07";

        private struct Sc07Joint
        {
            public string name;
            public string parent;
            public Vector3 pos;
        }

        private static readonly Sc07Joint[] Sc07Joints =
        {
            new Sc07Joint { name = "Hips", parent = null, pos = new Vector3(0f, 1.45f, -0.04f) },
            new Sc07Joint { name = "Spine", parent = "Hips", pos = new Vector3(0f, 1.85f, -0.05f) },
            new Sc07Joint { name = "Chest", parent = "Spine", pos = new Vector3(0f, 2.30f, -0.06f) },
            new Sc07Joint { name = "Neck", parent = "Chest", pos = new Vector3(0f, 2.80f, -0.08f) },
            new Sc07Joint { name = "Head", parent = "Neck", pos = new Vector3(0f, 2.98f, -0.04f) },
            new Sc07Joint { name = "HeadTop", parent = "Head", pos = new Vector3(0f, 3.62f, -0.02f) },
            new Sc07Joint { name = "ThighR", parent = "Hips", pos = new Vector3(0.22f, 1.40f, -0.06f) },
            new Sc07Joint { name = "ShinR", parent = "ThighR", pos = new Vector3(0.25f, 0.82f, -0.10f) },
            new Sc07Joint { name = "FootR", parent = "ShinR", pos = new Vector3(0.26f, 0.27f, -0.10f) },
            new Sc07Joint { name = "ToeR", parent = "FootR", pos = new Vector3(0.26f, 0.05f, 0.22f) },
            new Sc07Joint { name = "ThighL", parent = "Hips", pos = new Vector3(-0.22f, 1.40f, -0.06f) },
            new Sc07Joint { name = "ShinL", parent = "ThighL", pos = new Vector3(-0.25f, 0.82f, -0.10f) },
            new Sc07Joint { name = "FootL", parent = "ShinL", pos = new Vector3(-0.26f, 0.27f, -0.10f) },
            new Sc07Joint { name = "ToeL", parent = "FootL", pos = new Vector3(-0.26f, 0.05f, 0.22f) },
            new Sc07Joint { name = "UpperArmR", parent = "Chest", pos = new Vector3(0.50f, 2.66f, -0.17f) },
            new Sc07Joint { name = "ForeArmR", parent = "UpperArmR", pos = new Vector3(0.86f, 2.28f, -0.05f) },
            new Sc07Joint { name = "HandR", parent = "ForeArmR", pos = new Vector3(1.17f, 1.95f, 0.01f) },
            new Sc07Joint { name = "FingersR", parent = "HandR", pos = new Vector3(1.31f, 1.80f, 0.03f) },
            new Sc07Joint { name = "UpperArmL", parent = "Chest", pos = new Vector3(-0.50f, 2.66f, -0.17f) },
            new Sc07Joint { name = "ForeArmL", parent = "UpperArmL", pos = new Vector3(-0.86f, 2.28f, -0.05f) },
            new Sc07Joint { name = "HandL", parent = "ForeArmL", pos = new Vector3(-1.17f, 1.95f, 0.01f) },
            new Sc07Joint { name = "FingersL", parent = "HandL", pos = new Vector3(-1.31f, 1.80f, 0.03f) },
        };

        // bone -> its end joint, for the weight segments (the end joint is a child bone or a marker)
        private static readonly string[][] Sc07Segments =
        {
            new[] { "Hips", "Spine" }, new[] { "Spine", "Chest" }, new[] { "Chest", "Neck" }, new[] { "Neck", "Head" }, new[] { "Head", "HeadTop" },
            new[] { "ThighR", "ShinR" }, new[] { "ShinR", "FootR" }, new[] { "FootR", "ToeR" },
            new[] { "ThighL", "ShinL" }, new[] { "ShinL", "FootL" }, new[] { "FootL", "ToeL" },
            new[] { "UpperArmR", "ForeArmR" }, new[] { "ForeArmR", "HandR" }, new[] { "HandR", "FingersR" },
            new[] { "UpperArmL", "ForeArmL" }, new[] { "ForeArmL", "HandL" }, new[] { "HandL", "FingersL" },
        };

        private static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        // Is the bone allowed to move this vertex? Keeps the legs apart, the arms off the legs and the head off the chest.
        private static bool Sc07Allowed(string bone, Vector3 v)
        {
            var side = bone.EndsWith("R") ? 1f : bone.EndsWith("L") ? -1f : 0f;
            if (bone.StartsWith("Thigh") || bone.StartsWith("Shin") || bone.StartsWith("Foot"))
                return v.y < 1.62f && (Mathf.Abs(v.x) < 0.07f || Mathf.Sign(v.x) == side);
            if (bone.StartsWith("UpperArm") || bone.StartsWith("ForeArm") || bone.StartsWith("Hand"))
                return Mathf.Abs(v.x) > 0.36f && v.y > 1.5f && Mathf.Sign(v.x) == side;
            if (bone == "Head")
                return v.y > 2.7f;
            return true;
        }

        private static BoneWeight[] Sc07Weights(Vector3[] verts, string part, Dictionary<string, Vector3> jp)
        {
            var res = new BoneWeight[verts.Length];
            var order = new List<string>();
            foreach (var j in Sc07Joints)
                if (j.name != "HeadTop" && !j.name.StartsWith("Toe") && !j.name.StartsWith("Fingers"))
                    order.Add(j.name);
            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (part == "Eyes" || part == "Lashes")
                {
                    res[i] = new BoneWeight { boneIndex0 = order.IndexOf("Head"), weight0 = 1f };
                    continue;
                }

                var cand = new List<KeyValuePair<float, string>>();
                foreach (var seg in Sc07Segments)
                {
                    if (!Sc07Allowed(seg[0], v))
                        continue;
                    cand.Add(new KeyValuePair<float, string>(DistToSegment(v, jp[seg[0]], jp[seg[1]]), seg[0]));
                }

                cand.Sort((a, b) => a.Key.CompareTo(b.Key));
                var take = Mathf.Min(3, cand.Count);
                var ws = new float[3];
                var sum = 0f;
                for (var k = 0; k < take; k++)
                {
                    ws[k] = 1f / Mathf.Pow(cand[k].Key + 0.03f, 3.2f);
                    sum += ws[k];
                }

                var bw = new BoneWeight();
                if (take > 0)
                {
                    bw.boneIndex0 = order.IndexOf(cand[0].Value);
                    bw.weight0 = ws[0] / sum;
                }

                if (take > 1)
                {
                    bw.boneIndex1 = order.IndexOf(cand[1].Value);
                    bw.weight1 = ws[1] / sum;
                }

                if (take > 2)
                {
                    bw.boneIndex2 = order.IndexOf(cand[2].Value);
                    bw.weight2 = ws[2] / sum;
                }

                res[i] = bw;
            }

            return res;
        }

        // Builds (or rebuilds) Worker_<lod>.prefab and returns it. Joint constants are in glb units; the prefab keeps them, the
        // instances are scaled by about 0.48 (3.64 -> 1.75 m).
        private static GameObject Sc07MakeHuman(string lod)
        {
            Directory.CreateDirectory(Sc07GenDir);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc07/Worker_" + lod + ".glb");
            if (src == null)
            {
                Debug.LogWarning("[Presentable] Missing Worker_" + lod + ".glb");
                return null;
            }

            var root = new GameObject("Worker_" + lod);
            var jp = new Dictionary<string, Vector3>();
            var bones = new Dictionary<string, Transform>();
            foreach (var j in Sc07Joints)
            {
                var t = new GameObject(j.name).transform;
                t.SetParent(j.parent == null ? root.transform : bones[j.parent], false);
                t.position = j.pos;
                bones[j.name] = t;
                jp[j.name] = j.pos;
            }

            var order = new List<string>();
            foreach (var j in Sc07Joints)
                if (j.name != "HeadTop" && !j.name.StartsWith("Toe") && !j.name.StartsWith("Fingers"))
                    order.Add(j.name);
            var boneArr = new Transform[order.Count];
            var bind = new Matrix4x4[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                boneArr[i] = bones[order[i]];
                bind[i] = boneArr[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            var sockets = new[] { "HandR", "HandL" };
            foreach (var s in sockets)
            {
                var sock = new GameObject(s + "_Socket").transform;
                sock.SetParent(bones[s], false);
                sock.position = jp[s];
            }

            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                var part = mf.name;
                var m0 = mf.sharedMesh;
                if (m0 == null)
                    continue;
                var mesh = new Mesh { name = "Sc07Worker_" + lod + "_" + part };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                var verts = m0.vertices;
                mesh.vertices = verts;
                mesh.uv = m0.uv;
                if (part == "Uniform")
                {
                    // two sub-meshes by height: shirt (above the waist) and trousers, so a worker can wear two colours
                    var all = m0.triangles;
                    var upper = new List<int>();
                    var lower = new List<int>();
                    var waist = jp["Hips"].y + 0.02f; // lower split line: no jagged shirt / trouser edge
                    for (var k = 0; k + 2 < all.Length; k += 3)
                    {
                        var cy = (verts[all[k]].y + verts[all[k + 1]].y + verts[all[k + 2]].y) / 3f;
                        var dst = cy > waist ? upper : lower;
                        dst.Add(all[k]);
                        dst.Add(all[k + 1]);
                        dst.Add(all[k + 2]);
                    }

                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(upper, 0);
                    mesh.SetTriangles(lower, 1);
                }
                else
                {
                    mesh.triangles = m0.triangles;
                }

                mesh.RecalculateNormals();
                mesh.boneWeights = Sc07Weights(verts, part, jp);
                mesh.bindposes = bind;
                mesh.bounds = new Bounds(new Vector3(0f, 1.8f, 0f), new Vector3(3.2f, 4.2f, 2.4f));
                var meshPath = Sc07GenDir + "/Mesh_Worker_" + lod + "_" + part + ".asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                var go = new GameObject(part);
                go.transform.SetParent(root.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                smr.bones = boneArr;
                smr.rootBone = bones["Hips"];
                smr.localBounds = mesh.bounds;
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                if (part == "Head")
                    Sc07MakeCapShell(lod, mesh, boneArr, bind, order, root.transform, bones["Hips"]);
            }

            var prefabPath = Sc07GenDir + "/Worker_" + lod + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // A thin shell cut from the head mesh (everything above the hairline, offset a little along the normals): the cloth cap /
        // short hair of a garment worker, shaped exactly like the head. Used by the garment workers only (Sc07Dress).
        private static void Sc07MakeCapShell(string lod, Mesh head, Transform[] boneArr, Matrix4x4[] bind, List<string> order, Transform root, Transform rootBone)
        {
            var hv = head.vertices;
            var hn = head.normals;
            var ht = head.triangles;
            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nt = new List<int>();
            for (var k = 0; k + 2 < ht.Length; k += 3)
            {
                var cy = (hv[ht[k]].y + hv[ht[k + 1]].y + hv[ht[k + 2]].y) / 3f;
                var cz = (hv[ht[k]].z + hv[ht[k + 1]].z + hv[ht[k + 2]].z) / 3f;
                var take = cy > 3.44f || (cy > 3.33f && cz < 0.03f); // above the forehead; lower at the sides and the back (ears stay free)
                if (!take)
                    continue;
                for (var q = 0; q < 3; q++)
                {
                    var idx = ht[k + q];
                    if (!map.TryGetValue(idx, out var ni))
                    {
                        ni = nv.Count;
                        map[idx] = ni;
                        var off = 0.016f + Mathf.Clamp01((hv[idx].y - 3.40f) / 0.22f) * 0.014f;
                        nv.Add(hv[idx] + hn[idx] * off);
                    }

                    nt.Add(ni);
                }
            }

            if (nv.Count == 0)
                return;
            var m = new Mesh { name = "Sc07Worker_" + lod + "_Cap" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(nv);
            m.SetTriangles(nt, 0);
            m.RecalculateNormals();
            var headIx = order.IndexOf("Head");
            var bw = new BoneWeight[nv.Count];
            for (var i = 0; i < bw.Length; i++)
                bw[i] = new BoneWeight { boneIndex0 = headIx, weight0 = 1f };
            m.boneWeights = bw;
            m.bindposes = bind;
            m.bounds = head.bounds;
            var path = Sc07GenDir + "/Mesh_Worker_" + lod + "_Cap.asset";
            AssetDatabase.CreateAsset(m, path);
            var go = new GameObject("Cap");
            go.transform.SetParent(root, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            smr.bones = boneArr;
            smr.rootBone = rootBone;
            smr.localBounds = m.bounds;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
        }

        private sealed class Sc07Person
        {
            public string name;
            public Color skin;
            public float height;   // metres
            public float girth;    // width scale
            public int facial;
            public Color overall = new Color(0.42f, 0.46f, 0.50f);   // everybody wears the same grey-blue coveralls
            public Color helmet = new Color(1f, 0.74f, 0.04f);       // and a yellow helmet
            public Color boots = new Color(0.12f, 0.10f, 0.08f);
            public bool helmetOn = true;                              // garment workers (P1-P4) wear no helmet
            public bool garment;                                      // own shirt colour instead of the grey-blue coveralls
            public Color shirt;
            public Color hair = new Color(0.06f, 0.05f, 0.045f);
            public Color pants = new Color(0.16f, 0.18f, 0.24f);
        }

        // 0 = clean shaven, 1 = moustache, 2 = moustache and short beard
        private static readonly Sc07Person[] Sc07People =
        {
            new Sc07Person { name = "Hero", skin = new Color(0.96f, 0.82f, 0.70f), height = 1.80f, girth = 1.00f, facial = 0 },
            new Sc07Person { name = "P1", skin = new Color(0.52f, 0.35f, 0.25f), height = 1.74f, girth = 0.96f, facial = 0, garment = true, shirt = new Color(0.96f, 0.62f, 0.64f), pants = new Color(0.14f, 0.16f, 0.24f) },
            new Sc07Person { name = "P2", skin = new Color(0.80f, 0.60f, 0.44f), height = 1.68f, girth = 1.08f, facial = 0, garment = true, shirt = new Color(0.64f, 0.80f, 0.96f), pants = new Color(0.45f, 0.40f, 0.30f) },
            new Sc07Person { name = "P3", skin = new Color(1f, 0.89f, 0.80f), height = 1.86f, girth = 0.94f, facial = 0, garment = true, shirt = new Color(0.98f, 0.92f, 0.60f), pants = new Color(0.07f, 0.10f, 0.20f), hair = new Color(0.28f, 0.2f, 0.12f) },
            new Sc07Person { name = "P4", skin = new Color(0.64f, 0.43f, 0.31f), height = 1.64f, girth = 1.04f, facial = 0, garment = true, shirt = new Color(0.64f, 0.88f, 0.70f), pants = new Color(0.12f, 0.20f, 0.34f) },
        };

        private static Texture2D Sc07WorkerTex(string n) => Sc07Tex(n);

        // Dresses one worker instance: skin tone, overalls, helmet and boots colours (own materials per person).
        private static void Sc07Dress(GameObject human, Sc07Person p)
        {
            Material M(string part, Color c, Texture tex, bool clip = false, float smooth = 0.25f) =>
                Mat("Sc07" + p.name + "_" + part, c, smooth, 0f, tex, null, false, clip);
            foreach (var smr in human.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                switch (smr.name)
                {
                    case "Head": smr.sharedMaterial = M("Head", p.skin, Sc07WorkerTex("Worker_Head"), false, 0.3f); break;
                    case "Arms": smr.sharedMaterial = M("Arms", p.skin, Sc07WorkerTex("Worker_Arms"), false, 0.3f); break;
                    case "Uniform":
                        if (p.garment)
                            smr.sharedMaterials = new[] { M("Shirt", p.shirt, null, false, 0.12f), M("Shirt", p.shirt, null, false, 0.12f) }; // one light colour, shirt and trousers (client)
                        else
                        {
                            var coverall = M("Uniform", new Color(0.52f, 0.58f, 0.64f), Sc07WorkerTex("Worker_Uniform"), false, 0.2f);
                            smr.sharedMaterials = new[] { coverall, coverall };
                        }

                        break;
                    case "Collar": smr.sharedMaterial = M("Collar", p.garment ? p.shirt : new Color(0.50f, 0.53f, 0.57f), null, false, 0.2f); break;
                    case "Boots": smr.sharedMaterial = M("Boots", new Color(0.8f, 0.8f, 0.8f), Sc07WorkerTex("Worker_Boot"), false, 0.35f); break;
                    case "Helmet":
                        if (p.helmetOn)
                            smr.sharedMaterial = M("Helmet", p.helmet, null, false, 0.6f);
                        else
                            smr.gameObject.SetActive(false); // garment workers wear no helmet at all
                        break;
                    case "Cap":
                        smr.gameObject.SetActive(false); // the cap shell is not used: everybody wears the model's yellow helmet (client)
                        break;
                    case "Eyes": smr.sharedMaterial = M("Eyes", Color.white, Sc07WorkerTex("Worker_Eye"), false, 0.8f); break;
                    case "Lashes": smr.sharedMaterial = M("Lashes", Color.white, Sc07WorkerTex("Worker_Lash"), true); break;
                }
            }

        }
    }
}
#endif

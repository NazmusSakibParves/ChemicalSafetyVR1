#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Turns Art/Presentable/3D/GloveHand_L/R.bytes (made by Tools/generate_glove_hand.py) into skinned Mesh assets
    /// that use the XR Hands skeleton: same bone order and bind poses as the stock hand mesh, new glove surface.
    /// </summary>
    public static class GloveMeshImporter
    {
        private const string HandsDir = "Assets/Samples/XR Hands/1.9.0/HandVisualizer/Models";
        private const string DataDir = "Assets/Art/Presentable/3D";
        private const string OutDir = "Assets/Art/Presentable/Generated";

        [MenuItem("Chemical Safety/Presentable/Import Glove Hand Meshes")]
        public static void ImportAll()
        {
            Import(false);
            Import(true);
            AssetDatabase.SaveAssets();
        }

        public static Mesh Load(bool right)
        {
            var path = OutDir + "/GloveHand_" + (right ? "R" : "L") + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Import(right);
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            return mesh;
        }

        private static void Import(bool right)
        {
            var side = right ? "R" : "L";
            var bytesPath = DataDir + "/GloveHand_" + side + ".bytes";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HandsDir + "/" + (right ? "RightHand" : "LeftHand") + ".fbx");
            if (!File.Exists(bytesPath) || fbx == null)
            {
                Debug.LogWarning("[Presentable] Glove mesh data or hand model missing: " + bytesPath);
                return;
            }

            var inst = (GameObject)Object.Instantiate(fbx);
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var stock = smr.sharedMesh;
            var bones = smr.bones;
            var prefix = right ? "R_" : "L_";

            using (var br = new BinaryReader(File.OpenRead(bytesPath), Encoding.UTF8))
            {
                if (new string(br.ReadChars(4)) != "GLV1")
                {
                    Debug.LogError("[Presentable] Bad glove mesh file " + bytesPath);
                    Object.DestroyImmediate(inst);
                    return;
                }

                var n = br.ReadInt32();
                var tri = br.ReadInt32();
                var nameCount = br.ReadInt32();
                var boneIndex = new int[nameCount];
                for (var i = 0; i < nameCount; i++)
                {
                    var name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    boneIndex[i] = -1;
                    for (var b = 0; b < bones.Length; b++)
                    {
                        if (bones[b].name == prefix + name)
                            boneIndex[i] = b;
                    }

                    if (boneIndex[i] < 0)
                        Debug.LogWarning("[Presentable] Glove bone not found: " + prefix + name);
                }

                var verts = new Vector3[n];
                var normals = new Vector3[n];
                var uvs = new Vector2[n];
                for (var i = 0; i < n; i++)
                    verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (var i = 0; i < n; i++)
                    normals[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (var i = 0; i < n; i++)
                    uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                var bi = new int[n * 4];
                var bw = new float[n * 4];
                for (var i = 0; i < n * 4; i++)
                    bi[i] = br.ReadInt32();
                for (var i = 0; i < n * 4; i++)
                    bw[i] = br.ReadSingle();
                var tris = new int[tri * 3];
                for (var i = 0; i < tris.Length; i++)
                    tris[i] = br.ReadInt32();

                var weights = new BoneWeight[n];
                for (var i = 0; i < n; i++)
                {
                    var w = new BoneWeight
                    {
                        boneIndex0 = Mathf.Max(0, boneIndex[bi[i * 4]]), weight0 = bw[i * 4],
                        boneIndex1 = Mathf.Max(0, boneIndex[bi[i * 4 + 1]]), weight1 = bw[i * 4 + 1],
                    };
                    weights[i] = w;
                }

                var mesh = new Mesh { name = "GloveHand_" + side };
                mesh.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.boneWeights = weights;
                mesh.bindposes = stock.bindposes;
                mesh.RecalculateBounds();
                var b2 = mesh.bounds;
                b2.Expand(0.05f);
                mesh.bounds = b2;

                var path = OutDir + "/GloveHand_" + side + ".asset";
                Directory.CreateDirectory(OutDir);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                Debug.Log("[Presentable] Glove mesh " + side + ": " + n + " vertices, " + tri + " triangles");
            }

            Object.DestroyImmediate(inst);
        }
    }
}
#endif

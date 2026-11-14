using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Presentable.Editor
{
    /// <summary>
    /// Bakes the animator's film (the 17 clips of Sc19Film.glb) into plain data: the local position / rotation / scale of every animated transform at
    /// every frame (24 fps, 931 frames). Unity's animation system does not move the film's characters in the Quest build, so the player applies this
    /// data by code (<see cref="Sc19Cinematic"/>). Menu: Chemical Safety/Presentable/Bake Sc19 Film. Output: Assets/Resources/Sc19/Sc19FilmBake.bytes.
    /// </summary>
    public static class Sc19FilmBaker
    {
        private const string GlbPath = "Assets/Art/Presentable/3D/Sc19/Sc19Film.glb";
        private const string OutPath = "Assets/Resources/Sc19/Sc19FilmBake.bytes";
        private const int Frames = 931; // 0 .. 930 at 24 fps = 38.75 s
        private const float Fps = 24f;

        [MenuItem("Chemical Safety/Presentable/Bake Sc19 Film")]
        public static void Bake()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (asset == null)
            {
                Debug.LogError("[Sc19Bake] glb missing");
                return;
            }

            var clips = new List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GlbPath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                    clips.Add(c);

            var go = (GameObject)Object.Instantiate(asset);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var paths = new SortedSet<string>();
                foreach (var c in clips)
                    foreach (var b in AnimationUtility.GetCurveBindings(c))
                        if (!string.IsNullOrEmpty(b.path))
                            paths.Add(b.path);

                var list = new List<string>();
                var tr = new List<Transform>();
                foreach (var p in paths)
                {
                    var t = go.transform.Find(p);
                    if (t == null)
                        continue;
                    list.Add(p);
                    tr.Add(t);
                }

                var n = tr.Count;
                var pos = new Vector3[n][];
                var rot = new Quaternion[n][];
                var scl = new Vector3[n][];
                for (var i = 0; i < n; i++)
                {
                    pos[i] = new Vector3[Frames];
                    rot[i] = new Quaternion[Frames];
                    scl[i] = new Vector3[Frames];
                }

                for (var f = 0; f < Frames; f++)
                {
                    var t = Mathf.Min(f / Fps, 38.75f);
                    foreach (var c in clips)
                        c.SampleAnimation(go, Mathf.Min(t, c.length));
                    for (var i = 0; i < n; i++)
                    {
                        pos[i][f] = tr[i].localPosition;
                        rot[i][f] = tr[i].localRotation;
                        scl[i][f] = tr[i].localScale;
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                var kept = 0;
                using (var w = new BinaryWriter(File.Create(OutPath)))
                {
                    w.Write(0x53433139);
                    w.Write(Frames);
                    w.Write(Fps);
                    var flags = new byte[n];
                    for (var i = 0; i < n; i++)
                    {
                        byte fl = 0;
                        for (var f = 1; f < Frames; f++)
                        {
                            if ((pos[i][f] - pos[i][0]).sqrMagnitude > 1e-10f)
                                fl |= 1;
                            if (Quaternion.Angle(rot[i][f], rot[i][0]) > 0.001f)
                                fl |= 2;
                            if ((scl[i][f] - scl[i][0]).sqrMagnitude > 1e-10f)
                                fl |= 4;
                        }

                        flags[i] = fl;
                        if (fl != 0)
                            kept++;
                    }

                    w.Write(kept);
                    for (var i = 0; i < n; i++)
                    {
                        if (flags[i] == 0)
                            continue;
                        w.Write(list[i]);
                        w.Write(flags[i]);
                        for (var f = 0; f < Frames; f++)
                        {
                            if ((flags[i] & 1) != 0)
                            {
                                w.Write(pos[i][f].x);
                                w.Write(pos[i][f].y);
                                w.Write(pos[i][f].z);
                            }

                            if ((flags[i] & 2) != 0)
                            {
                                w.Write(rot[i][f].x);
                                w.Write(rot[i][f].y);
                                w.Write(rot[i][f].z);
                                w.Write(rot[i][f].w);
                            }

                            if ((flags[i] & 4) != 0)
                            {
                                w.Write(scl[i][f].x);
                                w.Write(scl[i][f].y);
                                w.Write(scl[i][f].z);
                            }
                        }
                    }
                }

                AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Sc19Bake] done: " + kept + " animated transforms of " + n + " bound, " + Frames + " frames, " + new FileInfo(OutPath).Length / 1024 + " KB");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

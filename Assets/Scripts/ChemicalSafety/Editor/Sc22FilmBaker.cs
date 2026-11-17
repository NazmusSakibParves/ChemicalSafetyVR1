using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Presentable.Editor
{
    /// <summary>
    /// Bakes the animator's Scenario 22 film (Sc22Film.glb: 290 frames at 24 fps) into plain data (local pos / rot / scale of every animated
    /// transform at every frame), because the glb clips move nothing in the Quest build (see Sc19FilmBaker). Menu: Chemical Safety/Presentable/Bake Sc22 Film.
    /// Output: Assets/Resources/Sc22/Sc22FilmBake.bytes (+ Docs/SC22_FILM_CUTS.txt with the camera cut frames).
    /// </summary>
    public static class Sc22FilmBaker
    {
        private const string GlbPath = "Assets/Art/Presentable/3D/Sc22/Sc22Film.glb";
        private const string OutPath = "Assets/Resources/Sc22/Sc22FilmBake.bytes";
        private const int Frames = 291; // 0 .. 290 at 24 fps = 12.1 s
        private const float Fps = 24f;

        [MenuItem("Chemical Safety/Presentable/Bake Sc22 Film")]
        public static void Bake()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (asset == null)
            {
                Debug.LogError("[Sc22Bake] glb missing");
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

                var camIdx = list.IndexOf("Camera");
                for (var f = 0; f < Frames; f++)
                {
                    var t = f / Fps;
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
                    w.Write(0x53433232);
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

                // camera cuts = frames where the film camera jumps
                var sb = new StringBuilder("camera cut frames (0-based): ");
                if (camIdx >= 0)
                    for (var f = 1; f < Frames; f++)
                        if ((pos[camIdx][f] - pos[camIdx][f - 1]).magnitude > 2f)
                            sb.Append(f).Append(' ');
                sb.AppendLine();
                sb.AppendLine("animated paths:");
                foreach (var p in list)
                    sb.AppendLine("  " + p);
                File.WriteAllText("Docs/SC22_FILM_CUTS.txt", sb.ToString());

                AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Sc22Bake] done: " + kept + " animated transforms of " + n + " bound, " + Frames + " frames, " + new FileInfo(OutPath).Length / 1024 + " KB");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

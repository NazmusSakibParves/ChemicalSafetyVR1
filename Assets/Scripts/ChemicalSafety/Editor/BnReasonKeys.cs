#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using ChemicalSafety.Localization;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Client 2026-11-19: a Bangla answer reason is drawn on ONE line where it fits. The wide rungs of its image must keep their true size, so the
    /// import limit of those images is raised (see ContentTextureImporter / BnFullWidthKeys.txt). Call AddForScenario(n) after the scenario's
    /// Bangla images were rendered (Tools/render_bn_auto.py --keep).
    /// </summary>
    public static class BnReasonKeys
    {
        private const string KeyFile = "Assets/Scripts/ChemicalSafety/Editor/BnFullWidthKeys.txt";

        public static string AddForScenario(int scenario, string key = "RayOptionReasons")
        {
            var path = "Assets/Resources/Content/SC" + scenario.ToString("00") + ".json";
            var txt = File.ReadAllText(path);
            var m = Regex.Match(txt, "\"" + key + "\".*?\"bn\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (!m.Success)
                return "no RayOptionReasons in " + path;
            var keys = new HashSet<string>(File.Exists(KeyFile) ? File.ReadAllLines(KeyFile) : new string[0]);
            var added = 0;
            var files = new List<string>();
            foreach (Match it in Regex.Matches(m.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                var s = Regex.Unescape(it.Groups[1].Value);
                foreach (var bold in new[] { false, true })
                {
                    var k = BnTmp.Hash(s, bold);
                    if (keys.Add(k))
                        added++;
                    foreach (var b in new[] { 900, 1500, 2400, 3600, 5200 })
                    {
                        var f = "Assets/Resources/BnAuto/" + k + "_" + b + ".png";
                        if (File.Exists(f))
                            files.Add(f);
                    }
                }
            }

            File.WriteAllLines(KeyFile, new List<string>(keys).ToArray());
            foreach (var f in files)
                AssetDatabase.ImportAsset(f, ImportAssetOptions.ForceUpdate);
            return "keys added = " + added + ", images reimported = " + files.Count;
        }
    }
}
#endif

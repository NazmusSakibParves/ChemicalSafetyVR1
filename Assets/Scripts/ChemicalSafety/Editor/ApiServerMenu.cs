using System.IO;
using System.Text.RegularExpressions;
using ChemicalSafety.Results;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemicalSafety.EditorTools
{
    /// <summary>
    /// Which server the next build talks to: menu Chemical Safety/Presentable/API Server. It only changes the "active" field of
    /// Assets/Resources/ResultApiConfig.json (stage | production). Ask which one BEFORE every APK build; the build log prints the one baked in.
    /// </summary>
    public static class ApiServerMenu
    {
        private const string Path = "Assets/Resources/ResultApiConfig.json";

        [MenuItem("Chemical Safety/Presentable/API Server/Use STAGE for the next build")]
        public static void UseStage() => Set("stage");

        [MenuItem("Chemical Safety/Presentable/API Server/Use PRODUCTION for the next build")]
        public static void UseProduction() => Set("production");

        [MenuItem("Chemical Safety/Presentable/API Server/Show the active server")]
        public static void Show() => Debug.Log("[API] active server in ResultApiConfig.json = " + Active().ToUpperInvariant());

        public static string Active()
        {
            var m = Regex.Match(File.ReadAllText(Path), "\"active\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "?";
        }

        public static void Set(string name)
        {
            var text = File.ReadAllText(Path);
            var re = new Regex("(\"active\"\\s*:\\s*\")[^\"]*(\")");
            if (!re.IsMatch(text))
            {
                Debug.LogError("[API] no \"active\" field in " + Path);
                return;
            }

            File.WriteAllText(Path, re.Replace(text, "${1}" + name + "${2}", 1));
            AssetDatabase.ImportAsset(Path);
            ResultApiConfig.Reload();
            Debug.Log("[API] the next build uses the " + name.ToUpperInvariant() + " server");
        }
    }

    /// <summary>Prints the server that is baked into every build, so a wrong APK is noticed before it is handed out.</summary>
    public sealed class ApiServerBuildReport : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[BUILD] API server baked into this build: " + ApiServerMenu.Active().ToUpperInvariant() + " (Assets/Resources/ResultApiConfig.json)");
        }
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    public static class ChemicalSafetyApkBuilder
    {
        public const string OutputApk = "Builds/Android/ChemicalSafetyVR.apk";

        [MenuItem("Chemical Safety/Build Quest APK")]
        public static void BuildApk()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("[ChemicalSafety] Scripts are compiling. Wait, then run Build Quest APK again.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[ChemicalSafety] Switching to Android…");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.chemicalsafety.vrtraining");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;

            var abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputApk));
            Directory.CreateDirectory(Path.GetDirectoryName(abs));

            var report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/ChemicalSafety_Demo.unity" },
                abs,
                BuildTarget.Android,
                BuildOptions.Development);

            Debug.Log($"[ChemicalSafety] APK build {report.summary.result}: {abs} errors={report.summary.totalErrors}");
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(abs);
        }
    }
}
#endif

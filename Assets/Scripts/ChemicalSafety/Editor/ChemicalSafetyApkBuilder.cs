#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// One APK per build, containing only the chosen scene (learning or presentable).
    /// </summary>
    public static class ChemicalSafetyApkBuilder
    {
        public const string OutputApk = "Builds/Android/ChemicalSafetyVR.apk";
        public const string PresentableApk = "Builds/Android/ChemicalSafetyVR_Presentable.apk";
        public const string LearningScene = "Assets/Scenes/ChemicalSafety_Demo.unity";
        public const string PresentableScene = "Assets/Scenes/ChemicalSafety_Presentable.unity";

        [MenuItem("Chemical Safety/Build Quest APK (Learning)")]
        public static void BuildApk()
        {
            Build(LearningScene, OutputApk);
        }

        [MenuItem("Chemical Safety/Build Quest APK (Presentable)")]
        public static void BuildPresentableApk()
        {
            Build(PresentableScene, PresentableApk);
        }

        private static void Build(string scene, string output)
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("[ChemicalSafety] Scripts are compiling. Wait, then run the build again.");
                return;
            }

            if (!File.Exists(scene))
            {
                Debug.LogError("[ChemicalSafety] Scene not found: " + scene);
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

            var abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
            Directory.CreateDirectory(Path.GetDirectoryName(abs));

            var report = BuildPipeline.BuildPlayer(new[] { scene }, abs, BuildTarget.Android, BuildOptions.Development);

            Debug.Log($"[ChemicalSafety] APK build {report.summary.result}: {abs} scene={scene} " +
                      $"errors={report.summary.totalErrors}");
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(abs);
        }
    }
}
#endif

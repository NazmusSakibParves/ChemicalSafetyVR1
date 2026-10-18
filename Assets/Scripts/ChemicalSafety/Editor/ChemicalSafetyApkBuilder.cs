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

        /// <summary>Release build with the sharp Presentable render pipeline asset (MSAA 4x, render scale 1.0).</summary>
        [MenuItem("Chemical Safety/Build Quest APK (Presentable)")]
        public static void BuildPresentableApk()
        {
            Build(PresentableScene, PresentableApk, false, PresentableRp);
        }

        [MenuItem("Chemical Safety/Build Quest APK (Presentable, Debug)")]
        public static void BuildPresentableApkDebug()
        {
            Build(PresentableScene, "Builds/Android/ChemicalSafetyVR_Presentable_Debug.apk", true, PresentableRp);
        }

        private const string PresentableRp = "Assets/Settings/Presentable_RPAsset.asset";

        private static void Build(string scene, string output, bool development = true, string renderPipeline = null)
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

            // The sharp render pipeline asset is applied only for this build; the Learning build keeps the original.
            var rp = renderPipeline != null ? AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(renderPipeline) : null;
            var previousLevel = QualitySettings.GetQualityLevel();
            var androidLevel = System.Array.IndexOf(QualitySettings.names, "Mobile");
            UnityEngine.Rendering.RenderPipelineAsset previousRp = null;
            if (rp != null && androidLevel >= 0)
            {
                QualitySettings.SetQualityLevel(androidLevel, false);
                previousRp = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = rp;
                Debug.Log("[ChemicalSafety] Using render pipeline asset " + renderPipeline);
            }

            var options = development ? BuildOptions.Development : BuildOptions.None;
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new[] { scene }, abs, BuildTarget.Android, options);
            }
            finally
            {
                if (rp != null && androidLevel >= 0)
                {
                    QualitySettings.renderPipeline = previousRp;
                    QualitySettings.SetQualityLevel(previousLevel, false);
                    AssetDatabase.SaveAssets();
                }
            }

            Debug.Log($"[ChemicalSafety] APK build {report.summary.result}: {abs} scene={scene} " +
                      $"errors={report.summary.totalErrors}");
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(abs);
        }
    }
}
#endif

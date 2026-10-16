#if UNITY_EDITOR
using ChemicalSafety.Data;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Creates SC01 ScriptableObject from GIZ MCS 1.1 content.
    /// </summary>
    public static class Sc01ContentFactory
    {
        private const string AssetPath = "Assets/Data/Scenarios/SC01_DrumTransfer.asset";

        [MenuItem("Chemical Safety/Create SC01 Scenario Asset")]
        public static ScenarioDefinition CreateOrUpdateSc01()
        {
            EnsureFolders();

            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ScenarioDefinition>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            var question = McqQuestion.Create(
                "MCS 1.1",
                "Which drum should you transfer?",
                new[]
                {
                    "The drum with a faded label",
                    "The drum with no label at all",
                    "The drum whose GHS label does not match the SDS",
                    "The drum with a GHS label matching the SDS"
                },
                correctIndex: 3,
                rationaleTexts: new[]
                {
                    "Cannot verify contents — unidentifiable. Never move unidentified chemicals.",
                    "No hazard information = highest risk. Report to supervisor.",
                    "Discrepancy signals mislabelled or contaminated chemical — stop and report.",
                    "Verified identification matches documentation — safe to transfer."
                });

            asset.EditorApplyContent(
                id: "SC01",
                scenarioTitle: "Drum Transfer — Label & SDS Verification",
                location: "Chemical Warehouse → Production Floor",
                bloom: BloomLevel.Analyze,
                task: "Select",
                nextId: "SC02",
                mcqs: new[] { question });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            Debug.Log($"[ChemicalSafety] SC01 asset ready at {AssetPath}");
            return asset;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Scenarios"))
                AssetDatabase.CreateFolder("Assets/Data", "Scenarios");
        }
    }
}
#endif

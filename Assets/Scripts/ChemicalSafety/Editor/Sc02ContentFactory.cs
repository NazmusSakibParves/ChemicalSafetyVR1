#if UNITY_EDITOR
using ChemicalSafety.Data;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    public static class Sc02ContentFactory
    {
        private const string AssetPath = "Assets/Data/Scenarios/SC02_ChemicalStorage.asset";

        [MenuItem("Chemical Safety/Create SC02 Scenario Asset")]
        public static ScenarioDefinition CreateOrUpdateSc02()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Scenarios"))
                AssetDatabase.CreateFolder("Assets/Data", "Scenarios");

            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ScenarioDefinition>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            var question = McqQuestion.Create(
                "MCS 2.1",
                "Where does the flammable solvent go?",
                new[]
                {
                    "With the oxidizers",
                    "In the flammable-rated compartment, away from oxidizers and acids",
                    "With the acids",
                    "In any open compartment"
                },
                correctIndex: 1,
                rationaleTexts: new[]
                {
                    "Flammable + oxidizer = fire/explosion risk. Never co-store.",
                    "Matches compatibility chart. Flammables stored separately with fire-rated shelving.",
                    "Acid + flammable solvent = potential ignition on contact.",
                    "Ignoring segregation defeats the entire storage system."
                });

            asset.EditorApplyContent(
                id: "SC02",
                scenarioTitle: "Chemical Storage — Compatibility Segregation",
                location: "Chemical Store Zone",
                bloom: BloomLevel.Apply,
                task: "Select + Place",
                nextId: "SC03",
                mcqs: new[] { question });

            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log("[ChemicalSafety] SC02 asset ready at " + AssetPath);
            return asset;
        }
    }
}
#endif

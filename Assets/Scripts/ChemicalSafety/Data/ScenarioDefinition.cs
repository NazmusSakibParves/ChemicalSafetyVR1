using UnityEngine;

namespace ChemicalSafety.Data
{
    public enum BloomLevel
    {
        Remember = 1,
        Understand = 2,
        Apply = 3,
        Analyze = 4,
        Evaluate = 5,
        Create = 6
    }

    /// <summary>
    /// Authorable training scenario. Add SC 02–22 as additional assets; no code change required.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScenarioDefinition",
        menuName = "Chemical Safety/Scenario Definition",
        order = 0)]
    public class ScenarioDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string scenarioId = "SC01";
        [SerializeField] private string title = "Untitled Scenario";
        [SerializeField] private string locationHint;

        [Header("Pedagogy")]
        [SerializeField] private BloomLevel bloomLevel = BloomLevel.Apply;
        [SerializeField] private string taskType = "Select";

        [Header("Flow")]
        [Tooltip("Next scenario id to unlock on success, e.g. SC02.")]
        [SerializeField] private string nextScenarioId;

        [Header("Questions")]
        [SerializeField] private McqQuestion[] questions = System.Array.Empty<McqQuestion>();

        public string ScenarioId => scenarioId;
        public string Title => title;
        public string LocationHint => locationHint;
        public BloomLevel BloomLevel => bloomLevel;
        public string TaskType => taskType;
        public string NextScenarioId => nextScenarioId;
        public McqQuestion[] Questions => questions;

        public McqQuestion GetPrimaryQuestion()
        {
            if (questions == null || questions.Length == 0)
                return null;

            return questions[0];
        }

#if UNITY_EDITOR
        public void EditorApplyContent(
            string id,
            string scenarioTitle,
            string location,
            BloomLevel bloom,
            string task,
            string nextId,
            McqQuestion[] mcqs)
        {
            scenarioId = id;
            title = scenarioTitle;
            locationHint = location;
            bloomLevel = bloom;
            taskType = task;
            nextScenarioId = nextId;
            questions = mcqs;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

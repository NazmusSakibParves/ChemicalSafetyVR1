using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Progress
{
    /// <summary>
    /// In-memory linear unlock service. Reusable for all 22 scenarios.
    /// </summary>
    public sealed class ScenarioProgressService : IScenarioProgressService
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        public ScenarioProgressService(string initiallyUnlockedScenarioId)
        {
            if (!string.IsNullOrWhiteSpace(initiallyUnlockedScenarioId))
                _unlocked.Add(initiallyUnlockedScenarioId);
        }

        public bool IsUnlocked(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
                return false;

            return _unlocked.Contains(scenarioId);
        }

        public void Unlock(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
                return;

            if (_unlocked.Add(scenarioId))
                Debug.Log($"[ChemicalSafety] Unlocked scenario: {scenarioId}");
        }

        public void ResetProgress(string initiallyUnlockedScenarioId)
        {
            _unlocked.Clear();
            if (!string.IsNullOrWhiteSpace(initiallyUnlockedScenarioId))
                _unlocked.Add(initiallyUnlockedScenarioId);
        }
    }
}

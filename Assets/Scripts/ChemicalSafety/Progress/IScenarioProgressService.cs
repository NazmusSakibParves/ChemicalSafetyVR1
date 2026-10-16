namespace ChemicalSafety.Progress
{
    /// <summary>
    /// Tracks which scenarios are unlocked. Implementations can later persist to PlayerPrefs or a LMS.
    /// </summary>
    public interface IScenarioProgressService
    {
        bool IsUnlocked(string scenarioId);
        void Unlock(string scenarioId);
        void ResetProgress(string initiallyUnlockedScenarioId);
    }
}

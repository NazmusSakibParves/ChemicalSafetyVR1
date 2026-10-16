using ChemicalSafety.Data;
using ChemicalSafety.Interaction;
using ChemicalSafety.Progress;
using ChemicalSafety.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ChemicalSafety.Flow
{
    /// <summary>
    /// hotspot → intro beat → MCQ → unlock → Next teleports to next zone (hotspot starts that scenario).
    /// </summary>
    public sealed class TrainingFlowController : MonoBehaviour
    {
        [Header("Start state")]
        [SerializeField] private string initiallyUnlockedScenarioId = "SC01";

        [Header("UI")]
        [SerializeField] private McqPresenter mcqPresenter;
        [SerializeField] private ScenarioIntroDirector introDirector;

        [Header("Locomotion")]
        [SerializeField] private Transform playerRoot;
        [Tooltip("Optional explicit spawn for SC02. Falls back to PlayerSpawn_{scenarioId} in scene.")]
        [SerializeField] private Transform playerSpawnSc02;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> onScenarioUnlocked;
        [SerializeField] private UnityEvent<ScenarioDefinition> onScenarioCompleted;

        private IScenarioProgressService _progress;
        private ScenarioDefinition _activeScenario;

        public IScenarioProgressService Progress => _progress;

        private void Awake()
        {
            _progress = new ScenarioProgressService(initiallyUnlockedScenarioId);
            if (introDirector == null)
                introDirector = GetComponent<ScenarioIntroDirector>();
            if (playerRoot == null)
                ResolvePlayerRoot();
        }

        private void OnEnable()
        {
            if (mcqPresenter == null)
                return;

            mcqPresenter.AnswerSubmitted += HandleAnswerSubmitted;
            mcqPresenter.Closed += HandlePresenterClosed;
            mcqPresenter.NextRequested += HandleNextRequested;
        }

        private void OnDisable()
        {
            if (mcqPresenter == null)
                return;

            mcqPresenter.AnswerSubmitted -= HandleAnswerSubmitted;
            mcqPresenter.Closed -= HandlePresenterClosed;
            mcqPresenter.NextRequested -= HandleNextRequested;
        }

        public bool TryOpenScenario(ScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                Debug.LogWarning("[ChemicalSafety] TryOpenScenario called with null scenario.");
                return false;
            }

            if (!_progress.IsUnlocked(scenario.ScenarioId))
            {
                Debug.Log($"[ChemicalSafety] Scenario locked: {scenario.ScenarioId}");
                return false;
            }

            var question = scenario.GetPrimaryQuestion();
            if (question == null)
            {
                Debug.LogWarning($"[ChemicalSafety] Scenario has no questions: {scenario.ScenarioId}");
                return false;
            }

            _activeScenario = scenario;

            if (introDirector != null)
            {
                introDirector.Play(scenario, () => ShowMcq(scenario, question));
                return true;
            }

            ShowMcq(scenario, question);
            return true;
        }

        private void ShowMcq(ScenarioDefinition scenario, McqQuestion question)
        {
            if (_activeScenario != scenario)
                return;

            mcqPresenter.Show(scenario, question);
        }

        private void HandleAnswerSubmitted(bool isCorrect, int selectedIndex)
        {
            if (_activeScenario == null)
                return;

            if (!isCorrect)
                return;

            CompleteActiveScenario();
        }

        private void CompleteActiveScenario()
        {
            var completed = _activeScenario;
            var nextId = completed.NextScenarioId;

            if (!string.IsNullOrWhiteSpace(nextId))
            {
                _progress.Unlock(nextId);
                onScenarioUnlocked?.Invoke(nextId);
            }

            onScenarioCompleted?.Invoke(completed);
            Debug.Log($"[ChemicalSafety] Completed {completed.ScenarioId}. Next unlocked: {nextId}");
        }

        private void HandleNextRequested(ScenarioDefinition completed)
        {
            _activeScenario = null;

            if (completed == null || string.IsNullOrWhiteSpace(completed.NextScenarioId))
                return;

            var nextId = completed.NextScenarioId;
            if (!_progress.IsUnlocked(nextId))
            {
                _progress.Unlock(nextId);
                onScenarioUnlocked?.Invoke(nextId);
            }

            // Next moves the player to the next zone; hotspot click starts that scenario's intro+MCQ.
            MovePlayerToScenarioZone(nextId);
            Debug.Log($"[ChemicalSafety] Next → teleported to zone for {nextId}. Click hotspot to start.");
        }

        private void HandlePresenterClosed()
        {
            _activeScenario = null;
        }

        private void MovePlayerToScenarioZone(string scenarioId)
        {
            if (playerRoot == null)
                ResolvePlayerRoot();

            if (playerRoot == null)
            {
                Debug.LogWarning("[ChemicalSafety] No XR Origin / player root found for teleport.");
                return;
            }

            var spawn = ResolveSpawnForScenario(scenarioId);
            if (spawn == null)
            {
                Debug.LogWarning($"[ChemicalSafety] No PlayerSpawn_{scenarioId} found; staying in place.");
                return;
            }

            var pos = spawn.position;
            playerRoot.SetPositionAndRotation(
                new Vector3(pos.x, playerRoot.position.y, pos.z),
                spawn.rotation);
        }

        private Transform ResolveSpawnForScenario(string scenarioId)
        {
            if (scenarioId == "SC02" && playerSpawnSc02 != null)
                return playerSpawnSc02;

            var byName = GameObject.Find("PlayerSpawn_" + scenarioId);
            if (byName != null)
                return byName.transform;

            // Fallback: stand a bit in front of the hotspot looking at it.
            var hotspot = GameObject.Find("Hotspot_" + scenarioId);
            if (hotspot == null)
                return null;

            return hotspot.transform;
        }

        private void ResolvePlayerRoot()
        {
            var origin = GameObject.Find("XR Origin (XR Rig)");
            if (origin == null)
                origin = GameObject.Find("XR Origin");
            if (origin != null)
                playerRoot = origin.transform;
        }

    }
}

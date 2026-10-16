using ChemicalSafety.Data;
using ChemicalSafety.Flow;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemicalSafety.Interaction
{
    /// <summary>
    /// Ray-selectable hotspot that opens a scenario through the training flow controller.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HotspotInteractable : MonoBehaviour
    {
        [SerializeField] private ScenarioDefinition scenario;
        [SerializeField] private TrainingFlowController flowController;
        [SerializeField] private bool requireUnlocked = true;

        public ScenarioDefinition Scenario => scenario;

        private XRSimpleInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
        }

        private void OnEnable()
        {
            if (_interactable != null)
                _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (_interactable != null)
                _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            TryActivate();
        }

        /// <summary>
        /// Also callable from a regular UI Button for editor testing without XR.
        /// </summary>
        public void TryActivate()
        {
            if (scenario == null || flowController == null)
            {
                Debug.LogWarning("[ChemicalSafety] Hotspot missing scenario or flow controller.", this);
                return;
            }

            if (requireUnlocked && !flowController.Progress.IsUnlocked(scenario.ScenarioId))
            {
                Debug.Log($"[ChemicalSafety] Hotspot locked: {scenario.ScenarioId}", this);
                return;
            }

            flowController.TryOpenScenario(scenario);
        }
    }
}

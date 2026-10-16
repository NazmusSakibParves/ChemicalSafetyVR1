using ChemicalSafety.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ChemicalSafety.Flow
{
    /// <summary>
    /// Subscribes to TrainingFlowController unlock events without Inspector wiring.
    /// </summary>
    public sealed class UnlockEventBridge : MonoBehaviour
    {
        [SerializeField] private TrainingFlowController flow;
        [SerializeField] private UnlockBannerView banner;

        private UnityEvent<string> _event;
        private bool _subscribed;

        public void Configure(TrainingFlowController trainingFlow, UnlockBannerView unlockBanner)
        {
            flow = trainingFlow;
            banner = unlockBanner;
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed || _event == null || banner == null)
                return;

            _event.RemoveListener(banner.ShowUnlocked);
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_subscribed || flow == null || banner == null)
                return;

            var field = typeof(TrainingFlowController).GetField(
                "onScenarioUnlocked",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            _event = field?.GetValue(flow) as UnityEvent<string>;
            if (_event == null)
                return;

            _event.AddListener(banner.ShowUnlocked);
            _subscribed = true;
        }
    }
}

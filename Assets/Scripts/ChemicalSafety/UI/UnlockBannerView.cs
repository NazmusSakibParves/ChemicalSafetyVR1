using TMPro;
using UnityEngine;

namespace ChemicalSafety.UI
{
    /// <summary>
    /// Simple feedback when the next scenario id is unlocked.
    /// </summary>
    public sealed class UnlockBannerView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float visibleSeconds = 3f;

        private void Awake()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void ShowUnlocked(string scenarioId)
        {
            if (messageText != null)
            {
                messageText.text = string.IsNullOrWhiteSpace(scenarioId)
                    ? "Scenario complete."
                    : $"Unlocked next: {scenarioId}";
            }

            if (root != null)
                root.SetActive(true);

            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), visibleSeconds);
        }

        private void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }
    }
}

using System;
using ChemicalSafety.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.UI
{
    /// <summary>
    /// MCQ panel (prefer Screen Space Overlay for readable demo UI).
    /// Hides intro/world plaques while the question is open.
    /// After a correct answer, the action button becomes Next and can open the unlocked scenario.
    /// </summary>
    public sealed class McqPresenter : MonoBehaviour
    {
        private const string LabelClose = "Close";
        private const string LabelNext = "Next";

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Hide while open")]
        [SerializeField] private GameObject[] hideWhileOpen = Array.Empty<GameObject>();

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text questionIdText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Options")]
        [SerializeField] private Button[] optionButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] optionLabels = Array.Empty<TMP_Text>();

        [Header("Actions")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeButtonLabel;

        private McqQuestion _currentQuestion;
        private ScenarioDefinition _currentScenario;
        private bool _answered;
        private bool _answeredCorrectly;
        private ColorBlock _defaultCloseColors;

        public event Action<bool, int> AnswerSubmitted;
        public event Action Closed;
        /// <summary>Fired when the learner presses Next after a correct answer (panel already hidden).</summary>
        public event Action<ScenarioDefinition> NextRequested;

        private void Awake()
        {
            if (closeButton != null)
                _defaultCloseColors = closeButton.colors;

            HideImmediate();
            WireButtons();
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        public void Show(ScenarioDefinition scenario, McqQuestion question)
        {
            if (scenario == null || question == null)
                return;

            _currentScenario = scenario;
            _currentQuestion = question;
            _answered = false;
            _answeredCorrectly = false;

            if (titleText != null)
                titleText.text = scenario.Title;

            if (questionIdText != null)
                questionIdText.text = question.QuestionId;

            if (promptText != null)
                promptText.text = question.Prompt;

            if (feedbackText != null)
                feedbackText.text = string.Empty;

            PopulateOptions(question);
            SetActionButtonLabel(LabelClose);
            ResetActionButtonStyle();
            SetHiddenWhileOpen(true);

            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Hide()
        {
            HideImmediate();
            Closed?.Invoke();
        }

        private void HideImmediate()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            SetHiddenWhileOpen(false);
        }

        private void SetHiddenWhileOpen(bool questionIsOpen)
        {
            if (hideWhileOpen == null)
                return;

            foreach (var go in hideWhileOpen)
            {
                if (go != null)
                    go.SetActive(!questionIsOpen);
            }
        }

        private void PopulateOptions(McqQuestion question)
        {
            var options = question.Options ?? Array.Empty<string>();
            var count = Mathf.Min(optionButtons.Length, optionLabels.Length, options.Length);

            for (var i = 0; i < optionButtons.Length; i++)
            {
                var visible = i < count;
                if (optionButtons[i] != null)
                {
                    optionButtons[i].gameObject.SetActive(visible);
                    optionButtons[i].interactable = visible;
                }

                if (visible && optionLabels[i] != null)
                {
                    var letter = (char)('A' + i);
                    optionLabels[i].text = $"{letter}. {options[i]}";
                }
            }
        }

        private void WireButtons()
        {
            for (var i = 0; i < optionButtons.Length; i++)
            {
                var index = i;
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => OnOptionClicked(index));
            }

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseOrNextClicked);
        }

        private void UnwireButtons()
        {
            foreach (var button in optionButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }

            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }

        private void OnCloseOrNextClicked()
        {
            var canGoNext = _answeredCorrectly
                && _currentScenario != null
                && !string.IsNullOrWhiteSpace(_currentScenario.NextScenarioId);

            if (canGoNext)
            {
                var completed = _currentScenario;
                HideImmediate();
                NextRequested?.Invoke(completed);
                return;
            }

            Hide();
        }

        private void OnOptionClicked(int index)
        {
            if (_answered || _currentQuestion == null)
                return;

            _answered = true;
            var correct = _currentQuestion.IsCorrect(index);
            var rationale = _currentQuestion.GetRationale(index);

            if (feedbackText != null)
            {
                feedbackText.text = correct
                    ? $"Correct. {rationale}"
                    : $"Incorrect. {rationale}";
            }

            foreach (var button in optionButtons)
            {
                if (button != null)
                    button.interactable = false;
            }

            AnswerSubmitted?.Invoke(correct, index);

            if (correct)
            {
                _answeredCorrectly = true;
                var hasNext = _currentScenario != null
                    && !string.IsNullOrWhiteSpace(_currentScenario.NextScenarioId);
                SetActionButtonLabel(hasNext ? LabelNext : LabelClose);
                if (hasNext)
                    HighlightActionButtonAsNext();
            }
            else
            {
                Invoke(nameof(AllowRetry), 1.25f);
            }
        }

        private void AllowRetry()
        {
            _answered = false;
            foreach (var button in optionButtons)
            {
                if (button != null && button.gameObject.activeSelf)
                    button.interactable = true;
            }
        }

        private void SetActionButtonLabel(string text)
        {
            var label = closeButtonLabel;
            if (label == null && closeButton != null)
                label = closeButton.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
                label.text = text;
        }

        private void HighlightActionButtonAsNext()
        {
            if (closeButton == null)
                return;

            var colors = closeButton.colors;
            colors.normalColor = new Color(0.55f, 0.85f, 0.55f);
            colors.highlightedColor = new Color(0.45f, 0.92f, 0.45f);
            colors.pressedColor = new Color(0.35f, 0.75f, 0.35f);
            closeButton.colors = colors;

            var image = closeButton.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.55f, 0.85f, 0.55f);
        }

        private void ResetActionButtonStyle()
        {
            if (closeButton == null)
                return;

            closeButton.colors = _defaultCloseColors;
            var image = closeButton.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.85f, 0.88f, 0.92f, 1f);
        }
    }
}

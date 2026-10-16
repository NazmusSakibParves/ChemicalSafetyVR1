using System;
using System.Collections;
using ChemicalSafety.Data;
using ChemicalSafety.UI;
using UnityEngine;

namespace ChemicalSafety.Flow
{
    /// <summary>
    /// Plays a timed intro beat (VO + instruction UI + optional highlight) before the MCQ opens.
    /// </summary>
    public sealed class ScenarioIntroDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class IntroContent
        {
            public string scenarioId = "SC01";
            [TextArea(1, 2)] public string greetingBn = "আসসালামু আলাইকুম";
            public string title = "INSTRUCTIONS";
            [TextArea(2, 4)] public string englishBody =
                "Select the correctly labeled drum based on the Safety Data Sheet (SDS).";
            [TextArea(2, 4)] public string banglaBody =
                "সেফটি ডাটা শিট (SDS) অনুসারে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।";
            public float durationSeconds = 10f;
            public AudioClip voiceClip;
            public Transform focusRoot;
            public GameObject[] highlightTargets = Array.Empty<GameObject>();
        }

        [SerializeField] private IntroInstructionPresenter introPresenter;
        [SerializeField] private IntroContent[] contents = Array.Empty<IntroContent>();
        [SerializeField] private bool includeGreetingOnlyOnFirstScenario = true;

        private bool _hasPlayedSessionGreeting;
        private Coroutine _running;

        public bool IsPlaying => _running != null;

        public void Configure(IntroInstructionPresenter presenter, IntroContent[] introContents)
        {
            introPresenter = presenter;
            contents = introContents ?? Array.Empty<IntroContent>();
        }

        /// <summary>
        /// Call when session welcome VO already played so SC01 intro skips repeating the greeting line.
        /// </summary>
        public void MarkSessionGreetingPlayed()
        {
            _hasPlayedSessionGreeting = true;
        }

        public void Play(ScenarioDefinition scenario, Action onComplete)
        {
            if (_running != null)
                StopCoroutine(_running);

            _running = StartCoroutine(RunIntro(scenario, onComplete));
        }

        public void Cancel()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            introPresenter?.Hide();
            ClearHighlights();
        }

        private IEnumerator RunIntro(ScenarioDefinition scenario, Action onComplete)
        {
            var content = FindContent(scenario != null ? scenario.ScenarioId : null);
            var duration = content != null ? Mathf.Max(1f, content.durationSeconds) : 10f;

            var greeting = string.Empty;
            if (content != null)
            {
                var useGreeting = !includeGreetingOnlyOnFirstScenario
                    || !_hasPlayedSessionGreeting
                    || string.Equals(content.scenarioId, "SC01", StringComparison.OrdinalIgnoreCase);

                if (useGreeting && !string.IsNullOrWhiteSpace(content.greetingBn))
                {
                    greeting = content.greetingBn;
                    _hasPlayedSessionGreeting = true;
                }
            }
            else if (!_hasPlayedSessionGreeting)
            {
                greeting = "আসসালামু আলাইকুম";
                _hasPlayedSessionGreeting = true;
            }

            var title = content != null ? content.title : "INSTRUCTIONS";
            var english = content != null ? content.englishBody : string.Empty;
            var bangla = content != null ? content.banglaBody : string.Empty;
            var clip = content != null ? content.voiceClip : null;

            SetHighlights(content);
            introPresenter?.Show(greeting, title, english, bangla, clip);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                introPresenter?.SetCountdown(duration - elapsed);
                yield return null;
            }

            introPresenter?.Hide();
            ClearHighlights();
            _running = null;
            onComplete?.Invoke();
        }

        private IntroContent FindContent(string scenarioId)
        {
            if (contents == null || string.IsNullOrWhiteSpace(scenarioId))
                return null;

            foreach (var c in contents)
            {
                if (c != null && string.Equals(c.scenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        private void SetHighlights(IntroContent content)
        {
            ClearHighlights();
            if (content?.highlightTargets == null)
                return;

            foreach (var go in content.highlightTargets)
            {
                if (go == null)
                    continue;
                var pulse = go.GetComponent<PulseHighlight>();
                if (pulse == null)
                    pulse = go.AddComponent<PulseHighlight>();
                pulse.Play();
            }
        }

        private void ClearHighlights()
        {
            var pulses = FindObjectsByType<PulseHighlight>(FindObjectsInactive.Include);
            foreach (var p in pulses)
            {
                if (p != null)
                    p.StopPulse();
            }
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.UI
{
    /// <summary>
    /// World-space instruction card shown during the 10s scenario intro beat.
    /// </summary>
    public sealed class IntroInstructionPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text greetingText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text englishBodyText;
        [SerializeField] private TMP_Text banglaBodyText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private AudioSource voiceSource;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        public void Show(string greeting, string title, string english, string bangla, AudioClip voiceClip)
        {
            if (greetingText != null)
                greetingText.text = greeting ?? string.Empty;
            if (titleText != null)
                titleText.text = title ?? "INSTRUCTIONS";
            if (englishBodyText != null)
                englishBodyText.text = english ?? string.Empty;
            if (banglaBodyText != null)
                banglaBodyText.text = bangla ?? string.Empty;
            if (countdownText != null)
                countdownText.text = string.Empty;

            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                StopAllCoroutines();
                StartCoroutine(FadeCanvas(0f, 1f, 0.45f));
            }

            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (voiceClip != null)
                {
                    voiceSource.clip = voiceClip;
                    voiceSource.Play();
                }
            }
        }

        public void SetCountdown(float secondsRemaining)
        {
            if (countdownText == null)
                return;
            countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining)).ToString();
        }

        public void Hide()
        {
            if (voiceSource != null)
                voiceSource.Stop();

            if (canvasGroup != null && panelRoot != null && panelRoot.activeSelf)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOutAndDisable());
                return;
            }

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private System.Collections.IEnumerator FadeOutAndDisable()
        {
            yield return FadeCanvas(canvasGroup.alpha, 0f, 0.3f);
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private System.Collections.IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (canvasGroup == null)
                yield break;

            var t = 0f;
            canvasGroup.alpha = from;
            while (t < duration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}

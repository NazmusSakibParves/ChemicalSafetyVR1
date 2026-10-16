using System.Collections;
using ChemicalSafety.Flow;
using TMPro;
using UnityEngine;

namespace ChemicalSafety.UI
{
    /// <summary>
    /// Plays Bangla welcome VO + subtitle once when the app/scene starts.
    /// </summary>
    public sealed class WelcomeVoicePlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip welcomeClip;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private CanvasGroup subtitleGroup;
        [SerializeField] private string subtitleBn = "আসসালামু আলাইকুম";
        [SerializeField] private float subtitleHoldSeconds = 3.5f;
        [SerializeField] private float fadeSeconds = 0.4f;
        [SerializeField] private float startDelaySeconds = 0.35f;

        private void Awake()
        {
            if (voiceSource == null)
            {
                voiceSource = gameObject.GetComponent<AudioSource>();
                if (voiceSource == null)
                    voiceSource = gameObject.AddComponent<AudioSource>();
            }

            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;

            if (subtitleGroup != null)
                subtitleGroup.alpha = 0f;
            if (subtitleText != null)
            {
                subtitleText.text = subtitleBn ?? string.Empty;
                if (subtitleText.gameObject.activeSelf == false)
                    subtitleText.gameObject.SetActive(true);
            }
        }

        private void Start()
        {
            StartCoroutine(PlayWelcome());
        }

        public void Configure(AudioClip clip, AudioSource source, TMP_Text subtitle, CanvasGroup group)
        {
            welcomeClip = clip;
            voiceSource = source;
            subtitleText = subtitle;
            subtitleGroup = group;
        }

        private IEnumerator PlayWelcome()
        {
            if (startDelaySeconds > 0f)
                yield return new WaitForSeconds(startDelaySeconds);

            var director = FindAnyObjectByType<ScenarioIntroDirector>();
            director?.MarkSessionGreetingPlayed();

            if (subtitleText != null)
                subtitleText.text = subtitleBn ?? string.Empty;

            if (subtitleGroup != null)
                yield return Fade(0f, 1f, fadeSeconds);

            if (voiceSource != null && welcomeClip != null)
            {
                voiceSource.clip = welcomeClip;
                voiceSource.Play();
            }
            else
            {
                Debug.LogWarning("[ChemicalSafety] Welcome VO clip missing — subtitle only.");
            }

            var hold = subtitleHoldSeconds;
            if (welcomeClip != null)
                hold = Mathf.Max(hold, welcomeClip.length + 0.25f);

            yield return new WaitForSeconds(hold);

            if (subtitleGroup != null)
                yield return Fade(1f, 0f, fadeSeconds);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (subtitleGroup == null)
                yield break;

            var t = 0f;
            subtitleGroup.alpha = from;
            var dur = Mathf.Max(0.01f, duration);
            while (t < dur)
            {
                t += Time.deltaTime;
                subtitleGroup.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }

            subtitleGroup.alpha = to;
        }
    }
}

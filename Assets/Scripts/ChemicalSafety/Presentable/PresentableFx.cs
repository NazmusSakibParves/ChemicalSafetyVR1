using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Haptics, procedural UI sounds and tween helpers shared by the Presentable demo.
    /// </summary>
    public static class PresentableFx
    {
        private const int SampleRate = 44100;

        private static AudioSource _sfxSource;
        private static AudioClip _chime;
        private static AudioClip _success;
        private static AudioClip _error;
        private static AudioClip _tick;
        private static AudioClip _whoosh;

        // Explicit == null (not ??=) so clips destroyed between play sessions are regenerated.
        public static AudioClip Chime => _chime != null ? _chime : _chime = Tones("SFX_Chime", new[] { 660f, 990f }, 0.16f, 6f, 0.35f);
        public static AudioClip Success => _success != null ? _success : _success = Tones("SFX_Success", new[] { 523f, 659f, 784f, 1047f }, 0.12f, 5f, 0.35f);
        public static AudioClip Error => _error != null ? _error : _error = Buzz("SFX_Error", 0.45f);
        public static AudioClip Tick => _tick != null ? _tick : _tick = Tones("SFX_Tick", new[] { 1500f }, 0.04f, 40f, 0.2f);
        public static AudioClip Whoosh => _whoosh != null ? _whoosh : _whoosh = Noise("SFX_Whoosh", 0.6f);

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
                return;

            if (_sfxSource == null)
            {
                var go = new GameObject("PresentableSfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _sfxSource = go.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f;
            }

            _sfxSource.PlayOneShot(clip, volume);
        }

        public static void Haptic(bool rightHand, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (device.isValid)
                device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }

        public static void HapticBoth(float amplitude, float duration)
        {
            Haptic(true, amplitude, duration);
            Haptic(false, amplitude, duration);
        }

        public static IEnumerator Tween(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= SmoothStep;
            var t = 0f;
            duration = Mathf.Max(0.0001f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }

            step(ease(1f));
        }

        public static IEnumerator Wait(float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public static float EaseOutCubic(float t)
        {
            var x = 1f - t;
            return 1f - x * x * x;
        }

        private static AudioClip Tones(string name, float[] freqs, float noteSeconds, float decay, float volume)
        {
            var noteSamples = Mathf.CeilToInt(noteSeconds * SampleRate);
            var tail = Mathf.CeilToInt(0.25f * SampleRate);
            var total = noteSamples * freqs.Length + tail;
            var data = new float[total];
            for (var n = 0; n < freqs.Length; n++)
            {
                var start = n * noteSamples;
                for (var i = 0; start + i < total; i++)
                {
                    var t = i / (float)SampleRate;
                    var env = Mathf.Exp(-decay * t) * Mathf.Clamp01(i / 200f);
                    var s = Mathf.Sin(2f * Mathf.PI * freqs[n] * t)
                            + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[n] * t);
                    data[start + i] += s * env * volume * 0.6f;
                }
            }

            return Make(name, data);
        }

        private static AudioClip Buzz(string name, float seconds)
        {
            var total = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[total];
            for (var i = 0; i < total; i++)
            {
                var t = i / (float)SampleRate;
                var f = t < seconds * 0.5f ? 180f : 140f;
                var square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                var env = Mathf.Clamp01(i / 300f) * Mathf.Clamp01((total - i) / 2000f);
                data[i] = square * env * 0.22f;
            }

            return Make(name, data);
        }

        private static AudioClip Noise(string name, float seconds)
        {
            var total = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[total];
            var rng = new System.Random(7);
            var last = 0f;
            for (var i = 0; i < total; i++)
            {
                var p = i / (float)total;
                var env = Mathf.Sin(p * Mathf.PI);
                var white = (float)(rng.NextDouble() * 2.0 - 1.0);
                var k = Mathf.Lerp(0.02f, 0.25f, p);
                last += k * (white - last);
                data[i] = last * env * 0.5f;
            }

            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

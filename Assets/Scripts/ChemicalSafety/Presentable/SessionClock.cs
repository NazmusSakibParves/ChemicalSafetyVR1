using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The 40-minute training clock (client): starts after the login, stops while paused, never runs on time scale.
    /// Ticked by <see cref="SessionHud"/>. The marks file gets <see cref="ElapsedMs"/>.
    /// </summary>
    public static class SessionClock
    {
        /// <summary>All 22 scenarios must fit in this time.</summary>
        public const float DefaultLimitSeconds = 40f * 60f;

        public static float LimitSeconds
        {
            get
            {
#if UNITY_EDITOR
                var v = UnityEditor.EditorPrefs.GetFloat("ChemSafetyTimeLimit", 0f); // smoke test / preview: short limit
                if (v > 1f)
                    return v;
#endif
                return DefaultLimitSeconds;
            }
        }

        public static bool Running { get; private set; }
        public static bool Paused { get; private set; }
        public static bool Expired { get; private set; }
        public static float Elapsed { get; private set; }

        /// <summary>Total seconds spent paused (the per-question time in the marks file excludes this).</summary>
        public static float PausedTotal { get; private set; }

        public static float Remaining => Mathf.Max(0f, LimitSeconds - Elapsed);
        public static long ElapsedMs => (long)(Elapsed * 1000f);

        public static void Begin()
        {
            Elapsed = 0f;
            PausedTotal = 0f;
            Running = true;
            Paused = false;
            Expired = false;
            Sync();
        }

        public static void Stop()
        {
            Running = false;
            Sync();
        }

        public static void Reset()
        {
            Begin();
            Running = false;
        }

        /// <summary>
        /// ONE switch for the full pause (client 2026-11-20): true = PAUSE freezes the whole simulation (game time, voice-over, all audio, films,
        /// animation, physics) and RESUME continues at the same point. false = the earlier behaviour (only the 40-minute clock stops) and
        /// <see cref="GameDelta"/> / <see cref="GameTime"/> are exactly the unscaled Unity time.
        /// </summary>
        public static bool FullPause = true;

        /// <summary>True while the simulation is frozen by PAUSE.</summary>
        public static bool Frozen => FullPause && Running && Paused;

        private static float _frozenSince;
        private static float _frozenTotal;
        private static bool _frozenApplied;

        // Enter Play Mode keeps statics (domain reload off): a pause left over from a stopped test must never start the next run frozen
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFreeze()
        {
            Paused = false;
            _frozenApplied = false;
            _frozenTotal = 0f;
            _frozenSince = 0f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        /// <summary>Frame time for everything that waits on the scenario (voice lines, films, delays): 0 while frozen, otherwise exactly Time.unscaledDeltaTime.</summary>
        public static float GameDelta => Frozen ? 0f : Time.unscaledDeltaTime;

        /// <summary>Scenario time: Time.unscaledTime minus the time spent frozen (identical to Time.unscaledTime when PAUSE was never used).</summary>
        public static float GameTime => (Frozen ? _frozenSince : Time.unscaledTime) - _frozenTotal;

        public static void SetPaused(bool paused)
        {
            if (Running)
                Paused = paused;
            Sync();
        }

        /// <summary>Applies / releases the freeze (Time.timeScale and AudioListener.pause). Safe to call at any time.</summary>
        public static void Sync()
        {
            var freeze = Frozen;
            if (freeze == _frozenApplied)
                return;
            _frozenApplied = freeze;
            if (freeze)
                _frozenSince = Time.unscaledTime;
            else
                _frozenTotal += Time.unscaledTime - _frozenSince;
            Time.timeScale = freeze ? 0f : 1f;
            AudioListener.pause = freeze;
        }

        public static void Tick(float unscaledDelta)
        {
            if (!Running)
                return;
            if (Paused)
            {
                PausedTotal += unscaledDelta;
                return;
            }

            Elapsed += unscaledDelta;
            if (Elapsed >= LimitSeconds)
            {
                Elapsed = LimitSeconds;
                Expired = true;
            }
        }

        public static string Format(float seconds)
        {
            var s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
    }
}

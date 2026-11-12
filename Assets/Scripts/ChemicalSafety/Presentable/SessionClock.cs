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
        }

        public static void Stop() => Running = false;

        public static void Reset()
        {
            Begin();
            Running = false;
        }

        public static void SetPaused(bool paused)
        {
            if (Running)
                Paused = paused;
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

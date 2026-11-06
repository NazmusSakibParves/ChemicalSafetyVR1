using UnityEngine;

namespace ChemicalSafety.Presentable
{
    public enum InputMode
    {
        FingerTouch,
        ControllerRay
    }

    /// <summary>
    /// THE one switch between finger touch and controller ray. Finger touch code is never removed:
    /// to go back to finger-only set <see cref="BuildDefault"/> to FingerTouch (or empty <see cref="RayScenarios"/>).
    /// Editor override: menu Chemical Safety/Presentable/Input Mode (stored as an EditorPref, -1 = use the default).
    /// </summary>
    public static class PresentableInput
    {
        /// <summary>Build-time default. FingerTouch = original behaviour everywhere.</summary>
        public const InputMode BuildDefault = InputMode.ControllerRay;

        /// <summary>Scenarios (1-based) in which the ray may be active. Everything else stays finger touch.</summary>
        private static readonly int[] RayScenarios = { 1, 2 };

        public const string PrefKey = "ChemSafetyInputMode";

        /// <summary>Scenario the director is currently running (0 = login / none).</summary>
        public static int ActiveScenario { get; set; }

        public static InputMode Mode
        {
            get
            {
#if UNITY_EDITOR
                var v = UnityEditor.EditorPrefs.GetInt(PrefKey, -1);
                if (v == (int)InputMode.FingerTouch || v == (int)InputMode.ControllerRay)
                    return (InputMode)v;
#endif
                return BuildDefault;
            }
        }

        public static bool AllowedIn(int scenario)
        {
            if (Mode != InputMode.ControllerRay)
                return false;
            foreach (var s in RayScenarios)
                if (s == scenario)
                    return true;
            return false;
        }

        /// <summary>True while the controller ray (not the fingertip) drives the UI.</summary>
        public static bool RayActive => AllowedIn(ActiveScenario);
    }
}

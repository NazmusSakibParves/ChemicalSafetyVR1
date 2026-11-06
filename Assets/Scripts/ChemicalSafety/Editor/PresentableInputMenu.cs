using ChemicalSafety.Presentable;
using UnityEditor;

namespace ChemicalSafety.Editor
{
    /// <summary>Editor override of the single input switch (PresentableInput). Not saved in the scene.</summary>
    public static class PresentableInputMenu
    {
        private const string Root = "Chemical Safety/Presentable/Input Mode/";

        [MenuItem(Root + "Finger touch (everywhere)")]
        private static void Finger() => EditorPrefs.SetInt(PresentableInput.PrefKey, (int)InputMode.FingerTouch);

        [MenuItem(Root + "Controller ray (Scenario 1)")]
        private static void Ray() => EditorPrefs.SetInt(PresentableInput.PrefKey, (int)InputMode.ControllerRay);

        [MenuItem(Root + "Use build default")]
        private static void Default() => EditorPrefs.DeleteKey(PresentableInput.PrefKey);

        [MenuItem(Root + "Finger touch (everywhere)", true)]
        private static bool FingerCheck()
        {
            Menu.SetChecked(Root + "Finger touch (everywhere)", PresentableInput.Mode == InputMode.FingerTouch);
            return true;
        }

        [MenuItem(Root + "Controller ray (Scenario 1)", true)]
        private static bool RayCheck()
        {
            Menu.SetChecked(Root + "Controller ray (Scenario 1)", PresentableInput.Mode == InputMode.ControllerRay);
            return true;
        }
    }
}

using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The Previous / Next buttons now live in the top control bar of <see cref="SessionHud"/> (client design BACK | NEXT | PAUSE | timer).
    /// This thin object keeps the old entry points for the director (<c>Create</c>, <c>Instance</c>, <c>SetBusy</c>, <c>SetVisible</c>) and
    /// forwards them to the bar; it draws nothing itself. The jump itself is done by the director (<see cref="ScenarioNav"/>).
    /// </summary>
    public sealed class ScenarioNavHud : MonoBehaviour
    {
        public static ScenarioNavHud Instance { get; private set; }

        public static ScenarioNavHud Create(Transform cam, Sprite previousIcon, Sprite nextIcon)
        {
            if (Instance != null)
                Destroy(Instance.gameObject);
            var go = new GameObject("ScenarioNavHud");
            Instance = go.AddComponent<ScenarioNavHud>();
            return Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetVisible(bool visible)
        {
            if (SessionHud.Instance != null)
                SessionHud.Instance.SetVisible(visible);
        }

        /// <summary>True while a jump is running: the buttons ignore presses (no double jump).</summary>
        public void SetBusy(bool busy)
        {
            if (SessionHud.Instance != null)
                SessionHud.Instance.SetBusy(busy);
        }
    }
}

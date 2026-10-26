using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 8 PPE Donning Station: a green arrow marks the four items one at a time (goggles, respirator, gloves, apron).
    /// A finger poke brings the marked item to the user, it is put on (it shrinks into the body) and its row on the hologram
    /// PPE SELECTION PANEL gets a check mark. After the fourth one <see cref="AllDone"/> is true.
    /// </summary>
    public sealed class Sc08Ppe : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("One root per PPE item (order = Sc08Content.PpeNames), each with a trigger collider and a PokeTarget.")]
        [SerializeField] private Transform[] items = new Transform[4];
        [SerializeField] private Transform arrow;

        private readonly bool[] _taken = new bool[4];
        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private Vector3[] _homeScale;
        private PokeTarget[] _pokes;
        private int _current;
        private bool _busy;
        private bool _on;
        private Transform _panel;
        private Image[] _checks;
        private Image[] _rows;
        private Transform _arrowCanvas;

        public bool AllDone { get; private set; }
        public int Taken { get; private set; }
        public int Count => items != null ? items.Length : 0;
        public bool PanelOpen => _panel != null && _panel.gameObject.activeSelf;
        public Transform PanelTransform => _panel;
        public Transform Item(int i) => items != null && i >= 0 && i < items.Length ? items[i] : null;
        public PokeTarget PokeOf(int i) => _pokes != null && i >= 0 && i < _pokes.Length ? _pokes[i] : null;
        public bool CheckVisible(int i) => _checks != null && i >= 0 && i < _checks.Length && _checks[i].gameObject.activeSelf;
        public int Current => _current;

        /// <summary>Raised when an item has been put on (index = Sc08Content.PpeNames order).</summary>
        public event Action<int> ItemTaken;

        private void Start()
        {
            _homePos = new Vector3[items.Length];
            _homeRot = new Quaternion[items.Length];
            _homeScale = new Vector3[items.Length];
            _pokes = new PokeTarget[items.Length];
            CaptureHomes();
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                _pokes[i] = items[i].GetComponent<PokeTarget>();
                if (_pokes[i] == null)
                    continue;
                var index = i;
                _pokes[i].Poked += _ => OnPoke(index);
                _pokes[i].SetInteractable(false);
            }

            BuildPanel();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        private void CaptureHomes()
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                _homePos[i] = items[i].position;
                _homeRot[i] = items[i].rotation;
                _homeScale[i] = items[i].localScale;
            }
        }

        /// <summary>Puts every item back on the wall board and clears the panel.</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            _busy = false;
            _on = false;
            AllDone = false;
            Taken = 0;
            _current = 0;
            for (var i = 0; i < _taken.Length; i++)
                _taken[i] = false;
            if (_homePos != null)
            {
                // before Start the arrays are still empty (the scenario resets the station right after switching the room on)
                for (var i = 0; i < items.Length && i < _homePos.Length && i < _homeRot.Length && i < _homeScale.Length && i < _pokes.Length; i++)
                {
                    if (items[i] == null)
                        continue;
                    items[i].gameObject.SetActive(true);
                    items[i].SetPositionAndRotation(_homePos[i], _homeRot[i]);
                    items[i].localScale = _homeScale[i];
                    if (_pokes[i] != null)
                        _pokes[i].SetInteractable(false);
                }
            }

            RefreshPanel();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        public void Begin()
        {
            ResetAll();
            _on = true;
            ShowPanel();
            PlaceArrow();
            SetPokes();
        }

        public void End()
        {
            _on = false;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (arrow == null || !arrow.gameObject.activeSelf || _current < 0 || _current >= items.Length || items[_current] == null)
                return;
            var tick = Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
            var b = BoundsOf(items[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.15f + tick * 0.045f, b.center.z) + _arrowShift;
            arrow.localScale = Vector3.one * (1f + 0.12f * tick);
        }

        private Vector3 _arrowShift;

        private void OnPoke(int index)
        {
            if (!_on || _busy || index != _current)
                return;
            StartCoroutine(Take(index));
        }

        private IEnumerator Take(int index)
        {
            _busy = true;
            SetPokes();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            var item = items[index];
            var from = item.position;
            var fromRot = item.rotation;
            var fromScale = item.localScale;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var head = cam != null ? cam.position : from;
            var dest = head + fwd * 0.42f + Vector3.down * 0.16f;
            var destRot = Quaternion.LookRotation(-fwd, Vector3.up);
            var holdScale = Mathf.Clamp(0.34f / Mathf.Max(0.05f, BoundsOf(item).size.y), 0.5f, 1.4f);
            for (var t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                item.position = Vector3.Lerp(from, dest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.05f);
                item.rotation = Quaternion.Slerp(fromRot, destRot, k);
                item.localScale = Vector3.Lerp(fromScale, _homeScale[index] * holdScale, k);
                yield return null;
            }

            yield return PresentableFx.Wait(0.35f);

            // put on: it slides toward the body and disappears, the row gets its check mark
            var held = item.position;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                var k = t / 0.35f;
                item.position = Vector3.Lerp(held, head + Vector3.down * 0.28f + fwd * 0.12f, k);
                item.localScale = Vector3.Lerp(_homeScale[index] * holdScale, _homeScale[index] * 0.02f, k * k);
                yield return null;
            }

            item.gameObject.SetActive(false);
            _taken[index] = true;
            Taken++;
            RefreshPanel();
            PresentableFx.Play(PresentableFx.Success, 0.6f);
            PresentableFx.Haptic(true, 0.4f, 0.08f);
            ItemTaken?.Invoke(index);
            _busy = false;
            if (Taken >= items.Length)
            {
                _on = false;
                AllDone = true;
                SetPokes();
            }
            else
            {
                _current = NextIndex();
                PlaceArrow();
                SetPokes();
            }
        }

        private int NextIndex()
        {
            for (var i = 0; i < _taken.Length; i++)
                if (!_taken[i])
                    return i;
            return 0;
        }

        private void SetPokes()
        {
            if (_pokes == null)
                return;
            for (var i = 0; i < _pokes.Length; i++)
                if (_pokes[i] != null)
                    _pokes[i].SetInteractable(_on && !_busy && i == _current);
        }

        private void PlaceArrow()
        {
            if (arrow == null || _current >= items.Length || items[_current] == null)
                return;
            EnsureArrow();
            arrow.gameObject.SetActive(true);
            var cam = Camera.main != null ? Camera.main.transform : null;
            // faces the player, who looks along the room, not along the board
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            arrow.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            var b = BoundsOf(items[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.15f, b.center.z) + _arrowShift;
        }

        private void EnsureArrow()
        {
            if (_arrowCanvas != null || arrow == null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("PpeArrow", arrow, new Vector2(200f, 280f), 0.0011f, false, 80);
            _arrowCanvas = canvas.transform;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", _arrowCanvas, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), art.radialGlow,
                    new Color(0.2f, 1f, 0.4f, 0.4f));
            if (art.arrowDown != null)
                PresentableUi.Img("Arrow", _arrowCanvas, new Vector2(0.14f, 0.06f), new Vector2(0.86f, 0.94f), art.arrowDown,
                    new Color(0.3f, 1f, 0.45f));
            PresentableUi.AlwaysOnTop(canvas);
        }

        // ------------------------------------------------------------------ the PPE SELECTION PANEL (designer's step03)

        private void BuildPanel()
        {
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("PpePanel", transform, new Vector2(760f, 620f), 0.00062f, false, 30);
            _panel = canvas.transform;
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null)
                bg.type = Image.Type.Sliced;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.83f), new Vector2(0.94f, 0.96f),
                Sc08Content.PanelTitle, 40f, new Color(0.96f, 0.99f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 40f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var line = PresentableUi.Rect("Divider", canvas.transform, new Vector2(0.08f, 0.815f), new Vector2(0.92f, 0.822f));
            PresentableUi.Img(line, art.roundedSolid, new Color(0.55f, 0.9f, 1f, 0.5f));

            var n = Sc08Content.PpeNames.Length;
            _checks = new Image[n];
            _rows = new Image[n];
            for (var i = 0; i < n; i++)
            {
                var top = 0.78f - i * 0.19f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.07f, top - 0.16f), new Vector2(0.93f, top));
                _rows[i] = PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, new Color(0.8f, 0.95f, 1f, 0.9f), true);
                var label = PresentableUi.Text("Label", row, new Vector2(0.05f, 0.05f), new Vector2(0.78f, 0.95f),
                    Sc08Content.PpeNames[i], 34f, new Color(0.96f, 0.99f, 1f), TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 34f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var box = PresentableUi.Rect("Box", row, new Vector2(0.82f, 0.2f), new Vector2(0.94f, 0.8f));
                PresentableUi.Img(box, art.roundedSolid, new Color(0.7f, 0.95f, 1f, 0.28f));
                var ck = PresentableUi.Rect("Check", row, new Vector2(0.80f, 0.08f), new Vector2(0.96f, 0.92f));
                _checks[i] = PresentableUi.Img(ck, art.check, new Color(0.9f, 1f, 1f), false, false, true);
                _checks[i].gameObject.SetActive(false);
            }

            PresentableUi.AlwaysOnTop(canvas);
            _panel.gameObject.SetActive(false);
        }

        private void ShowPanel()
        {
            if (_panel == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var head = cam != null ? cam.position : Vector3.zero;
            // eye level, right of the board's line of sight, within finger reach but never in front of the items
            _panel.position = head + fwd * 0.55f + right * 0.66f + Vector3.up * 0.0f;
            _panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            RefreshPanel();
            _panel.gameObject.SetActive(true);
        }

        private void RefreshPanel()
        {
            if (_checks == null)
                return;
            for (var i = 0; i < _checks.Length; i++)
            {
                var on = _taken[i];
                _checks[i].gameObject.SetActive(on);
                _rows[i].color = on ? new Color(0.6f, 1f, 0.8f, 1f) : new Color(0.8f, 0.95f, 1f, 0.9f);
            }
        }

        private static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return new Bounds(t.position, Vector3.one * 0.2f);
            var b = rs[0].bounds;
            for (var i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }
    }
}

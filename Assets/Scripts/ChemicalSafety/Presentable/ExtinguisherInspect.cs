using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7: one green arrow ticks over the wall extinguishers one by one (the player's left to right: Water, CO2, ABC).
    /// Poking the marked one brings it to the hands and opens its hologram summary card (name, what it is effective on, class
    /// badge, a speaker that replays the Bangla voice-over, a close X) at eye level on the right. Closing the card hangs it back
    /// and the arrow moves on. After the third one <see cref="AllSeen"/> is true; the director then opens the
    /// "Extinguisher inspection complete" card (the Scenario 6 gate). Modelled on MaskInspect.
    /// </summary>
    public sealed class ExtinguisherInspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("One root per extinguisher, local +Z = the side that faces the player. Order = Sc07Content.")]
        [SerializeField] private Transform[] items = new Transform[3];
        [SerializeField] private Transform arrow;

        private const float HoldScaleMax = 0.72f;
        private readonly bool[] _seen = new bool[3];
        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private Vector3[] _homeScale;
        private PokeTarget[] _pokes;
        private int _current;
        private int _held = -1;
        private bool _busy;
        private bool _on;
        private Transform _card;
        private TMP_Text _titleText;
        private TMP_Text _nameText;
        private TMP_Text _useText;
        private Image _badge;
        private Image _speaker;
        private Transform _arrowCanvas;
        private bool _speaking;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color HoloCyan = new Color(0.62f, 0.95f, 1f);

        /// <summary>True once every extinguisher was looked at (the card of the last one is closed).</summary>
        public bool AllSeen { get; private set; }
        public int Seen { get; private set; }
        public bool Holding => _held >= 0;
        public int HeldIndex => _held;
        public int Count => items != null ? items.Length : 0;
        public bool CardOpen => _card != null && _card.gameObject.activeSelf;
        public string CardTitle => _titleText != null ? _titleText.text : "";
        public string CardName => _nameText != null ? _nameText.text : "";
        public string CardUse => _useText != null ? _useText.text : "";
        public bool BadgeVisible => _badge != null && _badge.gameObject.activeSelf;
        public Transform CardTransform => _card;
        public Transform Item(int index) => items != null && index >= 0 && index < items.Length ? items[index] : null;

        /// <summary>Raised when an extinguisher is in the hands and its card is open (index = Sc07Content order).</summary>
        public event System.Action<int> Opened;

        /// <summary>Raised when the card is closed and the extinguisher hangs back on the wall.</summary>
        public event System.Action Closed;

        /// <summary>Raised when the speaker button on the card is pressed.</summary>
        public event System.Action<int> SpeakerClicked;

        /// <summary>Director: true while the voice-over plays, so the speaker button shows it is playing.</summary>
        public void SetSpeaking(bool on) => _speaking = on;

        public PokeTarget PokeOf(int index) => _pokes != null && index >= 0 && index < _pokes.Length ? _pokes[index] : null;

        /// <summary>True when the extinguisher's visible bounds centre is inside its poke volume (a finger can reach it).</summary>
        public bool MeshWithinReach(int index)
        {
            if (index < 0 || items == null || index >= items.Length || items[index] == null)
                return false;
            var col = items[index].GetComponent<BoxCollider>();
            if (col == null || !col.isTrigger)
                return false;
            var solid = LayerMask.NameToLayer("SolidProps");
            if (solid >= 0 && items[index].gameObject.layer == solid)
                return false;
            var b = BoundsOf(items[index]);
            return Vector3.Distance(col.ClosestPoint(b.center), b.center) <= 0.07f;
        }

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
                if (_pokes[i] != null)
                {
                    var index = i;
                    _pokes[i].Poked += _ => OnPoke(index);
                    _pokes[i].SetInteractable(false);
                }
            }

            BuildCard();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        public void Begin()
        {
            CaptureHomes();
            AllSeen = false;
            Seen = 0;
            for (var i = 0; i < _seen.Length; i++)
                _seen[i] = false;
            _on = true;
            _current = FirstIndex();
            HideCard();
            PlaceArrow();
            SetPokes(true);
        }

        /// <summary>Hides arrow and card and makes nothing pokeable (used when the hero takes the extinguisher).</summary>
        public void Stop()
        {
            _on = false;
            HideCard();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            if (_pokes != null)
                for (var i = 0; i < _pokes.Length; i++)
                    if (_pokes[i] != null)
                        _pokes[i].SetInteractable(false);
        }

        private void Update()
        {
            if (_speaker != null && _card != null && _card.gameObject.activeSelf)
            {
                var pulse = _speaking ? 0.5f + 0.5f * Mathf.Sin(Time.time * 9f) : 0f;
                _speaker.color = Color.Lerp(Color.white, new Color(0.45f, 1f, 0.7f), pulse);
            }

            if (arrow == null || !arrow.gameObject.activeSelf || _current < 0 || _current >= items.Length || items[_current] == null)
                return;
            // tick, tick: the arrow bobs and pulses over the marked extinguisher
            var tick = Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
            var b = BoundsOf(items[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.17f + tick * 0.05f, b.center.z + 0.06f);
            arrow.localScale = Vector3.one * (1f + 0.12f * tick);
        }

        private void OnPoke(int index)
        {
            if (!_on || _busy || _held >= 0 || index != _current)
                return;
            StartCoroutine(Hold(index));
        }

        private IEnumerator Hold(int index)
        {
            _busy = true;
            _held = index;
            SetPokes(false);
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            var item = items[index];
            var from = item.position;
            var fromRot = item.rotation;
            var fromScale = item.localScale;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var camPos = cam != null ? cam.position : from;
            // in the hands: upright, label toward the user, 0.5 m away, left of the card
            var holdScale = Mathf.Clamp(0.5f / Mathf.Max(0.05f, BoundsOf(item).size.y), 0.45f, HoldScaleMax);
            var dest = camPos + fwd * 0.5f - right * 0.2f + Vector3.down * 0.14f;
            var destRot = Quaternion.LookRotation(-fwd, Vector3.up);
            for (var t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                item.position = Vector3.Lerp(from, dest, k);
                item.rotation = Quaternion.Slerp(fromRot, destRot, k);
                item.localScale = Vector3.Lerp(fromScale, _homeScale[index] * holdScale, k);
                yield return null;
            }

            item.SetPositionAndRotation(dest, destRot);
            item.localScale = _homeScale[index] * holdScale;
            ShowCard(index);
            _busy = false;
            Opened?.Invoke(index);
        }

        public void CloseCard()
        {
            if (_held < 0 || _busy)
                return;
            StartCoroutine(PutBack(_held));
        }

        private IEnumerator PutBack(int index)
        {
            _busy = true;
            HideCard();
            Closed?.Invoke();
            var item = items[index];
            var from = item.position;
            var fromRot = item.rotation;
            var fromScale = item.localScale;
            for (var t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                item.position = Vector3.Lerp(from, _homePos[index], k);
                item.rotation = Quaternion.Slerp(fromRot, _homeRot[index], k);
                item.localScale = Vector3.Lerp(fromScale, _homeScale[index], k);
                yield return null;
            }

            item.SetPositionAndRotation(_homePos[index], _homeRot[index]);
            item.localScale = _homeScale[index];
            _held = -1;
            _busy = false;
            if (!_seen[index])
            {
                _seen[index] = true;
                Seen++;
            }

            if (Seen >= items.Length)
            {
                _on = false;
                if (arrow != null)
                    arrow.gameObject.SetActive(false);
                AllSeen = true;
            }
            else
            {
                _current = NextUnseen();
                PlaceArrow();
                SetPokes(true);
            }
        }

        // left to right as the player sees them (the viewer's left is +X)
        private int NextUnseen()
        {
            var best = -1;
            var bestX = float.MinValue;
            for (var i = 0; i < _seen.Length; i++)
            {
                if (_seen[i] || items[i] == null)
                    continue;
                if (_homePos[i].x > bestX)
                {
                    bestX = _homePos[i].x;
                    best = i;
                }
            }

            return best < 0 ? 0 : best;
        }

        public int FirstIndex()
        {
            var best = 0;
            var bestX = float.MinValue;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null && _homePos != null && _homePos[i].x > bestX)
                {
                    bestX = _homePos[i].x;
                    best = i;
                }
            }

            return best;
        }

        private void SetPokes(bool on)
        {
            for (var i = 0; i < _pokes.Length; i++)
                if (_pokes[i] != null)
                    _pokes[i].SetInteractable(on && i == _current);
        }

        private void PlaceArrow()
        {
            if (arrow == null || _current < 0 || _current >= items.Length || items[_current] == null)
                return;
            EnsureArrow();
            arrow.gameObject.SetActive(true);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            arrow.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            var b = BoundsOf(items[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.17f, b.center.z + 0.06f);
        }

        // Green glowing arrow, like the workflow's marker.
        private void EnsureArrow()
        {
            if (_arrowCanvas != null || arrow == null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("ExtArrow", arrow, new Vector2(200f, 280f), 0.0011f, false, 80);
            _arrowCanvas = canvas.transform;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", _arrowCanvas, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), art.radialGlow,
                    new Color(0.2f, 1f, 0.4f, 0.4f));
            if (art.arrowDown != null)
                PresentableUi.Img("Arrow", _arrowCanvas, new Vector2(0.14f, 0.06f), new Vector2(0.86f, 0.94f), art.arrowDown,
                    new Color(0.3f, 1f, 0.45f));
            PresentableUi.AlwaysOnTop(canvas);
        }

        private void ShowCard(int index)
        {
            if (_card == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            // eye level, to the right of the extinguisher in the hands, with a gap, within finger reach
            var head = cam != null ? cam.position : Vector3.zero;
            _card.position = head + fwd * 0.62f + right * 0.27f + Vector3.down * 0.02f;
            _card.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _titleText.text = Sc07Content.CardTitles[index];
            _nameText.text = Sc07Content.Names[index];
            _useText.text = Sc07Content.EffectiveOn[index];
            if (_badge != null)
                _badge.gameObject.SetActive(Sc07Content.ShowBcBadge[index]);
            _card.gameObject.SetActive(true);
        }

        private void HideCard()
        {
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        private RectTransform IconButton(Transform parent, string name, Sprite icon, Vector2 min, Vector2 max,
            UnityEngine.Events.UnityAction action, out Image image)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            image = PresentableUi.Img(rt, icon, Color.white, raycast: true, preserveAspect: true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(action);
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            btn.onClick.AddListener(lift.Press);
            return rt;
        }

        private TMP_Text Label(Transform parent, string name, string text, Vector2 min, Vector2 max)
        {
            var t = PresentableUi.Text(name, parent, min, max, text, 26f, HoloCyan, TextAlignmentOptions.BottomLeft, true, art.englishFont);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 26f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private TMP_Text Value(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            var t = PresentableUi.Text(name, parent, min, max, "", size, HoloWhite, TextAlignmentOptions.TopLeft, false, art.englishFont);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = size;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // The designer's EXTINGUISHER SUMMARY card (workflow step 05): title, speaker + close at the top right, a boxed
        // extinguisher icon with the name, a boxed flammable icon with what it is effective on, the class badge.
        private void BuildCard()
        {
            var panel = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("ExtCard", transform, new Vector2(960f, 560f), 0.00056f, true, 30);
            _card = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, panel, Color.white);
            if (art.holoPanel != null)
                plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            _titleText = PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.83f), new Vector2(0.75f, 0.95f), "", 30f,
                HoloWhite, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            _titleText.enableAutoSizing = true;
            _titleText.fontSizeMin = 16f;
            _titleText.fontSizeMax = 30f;
            _titleText.textWrappingMode = TextWrappingModes.NoWrap;

            IconButton(canvas.transform, "SpeakerButton", art.iconSpeaker, new Vector2(0.775f, 0.825f), new Vector2(0.862f, 0.955f),
                () => { if (_held >= 0) SpeakerClicked?.Invoke(_held); }, out _speaker);
            IconButton(canvas.transform, "CloseButton", art.iconClose != null ? art.iconClose : art.cross,
                new Vector2(0.872f, 0.825f), new Vector2(0.959f, 0.955f), CloseCard, out _);

            // row 1: extinguisher icon + name
            Box(canvas.transform, "IconBox1", new Vector2(0.06f, 0.50f), new Vector2(0.23f, 0.78f), art.extIconExtinguisher);
            Label(canvas.transform, "NameLabel", Sc07Content.NameLabel, new Vector2(0.27f, 0.67f), new Vector2(0.95f, 0.78f));
            _nameText = Value(canvas.transform, "NameValue", new Vector2(0.27f, 0.50f), new Vector2(0.95f, 0.67f), 32f);
            var line = PresentableUi.Rect("Divider", canvas.transform, new Vector2(0.06f, 0.465f), new Vector2(0.94f, 0.475f));
            PresentableUi.Img(line, art.roundedSolid, new Color(0.55f, 0.9f, 1f, 0.45f));
            // row 2: flammable icon + effective on (+ class badge)
            Box(canvas.transform, "IconBox2", new Vector2(0.06f, 0.12f), new Vector2(0.23f, 0.40f), art.extIconFlammable);
            Label(canvas.transform, "UseLabel", Sc07Content.UseLabel, new Vector2(0.27f, 0.30f), new Vector2(0.80f, 0.41f));
            _useText = Value(canvas.transform, "UseValue", new Vector2(0.27f, 0.08f), new Vector2(0.80f, 0.30f), 30f);
            var badge = PresentableUi.Rect("Badge", canvas.transform, new Vector2(0.82f, 0.14f), new Vector2(0.95f, 0.40f));
            _badge = PresentableUi.Img(badge, art.extBadgeBc, Color.white, preserveAspect: true);
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private static void Box(Transform parent, string name, Vector2 min, Vector2 max, Sprite icon)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            PresentableUi.Img(rt, null, new Color(0.65f, 0.95f, 1f, 0.10f));
            var frame = PresentableUi.Rect(name + "Frame", rt, Vector2.zero, Vector2.one);
            var img = PresentableUi.Img(frame, icon, Color.white, preserveAspect: true);
            img.rectTransform.offsetMin = new Vector2(6f, 6f);
            img.rectTransform.offsetMax = new Vector2(-6f, -6f);
        }

        private void CaptureHomes()
        {
            if (items == null || _homePos == null)
                return;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                _homePos[i] = items[i].position;
                _homeRot[i] = items[i].rotation;
                _homeScale[i] = items[i].localScale;
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

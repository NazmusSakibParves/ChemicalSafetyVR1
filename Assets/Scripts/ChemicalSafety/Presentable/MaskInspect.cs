using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 5: one green arrow ticks above the masks one by one. Poking the marked mask brings it to the hand and opens
    /// a MASK INFORMATION card (name and compatibility with toluene) at eye level on the right. When all four were
    /// seen a BACK / NEXT card asks if the user is ready for the question. Same flow as the SC04 gloves.
    /// </summary>
    public sealed class MaskInspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("One root per mask, local +Z = the side the mask shows to the player. Order = names in Sc05Content.")]
        [SerializeField] private Transform[] masks = new Transform[4];
        [SerializeField] private Transform arrow;

        private const float HoldHeight = 0.36f; // height of a mask in the hand, so the big full-face respirator is not bigger than the rest
        private readonly bool[] _seen = new bool[4];
        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private Vector3[] _homeScale;
        private PokeTarget[] _pokes;
        private int _current;
        private int _held = -1;
        private bool _busy;
        private bool _on;
        private Transform _card;
        private TMP_Text _nameText;
        private TMP_Text _compatText;
        private Image _compatIcon;
        private Transform _gate;
        private Transform _arrowCanvas;
        private Vector3 _arrowBase;

        public bool NextChosen { get; private set; }
        public int Seen { get; private set; }
        public bool Holding => _held >= 0;
        public int HeldIndex => _held;
        public int CurrentIndex => _current;
        public int Count => masks != null ? masks.Length : 0;
        public bool CardOpen => _card != null && _card.gameObject.activeSelf;
        public string CardName => _nameText != null ? _nameText.text : "";
        public string CardCompat => _compatText != null ? _compatText.text : "";

        /// <summary>Raised when a mask is in the hand and its card is open (index = Sc05Content order).</summary>
        public event System.Action<int> Opened;

        /// <summary>Raised when the card is closed and the mask goes back on its hook.</summary>
        public event System.Action Closed;

        /// <summary>Raised when the speaker button on the card is pressed (replay the voice of this mask).</summary>
        public event System.Action<int> SpeakerClicked;

        private Image _maskPicture;
        private Image _speaker;
        private bool _speaking;

        public bool WorkflowStyle => _maskPicture != null;
        public Sprite CardPicture => _maskPicture != null ? _maskPicture.sprite : null;
        public Transform CardTransform => _card;
        public Transform GateTransform => _gate;

        /// <summary>Director: true while a mask voice-over plays, so the speaker button shows it is playing.</summary>
        public void SetSpeaking(bool on) => _speaking = on;

        /// <summary>True when the mask in the hand touches another mask's bounds (the smoke test uses this).</summary>
        public bool HeldOverlapsOthers()
        {
            if (_held < 0 || masks[_held] == null)
                return false;
            var held = BoundsOf(masks[_held]);
            for (var i = 0; i < masks.Length; i++)
            {
                if (i == _held || masks[i] == null)
                    continue;
                if (held.Intersects(BoundsOf(masks[i])))
                    return true;
            }

            return false;
        }

        public PokeTarget PokeOf(int index)
        {
            if (_pokes == null || index < 0 || index >= _pokes.Length)
                return null;
            return _pokes[index];
        }

        /// <summary>True when the mask's visible bounds centre is inside its poke volume (a finger can reach it).</summary>
        public bool MeshWithinReach(int index)
        {
            if (index < 0 || masks == null || index >= masks.Length || masks[index] == null)
                return false;
            var col = masks[index].GetComponent<BoxCollider>();
            if (col == null || !col.isTrigger)
                return false;
            var solid = LayerMask.NameToLayer("SolidProps");
            if (solid >= 0 && masks[index].gameObject.layer == solid)
                return false;
            var b = BoundsOf(masks[index]);
            return Vector3.Distance(col.ClosestPoint(b.center), b.center) <= 0.07f;
        }

        /// <summary>Floor point in front of the rail (+Z, the side the player faces from).</summary>
        public Vector3 StandInFront()
        {
            var c = Vector3.zero;
            var n = 0;
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] == null)
                    continue;
                c += masks[i].position;
                n++;
            }

            if (n == 0)
                return transform.position;
            c /= n;
            return new Vector3(c.x, 0f, c.z + 0.9f);
        }

        private void Start()
        {
            _homePos = new Vector3[masks.Length];
            _homeRot = new Quaternion[masks.Length];
            _homeScale = new Vector3[masks.Length];
            _pokes = new PokeTarget[masks.Length];
            CaptureHomes();
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] == null)
                    continue;
                _pokes[i] = masks[i].GetComponent<PokeTarget>();
                if (_pokes[i] != null)
                {
                    var index = i;
                    _pokes[i].Poked += _ => OnPoke(index);
                    _pokes[i].SetInteractable(false);
                }
            }

            BuildCard();
            BuildGate();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        public void Begin()
        {
            CaptureHomes();
            NextChosen = false;
            Seen = 0;
            for (var i = 0; i < _seen.Length; i++)
                _seen[i] = false;
            _on = true;
            _current = 0;
            HideCard();
            HideGate();
            PlaceArrow();
            SetPokes(true);
        }

        private void Update()
        {
            if (_speaker != null && _card != null && _card.gameObject.activeSelf)
            {
                // the speaker breathes while its voice-over plays
                var pulse = _speaking ? 0.5f + 0.5f * Mathf.Sin(Time.time * 9f) : 0f;
                _speaker.color = Color.Lerp(Color.white, new Color(0.45f, 1f, 0.7f), pulse);
            }

            if (arrow == null || !arrow.gameObject.activeSelf || _current < 0 || _current >= masks.Length)
                return;
            // tick, tick: the arrow bobs and pulses over the marked mask
            var tick = Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
            var b = BoundsOf(masks[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.14f + tick * 0.045f, b.center.z + 0.08f);
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
            var mask = masks[index];
            var from = mask.position;
            var fromRot = mask.rotation;
            var fromScale = mask.localScale;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            // In front of the rail with a clear gap (>= 0.40 m from the masks that still hang there) so straps and loops
            // never go through them, and near enough to inspect. The big full-face respirator must not fill the view.
            var camPos = cam != null ? cam.position : from;
            var railDistance = Vector3.Dot(_homePos[index] - camPos, fwd);
            var distance = Mathf.Clamp(railDistance - 0.50f, 0.34f, 0.60f);
            var holdScale = Mathf.Clamp(HoldHeight / Mathf.Max(0.05f, BoundsOf(mask).size.y), 0.55f, 0.95f);
            var dest = camPos + fwd * distance - right * 0.2f + Vector3.down * 0.12f;
            var destRot = Quaternion.LookRotation(-fwd, Vector3.up); // local +Z (the mask's front) turns to the user
            for (var t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                mask.position = Vector3.Lerp(from, dest, k);
                mask.rotation = Quaternion.Slerp(fromRot, destRot, k);
                mask.localScale = Vector3.Lerp(fromScale, _homeScale[index] * holdScale, k);
                yield return null;
            }

            mask.SetPositionAndRotation(dest, destRot);
            mask.localScale = _homeScale[index] * holdScale;
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
            var mask = masks[index];
            var from = mask.position;
            var fromRot = mask.rotation;
            var fromScale = mask.localScale;
            for (var t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
                mask.position = Vector3.Lerp(from, _homePos[index], k);
                mask.rotation = Quaternion.Slerp(fromRot, _homeRot[index], k);
                mask.localScale = Vector3.Lerp(fromScale, _homeScale[index], k);
                yield return null;
            }

            mask.SetPositionAndRotation(_homePos[index], _homeRot[index]);
            mask.localScale = _homeScale[index];
            _held = -1;
            _busy = false;
            if (!_seen[index])
            {
                _seen[index] = true;
                Seen++;
            }

            if (Seen >= masks.Length)
            {
                _on = false;
                ShowGate();
            }
            else
            {
                _current = NextUnseen();
                PlaceArrow();
                SetPokes(true);
            }
        }

        private int NextUnseen()
        {
            // left to right as the player sees them (viewer's left is +X)
            var best = -1;
            var bestX = float.MinValue;
            for (var i = 0; i < _seen.Length; i++)
            {
                if (_seen[i] || masks[i] == null)
                    continue;
                if (masks[i].position.x > bestX)
                {
                    bestX = masks[i].position.x;
                    best = i;
                }
            }

            return best < 0 ? 0 : best;
        }

        /// <summary>Index of the mask that the arrow marks first (leftmost for the player).</summary>
        public int FirstIndex()
        {
            var best = 0;
            var bestX = float.MinValue;
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] != null && _homePos != null && _homePos[i].x > bestX)
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
            if (arrow == null)
                return;
            _current = NextUnseen();
            if (Seen == 0)
                _current = FirstIndex();
            if (_current < 0 || _current >= masks.Length || masks[_current] == null)
                return;
            EnsureArrow();
            arrow.gameObject.SetActive(true);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            arrow.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            var b = BoundsOf(masks[_current]);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.14f, b.center.z + 0.08f);
        }

        // Green glowing arrow, like the workflow's mask marker.
        private void EnsureArrow()
        {
            if (_arrowCanvas != null || arrow == null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("MaskArrow", arrow, new Vector2(200f, 280f), 0.0011f, false, 80);
            _arrowCanvas = canvas.transform;
            _arrowBase = _arrowCanvas.localScale;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", _arrowCanvas, new Vector2(0f, 0.1f), new Vector2(1f, 0.9f), art.radialGlow,
                    new Color(0.2f, 1f, 0.4f, 0.35f));
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
            // right of the mask in the hand, at eye level, in finger reach
            _card.position = (cam != null ? cam.position : Vector3.zero) + fwd * 0.58f + right * 0.24f + Vector3.up * 0.0f;
            _card.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _nameText.text = Sc05Content.MaskNames[index];
            if (_maskPicture != null && art.maskIcons != null && index < art.maskIcons.Length && art.maskIcons[index] != null)
                _maskPicture.sprite = art.maskIcons[index]; // the picked mask, upright, instead of a tick or cross
            var ok = Sc05Content.Compatible[index];
            _compatText.text = Sc05Content.Compatibility(index).ToUpperInvariant();
            _compatText.color = ok ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.45f, 0.4f);
            if (_compatIcon != null)
            {
                var sprite = ok ? art.check : (art.cross != null ? art.cross : art.circle);
                if (sprite != null)
                    _compatIcon.sprite = sprite;
                _compatIcon.color = ok ? new Color(0.35f, 0.95f, 0.55f) : new Color(1f, 0.4f, 0.35f);
            }

            _card.gameObject.SetActive(true);
        }

        private void HideCard()
        {
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        private void ShowGate()
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            if (_gate == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            _gate.position = (cam != null ? cam.position : Vector3.zero) + fwd * 0.5f;
            _gate.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _gate.gameObject.SetActive(true);
        }

        private void HideGate()
        {
            if (_gate != null)
                _gate.gameObject.SetActive(false);
        }

        private void OnBack()
        {
            HideGate();
            Begin();
        }

        private void OnNext()
        {
            NextChosen = true;
            HideGate();
        }

        private void BuildCard()
        {
            if (Sc05Content.WorkflowStyleCards && art.holoPanel != null && art.iconSpeaker != null)
                BuildCardWorkflow();
            else
                BuildCardClassic();
        }

        private void BuildGate()
        {
            if (Sc05Content.WorkflowStyleCards && art.holoPanel != null && art.iconNext != null)
                BuildGateWorkflow();
            else
                BuildGateClassic();
        }

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color HoloCyan = new Color(0.62f, 0.95f, 1f);

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

        // The designer's MASK INFORMATION card: title with speaker + close at the top right, the mask picture on the
        // left, name and compatibility on the right.
        private void BuildCardWorkflow()
        {
            var canvas = PresentableUi.WorldCanvas("MaskCard", transform, new Vector2(900f, 620f), 0.00058f, true, 30);
            _card = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, art.holoPanel, Color.white);
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.07f, 0.83f), new Vector2(0.70f, 0.95f),
                Sc05Content.CardTitle, 32f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 32f;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            IconButton(canvas.transform, "SpeakerButton", art.iconSpeaker, new Vector2(0.775f, 0.825f), new Vector2(0.862f, 0.955f),
                () => { if (_held >= 0) SpeakerClicked?.Invoke(_held); }, out _speaker);
            IconButton(canvas.transform, "CloseButton", art.iconClose != null ? art.iconClose : art.cross,
                new Vector2(0.872f, 0.825f), new Vector2(0.959f, 0.955f), CloseCard, out _);

            var pic = PresentableUi.Rect("MaskPicture", canvas.transform, new Vector2(0.05f, 0.14f), new Vector2(0.40f, 0.80f));
            _maskPicture = PresentableUi.Img(pic, art.maskIcons != null && art.maskIcons.Length > 0 ? art.maskIcons[0] : null,
                Color.white, preserveAspect: true);

            Label(canvas.transform, "NameLabel", Sc05Content.NameLabel, new Vector2(0.44f, 0.64f), new Vector2(0.95f, 0.76f));
            _nameText = Value(canvas.transform, "NameValue", new Vector2(0.44f, 0.40f), new Vector2(0.95f, 0.64f), 28f, HoloWhite);
            Label(canvas.transform, "CompatLabel", Sc05Content.CompatLabel, new Vector2(0.44f, 0.30f), new Vector2(0.95f, 0.40f));
            _compatText = Value(canvas.transform, "CompatValue", new Vector2(0.44f, 0.08f), new Vector2(0.95f, 0.30f), 28f,
                new Color(0.4f, 1f, 0.6f));
            _compatText.fontStyle = FontStyles.Bold;
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        // The designer's RESPIRATOR INSPECTION COMPLETE card: left-aligned text, [BACK with its icon] and [NEXT with a check].
        private void BuildGateWorkflow()
        {
            var canvas = PresentableUi.WorldCanvas("MaskGate", transform, new Vector2(1000f, 560f), 0.0005f, true, 31);
            _gate = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, art.holoPanel, Color.white);
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.07f, 0.70f), new Vector2(0.95f, 0.92f),
                Sc05Content.GateTitle, 36f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 36f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            PresentableUi.Text("Question", canvas.transform, new Vector2(0.07f, 0.50f), new Vector2(0.95f, 0.70f),
                Sc05Content.GateQuestion, 28f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, art.englishFont);
            PresentableUi.Text("Hint", canvas.transform, new Vector2(0.07f, 0.32f), new Vector2(0.95f, 0.50f),
                Sc05Content.GateBody, 28f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, art.englishFont);
            HoloButton("BackButton", canvas.transform, new Vector2(0.07f, 0.07f), new Vector2(0.44f, 0.29f), "BACK",
                art.iconBack, true, OnBack, art.holoButton);
            HoloButton("NextButton", canvas.transform, new Vector2(0.50f, 0.07f), new Vector2(0.93f, 0.29f), "NEXT",
                art.iconNext, false, OnNext, art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton);
            PresentableUi.AlwaysOnTop(canvas);
            _gate.gameObject.SetActive(false);
        }

        private void HoloButton(string name, Transform parent, Vector2 min, Vector2 max, string label, Sprite icon,
            bool iconFirst, UnityEngine.Events.UnityAction action, Sprite background)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            var img = PresentableUi.Img(rt, background, Color.white, sliced: true, raycast: true);
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(action);
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            btn.onClick.AddListener(lift.Press);
            var iconRt = iconFirst
                ? PresentableUi.Rect("Icon", rt, new Vector2(0.07f, 0.18f), new Vector2(0.27f, 0.82f))
                : PresentableUi.Rect("Icon", rt, new Vector2(0.72f, 0.18f), new Vector2(0.92f, 0.82f));
            if (icon != null)
                PresentableUi.Img(iconRt, icon, Color.white, preserveAspect: true);
            var textMin = iconFirst ? new Vector2(0.30f, 0.08f) : new Vector2(0.08f, 0.08f);
            var textMax = iconFirst ? new Vector2(0.94f, 0.92f) : new Vector2(0.70f, 0.92f);
            var text = PresentableUi.Text("Label", rt, textMin, textMax, label, 34f, HoloWhite, TextAlignmentOptions.Center, true,
                art.englishFont);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 34f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void BuildCardClassic()
        {
            var frame = art.frameWide != null ? art.frameWide : art.roundedPanel;
            var canvas = PresentableUi.WorldCanvas("MaskCard", transform, new Vector2(980f, 520f), 0.00052f, true, 30);
            _card = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, frame, Color.white);
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.08f, 0.80f), new Vector2(0.78f, 0.94f),
                Sc05Content.CardTitle, 28f, Color.white, TextAlignmentOptions.Left, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 28f;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var ring = art.ringThin != null ? art.ringThin : (art.ring != null ? art.ring : art.circle);
            var iconHost = PresentableUi.Rect("Icon", canvas.transform, new Vector2(0.06f, 0.30f), new Vector2(0.26f, 0.74f));
            if (ring != null)
                PresentableUi.Img(PresentableUi.Rect("Ring", iconHost, Vector2.zero, Vector2.one), ring, new Color(0.45f, 0.95f, 1f));
            _compatIcon = PresentableUi.Img(PresentableUi.Rect("Mark", iconHost, new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.76f)),
                art.check, Color.white);

            Label(canvas.transform, "NameLabel", Sc05Content.NameLabel, new Vector2(0.30f, 0.62f), new Vector2(0.94f, 0.76f));
            _nameText = Value(canvas.transform, "NameValue", new Vector2(0.30f, 0.42f), new Vector2(0.94f, 0.64f), 26f, Color.white);
            Label(canvas.transform, "CompatLabel", Sc05Content.CompatLabel, new Vector2(0.30f, 0.30f), new Vector2(0.94f, 0.42f));
            _compatText = Value(canvas.transform, "CompatValue", new Vector2(0.30f, 0.10f), new Vector2(0.94f, 0.32f), 26f,
                new Color(0.4f, 1f, 0.6f));

            var closeRt = PresentableUi.Rect("CloseButton", canvas.transform, new Vector2(0.86f, 0.80f), new Vector2(0.96f, 0.96f));
            PresentableUi.Img(closeRt, art.cross != null ? art.cross : art.circle, Color.white, raycast: true);
            var close = closeRt.gameObject.AddComponent<Button>();
            close.targetGraphic = closeRt.GetComponent<Image>();
            close.onClick.AddListener(CloseCard);
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void Label(Transform parent, string name, string text, Vector2 min, Vector2 max)
        {
            var t = PresentableUi.Text(name, parent, min, max, text, 22f, new Color(0.65f, 0.9f, 1f),
                TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 22f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private TMP_Text Value(Transform parent, string name, Vector2 min, Vector2 max, float size, Color color)
        {
            var t = PresentableUi.Text(name, parent, min, max, "", size, color, TextAlignmentOptions.TopLeft, false, art.englishFont);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = size;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private void BuildGateClassic()
        {
            var frame = art.frameWide != null ? art.frameWide : art.roundedPanel;
            var canvas = PresentableUi.WorldCanvas("MaskGate", transform, new Vector2(980f, 520f), 0.00055f, true, 31);
            _gate = canvas.transform;
            PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, frame, Color.white);
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.92f),
                Sc05Content.GateTitle, 30f, Color.white, TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 30f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            PresentableUi.Text("Question", canvas.transform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.72f),
                Sc05Content.GateQuestion, 24f, Color.white, TextAlignmentOptions.Center, false, art.englishFont);
            PresentableUi.Text("Hint", canvas.transform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.52f),
                Sc05Content.GateBody, 20f, new Color(0.8f, 0.9f, 0.95f), TextAlignmentOptions.Center, false, art.englishFont);
            MakeButton("BackButton", canvas.transform, new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.30f),
                "↩  BACK", null, OnBack);
            MakeButton("NextButton", canvas.transform, new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.30f),
                "NEXT", art.check, OnNext);
            PresentableUi.AlwaysOnTop(canvas);
            _gate.gameObject.SetActive(false);
        }

        private void MakeButton(string name, Transform parent, Vector2 min, Vector2 max, string label, Sprite icon,
            UnityEngine.Events.UnityAction action)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            var img = PresentableUi.Img(rt, art.button, Color.white, raycast: true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);
            var textMin = new Vector2(0.08f, 0.12f);
            if (icon != null)
            {
                PresentableUi.Img(PresentableUi.Rect("Icon", rt, new Vector2(0.08f, 0.22f), new Vector2(0.26f, 0.78f)),
                    icon, new Color(0.05f, 0.22f, 0.16f));
                textMin = new Vector2(0.28f, 0.12f);
            }

            PresentableUi.Text("Label", rt, textMin, new Vector2(0.94f, 0.88f), label, 26f,
                new Color(0.05f, 0.12f, 0.16f), TextAlignmentOptions.Center, true, art.englishFont);
        }

        private void CaptureHomes()
        {
            if (masks == null || _homePos == null)
                return;
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] == null)
                    continue;
                _homePos[i] = masks[i].position;
                _homeRot[i] = masks[i].rotation;
                _homeScale[i] = masks[i].localScale;
            }
        }

        private static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return new Bounds(t.position, Vector3.one * 0.2f);
            var box = rs[0].bounds;
            for (var i = 1; i < rs.Length; i++)
                box.Encapsulate(rs[i].bounds);
            return box;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }
    }
}

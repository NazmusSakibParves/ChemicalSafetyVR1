using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 4: the SC01 floating arrow marks one glove at a time (left to right). Touching it (or the glove) brings that
    /// glove to the hand and opens the GLOVE PROPERTIES card, in the Scenario 5 hologram look, at eye level to the right of the
    /// glove. Closing the card puts the glove back and moves the arrow on. After the third glove <see cref="AllSeen"/> is true;
    /// the director then opens the "Glove inspection complete" card.
    /// </summary>
    public sealed class GloveInspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform[] gloves = new Transform[3];
        [SerializeField] private FloatingTouchButton[] arrows = new FloatingTouchButton[3];

        private readonly bool[] _seen = new bool[3];
        private Vector3[] _homeLocal;
        private Quaternion[] _homeLocalRot;
        private PokeTarget[] _pokes;
        private int _current;
        private int _held = -1;
        private bool _busy;
        private bool _on;
        private Transform _card;
        private TMP_Text[] _values;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color HoloCyan = new Color(0.62f, 0.95f, 1f);

        /// <summary>True once every glove was looked at (the card of the last one is closed).</summary>
        public bool AllSeen { get; private set; }
        public int Seen { get; private set; }
        public bool Holding => _held >= 0;

        public PokeTarget PokeOf(int index)
        {
            if (_pokes == null || index < 0 || index >= _pokes.Length)
                return null;
            return _pokes[index];
        }

        /// <summary>The floating arrow of a glove (the smoke test presses it like a fingertip would).</summary>
        public FloatingTouchButton ArrowOf(int index)
        {
            if (arrows == null || index < 0 || index >= arrows.Length)
                return null;
            return arrows[index];
        }

        /// <summary>
        /// True when the visible mesh centre is inside the poke volume. The smoke test uses this
        /// because calling Fire() never checks that a fingertip can actually reach the glove.
        /// </summary>
        public bool MeshWithinReach(int index)
        {
            if (index < 0 || gloves == null || index >= gloves.Length || gloves[index] == null)
                return false;
            var col = gloves[index].GetComponent<BoxCollider>();
            var follow = gloves[index].GetComponent<GloveParts>();
            if (col == null || !col.isTrigger || follow == null || follow.Parts == null || follow.Parts.Length == 0)
                return false;
            var solid = LayerMask.NameToLayer("SolidProps");
            if (solid >= 0 && gloves[index].gameObject.layer == solid)
                return false;
            var b = BoundsOf(gloves[index]);
            return Vector3.Distance(col.ClosestPoint(b.center), b.center) <= 0.07f;
        }

        /// <summary>Visible bounds of a glove (all its mesh parts), for tests.</summary>
        public Bounds GloveBounds(int index)
        {
            return index >= 0 && index < gloves.Length && gloves[index] != null
                ? BoundsOf(gloves[index])
                : new Bounds(transform.position, Vector3.zero);
        }

        /// <summary>Floor point just in front of the gloves (+Z, the side the player faces from).</summary>
        public Vector3 StandInFront()
        {
            var c = Vector3.zero;
            var n = 0;
            for (var i = 0; i < gloves.Length; i++)
            {
                if (gloves[i] == null)
                    continue;
                c += gloves[i].position;
                n++;
            }

            if (n == 0)
                return transform.position;
            c /= n;
            return new Vector3(c.x, 0f, c.z + 0.85f);
        }

        private void Start()
        {
            _homeLocal = new Vector3[gloves.Length];
            _homeLocalRot = new Quaternion[gloves.Length];
            _pokes = new PokeTarget[gloves.Length];
            CaptureHomes();
            for (var i = 0; i < gloves.Length; i++)
            {
                if (gloves[i] == null)
                    continue;
                var index = i;
                _pokes[i] = gloves[i].GetComponent<PokeTarget>();
                if (_pokes[i] != null)
                {
                    _pokes[i].Poked += _ => OnPoke(index);
                    _pokes[i].SetInteractable(false);
                }

                if (arrows != null && i < arrows.Length && arrows[i] != null)
                    arrows[i].Pressed += _ => OnPoke(index);
            }

            BuildCard();
        }

        public void Begin()
        {
            CaptureHomes();
            AllSeen = false;
            Seen = 0;
            for (var i = 0; i < _seen.Length; i++)
                _seen[i] = false;
            _on = true;
            _current = 0;
            HideCard();
            PlaceArrow();
            SetPokes(true);
        }

        private void Update()
        {
            // the bench follows the head height and a glove travels to the hand: keep the arrow above where its glove is
            if (_on && _held < 0 && !_busy)
                PlaceArrow();
        }

        private void OnPoke(int index)
        {
            if (!_on || _busy || _held >= 0 || index != _current)
                return;
            StartCoroutine(Hold(index));
        }

        private Vector3 HandPoint(Transform cam, Vector3 fwd, Vector3 right)
        {
            return (cam != null ? cam.position : Vector3.zero) + fwd * 0.5f - right * 0.23f + Vector3.down * 0.1f;
        }

        private IEnumerator Hold(int index)
        {
            _busy = true;
            _held = index;
            SetPokes(false);
            DisarmArrows();
            var glove = gloves[index];
            var from = glove.position;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var dest = HandPoint(cam, fwd, right);
            for (var t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                glove.position = Vector3.Lerp(from, dest, Mathf.SmoothStep(0f, 1f, t / 0.4f));
                yield return null;
            }

            glove.position = dest;
            ShowCard(index);
            _busy = false;
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
            var glove = gloves[index];
            var from = glove.position;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                glove.position = Vector3.Lerp(from, HomeWorld(index), Mathf.SmoothStep(0f, 1f, t / 0.35f));
                yield return null;
            }

            glove.localPosition = _homeLocal[index];
            glove.localRotation = _homeLocalRot[index];
            _held = -1;
            _busy = false;
            if (!_seen[index])
            {
                _seen[index] = true;
                Seen++;
            }

            if (Seen >= gloves.Length)
            {
                _on = false;
                DisarmArrows();
                AllSeen = true;
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
            for (var i = 0; i < _seen.Length; i++)
                if (!_seen[i])
                    return i;
            return 0;
        }

        private void SetPokes(bool on)
        {
            for (var i = 0; i < _pokes.Length; i++)
                if (_pokes[i] != null)
                    _pokes[i].SetInteractable(on && i == _current);
        }

        private void DisarmArrows()
        {
            if (arrows == null)
                return;
            foreach (var a in arrows)
                if (a != null && a.IsArmed)
                    a.Disarm();
        }

        // The SC01 drum arrow floats above the glove that has to be touched next.
        private void PlaceArrow()
        {
            if (arrows == null || _current < 0 || _current >= gloves.Length || gloves[_current] == null)
                return;
            for (var i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null)
                    continue;
                if (i != _current)
                {
                    if (arrows[i].IsArmed)
                        arrows[i].Disarm();
                    continue;
                }

                var b = BoundsOf(gloves[_current]);
                arrows[i].SetBase(new Vector3(b.center.x, b.max.y + 0.17f, b.center.z));
                if (!arrows[i].IsArmed)
                    arrows[i].Arm(true);
            }
        }

        private void ShowCard(int index)
        {
            if (_card == null || _values == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            // eye level (head height, yaw only: works seated or standing), to the right of the glove in the hand with a gap
            var head = cam != null ? cam.position : Vector3.zero;
            _card.position = head + fwd * 0.6f + right * 0.27f + Vector3.down * 0.02f;
            _card.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            var lines = new[] { Sc04Content.Materials, Sc04Content.Ratings, Sc04Content.Times };
            for (var i = 0; i < _values.Length && i < lines.Length; i++)
                _values[i].text = index >= 0 && index < lines[i].Length ? lines[i][index] : "";
            _card.gameObject.SetActive(true);
        }

        private void HideCard()
        {
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        // The designer's hologram card (same look as the Scenario 5 mask card): title top left, close X top right, the glove
        // outline on the left, then MATERIAL / RATING / PERMEATION TIME, each with its icon, heading and value.
        private void BuildCard()
        {
            var panel = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("GloveCard", transform, new Vector2(840f, 500f), 0.00054f, true, 30);
            _card = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, panel, Color.white);
            if (art.holoPanel != null)
                plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.07f, 0.82f), new Vector2(0.80f, 0.95f),
                "GLOVE PROPERTIES", 34f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 34f;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var closeRt = PresentableUi.Rect("CloseButton", canvas.transform, new Vector2(0.872f, 0.825f), new Vector2(0.959f, 0.955f));
            var closeImg = PresentableUi.Img(closeRt, art.iconClose != null ? art.iconClose : art.cross, Color.white, raycast: true,
                preserveAspect: true);
            var close = closeRt.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImg;
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(CloseCard);
            var lift = closeRt.gameObject.AddComponent<HoverLift>();
            lift.Init(closeRt);
            close.onClick.AddListener(lift.Press);

            if (art.gloveCardGlove != null)
                PresentableUi.Img(PresentableUi.Rect("GlovePicture", canvas.transform, new Vector2(0.05f, 0.10f), new Vector2(0.31f, 0.78f)),
                    art.gloveCardGlove, Color.white, preserveAspect: true);

            var icons = new[] { art.gloveCardMaterial, art.gloveCardRating, art.gloveCardTime };
            _values = new TMP_Text[3];
            var rows = new[] { Sc04Content.Materials, Sc04Content.Ratings, Sc04Content.Times };
            const float rowH = 0.235f;
            for (var i = 0; i < 3; i++)
            {
                var y1 = 0.80f - i * rowH;
                var y0 = y1 - (rowH - 0.02f);
                var mid = y0 + (y1 - y0) * 0.5f;
                if (icons[i] != null)
                    PresentableUi.Img(PresentableUi.Rect("Icon" + i, canvas.transform, new Vector2(0.34f, y0 + 0.01f), new Vector2(0.45f, y1 - 0.01f)),
                        icons[i], Color.white, preserveAspect: true);
                var label = PresentableUi.Text("Label" + i, canvas.transform, new Vector2(0.48f, mid), new Vector2(0.96f, y1),
                    Sc04Content.RowLabels[i] + ":", 26f, HoloWhite, TextAlignmentOptions.BottomLeft, true, art.englishFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 26f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                _values[i] = PresentableUi.Text("Value" + i, canvas.transform, new Vector2(0.48f, y0), new Vector2(0.96f, mid),
                    rows[i].Length > 0 ? rows[i][0] : "", 30f, HoloCyan, TextAlignmentOptions.TopLeft, false, art.englishFont);
                _values[i].enableAutoSizing = true;
                _values[i].fontSizeMin = 14f;
                _values[i].fontSizeMax = 30f;
                _values[i].textWrappingMode = TextWrappingModes.NoWrap;
                _values[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void CaptureHomes()
        {
            if (gloves == null || _homeLocal == null)
                return;
            for (var i = 0; i < gloves.Length; i++)
            {
                if (gloves[i] == null)
                    continue;
                _homeLocal[i] = gloves[i].localPosition;
                _homeLocalRot[i] = gloves[i].localRotation;
            }
        }

        private Vector3 HomeWorld(int index)
        {
            var glove = gloves[index];
            if (glove == null || glove.parent == null)
                return _homeLocal[index];
            return glove.parent.TransformPoint(_homeLocal[index]);
        }

        private static Bounds BoundsOf(Transform t)
        {
            var follow = t.GetComponent<GloveParts>();
            var parts = follow != null ? follow.Parts : null;
            if (parts != null && parts.Length > 0)
            {
                Bounds b = default;
                var any = false;
                for (var i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null)
                        continue;
                    var rend = parts[i].GetComponent<Renderer>();
                    if (rend == null)
                        continue;
                    if (!any)
                    {
                        b = rend.bounds;
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(rend.bounds);
                    }
                }

                if (any)
                    return b;
            }

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

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 4: one pulsing arrow moves left to right across three gloves. Poking the marked glove
    /// brings it to the hand and opens a properties card at eye level on the right. The other two stay put.
    /// </summary>
    public sealed class GloveInspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform[] gloves = new Transform[3];
        [SerializeField] private Transform arrow;

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
        private Transform _gate;
        private Transform _ring;
        private Vector3 _ringBase;

        public bool NextChosen { get; private set; }
        public int Seen { get; private set; }
        public bool Holding => _held >= 0;

        public PokeTarget PokeOf(int index)
        {
            if (_pokes == null || index < 0 || index >= _pokes.Length)
                return null;
            return _pokes[index];
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
                _pokes[i] = gloves[i].GetComponent<PokeTarget>();
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
            if (_ring == null || arrow == null || !arrow.gameObject.activeSelf)
                return;
            var s = 1f + 0.07f * Mathf.Sin(Time.time * 3.1f);
            _ring.localScale = _ringBase * s;
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
            var glove = gloves[index];
            var from = glove.position;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var dest = (cam != null ? cam.position : from) + fwd * 0.5f - right * 0.16f + Vector3.down * 0.08f;
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

        private void PlaceArrow()
        {
            if (arrow == null || _current < 0 || _current >= gloves.Length || gloves[_current] == null)
                return;
            EnsureRing();
            var b = BoundsOf(gloves[_current]);
            arrow.gameObject.SetActive(true);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.16f, b.center.z);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            arrow.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Gold ring with the arrow inside, the same marker used on the SC03 drum.
        private void EnsureRing()
        {
            if (_ring != null || arrow == null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("GloveRing", arrow, new Vector2(280f, 280f), 0.00115f, false, 80);
            _ring = canvas.transform;
            _ringBase = _ring.localScale;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", _ring, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), art.radialGlow,
                    new Color(1f, 0.78f, 0.2f, 0.4f));
            var ringSprite = art.ringThin != null ? art.ringThin : art.ring;
            if (ringSprite != null)
                PresentableUi.Img("Ring", _ring, Vector2.zero, Vector2.one, ringSprite, Color.white);
            if (art.arrowDown != null)
                PresentableUi.Img("Arrow", _ring, new Vector2(0.30f, 0.16f), new Vector2(0.70f, 0.84f), art.arrowDown,
                    new Color(1f, 0.93f, 0.55f));
            PresentableUi.AlwaysOnTop(canvas);
        }

        private void ShowCard(int index)
        {
            if (_card == null || _values == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            _card.position = (cam != null ? cam.position : Vector3.zero) + fwd * 0.55f + right * 0.16f;
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

        private void ShowGate()
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            if (_gate == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            // Close enough that the fingertip meets the glass. PanelPush on the canvas stops the hand.
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
            var frame = art.frameWide != null ? art.frameWide : art.roundedPanel;
            var canvas = PresentableUi.WorldCanvas("GloveCard", transform, new Vector2(980f, 460f), 0.00052f, true, 30);
            _card = canvas.transform;
            var plate = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, frame, Color.white);
            plate.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.08f, 0.78f), new Vector2(0.78f, 0.93f),
                "GLOVE PROPERTIES", 28f, Color.white, TextAlignmentOptions.Left, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 28f;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            _values = new TMP_Text[3];
            var rows = new[] { Sc04Content.Materials, Sc04Content.Ratings, Sc04Content.Times };
            for (var i = 0; i < 3; i++)
            {
                var y1 = 0.70f - i * 0.18f;
                var y0 = y1 - 0.15f;
                RowIcon(canvas.transform, "Icon" + i, new Vector2(0.06f, y0 + 0.02f), new Vector2(0.15f, y1 - 0.02f), i);
                var label = PresentableUi.Text("Label" + i, canvas.transform, new Vector2(0.17f, y0), new Vector2(0.50f, y1),
                    Sc04Content.RowLabels[i], 24f, new Color(0.65f, 0.9f, 1f), TextAlignmentOptions.MidlineLeft, true,
                    art.englishFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                _values[i] = PresentableUi.Text("Value" + i, canvas.transform, new Vector2(0.50f, y0), new Vector2(0.94f, y1),
                    rows[i].Length > 0 ? rows[i][0] : "", 24f, Color.white, TextAlignmentOptions.MidlineLeft, false,
                    art.englishFont);
                _values[i].enableAutoSizing = true;
                _values[i].fontSizeMin = 14f;
                _values[i].fontSizeMax = 24f;
                _values[i].textWrappingMode = TextWrappingModes.NoWrap;
                _values[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            var closeRt = PresentableUi.Rect("CloseButton", canvas.transform, new Vector2(0.86f, 0.78f), new Vector2(0.96f, 0.94f));
            PresentableUi.Img(closeRt, art.cross != null ? art.cross : art.circle, Color.white, raycast: true);
            var close = closeRt.gameObject.AddComponent<Button>();
            close.targetGraphic = closeRt.GetComponent<Image>();
            close.onClick.AddListener(CloseCard);
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void RowIcon(Transform parent, string name, Vector2 min, Vector2 max, int kind)
        {
            var host = PresentableUi.Rect(name, parent, min, max);
            if (kind == 0)
            {
                PresentableUi.Img(host, art.roundedSolid, new Color(0.82f, 0.88f, 0.93f));
                PresentableUi.Img(PresentableUi.Rect("Palm", host, new Vector2(0.22f, 0.26f), new Vector2(0.78f, 0.8f)),
                    art.circle != null ? art.circle : art.roundedSolid, Color.white);
            }
            else if (kind == 1)
            {
                PresentableUi.Img(host, art.circle != null ? art.circle : art.roundedSolid, new Color(0.16f, 0.55f, 0.32f));
                if (art.check != null)
                    PresentableUi.Img(PresentableUi.Rect("Mark", host, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f)),
                        art.check, Color.white);
            }
            else
            {
                var ring = art.ringThin != null ? art.ringThin : (art.ring != null ? art.ring : art.circle);
                PresentableUi.Img(host, ring, new Color(0.95f, 0.78f, 0.22f));
            }
        }

        private void BuildGate()
        {
            var frame = art.frameWide != null ? art.frameWide : art.roundedPanel;
            var canvas = PresentableUi.WorldCanvas("GloveGate", transform, new Vector2(980f, 520f), 0.00055f, true, 31);
            _gate = canvas.transform;
            PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, frame, Color.white);
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.92f),
                Sc04Content.GateTitle, 30f, Color.white, TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 30f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            PresentableUi.Text("Question", canvas.transform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.72f),
                Sc04Content.GateQuestion, 24f, Color.white, TextAlignmentOptions.Center, false, art.englishFont);
            PresentableUi.Text("Hint", canvas.transform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.52f),
                Sc04Content.GateBody, 20f, new Color(0.8f, 0.9f, 0.95f), TextAlignmentOptions.Center, false, art.englishFont);
            MakeButton("BackButton", canvas.transform, new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.30f),
                "\u21A9  BACK", null, OnBack);
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

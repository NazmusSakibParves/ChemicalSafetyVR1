using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 12 PPE Donning Station. One pick round per locker (goggles / face shield, gloves, respirators, apron): the PPE SELECTION
    /// PANEL (workflow step04/05) lists four options in shuffled order, a finger poke on a row brings that item from the locker to
    /// the user's hands, a green RIGHT CHOICE card (the item is put on and ticked on the hologram) or a red WRONG CHOICE card (the item
    /// goes back, try again). After the fourth right pick the REVIEW hologram (step06: the donned operator + the checklist) is shown.
    /// The director drives it: <see cref="BeginRound"/>, wait for <see cref="Result"/>, <see cref="EndRound"/>.
    /// </summary>
    public sealed class Sc12Station : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [Tooltip("round * 4 + option (0 goggles/face shield, 1 gloves, 2 respirators, 3 aprons/suits); option 0 is the right one")]
        [SerializeField] private Transform[] items = new Transform[16];

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color RowIdle = new Color(0.8f, 0.95f, 1f, 0.9f);

        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private Vector3[] _homeScale;
        private Transform _panel;
        private TextMeshProUGUI[] _labels;
        private Image[] _boxes;
        private Image[] _rows;
        private Image[] _marks;
        private Transform _card;
        private Image _cardFrame;
        private TextMeshProUGUI _cardTitle;
        private TextMeshProUGUI _cardBody;
        private TextMeshProUGUI _prompt;
        private Transform _review;
        private Image[] _reviewChecks;
        private readonly int[] _order = new int[4];
        private int _round = -1;
        private bool _busy;
        private bool _open;
        private readonly bool[] _worn = new bool[4];

        /// <summary>0 = nothing yet, 1 = the right item was picked and put on, 2 = a wrong item (it is back in the locker).</summary>
        public int Result { get; private set; }

        public int Round => _round;

        /// <summary>Raised when NEXT on the review hologram is pressed.</summary>
        public event Action ReviewNext;
        public bool PanelOpen => _panel != null && _panel.gameObject.activeSelf;
        public int WornCount
        {
            get
            {
                var n = 0;
                foreach (var w in _worn)
                    if (w)
                        n++;
                return n;
            }
        }

        /// <summary>Fires for every poke on an option row (round, option, right).</summary>
        public event Action<int, int, bool> Picked;

        private void Start()
        {
            CaptureHomes();
            BuildPanel();
            BuildCard();
            BuildReview();
        }

        private void CaptureHomes()
        {
            if (_homePos != null && _homePos.Length == items.Length)
                return;
            Debug.Log("[Sc12Station] capturing homes for " + items.Length + " items (had " + (_homePos != null ? _homePos.Length : -1) + ")");
            _homePos = new Vector3[items.Length];
            _homeRot = new Quaternion[items.Length];
            _homeScale = new Vector3[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                _homePos[i] = items[i].position;
                _homeRot[i] = items[i].rotation;
                _homeScale[i] = items[i].localScale;
            }
        }

        public Transform Item(int round, int option) => items[round * 4 + option];

        /// <summary>Everything back in its locker, panels closed, nothing worn.</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            CaptureHomes();
            _busy = false;
            _open = false;
            _round = -1;
            Result = 0;
            for (var i = 0; i < _worn.Length; i++)
                _worn[i] = false;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;
                items[i].gameObject.SetActive(true);
                items[i].SetPositionAndRotation(_homePos[i], _homeRot[i]);
                items[i].localScale = _homeScale[i];
            }

            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (_card != null)
                _card.gameObject.SetActive(false);
            if (_review != null)
                _review.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ one pick round

        public void BeginRound(int round)
        {
            if (_panel == null)
                Start();
            _round = round;
            Result = 0;
            _busy = false;
            // shuffled order of the four options (the right one is not always on top)
            var pool = new[] { 0, 1, 2, 3 };
            for (var i = pool.Length - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var names = Sc12Content.Options(round);
            for (var r = 0; r < 4; r++)
            {
                _order[r] = pool[r];
                _labels[r].text = names[pool[r]];
                _marks[r].gameObject.SetActive(false);
                _boxes[r].gameObject.SetActive(true);
                _rows[r].color = RowIdle;
            }

            _prompt.text = Sc12Content.PickPrompt[round];
            _card.gameObject.SetActive(false);
            PlacePanel();
            _open = true;
            _panel.gameObject.SetActive(true);
            StartCoroutine(PopIn(_panel));
        }

        /// <summary>Closes the panel and the card (call after a right pick, once the item is on).</summary>
        public void EndRound()
        {
            _open = false;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        /// <summary>After a wrong pick: the panel accepts the next try.</summary>
        public void Retry()
        {
            Result = 0;
            _busy = false;
            _card.gameObject.SetActive(false);
            for (var r = 0; r < 4; r++)
            {
                _marks[r].gameObject.SetActive(false);
                _boxes[r].gameObject.SetActive(true);
                _rows[r].color = RowIdle;
            }
        }

        private void PlacePanel()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? cam.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            var head = cam != null ? cam.position : Vector3.zero;
            // left of the line of sight so the locker stays visible; the verdict card goes to its right
            _panel.position = head + fwd * 0.62f - right * 0.24f + Vector3.up * (-0.02f);
            _panel.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _card.position = head + fwd * 0.60f + right * 0.30f + Vector3.up * (-0.02f);
            _card.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private void OnRow(int row)
        {
            if (!_open || _busy || _round < 0)
                return;
            var option = _order[row];
            var right = option == 0;
            _busy = true;
            _marks[row].gameObject.SetActive(true);
            _marks[row].sprite = right ? art.check : art.cross;
            _marks[row].color = right ? PresentableUi.Green : PresentableUi.Red;
            _boxes[row].gameObject.SetActive(false);
            _rows[row].color = right ? new Color(0.6f, 1f, 0.8f, 1f) : new Color(1f, 0.7f, 0.68f, 1f);
            ShowCard(right);
            PresentableFx.Play(right ? PresentableFx.Success : PresentableFx.Error, 0.55f);
            PresentableFx.Haptic(true, right ? 0.4f : 0.7f, 0.08f);
            Picked?.Invoke(_round, option, right);
            StartCoroutine(Take(_round * 4 + option, right));
        }

        private void ShowCard(bool right)
        {
            _cardFrame.sprite = right ? art.frameCorrect : art.frameWrong;
            _cardTitle.text = right ? Sc12Content.RightTitle : Sc12Content.WrongTitle;
            _cardBody.text = right ? Sc12Content.RightBody : Sc12Content.WrongBody;
            _cardBody.color = right ? new Color(0.86f, 1f, 0.9f) : new Color(1f, 0.94f, 0.94f);
            _card.gameObject.SetActive(true);
            StartCoroutine(PopIn(_card));
        }

        // The picked item flies from the locker to the user's hands. Right: it is put on (slides into the body, the review gets its
        // tick). Wrong: it is shown for a moment and flies back to its shelf.
        private IEnumerator Take(int index, bool right)
        {
            var item = items[index];
            if (item == null)
            {
                Result = right ? 1 : 2;
                yield break;
            }

            var from = item.position;
            var fromRot = item.rotation;
            var fromScale = item.localScale;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var head = cam != null ? cam.position : from;
            var dest = head + fwd * 0.40f + Vector3.down * 0.22f;
            var destRot = Quaternion.LookRotation(-fwd, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
            var bounds = BoundsOf(item);
            var holdScale = Mathf.Clamp(0.34f / Mathf.Max(0.05f, bounds.size.y), 0.6f, 2.2f);
            for (var t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                item.position = Vector3.Lerp(from, dest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                item.rotation = Quaternion.Slerp(fromRot, destRot, k);
                item.localScale = Vector3.Lerp(fromScale, _homeScale[index] * holdScale, k);
                yield return null;
            }

            // a slow turn so the user can look at it
            var spin = 0f;
            for (var t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                spin += Time.deltaTime * 70f;
                item.rotation = destRot * Quaternion.Euler(0f, Mathf.Sin(spin * Mathf.Deg2Rad) * 25f, 0f);
                yield return null;
            }

            if (right)
            {
                var held = item.position;
                for (var t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    var k = t / 0.4f;
                    item.position = Vector3.Lerp(held, head + Vector3.down * 0.30f + fwd * 0.10f, k);
                    item.localScale = Vector3.Lerp(_homeScale[index] * holdScale, _homeScale[index] * 0.02f, k * k);
                    yield return null;
                }

                item.gameObject.SetActive(false);
                _worn[_round] = true;
                PresentableFx.Haptic(true, 0.4f, 0.08f);
                Result = 1;
            }
            else
            {
                var held = item.position;
                var heldRot = item.rotation;
                var heldScale = item.localScale;
                for (var t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                    item.position = Vector3.Lerp(held, _homePos[index], k);
                    item.rotation = Quaternion.Slerp(heldRot, _homeRot[index], k);
                    item.localScale = Vector3.Lerp(heldScale, _homeScale[index], k);
                    yield return null;
                }

                item.SetPositionAndRotation(_homePos[index], _homeRot[index]);
                item.localScale = _homeScale[index];
                Result = 2;
            }
        }

        // ------------------------------------------------------------------ review hologram (workflow step06)

        public void ShowReview(bool on)
        {
            if (_review == null)
                Start();
            if (!on)
            {
                _review.gameObject.SetActive(false);
                return;
            }

            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? cam.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;
            var head = cam != null ? cam.position : Vector3.zero;
            _review.SetPositionAndRotation(head + fwd * 0.85f + Vector3.up * (-0.02f), Quaternion.LookRotation(fwd, Vector3.up));
            foreach (var c in _reviewChecks)
                c.gameObject.SetActive(false);
            _review.gameObject.SetActive(true);
            StartCoroutine(PopIn(_review));
            StartCoroutine(TickReview());
        }

        private IEnumerator TickReview()
        {
            for (var i = 0; i < _reviewChecks.Length; i++)
            {
                yield return PresentableFx.Wait(0.55f);
                _reviewChecks[i].gameObject.SetActive(true);
                PresentableFx.Play(PresentableFx.Chime, 0.5f);
            }
        }

        // ------------------------------------------------------------------ UI

        private IEnumerator PopIn(Transform t)
        {
            var group = t.GetComponent<CanvasGroup>();
            var baseScale = Vector3.one * (t == _review ? 0.00066f : t == _card ? 0.0006f : 0.00064f);
            t.localScale = baseScale * 0.85f;
            if (group != null)
                group.alpha = 0f;
            for (var s = 0f; s < 0.3f; s += Time.deltaTime)
            {
                var k = PresentableFx.EaseOutBack(s / 0.3f);
                t.localScale = baseScale * Mathf.Lerp(0.85f, 1f, k);
                if (group != null)
                    group.alpha = Mathf.Clamp01(s / 0.2f);
                yield return null;
            }

            t.localScale = baseScale;
            if (group != null)
                group.alpha = 1f;
        }

        private void BuildPanel()
        {
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("Sc12Panel", transform, new Vector2(760f, 700f), 0.00064f, true, 30);
            _panel = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null)
                bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.97f),
                Sc12Content.PanelTitle, 44f, new Color(0.62f, 0.9f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 22f;
            title.fontSizeMax = 44f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            _prompt = PresentableUi.Text("Prompt", canvas.transform, new Vector2(0.07f, 0.765f), new Vector2(0.93f, 0.85f),
                "", 28f, HoloWhite, TextAlignmentOptions.Center, false, art.englishFont);
            _prompt.enableAutoSizing = true;
            _prompt.fontSizeMin = 16f;
            _prompt.fontSizeMax = 28f;
            var line = PresentableUi.Rect("Divider", canvas.transform, new Vector2(0.08f, 0.755f), new Vector2(0.92f, 0.762f));
            PresentableUi.Img(line, art.roundedSolid, new Color(0.55f, 0.9f, 1f, 0.5f));

            _labels = new TextMeshProUGUI[4];
            _boxes = new Image[4];
            _rows = new Image[4];
            _marks = new Image[4];
            for (var i = 0; i < 4; i++)
            {
                var top = 0.735f - i * 0.158f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.07f, top - 0.138f), new Vector2(0.93f, top));
                _rows[i] = PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, RowIdle, true, true);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = _rows[i];
                btn.transition = Selectable.Transition.None;
                var index = i;
                btn.onClick.AddListener(() => OnRow(index));
                var lift = row.gameObject.AddComponent<HoverLift>();
                lift.Init(row);
                btn.onClick.AddListener(lift.Press);
                _labels[i] = PresentableUi.Text("Label", row, new Vector2(0.05f, 0.05f), new Vector2(0.78f, 0.95f),
                    "", 36f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 18f;
                _labels[i].fontSizeMax = 36f;
                _labels[i].textWrappingMode = TextWrappingModes.Normal;
                var box = PresentableUi.Rect("Box", row, new Vector2(0.82f, 0.2f), new Vector2(0.94f, 0.8f));
                _boxes[i] = PresentableUi.Img(box, art.roundedSolid, new Color(0.7f, 0.95f, 1f, 0.28f));
                var mk = PresentableUi.Rect("Mark", row, new Vector2(0.80f, 0.08f), new Vector2(0.96f, 0.92f));
                _marks[i] = PresentableUi.Img(mk, art.check, Color.white, false, false, true);
                _marks[i].gameObject.SetActive(false);
            }

            PresentableUi.AlwaysOnTop(canvas);
            _panel.gameObject.SetActive(false);
        }

        // Same layout as the MCQ right / wrong popups: the frame art is 900 x 482, the title sits in its header band.
        private void BuildCard()
        {
            var canvas = PresentableUi.WorldCanvas("Sc12Card", transform, new Vector2(900f, 482f), 0.0006f, false, 32);
            _card = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            _cardFrame = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, art.frameCorrect, Color.white);
            _cardTitle = PresentableUi.Text("Title", canvas.transform, new Vector2(0.18f, 0.855f), new Vector2(0.82f, 0.93f),
                "", 42f, Color.white, TextAlignmentOptions.Center, true, art.englishFont);
            _cardTitle.enableAutoSizing = true;
            _cardTitle.fontSizeMin = 14f;
            _cardTitle.fontSizeMax = 42f;
            _cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _cardBody = PresentableUi.Text("Body", canvas.transform, new Vector2(0.16f, 0.22f), new Vector2(0.84f, 0.74f),
                "", 34f, HoloWhite, TextAlignmentOptions.Center, false, art.englishFont);
            _cardBody.enableAutoSizing = true;
            _cardBody.fontSizeMin = 16f;
            _cardBody.fontSizeMax = 46f;
            _cardBody.textWrappingMode = TextWrappingModes.Normal;
            _cardBody.overflowMode = TextOverflowModes.Truncate;
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void BuildReview()
        {
            var canvas = PresentableUi.WorldCanvas("Sc12Review", transform, new Vector2(1100f, 760f), 0.00066f, true, 30);
            _review = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, new Color(1f, 1f, 1f, 0.92f));
            if (art.holoPanel != null)
                bg.type = Image.Type.Sliced;
            // left: the donned operator
            var glow = PresentableUi.Rect("OperatorGlow", canvas.transform, new Vector2(0.03f, 0.06f), new Vector2(0.43f, 0.94f));
            PresentableUi.Img(glow, art.radialGlow, new Color(0.3f, 0.9f, 1f, 0.28f));
            if (art.operatorSc12 != null)
            {
                var op = PresentableUi.Rect("Operator", canvas.transform, new Vector2(0.05f, 0.07f), new Vector2(0.41f, 0.93f));
                PresentableUi.Img(op, art.operatorSc12, Color.white, preserveAspect: true);
            }

            // right: CORROSIVE ACID COMPATIBLE PPE
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.46f, 0.82f), new Vector2(0.96f, 0.96f),
                Sc12Content.ReviewTitle, 40f, new Color(0.62f, 0.95f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 40f;
            var n = Sc12Content.ReviewItems.Length;
            _reviewChecks = new Image[n];
            for (var i = 0; i < n; i++)
            {
                var top = 0.80f - i * 0.16f;
                var row = PresentableUi.Rect("Item" + i, canvas.transform, new Vector2(0.46f, top - 0.14f), new Vector2(0.96f, top));
                PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, RowIdle, true);
                if (art.iconSc12Ppe != null && i < art.iconSc12Ppe.Length && art.iconSc12Ppe[i] != null)
                {
                    var ic = PresentableUi.Rect("Icon", row, new Vector2(0.03f, 0.1f), new Vector2(0.24f, 0.9f));
                    PresentableUi.Img(ic, art.iconSc12Ppe[i], Color.white, preserveAspect: true);
                }

                var label = PresentableUi.Text("Label", row, new Vector2(0.27f, 0.05f), new Vector2(0.80f, 0.95f),
                    Sc12Content.ReviewItems[i], 34f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 34f;
                var ck = PresentableUi.Rect("Check", row, new Vector2(0.82f, 0.1f), new Vector2(0.97f, 0.9f));
                _reviewChecks[i] = PresentableUi.Img(ck, art.check, PresentableUi.Green, false, false, true);
                _reviewChecks[i].gameObject.SetActive(false);
            }

            var nextRt = PresentableUi.Rect("NextButton", canvas.transform, new Vector2(0.60f, 0.03f), new Vector2(0.96f, 0.155f));
            var nextImg = PresentableUi.Img(nextRt, art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton, Color.white, true, true);
            var nextBtn = nextRt.gameObject.AddComponent<Button>();
            nextBtn.targetGraphic = nextImg;
            nextBtn.transition = Selectable.Transition.None;
            nextBtn.onClick.AddListener(() => ReviewNext?.Invoke());
            var nextLift = nextRt.gameObject.AddComponent<HoverLift>();
            nextLift.Init(nextRt);
            nextBtn.onClick.AddListener(nextLift.Press);
            var nextText = PresentableUi.Text("Label", nextRt, new Vector2(0.08f, 0.08f), new Vector2(0.70f, 0.92f),
                Sc12Content.NextLabel, 34f, HoloWhite, TextAlignmentOptions.Center, true, art.englishFont);
            nextText.enableAutoSizing = true;
            nextText.fontSizeMin = 18f;
            nextText.fontSizeMax = 34f;
            nextText.textWrappingMode = TextWrappingModes.NoWrap;
            if (art.iconNext != null)
                PresentableUi.Img(PresentableUi.Rect("Icon", nextRt, new Vector2(0.72f, 0.18f), new Vector2(0.92f, 0.82f)), art.iconNext, Color.white, preserveAspect: true);
            PresentableUi.AlwaysOnTop(canvas);
            _review.gameObject.SetActive(false);
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

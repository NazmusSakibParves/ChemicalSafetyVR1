using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 12 "PPE SELECTION ASSESSMENT" (workflow step08/09): a multiple-answer question. Every row has a check box that a finger
    /// poke toggles, SUBMIT checks the whole selection. Right: green "Right Choice!" card and NEXT. Wrong: red "Wrong Choice!" card
    /// (the boots text when a wrong item is ticked, the missing-item text otherwise); the selection stays and can be edited and submitted again.
    /// </summary>
    public sealed class Sc12Assessment : MonoBehaviour
    {
        [Serializable]
        public sealed class Question
        {
            public string header;
            public string prompt;
            public string[] labels;
            public bool[] correct;
            public string submitLabel;
            public string nextLabel;
            public string rightTitle;
            public string rightBody;
            public string wrongTitle;
            public string wrongItemBody;
            public string missedBody;
        }

        [SerializeField] private PresentableArt art;

        private static readonly Color HoloWhite = new Color(0.96f, 0.99f, 1f);
        private static readonly Color RowIdle = new Color(0.8f, 0.95f, 1f, 0.9f);
        private static readonly Color RowOn = new Color(0.55f, 0.92f, 1f, 1f);

        private const float CardScale = 0.95f;
        private Transform _panel;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI[] _labels;
        private Image[] _rows;
        private Image[] _checks;
        private Transform _card;
        private Image _cardFrame;
        private TextMeshProUGUI _cardTitle;
        private TextMeshProUGUI _cardBody;
        private Button _submit;
        private Image _submitImg;
        private TextMeshProUGUI _submitText;
        private Button _next;
        private TextMeshProUGUI _nextText;
        private Question _q;
        private bool[] _on;
        private bool _locked;
        private bool _won;
        private Coroutine _cardHide;

        /// <summary>Raised on SUBMIT: (selection by shown row, all right).</summary>
        public event Action<bool[], bool> Submitted;

        public event Action NextPressed;

        public bool Visible => _panel != null && _panel.gameObject.activeSelf;
        public bool Won => _won;
        public int RowCount => _labels != null ? _labels.Length : 0;

        private void Start()
        {
            Build();
        }

        // ------------------------------------------------------------------ driver API (also used by the smoke test)

        public void Show(Question q)
        {
            if (_panel == null)
                Build();
            _q = q;
            _on = new bool[RowCount];
            _won = false;
            _locked = false;
            _header.text = q.header;
            _prompt.text = q.prompt;
            for (var i = 0; i < RowCount; i++)
            {
                _labels[i].text = q.labels[i];
                _checks[i].gameObject.SetActive(false);
                _rows[i].color = RowIdle;
            }

            _submitText.text = q.submitLabel;
            _nextText.text = q.nextLabel;
            _next.gameObject.SetActive(false);
            _submit.gameObject.SetActive(true);
            _card.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            StartCoroutine(PopIn());
        }

        public void Hide()
        {
            if (_cardHide != null)
                StopCoroutine(_cardHide);
            _cardHide = null;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        public void ToggleRow(int row)
        {
            if (_locked || row < 0 || row >= RowCount || !Visible)
                return;
            _on[row] = !_on[row];
            _checks[row].gameObject.SetActive(_on[row]);
            _rows[row].color = _on[row] ? RowOn : RowIdle;
            if (_card.gameObject.activeSelf && !_won)
                _card.gameObject.SetActive(false);
            PresentableFx.Play(PresentableFx.Tick, 0.7f);
        }

        public void PressSubmit()
        {
            if (_locked || !Visible)
                return;
            var any = false;
            var wrongTicked = false;
            var allRight = true;
            for (var i = 0; i < RowCount; i++)
            {
                any |= _on[i];
                if (_on[i] != _q.correct[i])
                    allRight = false;
                if (_on[i] && !_q.correct[i])
                    wrongTicked = true;
            }

            if (!any)
                return;
            _cardFrame.sprite = allRight ? art.frameCorrect : art.frameWrong;
            _cardTitle.text = allRight ? _q.rightTitle : _q.wrongTitle;
            _cardBody.text = allRight ? _q.rightBody : (wrongTicked ? _q.wrongItemBody : _q.missedBody);
            _cardBody.color = allRight ? new Color(0.86f, 1f, 0.9f) : new Color(1f, 0.94f, 0.94f);
            _card.gameObject.SetActive(true);
            StartCoroutine(PopCard());
            PresentableFx.Play(allRight ? PresentableFx.Success : PresentableFx.Error, 0.55f);
            PresentableFx.Haptic(true, allRight ? 0.4f : 0.7f, 0.08f);
            // the ticked rows show their verdict
            for (var i = 0; i < RowCount; i++)
            {
                if (!_on[i])
                    continue;
                _checks[i].sprite = _q.correct[i] ? art.check : art.cross;
                _checks[i].color = _q.correct[i] ? PresentableUi.Green : PresentableUi.Red;
            }

            if (allRight)
            {
                _won = true;
                _locked = true;
                _submit.gameObject.SetActive(false);
                _next.gameObject.SetActive(true);
            }
            else
            {
                if (_cardHide != null)
                    StopCoroutine(_cardHide);
                _cardHide = StartCoroutine(HideCardLater());
            }

            Submitted?.Invoke((bool[])_on.Clone(), allRight);
        }

        public void PressNext()
        {
            if (_won)
                NextPressed?.Invoke();
        }

        private IEnumerator HideCardLater()
        {
            yield return PresentableFx.Wait(4f);
            if (!_won)
            {
                _card.gameObject.SetActive(false);
                for (var i = 0; i < RowCount; i++)
                {
                    _checks[i].sprite = art.check;
                    _checks[i].color = Color.white;
                }
            }

            _cardHide = null;
        }

        // ------------------------------------------------------------------ build

        private IEnumerator PopIn()
        {
            var baseScale = Vector3.one * 0.00064f;
            var canvas = _panel.GetComponent<CanvasGroup>();
            _panel.localScale = baseScale * 0.85f;
            if (canvas != null)
                canvas.alpha = 0f;
            for (var t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                var k = PresentableFx.EaseOutBack(t / 0.3f);
                _panel.localScale = baseScale * Mathf.Lerp(0.85f, 1f, k);
                if (canvas != null)
                    canvas.alpha = Mathf.Clamp01(t / 0.2f);
                yield return null;
            }

            _panel.localScale = baseScale;
            if (canvas != null)
                canvas.alpha = 1f;
        }

        private IEnumerator PopCard()
        {
            var baseScale = Vector3.one * CardScale;
            _card.localScale = baseScale * 0.85f;
            for (var t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                _card.localScale = baseScale * Mathf.Lerp(0.85f, 1f, PresentableFx.EaseOutBack(t / 0.25f));
                yield return null;
            }

            _card.localScale = baseScale;
        }

        private Button HoloButton(string name, Transform parent, Vector2 min, Vector2 max, Sprite bg, out TextMeshProUGUI text, out Image img)
        {
            var rt = PresentableUi.Rect(name, parent, min, max);
            img = PresentableUi.Img(rt, bg, Color.white, true, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            btn.onClick.AddListener(lift.Press);
            text = PresentableUi.Text("Label", rt, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), "", 36f, HoloWhite,
                TextAlignmentOptions.Center, true, art.englishFont);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 36f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return btn;
        }

        private void Build()
        {
            if (_panel != null || art == null)
                return;
            const int rows = 4;
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("Sc12AssessPanel", transform, new Vector2(820f, 780f), 0.00064f, true, 30);
            _panel = canvas.transform;
            canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null)
                bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            _header = PresentableUi.Text("Header", canvas.transform, new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.935f), "", 40f,
                new Color(0.7f, 0.93f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            _header.enableAutoSizing = true;
            _header.fontSizeMin = 20f;
            _header.fontSizeMax = 40f;
            _header.textWrappingMode = TextWrappingModes.NoWrap;
            _prompt = PresentableUi.Text("Prompt", canvas.transform, new Vector2(0.07f, 0.745f), new Vector2(0.93f, 0.85f), "", 34f,
                HoloWhite, TextAlignmentOptions.Center, false, art.englishFont);
            _prompt.enableAutoSizing = true;
            _prompt.fontSizeMin = 18f;
            _prompt.fontSizeMax = 34f;

            _labels = new TextMeshProUGUI[rows];
            _rows = new Image[rows];
            _checks = new Image[rows];
            for (var i = 0; i < rows; i++)
            {
                var top = 0.73f - i * 0.135f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.07f, top - 0.118f), new Vector2(0.93f, top));
                _rows[i] = PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, RowIdle, true, true);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = _rows[i];
                btn.transition = Selectable.Transition.None;
                var index = i;
                btn.onClick.AddListener(() => ToggleRow(index));
                var lift = row.gameObject.AddComponent<HoverLift>();
                lift.Init(row);
                btn.onClick.AddListener(lift.Press);
                var box = PresentableUi.Rect("Box", row, new Vector2(0.03f, 0.18f), new Vector2(0.11f, 0.82f));
                PresentableUi.Img(box, art.roundedSolid, new Color(0.7f, 0.95f, 1f, 0.28f));
                var ck = PresentableUi.Rect("Check", row, new Vector2(0.02f, 0.06f), new Vector2(0.125f, 0.94f));
                _checks[i] = PresentableUi.Img(ck, art.check, Color.white, false, false, true);
                _checks[i].gameObject.SetActive(false);
                _labels[i] = PresentableUi.Text("Label", row, new Vector2(0.15f, 0.04f), new Vector2(0.97f, 0.96f), "", 34f, HoloWhite,
                    TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                _labels[i].enableAutoSizing = true;
                _labels[i].fontSizeMin = 16f;
                _labels[i].fontSizeMax = 34f;
                _labels[i].textWrappingMode = TextWrappingModes.Normal;
            }

            var holo = art.holoButtonPrimary != null ? art.holoButtonPrimary : art.holoButton;
            _submit = HoloButton("SubmitButton", canvas.transform, new Vector2(0.30f, 0.055f), new Vector2(0.70f, 0.18f), holo, out _submitText, out _submitImg);
            _submit.onClick.AddListener(PressSubmit);
            _next = HoloButton("NextButton", canvas.transform, new Vector2(0.30f, 0.055f), new Vector2(0.70f, 0.18f), holo, out _nextText, out _);
            _next.onClick.AddListener(PressNext);
            _next.gameObject.SetActive(false);
            PresentableUi.AlwaysOnTop(canvas);

            // verdict card (MCQ popup layout, 900 x 482 frame): overlaps the left edge of the panel like the workflow picture
            var cardCanvas = PresentableUi.WorldCanvas("Sc12AssessCard", _panel, new Vector2(900f, 482f), 1f, false, 32);
            _card = cardCanvas.transform;
            _card.localScale = Vector3.one * CardScale;
            var crt = (RectTransform)_card;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0.62f, 0.5f);
            crt.anchoredPosition = new Vector2(-150f, 130f);
            cardCanvas.gameObject.AddComponent<CanvasGroup>();
            _cardFrame = PresentableUi.Img("Frame", cardCanvas.transform, Vector2.zero, Vector2.one, art.frameCorrect, Color.white);
            _cardTitle = PresentableUi.Text("Title", cardCanvas.transform, new Vector2(0.18f, 0.855f), new Vector2(0.82f, 0.93f), "", 30f,
                Color.white, TextAlignmentOptions.Center, true, art.englishFont);
            _cardTitle.enableAutoSizing = true;
            _cardTitle.fontSizeMin = 14f;
            _cardTitle.fontSizeMax = 42f;
            _cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _cardBody = PresentableUi.Text("Body", cardCanvas.transform, new Vector2(0.16f, 0.22f), new Vector2(0.84f, 0.74f), "", 34f,
                HoloWhite, TextAlignmentOptions.Center, false, art.englishFont);
            _cardBody.enableAutoSizing = true;
            _cardBody.fontSizeMin = 16f;
            _cardBody.fontSizeMax = 46f;
            _cardBody.textWrappingMode = TextWrappingModes.Normal;
            _cardBody.overflowMode = TextOverflowModes.Truncate;
            PresentableUi.AlwaysOnTop(cardCanvas);
            _card.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
        }
    }
}

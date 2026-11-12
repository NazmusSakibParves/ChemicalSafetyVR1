using System;
using System.Collections;
using System.Collections.Generic;
using ChemicalSafety.Localization;
using ChemicalSafety.Results;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// "Supervisor Pre-Task Safety Check" (scenarios 13-18, one runner for all six). Everything that changes between the scenarios
    /// is data: Resources/Content/SCnn.json (texts, three questions with four options, the SDS sheet names), optional pictures
    /// Resources/ScCheck/SCnn/{label,poster,sds1,sds2,sds3}.png (they fall back to the SC13 pictures) and voice-over clips
    /// Resources/ScCheckVo/{bn,en}/SCnn_Key.mp3 (made by Tools/generate_sccheck_vo.py). The flow: instruction card, start card, the
    /// chemical on the table, three MCQs with a scrollable SDS side panel, the round results with NEXT ROUND.
    /// </summary>
    public sealed class ScCheckRunner : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private LabSupervisor supervisor;
        [SerializeField] private Renderer labelRenderer;
        [SerializeField] private Renderer posterRenderer;
        [SerializeField] private Transform pointTarget;
        [SerializeField] private Sc04BenchHeight bench;
        [SerializeField] private Transform viewStand;  // where the learner is taken after the instruction (close to the table)
        [SerializeField] private Transform chemAnchor; // point above the chemical: popups sit above it
        [SerializeField] private GameObject[] chemProps; // one chemical per scenario 13..18 (index n-13)
        [SerializeField] private Transform supStart;
        [SerializeField] private Transform supStand;
        [SerializeField] private Transform supSide;

        private static readonly Color HoloWhite = new Color(0.93f, 0.98f, 1f);
        private static readonly Color HoloCyan = new Color(0.55f, 0.88f, 1f);
        private static readonly Color Good = new Color(0.40f, 1f, 0.55f);
        private static readonly Color Bad = new Color(1f, 0.38f, 0.34f);
        private const float Scale = 0.0008f;

        private string _sc; // "SC13"
        private Transform _cam;
        private AudioSource _audio;
        private Canvas _main;
        private CanvasGroup _mainGroup;
        private Canvas _results;
        private Canvas _card;

        // question panel
        private TextMeshProUGUI _title, _chemLabel, _chemName, _mcq, _prompt, _verdict, _sdsToggleText, _nextText, _reason;
        private readonly TextMeshProUGUI[] _optText = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _optLetter = new TextMeshProUGUI[4];
        private readonly Image[] _optBg = new Image[4];
        private RectTransform _nextBtn, _sdsPanel, _sdsContent, _roundBtn;
        private ScrollRect _sdsScroll;
        private ScrollZone _sdsZone;
        private Image _sdsSheet;
        private bool _sdsOpen;
        private Coroutine _sdsAnim;

        // card + results
        private TextMeshProUGUI _cardText, _cardButtonText, _cardTitle, _cardSub, _refTitle;
        private Image _cardIcon;
        private RectTransform _cardButton;
        private Image _cardButtonImg;
        private TextMeshProUGUI _resTitle, _resScore, _resMsg, _resNextText, _resBadge;
        private Image _verdictBg;
        private readonly TextMeshProUGUI[] _resRows = new TextMeshProUGUI[3];
        private RectTransform _resNext;

        // state the smoke test and the finger read
        private int _answered = -1;
        private bool _next, _locked;
        private int _correctShown;
        private bool _cardPressed;
        private Localization.LQuestion _currentQuestion;

        /// <summary>The director shows the instruction with the SAME card as Scenarios 6 / 8 / 9 (text, voice clip, "pressed" probe for the smoke test).</summary>
        public System.Func<string, AudioClip, System.Func<bool>, IEnumerator> InstructionRoutine;
        public Transform ViewStand => viewStand;
        private bool _hookWaiting;

        public bool SdsOpen => _sdsOpen;
        public int CorrectRow => _correctShown;
        public bool QuestionVisible => _main != null && _main.gameObject.activeSelf;
        public bool AnswerLocked => _locked;
        public Transform MainPanel => _main != null ? _main.transform : null;
        public Transform ResultsPanel => _results != null ? _results.transform : null;
        public Transform CardPanel => _card != null ? _card.transform : null;
        public string Stage { get; private set; } = "idle";
        public int LastScore { get; private set; }

        private InputAction _sdsKey;

        // the left controller's X button opens / hides the reference SDS in every scenario; here it does the same for the SDS panel beside the question
        private void OnEnable()
        {
            _sdsKey = new InputAction("ScCheckSds", InputActionType.Button);
            _sdsKey.AddBinding("<XRController>{LeftHand}/primaryButton");
            _sdsKey.AddBinding("<Keyboard>/c");
            _sdsKey.performed += _ =>
            {
                if (QuestionVisible)
                    ToggleSds();
            };
            _sdsKey.Enable();
            // the thumbsticks scroll the sheet up and down (the sheet is taller than its window)
            _sdsStick = new InputAction("ScCheckSdsScroll", InputActionType.Value);
            _sdsStick.AddBinding("<XRController>{RightHand}/thumbstick");
            _sdsStick.AddBinding("<XRController>{LeftHand}/thumbstick");
            _sdsStick.Enable();
        }

        private InputAction _sdsStick;

        private void OnDisable()
        {
            _sdsStick?.Dispose();
            _sdsStick = null;
            _sdsKey?.Dispose();
            _sdsKey = null;
        }

        private void Update()
        {
            if (_sdsStick != null && _sdsOpen && QuestionVisible)
            {
                var y = _sdsStick.ReadValue<Vector2>().y;
                if (Mathf.Abs(y) > 0.2f)
                    ScrollSds(y * Time.unscaledDeltaTime * 0.8f);
            }

            // the table stands a comfortable look-down below the eyes, seated or standing
            if (bench != null && _cam != null && Stage != "idle" && Stage != "done")
                bench.Follow(_cam.position.y);
        }

        // ------------------------------------------------------------------ data

        private string Str(string key) => Loc.T(_sc, key);

        private static Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);

        private Sprite SdsSprite(int q)
        {
            var key = "sds" + (q + 1);
            var s = LoadSprite("ScCheck/" + _sc + "/" + key);
            return s != null ? s : LoadSprite("ScCheck/" + key);
        }

        private AudioClip Clip(string key)
        {
            var c = Resources.Load<AudioClip>("ScCheckVo/" + Loc.Suffix() + "/" + _sc + "_" + key);
            return c;
        }

        private IEnumerator Say(string key)
        {
            var clip = Clip(key);
            if (clip == null)
            {
                yield return PresentableFx.Wait(1.2f);
                yield break;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.Stop();
            _audio.clip = clip;
            _audio.Play();
            var wait = 0f;
            while (!_audio.isPlaying && wait < 2f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            wait = 0f;
            while (_audio.isPlaying && wait < 40f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public void StopVoice()
        {
            if (_audio != null)
                _audio.Stop();
        }

        // ------------------------------------------------------------------ driver

        /// <summary>Plays scenario number n (13..18) completely. The caller has set the environment up and placed the rig.</summary>
        public IEnumerator Run(int n, Transform cam, bool smoke = false)
        {
            _sc = "SC" + n;
            _cam = cam;
            Stage = "start";
            PresentableInput.ActiveScenario = n;
            if (chemProps != null)
                for (var i = 0; i < chemProps.Length; i++)
                    if (chemProps[i] != null)
                        chemProps[i].SetActive(i == n - 13);
            ApplyArt();
            Build();
            _refTitle.text = Str("RefSds");
            _nextText.text = Str("Next");
            if (supervisor != null)
            {
                supervisor.LookAt(cam);
                supervisor.SetPose(SupPose.Relaxed);
                if (supStart != null && supStand != null)
                {
                    supervisor.PlaceAt(supStart.position, supStand.position);
                    supervisor.WalkTo(supStand.position);
                }
            }

            // 1. instruction: the same glass card as Scenarios 6 / 8 / 9 (shown by the director, left side), then the learner is taken to the table
            Stage = "intro";
            if (n == 13)
                yield return Card(CardMode.Instruction, "Instruction", Str("Next")); // 14-18 go straight to the chemical (the rules were told once)
            // 2. the chemical's name is only SPOKEN (no card), the chemical stands on the table in front of the learner
            Stage = "bottle";
            yield return Say("BottleName");
            yield return PresentableFx.Wait(0.3f);

            // 4. three questions
            SessionResults.BeginScenario(_sc, Loc.Suffix());
            var correctFlags = new bool[3];
            for (var q = 0; q < 3; q++)
            {
                Stage = "q" + (q + 1);
                yield return Question(n, q, correctFlags);
            }

            SessionResults.EndScenario();
            HideMain();

            // 5. round results
            Stage = "results";
            yield return Results(n, correctFlags);
            if (supervisor != null)
                supervisor.SetPose(SupPose.Relaxed);
            Stage = "done";
        }

        private void ApplyArt()
        {
            if (labelRenderer != null)
            {
                var tex = Resources.Load<Texture2D>("ScCheck/" + _sc + "/label");
                if (tex != null)
                    labelRenderer.material.SetTexture("_BaseMap", tex);
            }

            if (posterRenderer != null)
            {
                var tex = Resources.Load<Texture2D>("ScCheck/" + _sc + "/poster");
                if (tex != null)
                    posterRenderer.material.SetTexture("_BaseMap", tex);
            }
        }

        // above the chemical on the table (clear of it), turned to the learner; falls back to the old eye-relative spot without an anchor
        private void PlaceAbove(Transform t, float heightMeters)
        {
            if (_cam == null)
                return;
            if (chemAnchor == null)
            {
                Place(t, 1.05f, 0.12f, 0f);
                return;
            }

            var toAnchor = Vector3.ProjectOnPlane(chemAnchor.position - _cam.position, Vector3.up);
            if (toAnchor.sqrMagnitude < 0.01f)
                toAnchor = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
            toAnchor.Normalize();
            var pos = _cam.position + toAnchor * 1.6f; // 1.6 m from the eyes, in line with the chemical, so it reads big and sits above it
            // client: popups open at eye level in free space; only lifted when eye level would put the card into the chemical
            pos.y = Mathf.Max(_cam.position.y + 0.03f, chemAnchor.position.y + 0.05f + heightMeters * 0.5f);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(toAnchor, Vector3.up));
            if (t == _main.transform)
            {
                _mainBase = pos;
                _mainRight = Vector3.Cross(Vector3.up, toAnchor);
                ApplyMainOffset();
            }
        }

        private Vector3 _mainBase, _mainRight;

        // SDS hidden: the question panel is the left 55% of the canvas, so the canvas slides right to bring it over the chemical;
        // SDS open: question and SDS stand side by side with the chemical under the gap between them
        private void ApplyMainOffset()
        {
            if (_main == null || chemAnchor == null || _mainRight == Vector3.zero)
                return;
            _main.transform.position = _mainBase + _mainRight * (_sdsOpen ? 0f : 0.46f);
        }

        private void Place(Transform t, float distance, float drop, float side = 0f)
        {
            if (_cam == null)
                return;
            var fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            t.SetPositionAndRotation(_cam.position + fwd * distance + right * side + Vector3.up * drop, Quaternion.LookRotation(fwd, Vector3.up));
        }

        // ------------------------------------------------------------------ cards (instruction, start, bottle)

        private enum CardMode { Instruction, Start, Bottle }

        private static void Anchor(Graphic g, float x0, float y0, float x1, float y1) => Anchor(g.rectTransform, x0, y0, x1, y1);

        private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void ConfigureCard(CardMode mode)
        {
            _cardIcon.gameObject.SetActive(mode != CardMode.Bottle);
            _cardIcon.sprite = mode == CardMode.Start ? LoadSprite("ScCheck/ui_extra") : art.speaker;
            _cardSub.text = string.Empty;
            _cardText.text = string.Empty;
            _cardTitle.text = string.Empty;
            if (mode == CardMode.Instruction)
            {
                Anchor(_cardTitle, 0.1f, 0.84f, 0.9f, 0.95f);
                _cardTitle.text = Str("Instructions");
                _cardTitle.color = HoloWhite;
                Anchor(_cardIcon, 0.44f, 0.64f, 0.56f, 0.83f);
                Anchor(_cardText, 0.09f, 0.14f, 0.91f, 0.62f);
                _cardText.text = Str("InstructionEnglish");
                Anchor(_cardButton, 0.3f, -0.2f, 0.7f, -0.02f);
            }
            else if (mode == CardMode.Start)
            {
                Anchor(_cardIcon, 0.38f, 0.62f, 0.62f, 0.94f);
                Anchor(_cardTitle, 0.07f, 0.38f, 0.93f, 0.62f);
                _cardTitle.text = Str("StartTitle");
                _cardTitle.color = Color.white;
                Anchor(_cardSub, 0.1f, 0.27f, 0.9f, 0.38f);
                _cardSub.text = Str("StartSub");
                Anchor(_cardButton, 0.27f, 0.06f, 0.73f, 0.25f);
            }
            else
            {
                Anchor(_cardTitle, 0.07f, 0.64f, 0.93f, 0.90f);
                _cardTitle.text = Str("BottleTitle");
                _cardTitle.color = Color.white;
                Anchor(_cardText, 0.07f, 0.42f, 0.93f, 0.60f);
                _cardText.text = Str("BottleMcq");
                Anchor(_cardSub, 0.07f, 0.28f, 0.93f, 0.40f);
                _cardSub.text = Str("BottleHint");
                Anchor(_cardButton, 0.3f, -0.2f, 0.7f, -0.02f);
            }

            _cardTitle.fontSizeMax = mode == CardMode.Start ? 60f : mode == CardMode.Instruction ? 34f : 46f;
            _cardTitle.fontStyle = FontStyles.Bold;
            _cardText.fontSizeMax = mode == CardMode.Bottle ? 42f : 38f;
        }

        private IEnumerator Card(CardMode mode, string voiceKey, string button)
        {
            _cardPressed = false;
            if (mode == CardMode.Instruction && InstructionRoutine != null)
            {
                _hookWaiting = true;
                yield return InstructionRoutine(Str("InstructionEnglish"), Clip(voiceKey), () => _cardPressed);
                _hookWaiting = false;
                yield break;
            }

            _cardButtonText.text = button;
            _cardButtonImg.sprite = LoadSprite(mode == CardMode.Start ? "ScCheck/ui_start" : "ScCheck/ui_next");
            ConfigureCard(mode);
            Place(_card.transform, 1.0f, -0.02f, mode == CardMode.Instruction ? -0.38f : 0f);
            _card.gameObject.SetActive(true);
            yield return PopIn(_card.transform, 1f);
            _cardButton.gameObject.SetActive(false);
            StartCoroutine(Gesture());
            yield return Say(voiceKey);
            _cardButton.gameObject.SetActive(true);
            while (!_cardPressed)
                yield return null;
            StopVoice();
            _card.gameObject.SetActive(false);
        }

        private IEnumerator Gesture()
        {
            yield return PresentableFx.Wait(0.6f);
            if (supervisor != null)
                supervisor.Gesture();
        }

        public void PressCard()
        {
            if (_hookWaiting)
            {
                _cardPressed = true; // the smoke test / finger press the director's Continue
                return;
            }

            if (_card != null && _card.gameObject.activeSelf && _cardButton.gameObject.activeSelf)
            {
                PresentableFx.Play(PresentableFx.Tick);
                _cardPressed = true;
            }
        }

        // ------------------------------------------------------------------ one question

        private IEnumerator Question(int n, int q, bool[] flags)
        {
            var qq = Loc.Question(_sc, _sc + ".Q" + (q + 1));
            _currentQuestion = qq;
            var count = Mathf.Min(4, qq.options.Length);
            var order = OptionShuffle.Order(count, SessionResults.ShuffleSeed, qq.id, qq.shuffle);
            var shown = new List<OptionPair>();
            var correctLetter = "A";
            var correctRow = 0;
            for (var i = 0; i < count; i++)
            {
                var opt = qq.options[order[i]];
                var letter = ((char)('A' + i)).ToString();
                shown.Add(new OptionPair { key = letter, text = opt.text.Get(Loc.Current), optionId = opt.id });
                if (opt.id == qq.correct)
                {
                    correctLetter = letter;
                    correctRow = i;
                }
            }

            SessionResults.OpenQuestion(qq.id, qq.prompt.Get(Loc.Current), shown, correctLetter);

            // fill the panel
            _title.text = qq.header.Get(Loc.Current);
            _chemLabel.text = Str("ChemLabel");
            _chemName.text = Str("ChemName");
            _mcq.text = Str("Mcq" + (q + 1));
            _prompt.text = qq.prompt.Get(Loc.Current);
            for (var i = 0; i < 4; i++)
            {
                _optBg[i].transform.parent.gameObject.SetActive(i < count);
                if (i >= count)
                    continue;
                _optText[i].text = shown[i].text;
                _optBg[i].color = new Color(0.45f, 0.78f, 1f, 0.30f);
                _optLetter[i].color = HoloWhite;
            }

            _verdict.text = string.Empty;
            _verdictBg.color = new Color(0f, 0f, 0f, 0f);
            if (_reason != null) _reason.text = string.Empty;
            _nextBtn.gameObject.SetActive(false);
            _sdsSheet.sprite = SdsSprite(q);
            SetSdsSheetSize();
            SetSds(true, true); // the SDS opens by itself beside the question and scrolls slowly so it can be read
            if (_autoScroll != null)
                StopCoroutine(_autoScroll);
            _autoScroll = StartCoroutine(AutoScrollSds());
            _answered = -1;
            _next = false;
            _locked = false;
            _correctShown = correctRow;

            PlaceAbove(_main.transform, 0.688f);
            ShowMain();
            if (supervisor != null)
            {
                supervisor.SetPose(SupPose.Talk);
                supervisor.Gesture();
            }

            var say = StartCoroutine(Say("Q" + (q + 1)));
            while (_answered < 0)
                yield return null;
            StopCoroutine(say);
            StopVoice();

            var right = _answered == correctRow;
            flags[q] = right;
            SessionResults.RecordTry(((char)('A' + _answered)).ToString(), right);
            if (supervisor != null)
            {
                supervisor.SetPose(SupPose.Wait);
                if (right)
                    supervisor.Nod();
                else
                    supervisor.ShakeHead();
            }

            StartCoroutine(Say(right ? "Right" : "Wrong"));
            _next = false;
            _nextBtn.gameObject.SetActive(true);
            yield return PopIn(_nextBtn, 1f);
            while (!_next)
                yield return null;
            StopVoice();
            SetSds(false, true);
            yield return PresentableFx.Wait(0.2f);
        }

        // finger / smoke API ------------------------------------------------

        public void SelectOption(int row)
        {
            if (_locked || _main == null || !_main.gameObject.activeSelf || row < 0 || row > 3 || !_optBg[row].transform.parent.gameObject.activeSelf)
                return;
            _locked = true;
            _answered = row;
            var right = row == _correctShown;
            PresentableFx.Play(right ? PresentableFx.Success : PresentableFx.Error);
            PresentableFx.Haptic(true, right ? 0.5f : 0.9f, 0.18f);
            for (var i = 0; i < 4; i++)
            {
                if (!_optBg[i].transform.parent.gameObject.activeSelf)
                    continue;
                if (i == _correctShown)
                    _optBg[i].color = new Color(0.25f, 0.9f, 0.45f, 0.55f);
                else if (i == row)
                    _optBg[i].color = new Color(0.95f, 0.25f, 0.2f, 0.55f);
                else
                    _optBg[i].color = new Color(0.30f, 0.5f, 0.7f, 0.16f);
            }

            _verdict.color = right ? Good : Bad;
            _verdict.text = right ? Str("Right") : Str("Wrong");
            _verdictBg.color = right ? new Color(0.1f, 0.45f, 0.25f, 0.45f) : new Color(0.55f, 0.12f, 0.1f, 0.45f);
            if (_reason != null && _currentQuestion != null)
            {
                var rField = right ? _currentQuestion.reasonCorrect : _currentQuestion.reasonWrong;
                _reason.text = rField != null ? rField.Get(Localization.Loc.Current) : string.Empty;
                _reason.color = right ? new Color(0.7f, 1f, 0.8f) : new Color(1f, 0.8f, 0.75f);
            }
        }

        public void PressNext()
        {
            if (_locked && _nextBtn != null && _nextBtn.gameObject.activeSelf)
            {
                PresentableFx.Play(PresentableFx.Tick);
                _next = true;
            }
        }

        public void ToggleSds()
        {
            if (_main == null || !_main.gameObject.activeSelf)
                return;
            SetSds(!_sdsOpen, false);
            if (supervisor != null && _sdsOpen && pointTarget != null)
                StartCoroutine(PointBriefly());
        }

        private IEnumerator PointBriefly()
        {
            supervisor.PointAt(pointTarget);
            yield return PresentableFx.Wait(2.4f);
        }

        private void SetSds(bool open, bool instant)
        {
            _sdsOpen = open;
            ApplyMainOffset();
            _sdsToggleText.text = Str(open ? "HideSds" : "CheckSds");
            if (_sdsAnim != null)
                StopCoroutine(_sdsAnim);
            if (instant || !isActiveAndEnabled)
            {
                _sdsPanel.anchoredPosition = new Vector2(open ? 0f : 800f, _sdsPanel.anchoredPosition.y);
                _sdsPanel.gameObject.SetActive(open);
                return;
            }

            _sdsAnim = StartCoroutine(SlideSds(open));
        }

        private IEnumerator SlideSds(bool open)
        {
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
            _sdsPanel.gameObject.SetActive(true);
            var from = _sdsPanel.anchoredPosition.x;
            var to = open ? 0f : 800f;
            yield return PresentableFx.Tween(0.35f, t =>
            {
                _sdsPanel.anchoredPosition = new Vector2(Mathf.Lerp(from, to, t), _sdsPanel.anchoredPosition.y);
                var g = _sdsPanel.GetComponent<CanvasGroup>();
                g.alpha = open ? t : 1f - t * t;
            }, PresentableFx.EaseOutCubic);
            _sdsPanel.GetComponent<CanvasGroup>().alpha = open ? 1f : 0f;
            _sdsPanel.gameObject.SetActive(open);
            _sdsAnim = null;
        }

        public void ScrollSds(float delta)
        {
            if (_sdsScroll == null || !_sdsOpen)
                return;
            _sdsScroll.verticalNormalizedPosition = Mathf.Clamp01(_sdsScroll.verticalNormalizedPosition + delta);
        }

        private void SetSdsSheetSize()
        {
            var vp = (RectTransform)_sdsScroll.viewport;
            var w = vp.rect.width;
            var sp = _sdsSheet.sprite;
            var aspect = sp != null ? sp.rect.height / Mathf.Max(1f, sp.rect.width) : 1.34f;
            // the sheet fills the window width (text as large as the picture allows) and is taller than the window, so it scrolls
            _sdsContent.sizeDelta = new Vector2(0f, w * aspect);
            _sdsScroll.verticalNormalizedPosition = 1f;
        }

        // ------------------------------------------------------------------ results

        private IEnumerator Results(int n, bool[] flags)
        {
            var score = 0;
            for (var i = 0; i < 3; i++)
            {
                if (flags[i])
                    score++;
                _resRows[i].text = Str((flags[i] ? "McqCorrect" : "McqWrong") + (i + 1));
                _resRows[i].color = flags[i] ? Good : Bad;
            }

            LastScore = score;
            var last = n == 18;
            _resTitle.text = Str("RoundTitle");
            _resScore.text = Str("Score" + score);
            _resBadge.text = score + "/3";
            _resMsg.text = Str(score >= 2 ? (last ? "ResultGoodLast" : "ResultGood") : (last ? "ResultLowLast" : "ResultLow"));
            _resNextText.text = Str("NextRound");
            PlaceAbove(_results.transform, 0.52f);
            _results.gameObject.SetActive(true);
            _next = false;
            if (supervisor != null)
            {
                supervisor.SetPose(score >= 2 ? SupPose.Talk : SupPose.Wait);
                if (score >= 2)
                    supervisor.Nod();
            }

            PresentableFx.Play(PresentableFx.Whoosh, 0.5f);
            yield return PopIn(_results.transform, 1f);
            _resNext.gameObject.SetActive(false);
            yield return Say((score >= 2 ? "ResultGood" : "ResultLow") + (last ? "Last" : string.Empty));
            _resNext.gameObject.SetActive(true);
            while (!_next)
                yield return null;
            StopVoice();
            _results.gameObject.SetActive(false);
        }

        public void PressNextRound()
        {
            if (_results != null && _results.gameObject.activeSelf && _resNext.gameObject.activeSelf)
            {
                PresentableFx.Play(PresentableFx.Tick);
                _next = true;
            }
        }

        // ------------------------------------------------------------------ small tweens

        private IEnumerator PopIn(Transform t, float to)
        {
            var baseScale = t.GetComponent<Canvas>() != null ? Vector3.one * Scale : Vector3.one;
            var g = t.GetComponent<CanvasGroup>();
            yield return PresentableFx.Tween(0.3f, k =>
            {
                t.localScale = baseScale * Mathf.LerpUnclamped(0.86f, to, k);
                if (g != null)
                    g.alpha = Mathf.Clamp01(k * 2f);
            }, PresentableFx.EaseOutBack);
            t.localScale = baseScale * to;
            if (g != null)
                g.alpha = 1f;
        }

        private void ShowMain()
        {
            _main.gameObject.SetActive(true);
            StartCoroutine(PopIn(_main.transform, 1f));
        }

        private Coroutine _autoScroll;

        // top to bottom and back, slowly (one pass about 28 s); any touch of the sheet (stick, drag, arrows) pauses it for a few seconds
        private IEnumerator AutoScrollSds()
        {
            yield return PresentableFx.Wait(2.5f);
            var dir = -1f;
            var pause = 0f;
            var last = _sdsScroll.verticalNormalizedPosition;
            while (true)
            {
                if (!_sdsOpen || !QuestionVisible)
                {
                    last = _sdsScroll.verticalNormalizedPosition;
                    yield return null;
                    continue;
                }

                var cur = _sdsScroll.verticalNormalizedPosition;
                if (Mathf.Abs(cur - last) > 0.003f)
                    pause = 6f;
                if (pause > 0f)
                {
                    pause -= Time.unscaledDeltaTime;
                    last = cur;
                    yield return null;
                    continue;
                }

                cur = Mathf.Clamp01(cur + dir * Time.unscaledDeltaTime / 28f);
                _sdsScroll.verticalNormalizedPosition = cur;
                last = cur;
                if (cur <= 0.001f)
                {
                    dir = 1f;
                    pause = 2f;
                }
                else if (cur >= 0.999f)
                {
                    dir = -1f;
                    pause = 2f;
                }

                yield return null;
            }
        }

        private void HideMain()
        {
            if (_autoScroll != null)
            {
                StopCoroutine(_autoScroll);
                _autoScroll = null;
            }

            if (_main != null)
                _main.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ build

        private TextMeshProUGUI T(string name, Transform parent, Vector2 min, Vector2 max, float size, Color color,
            TextAlignmentOptions align, bool bold = false, float minSize = 14f)
        {
            var t = PresentableUi.Text(name, parent, min, max, string.Empty, size, color, align, bold, art.englishFont);
            t.enableAutoSizing = true;
            t.fontSizeMin = minSize;
            t.fontSizeMax = size;
            t.margin = new Vector4(4f, 2f, 4f, 2f);
            return t;
        }

        private Button MakeButton(string name, Transform parent, RectTransform rt, Image img, Action onClick)
        {
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            var lift = rt.gameObject.AddComponent<HoverLift>();
            lift.Init(rt);
            b.onClick.AddListener(lift.Press);
            b.onClick.AddListener(() => onClick());
            return b;
        }

        private void Build()
        {
            if (_main != null)
                return;
            var screen = LoadSprite("ScCheck/ui_screen");
            var popup = LoadSprite("ScCheck/ui_popup");
            var nextSp = LoadSprite("ScCheck/ui_next");
            var round = LoadSprite("ScCheck/ui_round_button");
            var sdsFrame = LoadSprite("ScCheck/ui_sds_frame");
            var cardSp = art.holoButton != null ? art.holoButton : art.roundedSolid;
            var panelTint = new Color(0.16f, 0.42f, 0.72f, 0.52f); // glass blue on top of a dark underlay (below): the reference look, the words stay clear

            // ---- glass card (instructions / start / bottle)
            _card = PresentableUi.WorldCanvas("ScCheckCard", transform, new Vector2(900f, 560f), Scale, true, 30);
            _card.gameObject.AddComponent<CanvasGroup>();
            DraggablePanel.Attach(_card);
            PresentableUi.Img("Frame", _card.transform, Vector2.zero, Vector2.one, popup, Color.white).type = Image.Type.Sliced;
            _cardIcon = PresentableUi.Img(PresentableUi.Rect("Icon", _card.transform, new Vector2(0.4f, 0.6f), new Vector2(0.6f, 0.9f)), null, Color.white, false, false, true);
            _cardTitle = T("Title", _card.transform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.94f), 42f, HoloWhite, TextAlignmentOptions.Center, true, 20f);
            _cardText = T("Text", _card.transform, new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.8f), 38f, HoloWhite, TextAlignmentOptions.Center, false, 18f);
            _cardSub = T("Sub", _card.transform, new Vector2(0.1f, 0.27f), new Vector2(0.9f, 0.38f), 34f, HoloWhite, TextAlignmentOptions.Center, false, 16f);
            _cardButton = PresentableUi.Rect("Button", _card.transform, new Vector2(0.3f, 0.07f), new Vector2(0.7f, 0.27f));
            _cardButtonImg = PresentableUi.Img(_cardButton, nextSp, Color.white, false, true, true);
            _cardButtonText = T("Label", _cardButton, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f), 40f, HoloWhite, TextAlignmentOptions.Center, true, 18f);
            MakeButton("CardBtn", _card.transform, _cardButton, _cardButtonImg, PressCard);
            PresentableUi.AlwaysOnTop(_card);
            _card.gameObject.SetActive(false);

            // ---- question panel (left) + SDS panel (right) on one canvas
            _main = PresentableUi.WorldCanvas("ScCheckMain", transform, new Vector2(2200f, 860f), Scale, true, 30);
            _mainGroup = _main.gameObject.AddComponent<CanvasGroup>();
            DraggablePanel.Attach(_main);
            var panel = PresentableUi.Rect("Panel", _main.transform, new Vector2(0f, 0.255f), new Vector2(0.4773f, 1f));
            var glow = PresentableUi.Rect("Glow", panel, new Vector2(-0.012f, -0.012f), new Vector2(1.012f, 1.012f));
            PresentableUi.Img(glow, screen, new Color(0.25f, 0.65f, 1f, 0.45f), true);
            PresentableUi.Img(PresentableUi.Rect("Under", panel, new Vector2(0.004f, 0.004f), new Vector2(0.996f, 0.996f)), art.roundedSolid, new Color(0.02f, 0.07f, 0.15f, 0.80f), true);
            PresentableUi.Img(panel, screen, panelTint, true).raycastTarget = true;
            _title = T("Title", panel, new Vector2(0.04f, 0.865f), new Vector2(0.96f, 0.985f), 52f, HoloWhite, TextAlignmentOptions.Center, true, 22f);
            _chemLabel = T("ChemLabel", panel, new Vector2(0.05f, 0.775f), new Vector2(0.30f, 0.865f), 40f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 18f);
            _chemName = T("ChemName", panel, new Vector2(0.30f, 0.775f), new Vector2(0.97f, 0.865f), 38f, HoloCyan, TextAlignmentOptions.MidlineLeft, false, 18f);
            _mcq = T("Mcq", panel, new Vector2(0.05f, 0.66f), new Vector2(0.21f, 0.77f), 42f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 18f);
            _prompt = T("Prompt", panel, new Vector2(0.21f, 0.615f), new Vector2(0.96f, 0.78f), 42f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, 18f);
            const float gap = 0.014f;
            const float x0 = 0.045f;
            const float totalW = 0.91f;
            var w = (totalW - gap * 3f) / 4f;
            for (var i = 0; i < 4; i++)
            {
                var min = new Vector2(x0 + i * (w + gap), 0.19f);
                var max = new Vector2(x0 + i * (w + gap) + w, 0.60f);
                var card = PresentableUi.Rect("Option" + i, panel, min, max);
                _optBg[i] = PresentableUi.Img(card, cardSp, new Color(0.45f, 0.78f, 1f, 0.30f), true, true);
                var idx = i;
                MakeButton("OptBtn" + i, panel, card, _optBg[i], () => SelectOption(idx));
                _optLetter[i] = T("Letter", card, new Vector2(0.07f, 0.84f), new Vector2(0.45f, 0.99f), 64f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 22f);
                _optLetter[i].text = ((char)('A' + i)).ToString();
                _optText[i] = T("Text", card, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.83f), 42f, HoloWhite, TextAlignmentOptions.Center, false, 22f);
                _optText[i].fontSizeMin = 14f; // the longest answers (Scenarios 17 / 18) must still fit the narrow card
                _optText[i].textWrappingMode = TextWrappingModes.Normal;
            }

            // verdict strip inside the panel: its own padded band between the cards and the bottom edge
            var strip = PresentableUi.Rect("VerdictStrip", panel, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.19f));
            _verdictBg = PresentableUi.Img(strip, art.roundedSolid, new Color(0.1f, 0.3f, 0.5f, 0.0f), true);
            _verdict = T("Verdict", strip, new Vector2(0.03f, 0.52f), new Vector2(0.97f, 0.95f), 28f, Good, TextAlignmentOptions.Center, true, 14f);
            _reason = T("Reason", strip, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.52f), 20f, HoloWhite, TextAlignmentOptions.Center, false, 10f);
            _reason.textWrappingMode = TextWrappingModes.Normal;

            _nextBtn = PresentableUi.Rect("Next", _main.transform, new Vector2(0.166f, 0.0f), new Vector2(0.311f, 0.097f));
            _nextBtn.gameObject.AddComponent<CanvasGroup>();
            var nextImg = PresentableUi.Img(_nextBtn, nextSp, Color.white, false, true, true);
            _nextText = T("Label", _nextBtn, new Vector2(0.12f, 0.15f), new Vector2(0.88f, 0.85f), 46f, HoloWhite, TextAlignmentOptions.Center, true, 18f);
            MakeButton("NextBtn", _main.transform, _nextBtn, nextImg, PressNext);

            // SDS side panel: the frame picture (584x924, glow padding inside the file) is shown unsliced; everything sits inside its visible edge
            _sdsPanel = PresentableUi.Rect("SdsPanel", _main.transform, new Vector2(0.495f, 0f), new Vector2(1f, 1f));
            _sdsPanel.gameObject.AddComponent<CanvasGroup>();
            var sdsBg = PresentableUi.Img(_sdsPanel, sdsFrame, new Color(0.82f, 0.9f, 1f, 0.97f), false);
            sdsBg.type = Image.Type.Simple;
            sdsBg.raycastTarget = true;
            _refTitle = T("RefTitle", _sdsPanel, new Vector2(0.15f, 0.87f), new Vector2(0.90f, 0.94f), 36f, HoloWhite, TextAlignmentOptions.Center, true, 14f);
            var viewport = PresentableUi.Rect("Viewport", _sdsPanel, new Vector2(0.08f, 0.15f), new Vector2(0.93f, 0.86f));
            PresentableUi.Img(viewport, null, new Color(0f, 0f, 0f, 0.01f), false, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            _sdsContent = PresentableUi.Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
            _sdsContent.pivot = new Vector2(0.5f, 1f);
            _sdsContent.anchoredPosition = Vector2.zero;
            _sdsContent.sizeDelta = new Vector2(0f, 800f);
            _sdsSheet = PresentableUi.Img(PresentableUi.Rect("Sheet", _sdsContent, Vector2.zero, Vector2.one), null, Color.white, false, true, true);
            _sdsScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _sdsScroll.viewport = viewport;
            _sdsScroll.content = _sdsContent;
            _sdsScroll.horizontal = false;
            _sdsScroll.vertical = true;
            _sdsScroll.movementType = ScrollRect.MovementType.Clamped;
            _sdsScroll.scrollSensitivity = 30f;
            _sdsScroll.inertia = false;
            // finger drag like a phone (FingerPokeSystem drives it); the ^ and v buttons stay as a backup
            _sdsZone = viewport.gameObject.AddComponent<ScrollZone>();
            _sdsZone.Init(viewport, _sdsContent);
            var up = PresentableUi.Rect("Up", _sdsPanel, new Vector2(0.17f, 0.075f), new Vector2(0.31f, 0.135f));
            var upImg = PresentableUi.Img(up, art.roundedSolid, new Color(0.55f, 0.85f, 1f, 0.35f), true, true);
            T("Arrow", up, Vector2.zero, Vector2.one, 34f, HoloWhite, TextAlignmentOptions.Center, true, 14f).text = "^";
            MakeButton("UpBtn", _sdsPanel, up, upImg, () => ScrollSds(0.35f));
            var down = PresentableUi.Rect("Down", _sdsPanel, new Vector2(0.34f, 0.075f), new Vector2(0.48f, 0.135f));
            var downImg = PresentableUi.Img(down, art.roundedSolid, new Color(0.55f, 0.85f, 1f, 0.35f), true, true);
            T("Arrow", down, Vector2.zero, Vector2.one, 34f, HoloWhite, TextAlignmentOptions.Center, true, 14f).text = "v";
            MakeButton("DownBtn", _sdsPanel, down, downImg, () => ScrollSds(-0.35f));

            // the round CHECK SDS toggle overlaps the lower right corner, like the picture
            _roundBtn = PresentableUi.Rect("CheckSds", _main.transform, new Vector2(1f, 0f), new Vector2(1f, 0f));
            _roundBtn.sizeDelta = new Vector2(190f, 186f);
            _roundBtn.anchoredPosition = new Vector2(-48f, 96f);
            var roundImg = PresentableUi.Img(_roundBtn, round, Color.white, false, true, true);
            _sdsToggleText = T("Label", _roundBtn, new Vector2(0.14f, 0.28f), new Vector2(0.86f, 0.72f), 34f, HoloWhite, TextAlignmentOptions.Center, true, 14f);
            MakeButton("SdsBtn", _main.transform, _roundBtn, roundImg, ToggleSds);
            PresentableUi.AlwaysOnTop(_main);
            _main.gameObject.SetActive(false);

            // ---- results (translucent glass like the picture, rows with bracket frames)
            _results = PresentableUi.WorldCanvas("ScCheckResults", transform, new Vector2(900f, 650f), Scale, true, 30);
            _results.gameObject.AddComponent<CanvasGroup>();
            DraggablePanel.Attach(_results);
            PresentableUi.Img("Back", _results.transform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.99f), art.roundedSolid, new Color(0.07f, 0.14f, 0.26f, 0.94f));
            PresentableUi.Img("Frame", _results.transform, Vector2.zero, Vector2.one, popup, Color.white).type = Image.Type.Sliced;
            _resTitle = T("Title", _results.transform, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f), 58f, HoloCyan, TextAlignmentOptions.Center, true, 22f);
            _resScore = T("Score", _results.transform, new Vector2(0.1f, 0.67f), new Vector2(0.72f, 0.80f), 40f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, 18f);
            var badge = PresentableUi.Rect("Badge", _results.transform, new Vector2(0.74f, 0.67f), new Vector2(0.9f, 0.80f));
            PresentableUi.Img(badge, cardSp, new Color(0.55f, 0.85f, 1f, 0.3f), true);
            _resBadge = T("BadgeText", badge, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), 44f, HoloWhite, TextAlignmentOptions.Center, true, 18f);
            for (var i = 0; i < 3; i++)
            {
                var y = 0.62f - i * 0.115f;
                var row = PresentableUi.Rect("Row" + i, _results.transform, new Vector2(0.1f, y - 0.1f), new Vector2(0.9f, y));
                PresentableUi.Img(row, cardSp, new Color(0.55f, 0.85f, 1f, 0.2f), true);
                _resRows[i] = T("Text", row, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f), 36f, Good, TextAlignmentOptions.MidlineLeft, false, 16f);
            }

            _resMsg = T("Message", _results.transform, new Vector2(0.1f, 0.19f), new Vector2(0.9f, 0.285f), 30f, HoloWhite, TextAlignmentOptions.Center, false, 14f);
            _resNext = PresentableUi.Rect("NextRound", _results.transform, new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.17f));
            var rnImg = PresentableUi.Img(_resNext, nextSp, Color.white, false, true, true);
            _resNextText = T("Label", _resNext, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f), 40f, HoloWhite, TextAlignmentOptions.Center, true, 16f);
            MakeButton("NextRoundBtn", _results.transform, _resNext, rnImg, PressNextRound);
            PresentableUi.AlwaysOnTop(_results);
            _results.gameObject.SetActive(false);
        }
    }
}

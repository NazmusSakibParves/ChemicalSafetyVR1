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
        private static float Scale => NewMcqUi ? 0.0015f : 0.0008f; // the new design: bigger, and 3 m away (not 1.6 m) so it is not on the eyes in the world so the answers can be read

        private string _sc; // "SC13"

        /// <summary>The scenario id being played ("SC13" ... "SC18").</summary>
        public string CurrentSc => _sc;

        /// <summary>The question panel (with the SDS beside it): visible, and the world Y of its top edge (the top bar must stay above it).</summary>
        public Transform MainTransform => _main != null ? _main.transform : null;
        public bool MainVisible => _main != null && _main.gameObject.activeInHierarchy;

        public float MainTopWorldY
        {
            get
            {
                var corners = new Vector3[4];
                ((RectTransform)_main.transform).GetWorldCorners(corners);
                return Mathf.Max(corners[1].y, corners[2].y);
            }
        }
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
        private readonly Image[] _optImg = new Image[4]; // picture questions (Scenario 18 pictograms): a picture instead of the text
        private LOption[] _shownOptions = new LOption[4]; // the option shown in each card (slot order)

        /// <summary>
        /// 2026-11-14: Scenario 18 question 2 is "which pictogram is NOT a bleaching powder pictogram", with pictures in the option cards:
        /// 3 of the 4 pictograms of the chemical + 1 of the 2 that do not belong, drawn per session. <c>false</c> = the earlier text question
        /// (SC18.Q2.old in SC18.json).
        /// </summary>
        public const bool PictogramQuestion = true;

        /// <summary>
        /// 2026-11-14 (client): the reference SDS beside the question is bigger and clearer: the canvas is 2500 x 1000 px (was 2200 x 860), the question
        /// panel keeps its size (1050 x 641 px), the SDS panel grows from 1111 x 860 to 1400 x 1000 px. <c>false</c> = the earlier sizes.
        /// </summary>
        public const bool BigSds = true;

        /// <summary>
        /// 2026-11-19 (client): the question box of Scenarios 13-18 has its own design (Downloads/Scene13-18 MCQ UI): a translucent neon frame with a
        /// title strip, "MCQ n: chemical", the question, four answer rows with a letter box (the pictogram question: four cards), NEXT below.
        /// <c>false</c> = the earlier four-card panel.
        /// </summary>
        public const bool NewMcqUi = true;

        private Sprite _ansOff, _ansOn;
        private readonly Image[] _optLetterBox = new Image[4];

        /// <summary>
        /// 2026-11-14 (client): the SDS is in two places in Scenarios 13-18: the panel beside the question (as it was, opens by itself, its own
        /// HIDE / SHOW SDS button) AND the clipboard in the learner's left hand like in the other scenarios (left X button raises / lowers it).
        /// <c>true</c>: X belongs to the clipboard (the director gives it the sheet of the question through <see cref="SheetChanged"/>);
        /// <c>false</c>: X works the side panel, the clipboard is off, as before.
        /// </summary>
        public const bool ClipboardOnX = true;

        /// <summary>Raised with the sheet of the current question (the director puts it on the left-hand clipboard).</summary>
        public Action<Sprite> SheetChanged;
        // the new design: question panel 1250 px, a 700 px gap (the chemical stands in it, the top bar stays clear), SDS panel 1250 px
        private const float MainDist = 2.4f; // metres from the eyes to the question + SDS boxes (the new design)
        private const float QuestionW = 2000f, GapW = 90f, SdsW = 1300f; // side by side, as in the client's workflow picture (a small gap) // the SDS panel keeps the earlier 1400 x 1000 px (the whole sheet is visible)
        private static float CanvasW => NewMcqUi ? QuestionW + GapW + SdsW : BigSds ? 2500f : 2200f;
        private static float CanvasH => NewMcqUi ? 1600f : BigSds ? 1000f : 860f;

        private static readonly Dictionary<string, Sprite> PictureCache = new Dictionary<string, Sprite>();
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
            if (!ClipboardOnX)
                EnableSdsKey();
            // the thumbsticks scroll the sheet up and down (the sheet is taller than its window)
            _sdsStick = new InputAction("ScCheckSdsScroll", InputActionType.Value);
            _sdsStick.AddBinding("<XRController>{RightHand}/thumbstick");
            _sdsStick.AddBinding("<XRController>{LeftHand}/thumbstick");
            _sdsStick.Enable();
        }

        private void EnableSdsKey()
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

        // the same page on the wooden clipboard with the binder clip (Tools/prepare_sc13_18_clipboard.py): what the left X button raises
        private Sprite ClipSprite(int q)
        {
            var key = "clip_sds" + (q + 1);
            var s = LoadPicture("ScCheck/" + _sc + "/" + key);
            if (s == null)
                s = LoadPicture("ScCheck/" + key);
            return s != null ? s : SdsSprite(q);
        }

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

        /// <summary>Set by the director: stops the main voice channel (Scenarios 13-18 speak on their own channel).</summary>
        public static System.Action MainVoiceStop;

        /// <summary>Stops this runner's own voice channel.</summary>
        public void StopOwnVoice()
        {
            if (_audio != null)
                _audio.Stop();
        }

        private IEnumerator SayAnswer(bool right, int q)
        {
            var why = (right ? "RightWhy" : "WrongWhy") + (q + 1);
            // client 2026-11-19: a right answer's explanation already begins with "Correct!" - so only the explanation is played (the old
            // "Correct!" line would be heard twice); a wrong answer: the "not quite" line, then the explanation
            if (right && Clip(why) != null)
            {
                yield return Say(why);
                yield break;
            }

            yield return Say(right ? "Right" : "Wrong");
            if (Clip(why) != null)
                yield return Say(why);
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

            MainVoiceStop?.Invoke(); // one voice at a time: the main channel is silent before this line starts
            _audio.Stop();
            _audio.clip = clip;
            _audio.Play();
#if UNITY_EDITOR
            // Editor only: the voice trace of the smoke test (same file and format as the director's)
            try
            {
                System.IO.File.AppendAllText("Temp/Smoke/voice_trace.log", _sc + " | " + Localization.Loc.Current + " | " + clip.name + System.Environment.NewLine);
            }
            catch
            {
                // tracing must never disturb the game
            }
#endif
            var wait = 0f;
            while (!_audio.isPlaying && wait < 2f)
            {
                wait += SessionClock.GameDelta;
                yield return null;
            }

            wait = 0f;
            while (_audio.isPlaying && wait < 40f)
            {
                wait += SessionClock.GameDelta;
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
            SheetChanged?.Invoke(ClipSprite(0)); // the clipboard holds this scenario's sheet from the start
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
            var pos = _cam.position + toAnchor * (NewMcqUi ? MainDist : 1.6f); // 1.6 m (the new design: 2.4 m: not on the eyes, still easy to point at with the ray) from the eyes, in line with the chemical, so it reads big and sits above it
            // client: popups open at eye level in free space; only lifted when eye level would put the card into the chemical
            pos.y = Mathf.Max(_cam.position.y + 0.03f, chemAnchor.position.y + 0.05f + heightMeters * 0.5f);
            if (NewMcqUi && _main != null && t == _main.transform)
            {
                // client 2026-11-19: the question box and the SDS box stand ABOVE the chemical (its head is never covered): their lower edge is
                // 4 cm over the chemical's top, their upper edge stays under the top bar; if that is less room than the boxes need, they scale down
                var chemTop = chemAnchor.position.y + 0.34f;
                _fit = 1f;
                // the panels stand beside the chemical (it is in the 70 cm gap) with their lower edge level with its head; the top bar is clear of them
                // because they are 35 cm and more off the middle
                // top edge 62 cm over the eyes (about 13 degrees up at 2.6 m, under the top bar's level at the sides), the boxes hang down beside the chemical
                pos.y = MainCenterY();
            }

            t.SetPositionAndRotation(pos, Quaternion.LookRotation(toAnchor, Vector3.up));
            if (t == _main.transform)
            {
                _mainBase = pos;
                _mainRight = Vector3.Cross(Vector3.up, toAnchor);
                ApplyMainOffset();
            }
        }

        private Vector3 _mainBase, _mainRight;
        private float _fit = 1f; // the new design: how much the question + SDS canvas is scaled down to fit between the chemical and the top bar

        // SDS hidden: the question panel is the left 55% of the canvas, so the canvas slides right to bring it over the chemical;
        // SDS open: question and SDS stand side by side with the chemical under the gap between them
        private void ApplyMainOffset()
        {
            if (_main == null || chemAnchor == null || _mainRight == Vector3.zero)
                return;
            // the gap between the question panel and the SDS panel stands over the chemical; SDS hidden: the question panel does
            var openShift = NewMcqUi ? (CanvasW * 0.5f - (QuestionW + GapW * 0.5f)) * Scale * _fit : BigSds ? 0.14f : 0f; // the new design: the gap between the panels is the middle of the canvas // the new design: the gap between the two panels is at 0.505 of the canvas
            var closedShift = NewMcqUi ? (CanvasW * 0.5f - QuestionW * 0.5f) * Scale * _fit : BigSds ? 0.58f : 0.46f;
            _main.transform.position = _mainBase + _mainRight * (_sdsOpen ? openShift : closedShift);
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

        // a picture of the content (Resources path); the png may be imported as a texture, then a sprite is made from it
        private static Sprite LoadPicture(string path)
        {
            if (PictureCache.TryGetValue(path, out var cached) && cached != null)
                return cached;
            var sp = Resources.Load<Sprite>(path);
            if (sp == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            if (sp != null)
                PictureCache[path] = sp;
            else
                Debug.LogWarning("[ScCheck] picture missing: " + path);
            return sp;
        }

        private static uint StableHash(string s)
        {
            var h = 2166136261u;
            foreach (var c in s)
                h = (h ^ c) * 16777619u;
            return h;
        }

        // Picture question with a pool: pickMembers of the options that belong + pickOutsiders of the outsiders, drawn with a generator seeded by the
        // session and the question id, so a question shown again (Previous button) has the very same four pictures. The outsider is the right answer.
        private static LQuestion DrawPool(LQuestion src)
        {
            var rng = new System.Random((int)(StableHash(SessionResults.ShuffleSeed + "|" + src.id + "|pool") & 0x7fffffff));
            var members = new List<LOption>();
            var outsiders = new List<LOption>();
            foreach (var o in src.options)
                (o.outsider ? outsiders : members).Add(o);
            for (var i = members.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (members[i], members[j]) = (members[j], members[i]);
            }

            for (var i = outsiders.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (outsiders[i], outsiders[j]) = (outsiders[j], outsiders[i]);
            }

            var picked = new List<LOption>();
            for (var i = 0; i < src.pickMembers && i < members.Count; i++)
                picked.Add(members[i]);
            string correct = null;
            for (var i = 0; i < src.pickOutsiders && i < outsiders.Count; i++)
            {
                picked.Add(outsiders[i]);
                if (correct == null)
                    correct = outsiders[i].id;
            }

            return new LQuestion
            {
                id = src.id,
                header = src.header,
                prompt = src.prompt,
                options = picked.ToArray(),
                correct = correct,
                correctTitle = src.correctTitle,
                wrongTitle = src.wrongTitle,
                nextLabel = src.nextLabel,
                shuffle = src.shuffle,
                reasonCorrect = src.reasonCorrect,
                reasonWrong = src.reasonWrong
            };
        }

        private IEnumerator Question(int n, int q, bool[] flags)
        {
            var qid = _sc + ".Q" + (q + 1);
            if (!PictogramQuestion && qid == "SC18.Q2")
                qid += ".old";
            var qq = Loc.Question(_sc, qid);
            if (PictogramQuestion && qq != null && qq.pickMembers > 0)
                qq = DrawPool(qq);
            _currentQuestion = qq;
            var count = Mathf.Min(4, qq.options.Length);
            var order = OptionShuffle.Order(count, SessionResults.ShuffleSeed, qq.id, qq.shuffle);
            var shown = new List<OptionPair>();
            var correctLetter = "A";
            var correctRow = 0;
            for (var i = 0; i < count; i++)
            {
                var opt = qq.options[order[i]];
                _shownOptions[i] = opt;
                var letter = ((char)('A' + i)).ToString();
                shown.Add(new OptionPair { key = letter, text = opt.text.Get(Loc.Current), optionId = opt.id });
                if (opt.id == qq.correct)
                {
                    correctLetter = letter;
                    correctRow = i;
                }
            }

            // answered in an earlier visit (Previous / Next buttons): the first pick is shown locked with its reason, nothing is recorded again
            var lockedBefore = SessionResults.TryGetAnswered(qq.id, out var lockedLetter, out _);
            if (!lockedBefore)
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
                var pic = !string.IsNullOrEmpty(_shownOptions[i].image) ? LoadPicture(_shownOptions[i].image) : null;
                _optImg[i].sprite = pic;
                _optImg[i].gameObject.SetActive(pic != null);
                _optText[i].gameObject.SetActive(pic == null);
                if (NewMcqUi && _ansOff != null)
                {
                    _optBg[i].sprite = _ansOff;
                    _optBg[i].type = Image.Type.Simple;
                    _optBg[i].color = new Color(1f, 1f, 1f, 0.78f);
                }
                else
                    _optBg[i].color = new Color(0.45f, 0.78f, 1f, 0.30f);
                _optLetter[i].color = HoloWhite;
            }

            for (var i = 0; i < 4; i++)
            {
                _reasonShown[i] = null;
                if (_optReason[i] != null)
                    _optReason[i].gameObject.SetActive(false);
            }

            if (NewMcqUi && _ansOff != null)
            {
                ApplyFixedSizes();
                LayoutNewDynamic(count, qq.reasonCorrect != null ? qq.reasonCorrect.Get(Loc.Current) : string.Empty,
                    qq.reasonWrong != null ? qq.reasonWrong.Get(Loc.Current) : string.Empty);
            }

            _verdict.text = string.Empty;
            _verdictBg.color = new Color(0f, 0f, 0f, 0f);
            if (_reason != null) _reason.text = string.Empty;
            _nextBtn.gameObject.SetActive(false);
            _sdsSheet.sprite = SdsSprite(q);
            SheetChanged?.Invoke(ClipSprite(q));
            SetSdsSheetSize();
            SetSds(true, true); // the SDS opens by itself beside the question and scrolls slowly so it can be read
            if (_autoScroll != null)
                StopCoroutine(_autoScroll);
            if (!NewMcqUi)
                _autoScroll = StartCoroutine(AutoScrollSds()); // the new plain SDS does not move by itself: the learner scrolls it
            _answered = -1;
            _next = false;
            _locked = false;
            _correctShown = correctRow;

            PlaceAbove(_main.transform, CanvasH * Scale);
            ShowMain();
            if (supervisor != null)
            {
                supervisor.SetPose(SupPose.Talk);
                supervisor.Gesture();
            }

            if (lockedBefore)
            {
                var lockedRow = lockedLetter[0] - 'A';
                _silentSelect = true;
                SelectOption(lockedRow);
                _silentSelect = false;
                flags[q] = lockedRow == correctRow;
                _next = false;
                _nextBtn.gameObject.SetActive(true);
                yield return PopIn(_nextBtn, 1f);
                while (!_next)
                    yield return null;
                SetSds(false, true);
                yield return PresentableFx.Wait(0.2f);
                yield break;
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

            // client 2026-11-19: the scenario's right / wrong line, then the client's explanation of this question (as Scenarios 4-12)
            var answerSay = StartCoroutine(SayAnswer(right, q));
            _next = false;
            _nextBtn.gameObject.SetActive(true);
            yield return PopIn(_nextBtn, 1f);
            while (!_next)
                yield return null;
            StopCoroutine(answerSay);
            StopVoice();
            SetSds(false, true);
            yield return PresentableFx.Wait(0.2f);
        }

        // finger / smoke API ------------------------------------------------

        private bool _silentSelect;

        // client 2026-11-19: every question, every answer and every reason of Scenarios 13-18 is drawn at ONE fixed size (nothing auto-shrunk)
        private void ApplyFixedSizes()
        {
            var bn = Localization.Loc.IsBangla;
            // client 2026-11-19: large, clear sizes (every question, every answer and every reason at ONE size)
            // client 2026-11-20: bigger again (the box has room): readable from 2.4 m in the headset
            FixSize(_title, 56f);
            FixSize(_mcq, bn ? 38f : 46f);
            FixSize(_chemLabel, bn ? 38f : 46f);
            FixSize(_chemName, bn ? 38f : 46f);
            FixSize(_prompt, bn ? 44f : 54f); // Bangla stays at its earlier size: the longer Bangla lines must fit the box (English is the larger one)
            for (var i = 0; i < 4; i++)
            {
                FixSize(_optText[i], bn ? 38f : 50f);
                FixSize(_optLetter[i], 60f);
            }

            FixSize(_verdict, 34f);
            FixSize(_reason, bn ? 30f : 38f);
            for (var i = 0; i < 4; i++)
                FixSize(_optReason[i], bn ? 30f : 38f);
        }

        private static void FixSize(TextMeshProUGUI t, float size)
        {
            if (t == null)
                return;
            t.enableAutoSizing = false;
            t.fontSize = size;
            if (t is Localization.BnTmp bt)
            {
                bt.NoShrink = true; // the Bangla image is drawn at its nominal size (never scaled down to the box)
                bt.AvoidCapped = true;
            }
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        // The client's answer rows (client 2026-11-19), DYNAMIC: every text is measured at its fixed size in its real slot width and the box takes
        // the height the content needs (nothing overlaps, nothing is squeezed). A picture question (Scenario 18) uses four cards.
        private RectTransform _panelRt, _stripRt;

        private static float MeasureTxt(TextMeshProUGUI t, string text, float widthPx)
        {
            if (t == null || string.IsNullOrEmpty(text))
                return 0f;
            if (t is Localization.BnTmp bt)
            {
                var h = bt.MeasureHeight(text, widthPx);
                if (h >= 0f)
                    return h;
            }

            return t.GetPreferredValues(text, widthPx, 0f).y;
        }

        private static void PlacePx(RectTransform rt, float x0, float x1, float yTop, float yBottom, float w, float h)
        {
            rt.anchorMin = new Vector2(x0 / w, 1f - yBottom / h);
            rt.anchorMax = new Vector2(x1 / w, 1f - yTop / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private int _newCount;

        /// <summary>Dev check: the lower and upper edges of the question box and of the SDS sheet panel (world cm).</summary>
        private string AlignReport()
        {
            if (_panelRt == null || _sdsPanel == null)
                return "";
            var c = new Vector3[4];
            _panelRt.GetWorldCorners(c);
            var bBot = c[0].y;
            var bTop = c[1].y;
            _sdsPanel.GetWorldCorners(c);
            return ", ALIGN box bottom " + (bBot * 100f).ToString("0") + " top " + (bTop * 100f).ToString("0") + " cm | SDS bottom " + (c[0].y * 100f).ToString("0") + " top " + (c[1].y * 100f).ToString("0") + " cm (panel scale " + _sdsPanel.localScale.x.ToString("0.00") + ", rect " + _sdsPanel.rect.height.ToString("0") + " px, box " + _panelRt.rect.height.ToString("0") + " px)";
        }

        /// <summary>Dev check: the angular size in the headset of the question, answer, reason and SDS body text.</summary>
        public string LegibilityReport()
        {
            if (_main == null || _cam == null)
                return "n/a";
            var d = Vector3.Distance(_cam.position, _main.transform.position);
            var k = _main.transform.lossyScale.y;
            string Deg(float px) => (2f * Mathf.Atan(px * k * 0.5f / d) * Mathf.Rad2Deg).ToString("0.00");
            var sheetPx = _sdsContent != null ? _sdsContent.rect.height : 0f;
            var sp = _sdsSheet != null ? _sdsSheet.sprite : null;
            var bodyPx = sp != null && sheetPx > 0f ? 30f * sheetPx / sp.rect.height : 0f; // the sheet's body text is ~30 px high in its 1250 x 1705 picture
            return "dist " + d.ToString("0.00") + " m, scale " + k.ToString("0.00000") + ", answer em " + Deg(_optText[0].fontSize) + " deg, question em " + Deg(_prompt.fontSize)
                   + " deg, reason em " + Deg(_optReason[0].fontSize) + " deg, SDS body em " + Deg(bodyPx * SdsBase) + " deg, SDS sheet " + sheetPx.ToString("0") + " px tall, box " + (_panelRt != null ? _panelRt.rect.height.ToString("0") : "?") + " px, fit " + _fit.ToString("0.00") + AlignReport();
        }
        private const float StandardBoxH = 1230f; // client 2026-11-20: the shorter the box, the larger it can be drawn between the timer bar and the table (text readable)
        private Image _newFrame, _newUnder;
        private readonly Image[] _oldLook = new Image[3];
        private Image _sdsBg;
        private RectTransform _sdsViewport;
        private readonly RectTransform[] _sdsArrows = new RectTransform[2];

        // client 2026-11-20: the picture question (Scenario 18 question 2) has the box of the earlier version (glow + dark panel); every other question the new glass box
        private void SetOldLook(bool old)
        {
            if (_newFrame != null)
                _newFrame.enabled = !old; // the frame is the panel's own Image: only its component is switched
            if (_newUnder != null)
                _newUnder.gameObject.SetActive(!old);
            foreach (var ol in _oldLook)
                if (ol != null)
                    ol.gameObject.SetActive(old);
        }

        // client 2026-11-20: beside EVERY question (Scenarios 13-18) the SDS is the plain sheet without its frame box, as tall as the question box,
        // the whole sheet fitted into it (no scroll bar)
        private Scrollbar _sdsBar;

        private void ApplyPlainSds()
        {
            if (_sdsBg != null)
                _sdsBg.enabled = false;
            if (_refTitle != null)
                _refTitle.gameObject.SetActive(false);
            foreach (var a in _sdsArrows)
                if (a != null)
                    a.gameObject.SetActive(false);
            if (_sdsViewport != null)
            {
                _sdsViewport.anchorMin = new Vector2(0.02f, 0.02f);
                _sdsViewport.anchorMax = new Vector2(0.98f, 0.98f);
                _sdsViewport.offsetMin = Vector2.zero;
                _sdsViewport.offsetMax = Vector2.zero;
            }

        }

        private bool _syncingBar;

        // client 2026-11-20: the SDS body text is only ~0.3 deg in the headset: a tap / ray click on the sheet enlarges it (2.2x, over the question box,
        // centred), a second click returns it
        private const float SdsZoom = 2.2f;
        // client 2026-11-20: the sheet at rest is 1.2x larger than its box (lower and left edge fixed: it grows up and to the right), text ~10 px instead of 8
        private const float SdsBase = 1.2f;
        private float BaseX() => _sdsPanel != null ? 0.5f * (SdsBase - 1f) * _sdsPanel.rect.width : 0f;
        private float BaseY() => _sdsPanel != null ? 0.5f * (SdsBase - 1f) * _sdsPanel.rect.height : 0f; // the lower edge stays on the question box's lower edge: it grows upwards
        private bool _sdsZoomed;
        private Coroutine _zoomAnim;

        public bool SdsZoomed => _sdsZoomed;

        public void ToggleSdsZoom()
        {
            if (_main == null || !_main.gameObject.activeSelf || !_sdsOpen || _sdsPanel == null)
                return;
            SetSdsZoom(!_sdsZoomed, false);
        }

        private void SetSdsZoom(bool on, bool instant)
        {
            if (_sdsPanel == null)
                return;
            _sdsZoomed = on;
            var centre = (_sdsPanel.anchorMin.x + _sdsPanel.anchorMax.x) * 0.5f * CanvasW;
            var toScale = on ? SdsZoom : SdsBase;
            var toX = on ? 1700f - centre : BaseX();
            // the top edge of the enlarged sheet stays just under the timer bar (it slides down over the table instead)
            var toY = BaseY();
            if (on && _cam != null)
            {
                var limitPx = (SessionHud.SafeBarRatio * MainDist - 0.03f) / Mathf.Max(0.0001f, Scale * _fit);
                toY = -Mathf.Max(0f, _sdsPanel.rect.height * SdsZoom * 0.5f - limitPx + 10f);
            }

            if (_sdsBacking != null)
                _sdsBacking.gameObject.SetActive(on); // an opaque backing so the room does not shine through the enlarged sheet
            if (_zoomAnim != null)
                StopCoroutine(_zoomAnim);
            _zoomAnim = null;
            if (instant || !isActiveAndEnabled)
            {
                _sdsPanel.localScale = Vector3.one * toScale;
                _sdsPanel.anchoredPosition = new Vector2(toX, toY);
                return;
            }

            _zoomAnim = StartCoroutine(ZoomAnim(toScale, toX, toY));
        }

        private Image _sdsBacking;

        private IEnumerator ZoomAnim(float toScale, float toX, float toY)
        {
            var from = _sdsPanel.localScale.x;
            var fromX = _sdsPanel.anchoredPosition.x;
            var fromY = _sdsPanel.anchoredPosition.y;
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                var e = Mathf.SmoothStep(0f, 1f, t);
                _sdsPanel.localScale = Vector3.one * Mathf.Lerp(from, toScale, e);
                _sdsPanel.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, e), Mathf.Lerp(fromY, toY, e));
                yield return null;
            }

            _sdsPanel.localScale = Vector3.one * toScale;
            _sdsPanel.anchoredPosition = new Vector2(toX, toY);
            _zoomAnim = null;
        }

        private void SyncSdsBar()
        {
            if (_sdsBar == null || _sdsScroll == null || _sdsScroll.content == null)
                return;
            _syncingBar = true;
            var ch = _sdsScroll.content.rect.height;
            var vh = ((RectTransform)_sdsScroll.viewport).rect.height;
            _sdsBar.size = ch > 1f ? Mathf.Clamp01(vh / ch) : 1f;
            _sdsBar.SetValueWithoutNotify(_sdsScroll.verticalNormalizedPosition);
            _syncingBar = false;
        }

        // client 2026-11-19: the boxes and the NEXT button must never sink into the table: the lower edge of the question box (and of the
        // 1000 px SDS box) stands 10 cm above the table top (the chemical's base), however tall the question box is
        private float MainCenterY()
        {
            var hh = _panelRt != null ? _panelRt.rect.height + 6f + 112f : 1000f; // the box + the NEXT button under it (the SDS is as tall as the box)
            var tableTop = chemAnchor != null ? chemAnchor.position.y : _cam.position.y - 0.7f;
            // client 2026-11-19: the boxes stand UNDER the timer bar and ABOVE the table: if the room between them is less than the boxes need,
            // the pair is scaled down as much as needed (never below 55 %)
            var barY = _cam.position.y + SessionHud.SafeBarRatio * MainDist - 0.03f;
            // client 2026-11-19: at the user's EYE LEVEL: the pair is centred on the eyes, so its half-height is limited by the bar above and the table below
            var eyeY = _cam.position.y;
            var avail = 2f * Mathf.Min(barY - eyeY, eyeY - (tableTop + 0.10f));
            _fit = Mathf.Clamp(avail / (hh * Scale), 0.55f, 1f);
            var top = eyeY + hh * Scale * _fit * 0.5f;
            return top - CanvasH * 0.5f * Scale * _fit;
        }
        private readonly TextMeshProUGUI[] _optReason = new TextMeshProUGUI[4]; // the reason under an answer (client 2026-11-19: as Scenarios 1-12)
        private readonly string[] _reasonShown = new string[4];

        private void LayoutNewDynamic(int count, string reasonA, string reasonB, bool reveal = false)
        {
            _layoutBoost = 0f;
            LayoutNewDynamicCore(count, reasonA, reasonB, reveal);
            // client 2026-11-20: whatever is really DRAWN (Bangla pictures may be taller than their measure) must stay inside the box: when the lowest
            // drawn text edge is below the frame's inner bottom, the box grows by the difference and the layout runs again
            Canvas.ForceUpdateCanvases();
            var depth = LowestDrawnDepth();
            var innerBottom = _panelRt.rect.height - 83f; // the frame's glow below the content
            if (depth > innerBottom + 1f)
            {
                _layoutBoost = depth - innerBottom + 6f;
                LayoutNewDynamicCore(count, reasonA, reasonB, reveal);
                _layoutBoost = 0f;
            }

            RefreshSdsBase(); // the box may have changed its height: the sheet's lower edge follows it exactly
        }

        private float _layoutBoost;

        // the lowest edge (px below the box's top edge) of the answer and reason texts that are on show, Bangla pictures included
        private float LowestDrawnDepth()
        {
            var deepest = 0f;
            void Take(Component c)
            {
                if (c == null || !c.gameObject.activeInHierarchy)
                    return;
                var rts = c.GetComponentsInChildren<RectTransform>(false);
                var corners = new Vector3[4];
                foreach (var rt in rts)
                {
                    if (rt == null || !rt.gameObject.activeInHierarchy)
                        continue;
                    var img = rt.GetComponent<Image>();
                    if (rt != c.transform && img == null)
                        continue;
                    rt.GetWorldCorners(corners);
                    for (var k = 0; k < 4; k++)
                    {
                        var d = -_panelRt.InverseTransformPoint(corners[k]).y;
                        if (d > deepest)
                            deepest = d;
                    }
                }
            }

            for (var i = 0; i < 4; i++)
            {
                if (_optText[i] != null && _optText[i].transform.parent.parent.gameObject.activeSelf)
                    Take(_optText[i]);
                if (_optReason[i] != null && _optReason[i].gameObject.activeSelf && _reasonShown[i] != null)
                    Take(_optReason[i]);
            }

            return deepest;
        }

        // client 2026-11-20: the SDS stands on the same lower line as the question box (it is larger upwards only)
        private void RefreshSdsBase()
        {
            if (_sdsPanel == null || _sdsZoomed || _zoomAnim != null)
                return;
            Canvas.ForceUpdateCanvases();
            _sdsPanel.localScale = Vector3.one * SdsBase;
            _sdsPanel.anchoredPosition = new Vector2(_sdsPanel.anchoredPosition.x, BaseY());
        }

        private void LayoutNewDynamicCore(int count, string reasonA, string reasonB, bool reveal)
        {
            _newCount = count;
            var picture = false;
            for (var i = 0; i < count; i++)
                if (_optImg[i] != null && _optImg[i].gameObject.activeSelf)
                    picture = true;
            SetOldLook(picture);
            var w = QuestionW; // the question panel
            const float topPx = 132f, bottomPx = 83f; // the frame's title strip above the content, its glow below (NEXT hangs under the frame)
            var textW = 0.88f * w;
            var chemH = 56f; // the "Chemical Name: ..." line
            var promptH = Mathf.Max(64f, MeasureTxt(_prompt, _prompt.text, 0.74f * w) + 10f);
            var cardW = (0.955f - 0.145f) * w;
            var rowH = new float[4];
            var rowsH = 0f;
            if (picture)
                rowsH = 300f;
            else
            {
                for (var i = 0; i < count; i++)
                {
                    rowH[i] = Mathf.Max(80f, MeasureTxt(_optText[i], _optText[i].text, cardW * 0.94f) + 28f);
                    rowsH += rowH[i] + (i > 0 ? 10f : 0f);
                }
            }

            const float verdictH = 48f;
            // the reason is drawn after the answer: the box grows downwards then (the question state is shorter)
            var reasonRow = new float[4];
            if (reveal && !picture)
            {
                for (var i = 0; i < count; i++)
                {
                    reasonRow[i] = string.IsNullOrEmpty(_reasonShown[i]) ? 0f : MeasureTxt(_optReason[i], _optReason[i].text, cardW * 0.97f) + 12f;
                    rowsH += reasonRow[i];
                }
            }

            var reasonH = 0f;
            var stripH = 0.01f; // no verdict strip any more: the explanation is the voice-over, the reasons stand under the answers
            if (picture && reveal)
            {
                // the picture question keeps one verdict line with the reason under the four cards
                reasonH = MeasureTxt(_reason, _reason.text, textW) + 14f;
                stripH = verdictH + reasonH;
            }
            var content = chemH + 4f + promptH + 14f + rowsH + stripH;
            // client 2026-11-20: ONE standard box size for every question (13-18): the same height, the spare room spread between the rows
            var h = Mathf.Max(StandardBoxH, Mathf.Clamp(content + topPx + bottomPx, 600f, 1450f)) + _layoutBoost;
            var extra = picture ? 0f : Mathf.Clamp((h - (content + topPx + bottomPx)) / (count + 2), 0f, 70f);
            _panelRt.sizeDelta = new Vector2(0f, h);
            _panelRt.anchoredPosition = Vector2.zero;
            if (_sdsPanel != null)
            {
                // the SDS panel is exactly as tall as the question box and its top edge is on the same line (side by side, like the workflow picture)
                _sdsPanel.anchorMin = new Vector2(_sdsPanel.anchorMin.x, 1f - h / CanvasH); // client 2026-11-20: the same lower line as the question box (the sheet is taller upwards)
                _sdsPanel.offsetMin = new Vector2(_sdsPanel.offsetMin.x, 0f);
                _sdsPanel.offsetMax = new Vector2(_sdsPanel.offsetMax.x, 0f); // (the base shift of the sheet moved both edges: the height must not grow from question to question)
                if (_roundBtn != null)
                    _roundBtn.anchorMin = _roundBtn.anchorMax = new Vector2(1f, 1f - h / CanvasH);
            }
            // the NEXT button hangs right under the box, whatever its height, big enough to read from 3 m
            if (_nextBtn != null)
            {
                _nextBtn.anchorMin = _nextBtn.anchorMax = new Vector2(QuestionW * 0.5f / CanvasW, 1f);
                _nextBtn.pivot = new Vector2(0.5f, 1f);
                _nextBtn.sizeDelta = new Vector2(460f, 112f);
                _nextBtn.anchoredPosition = new Vector2(0f, -(h + 6f)); // outside the box, right under it (client 2026-11-19)
                FixSize(_nextText, 60f);
            }

            if (_mainBase != Vector3.zero && _cam != null)
            {
                _mainBase.y = MainCenterY();
                if (_main != null)
                    _main.transform.localScale = Vector3.one * (Scale * _fit);
                ApplyMainOffset(); // the box grew or shrank: it stays under the timer bar and above the table
            }

            if (picture)
            {
                // the earlier box (client 2026-11-20): every part at the old fractions of the panel
                PlacePx(_title.rectTransform, 0.04f * w, 0.96f * w, 0.015f * h, 0.135f * h, w, h);
                _chemLabel.gameObject.SetActive(true);
                _chemName.gameObject.SetActive(true);
                PlacePx(_chemLabel.rectTransform, 0.05f * w, 0.30f * w, 0.135f * h, 0.225f * h, w, h);
                PlacePx(_chemName.rectTransform, 0.30f * w, 0.97f * w, 0.135f * h, 0.225f * h, w, h);
                PlacePx(_mcq.rectTransform, 0.05f * w, 0.21f * w, 0.23f * h, 0.34f * h, w, h);
                PlacePx(_prompt.rectTransform, 0.21f * w, 0.96f * w, 0.22f * h, 0.385f * h, w, h);
                const float pgap = 0.014f, px0 = 0.045f;
                var pw = (0.91f - pgap * 3f) / 4f;
                for (var i = 0; i < 4; i++)
                {
                    PlacePx(_optBg[i].rectTransform, (px0 + i * (pw + pgap)) * w, (px0 + i * (pw + pgap) + pw) * w, 0.40f * h, 0.81f * h, w, h);
                    _optLetterBox[i].gameObject.SetActive(false);
                    _optLetter[i].rectTransform.SetParent(_optBg[i].rectTransform, false);
                    _optLetter[i].rectTransform.anchorMin = new Vector2(0.07f, 0.84f);
                    _optLetter[i].rectTransform.anchorMax = new Vector2(0.45f, 0.99f);
                    _optLetter[i].alignment = TextAlignmentOptions.MidlineLeft;
                }

                var sh = 0.18f * h;
                PlacePx(_stripRt, 0.04f * w, 0.96f * w, 0.81f * h, 0.81f * h + sh, w, h);
                var pvf = verdictH / sh;
                _verdict.rectTransform.anchorMin = new Vector2(0.03f, 1f - pvf);
                _verdict.rectTransform.anchorMax = new Vector2(0.97f, 1f);
                _reason.rectTransform.anchorMin = new Vector2(0f, 0f);
                _reason.rectTransform.anchorMax = new Vector2(1f, 1f - pvf);
                _verdict.rectTransform.offsetMin = Vector2.zero;
                _verdict.rectTransform.offsetMax = Vector2.zero;
                _reason.rectTransform.offsetMin = Vector2.zero;
                _reason.rectTransform.offsetMax = Vector2.zero;
                return;
            }

            PlacePx(_title.rectTransform, 0.08f * w, 0.92f * w, 44f, 114f, w, h);
            var y = topPx;
            _chemLabel.gameObject.SetActive(true);
            _chemName.gameObject.SetActive(true);
            PlacePx(_chemLabel.rectTransform, 0.07f * w, 0.30f * w, y, y + chemH, w, h);
            PlacePx(_chemName.rectTransform, 0.30f * w, 0.95f * w, y, y + chemH, w, h);
            y += chemH + 4f;
            PlacePx(_mcq.rectTransform, 0.07f * w, 0.21f * w, y, y + 56f, w, h);
            PlacePx(_prompt.rectTransform, 0.21f * w, 0.95f * w, y, y + promptH, w, h);
            y += promptH + 14f + extra;
            for (var i = 0; i < 4; i++)
            {
                var rt = _optBg[i].rectTransform;
                if (picture)
                {
                    const float gap = 0.015f;
                    var cw = (0.88f - gap * 3f) / 4f;
                    PlacePx(rt, (0.07f + i * (cw + gap)) * w, (0.07f + i * (cw + gap) + cw) * w, y, y + rowsH, w, h);
                    _optLetterBox[i].gameObject.SetActive(false);
                    _optLetter[i].rectTransform.SetParent(rt, false);
                    _optLetter[i].rectTransform.anchorMin = new Vector2(0.07f, 0.84f);
                    _optLetter[i].rectTransform.anchorMax = new Vector2(0.45f, 0.99f);
                    _optLetter[i].alignment = TextAlignmentOptions.MidlineLeft;
                }
                else
                {
                    if (i >= count)
                        continue;
                    PlacePx(rt, 0.145f * w, 0.955f * w, y, y + rowH[i], w, h);
                    _optLetterBox[i].gameObject.SetActive(true);
                    var lb = _optLetterBox[i].rectTransform;
                    lb.anchorMin = new Vector2(-(86f + 14f) / cardW, 0f);
                    lb.anchorMax = new Vector2(-14f / cardW, 1f);
                    lb.offsetMin = Vector2.zero;
                    lb.offsetMax = Vector2.zero;
                    _optLetter[i].rectTransform.SetParent(lb, false);
                    _optLetter[i].rectTransform.anchorMin = Vector2.zero;
                    _optLetter[i].rectTransform.anchorMax = Vector2.one;
                    _optLetter[i].alignment = TextAlignmentOptions.Center;
                    _optText[i].rectTransform.anchorMin = new Vector2(0.03f, 0.04f);
                    _optText[i].rectTransform.anchorMax = new Vector2(0.97f, 0.96f);
                    _optText[i].alignment = TextAlignmentOptions.MidlineLeft;
                    y += rowH[i];
                    if (reasonRow[i] > 0f)
                    {
                        _optReason[i].gameObject.SetActive(true);
                        PlacePx(_optReason[i].rectTransform, 0.16f * w, 0.95f * w, y + 2f, y + reasonRow[i], w, h);
                        y += reasonRow[i];
                    }
                    else if (_optReason[i] != null)
                        _optReason[i].gameObject.SetActive(false);
                    y += 10f + extra;
                }
            }

            if (picture)
                y += rowsH;
            y += 6f;
            PlacePx(_stripRt, 0.07f * w, 0.95f * w, y, y + stripH, w, h);
            var vf = verdictH / stripH;
            _verdict.rectTransform.anchorMin = new Vector2(0.03f, 1f - vf);
            _verdict.rectTransform.anchorMax = new Vector2(0.97f, 1f);
            _reason.rectTransform.anchorMin = new Vector2(0f, 0f);
            _reason.rectTransform.anchorMax = new Vector2(1f, 1f - vf);
            _verdict.rectTransform.offsetMin = Vector2.zero;
            _verdict.rectTransform.offsetMax = Vector2.zero;
            _reason.rectTransform.offsetMin = Vector2.zero;
            _reason.rectTransform.offsetMax = Vector2.zero;
        }

        public void SelectOption(int row)
        {
            if (_locked || _main == null || !_main.gameObject.activeSelf || row < 0 || row > 3 || !_optBg[row].transform.parent.gameObject.activeSelf)
                return;
            _locked = true;
            _answered = row;
            var right = row == _correctShown;
            if (!_silentSelect)
            {
                PresentableFx.Play(right ? PresentableFx.Success : PresentableFx.Error);
                PresentableFx.Haptic(true, right ? 0.5f : 0.9f, 0.18f);
            }

            for (var i = 0; i < 4; i++)
            {
                if (!_optBg[i].transform.parent.gameObject.activeSelf)
                    continue;
                if (NewMcqUi && _ansOn != null)
                {
                    _optBg[i].sprite = i == _correctShown || i == row ? _ansOn : _ansOff;
                    _optBg[i].color = i == _correctShown ? new Color(0.45f, 1f, 0.6f, 0.9f) : i == row ? new Color(1f, 0.45f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
                    continue;
                }

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
                var rText = rField != null ? rField.Get(Localization.Loc.Current) : string.Empty;
                var own = row < _shownOptions.Length ? _shownOptions[row] : null;
                if (own != null)
                {
                    var ownField = right ? own.reasonRight : own.reasonWrong; // picture questions: a reason for this very picture
                    var ownText = ownField != null ? ownField.Get(Localization.Loc.Current) : string.Empty;
                    if (!string.IsNullOrEmpty(ownText))
                        rText = ownText;
                }

                _reason.text = rText;
                if (NewMcqUi && _ansOff != null && _panelRt != null)
                {
                    // the reason under the right answer, and under the wrong answer that was picked (the doc has one "right" and one "wrong" text per question)
                    var pictureQ = false;
                    for (var k = 0; k < _newCount; k++)
                        if (_optImg[k] != null && _optImg[k].gameObject.activeSelf)
                            pictureQ = true;
                    if (!pictureQ)
                    {
                        _verdict.text = string.Empty;
                        _reason.text = string.Empty;
                    }

                    // client 2026-11-19: under every answer the reason why THIS answer is right / wrong (as Scenarios 1-12); the explanation of the
                    // whole question is the voice-over, never written here. The reasons come from the option's own reasonRight / reasonWrong in SCxx.json.
                    for (var i = 0; i < _newCount; i++)
                    {
                        string t = null;
                        var oo = i < _shownOptions.Length ? _shownOptions[i] : null;
                        if (oo != null)
                        {
                            var f = i == _correctShown ? oo.reasonRight : oo.reasonWrong;
                            t = f != null ? f.Get(Localization.Loc.Current) : null;
                        }

                        _reasonShown[i] = string.IsNullOrEmpty(t) || pictureQ ? null : t;
                        if (_optReason[i] != null)
                        {
                            _optReason[i].text = string.IsNullOrEmpty(t) || pictureQ ? string.Empty : t; // (no "Reason:" tag in front: a joined line would need its own Bangla picture)
                            _optReason[i].color = i == _correctShown ? new Color(0.7f, 1f, 0.8f) : new Color(1f, 0.8f, 0.75f);
                        }
                    }

                    LayoutNewDynamic(_newCount, string.Empty, string.Empty, true);
                }
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
            SetSdsZoom(false, true); // a new question / the toggle button: the sheet is back at its normal size
            ApplyMainOffset();
            _sdsToggleText.text = Str(open ? "HideSds" : "CheckSds");
            if (_sdsAnim != null)
                StopCoroutine(_sdsAnim);
            if (instant || !isActiveAndEnabled)
            {
                _sdsPanel.anchoredPosition = new Vector2((open ? 0f : 800f) + BaseX(), _sdsPanel.anchoredPosition.y);
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
            var to = (open ? 0f : 800f) + BaseX();
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
            if (NewMcqUi)
                Canvas.ForceUpdateCanvases(); // the SDS panel was just resized with the question box: measure its window after that
            var vp = (RectTransform)_sdsScroll.viewport;
            var w = vp.rect.width;
            var sp = _sdsSheet.sprite;
            var aspect = sp != null ? sp.rect.height / Mathf.Max(1f, sp.rect.width) : 1.34f;
            if (NewMcqUi)
            {
                // client 2026-11-20: the whole sheet is visible at once, plain (no frame box, no scroll bar), as tall as the question box
                var fitH = Mathf.Min(w * aspect, vp.rect.height);
                var fitW = fitH / aspect;
                _sdsContent.sizeDelta = new Vector2(fitW - w, fitH);
                _sdsScroll.verticalNormalizedPosition = 1f;
                return;
            }

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
            var baseScale = t.GetComponent<Canvas>() != null ? Vector3.one * (Scale * (_main != null && t == _main.transform ? _fit : 1f)) : Vector3.one;
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
                    pause -= SessionClock.GameDelta;
                    last = cur;
                    yield return null;
                    continue;
                }

                cur = Mathf.Clamp01(cur + dir * SessionClock.GameDelta / 28f);
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
            _main = PresentableUi.WorldCanvas("ScCheckMain", transform, new Vector2(CanvasW, CanvasH), Scale, true, 30);
            _mainGroup = _main.gameObject.AddComponent<CanvasGroup>();
            DraggablePanel.Attach(_main);
            var panel = BigSds
                ? PresentableUi.Rect("Panel", _main.transform, new Vector2(0f, NewMcqUi ? 0.10f : 0.359f), new Vector2(NewMcqUi ? QuestionW / CanvasW : 0.42f, 1f)) // 1050 x 641 px (the new design: 1050 x 800)
                : PresentableUi.Rect("Panel", _main.transform, new Vector2(0f, 0.255f), new Vector2(0.4773f, 1f));
            var frameSp = NewMcqUi ? LoadSprite("ScCheck/ui_mcq_frame") : null;
            var newUi = NewMcqUi && frameSp != null;
            if (newUi)
            {
                // dynamic height (LayoutNewDynamic): the panel hangs from the top of the canvas
                _panelRt = panel;
                panel.anchorMin = new Vector2(0f, 1f);
                panel.anchorMax = new Vector2(QuestionW / CanvasW, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.offsetMin = Vector2.zero;
                panel.offsetMax = Vector2.zero;
                panel.sizeDelta = new Vector2(0f, 900f);
            }

            _ansOff = LoadSprite("ScCheck/ui_mcq_ans_off");
            _ansOn = LoadSprite("ScCheck/ui_mcq_ans_on");
            if (newUi)
            {
                // the client's translucent neon frame: the room shows through it (transparent, as in the picture)
                _newUnder = PresentableUi.Img(PresentableUi.Rect("Under", panel, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f)), art.roundedSolid, new Color(0.01f, 0.05f, 0.14f, 0.62f), true);
                var fr = PresentableUi.Img(panel, frameSp, new Color(1f, 1f, 1f, 0.9f), true);
                fr.type = Image.Type.Sliced;
                fr.pixelsPerUnitMultiplier = 1.2f; // the border pieces (title strip, glow) stay the same size however tall the box grows
                fr.raycastTarget = true;
                _newFrame = fr;
                // client 2026-11-20: the picture question (Scenario 18 question 2) keeps the box of the earlier version so the pictograms fit: old glow + dark panel
                var oldGlow = PresentableUi.Rect("OldGlow", panel, new Vector2(-0.012f, -0.012f), new Vector2(1.012f, 1.012f));
                _oldLook[0] = PresentableUi.Img(oldGlow, screen, new Color(0.25f, 0.65f, 1f, 0.45f), true);
                _oldLook[1] = PresentableUi.Img(PresentableUi.Rect("OldUnder", panel, new Vector2(0.004f, 0.004f), new Vector2(0.996f, 0.996f)), art.roundedSolid, new Color(0.02f, 0.07f, 0.15f, 0.80f), true);
                _oldLook[2] = PresentableUi.Img(PresentableUi.Rect("OldTint", panel, Vector2.zero, Vector2.one), screen, panelTint, true);
                _oldLook[2].raycastTarget = false;
                foreach (var ol in _oldLook)
                    ol.gameObject.SetActive(false);
                oldGlow.SetAsFirstSibling();
                _oldLook[1].transform.SetSiblingIndex(1);
            }
            else
            {
                var glow = PresentableUi.Rect("Glow", panel, new Vector2(-0.012f, -0.012f), new Vector2(1.012f, 1.012f));
                PresentableUi.Img(glow, screen, new Color(0.25f, 0.65f, 1f, 0.45f), true);
                PresentableUi.Img(PresentableUi.Rect("Under", panel, new Vector2(0.004f, 0.004f), new Vector2(0.996f, 0.996f)), art.roundedSolid, new Color(0.02f, 0.07f, 0.15f, 0.80f), true);
                PresentableUi.Img(panel, screen, panelTint, true).raycastTarget = true;
            }

            _title = newUi
                ? T("Title", panel, new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.945f), 52f, HoloWhite, TextAlignmentOptions.Center, true, 22f)
                : T("Title", panel, new Vector2(0.04f, 0.865f), new Vector2(0.96f, 0.985f), 52f, HoloWhite, TextAlignmentOptions.Center, true, 22f);
            _chemLabel = T("ChemLabel", panel, new Vector2(0.05f, 0.775f), new Vector2(0.30f, 0.865f), 40f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 18f);
            _chemName = T("ChemName", panel, new Vector2(0.30f, 0.775f), new Vector2(0.97f, 0.865f), 38f, HoloCyan, TextAlignmentOptions.MidlineLeft, false, 18f);
            _mcq = T("Mcq", panel, new Vector2(0.05f, 0.66f), new Vector2(0.21f, 0.77f), 42f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 18f);
            _prompt = T("Prompt", panel, new Vector2(0.21f, 0.615f), new Vector2(0.96f, 0.78f), 42f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, 18f);
            if (newUi)
            {
                // client 2026-11-19 (the workflow picture): the title, then "Chemical Name: ...", then "MCQ n:" with the question beside it
                _mcq.alignment = TextAlignmentOptions.TopLeft;
                _prompt.alignment = TextAlignmentOptions.TopLeft;
                _prompt.rectTransform.anchorMin = new Vector2(0.07f, 0.605f);
                _prompt.rectTransform.anchorMax = new Vector2(0.95f, 0.78f);
            }

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
                if (newUi)
                {
                    var lb = PresentableUi.Rect("LetterBox", card, new Vector2(0f, 0f), new Vector2(0.05f, 1f));
                    _optLetterBox[i] = PresentableUi.Img(lb, LoadSprite("ScCheck/ui_mcq_letter"), new Color(1f, 1f, 1f, 0.78f), false, false, true);
                }

                _optLetter[i] = T("Letter", card, new Vector2(0.07f, 0.84f), new Vector2(0.45f, 0.99f), 64f, HoloWhite, TextAlignmentOptions.MidlineLeft, true, 22f);
                _optLetter[i].text = ((char)('A' + i)).ToString();
                _optText[i] = T("Text", card, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.83f), 42f, HoloWhite, TextAlignmentOptions.Center, false, 22f);
                _optText[i].fontSizeMin = 14f; // the longest answers (Scenarios 17 / 18) must still fit the narrow card
                _optText[i].textWrappingMode = TextWrappingModes.Normal;
                // picture questions: the pictogram fills the card below the letter, transparent so the glass card shows around the diamond
                var imgRt = PresentableUi.Rect("Picture", card, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.86f));
                _optImg[i] = PresentableUi.Img(imgRt, null, Color.white, false, false, true);
                _optImg[i].gameObject.SetActive(false);
            }

            if (newUi)
            {
                for (var i = 0; i < 4; i++)
                {
                    _optReason[i] = T("OptReason" + i, panel, new Vector2(0.16f, 0.4f), new Vector2(0.95f, 0.45f), 24f, HoloWhite, TextAlignmentOptions.MidlineLeft, false, 12f);
                    _optReason[i].textWrappingMode = TextWrappingModes.Normal;
                    _optReason[i].gameObject.SetActive(false);
                }
            }

            // verdict strip inside the panel: its own padded band between the cards and the bottom edge
            var strip = newUi
                ? PresentableUi.Rect("VerdictStrip", panel, new Vector2(0.07f, 0.02f), new Vector2(0.95f, 0.16f))
                : PresentableUi.Rect("VerdictStrip", panel, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.19f));
            _stripRt = strip;
            _verdictBg = PresentableUi.Img(strip, art.roundedSolid, new Color(0.1f, 0.3f, 0.5f, 0.0f), true);
            _verdict = T("Verdict", strip, new Vector2(0.03f, 0.52f), new Vector2(0.97f, 0.95f), 28f, Good, TextAlignmentOptions.Center, true, 14f);
            _reason = T("Reason", strip, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.52f), 20f, HoloWhite, TextAlignmentOptions.Center, false, 10f);
            _reason.textWrappingMode = TextWrappingModes.Normal;
            if (newUi)
            {
                // client 2026-11-19: the reason is drawn on ONE line where it fits (a second line only when there is no room)
                _reason.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                _reason.rectTransform.anchorMax = new Vector2(1.0f, 0.55f);
                _reason.fontSizeMax = 26f;
                _reason.fontSizeMin = 14f;
                if (_reason is Localization.BnTmp rbt)
                    rbt.AvoidCapped = true;
            }

            _nextBtn = BigSds
                ? (NewMcqUi
                    ? PresentableUi.Rect("Next", _main.transform, new Vector2(0.18f, 0.0f), new Vector2(0.28f, 0.058f)) // under the middle of the question panel
                    : PresentableUi.Rect("Next", _main.transform, new Vector2(0.146f, 0.0f), new Vector2(0.2736f, 0.083f)))
                : PresentableUi.Rect("Next", _main.transform, new Vector2(0.166f, 0.0f), new Vector2(0.311f, 0.097f));
            _nextBtn.gameObject.AddComponent<CanvasGroup>();
            var nextImg = PresentableUi.Img(_nextBtn, nextSp, Color.white, false, true, true);
            _nextText = T("Label", _nextBtn, new Vector2(0.12f, 0.15f), new Vector2(0.88f, 0.85f), 46f, HoloWhite, TextAlignmentOptions.Center, true, 18f);
            MakeButton("NextBtn", _main.transform, _nextBtn, nextImg, PressNext);

            // SDS side panel: the frame picture (584x924, glow padding inside the file) is shown unsliced; everything sits inside its visible edge
            _sdsPanel = PresentableUi.Rect("SdsPanel", _main.transform, new Vector2(NewMcqUi ? (QuestionW + GapW) / CanvasW : BigSds ? 0.44f : 0.495f, NewMcqUi ? 1f - 1000f / CanvasH : 0f), new Vector2(1f, 1f));
            _sdsPanel.gameObject.AddComponent<CanvasGroup>();
            var sdsBg = PresentableUi.Img(_sdsPanel, sdsFrame, new Color(0.82f, 0.9f, 1f, 0.97f), false);
            _sdsBg = sdsBg;
            sdsBg.type = Image.Type.Simple;
            sdsBg.raycastTarget = true;
            // client 2026-11-19: only the question box has the new design; the SDS panel keeps its earlier look (frame, title, window)
            _refTitle = T("RefTitle", _sdsPanel, new Vector2(0.15f, 0.87f), new Vector2(0.90f, 0.94f), 36f, HoloWhite, TextAlignmentOptions.Center, true, 14f);
            // inside the frame picture's visible edge (its left glow edge is at 0.09-0.11 of the width, the right one at 0.955): the sheet must not stick out of the box
            var viewport = PresentableUi.Rect("Viewport", _sdsPanel, new Vector2(0.118f, 0.15f), new Vector2(0.955f, 0.86f));
            _sdsViewport = viewport;
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
            _sdsArrows[0] = up;
            var down = PresentableUi.Rect("Down", _sdsPanel, new Vector2(0.34f, 0.075f), new Vector2(0.48f, 0.135f));
            var downImg = PresentableUi.Img(down, art.roundedSolid, new Color(0.55f, 0.85f, 1f, 0.35f), true, true);
            T("Arrow", down, Vector2.zero, Vector2.one, 34f, HoloWhite, TextAlignmentOptions.Center, true, 14f).text = "v";
            MakeButton("DownBtn", _sdsPanel, down, downImg, () => ScrollSds(-0.35f));
            _sdsArrows[1] = down;

            if (NewMcqUi)
            {
                ApplyPlainSds();
                var cover = PresentableUi.Rect("ZoomCover", _sdsPanel, Vector2.zero, Vector2.one);
                var coverImg = PresentableUi.Img(cover, null, new Color(0f, 0f, 0f, 0.004f), false, true);
                MakeButton("SdsZoomBtn", _sdsPanel, cover, coverImg, ToggleSdsZoom);
            }

            // the round CHECK SDS toggle overlaps the lower right corner, like the picture
            _roundBtn = PresentableUi.Rect("CheckSds", _main.transform, new Vector2(1f, NewMcqUi ? 1f - 1000f / CanvasH : 0f), new Vector2(1f, NewMcqUi ? 1f - 1000f / CanvasH : 0f));
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

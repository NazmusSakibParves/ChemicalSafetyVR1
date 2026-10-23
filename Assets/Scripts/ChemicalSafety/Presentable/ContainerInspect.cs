using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC02 explore step: poking a container on the pallet tweens it into the left hand (desk: a fixed pose in
    /// front of the camera) and shows a details card beside it, with a small compatibility-chart box that opens
    /// the large chart. One container at a time; poking another, or the held one again, sends it back to the pallet.
    /// </summary>
    public sealed class ContainerInspect : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public Transform model;
            public string title;
            public string hazard;
            public string storage;
        }

        [SerializeField] private PresentableArt art;
        [SerializeField] private ChartViewer chart;
        [SerializeField] private Entry[] entries = new Entry[0];
        [SerializeField] private float heldHeight = 0.30f;
        [Tooltip("Additive dotted material for the scan beam between the container and the details card.")]
        [SerializeField] private Material scanMaterial;

        private sealed class Item
        {
            public Entry entry;
            public PokeTarget poke;
            public Vector3 homePos;
            public Quaternion homeRot;
            public Vector3 homeScale;
            public Vector3 homeCenter;
            public Vector3 localCenter;
            public Vector3 localBottom;
            public Mesh mesh;
            public float scaleFactor;
            public float holdRadius;
            public float grasp;
            public float blend;
            public Coroutine routine;
            public bool placed;
        }

        private Item[] _items = new Item[0];
        private Item _held;
        private Transform _card;
        private Vector3 _cardScale;
        private CanvasGroup _cardGroup;
        private TMP_Text _title;
        private TMP_Text _hazard;
        private TMP_Text _storage;
        private Coroutine _cardRoutine;
        private bool _interactable;
        private bool _cardAllowed = true;
        private bool _chartOpen;
        private bool _placementHidden;
        private Vector3 _holdPos;
        private Vector3 _holdVel;
        private LineRenderer _beam;
        private MaterialPropertyBlock _beamBlock;
        private float _beamLevel = 0.5f;
        private float _unfoldX = 1f;
        private CanvasGroup _boxGroup;
        private Coroutine _reveal;
        private bool _forceHand;
        private Quaternion _cardRot = Quaternion.identity;
        private Vector3 _cardFwd = Vector3.forward;
        private bool _cardHasFwd;

        public bool HasHeld => _held != null;
        public string HeldName => _held != null ? _held.entry.title : "";
        public int Count => _items.Length;

        /// <summary>The held container's pallet node (for the placement logic), or null.</summary>
        public Transform HeldTransform => _held != null ? _held.entry.model : null;
        public Entry HeldEntry => _held != null ? _held.entry : null;
        public int PlacedCount { get; private set; }

        /// <summary>Index (builder order) of the held container, or -1.</summary>
        public int HeldIndex => _held != null ? Array.IndexOf(_items, _held) : -1;

        /// <summary>Mesh of the held container (for the slot ghost).</summary>
        public Mesh HeldMesh => _held != null ? _held.mesh : null;

        /// <summary>
        /// Pose that puts the held container upright on a slot: bottom centre on the anchor, label side to +Z
        /// (the player side), scaled by <paramref name="scaleMul"/> relative to its pallet size.
        /// </summary>
        public void HeldPlacementPose(Vector3 anchor, float scaleMul, out Vector3 position, out Quaternion rotation,
            out Vector3 localScale)
        {
            var item = _held;
            rotation = item.homeRot;
            localScale = item.homeScale * scaleMul;
            var parentScale = item.entry.model.parent != null ? item.entry.model.parent.lossyScale : Vector3.one;
            var lossy = Vector3.Scale(localScale, parentScale);
            position = anchor - rotation * Vector3.Scale(lossy, item.localBottom);
        }

        /// <summary>Raised whenever a container is picked up, put back or placed.</summary>
        public event Action HeldChanged;

        private void Start()
        {
            _items = new Item[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var item = new Item { entry = e };
                _items[i] = item;
                var t = e.model;
                item.homePos = t.position;
                item.homeRot = t.rotation;
                item.homeScale = t.localScale;
                var r = t.GetComponent<Renderer>();
                var b = r != null ? r.bounds : new Bounds(t.position, Vector3.one * 0.2f);
                item.homeCenter = b.center;
                item.localCenter = t.InverseTransformPoint(b.center);
                var mf = t.GetComponent<MeshFilter>();
                item.mesh = mf != null ? mf.sharedMesh : null;
                if (item.mesh != null)
                {
                    var mb = item.mesh.bounds;
                    item.localBottom = new Vector3(mb.center.x, mb.min.y, mb.center.z);
                }
                item.scaleFactor = Mathf.Clamp(heldHeight / Mathf.Max(0.05f, b.size.y), 0.45f, 1.5f);
                // half width of the container in the hand; wide ones are held with the fingers only half closed
                item.holdRadius = Mathf.Max(b.size.x, b.size.z) * 0.5f * item.scaleFactor;
                item.grasp = Mathf.Clamp(Mathf.InverseLerp(0.15f, 0.05f, item.holdRadius), 0.35f, 1f);
                item.poke = t.GetComponent<PokeTarget>();
                if (item.poke != null)
                {
                    var captured = item;
                    item.poke.Poked += _ => OnPoked(captured);
                }
            }

            BuildCard();
            BuildBeam();
            if (chart != null)
            {
                chart.Opened += OnChartOpened;
                chart.Closed += OnChartClosed;
            }
        }

        private void OnChartOpened()
        {
            _chartOpen = true;
            ShowCard(null);
        }

        private void OnChartClosed()
        {
            _chartOpen = false;
            if (_held != null)
                ShowCard(_held, false);
        }

        /// <summary>The details card is for inspecting at the pallet; it is switched off at the cabinet.</summary>
        public void SetCardAllowed(bool allowed)
        {
            _cardAllowed = allowed;
            ShowCard(allowed && _held != null ? _held : null, false);
        }

        public bool CardVisible => _card != null && _card.gameObject.activeSelf && _cardGroup.alpha > 0.5f;

        public void SetInteractable(bool value)
        {
            _interactable = value;
            foreach (var item in _items)
            {
                if (item.poke != null)
                    item.poke.SetInteractable(value && !item.placed && item != _held);
            }

            if (!value)
                ReturnAll();
        }

        public PokeTarget PokeOf(int index) => _items[index].poke;

        /// <summary>Hides the details card while the held container stays in the hand (door feedback shows instead).</summary>
        public void HideCardForPlacement()
        {
            _placementHidden = true;
            ShowCard(null);
        }

        /// <summary>Brings the details card back for the container still in the hand.</summary>
        public void RestoreCard()
        {
            _placementHidden = false;
            if (_held != null)
                ShowCard(_held, false);
        }

        /// <summary>Editor tests: hold the container at the glove palm even without a headset.</summary>
        public void ForceHandMode() => _forceHand = true;

        public void ReturnAll()
        {
            if (_held != null)
                Release(_held);
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            _held = null;
            ShowCard(null);
            HeldChanged?.Invoke();
        }

        /// <summary>
        /// Takes the held container out of the inspect system for good (it was placed in a compartment).
        /// The caller animates the returned transform; the card closes and the pallet loses the item.
        /// </summary>
        public Transform DetachHeld()
        {
            if (_held == null)
                return null;
            var item = _held;
            if (item.routine != null)
                StopCoroutine(item.routine);
            item.routine = null;
            item.placed = true;
            if (item.poke != null)
                item.poke.SetInteractable(false);
            _held = null;
            GlovedHandPose.Left?.SetGrasp(0f);
            GlovedHandPose.Left?.SetCradle(false);
            PlacedCount++;
            _placementHidden = false;
            ShowCard(null);
            HeldChanged?.Invoke();
            return item.entry.model;
        }

        private void OnPoked(Item item)
        {
            if (!_interactable || item.placed)
                return;
            if (_held == item)
                return;

            if (_held != null)
            {
                Release(_held);
                if (_held.poke != null)
                    _held.poke.SetInteractable(true);
            }
            if (_held == null)
            {
                _holdPos = HeldCenter(item);
                _holdVel = Vector3.zero;
            }

            _held = item;
            if (item.poke != null)
                item.poke.SetInteractable(false); // no accidental pokes on the container in the hand
            Pick(item);
            ShowCard(item);
            HeldChanged?.Invoke();
        }

        private void Pick(Item item)
        {
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
            GlovedHandPose.Left?.SetCradle(true); // the glove turns palm up under the container
            GlovedHandPose.Left?.SetGrasp(item.grasp); // fingers curl round it from below
            Tween(item, 1f);
        }

        private void Release(Item item)
        {
            PresentableFx.Play(PresentableFx.Whoosh, 0.25f);
            Tween(item, 0f);
        }

        private void Tween(Item item, float target)
        {
            if (item.routine != null)
                StopCoroutine(item.routine);
            item.routine = StartCoroutine(BlendTo(item, target));
        }

        private IEnumerator BlendTo(Item item, float target)
        {
            var from = item.blend;
            yield return PresentableFx.Tween(0.6f, t => item.blend = Mathf.Lerp(from, target, t),
                PresentableFx.EaseOutCubic);
            item.blend = target;
            Apply(item);
            item.routine = null;
        }

        private void LateUpdate()
        {
            if (_held != null)
                _holdPos = Vector3.SmoothDamp(_holdPos, HeldCenter(_held), ref _holdVel, 0.07f);
            foreach (var item in _items)
            {
                if (!item.placed && (item.blend > 0f || item.routine != null))
                    Apply(item);
            }

            if (_held != null && _card != null && _card.gameObject.activeSelf)
                PlaceCard();
            UpdateBeam();
        }

        private void Apply(Item item)
        {
            var t = item.entry.model;
            if (item.blend <= 0f)
            {
                t.SetPositionAndRotation(item.homePos, item.homeRot);
                t.localScale = item.homeScale;
                return;
            }

            var e = Mathf.SmoothStep(0f, 1f, item.blend);
            var center = Vector3.Lerp(item.homeCenter, _holdPos, e);
            var rot = Quaternion.Slerp(item.homeRot, HeldRotation(item, center), e);
            t.localScale = Vector3.Lerp(item.homeScale, item.homeScale * item.scaleFactor, e);
            t.rotation = rot;
            t.position = center - rot * Vector3.Scale(t.lossyScale, item.localCenter);
        }

        private static Vector3 FlatForward(Transform cam)
        {
            var fwd = cam.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude < 0.001f ? Vector3.forward : fwd.normalized;
        }

        private Vector3 HeldCenter(Item item)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return item.homeCenter;
            var fwd = FlatForward(cam);
            var right = Vector3.Cross(Vector3.up, fwd);
            var hand = GlovedHandPose.Left;
            if ((XRSettings.isDeviceActive || _forceHand) && hand != null && hand.gameObject.activeInHierarchy)
            {
                // cradled: the container stands on the open palm (the glove turns palm up), the fingers curl round
                // its lower half from below, so the glove never ends up inside it
                if (hand.TryGetPalmFrame(out var pc, out _, out var pf))
                {
                    pf.y = 0f;
                    pf = pf.sqrMagnitude > 0.001f ? pf.normalized : fwd;
                    return pc + Vector3.up * (heldHeight * 0.5f + 0.03f) + pf * 0.02f;
                }

                var palm = hand.Palm != null ? hand.Palm.position : hand.transform.position;
                return palm + Vector3.up * (heldHeight * 0.32f) + fwd * 0.025f;
            }

            return cam.position + fwd * 0.62f + right * -0.2f + Vector3.up * -0.1f;
        }

        // The labels face +Z on the pallet (the player side); that side turns toward the viewer.
        private static Quaternion HeldRotation(Item item, Vector3 center)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return item.homeRot;
            var to = cam.position - center;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f)
                return item.homeRot;
            return Quaternion.FromToRotation(Vector3.forward, to.normalized) * item.homeRot;
        }

        // ------------------------------------------------------------------ card

        private void ShowCard(Item item, bool animate = true)
        {
            if (_card == null)
                return;
            if (item != null && (!_cardAllowed || _chartOpen || _placementHidden))
                item = null;
            if (_cardRoutine != null)
                StopCoroutine(_cardRoutine);
            if (_reveal != null)
            {
                StopCoroutine(_reveal);
                _reveal = null;
            }

            if (item != null)
            {
                _title.text = item.entry.title;
                _hazard.text = item.entry.hazard;
                _storage.text = item.entry.storage;
                _card.gameObject.SetActive(true);
                PlaceCard();
                if (animate)
                {
                    _reveal = StartCoroutine(Reveal());
                }
                else
                {
                    SetRevealed();
                }
            }

            _cardRoutine = StartCoroutine(FadeCard(item != null ? 1f : 0f));
        }

        private TMP_Text[] RevealTexts => new[] { _title, _hazard, _storage };

        private void SetRevealed()
        {
            _unfoldX = 1f;
            _beamLevel = 0.5f;
            foreach (var t in RevealTexts)
                t.maxVisibleCharacters = 99999;
            if (_boxGroup != null)
                _boxGroup.alpha = 1f;
        }

        // Card unfolds from the container side, the text types in, the chart box fades in last, and a dotted
        // scan beam streams from the container to the card (concept: "OXIDIZER PROPERTIES" hologram).
        private IEnumerator Reveal()
        {
            var texts = RevealTexts;
            var totals = new int[texts.Length];
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i].ForceMeshUpdate();
                totals[i] = texts[i].textInfo.characterCount;
                texts[i].maxVisibleCharacters = 0;
            }

            _boxGroup.alpha = 0f;
            _beamLevel = 1f;
            _unfoldX = 0.15f;
            PresentableFx.Play(PresentableFx.Chime, 0.25f);
            yield return PresentableFx.Tween(0.32f, k => _unfoldX = Mathf.Lerp(0.15f, 1f, k), PresentableFx.EaseOutCubic);
            yield return PresentableFx.Tween(0.55f, k =>
            {
                for (var i = 0; i < texts.Length; i++)
                    texts[i].maxVisibleCharacters = Mathf.CeilToInt(k * totals[i]);
            }, x => x);
            foreach (var t in texts)
                t.maxVisibleCharacters = 99999;
            yield return PresentableFx.Tween(0.25f, k => _boxGroup.alpha = k);
            yield return PresentableFx.Tween(0.6f, k => _beamLevel = Mathf.Lerp(1f, 0.5f, k));
            _reveal = null;
        }

        private void BuildBeam()
        {
            var go = new GameObject("ScanBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<LineRenderer>();
            _beam.sharedMaterial = scanMaterial;
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.textureMode = LineTextureMode.Tile;
            _beam.alignment = LineAlignment.View;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.numCapVertices = 0;
            _beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1f));
            _beam.widthMultiplier = 0.055f;
            _beam.enabled = false;
            _beamBlock = new MaterialPropertyBlock();
        }

        private void UpdateBeam()
        {
            if (_beam == null)
                return;
            var show = scanMaterial != null && _held != null && _card != null && _card.gameObject.activeSelf &&
                       _cardGroup.alpha > 0.02f;
            _beam.enabled = show;
            if (!show)
                return;
            var half = 900f * _card.localScale.x * 0.5f;
            var to = _card.position - _card.right * half + _card.up * 0.05f;
            var from = _holdPos + Vector3.up * (heldHeight * 0.1f);
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, to);
            _beam.textureScale = new Vector2(Mathf.Max(1f, Vector3.Distance(from, to) * 14f), 1f);
            _beamBlock.SetVector("_BaseMap_ST", new Vector4(1f, 1f, -Time.time * 1.6f, 0f));
            _beamBlock.SetColor("_BaseColor", new Color(0.45f, 0.92f, 1f, _cardGroup.alpha * _beamLevel * 0.7f));
            _beam.SetPropertyBlock(_beamBlock);
        }

        private IEnumerator FadeCard(float target)
        {
            var from = _cardGroup.alpha;
            yield return PresentableFx.Tween(0.25f, t =>
            {
                _cardGroup.alpha = Mathf.Lerp(from, target, t);
            });
            if (target <= 0f)
                _card.gameObject.SetActive(false);
            _cardRoutine = null;
        }

        private void PlaceCard()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null || _held == null)
                return;
            var camFwd = FlatForward(cam);
            var visible = _card.gameObject.activeSelf && _cardGroup.alpha > 0.05f;
            if (!_cardHasFwd || !visible)
            {
                _cardFwd = camFwd; // a new card opens straight ahead
                _cardHasFwd = true;
            }
            else
            {
                // lazy follow: the card stays put while the user looks around within about 24 degrees and only
                // re-centres, gently, when they turn further (it never sweeps over the container in the hand)
                var ang = Vector3.SignedAngle(_cardFwd, camFwd, Vector3.up);
                var excess = Mathf.Abs(ang) - 24f;
                if (excess > 0f)
                {
                    var step = excess * (1f - Mathf.Exp(-4f * Time.deltaTime));
                    _cardFwd = Quaternion.Euler(0f, Mathf.Sign(ang) * step, 0f) * _cardFwd;
                }
            }

            var fwd = _cardFwd;
            var right = Vector3.Cross(Vector3.up, fwd);
            // eye level, just right of centre (the container is in the left hand), about 0.7 m away so the whole
            // card, chart box included, can be touched
            var pos = new Vector3(cam.position.x, cam.position.y - 0.02f, cam.position.z) + fwd * 0.68f + right * 0.17f;
            var toHead = cam.position - pos;
            toHead.y = 0f;
            var want = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
            _cardRot = _card.gameObject.activeSelf && _cardGroup.alpha > 0.05f
                ? Quaternion.Slerp(_cardRot, want, 1f - Mathf.Exp(-12f * Time.deltaTime))
                : want;
            _card.position = pos;
            _card.rotation = _cardRot;
            _card.localScale = Vector3.Scale(_cardScale * Mathf.Lerp(0.9f, 1f, _cardGroup.alpha),
                new Vector3(_unfoldX, 1f, 1f));
        }

        private static TMP_Text Fit(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            t.overflowMode = TextOverflowModes.Overflow;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>Empty when every card text fits its box, otherwise the names of the ones that spill.</summary>
        public string CheckCardFits()
        {
            var bad = "";
            if (_card == null)
                return bad;
            foreach (var t in _card.GetComponentsInChildren<TMP_Text>(true))
            {
                t.ForceMeshUpdate();
                var rt = t.rectTransform;
                if (t.preferredHeight > rt.rect.height + 0.5f || t.isTextOverflowing)
                    bad += t.name + "(" + t.fontSize.ToString("0") + ") ";
            }

            return bad;
        }

        private void BuildCard()
        {
            var canvas = PresentableUi.WorldCanvas("InspectCard", transform, new Vector2(920f, 980f), 0.00042f, true, 120);
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            _card = canvas.transform;
            _cardScale = _card.localScale;
            _cardGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;
            var font = art != null ? art.englishFont : null;
            var cyan = PresentableUi.Cyan;

            var info = PresentableUi.Rect("Details", _card, new Vector2(0.02f, 0.4f), new Vector2(0.98f, 0.98f));
            if (art.frameWide != null)
                PresentableUi.Img(info, art.frameWide, Color.white);
            else
                PresentableUi.FlatBox(info, art.roundedSolid, PresentableUi.FlatBorder, PresentableUi.FlatFill);
            _title = Fit(PresentableUi.Text("Title", info, new Vector2(0.14f, 0.78f), new Vector2(0.86f, 0.9f), "", 28f,
                Color.white, TextAlignmentOptions.Left, true, font), 16f, 26f);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            PresentableUi.Text("HazardLabel", info, new Vector2(0.14f, 0.62f), new Vector2(0.86f, 0.74f), "HAZARD", 22f,
                cyan, TextAlignmentOptions.Left, true, font);
            _hazard = Fit(PresentableUi.Text("Hazard", info, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.6f), "", 26f,
                Color.white, TextAlignmentOptions.TopLeft, false, font), 16f, 26f);
            PresentableUi.Text("StoreLabel", info, new Vector2(0.14f, 0.26f), new Vector2(0.86f, 0.38f), "STORE IN", 22f,
                cyan, TextAlignmentOptions.Left, true, font);
            _storage = Fit(PresentableUi.Text("Storage", info, new Vector2(0.14f, 0.12f), new Vector2(0.86f, 0.26f), "",
                24f, Color.white, TextAlignmentOptions.TopLeft, false, font), 16f, 24f);

            var box = PresentableUi.Centered("ChartBox", _card, new Vector2(0f, -300f), new Vector2(340f, 340f));
            box.localPosition += new Vector3(0f, 0f, -8f);
            _boxGroup = box.gameObject.AddComponent<CanvasGroup>();
            var hit = PresentableUi.Img(box, art.frameWide != null ? art.frameWide : art.roundedSolid, Color.white, raycast: true);
            // Centred in the glass, between the top border and the caption. Not stuck to either.
            const float cell = 0.048f;
            const float step = 0.058f;
            const float x0 = 0.418f;
            const float y0 = 0.628f;
            for (var gy = 0; gy < 3; gy++)
            for (var gx = 0; gx < 3; gx++)
            {
                var x = x0 + gx * step;
                var y = y0 + gy * step;
                var tile = PresentableUi.Rect("Cell" + gy + gx, box, new Vector2(x, y), new Vector2(x + cell, y + cell));
                PresentableUi.Img(tile, art.roundedSolid, gy == 1 && gx == 1 ? PresentableUi.Green : cyan);
            }
            Fit(PresentableUi.Text("Label", box, new Vector2(0.12f, 0.38f), new Vector2(0.88f, 0.6f),
                "CHEMICAL\nCOMPATIBILITY\nCHART", 22f, Color.white, TextAlignmentOptions.Center, true, font), 14f, 22f);
            var scan = PresentableUi.Rect("Scan", box, new Vector2(0.28f, 0.16f), new Vector2(0.72f, 0.32f));
            PresentableUi.Img(scan, art.button, PresentableUi.Green);
            PresentableUi.Text("ScanLabel", scan, Vector2.zero, Vector2.one, "SCAN", 22f, Color.white,
                TextAlignmentOptions.Center, true, font);
            var btn = box.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() =>
            {
                PresentableFx.Play(PresentableFx.Tick);
                if (chart != null)
                    chart.Open();
            });

            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }
    }
}

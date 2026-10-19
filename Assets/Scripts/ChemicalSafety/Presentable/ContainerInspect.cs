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
        [SerializeField] private float heldHeight = 0.34f;

        private sealed class Item
        {
            public Entry entry;
            public PokeTarget poke;
            public Vector3 homePos;
            public Quaternion homeRot;
            public Vector3 homeScale;
            public Vector3 homeCenter;
            public Vector3 localCenter;
            public float scaleFactor;
            public float blend;
            public Coroutine routine;
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
        private Vector3 _holdPos;
        private Vector3 _holdVel;
        private Quaternion _cardRot = Quaternion.identity;

        public bool HasHeld => _held != null;
        public string HeldName => _held != null ? _held.entry.title : "";
        public int Count => _items.Length;

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
                item.scaleFactor = Mathf.Clamp(heldHeight / Mathf.Max(0.05f, b.size.y), 0.45f, 1.5f);
                item.poke = t.GetComponent<PokeTarget>();
                if (item.poke != null)
                {
                    var captured = item;
                    item.poke.Poked += _ => OnPoked(captured);
                }
            }

            BuildCard();
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            foreach (var item in _items)
            {
                if (item.poke != null)
                    item.poke.SetInteractable(value);
            }

            if (!value)
                ReturnAll();
        }

        public PokeTarget PokeOf(int index) => _items[index].poke;

        public void ReturnAll()
        {
            if (_held != null)
                Release(_held);
            _held = null;
            ShowCard(null);
        }

        private void OnPoked(Item item)
        {
            if (!_interactable)
                return;
            if (_held == item)
            {
                Release(item);
                _held = null;
                ShowCard(null);
                return;
            }

            if (_held != null)
                Release(_held);
            if (_held == null)
            {
                _holdPos = HeldCenter(item);
                _holdVel = Vector3.zero;
            }

            _held = item;
            Pick(item);
            ShowCard(item);
        }

        private void Pick(Item item)
        {
            PresentableFx.Play(PresentableFx.Whoosh, 0.35f);
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
                if (item.blend > 0f || item.routine != null)
                    Apply(item);
            }

            if (_held != null && _card != null && _card.gameObject.activeSelf)
                PlaceCard();
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
            if (XRSettings.isDeviceActive && hand != null && hand.gameObject.activeInHierarchy)
                return hand.transform.position + Vector3.up * (0.07f + heldHeight * 0.5f) + fwd * 0.03f;
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

        private void ShowCard(Item item)
        {
            if (_card == null)
                return;
            if (_cardRoutine != null)
                StopCoroutine(_cardRoutine);
            if (item != null)
            {
                _title.text = item.entry.title;
                _hazard.text = item.entry.hazard;
                _storage.text = item.entry.storage;
                _card.gameObject.SetActive(true);
                PlaceCard();
            }

            _cardRoutine = StartCoroutine(FadeCard(item != null ? 1f : 0f));
        }

        private IEnumerator FadeCard(float target)
        {
            var from = _cardGroup.alpha;
            yield return PresentableFx.Tween(0.25f, t =>
            {
                _cardGroup.alpha = Mathf.Lerp(from, target, t);
                _card.localScale = _cardScale * Mathf.Lerp(0.9f, 1f, _cardGroup.alpha);
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
            var fwd = FlatForward(cam);
            var right = Vector3.Cross(Vector3.up, fwd);
            var pos = _holdPos + right * 0.42f + Vector3.up * 0.08f;
            var want = Quaternion.LookRotation(pos - cam.position, Vector3.up);
            _cardRot = _card.gameObject.activeSelf && _cardGroup.alpha > 0.05f
                ? Quaternion.Slerp(_cardRot, want, 1f - Mathf.Exp(-12f * Time.deltaTime))
                : want;
            _card.position = pos;
            _card.rotation = _cardRot;
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
            var canvas = PresentableUi.WorldCanvas("InspectCard", transform, new Vector2(880f, 1000f), 0.00048f, true, 120);
            canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            _card = canvas.transform;
            _cardScale = _card.localScale;
            _cardGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;
            var font = art != null ? art.englishFont : null;
            var cyan = PresentableUi.Cyan;
            var fill = new Color(0.02f, 0.1f, 0.15f, 0.94f);

            var info = PresentableUi.Rect("Details", _card, new Vector2(0f, 0.34f), Vector2.one);
            PresentableUi.Img(PresentableUi.Rect("Fill", info, Vector2.zero, Vector2.one), art.roundedSolid, fill, sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Frame", info, Vector2.zero, Vector2.one), art.popupFrame, Color.white,
                sliced: true);
            _title = Fit(PresentableUi.Text("Title", info, new Vector2(0.09f, 0.82f), new Vector2(0.91f, 0.94f), "", 44f,
                cyan, TextAlignmentOptions.Left, true, font), 26f, 44f);
            PresentableUi.Img("Divider", info, new Vector2(0.09f, 0.79f), new Vector2(0.91f, 0.795f), art.roundedSolid,
                new Color(cyan.r, cyan.g, cyan.b, 0.55f));
            PresentableUi.Text("HazardLabel", info, new Vector2(0.09f, 0.7f), new Vector2(0.91f, 0.78f), "HAZARD", 30f,
                cyan, TextAlignmentOptions.Left, true, font);
            _hazard = Fit(PresentableUi.Text("Hazard", info, new Vector2(0.09f, 0.44f), new Vector2(0.91f, 0.7f), "", 36f,
                Color.white, TextAlignmentOptions.TopLeft, false, font), 22f, 36f);
            PresentableUi.Text("StoreLabel", info, new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.42f), "STORE IN", 30f,
                cyan, TextAlignmentOptions.Left, true, font);
            _storage = Fit(PresentableUi.Text("Storage", info, new Vector2(0.09f, 0.08f), new Vector2(0.91f, 0.34f), "",
                36f, Color.white, TextAlignmentOptions.TopLeft, false, font), 22f, 36f);

            var box = PresentableUi.Rect("ChartBox", _card, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.3f));
            var hit = PresentableUi.Img(PresentableUi.Rect("Hit", box, Vector2.zero, Vector2.one), null,
                new Color(1f, 1f, 1f, 0.02f), raycast: true);
            PresentableUi.Img(PresentableUi.Rect("Fill", box, Vector2.zero, Vector2.one), art.roundedSolid, fill, sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Frame", box, Vector2.zero, Vector2.one), art.answerBox, Color.white,
                sliced: true);
            PresentableUi.Img("Thumb", box, new Vector2(0.06f, 0.16f), new Vector2(0.36f, 0.84f), art.chartSc02,
                Color.white, true);
            Fit(PresentableUi.Text("Label", box, new Vector2(0.4f, 0.45f), new Vector2(0.94f, 0.86f),
                "CHEMICAL COMPATIBILITY CHART", 30f, cyan, TextAlignmentOptions.Left, true, font), 20f, 30f);
            Fit(PresentableUi.Text("Hint", box, new Vector2(0.4f, 0.14f), new Vector2(0.94f, 0.45f), "Touch to enlarge",
                26f, Color.white, TextAlignmentOptions.TopLeft, false, font), 18f, 26f);
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

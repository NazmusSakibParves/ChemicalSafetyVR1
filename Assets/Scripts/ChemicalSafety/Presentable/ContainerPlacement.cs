using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// SC02 storage game. With a container in the hand, touching a glass door opens it: wrong compartment = red door
    /// and a warning card, right compartment = green door and a blinking empty slot. Tapping the slot sets the
    /// container down there. After the sixth one <see cref="AllPlaced"/> fires.
    /// </summary>
    public sealed class ContainerPlacement : MonoBehaviour
    {
        private static readonly string[] CompartmentNames = { "Flammables", "Oxidizers", "Acids", "General Storage" };

        // [container category 0 flammable, 1 oxidizer, 2 acid, 3 general][compartment]
        private static readonly string[,] Hazards =
        {
            { "", "Flammable plus oxidizer: fire and explosion risk.", "Flammable solvent near acids: fire risk and corroded containers.", "Flammables belong in the fire-rated flammable cabinet." },
            { "An oxidizer feeds a fire. Never store it with flammables.", "", "Oxidizer with acids can release toxic gas.", "Oxidizers need their own segregated compartment." },
            { "Corrosive acid in flammable storage. Never store together.", "Acid with oxidizers can release toxic gas.", "", "Acids need the acid compartment, away from cleaners and bases." },
            { "Cleaners and reactive chemicals do not belong in the flammable cabinet.", "Reactive and cleaning chemicals can react with oxidizers.", "Cleaners, bases and reactive metals can react violently with acids.", "" },
        };

        [SerializeField] private PresentableArt art;
        [SerializeField] private ContainerInspect inspect;
        [SerializeField] private CompartmentDoor[] doors = new CompartmentDoor[4];
        [Tooltip("Compartment index (0 Flammables, 1 Oxidizers, 2 Acids, 3 General) for each container, builder order.")]
        [SerializeField] private int[] containerCompartment = new int[0];
        [Tooltip("Final size of each container in the compartment, relative to its pallet size.")]
        [SerializeField] private float[] placedScale = new float[0];
        [SerializeField] private Transform[] slotAnchors = new Transform[0];
        [SerializeField] private int[] slotCompartment = new int[0];
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private CabinetRows rows;
        [Tooltip("Dashed ring drawn on the glass over the slot (where to tap).")]
        [SerializeField] private Material tapMarkMaterial;

        private bool[] _slotUsed;
        private bool _active;
        private bool _busy;
        private Transform _card;
        private CanvasGroup _cardGroup;
        private Image _cardBorder;
        private TMP_Text _cardTitle;
        private TMP_Text _cardSub;
        private TMP_Text _cardBody;
        private TMP_Text _cardCount;
        private Coroutine _cardRoutine;
        private GameObject _ghost;
        private MeshFilter _ghostMesh;
        private MeshRenderer _ghostRenderer;
        private PokeTarget _ghostPoke;
        private BoxCollider _ghostCollider;
        private MaterialPropertyBlock _block;
        private bool _slotTapped;
        private GameObject _tapZone;
        private BoxCollider _tapBox;
        private PokeTarget _tapPoke;
        private GameObject _tapMark;
        private MeshRenderer _tapMarkRenderer;
        private float _frontZ = float.NaN;
        private bool _cancel;
        private CompartmentDoor _nextDoor;

        public int Placed { get; private set; }
        public int Total => containerCompartment.Length;
        public bool IsBusy => _busy;
        public bool IsActive => _active;
        public event Action AllPlaced;
        public event Action<int> ContainerPlaced;

        private void Start()
        {
            _slotUsed = new bool[slotAnchors.Length];
            _block = new MaterialPropertyBlock();
            foreach (var d in doors)
            {
                if (d == null)
                    continue;
                d.toggleOnPoke = false;
                d.Poked += OnDoorPoked;
            }

            BuildCard();
            BuildGhost();
            BuildTapZone();
        }

        // Front of the cabinet (the glass plane): the doors' pick colliders reach 4 cm past it.
        private float FrontZ()
        {
            if (!float.IsNaN(_frontZ))
                return _frontZ;
            var z = float.MinValue;
            foreach (var d in doors)
            {
                var hit = d != null ? d.transform.Find("Hit") : null;
                var c = hit != null ? hit.GetComponent<BoxCollider>() : null;
                if (c != null)
                    z = Mathf.Max(z, c.bounds.max.z);
            }

            _frontZ = z > float.MinValue ? z - 0.04f : slotAnchors.Length > 0 ? slotAnchors[0].position.z + 0.55f : 0f;
            return _frontZ;
        }

        // The slot is deep inside the cabinet and the glove stops at the glass, so the finger taps a zone that reaches
        // from the slot out to the glass; a dashed ring on the glass shows where.
        private void BuildTapZone()
        {
            _tapZone = new GameObject("SlotTapZone");
            _tapZone.transform.SetParent(transform, false);
            _tapBox = _tapZone.AddComponent<BoxCollider>();
            _tapBox.isTrigger = true;
            _tapPoke = _tapZone.AddComponent<PokeTarget>();
            _tapPoke.Poked += _ => _slotTapped = true;

            _tapMark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _tapMark.name = "SlotTapMark";
            _tapMark.transform.SetParent(transform, false);
            Destroy(_tapMark.GetComponent<Collider>());
            _tapMarkRenderer = _tapMark.GetComponent<MeshRenderer>();
            _tapMarkRenderer.sharedMaterial = tapMarkMaterial != null ? tapMarkMaterial : ghostMaterial;
            _tapMarkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tapMarkRenderer.receiveShadows = false;
            _tapZone.SetActive(false);
            _tapMark.SetActive(false);
        }

        private void ShowTapZone()
        {
            var b = _ghostRenderer.bounds;
            var front = FrontZ();
            var backZ = b.min.z;
            var frontEdge = front + 0.07f;
            var depth = Mathf.Max(0.1f, frontEdge - backZ);
            _tapZone.transform.SetPositionAndRotation(
                new Vector3(b.center.x, b.center.y, (backZ + frontEdge) * 0.5f), Quaternion.identity);
            _tapBox.center = Vector3.zero;
            _tapBox.size = new Vector3(Mathf.Max(0.22f, b.size.x + 0.1f), Mathf.Max(0.22f, b.size.y + 0.05f), depth);
            _tapZone.SetActive(true);
            _tapPoke.SetInteractable(true);

            var s = Mathf.Max(0.3f, Mathf.Max(b.size.x, b.size.y) + 0.14f);
            _tapMark.transform.SetPositionAndRotation(new Vector3(b.center.x, b.center.y, front + 0.006f),
                Quaternion.Euler(0f, 180f, 0f)); // visible from +z, the player side
            _tapMark.transform.localScale = new Vector3(s, s, 1f);
            _tapMark.SetActive(true);
        }

        private void HideTapZone()
        {
            if (_tapZone == null)
                return;
            _tapPoke.SetInteractable(false);
            _tapZone.SetActive(false);
            _tapMark.SetActive(false);
        }

        /// <summary>Moves the eye-level board with the empty places to the user's eye height (seated or standing).</summary>
        public void AdjustRows(float headHeight)
        {
            if (rows != null)
                rows.AdjustToHead(headHeight);
        }

        public void SetActive(bool value)
        {
            _active = value;
            foreach (var d in doors)
                d?.SetInteractable(value);
            if (!value)
                HideGhost();
        }

        // ------------------------------------------------------------------ doors

        private void OnDoorPoked(CompartmentDoor door)
        {
            if (!_active)
                return;
            if (_busy)
            {
                // while a correct door waits for its slot, another door cancels that attempt
                if (_ghost != null && _ghost.activeSelf)
                {
                    _cancel = true;
                    _nextDoor = door;
                }

                return;
            }

            var held = inspect != null ? inspect.HeldIndex : -1;
            if (held < 0)
            {
                if (!door.IsBusy)
                {
                    if (door.IsOpen)
                        door.Close();
                    else
                        door.Open();
                }

                return;
            }

            StartCoroutine(TryPlace(door, held));
        }

        private IEnumerator TryPlace(CompartmentDoor door, int held)
        {
            _busy = true;
            _cancel = false;
            _slotTapped = false;
            var doorIndex = Array.IndexOf(doors, door);
            var target = containerCompartment[held];
            var correct = doorIndex == target;
            var title = inspect.HeldEntry != null ? Humanize(inspect.HeldEntry.title) : "Container";
            inspect.HideCardForPlacement();

            // other doors close first so only one is open
            foreach (var other in doors)
            {
                if (other != null && other != door && other.IsOpen)
                    other.Close();
            }

            if (!door.IsOpen)
                yield return door.OpenRoutine();

            if (!correct)
            {
                door.SetState(DoorState.Wrong);
                ShowCard(door, false, "WRONG PLACEMENT!", "(Incompatible Storage)",
                    "HAZARD: " + Hazards[Category(held), Mathf.Clamp(doorIndex, 0, 3)]);
                yield return PresentableFx.Wait(2.2f);
                HideCard();
                inspect.RestoreCard();
                door.SetState(DoorState.Neutral);
                yield return door.CloseRoutine();
                _busy = false;
                yield break;
            }

            door.SetState(DoorState.Correct);
            _placedLine = "(" + title + " to " + CompartmentNames[target] + ")";
            var slot = FreeSlot(target);
            if (slot < 0)
            {
                // should not happen; fall back to a pose in the compartment centre
                Debug.LogWarning("[Presentable] No free slot in compartment " + target);
                _busy = false;
                yield break;
            }

            ShowGhost(slot, held);
            while (!_slotTapped && !_cancel)
                yield return null;

            if (_cancel)
            {
                HideGhost();
                HideCard();
                inspect.RestoreCard();
                door.SetState(DoorState.Neutral);
                yield return door.CloseRoutine();
                _busy = false;
                var next = _nextDoor;
                _nextDoor = null;
                if (next != null && next != door)
                    OnDoorPoked(next);
                yield break;
            }

            HideGhost();
            yield return PlaceHeld(slot, held, door);
            _busy = false;
        }

        private string _placedLine = "";

        private IEnumerator PlaceHeld(int slot, int held, CompartmentDoor door)
        {
            _slotUsed[slot] = true;
            var mul = held < placedScale.Length ? placedScale[held] : 0.8f;
            inspect.HeldPlacementPose(slotAnchors[slot].position, mul, out var endPos, out var endRot, out var endScale);
            var t = inspect.DetachHeld();
            if (t == null)
                yield break;

            var startPos = t.position;
            var startRot = t.rotation;
            var startScale = t.localScale;
            PresentableFx.Play(PresentableFx.Whoosh, 0.5f);
            yield return PresentableFx.Tween(0.75f, k =>
            {
                var arc = Mathf.Sin(k * Mathf.PI) * 0.14f;
                t.position = Vector3.Lerp(startPos, endPos, k) + Vector3.up * arc;
                t.rotation = Quaternion.Slerp(startRot, endRot, k);
                t.localScale = Vector3.Lerp(startScale, endScale, k);
            }, PresentableFx.EaseOutCubic);
            t.SetPositionAndRotation(endPos, endRot);
            t.localScale = endScale;

            PresentableFx.Play(PresentableFx.Success, 0.6f);
            PresentableFx.Haptic(true, 0.6f, 0.1f);
            Placed++;
            ContainerPlaced?.Invoke(Placed);
            UpdateCount(true);
            door.SetState(DoorState.Neutral);
            // the only "correct placement" card: shown after the container is down, with the counter already raised
            ShowCard(door, true, "CORRECT PLACEMENT!", _placedLine, "Container stored safely.");
            yield return PresentableFx.Wait(1.4f);
            HideCard();
            yield return door.CloseRoutine();

            if (Placed >= Total)
            {
                yield return PresentableFx.Wait(0.4f);
                AllPlaced?.Invoke();
            }
        }

        private int FreeSlot(int compartment)
        {
            for (var i = 0; i < slotAnchors.Length; i++)
            {
                if (!_slotUsed[i] && slotCompartment[i] == compartment)
                    return i;
            }

            return -1;
        }

        private static int Category(int container) => Mathf.Clamp(container, 0, 3) == container && container < 3 ? container : 3;

        private static string Humanize(string s) =>
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase((s ?? "").ToLowerInvariant());

        // ------------------------------------------------------------------ ghost slot

        private void BuildGhost()
        {
            _ghost = new GameObject("SlotGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostMesh = _ghost.AddComponent<MeshFilter>();
            _ghostRenderer = _ghost.AddComponent<MeshRenderer>();
            _ghostRenderer.sharedMaterial = ghostMaterial;
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;
            _ghostCollider = _ghost.AddComponent<BoxCollider>();
            _ghostCollider.isTrigger = true;
            _ghostPoke = _ghost.AddComponent<PokeTarget>();
            _ghostPoke.Poked += _ => _slotTapped = true;
            _ghost.SetActive(false);
        }

        private void ShowGhost(int slot, int held)
        {
            var mesh = inspect.HeldMesh;
            var mul = held < placedScale.Length ? placedScale[held] : 0.8f;
            inspect.HeldPlacementPose(slotAnchors[slot].position, mul, out var pos, out var rot, out var scale);
            var parent = inspect.HeldTransform != null ? inspect.HeldTransform.parent : null;
            _ghost.transform.SetPositionAndRotation(pos, rot);
            _ghost.transform.localScale = parent != null ? Vector3.Scale(scale, parent.lossyScale) : scale;
            _ghostMesh.sharedMesh = mesh;
            if (mesh != null)
            {
                _ghostCollider.center = mesh.bounds.center;
                _ghostCollider.size = mesh.bounds.size + new Vector3(0.12f, 0.06f, 0.12f) / Mathf.Max(0.05f, _ghost.transform.localScale.x);
            }

            _slotTapped = false;
            _ghost.SetActive(true);
            _ghostPoke.SetInteractable(true);
            ShowTapZone();
            StartCoroutine(BlinkGhost());
        }

        private void HideGhost()
        {
            if (_ghost == null)
                return;
            _ghostPoke.SetInteractable(false);
            _ghost.SetActive(false);
            HideTapZone();
        }

        private IEnumerator BlinkGhost()
        {
            var t = 0f;
            while (_ghost != null && _ghost.activeSelf)
            {
                t += Time.deltaTime;
                var a = Mathf.Lerp(0.18f, 0.7f, 0.5f + 0.5f * Mathf.Sin(t * 7f));
                _block.SetColor("_BaseColor", new Color(0.3f, 1f, 0.5f, a));
                _ghostRenderer.SetPropertyBlock(_block);
                if (_tapMarkRenderer != null)
                {
                    var m = new MaterialPropertyBlock();
                    m.SetColor("_BaseColor", new Color(0.35f, 1f, 0.55f, Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(t * 7f))));
                    _tapMarkRenderer.SetPropertyBlock(m);
                    _tapMark.transform.Rotate(0f, 0f, 20f * Time.deltaTime, Space.Self);
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ banner card

        private void BuildCard()
        {
            var font = art != null ? art.englishFont : null;
            var canvas = PresentableUi.WorldCanvas("PlacementCard", transform, new Vector2(820f, 330f), 0.0005f, false, 150);
            canvas.gameObject.AddComponent<YawBillboard>();
            _card = canvas.transform;
            _cardGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;
            _cardBorder = PresentableUi.Img(PresentableUi.Rect("Border", _card, Vector2.zero, Vector2.one),
                art.roundedSolid, PresentableUi.Green, sliced: true);
            PresentableUi.Img(PresentableUi.Rect("Fill", _card, Vector2.zero, Vector2.one, new Vector2(6f, 6f),
                new Vector2(-6f, -6f)), art.roundedSolid, new Color(0.02f, 0.08f, 0.12f, 0.93f), sliced: true);
            _cardTitle = Fit(PresentableUi.Text("Title", _card, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.94f), "", 58f,
                PresentableUi.Green, TextAlignmentOptions.Center, true, font), 30f, 58f);
            _cardSub = Fit(PresentableUi.Text("Sub", _card, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.62f), "", 38f,
                Color.white, TextAlignmentOptions.Center, true, font), 22f, 38f);
            _cardBody = Fit(PresentableUi.Text("Body", _card, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.44f), "", 34f,
                new Color(1f, 0.82f, 0.8f), TextAlignmentOptions.Center, false, font), 20f, 34f);
            _cardCount = Fit(PresentableUi.Text("Count", _card, new Vector2(0.55f, 0.02f), new Vector2(0.97f, 0.13f), "", 28f,
                PresentableUi.Cyan, TextAlignmentOptions.Right, true, font), 18f, 28f);
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private static TMP_Text Fit(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private void ShowCard(CompartmentDoor door, bool good, string title, string sub, string body)
        {
            var c = good ? PresentableUi.Green : PresentableUi.Red;
            _cardBorder.color = c;
            _cardTitle.color = c;
            _cardTitle.text = title;
            _cardSub.text = sub;
            _cardBody.text = body;
            _cardBody.color = good ? new Color(0.75f, 1f, 0.85f) : new Color(1f, 0.82f, 0.8f);
            UpdateCount(false);
            var camT = Camera.main != null ? Camera.main.transform : null;
            if (camT != null)
            {
                var fwd = camT.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
                _card.SetPositionAndRotation(camT.position + fwd * 0.55f, Quaternion.LookRotation(fwd, Vector3.up));
            }
            _card.gameObject.SetActive(true);
            if (_cardRoutine != null)
                StopCoroutine(_cardRoutine);
            _cardRoutine = StartCoroutine(FadeCard(1f));
        }

        private void UpdateCount(bool placedJust)
        {
            _cardCount.text = Placed + " / " + Total + " placed";
        }

        private void HideCard()
        {
            if (_cardRoutine != null)
                StopCoroutine(_cardRoutine);
            _cardRoutine = StartCoroutine(FadeCard(0f));
        }

        private IEnumerator FadeCard(float target)
        {
            var from = _cardGroup.alpha;
            var baseScale = Vector3.one * 0.0005f;
            yield return PresentableFx.Tween(0.25f, t =>
            {
                _cardGroup.alpha = Mathf.Lerp(from, target, t);
                _card.localScale = baseScale * Mathf.Lerp(0.9f, 1f, _cardGroup.alpha);
            });
            if (target <= 0f)
                _card.gameObject.SetActive(false);
            _cardRoutine = null;
        }
    }
}

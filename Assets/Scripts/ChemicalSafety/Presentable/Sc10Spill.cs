using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 10: the acid spill and the red SPILL KIT. Poke the kit door, collect five items (hologram checklist), then five
    /// floating neon icons (CONTAIN, NEUTRALIZE, ABSORB, CLEAN-UP GLASS, DISPOSE) that play a scripted animation each: the absorbent
    /// sock is laid round the spill, soda ash is sprinkled, pads drop and soak, the brush sweeps the glass into the dustpan, and
    /// everything goes into the hazardous waste bag and bin. Scripted (no rigidbodies), objects are found by the names the scene
    /// builder gives them, so the behaviour is identical on the desk and on the Quest.
    /// </summary>
    public sealed class Sc10Spill : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Material sockMaterial;
        [SerializeField] private Material powderMaterial;
        [SerializeField] private Material fizzMaterial;
        [SerializeField] private Material vaporMaterial;
        [SerializeField] private AudioClip thudClip;
        [SerializeField] private AudioClip powderClip;
        [SerializeField] private AudioClip fizzClip;
        [SerializeField] private AudioClip sweepClip;
        [SerializeField] private AudioClip rustleClip;
        [SerializeField] private AudioClip glassClip;

        private const int ItemCount = 5;
        private const int PadCount = 4;
        private const float PuddleW = 1.25f;
        private const float PuddleD = 0.9f;

        private Transform _root;
        private Transform _spill;
        private Transform _kitDoor;
        private PokeTarget _kitPoke;
        private readonly Transform[] _items = new Transform[ItemCount];
        private readonly PokeTarget[] _itemPokes = new PokeTarget[ItemCount];
        private readonly Vector3[] _itemHomePos = new Vector3[ItemCount];
        private readonly Quaternion[] _itemHomeRot = new Quaternion[ItemCount];
        private readonly Vector3[] _itemHomeScale = new Vector3[ItemCount];
        private readonly bool[] _collected = new bool[ItemCount];
        private Transform _puddle;
        private Renderer _puddleR;
        private Vector3 _puddleScale;
        private Transform _bottle0;
        private Transform _bottle1;
        private readonly List<Transform> _shards = new List<Transform>();
        private readonly List<Vector3> _shardHome = new List<Vector3>();
        private readonly List<Quaternion> _shardHomeRot = new List<Quaternion>();
        private readonly List<Vector3> _shardHomeScale = new List<Vector3>();
        private readonly List<Transform> _shardParent = new List<Transform>();
        private Transform _bottle0Parent, _bottle1Parent;
        private Vector3 _bottle0Home, _bottle1Home;
        private Quaternion _bottle0Rot, _bottle1Rot;
        private Transform _powderDecal;
        private Renderer _powderDecalR;
        private readonly Transform[] _pads = new Transform[PadCount];
        private readonly Renderer[] _padR = new Renderer[PadCount];
        private readonly Vector3[] _padScale = new Vector3[PadCount];
        private Transform _pan;
        private Transform _brush;
        private Transform _bin;
        private Transform _binLid;
        private Quaternion _binLidHome;
        private Transform _bagOpen;
        private Transform _bagFill;
        private Transform _bagNeck;
        private Transform _binPoint;
        private Transform _panPoint;
        private Transform _sock;
        private MeshFilter _sockMf;
        private Mesh _sockMesh;
        private ParticleSystem _powderFx;
        private ParticleSystem _fizzFx;
        private ParticleSystem _vaporFx;
        private AudioSource _audio;
        private Transform _icon;
        private PokeTarget _iconPoke;
        private Image _iconImg;
        private bool _iconPoked;
        private Transform _panel;
        private Image[] _checks;
        private Image[] _rows;
        private bool _busy;
        private bool _kitOpen;

        public bool KitOpen => _kitOpen;
        public int Collected { get; private set; }
        public bool AllCollected => Collected >= ItemCount;
        public Transform KitDoor => _kitDoor;
        public Transform SpillCentre => _spill;
        public Transform Item(int i) => i >= 0 && i < ItemCount ? _items[i] : null;
        public string DebugState
        {
            get
            {
                var h = GlovedHandPose.Right;
                var tip = h != null && h.HasTip ? h.TipPosition : Vector3.zero;
                var sb = new System.Text.StringBuilder("wanted " + _itemsWanted + " kitOpen " + _kitOpen + " busy " + _busy + " collected " + Collected + " tip " + tip.ToString("F2"));
                for (var i = 0; i < ItemCount; i++)
                    sb.Append(" | i" + i + (_collected[i] ? " COLLECTED" : "") + " armed " + (_itemPokes[i] != null && _itemPokes[i].Interactable) + " d " + (_items[i] != null ? Mathf.Sqrt(BoundsOf(_items[i]).SqrDistance(tip)).ToString("F2") : "-"));
                return sb.ToString();
            }
        }

        public PokeTarget ItemPoke(int i) => i >= 0 && i < ItemCount ? _itemPokes[i] : null;
        public PokeTarget KitPoke => _kitPoke;
        public PokeTarget IconPoke => _iconPoke;
        public bool IconVisible => _icon != null && _icon.gameObject.activeSelf;
        public bool PanelVisible => _panel != null && _panel.gameObject.activeSelf;
        public bool CheckVisible(int i) => _checks != null && i >= 0 && i < _checks.Length && _checks[i].gameObject.activeSelf;
        public float PuddleAlpha => _puddleR != null ? _puddleR.material.GetColor("_BaseColor").a : 0f;
        public bool SockLaid { get; private set; }
        public bool Neutralized { get; private set; }
        public bool Absorbed { get; private set; }
        public bool GlassCleaned { get; private set; }
        public bool Disposed { get; private set; }
        public int ShardsInPan { get; private set; }

        public string PadInfo
        {
            get
            {
                var r = _padR[0];
                if (r == null)
                    return "no pad";
                var m = r.sharedMaterial;
                return "pad0 active " + r.gameObject.activeSelf + " pos " + r.transform.position.ToString("F2") + " rot " + r.transform.eulerAngles.ToString("F0") +
                    " scale " + r.transform.localScale.ToString("F2") + " color " + m.GetColor("_BaseColor") + " tex " + (m.GetTexture("_BaseMap") != null) +
                    " shader " + m.shader.name + " shadows " + r.shadowCastingMode;
            }
        }

        public event Action KitOpened;
        public event Action<int> ItemCollected;

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void Awake()
        {
            _root = transform.parent != null ? transform.parent : transform;
            _spill = Find(_root, "Sc10Spill");
            _kitDoor = Find(_root, "Sc10KitDoor");
            var kp = Find(_root, "Sc10KitPoke");
            _kitPoke = kp != null ? kp.GetComponent<PokeTarget>() : null;
            for (var i = 0; i < ItemCount; i++)
            {
                _items[i] = Find(_root, "Sc10Item" + i);
                if (_items[i] == null)
                    continue;
                _itemPokes[i] = _items[i].GetComponent<PokeTarget>();
                _itemHomePos[i] = _items[i].position;
                _itemHomeRot[i] = _items[i].rotation;
                _itemHomeScale[i] = _items[i].localScale;
            }

            _puddle = Find(_root, "Sc10Puddle");
            if (_puddle != null)
            {
                _puddleR = _puddle.GetComponent<Renderer>();
                _puddleScale = _puddle.localScale;
                _puddleR.material = new Material(_puddleR.sharedMaterial);
            }

            _bottle0 = Find(_root, "Sc10Bottle0");
            _bottle1 = Find(_root, "Sc10Bottle1");
            if (_bottle0 != null)
            {
                _bottle0Parent = _bottle0.parent;
                _bottle0Home = _bottle0.position;
                _bottle0Rot = _bottle0.rotation;
            }

            if (_bottle1 != null)
            {
                _bottle1Parent = _bottle1.parent;
                _bottle1Home = _bottle1.position;
                _bottle1Rot = _bottle1.rotation;
            }

            for (var i = 0; i < 40; i++)
            {
                var s = Find(_root, "Sc10Shard" + i);
                if (s == null)
                    break;
                _shards.Add(s);
                _shardParent.Add(s.parent);
                _shardHome.Add(s.position);
                _shardHomeRot.Add(s.rotation);
                _shardHomeScale.Add(s.localScale);
            }

            _powderDecal = Find(_root, "Sc10PowderDecal");
            _powderDecalR = _powderDecal != null ? _powderDecal.GetComponent<Renderer>() : null;
            for (var i = 0; i < PadCount; i++)
            {
                _pads[i] = Find(_root, "Sc10Pad" + i);
                _padR[i] = _pads[i] != null ? _pads[i].GetComponent<Renderer>() : null;
                if (_pads[i] != null)
                    _padScale[i] = _pads[i].localScale;
                if (_padR[i] != null)
                    _padR[i].material = new Material(_padR[i].sharedMaterial);
            }

            _pan = Find(_root, "Sc10Pan");
            _brush = Find(_root, "Sc10Brush");
            _bin = Find(_root, "Sc10Bin");
            _binLid = Find(_root, "Sc10BinLid");
            if (_binLid != null)
                _binLidHome = _binLid.localRotation;
            _bagOpen = Find(_root, "Sc10BagOpen");
            _bagFill = Find(_root, "Sc10BagFill");
            _bagNeck = Find(_root, "Sc10BagNeck");
            _binPoint = Find(_root, "Sc10BinPoint");
            _panPoint = Find(_root, "Sc10PanPoint");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        // Backup for the touch volumes: a fingertip that comes close to a kit item's visible shape picks it up, even if the volume
        // is a little off or the arm is short. A finger must have been away from the item first, so it never fires by itself.
        private const float TipRadius = 0.16f;
        private readonly bool[] _tipArmed = new bool[ItemCount];

        private bool _itemsWanted;
        private float _pickAllowedAt;

        private void Update()
        {
            var hand = GlovedHandPose.Right;

            // The collect step asked for touchable items. Each one is armed as soon as nothing is moving AND the fingertip is outside
            // its touch volume, so a hand resting at the kit (after the door) or at the last item never picks one up by itself.
            if (_itemsWanted && _kitOpen && !_busy)
            {
                for (var i = 0; i < ItemCount; i++)
                {
                    var pk = _itemPokes[i];
                    if (pk == null || _collected[i] || pk.Interactable)
                        continue;
                    if (hand == null || !hand.HasTip)
                    {
                        pk.SetInteractable(true); // desk mode: the mouse needs no arming
                        continue;
                    }

                    var vol = pk.GetComponent<Collider>();
                    var away = vol == null || (vol.ClosestPoint(hand.TipPosition) - hand.TipPosition).magnitude > 0.17f;
                    if (away)
                        pk.SetInteractable(true);
                }
            }

            if (!_kitOpen || _busy || hand == null || !hand.HasTip)
            {
                for (var i = 0; i < ItemCount; i++)
                    _tipArmed[i] = false;
                return;
            }

            var tip = hand.TipPosition;
            var best = -1;
            var bestD = float.MaxValue;
            for (var i = 0; i < ItemCount; i++)
            {
                if (_items[i] == null || _collected[i] || !_items[i].gameObject.activeSelf || _itemPokes[i] == null || !_itemPokes[i].Interactable)
                {
                    _tipArmed[i] = false;
                    continue;
                }

                var d = Mathf.Sqrt(BoundsOf(_items[i]).SqrDistance(tip));
                if (d > TipRadius + 0.06f)
                    _tipArmed[i] = true;
                else if (_tipArmed[i] && d <= TipRadius && d < bestD)
                {
                    best = i;
                    bestD = d;
                }
            }

            if (best < 0)
                return;
            _tipArmed[best] = false;
            Debug.Log("[SC10] fingertip backup picked item " + best + " at " + bestD.ToString("F2") + " m");
            hand.PlayPokeCurl();
            PresentableFx.Haptic(true, 0.55f, 0.07f);
            PresentableFx.Play(PresentableFx.Tick, 0.7f);
            OnItemPoked(best);
        }

        private void Start()
        {
            if (_kitPoke != null)
            {
                _kitPoke.Poked += _ => OnKitPoked();
                _kitPoke.SetInteractable(false);
            }

            for (var i = 0; i < ItemCount; i++)
            {
                if (_itemPokes[i] == null)
                    continue;
                var index = i;
                _itemPokes[i].Poked += _ => OnItemPoked(index);
                _itemPokes[i].SetInteractable(false);
            }

            BuildFx();
            BuildPanel();
            ResetAll();
        }

        // ------------------------------------------------------------------ reset

        public void ResetAll()
        {
            StopAllCoroutines();
            _busy = false;
            _kitOpen = false;
            _itemsWanted = false;
            Collected = 0;
            SockLaid = Neutralized = Absorbed = GlassCleaned = Disposed = false;
            ShardsInPan = 0;
            for (var i = 0; i < ItemCount; i++)
            {
                _collected[i] = false;
                if (_items[i] == null)
                    continue;
                _items[i].gameObject.SetActive(true);
                _items[i].SetPositionAndRotation(_itemHomePos[i], _itemHomeRot[i]);
                _items[i].localScale = _itemHomeScale[i];
                if (_itemPokes[i] != null)
                    _itemPokes[i].SetInteractable(false);
            }

            if (_kitPoke != null)
                _kitPoke.SetInteractable(false);
            if (_kitDoor != null)
                _kitDoor.localRotation = Quaternion.identity;
            if (_puddle != null)
            {
                _puddle.gameObject.SetActive(true);
                _puddle.localScale = _puddleScale;
                SetPuddleAlpha(1f);
            }

            if (_bottle0 != null)
            {
                _bottle0.SetParent(_bottle0Parent, true);
                _bottle0.gameObject.SetActive(true);
                _bottle0.SetPositionAndRotation(_bottle0Home, _bottle0Rot);
                _bottle0.localScale = Vector3.one;
            }

            if (_bottle1 != null)
            {
                _bottle1.SetParent(_bottle1Parent, true);
                _bottle1.gameObject.SetActive(true);
                _bottle1.SetPositionAndRotation(_bottle1Home, _bottle1Rot);
                _bottle1.localScale = Vector3.one;
            }

            for (var i = 0; i < _shards.Count; i++)
            {
                _shards[i].SetParent(_shardParent[i], true);
                _shards[i].gameObject.SetActive(true);
                _shards[i].SetPositionAndRotation(_shardHome[i], _shardHomeRot[i]);
                _shards[i].localScale = _shardHomeScale[i];
            }

            if (_powderDecal != null)
                _powderDecal.gameObject.SetActive(false);
            for (var i = 0; i < PadCount; i++)
                if (_pads[i] != null)
                    _pads[i].gameObject.SetActive(false);
            if (_pan != null)
                _pan.gameObject.SetActive(false);
            if (_brush != null)
                _brush.gameObject.SetActive(false);
            if (_bagOpen != null)
                _bagOpen.gameObject.SetActive(false);
            if (_binLid != null)
                _binLid.localRotation = _binLidHome;
            if (_bin != null)
                _bin.gameObject.SetActive(false);
            if (_sock != null)
                _sock.gameObject.SetActive(false);
            if (_icon != null)
                _icon.gameObject.SetActive(false);
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (_powderFx != null)
                _powderFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_fizzFx != null)
                _fizzFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            RefreshPanel();
            if (_vaporFx != null)
            {
                _vaporFx.transform.position = SpillPos() + Vector3.up * 0.05f;
                _vaporFx.Play();
            }
        }

        // ------------------------------------------------------------------ spill kit

        public void SetKitInteractable(bool on)
        {
            if (_kitPoke != null)
                _kitPoke.SetInteractable(on && !_kitOpen);
        }

        private void OnKitPoked()
        {
            if (_kitOpen || _busy)
                return;
            StartCoroutine(OpenKit());
        }

        private IEnumerator OpenKit()
        {
            _busy = true;
            if (_kitPoke != null)
                _kitPoke.SetInteractable(false);
            Sfx(thudClip, 0.5f);
            if (_kitDoor != null)
            {
                for (var t = 0f; t < 0.9f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                    // a little overshoot so the glass door swings loose
                    var a = 112f * k + Mathf.Sin(k * Mathf.PI) * 6f * (1f - k);
                    _kitDoor.localRotation = Quaternion.Euler(0f, a, 0f);
                    yield return null;
                }

                _kitDoor.localRotation = Quaternion.Euler(0f, 112f, 0f);
            }

            // open and ready only now: the director waits on KitOpen, and the items must not be switched on while the door still moves
            _kitOpen = true;
            _busy = false;
            KitOpened?.Invoke();
        }

        public void ShowCollection(bool on)
        {
            if (_panel == null)
                return;
            if (on)
            {
                var cam = Camera.main != null ? Camera.main.transform : null;
                var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var head = cam != null ? cam.position : Vector3.zero;
                // to the right of the pickable items: right of the head, a little lower, never inside the kit (it faces -X)
                var pos = head + fwd * 0.5f + right * 0.44f;
                pos.y = head.y - 0.08f;
                var kitCol = _kitPoke != null ? _kitPoke.GetComponent<Collider>() : null;
                if (kitCol != null)
                    pos.x = Mathf.Min(pos.x, kitCol.bounds.min.x - 0.08f);
                _panel.position = pos;
                _panel.rotation = Quaternion.LookRotation(Flat(pos - head), Vector3.up);
                RefreshPanel();
            }

            _panel.gameObject.SetActive(on);
        }

        public void SetItemsInteractable(bool on)
        {
            if (on)
                _pickAllowedAt = Time.time + 0.7f; // a hand that is still at the kit door cannot take anything right away
            _itemsWanted = on;
            // switching on is done by Update (armed per item); switching off is immediate
            for (var i = 0; i < ItemCount; i++)
                if (_itemPokes[i] != null && !on)
                    _itemPokes[i].SetInteractable(false);
        }

        private void OnItemPoked(int i)
        {
            if (Time.time < _pickAllowedAt)
                return;
            Debug.Log("[SC10] item " + i + " poked, busy " + _busy + " collected " + _collected[i] + " kitOpen " + _kitOpen + " tip " + (GlovedHandPose.Right != null ? GlovedHandPose.Right.TipPosition.ToString("F2") : "none"));
            if (_busy || _collected[i] || !_kitOpen)
                return;
            StartCoroutine(Collect(i));
        }

        private IEnumerator Collect(int i)
        {
            _busy = true;
            SetItemsInteractable(false);
            var item = _items[i];
            var from = item.position;
            var fromRot = item.rotation;
            var fromScale = item.localScale;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var head = cam != null ? cam.position : from;
            var dest = head + fwd * 0.42f + right * 0.1f + Vector3.down * 0.2f;
            var destRot = Quaternion.LookRotation(-fwd, Vector3.up);
            var hold = Mathf.Clamp(0.30f / Mathf.Max(0.05f, BoundsOf(item).size.y), 0.5f, 1.6f);
            Sfx(rustleClip, 0.5f);
            for (var t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                item.position = Vector3.Lerp(from, dest, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                item.rotation = Quaternion.Slerp(fromRot, destRot, k);
                item.localScale = Vector3.Lerp(fromScale, _itemHomeScale[i] * hold, k);
                yield return null;
            }

            yield return PresentableFx.Wait(0.5f);
            var held = item.position;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                var k = t / 0.35f;
                item.position = Vector3.Lerp(held, head + Vector3.down * 0.34f + fwd * 0.1f, k);
                item.localScale = Vector3.Lerp(_itemHomeScale[i] * hold, _itemHomeScale[i] * 0.02f, k * k);
                yield return null;
            }

            item.gameObject.SetActive(false);
            _collected[i] = true;
            Collected++;
            RefreshPanel();
            PresentableFx.Play(PresentableFx.Success, 0.6f);
            PresentableFx.Haptic(true, 0.4f, 0.08f);
            _busy = false;
            ItemCollected?.Invoke(i);
            SetItemsInteractable(!AllCollected);
        }

        private void BuildPanel()
        {
            if (art == null)
                return;
            var plate = art.holoPanel != null ? art.holoPanel : art.frameWide;
            var canvas = PresentableUi.WorldCanvas("Sc10CollectPanel", transform, new Vector2(760f, 760f), 0.00060f, false, 30);
            _panel = canvas.transform;
            _panel.localScale *= 1.13f; // a bit larger so the checklist reads in the headset
            var bg = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, plate, Color.white);
            if (art.holoPanel != null)
                bg.type = Image.Type.Sliced;
            var title = PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.97f),
                Sc10Content.CollectionTitle, 40f, new Color(0.62f, 0.9f, 1f), TextAlignmentOptions.Center, true, art.englishFont);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 40f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var n = Sc10Content.CollectionItems.Length;
            _checks = new Image[n];
            _rows = new Image[n];
            for (var i = 0; i < n; i++)
            {
                var top = 0.84f - i * 0.165f;
                var row = PresentableUi.Rect("Row" + i, canvas.transform, new Vector2(0.05f, top - 0.145f), new Vector2(0.95f, top));
                _rows[i] = PresentableUi.Img(row, art.holoButton != null ? art.holoButton : art.roundedSolid, new Color(0.8f, 0.95f, 1f, 0.9f), true);
                var label = PresentableUi.Text("Label", row, new Vector2(0.05f, 0.05f), new Vector2(0.76f, 0.95f),
                    Sc10Content.CollectionItems[i], 38f, new Color(0.96f, 0.99f, 1f), TextAlignmentOptions.MidlineLeft, false, art.englishFont);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 38f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var box = PresentableUi.Rect("Box", row, new Vector2(0.82f, 0.2f), new Vector2(0.94f, 0.8f));
                PresentableUi.Img(box, art.roundedSolid, new Color(0.7f, 0.95f, 1f, 0.28f));
                var ck = PresentableUi.Rect("Check", row, new Vector2(0.80f, 0.08f), new Vector2(0.96f, 0.92f));
                _checks[i] = PresentableUi.Img(ck, art.check, new Color(0.9f, 1f, 1f), false, false, true);
                _checks[i].gameObject.SetActive(false);
            }

            PresentableUi.AlwaysOnTop(canvas);
            _panel.gameObject.SetActive(false);
        }

        private void RefreshPanel()
        {
            if (_checks == null)
                return;
            for (var i = 0; i < _checks.Length; i++)
            {
                var on = _collected[i];
                _checks[i].gameObject.SetActive(on);
                _rows[i].color = on ? new Color(0.6f, 1f, 0.8f, 1f) : new Color(0.8f, 0.95f, 1f, 0.9f);
            }
        }

        // ------------------------------------------------------------------ the neon step icons

        private void EnsureIcon()
        {
            if (_icon != null || art == null)
                return;
            var canvas = PresentableUi.WorldCanvas("Sc10Icon", transform, new Vector2(520f, 640f), 0.00085f, false, 40);
            _icon = canvas.transform;
            if (art.radialGlow != null)
                PresentableUi.Img("Glow", canvas.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), art.radialGlow, new Color(0.2f, 1f, 0.4f, 0.22f));
            _iconImg = PresentableUi.Img("Icon", canvas.transform, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.90f), null, Color.white, true);
            var label = PresentableUi.Text("Label", canvas.transform, new Vector2(0.0f, 0.0f), new Vector2(1f, 0.32f), "", 74f,
                new Color(0.35f, 1f, 0.5f), TextAlignmentOptions.Center, true, art.englishFont);
            label.enableAutoSizing = true;
            label.fontSizeMin = 34f;
            label.fontSizeMax = 80f;
            label.name = "Label";
            PresentableUi.AlwaysOnTop(canvas);
            var col = _icon.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(520f, 640f, 200f);
            _iconPoke = _icon.gameObject.AddComponent<PokeTarget>();
            _iconPoke.SetInteractable(false);
            _iconPoke.Poked += _ => _iconPoked = true;
            _icon.gameObject.SetActive(false);
        }

        /// <summary>Shows a neon icon 0.6 m in front of the user at chest height and waits until it is touched.</summary>
        public IEnumerator WaitIcon(Sprite icon, string label, string sub = null)
        {
            EnsureIcon();
            if (_icon == null)
                yield break;
            _iconImg.sprite = icon;
            _iconImg.enabled = icon != null;
            var tmp = _icon.Find("Label") != null ? _icon.Find("Label").GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
                tmp.text = string.IsNullOrEmpty(sub) ? label : label + "\n<size=40%>" + sub + "</size>";
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var head = cam != null ? cam.position : _icon.position;
            _icon.position = head + fwd * 0.62f + Vector3.down * 0.10f;
            _icon.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _iconPoked = false;
            const float baseScale = 0.00085f;
            _icon.localScale = Vector3.zero;
            _icon.gameObject.SetActive(true);
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
                _icon.localScale = Vector3.one * (baseScale * k);
                yield return null;
            }

            // a finger that is already resting where the icon appears must not trigger it: wait until the hand has moved away
            for (var wait = 0f; wait < 6f; wait += Time.deltaTime)
            {
                var hand = GlovedHandPose.Right;
                if (hand == null || !hand.HasTip || (hand.TipPosition - _icon.position).magnitude > 0.3f)
                    break;
                yield return null;
            }

            yield return PresentableFx.Wait(0.3f);
            _iconPoke.SetInteractable(true);
            while (!_iconPoked)
            {
                var p = 1f + 0.05f * Mathf.Sin(Time.time * 4f);
                _icon.localScale = Vector3.one * (baseScale * p);
                yield return null;
            }

            _iconPoke.SetInteractable(false);
            PresentableFx.Haptic(true, 0.5f, 0.1f);
            PresentableFx.Play(PresentableFx.Success, 0.6f);
            for (var t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                _icon.localScale = Vector3.one * (baseScale * (1f + 0.25f * Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f)) * (1f - t / 0.25f));
                yield return null;
            }

            _icon.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 1. CONTAIN: the absorbent sock rings the spill

        public IEnumerator Contain()
        {
            _busy = true;
            var sockItem = _items[0];
            Vector3 c = SpillPos();
            var userSide = UserSideAngle(c);
            EnsureSock();
            _sock.gameObject.SetActive(true);
            const int n = 84;
            var a = 0.95f;
            var b = 0.62f;
            var radius = 0.042f;
            var pts = new List<Vector3>(n + 8);
            // the ring starts on the user's side and runs once round, overlapping its own start by a little
            var total = n + 6;
            for (var i = 0; i <= total; i++)
            {
                var th = userSide + (float)i / n * Mathf.PI * 2f;
                var wob = 0.02f * Mathf.Sin(th * 3f + 1f);
                pts.Add(c + new Vector3(Mathf.Cos(th) * (a + wob), 0f, Mathf.Sin(th) * (b - wob)));
            }

            if (sockItem != null)
            {
                sockItem.gameObject.SetActive(true);
                sockItem.localScale = _itemHomeScale[0];
            }

            Sfx(sweepClip, 0.5f);
            var dur = 3.4f;
            for (var t = 0f; t < dur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / dur);
                var count = Mathf.Clamp(Mathf.RoundToInt(k * total) + 2, 2, total + 1);
                var head = pts[Mathf.Min(count - 1, pts.Count - 1)];
                // the free end swings above the floor and settles as it is laid
                var lift = 0.30f * (1f - k) + 0.06f * Mathf.Sin(k * Mathf.PI * 3f) * (1f - k);
                BuildSockMesh(pts, count, radius, lift);
                if (sockItem != null)
                {
                    sockItem.position = head + Vector3.up * (radius + lift + 0.05f);
                    sockItem.rotation = Quaternion.Euler(0f, t * 70f, 0f);
                }

                yield return null;
            }

            BuildSockMesh(pts, total + 1, radius, 0f);
            if (sockItem != null)
                sockItem.gameObject.SetActive(false);
            Sfx(thudClip, 0.7f);
            PresentableFx.Haptic(true, 0.4f, 0.1f);
            SockLaid = true;
            _busy = false;
        }

        private void EnsureSock()
        {
            if (_sock != null)
                return;
            var go = new GameObject("Sc10SockRing");
            go.transform.SetParent(_root, false);
            _sock = go.transform;
            _sockMf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sockMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _sockMesh = new Mesh { name = "Sc10SockRuntime" };
            _sockMesh.MarkDynamic();
            _sockMf.sharedMesh = _sockMesh;
        }

        private void BuildSockMesh(List<Vector3> pts, int count, float r, float headLift)
        {
            const int sides = 10;
            var verts = new List<Vector3>(count * (sides + 1));
            var uvs = new List<Vector2>(count * (sides + 1));
            var tris = new List<int>(count * sides * 6);
            var run = 0f;
            for (var i = 0; i < count; i++)
            {
                var p = pts[i];
                var prev = pts[Mathf.Max(0, i - 1)];
                var next = pts[Mathf.Min(pts.Count - 1, i + 1)];
                var tangent = (next - prev);
                tangent.y = 0f;
                tangent = tangent.sqrMagnitude < 1e-8f ? Vector3.right : tangent.normalized;
                var side = Vector3.Cross(Vector3.up, tangent);
                // the last 10 rings rise (the end that is still being laid)
                var fromEnd = count - 1 - i;
                var lift = headLift * Mathf.Clamp01(1f - fromEnd / 10f);
                lift = lift * lift;
                var taper = i == count - 1 ? 0.55f : 1f;
                if (i > 0)
                    run += Vector3.Distance(prev, p);
                for (var s = 0; s <= sides; s++)
                {
                    var ang = (float)s / sides * Mathf.PI * 2f;
                    var off = side * (Mathf.Cos(ang) * r * taper) + Vector3.up * (Mathf.Sin(ang) * r * 0.86f * taper);
                    verts.Add(p + Vector3.up * (r * 0.86f + lift) + off);
                    uvs.Add(new Vector2(run * 2.2f, (float)s / sides));
                }
            }

            for (var i = 0; i < count - 1; i++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var i0 = i * (sides + 1) + s;
                    var i1 = i0 + 1;
                    var i2 = i0 + sides + 1;
                    var i3 = i2 + 1;
                    tris.Add(i0);
                    tris.Add(i2);
                    tris.Add(i1);
                    tris.Add(i1);
                    tris.Add(i2);
                    tris.Add(i3);
                }
            }

            _sockMesh.Clear();
            _sockMesh.SetVertices(verts);
            _sockMesh.SetUVs(0, uvs);
            _sockMesh.SetTriangles(tris, 0);
            _sockMesh.RecalculateNormals();
            _sockMesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ 2. NEUTRALIZE: soda ash

        public IEnumerator Neutralize()
        {
            _busy = true;
            var bag = _items[1];
            Vector3 c = SpillPos();
            var cam = Camera.main != null ? Camera.main.transform : null;
            var toUser = cam != null ? Flat(cam.position - c) : Vector3.back;
            var right = Vector3.Cross(Vector3.up, -toUser);
            bag.gameObject.SetActive(true);
            var scale = _itemHomeScale[1];
            var head = cam != null ? cam.position : c + Vector3.up;
            var start = head + (-toUser) * 0.45f + Vector3.down * 0.25f;
            var over = c + toUser * 0.25f + Vector3.up * 0.62f;
            var baseRot = Quaternion.LookRotation(toUser, Vector3.up);
            // the bag front faces the user; tilting about its right-hand axis sends the top toward the spill
            for (var t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                bag.position = Vector3.Lerp(start, over, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.08f);
                bag.rotation = baseRot;
                bag.localScale = Vector3.Lerp(_itemHomeScale[1] * 0.6f, scale * 1.25f, k);
                yield return null;
            }

            Sfx(rustleClip, 0.5f);
            var mouth = bag.Find("PourMouth");
            var tiltDur = 0.9f;
            for (var t = 0f; t < tiltDur; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / tiltDur);
                bag.rotation = Quaternion.AngleAxis(112f * k, right) * baseRot;
                yield return null;
            }

            if (_powderFx != null && mouth != null)
            {
                _powderFx.transform.position = mouth.position;
                _powderFx.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }

            if (_powderDecal != null)
            {
                _powderDecal.gameObject.SetActive(true);
                _powderDecal.localScale = new Vector3(0.1f, 0.1f, 1f);
            }

            if (_fizzFx != null)
            {
                _fizzFx.transform.position = c + Vector3.up * 0.03f;
                _fizzFx.Play();
            }

            if (_powderFx != null)
                _powderFx.Play();
            if (_vaporFx != null)
                _vaporFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Sfx(powderClip, 0.7f);
            Sfx(fizzClip, 0.5f);
            var pour = 3.4f;
            for (var t = 0f; t < pour; t += Time.deltaTime)
            {
                var k = t / pour;
                // the bag shakes gently while it empties and the powder stream follows the mouth
                var shake = Mathf.Sin(t * 22f) * 3.5f;
                bag.rotation = Quaternion.AngleAxis(112f + shake + 8f * k, right) * baseRot;
                if (_powderFx != null && mouth != null)
                    _powderFx.transform.position = mouth.position;
                if (_powderDecal != null)
                {
                    var s = Mathf.Lerp(0.12f, 1.0f, Mathf.SmoothStep(0f, 1f, k));
                    _powderDecal.localScale = new Vector3(PuddleW * 0.88f * s, PuddleD * 0.88f * s, 1f);
                    SetRendererAlpha(_powderDecalR, Mathf.Lerp(0.2f, 0.85f, k));
                }

                if (_puddle != null)
                {
                    _puddle.localScale = _puddleScale * Mathf.Lerp(1f, 0.84f, k);
                    SetPuddleAlpha(Mathf.Lerp(1f, 0.55f, k));
                }

                yield return null;
            }

            if (_powderFx != null)
                _powderFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (var t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
                bag.rotation = Quaternion.Slerp(Quaternion.AngleAxis(120f, right) * baseRot, baseRot, k);
                bag.position = Vector3.Lerp(over, over + Vector3.up * 0.1f, k);
                yield return null;
            }

            // the fizz calms down; the bag goes back to the hand and is put away
            for (var t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                var k = t / 0.5f;
                bag.position = Vector3.Lerp(over + Vector3.up * 0.1f, head + Vector3.down * 0.34f, k);
                bag.localScale = Vector3.Lerp(scale * 1.25f, scale * 0.02f, k * k);
                yield return null;
            }

            bag.gameObject.SetActive(false);
            if (_fizzFx != null)
                _fizzFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Neutralized = true;
            _busy = false;
        }

        // ------------------------------------------------------------------ 3. ABSORB: pads

        public IEnumerator Absorb()
        {
            _busy = true;
            var stack = _items[2];
            Vector3 c = SpillPos();
            var cam = Camera.main != null ? Camera.main.transform : null;
            var head = cam != null ? cam.position : c + Vector3.up;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            stack.gameObject.SetActive(true);
            var scale = _itemHomeScale[2];
            var from = head + fwd * 0.45f + Vector3.down * 0.3f;
            var over = c + Vector3.up * 0.75f;
            for (var t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                stack.position = Vector3.Lerp(from, over, k);
                stack.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, 20f, 0f), k);
                stack.localScale = Vector3.Lerp(scale * 0.6f, scale, k);
                yield return null;
            }

            // each pad is laid on the spill in turn (slightly overlapping, rotated), then darkens as it soaks the liquid
            var offsets = new[]
            {
                new Vector3(-0.38f, 0f, -0.2f), new Vector3(0.28f, 0f, -0.26f),
                new Vector3(-0.2f, 0f, 0.26f), new Vector3(0.36f, 0f, 0.2f)
            };
            var yaws = new[] { 8f, -14f, 21f, -6f };
            for (var i = 0; i < PadCount; i++)
            {
                var pad = _pads[i];
                if (pad == null)
                    continue;
                pad.gameObject.SetActive(true);
                var target = c + offsets[i];
                target.y = 0.012f + i * 0.0016f;
                var start = stack.position + Vector3.up * 0.04f;
                var q0 = Quaternion.Euler(18f, yaws[i] + 50f, 12f);
                var q1 = Quaternion.Euler(0f, yaws[i], 0f);
                var fall = 0.55f;
                Sfx(rustleClip, 0.4f);
                for (var t = 0f; t < fall; t += Time.deltaTime)
                {
                    var k = t / fall;
                    var e = k * k;
                    var pos = Vector3.Lerp(start, target, e);
                    pos.y += Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k);
                    // a little flutter on the way down
                    pad.position = pos + new Vector3(Mathf.Sin(k * 9f) * 0.02f * (1f - k), 0f, 0f);
                    pad.rotation = Quaternion.Slerp(q0, q1, Mathf.SmoothStep(0f, 1f, k));
                    pad.localScale = Vector3.Lerp(_padScale[i] * 0.5f, _padScale[i], Mathf.SmoothStep(0f, 1f, k));
                    yield return null;
                }

                pad.position = target;
                pad.rotation = q1;
                Sfx(thudClip, 0.35f);
                StartCoroutine(Soak(i));
                yield return PresentableFx.Wait(0.35f);
            }

            stack.gameObject.SetActive(false);
            yield return PresentableFx.Wait(1.2f);
            SetPuddleAlpha(0f);
            Absorbed = true;
            _busy = false;
        }

        private IEnumerator Soak(int i)
        {
            var r = _padR[i];
            if (r == null)
                yield break;
            var from = Color.white;
            var to = new Color(0.82f, 0.85f, 0.88f);
            for (var t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 2.2f);
                r.material.SetColor("_BaseColor", Color.Lerp(from, to, k));
                if (_puddleR != null)
                    SetPuddleAlpha(Mathf.Min(PuddleAlpha, Mathf.Lerp(0.55f, 0.04f, (i + k) / PadCount)));
                yield return null;
            }
        }

        // ------------------------------------------------------------------ 4. CLEAN-UP GLASS: brush and dustpan

        public IEnumerator CleanGlass()
        {
            _busy = true;
            Vector3 c = SpillPos();
            var panAt = _panPoint != null ? _panPoint.position : c + new Vector3(-0.75f, 0.02f, 0.25f);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var head = cam != null ? cam.position : c + Vector3.up;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var tools = _items[3];
            if (tools != null)
                tools.gameObject.SetActive(false);
            if (_pan != null)
            {
                _pan.gameObject.SetActive(true);
                _pan.position = head + fwd * 0.5f + Vector3.down * 0.35f;
                _pan.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }

            if (_brush != null)
            {
                _brush.gameObject.SetActive(true);
                _brush.position = head + fwd * 0.45f + Vector3.down * 0.3f + Vector3.Cross(Vector3.up, fwd) * 0.18f;
            }

            // the pan is set on the floor on the room side of the spill; the brush hovers over the glass
            var panStart = _pan != null ? _pan.position : panAt;
            var panRot = Quaternion.LookRotation(Flat(c - panAt), Vector3.up);
            for (var t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                if (_pan != null)
                {
                    _pan.position = Vector3.Lerp(panStart, panAt, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f);
                    _pan.rotation = Quaternion.Slerp(Quaternion.LookRotation(fwd, Vector3.up), panRot, k);
                }

                yield return null;
            }

            // glass pieces to sweep: the loose shards and the two big bottle pieces
            var pieces = new List<Transform>();
            if (_bottle0 != null)
                pieces.Add(_bottle0);
            if (_bottle1 != null)
                pieces.Add(_bottle1);
            pieces.AddRange(_shards);
            var panLocalTop = panAt + Vector3.up * 0.03f;
            var sweepDir = Flat(panAt - c);
            var side = Vector3.Cross(Vector3.up, sweepDir);
            var perStroke = 3;
            var strokes = Mathf.CeilToInt(pieces.Count / (float)perStroke);
            var idx = 0;
            for (var s = 0; s < strokes; s++)
            {
                var from = c + side * (0.38f - 0.76f * (s / Mathf.Max(1f, strokes - 1f))) - sweepDir * 0.35f;
                var to = panAt + sweepDir * 0.02f + side * 0.0f;
                var batchStart = idx;
                var batchEnd = Mathf.Min(pieces.Count, idx + perStroke);
                var starts = new List<Vector3>();
                for (var j = batchStart; j < batchEnd; j++)
                    starts.Add(pieces[j].position);
                Sfx(sweepClip, 0.5f);
                var dur = 0.95f;
                for (var t = 0f; t < dur; t += Time.deltaTime)
                {
                    var k = t / dur;
                    var e = Mathf.SmoothStep(0f, 1f, k);
                    if (_brush != null)
                    {
                        var bp = Vector3.Lerp(from, to, e);
                        bp.y = 0.09f + Mathf.Sin(k * Mathf.PI) * 0.02f;
                        _brush.position = bp + side * Mathf.Sin(k * Mathf.PI * 3f) * 0.05f;
                        _brush.rotation = Quaternion.LookRotation(sweepDir, Vector3.up) * Quaternion.Euler(28f, 0f, Mathf.Sin(k * Mathf.PI * 3f) * 9f);
                    }

                    for (var j = batchStart; j < batchEnd; j++)
                    {
                        var jj = j - batchStart;
                        var kk = Mathf.Clamp01((k - 0.15f * jj / perStroke) / (1f - 0.15f));
                        var ee = Mathf.SmoothStep(0f, 1f, kk);
                        var p = Vector3.Lerp(starts[jj], panLocalTop + side * ((jj - 1) * 0.05f), ee);
                        p.y = Mathf.Lerp(starts[jj].y, panLocalTop.y + 0.004f, ee) + Mathf.Sin(kk * Mathf.PI) * 0.025f;
                        pieces[j].position = p;
                        pieces[j].Rotate(0f, 240f * Time.deltaTime * (1f - kk), 0f, Space.World);
                        if (j < 2)
                            pieces[j].localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, ee);
                    }

                    yield return null;
                }

                for (var j = batchStart; j < batchEnd; j++)
                    ShardsInPan++;
                Sfx(glassClip, 0.35f);
                idx = batchEnd;
                PresentableFx.Haptic(true, 0.25f, 0.05f);
            }

            // pieces rest in the pan and ride with it
            for (var j = 0; j < pieces.Count; j++)
                pieces[j].SetParent(_pan != null ? _pan : pieces[j].parent, true);
            for (var t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (_brush != null)
                    _brush.position = Vector3.Lerp(_brush.position, panAt + Vector3.up * 0.25f + side * 0.35f, Time.deltaTime * 4f);
                yield return null;
            }

            GlassCleaned = true;
            _busy = false;
        }

        // ------------------------------------------------------------------ 5. DISPOSE: everything into the bag, the bag into the bin

        public IEnumerator Dispose()
        {
            _busy = true;
            // the waste bin only appears now, popping up away from the spill
            if (_bin != null)
            {
                _bin.gameObject.SetActive(true);
                Sfx(thudClip, 0.6f);
                for (var t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                    _bin.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, k), Mathf.Lerp(0.05f, 1f, k), Mathf.Lerp(0.2f, 1f, k));
                    yield return null;
                }

                _bin.localScale = Vector3.one;
            }

            var bagPos = _binPoint != null ? new Vector3(_binPoint.position.x - 0.75f, 0f, _binPoint.position.z) : SpillPos();
            if (_bagOpen != null)
            {
                _bagOpen.gameObject.SetActive(true);
                _bagOpen.position = bagPos;
                _bagOpen.localScale = Vector3.one * 0.01f;
                for (var t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    _bagOpen.localScale = Vector3.one * Mathf.SmoothStep(0.01f, 1f, t / 0.5f);
                    yield return null;
                }

                _bagOpen.localScale = Vector3.one;
            }

            Sfx(rustleClip, 0.6f);
            var mouthPos = bagPos + Vector3.up * 0.55f;
            // the loose pieces: sock, the four pads, the pan with the glass
            var flights = new List<Transform>();
            if (_sock != null && _sock.gameObject.activeSelf)
                flights.Add(_sock);
            for (var i = 0; i < PadCount; i++)
                if (_pads[i] != null && _pads[i].gameObject.activeSelf)
                    flights.Add(_pads[i]);
            var fill = 0f;
            foreach (var f in flights)
            {
                var from = f.position;
                var fromScale = f.localScale;
                var dur = 0.85f;
                for (var t = 0f; t < dur; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / dur);
                    var p = Vector3.Lerp(from, mouthPos, k);
                    p.y += Mathf.Sin(k * Mathf.PI) * 0.35f;
                    f.position = p;
                    f.localScale = fromScale * Mathf.Lerp(1f, 0.25f, k);
                    yield return null;
                }

                f.gameObject.SetActive(false);
                f.localScale = fromScale;
                fill += 0.18f;
                if (_bagFill != null)
                    _bagFill.localScale = new Vector3(1f, 0.4f + fill, 1f);
                PresentableFx.Haptic(true, 0.3f, 0.05f);
                Sfx(rustleClip, 0.35f);
            }

            // the pan is carried over the bag and tipped
            if (_pan != null)
            {
                var from = _pan.position;
                var fromRot = _pan.rotation;
                var carry = mouthPos + Vector3.up * 0.25f;
                var tip = Quaternion.LookRotation(Flat(mouthPos - from), Vector3.up);
                for (var t = 0f; t < 1.0f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 1.0f);
                    _pan.position = Vector3.Lerp(from, carry, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f);
                    _pan.rotation = Quaternion.Slerp(fromRot, tip, k) * Quaternion.Euler(Mathf.Lerp(0f, 70f, k), 0f, 0f);
                    yield return null;
                }

                Sfx(glassClip, 0.6f);
                var panned = _pan.GetComponentsInChildren<Transform>(true);
                foreach (var p in panned)
                    if (p != _pan && p.name.StartsWith("Sc10Shard") || p.name.StartsWith("Sc10Bottle"))
                    {
                        // glass drops into the bag while the pan is tipped
                        p.gameObject.SetActive(false);
                    }

                fill += 0.12f;
                if (_bagFill != null)
                    _bagFill.localScale = new Vector3(1f, 0.4f + fill, 1f);
                yield return PresentableFx.Wait(0.6f);
                _pan.gameObject.SetActive(false);
                if (_brush != null)
                    _brush.gameObject.SetActive(false);
            }

            // twist the neck shut, then lift the bag into the bin
            if (_bagNeck != null)
            {
                for (var t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                    _bagNeck.localScale = new Vector3(Mathf.Lerp(1f, 0.28f, k), 1f, Mathf.Lerp(1f, 0.28f, k));
                    _bagNeck.localRotation = Quaternion.Euler(0f, 220f * k, 0f);
                    yield return null;
                }
            }

            Sfx(rustleClip, 0.6f);
            if (_binLid != null)
            {
                for (var t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
                    _binLid.localRotation = _binLidHome * Quaternion.Euler(-100f * k, 0f, 0f);
                    yield return null;
                }
            }

            if (_bagOpen != null && _binPoint != null)
            {
                var from = _bagOpen.position;
                var up = _binPoint.position + Vector3.up * 1.05f;
                for (var t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                    _bagOpen.position = Vector3.Lerp(from, up, k);
                    yield return null;
                }

                for (var t = 0f; t < 0.55f; t += Time.deltaTime)
                {
                    var k = t / 0.55f;
                    _bagOpen.position = Vector3.Lerp(up, _binPoint.position, k * k);
                    yield return null;
                }

                _bagOpen.gameObject.SetActive(false);
                Sfx(thudClip, 0.8f);
            }

            if (_binLid != null)
            {
                for (var t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
                    var bounce = t > 0.5f ? Mathf.Sin((t - 0.5f) * 40f) * 2f * (1f - t / 0.7f) : 0f;
                    _binLid.localRotation = _binLidHome * Quaternion.Euler(-100f * (1f - k) + bounce, 0f, 0f);
                    yield return null;
                }

                _binLid.localRotation = _binLidHome;
                Sfx(thudClip, 0.9f);
            }

            PresentableFx.Haptic(true, 0.4f, 0.1f);
            Disposed = true;
            _busy = false;
        }

        // ------------------------------------------------------------------ helpers

        private Vector3 SpillPos() => _spill != null ? _spill.position : transform.position;

        private float UserSideAngle(Vector3 c)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
                return Mathf.PI;
            var d = cam.position - c;
            return Mathf.Atan2(d.z, d.x);
        }

        private void SetPuddleAlpha(float a)
        {
            if (_puddleR == null)
                return;
            var m = _puddleR.material;
            var col = m.GetColor("_BaseColor");
            col.a = a;
            m.SetColor("_BaseColor", col);
            _puddleR.enabled = a > 0.01f;
        }

        private static void SetRendererAlpha(Renderer r, float a)
        {
            if (r == null)
                return;
            var m = r.material;
            var col = m.GetColor("_BaseColor");
            col.a = a;
            m.SetColor("_BaseColor", col);
        }

        private void Sfx(AudioClip clip, float volume)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip, volume);
        }

        private void BuildFx()
        {
            if (powderMaterial != null)
            {
                var go = new GameObject("Sc10PowderFx");
                go.transform.SetParent(transform, false);
                _powderFx = go.AddComponent<ParticleSystem>();
                var main = _powderFx.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
                main.startColor = new Color(0.97f, 0.97f, 0.95f, 0.95f);
                main.gravityModifier = 0.6f;
                main.maxParticles = 600;
                var em = _powderFx.emission;
                em.rateOverTime = 260f;
                var shape = _powderFx.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 5f;
                shape.radius = 0.018f;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = powderMaterial;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                _powderFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (vaporMaterial != null)
            {
                // a very faint haze rising from the acid on the floor (only while the acid is still there)
                var go = new GameObject("Sc10VaporFx");
                go.transform.SetParent(transform, false);
                _vaporFx = go.AddComponent<ParticleSystem>();
                var main = _vaporFx.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
                main.startColor = new Color(0.92f, 0.95f, 0.97f, 0.07f);
                main.gravityModifier = -0.012f;
                main.maxParticles = 40;
                var em = _vaporFx.emission;
                em.rateOverTime = 2.5f;
                var shape = _vaporFx.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(PuddleW * 0.5f, 0.01f, PuddleD * 0.45f);
                var col = _vaporFx.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = vaporMaterial;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                _vaporFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (fizzMaterial != null)
            {
                var go = new GameObject("Sc10FizzFx");
                go.transform.SetParent(transform, false);
                _fizzFx = go.AddComponent<ParticleSystem>();
                var main = _fizzFx.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
                main.startColor = new Color(0.92f, 0.97f, 1f, 0.5f);
                main.gravityModifier = -0.02f;
                main.maxParticles = 300;
                var em = _fizzFx.emission;
                em.rateOverTime = 55f;
                var shape = _fizzFx.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(PuddleW * 0.7f, 0.01f, PuddleD * 0.7f);
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = fizzMaterial;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                _fizzFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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

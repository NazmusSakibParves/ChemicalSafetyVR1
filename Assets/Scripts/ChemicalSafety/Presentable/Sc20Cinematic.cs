using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The film of Scenario 20, as the client's animator made it (Scene 20.blend: 780 frames at 24 fps = 32.5 s): a worker carries a chemical drum on a
    /// trolley, trips, the chemical splashes over his hands and clothes, he runs to the emergency shower, pulls the handle, takes off the vest (and the
    /// helmet) under the water, and reports to the supervisor. Blender exported the finished timeline to Sc20Film.glb; the Quest does not play glb clips,
    /// so <c>Sc20FilmBake.bytes</c> (menu Chemical Safety/Presentable/Bake Sc20 Film) holds the pose of every animated transform per frame and this class
    /// applies it by code. What the animator's file cannot hold is added here: the chemical splash and the stains on the worker's clothes, the wet look
    /// and the shower water, the pile of taken-off clothes, the voice-over lines and sounds. The learner's OWN camera follows the film camera (as in
    /// Scenario 19). Cuts (0-based frames): 426, 536, 577, 661. Scenario 19 is not touched by this class.
    /// </summary>
    public sealed class Sc20Cinematic : MonoBehaviour
    {
        public sealed class Hooks
        {
            /// <summary>A camera cut of the animator's film (shot index): the director softens it with a short fade.</summary>
            public Action<int> Cut;
            /// <summary>Voice-over line n1..n7; does not wait.</summary>
            public Action<string> SayAsync;
        }

        public const float Duration = 32.5f;
        private const float Fps = 24f;
        private static readonly int[] CutFrames = { 0, 426, 536, 577, 661 };
        // moments of the film (seconds): the trip, the handle, the vest comes off (Blender frames 536 / 577 -> (f-1)/24)
        private const float TripAt = 11.25f;
        private const float StainFrom = 11.35f, StainFull = 12.3f;
        /// <summary>Gravity multiplier of the chemical jet (the builder sets the same on the particle system); the film aims the jet with it.</summary>
        public const float StreamGravity = 1.2f;
        private const float StreamFlight = 0.26f;
        private const float HandleAt = 19.55f, WaterAt = 19.9f;
        private const float VestOff = 22.29f, ShirtlessAt = 24.0f;
        // the undressing, step by step, as in the animator's video (he hides the swap to the shirtless man behind a camera cut at 24.0 s): first the red vest
        // (22.29 s, his own timing), then the cap (hands on the head), last the dark jacket (the shirtless body is posed exactly like the dressed one, arms over
        // the head), then the cut to the shirtless man
        private const float CapOff = 23.30f, JacketOff = 23.85f;
        private const float WaterOffAt = 26.4f;               // the shower is closed before the supervisor comes (27.5 s)
        private const float RunFrom = 15.0f, RunTo = 17.6f, CutToShower = 17.75f;
        /// <summary>The whole shower scene (shower, handle, the three men, the shower cameras) is moved by this in film units along film z so the pole and the
        /// pull handle stand clear of the hall wall; the worker's run is blended into it. The builder moves the shower itself by the same amount.</summary>
        public static readonly Vector3 ShowerShift = new Vector3(0f, 0f, -1.8f);

        [SerializeField] private Transform filmRoot;
        [SerializeField] private AnimationClip[] clips;
        [SerializeField] private Renderer[] stainRenderers;   // overlay copies of the worker's clothes (the chemical)
        [SerializeField] private Renderer[] wetRenderers;     // clothes + skin that get the wet look under the shower
        [SerializeField] private Renderer vestRenderer;       // taken off at 22.3 s (the animator moves it away; a skinned node move is ignored in Unity)
        [SerializeField] private Renderer vestStainRenderer;  // the chemical overlay of the vest goes with the vest
        [SerializeField] private GameObject[] clothesPile;    // 0 = vest in the yellow bin (22.3 s), 1 = the rest of the clothes (24 s)
        [SerializeField] private GameObject puddle;           // the spill on the floor, grows after the trip
        [SerializeField] private GameObject showerPuddle;     // the wet floor under the shower (drain beside it)
        [SerializeField] private ParticleSystem stream;       // the jet of chemical out of the drum mouth
        [SerializeField] private Renderer drumRenderer;
        [SerializeField] private ParticleSystem splash;
        [SerializeField] private ParticleSystem drips;
        [SerializeField] private ParticleSystem fumes;
        [SerializeField] private ParticleSystem shower;
        [SerializeField] private ParticleSystem showerMist;
        [SerializeField] private Transform showerHead;
        [SerializeField] private AudioSource fxSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioClip slosh;
        [SerializeField] private AudioClip splashClip;
        [SerializeField] private AudioClip sizzle;
        [SerializeField] private AudioClip pull;
        [SerializeField] private AudioClip showerLoop;
        [SerializeField] private AudioClip stepClip;
        [SerializeField] private AudioClip dripClip;

        private Transform _cam, _chest, _handL, _handR, _hip;
        private float _t;
        private bool _inited;
        private Transform _rig, _head;
        private bool _rigDrive;
        private Transform[] _bTr;
        private byte[] _bFlags;
        private float[][] _bData;
        private int _bFrames;
        private float _bFps = 24f;
        private int _camIdx = -1;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int GltfColor = Shader.PropertyToID("baseColorFactor");
        private static readonly int GltfRough = Shader.PropertyToID("roughnessFactor");
        private static readonly int GltfMetal = Shader.PropertyToID("metallicFactor");
        private Transform _filmRoot2;
        private Transform[] _rigT;
        private Vector3[] _rigBase;
        private bool[] _rigBaked;
        private Transform[] _pairSrc, _pairDst;
        private Vector3[] _dstPos, _dstScl;
        private Quaternion[] _dstRot;
        private bool[] _dstBaked;
        private Quaternion[] _rigBaseRot;
        private Vector3[] _rigBaseScl;
        private Renderer[] _h0Renderers;
        private Color[] _wetBase;
        private float[] _wetRough;
        private bool[] _wetGltf;

        /// <summary>The film camera node (the view of the animator's video).</summary>
        public Transform CameraNode => _cam;

        public float FilmTime => _t;
        /// <summary>The worker chest in world space (the smoke test aims its close-up of the spill at it).</summary>
        public Vector3 ChestWorld => ChestPos();
        public int ShotCount => CutFrames.Length;
        public bool Baked => _bTr != null;

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            if (_inited || filmRoot == null)
                return;
            _inited = true;
            _mpb = new MaterialPropertyBlock();
            LoadBake();
            _cam = filmRoot.Find("Camera");
            foreach (var t in filmRoot.GetComponentsInChildren<Transform>(true))
            {
                // the worker is the first rig ("rig"); the other rigs carry the same bone names
                if (_chest == null && t.name == "spine_01.x" && t.IsChildOf(Rig("rig")))
                    _chest = t;
                if (_handL == null && t.name == "hand.l" && t.IsChildOf(Rig("rig")))
                    _handL = t;
                if (_handR == null && t.name == "hand.r" && t.IsChildOf(Rig("rig")))
                    _handR = t;
                if (_hip == null && t.name == "root.x" && t.IsChildOf(Rig("rig")))
                    _hip = t;
            }

            // the shower scene is shifted (see ShowerShift): the three men are moved after every pose
            _rigT = new[] { Rig("rig"), Rig("rig.001"), Rig("rig.002") };
            _rigBase = new Vector3[_rigT.Length];
            _rigBaked = new bool[_rigT.Length];
            for (var i = 0; i < _rigT.Length; i++)
            {
                _rigBase[i] = _rigT[i].localPosition;
                _rigBaked[i] = _bTr != null && System.Array.IndexOf(_bTr, _rigT[i]) >= 0;
            }

            // the dressed man (rig) and the shirtless man (rig.001) have the same skeleton: bone pairs by their path below the rig
            var r0 = _rigT[0];
            var r1 = _rigT[1];
            var srcs = new System.Collections.Generic.List<Transform>();
            var dsts = new System.Collections.Generic.List<Transform>();
            var rends = new System.Collections.Generic.List<Renderer>();
            if (r0 != null && r1 != null && r0 != filmRoot && r1 != filmRoot)
            {
                foreach (var t0 in r0.GetComponentsInChildren<Transform>(true))
                {
                    if (t0 == r0 || t0.GetComponent<Renderer>() != null)
                        continue;
                    var path = t0.name;
                    for (var q = t0.parent; q != null && q != r0; q = q.parent)
                        path = q.name + "/" + path;
                    var t1 = r1.Find(path);
                    if (t1 != null)
                    {
                        srcs.Add(t0);
                        dsts.Add(t1);
                    }
                }

                foreach (var sk in r0.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (!sk.name.StartsWith("cs_") && !sk.name.EndsWith("_Chem"))
                        rends.Add(sk);
            }

            _pairSrc = srcs.ToArray();
            _pairDst = dsts.ToArray();
            // what the shirtless man looked like before he was posed like the dressed one (bones the film does not animate are put back to this afterwards)
            _dstPos = new Vector3[_pairDst.Length];
            _dstRot = new Quaternion[_pairDst.Length];
            _dstScl = new Vector3[_pairDst.Length];
            _dstBaked = new bool[_pairDst.Length];
            for (var i = 0; i < _pairDst.Length; i++)
            {
                _dstPos[i] = _pairDst[i].localPosition;
                _dstRot[i] = _pairDst[i].localRotation;
                _dstScl[i] = _pairDst[i].localScale;
                _dstBaked[i] = _bTr != null && System.Array.IndexOf(_bTr, _pairDst[i]) >= 0;
            }

            _rigBaseRot = new Quaternion[_rigT.Length];
            _rigBaseScl = new Vector3[_rigT.Length];
            for (var i = 0; i < _rigT.Length; i++)
            {
                _rigBaseRot[i] = _rigT[i].localRotation;
                _rigBaseScl[i] = _rigT[i].localScale;
            }
            _h0Renderers = rends.ToArray();

            if (wetRenderers != null)
            {
                _wetBase = new Color[wetRenderers.Length];
                _wetRough = new float[wetRenderers.Length];
                _wetGltf = new bool[wetRenderers.Length];
                for (var i = 0; i < wetRenderers.Length; i++)
                {
                    var m = wetRenderers[i] != null ? wetRenderers[i].sharedMaterial : null;
                    _wetBase[i] = Color.white;
                    _wetRough[i] = 0.7f;
                    if (m == null)
                        continue;
                    // the animator's materials (glTF shader graph) and the swapped URP Lit ones name their properties differently
                    _wetGltf[i] = m.HasProperty(GltfColor);
                    if (_wetGltf[i])
                    {
                        _wetBase[i] = m.GetColor(GltfColor);
                        _wetRough[i] = m.HasProperty(GltfRough) ? m.GetFloat(GltfRough) : 0.7f;
                    }
                    else if (m.HasProperty(BaseColor))
                    {
                        _wetBase[i] = m.GetColor(BaseColor);
                        _wetRough[i] = 1f - (m.HasProperty(Smoothness) ? m.GetFloat(Smoothness) : 0.3f);
                    }
                }
            }

            ClearEffects();
        }

        private Transform Rig(string n)
        {
            foreach (var t in filmRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == n)
                    return t;
            return filmRoot;
        }

        /// <summary>From now on the rig follows the film camera every frame (until <see cref="EndRigDrive"/>).</summary>
        public void BeginRigDrive(Transform rig, Transform head)
        {
            _rig = rig;
            _head = head;
            _rigDrive = rig != null && head != null;
        }

        public void EndRigDrive()
        {
            _rigDrive = false;
        }

        private void LateUpdate()
        {
            if (_cam == null || !_rigDrive)
                return;
            // the glTF camera looks along the node's -Z; the roll is dropped (a rolled horizon makes people sick)
            var target = Quaternion.LookRotation(-_cam.forward, Vector3.up);
            _rig.rotation = target;
            _rig.position += _cam.position - _head.position; // the head ends exactly on the film camera; the tracking offset stays on top
        }

        private void LoadBake()
        {
            var ta = Resources.Load<TextAsset>("Sc20/Sc20FilmBake");
            if (ta == null || filmRoot == null)
                return;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
                {
                    if (r.ReadInt32() != 0x53433230)
                        return;
                    _bFrames = r.ReadInt32();
                    _bFps = r.ReadSingle();
                    var n = r.ReadInt32();
                    var tr = new Transform[n];
                    var fl = new byte[n];
                    var data = new float[n][];
                    for (var i = 0; i < n; i++)
                    {
                        var path = r.ReadString();
                        fl[i] = r.ReadByte();
                        var stride = ((fl[i] & 1) != 0 ? 3 : 0) + ((fl[i] & 2) != 0 ? 4 : 0) + ((fl[i] & 4) != 0 ? 3 : 0);
                        var d = new float[stride * _bFrames];
                        for (var k = 0; k < d.Length; k++)
                            d[k] = r.ReadSingle();
                        data[i] = d;
                        tr[i] = filmRoot.Find(path);
                        if (path == "Camera")
                            _camIdx = i;
                    }

                    _bTr = tr;
                    _bFlags = fl;
                    _bData = data;
                    Debug.Log("[Sc20] baked film loaded: " + n + " animated transforms, " + _bFrames + " frames");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Sc20] baked film unreadable: " + e.Message);
                _bTr = null;
            }
        }

        private static Vector3 Spline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Quaternion QLog(Quaternion q)
        {
            // log of a unit quaternion as a pure quaternion (x, y, z, 0)
            var v = new Vector3(q.x, q.y, q.z);
            var m = v.magnitude;
            if (m < 1e-6f)
                return new Quaternion(0f, 0f, 0f, 0f);
            var a = Mathf.Atan2(m, q.w) / m;
            return new Quaternion(v.x * a, v.y * a, v.z * a, 0f);
        }

        private static Quaternion QExp(Quaternion q)
        {
            var v = new Vector3(q.x, q.y, q.z);
            var m = v.magnitude;
            if (m < 1e-6f)
                return Quaternion.identity;
            var s = Mathf.Sin(m) / m;
            return new Quaternion(v.x * s, v.y * s, v.z * s, Mathf.Cos(m));
        }

        private static Quaternion QScale(Quaternion q, float f) => new Quaternion(q.x * f, q.y * f, q.z * f, 0f);

        private static Quaternion QAdd(Quaternion a, Quaternion b) => new Quaternion(a.x + b.x, a.y + b.y, a.z + b.z, 0f);

        private static Quaternion SquadPoint(Quaternion qm, Quaternion q, Quaternion qp)
        {
            var inv = Quaternion.Inverse(q);
            var l = QAdd(QLog(inv * qp), QLog(inv * qm));
            return q * QExp(QScale(l, -0.25f));
        }

        private static Quaternion Near(Quaternion q, Quaternion to) => Quaternion.Dot(q, to) < 0f ? new Quaternion(-q.x, -q.y, -q.z, -q.w) : q;

        /// <summary>Smooth squad interpolation between the baked 24 fps poses (linear steps showed as 24 Hz judder on a head-height camera).</summary>
        private static Quaternion Squad(Quaternion q0, Quaternion q1, Quaternion q2, Quaternion q3, float t)
        {
            q0 = Near(q0, q1);
            q2 = Near(q2, q1);
            q3 = Near(q3, q2);
            var s1 = SquadPoint(q0, q1, q2);
            var s2 = SquadPoint(q1, q2, q3);
            var a = Quaternion.Slerp(q1, q2, t);
            var b = Quaternion.Slerp(s1, s2, t);
            return Quaternion.Slerp(a, b, 2f * t * (1f - t));
        }

        private void ApplyBake(float t)
        {
            var f = Mathf.Clamp(t * _bFps, 0f, _bFrames - 1);
            var i1 = Mathf.Min((int)f, _bFrames - 1);
            var i2 = Mathf.Min(i1 + 1, _bFrames - 1);
            var i0 = Mathf.Max(i1 - 1, 0);
            var i3 = Mathf.Min(i2 + 1, _bFrames - 1);
            var a = f - i1;
            for (var i = 0; i < _bTr.Length; i++)
            {
                var tr = _bTr[i];
                if (tr == null)
                    continue;
                var fl = _bFlags[i];
                var d = _bData[i];
                var stride = ((fl & 1) != 0 ? 3 : 0) + ((fl & 2) != 0 ? 4 : 0) + ((fl & 4) != 0 ? 3 : 0);
                var o0 = i0 * stride;
                var o1 = i1 * stride;
                var o2 = i2 * stride;
                var o3 = i3 * stride;
                var k = 0;
                if ((fl & 1) != 0)
                {
                    var p0 = new Vector3(d[o0], d[o0 + 1], d[o0 + 2]);
                    var p1 = new Vector3(d[o1], d[o1 + 1], d[o1 + 2]);
                    var p2 = new Vector3(d[o2], d[o2 + 1], d[o2 + 2]);
                    var p3 = new Vector3(d[o3], d[o3 + 1], d[o3 + 2]);
                    // a jump (camera cut, a man put under the floor): never blend across it
                    if ((p2 - p1).sqrMagnitude > 4f)
                        tr.localPosition = a < 0.5f ? p1 : p2;
                    else if ((p1 - p0).sqrMagnitude > 4f || (p3 - p2).sqrMagnitude > 4f)
                        tr.localPosition = Vector3.Lerp(p1, p2, a);
                    else
                        tr.localPosition = Spline(p0, p1, p2, p3, a);
                    k = 3;
                }

                if ((fl & 2) != 0)
                {
                    var q0 = new Quaternion(d[o0 + k], d[o0 + k + 1], d[o0 + k + 2], d[o0 + k + 3]);
                    var q1 = new Quaternion(d[o1 + k], d[o1 + k + 1], d[o1 + k + 2], d[o1 + k + 3]);
                    var q2 = new Quaternion(d[o2 + k], d[o2 + k + 1], d[o2 + k + 2], d[o2 + k + 3]);
                    var q3 = new Quaternion(d[o3 + k], d[o3 + k + 1], d[o3 + k + 2], d[o3 + k + 3]);
                    // a hard cut of the camera rotation (> 30 degrees in one frame) is held, not interpolated
                    if (i == _camIdx && Quaternion.Angle(q1, q2) > 30f)
                        tr.localRotation = a < 0.5f ? q1 : q2;
                    else if (Quaternion.Angle(q1, q0) > 60f || Quaternion.Angle(q2, q3) > 60f || Quaternion.Angle(q1, q2) > 60f)
                        tr.localRotation = Quaternion.Slerp(q1, q2, a);
                    else
                        tr.localRotation = Squad(q0, q1, q2, q3, a);
                    k += 4;
                }

                if ((fl & 4) != 0)
                {
                    var s1 = new Vector3(d[o1 + k], d[o1 + k + 1], d[o1 + k + 2]);
                    var s2 = new Vector3(d[o2 + k], d[o2 + k + 1], d[o2 + k + 2]);
                    tr.localScale = Vector3.Lerp(s1, s2, a);
                }
            }
        }

        /// <summary>The shower scene is moved clear of the wall (ShowerShift): the three men, the shower cameras; the worker's run into it is blended.</summary>
        private void ApplyShift(float t)
        {
            if (_rigT == null)
                return;
            for (var i = 0; i < _rigT.Length; i++)
            {
                var r = _rigT[i];
                if (r == null)
                    continue;
                var w = i == 0 ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RunFrom, RunTo, t)) : 1f;
                if (!_rigBaked[i])
                {
                    r.localPosition = _rigBase[i];
                    r.localRotation = _rigBaseRot[i];
                    r.localScale = _rigBaseScl[i];
                }
                r.position += filmRoot.TransformVector(ShowerShift) * w;
            }

            if (_cam != null && t >= CutToShower)
                _cam.position += filmRoot.TransformVector(ShowerShift);

            if ((t < JacketOff || t >= ShirtlessAt) && _pairDst != null)
                for (var i = 0; i < _pairDst.Length; i++)
                {
                    if (_dstBaked[i])
                        continue;
                    _pairDst[i].localPosition = _dstPos[i];
                    _pairDst[i].localRotation = _dstRot[i];
                    _pairDst[i].localScale = _dstScl[i];
                }

            // from the jacket moment to the cut the shirtless man takes the dressed man's place and pose (so the jacket and then the cap can come off in view)
            if (t >= JacketOff && t < ShirtlessAt && _pairSrc != null && _pairSrc.Length > 0 && _rigT[0] != null && _rigT[1] != null)
            {
                for (var i = 0; i < _pairSrc.Length; i++)
                {
                    _pairDst[i].localPosition = _pairSrc[i].localPosition;
                    _pairDst[i].localRotation = _pairSrc[i].localRotation;
                    _pairDst[i].localScale = _pairSrc[i].localScale;
                }

                _rigT[1].SetPositionAndRotation(_rigT[0].position, _rigT[0].rotation);
                _rigT[1].localScale = _rigT[0].localScale;
            }
        }

        // ------------------------------------------------------------------ playback
        /// <summary>Poses the whole film at time t (seconds), including what is a function of time only (stains, wet look, the taken-off vest).</summary>
        public void ApplyTime(float t)
        {
            _t = Mathf.Clamp(t, 0f, Duration);
            if (_bTr != null)
                ApplyBake(_t);
            else if (clips != null && filmRoot != null)
                foreach (var c in clips)
                    if (c != null)
                        c.SampleAnimation(filmRoot.gameObject, Mathf.Min(_t, c.length));
            ApplyShift(_t);
            ApplyLook(_t);
        }

        private void ApplyLook(float t)
        {
            if (_h0Renderers != null)
            {
                foreach (var r in _h0Renderers)
                {
                    if (r == null)
                        continue;
                    bool on;
                    if (t >= ShirtlessAt)
                        on = false;
                    else if (t >= JacketOff)
                        on = r.name.StartsWith("KSTM"); // only the gloves stay on the raised hands, the rest (jacket, body suit, trousers, head) is the dressed man
                    else if (r.name.StartsWith("Helmet"))
                        on = t < CapOff;
                    else if (r == vestRenderer)
                        on = t < VestOff;
                    else
                        on = true;
                    if (r.enabled != on)
                        r.enabled = on;
                }
            }
            else if (vestRenderer != null)
            {
                vestRenderer.enabled = t < VestOff;
            }

            if (clothesPile != null)
            {
                if (clothesPile.Length > 0 && clothesPile[0] != null)
                    clothesPile[0].SetActive(t >= VestOff);
                if (clothesPile.Length > 1 && clothesPile[1] != null)
                    clothesPile[1].SetActive(t >= JacketOff);
            }

            if (puddle != null)
            {
                var pt = Mathf.Clamp01((t - TripAt - 0.15f) / 2.2f);
                var on = pt > 0f;
                if (puddle.activeSelf != on)
                    puddle.SetActive(on);
                if (on)
                    puddle.transform.localScale = Vector3.one * (0.35f + 1.15f * pt);
            }

            if (showerPuddle != null)
            {
                var sp = Mathf.Clamp01((t - WaterAt) / 3.5f);
                var on = sp > 0f;
                if (showerPuddle.activeSelf != on)
                    showerPuddle.SetActive(on);
                if (on)
                    showerPuddle.transform.localScale = Vector3.one * (0.45f + 0.75f * sp);
            }

            // the chemical: soaks in over 1.5 s after the trip, is rinsed away while the shower runs (water at 19.9 s, mostly gone by 24 s)
            var stain = Mathf.Clamp01((t - StainFrom) / (StainFull - StainFrom));
            if (t >= WaterAt)
                stain *= 1f - Mathf.Clamp01((t - WaterAt - 0.6f) / 3.6f);
            if (stainRenderers != null)
                foreach (var r in stainRenderers)
                {
                    if (r == null)
                        continue;
                    var show = stain > 0.01f && t < JacketOff && !(r == vestStainRenderer && t >= VestOff);
                    if (r.enabled != show)
                        r.enabled = show;
                    if (!show)
                        continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColor, new Color(1f, 1f, 1f, stain * 0.9f));
                    r.SetPropertyBlock(_mpb);
                }

            // the wet look: darker, glossy cloth once the chemical or the water is on it
            var wet = Mathf.Max(Mathf.Clamp01((t - StainFrom) / 1.0f) * 0.85f, Mathf.Clamp01((t - WaterAt) / 1.6f));
            if (wetRenderers != null)
                for (var i = 0; i < wetRenderers.Length; i++)
                {
                    var r = wetRenderers[i];
                    if (r == null)
                        continue;
                    r.GetPropertyBlock(_mpb);
                    var b = _wetBase != null && i < _wetBase.Length ? _wetBase[i] : Color.white;
                    var dark = Color.Lerp(b, new Color(b.r * 0.58f, b.g * 0.58f, b.b * 0.58f, b.a), wet);
                    var rough = Mathf.Lerp(_wetRough != null && i < _wetRough.Length ? _wetRough[i] : 0.7f, 0.62f, wet);
                    if (_wetGltf != null && i < _wetGltf.Length && _wetGltf[i])
                    {
                        _mpb.SetColor(GltfColor, dark);
                        _mpb.SetFloat(GltfRough, rough);
                        _mpb.SetFloat(GltfMetal, 0f);
                    }
                    else
                    {
                        _mpb.SetColor(BaseColor, dark);
                        _mpb.SetFloat(Smoothness, 1f - rough);
                    }

                    r.SetPropertyBlock(_mpb);
                }
        }

        private void ClearEffects()
        {
            foreach (var ps in new[] { splash, drips, fumes, shower, showerMist, stream })
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            Init();
            ClearEffects();
            if (loopSource != null)
            {
                loopSource.Stop();
                loopSource.volume = 0.5f;
            }

            if (fxSource != null)
                fxSource.Stop();
            ApplyTime(0f);
        }

        /// <summary>Head position and flat view direction of the film camera shortly after a cut (for the rig placement before / after the film).</summary>
        public void ShotPose(int shot, out Vector3 head, out Vector3 forward)
        {
            Init();
            shot = Mathf.Clamp(shot, 0, CutFrames.Length - 1);
            var t = CutFrames[shot] / Fps + 0.25f;
            var keep = _t;
            ApplyTime(t);
            head = _cam != null ? _cam.position : filmRoot.position + Vector3.up * 2f;
            var f = _cam != null ? -_cam.forward : Vector3.forward;
            f.y = 0f;
            forward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            ApplyTime(keep);
        }

        private void Sfx(AudioClip c, Vector3 at, float vol = 1f)
        {
            if (c == null || fxSource == null)
                return;
            fxSource.transform.position = at;
            fxSource.PlayOneShot(c, vol);
        }

        private Vector3 ChestPos() => _chest != null ? _chest.position : filmRoot.position + Vector3.up;

        private void PlaceAt(ParticleSystem ps, Transform at, Vector3 offsetWorld)
        {
            if (ps == null || at == null)
                return;
            ps.transform.position = at.position + offsetWorld;
        }

        /// <summary>The whole film, from 0 to 32.5 s, with the camera cuts, the chemical, the water, the voice-over and the sounds at their times.</summary>
        public IEnumerator Play(Hooks h)
        {
            ResetAll();
            var shot = 0;
            var cues = new (float t, string id)[]
            {
                (0.5f, "n1"), (11.0f, "slosh"), (TripAt - 0.15f, "stream"), (TripAt - 0.15f + StreamFlight, "splash"), (TripAt + 0.05f, "n2"), (11.9f, "sizzle"),
                (15.2f, "n3"), (HandleAt, "pull"), (WaterAt, "water"), (WaterAt + 0.05f, "n4"), (21.0f, "n5"), (25.3f, "n6"), (WaterOffAt, "waterOff"),
                (28.3f, "n7")
            };
            var cue = 0;
            var t = 0f;
            var chest0 = ChestPos();
            var maxMoved = 0f;
            var nextDrip = 0f;
            while (t < Duration)
            {
                ApplyTime(t);
                while (shot + 1 < CutFrames.Length && t >= CutFrames[shot + 1] / Fps)
                {
                    shot++;
                    h?.Cut?.Invoke(shot);
                }

                while (cue < cues.Length && t >= cues[cue].t)
                {
                    Cue(cues[cue].id, h);
                    cue++;
                }

                // the drips and the vapour follow the soaked worker until the water takes over
                if (t > TripAt + 0.5f && t < WaterAt + 2.5f)
                {
                    PlaceAt(drips, _chest, Vector3.zero);
                    PlaceAt(fumes, _chest, Vector3.up * 0.1f);
                    if (t >= nextDrip && t < WaterAt)
                    {
                        Sfx(dripClip, ChestPos() + Vector3.down * 0.8f, 0.25f);
                        nextDrip = t + UnityEngine.Random.Range(0.9f, 1.8f);
                    }
                }

                maxMoved = Mathf.Max(maxMoved, (ChestPos() - chest0).magnitude);
                yield return null;
                t += Mathf.Min(SessionClock.GameDelta, 0.1f);
            }

            ApplyTime(Duration);
            Debug.Log("[Sc20] film done: worker chest moved " + maxMoved.ToString("F2") + " units, baked=" + (_bTr != null));
            if (loopSource != null)
                loopSource.Stop();
            ClearEffects();
        }

        private IEnumerator FadeOutLoop(float seconds)
        {
            if (loopSource == null)
                yield break;
            var v0 = loopSource.volume;
            var e = 0f;
            while (e < seconds)
            {
                e += SessionClock.GameDelta;
                loopSource.volume = Mathf.Lerp(v0, 0f, e / seconds);
                yield return null;
            }

            loopSource.Stop();
            loopSource.volume = v0;
        }

        private void Cue(string id, Hooks h)
        {
            switch (id)
            {
                case "slosh": Sfx(slosh, ChestPos(), 0.9f); break;
                case "stream":
                    if (stream != null)
                    {
                        // out of the drum mouth, aimed at the worker's chest
                        // the drum mouth = the upper edge of the drum on the worker side (the drum stands beside him, a little tilted on the trolley)
                        var chest = ChestPos();
                        Vector3 mouth;
                        if (drumRenderer != null)
                        {
                            var db = drumRenderer.bounds;
                            var side = chest - db.center;
                            side.y = 0f;
                            side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.forward;
                            mouth = db.center + Vector3.up * (db.extents.y * 0.85f) + side * (Mathf.Min(db.extents.x, db.extents.z) * 0.7f);
                        }
                        else
                        {
                            mouth = chest + Vector3.up * 0.2f;
                        }

                        // aim at the chest, a little above, with the drop of the jet over its flight time added, so the drops end ON the worker
                        var aim = chest + Vector3.up * 0.06f;
                        var drop = 0.5f * 9.81f * StreamGravity * StreamFlight * StreamFlight;
                        var dir = aim + Vector3.up * drop - mouth;
                        if (dir.sqrMagnitude < 1e-4f)
                            dir = Vector3.forward;
                        var sm = stream.main;
                        sm.startSpeed = dir.magnitude / StreamFlight;
                        sm.startLifetime = StreamFlight;
                        stream.transform.position = mouth;
                        stream.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        stream.Play();
                    }

                    break;
                case "splash":
                    Sfx(splashClip, ChestPos(), 1f);
                    if (splash != null)
                    {
                        // burst from the drum mouth towards the worker's chest and arms
                        splash.transform.position = ChestPos() + Vector3.up * 0.05f;
                        splash.Play();
                    }

                    if (drips != null)
                        drips.Play();
                    if (fumes != null)
                        fumes.Play();
                    break;
                case "sizzle": Sfx(sizzle, ChestPos(), 0.55f); break;
                case "waterOff":
                    // the shower is closed: the drops already in the air fall, the loop fades out
                    if (shower != null)
                        shower.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    if (showerMist != null)
                        showerMist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    StartCoroutine(FadeOutLoop(1.2f));
                    break;
                case "pull": Sfx(pull, showerHead != null ? showerHead.position : filmRoot.position, 1f); break;
                case "water":
                    if (drips != null)
                        drips.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    if (fumes != null)
                        fumes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    if (shower != null)
                        shower.Play();
                    if (showerMist != null)
                        showerMist.Play();
                    if (loopSource != null && showerLoop != null)
                    {
                        loopSource.transform.position = showerHead != null ? showerHead.position : filmRoot.position;
                        loopSource.clip = showerLoop;
                        loopSource.loop = true;
                        loopSource.Play();
                    }

                    break;
                default:
                    h?.SayAsync?.Invoke(id);
                    break;
            }
        }
    }
}

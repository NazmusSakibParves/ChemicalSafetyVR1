using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The film of Scenario 21, as the client's animator made it (Sc_21_v4.blend: 680 frames at 24 fps = 28.3 s): a supervisor orders a chemical coating
    /// job in a poorly ventilated room, there is no JHA, the worker refuses, the two prepare the JHA together, the worker puts on a respirator and starts.
    /// Blender exported the finished timeline to Sc22Film.glb; Sc22FilmBake.bytes (menu Chemical Safety/Presentable/Bake Sc22 Film) holds the pose of
    /// every animated transform per frame and this class applies it by code (glb clips move nothing on the Quest). The learner's OWN camera follows the
    /// film camera (as in Scenarios 19 and 20); the controllers cannot move him. Cuts (0-based frames): 228, 267, 349, 373, 505, 590.
    /// </summary>
    public sealed class Sc22Cinematic : MonoBehaviour
    {
        public sealed class Hooks
        {
            /// <summary>A camera cut of the animator's film (shot index): the director softens it with a short fade.</summary>
            public Action<int> Cut;
            /// <summary>Voice-over line n1..n6; does not wait.</summary>
            public Action<string> SayAsync;
            /// <summary>The film pauses (the poses hold, the time stands) while this coroutine runs, e.g. the caution popup with its voice.</summary>
            public Func<string, IEnumerator> Pause;
        }

        public const float Duration = 12.08f;
        private const float Fps = 24f;
        private static readonly int[] CutFrames = { 0 };
        private static readonly (float t, string id)[] Cues = new (float t, string id)[0];
        /// <summary>Film time (s) of the pauses: the supervisor reaches the bottle and stops the worker (the caution popup).</summary>
        private static readonly (float t, string id)[] PauseCues = { (2.3f, "caution") };

        [SerializeField] private Transform filmRoot;
        [SerializeField] private AudioSource fxSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private Sc22Pour pour;   // the acetone: stream from the tap, drops, liquid level

        private Transform _cam;
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

        /// <summary>The film camera node (the view of the animator's video).</summary>
        public Transform CameraNode => _cam;
        public float FilmTime => _t;
        public int ShotCount => CutFrames.Length;
        public bool Baked => _bTr != null;
        public Sc22Pour Pour => pour;

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            if (_inited || filmRoot == null)
                return;
            _inited = true;
            LoadBake();
            _cam = filmRoot.Find("Camera");
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
            var ta = Resources.Load<TextAsset>("Sc22/Sc22FilmBake");
            if (ta == null || filmRoot == null)
                return;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
                {
                    if (r.ReadInt32() != 0x53433232)
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
                    Debug.Log("[Sc22] baked film loaded: " + n + " animated transforms, " + _bFrames + " frames");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Sc22] baked film unreadable: " + e.Message);
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

        // ------------------------------------------------------------------ playback
        /// <summary>Poses the whole film at time t (seconds).</summary>
        public void ApplyTime(float t)
        {
            _t = Mathf.Clamp(t, 0f, Duration);
            if (_bTr != null)
                ApplyBake(_t);
            if (pour != null)
                pour.Apply(_t);
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            Init();
            if (fxSource != null)
                fxSource.Stop();
            if (loopSource != null)
                loopSource.Stop();
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

        /// <summary>The whole film, from 0 to 12.1 s, with the camera cuts and the voice-over at their times.</summary>
        public IEnumerator Play(Hooks h)
        {
            ResetAll();
            var shot = 0;
            var cue = 0;
            var pauseCue = 0;
            var t = 0f;
            while (t < Duration)
            {
                ApplyTime(t);
                while (shot + 1 < CutFrames.Length && t >= CutFrames[shot + 1] / Fps)
                {
                    shot++;
                    h?.Cut?.Invoke(shot);
                }

                while (cue < Cues.Length && t >= Cues[cue].t)
                {
                    h?.SayAsync?.Invoke(Cues[cue].id);
                    cue++;
                }

                while (pauseCue < PauseCues.Length && t >= PauseCues[pauseCue].t)
                {
                    var id = PauseCues[pauseCue].id;
                    pauseCue++;
                    if (h != null && h.Pause != null)
                    {
                        if (pour != null)
                            pour.SetFrozen(true);
                        yield return h.Pause(id);   // the film stands still: ApplyTime is not called, the poses hold
                        if (pour != null)
                            pour.SetFrozen(false);
                    }
                }

                yield return null;
                t += Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.1f);
            }

            ApplyTime(Duration);
            Debug.Log("[Sc22] film done, baked=" + (_bTr != null));
        }
    }
}

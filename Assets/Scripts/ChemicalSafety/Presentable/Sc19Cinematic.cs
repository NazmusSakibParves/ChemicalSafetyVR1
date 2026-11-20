using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The film of Scenario 19, exactly as the client's animator made it (Tank pipe24.blend: 930 frames at 24 fps = 38.75 s). Blender exported the
    /// finished timeline to one glb (Sc19Film.glb): the tank scene, four characters, the props, and 17 clips that all run the whole 38.75 s,
    /// including the film camera. This class plays those clips (sampled by time, so Back / replay and pausing are exact) and adds only what the
    /// glb cannot hold: the voice-over, the cough / gasp / fall sounds, the toxic haze inside the tank, the blower hum.
    /// The learner sees the film the way the animator framed it, with the learner's OWN camera (no second camera): every frame the XR rig is
    /// moved and turned so that its origin sits exactly on the film camera node (position and rotation, the animator's dolly and pan, all 11 cuts,
    /// each cut softened by a short fade); the headset tracking stays on top of that, so the learner can still look around. The learner is
    /// carried by the rig, not by walking: the CharacterController and the locomotion are off during the film.
    /// (The film camera has no lens zoom: the lens is 35 mm in every frame, "zoom" is the camera moving towards / away.)
    /// Switch: <c>useScreen</c> (set by the builder, <c>Sc19FilmOnScreen</c>) shows the film on a big screen with a second camera instead.
    /// Cuts (Blender frames): 1, 181, 321, 391, 431, 529, 559, 626, 673, 732, 818.
    /// </summary>
    public sealed class Sc19Cinematic : MonoBehaviour
    {
        public sealed class Hooks
        {
            /// <summary>A camera cut of the animator's film (shot index): the director softens it with a short fade.</summary>
            public Action<int> Cut;
            /// <summary>Caption + voice for a key (w1..w3, r1..r6); does not wait.</summary>
            public Action<string> SayAsync;
        }

        public const float Duration = 38.75f;
        private const float Fps = 24f;
        private static readonly int[] CutFrames = { 1, 181, 321, 391, 431, 529, 559, 626, 673, 732, 818 };

        [SerializeField] private Transform filmRoot;
        [SerializeField] private AnimationClip[] clips;
        [SerializeField] private ParticleSystem[] smoke;
        [SerializeField] private bool useScreen;
        /// <summary>true (default) = the glb clips are played through PlayableGraphs (works in a player build, where AnimationClip.SampleAnimation on
        /// non-legacy clips moves nothing on the Quest); false = the old SampleAnimation path (editor only).</summary>
        [SerializeField] private bool usePlayables = true;
        [SerializeField] private Camera filmCam;
        [SerializeField] private Transform screen;      // the quad that shows the film camera
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private AudioSource fxSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioClip[] coughs;
        [SerializeField] private AudioClip gasp;
        [SerializeField] private AudioClip thud;
        [SerializeField] private AudioClip blowerLoop;
        [SerializeField] private AudioClip beep;
        [SerializeField] private AudioClip clank;
        [SerializeField] private AudioClip pen;

        private Transform _cam, _entrantHead, _blower;
        private AnimationClip _camClip;
        private float _t;
        private bool _inited;
        private RenderTexture _rt;
        private bool _screenOn;
        private Transform _rig, _head;
        private bool _rigDrive;
        // baked film (Sc19FilmBake.bytes): the motion of every animated transform, applied by code (the Animation system moves nothing in the Quest build)
        private Transform[] _bTr;
        private byte[] _bFlags;
        private float[][] _bData;
        private int _bFrames;
        private float _bFps = 24f;
        private int _camIdx = -1;
        private PlayableGraph[] _graphs;
        private AnimationClipPlayable[] _playables;

        /// <summary>The film camera node (the view of the animator's video).</summary>
        public Transform CameraNode => _cam;

        public float FilmTime => _t;
        public int ShotCount => CutFrames.Length;
        public Transform Screen => screen;

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
            if (_bTr == null && usePlayables)
                BuildGraphs();
            _cam = filmRoot.Find("Camera");
            var rig2 = FindRig("rig.002");
            foreach (var t in filmRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cylinder" && _blower == null)
                    _blower = t;
                if (t.name == "head.x" && _entrantHead == null && t.IsChildOf(rig2))
                    _entrantHead = t;
            }

            foreach (var c in clips)
                if (c != null && c.name == "Camera")
                    _camClip = c;
            if (filmCam != null && screenRenderer != null)
            {
                _rt = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32) { name = "Sc19FilmRT", antiAliasing = 1 };
                filmCam.targetTexture = _rt;
                filmCam.enabled = false;
                screenRenderer.material.SetTexture("_BaseMap", _rt);
                screenRenderer.material.SetTexture("_MainTex", _rt);
            }

            SetScreen(false);
        }

        private void OnDestroy()
        {
            DestroyGraphs();
            if (_rt != null)
                _rt.Release();
        }

        /// <summary>Shows / hides the film screen (and its camera: it is only rendered while the screen is visible).</summary>
        public void SetScreen(bool on)
        {
            _screenOn = on;
            if (screen != null)
                screen.gameObject.SetActive(on);
            if (filmCam != null)
                filmCam.enabled = on;
        }

        public void PlaceScreen(Vector3 pos, Quaternion rot)
        {
            if (screen != null)
                screen.SetPositionAndRotation(pos, rot);
        }

        public bool UseScreen => useScreen;

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
            if (_cam == null)
                return;
            // the glTF camera looks along the node's -Z; the roll is dropped (a rolled horizon makes people sick), the film's own roll is ~0
            var target = Quaternion.LookRotation(-_cam.forward, Vector3.up);
            if (_screenOn && filmCam != null)
                filmCam.transform.SetPositionAndRotation(_cam.position, target);
            if (_rigDrive)
            {
                _rig.rotation = target;
                _rig.position += _cam.position - _head.position; // the head ends exactly on the film camera; the tracking offset stays on top
            }
        }

        private void LoadBake()
        {
            var ta = Resources.Load<TextAsset>("Sc19/Sc19FilmBake");
            if (ta == null || filmRoot == null)
                return;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
                {
                    if (r.ReadInt32() != 0x53433139)
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
                    Debug.Log("[Sc19] baked film loaded: " + n + " animated transforms, " + _bFrames + " frames");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Sc19] baked film unreadable: " + e.Message);
                _bTr = null;
            }
        }

        private void ApplyBake(float t)
        {
            var f = Mathf.Clamp(t * _bFps, 0f, _bFrames - 1);
            var i0 = Mathf.Min((int)f, _bFrames - 1);
            var i1 = Mathf.Min(i0 + 1, _bFrames - 1);
            var a = f - i0;
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
                var aa = a;
                if (i == _camIdx && (fl & 1) != 0)
                {
                    // a camera cut is a jump between two frames: never blend across it
                    var dx = d[o1] - d[o0];
                    var dy = d[o1 + 1] - d[o0 + 1];
                    var dz = d[o1 + 2] - d[o0 + 2];
                    if (dx * dx + dy * dy + dz * dz > 4f)
                        aa = a < 0.5f ? 0f : 1f;
                }

                var k = 0;
                if ((fl & 1) != 0)
                {
                    tr.localPosition = new Vector3(Mathf.Lerp(d[o0], d[o1], aa), Mathf.Lerp(d[o0 + 1], d[o1 + 1], aa), Mathf.Lerp(d[o0 + 2], d[o1 + 2], aa));
                    k = 3;
                }

                if ((fl & 2) != 0)
                {
                    var q0 = new Quaternion(d[o0 + k], d[o0 + k + 1], d[o0 + k + 2], d[o0 + k + 3]);
                    var q1 = new Quaternion(d[o1 + k], d[o1 + k + 1], d[o1 + k + 2], d[o1 + k + 3]);
                    tr.localRotation = Quaternion.Slerp(q0, q1, aa);
                    k += 4;
                }

                if ((fl & 4) != 0)
                    tr.localScale = new Vector3(Mathf.Lerp(d[o0 + k], d[o1 + k], aa), Mathf.Lerp(d[o0 + k + 1], d[o1 + k + 1], aa), Mathf.Lerp(d[o0 + k + 2], d[o1 + k + 2], aa));
            }
        }

        /// <summary>One manual PlayableGraph per clip on a controller-less Animator of the film root: each graph writes only the transforms its clip animates.</summary>
        private void BuildGraphs()
        {
            if (clips == null || filmRoot == null)
                return;
            var an = filmRoot.GetComponent<Animator>();
            if (an == null)
                an = filmRoot.gameObject.AddComponent<Animator>();
            an.runtimeAnimatorController = null;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graphs = new PlayableGraph[clips.Length];
            _playables = new AnimationClipPlayable[clips.Length];
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;
                var g = PlayableGraph.Create("Sc19Clip" + i);
                g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var cp = AnimationClipPlayable.Create(g, clips[i]);
                cp.SetApplyFootIK(false);
                cp.SetApplyPlayableIK(false);
                var o = AnimationPlayableOutput.Create(g, "o" + i, an);
                o.SetSourcePlayable(cp);
                g.Play();
                cp.Pause();
                _graphs[i] = g;
                _playables[i] = cp;
            }
        }

        private void DestroyGraphs()
        {
            if (_graphs == null)
                return;
            foreach (var g in _graphs)
                if (g.IsValid())
                    g.Destroy();
            _graphs = null;
        }

        private Transform FindRig(string n)
        {
            foreach (var t in filmRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == n)
                    return t;
            return filmRoot;
        }

        // ------------------------------------------------------------------ playback
        /// <summary>Poses the whole film at time t (seconds).</summary>
        public void ApplyTime(float t)
        {
            _t = Mathf.Clamp(t, 0f, Duration);
            if (_bTr != null)
            {
                ApplyBake(_t);
                return;
            }

            if (clips == null || filmRoot == null)
                return;
            if (usePlayables && _graphs != null)
            {
                for (var i = 0; i < _graphs.Length; i++)
                {
                    if (!_graphs[i].IsValid() || clips[i] == null)
                        continue;
                    _playables[i].SetTime(Mathf.Min(_t, clips[i].length));
                    _graphs[i].Evaluate(0f);
                }

                return;
            }

            var go = filmRoot.gameObject;
            foreach (var c in clips)
                if (c != null)
                    c.SampleAnimation(go, Mathf.Min(_t, c.length));
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            Init();
            SmokeOn(false, true);
            if (loopSource != null)
                loopSource.Stop();
            if (fxSource != null)
                fxSource.Stop();
            ApplyTime(0f);
        }

        /// <summary>Head position and flat view direction of the film camera shortly after a cut.</summary>
        public void ShotPose(int shot, out Vector3 head, out Vector3 forward)
        {
            Init();
            shot = Mathf.Clamp(shot, 0, CutFrames.Length - 1);
            var t = (CutFrames[shot] - 1) / Fps + 0.25f;
            var keep = _t;
            ApplyTime(t);
            head = _cam != null ? _cam.position : filmRoot.position + Vector3.up * 2f;
            // the glTF camera looks along the node's -Z
            var f = _cam != null ? -_cam.forward : Vector3.forward;
            f.y = 0f;
            forward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            ApplyTime(keep);
        }

        private void SmokeOn(bool on, bool clear = false)
        {
            if (smoke == null)
                return;
            foreach (var ps in smoke)
            {
                if (ps == null)
                    continue;
                if (on)
                    ps.Play();
                else
                    ps.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Sfx(AudioClip c, Vector3 at, float vol = 1f)
        {
            if (c == null || fxSource == null)
                return;
            fxSource.transform.position = at;
            fxSource.PlayOneShot(c, vol);
        }

        private Vector3 HeadPos() => _entrantHead != null ? _entrantHead.position : filmRoot.position + Vector3.up;

        /// <summary>The whole film, from 0 to 38.75 s, with the camera cuts, voice-over, sounds and haze at their times.</summary>
        public IEnumerator Play(Hooks h)
        {
            ResetAll();
            SetScreen(useScreen);
            var shot = -1;
            var coughN = 0;
            var cues = new (float t, string id)[]
            {
                // client 2026-11-20: w1+w2 and r2+r3 are recorded as ONE clip each (keys w2 and r3), so the first cue of each pair plays it
                (0.2f, "w2"), (5.6f, "smokeOn"), (7.8f, "w3"), (9.8f, "cough"), (10.7f, "cough"), (11.15f, "gasp"),
                (11.55f, "thud"), (13.3f, "smokeOff"), (13.4f, "r1"), (13.4f, "hum"), (17.0f, "r3"), (17.2f, "beep"), (21.8f, "r4"),
                (22.1f, "pen"), (25.0f, "r5"), (28.3f, "r6"), (28.6f, "clank")
            };
            var cue = 0;
            var t = 0f;
            var head0 = HeadPos();
            while (t < Duration)
            {
                ApplyTime(t);
                while (shot + 1 < CutFrames.Length && t >= (CutFrames[shot + 1] - 1) / Fps)
                {
                    shot++;
                    h?.Cut?.Invoke(shot);
                }

                while (cue < cues.Length && t >= cues[cue].t)
                {
                    Cue(cues[cue].id, h, ref coughN);
                    cue++;
                }

                yield return null;
                t += Mathf.Min(SessionClock.GameDelta, 0.1f);
            }

            ApplyTime(Duration);
            Debug.Log("[Sc19] film done: entrant head moved " + (HeadPos() - head0).magnitude.ToString("F2") + " units, baked=" + (_bTr != null) + " playables=" + (usePlayables && _graphs != null));
            if (loopSource != null)
                loopSource.Stop();
        }

        private void Cue(string id, Hooks h, ref int coughN)
        {
            switch (id)
            {
                case "smokeOn": SmokeOn(true); break;
                case "smokeOff": SmokeOn(false); break;
                case "cough":
                    if (coughs != null && coughs.Length > 0)
                        Sfx(coughs[coughN % coughs.Length], HeadPos(), 1f);
                    coughN++;
                    break;
                case "gasp": Sfx(gasp, HeadPos(), 1f); break;
                case "thud": Sfx(thud, HeadPos(), 1f); break;
                case "beep": Sfx(beep, HeadPos(), 0.8f); break;
                case "pen": Sfx(pen, filmRoot.position + Vector3.up * 3f, 0.8f); break;
                case "clank": Sfx(clank, filmRoot.position + Vector3.up * 3f, 0.9f); break;
                case "hum":
                    if (loopSource != null && blowerLoop != null)
                    {
                        loopSource.transform.position = _blower != null ? _blower.position : filmRoot.position;
                        loopSource.clip = blowerLoop;
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

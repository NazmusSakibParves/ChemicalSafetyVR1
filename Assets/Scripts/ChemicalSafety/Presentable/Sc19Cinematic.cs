using System;
using System.Collections;
using UnityEngine;

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
            if (clips == null || filmRoot == null)
                return;
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
            if (_camClip != null && filmRoot != null)
                _camClip.SampleAnimation(filmRoot.gameObject, t);
            head = _cam != null ? _cam.position : filmRoot.position + Vector3.up * 2f;
            // the glTF camera looks along the node's -Z
            var f = _cam != null ? -_cam.forward : Vector3.forward;
            f.y = 0f;
            forward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            if (_camClip != null && filmRoot != null)
                _camClip.SampleAnimation(filmRoot.gameObject, Mathf.Min(_t, _camClip.length));
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
                (0.2f, "w1"), (3.9f, "w2"), (5.6f, "smokeOn"), (7.8f, "w3"), (9.8f, "cough"), (10.7f, "cough"), (11.15f, "gasp"),
                (11.55f, "thud"), (13.3f, "smokeOff"), (13.4f, "r1"), (13.4f, "hum"), (17.0f, "r2"), (17.2f, "beep"), (19.2f, "r3"), (21.8f, "r4"),
                (22.1f, "pen"), (25.0f, "r5"), (28.3f, "r6"), (28.6f, "clank")
            };
            var cue = 0;
            var t = 0f;
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
                t += Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.1f);
            }

            ApplyTime(Duration);
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

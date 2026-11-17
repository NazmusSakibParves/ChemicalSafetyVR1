using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// The acetone of Scenario 22 (client, 2026-11-17: "the liquid must come out of the drum tap and fill the empty bottle, then the can"). The film is the animator's
    /// and has no liquid, so this is driven from the film time (<see cref="Apply"/>, called by <see cref="Sc22Cinematic.ApplyTime"/>; a pure function of the time, so a
    /// replay or a jump always looks right): a clear stream from the tap tip to the mouth of the container that is under the tap, splash drops at the mouth, and the
    /// liquid body inside the container (a stretched cylinder in the bottle, a stretched box in the can) that rises while it is filled. Real acetone is colourless and
    /// clear like water: it is seen through the glass by its meniscus, highlights, ripples and drops, not by a colour.
    /// Film times (s): bottle under the tap 1.2 - 2.7 (the worker is stopped there), can under the tap 9.8 - 12.08.
    /// </summary>
    public sealed class Sc22Pour : MonoBehaviour
    {
        [SerializeField] private Transform tapTip;          // where the water leaves the drum tap (a child of the drum)
        [SerializeField] private Transform bottleMouth;     // child of the animator's bottle (Bottole), at its neck opening
        [SerializeField] private Transform bottleLiquid;    // liquid body inside Bottole (local +Z of the node = towards the bottle bottom)
        [SerializeField] private Transform bottle2Liquid;   // the same liquid in the second bottle node (Bottole.001: the bottle that is carried to the bin)
        [SerializeField] private Transform canMouth;        // child of Can.001 at its spout
        [SerializeField] private Transform canLiquid;       // liquid body inside the can
        [SerializeField] private Transform bottleCap;       // blue cap of Bottole: off while it is filled, back on when the supervisor takes it
        [SerializeField] private Transform canCap;          // blue cap of Can.001: unscrewed and lifted away just before the pour
        [SerializeField] private Transform stream;          // unit tube along +Z
        [SerializeField] private ParticleSystem splash;     // drops at the mouth of the container

        public const float BottleFrom = 1.2f, BottleTo = 2.7f, CanFrom = 9.8f, CanTo = 12.08f;
        private const float BottleMaxH = 3.1f;   // liquid height in the bottle node's local units (the body is 4 units of cylinder + shoulder)
        private const float CanMaxH = 0.56f;     // the can body is 0.94 units tall

        private bool _frozen;
        private bool _streamOn;
        private bool _capBase;
        private Vector3 _canCapPos;
        private Vector3 _canCapScale;

        /// <summary>Level of the bottle (0..1 of its end level) and of the can, for the smoke test.</summary>
        public float BottleLevel { get; private set; }
        public float CanLevel { get; private set; }
        public bool StreamOn => _streamOn;

        /// <summary>The film is paused (caution popup): the drops stop, the stream stays frozen where it is.</summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            if (frozen)
                SetSplash(false, transform.position);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static void SetLiquid(Transform tr, float h)
        {
            if (tr == null)
                return;
            var on = h > 0.01f;
            if (tr.gameObject.activeSelf != on)
                tr.gameObject.SetActive(on);
            if (on)
            {
                var s = tr.localScale;
                tr.localScale = new Vector3(s.x, s.y, h);
            }
        }

        private void SetSplash(bool on, Vector3 at)
        {
            if (splash == null)
                return;
            splash.transform.position = at;
            var em = splash.emission;
            em.rateOverTime = on ? 85f : 0f;
            if (on && !splash.isPlaying)
                splash.Play();
        }

        /// <summary>Poses the liquid at film time t (seconds).</summary>
        public void Apply(float t)
        {
            var pb = Smooth(Mathf.InverseLerp(BottleFrom, BottleTo, t));
            var pc = Smooth(Mathf.InverseLerp(CanFrom, CanTo, t));
            if (bottleCap != null)
            {
                var on = t >= 2.95f;   // the filled bottle is closed again when the supervisor takes it (Bottole.001 keeps its cap on)
                if (bottleCap.gameObject.activeSelf != on)
                    bottleCap.gameObject.SetActive(on);
            }

            if (canCap != null)
            {
                if (!_capBase)
                {
                    _capBase = true;
                    _canCapPos = canCap.localPosition;
                    _canCapScale = canCap.localScale;
                }

                // "he opens the bottle before he pours": the cap turns and rises off the spout between 9.1 and 9.6 s, then it is gone
                var e = Smooth(Mathf.InverseLerp(9.1f, 9.6f, t));
                var on = e < 0.995f;
                if (canCap.gameObject.activeSelf != on)
                    canCap.gameObject.SetActive(on);
                if (on)
                {
                    canCap.localPosition = _canCapPos + new Vector3(0f, 0f, -0.6f * e);
                    canCap.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(0f, 0f, 720f * e);
                    canCap.localScale = _canCapScale * (1f - 0.4f * e);
                }
            }

            BottleLevel = pb;
            CanLevel = pc;
            SetLiquid(bottleLiquid, pb * BottleMaxH);
            SetLiquid(bottle2Liquid, pb * BottleMaxH);   // follows the bottle into the bin
            SetLiquid(canLiquid, pc * CanMaxH);

            Transform mouth = null;
            if (t >= BottleFrom && t < BottleTo)
                mouth = bottleMouth;
            else if (t >= CanFrom && t <= CanTo + 0.02f)
                mouth = canMouth;

            if (mouth == null || tapTip == null || stream == null)
            {
                _streamOn = false;
                if (stream != null && stream.gameObject.activeSelf)
                    stream.gameObject.SetActive(false);
                SetSplash(false, transform.position);
                return;
            }

            var a = tapTip.position;
            var b = mouth.position;
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.01f)
            {
                _streamOn = false;
                stream.gameObject.SetActive(false);
                return;
            }

            _streamOn = true;
            if (!stream.gameObject.activeSelf)
                stream.gameObject.SetActive(true);
            var r = 0.0095f * (1f + (_frozen ? 0f : 0.10f * Mathf.Sin(Time.unscaledTime * 38f)));
            stream.SetPositionAndRotation(a, Quaternion.LookRotation(d));
            stream.localScale = new Vector3(r, r, len);
            SetSplash(!_frozen, b);
        }
    }
}

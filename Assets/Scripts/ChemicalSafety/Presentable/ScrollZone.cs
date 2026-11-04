using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Phone-style touch scrolling for a world-space viewport: while the gloved fingertip rests on the viewport and moves up or down
    /// the content follows the finger, and it keeps gliding a little after the finger lifts. Driven by FingerPokeSystem.Handle, which
    /// only does anything when a zone is registered (so scenarios without one behave exactly as before).
    /// </summary>
    public sealed class ScrollZone : MonoBehaviour
    {
        private static readonly List<ScrollZone> Zones = new List<ScrollZone>();

        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;

        private bool _touching;
        private float _lastY;
        private float _velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Zones.Clear();
        }

        public void Init(RectTransform view, RectTransform body)
        {
            viewport = view;
            content = body;
        }

        private void OnEnable()
        {
            if (!Zones.Contains(this))
                Zones.Add(this);
        }

        private void OnDisable()
        {
            Zones.Remove(this);
            _touching = false;
            _velocity = 0f;
        }

        /// <summary>How far the content can move (canvas units).</summary>
        private float Range()
        {
            return Mathf.Max(0f, content.rect.height - viewport.rect.height);
        }

        public void ScrollBy(float dy)
        {
            if (content == null || viewport == null)
                return;
            var p = content.anchoredPosition;
            p.y = Mathf.Clamp(p.y + dy, 0f, Range());
            content.anchoredPosition = p;
        }

        private void Update()
        {
            if (_touching || Mathf.Abs(_velocity) < 1f)
            {
                if (!_touching)
                    _velocity = 0f;
                return;
            }

            ScrollBy(_velocity * Time.unscaledDeltaTime);
            _velocity = Mathf.MoveTowards(_velocity, 0f, Mathf.Abs(_velocity) * 3.2f * Time.unscaledDeltaTime + 20f * Time.unscaledDeltaTime);
        }

        /// <summary>Called every frame with the fingertip (world) and its press threshold. Returns true while a zone is being dragged.</summary>
        public static bool Handle(Vector3 tip, float touchDepth)
        {
            var any = false;
            for (var i = Zones.Count - 1; i >= 0; i--)
            {
                var z = Zones[i];
                if (z == null || z.viewport == null || !z.viewport.gameObject.activeInHierarchy)
                    continue;
                var local = z.viewport.InverseTransformPoint(tip);
                var depth = -local.z * z.viewport.lossyScale.z; // positive = in front of the panel
                var r = z.viewport.rect;
                var inside = r.Contains(new Vector2(local.x, local.y)) && depth < touchDepth && depth > -0.04f;
                if (inside)
                {
                    if (!z._touching)
                    {
                        z._touching = true;
                        z._lastY = local.y;
                        z._velocity = 0f;
                    }
                    else
                    {
                        var dy = local.y - z._lastY;
                        z._lastY = local.y;
                        z.ScrollBy(dy);
                        var dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        z._velocity = Mathf.Lerp(z._velocity, dy / dt, 0.35f);
                    }

                    any = true;
                }
                else
                {
                    z._touching = false;
                }
            }

            return any;
        }
    }
}

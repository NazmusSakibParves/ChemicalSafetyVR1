using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// If the user physically walks their head into a solid prop (drum, rack, carrier) the view softly fades
    /// to black and back, so they never see the inside of the model.
    /// </summary>
    public sealed class HeadSolidGuard : MonoBehaviour
    {
        [SerializeField] private ScreenFader fader;
        [SerializeField] private float radius = 0.14f;

        private Transform _cam;
        private int _mask;
        private float _extra;
        private readonly Collider[] _hits = new Collider[4];

        private void Start()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _mask = LayerMask.GetMask("SolidProps");
        }

        private void Update()
        {
            if (_cam == null || fader == null || _mask == 0)
                return;

            var n = Physics.OverlapSphereNonAlloc(_cam.position, radius, _hits, _mask, QueryTriggerInteraction.Ignore);
            var target = n > 0 ? 0.92f : 0f;
            _extra = Mathf.MoveTowards(_extra, target, Time.unscaledDeltaTime * (target > _extra ? 6f : 3f));
            fader.SetExtra(_extra);
        }
    }
}

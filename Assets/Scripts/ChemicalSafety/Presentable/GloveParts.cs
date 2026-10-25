using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Moves (and turns) the glove meshes with the poke handle. The meshes stay inside the imported model,
    /// because a prefab instance will not allow them to be reparented.
    /// </summary>
    public sealed class GloveParts : MonoBehaviour
    {
        [SerializeField] private Transform[] parts;

        private Vector3[] _offset;
        private Quaternion[] _rel;

        public Transform[] Parts => parts;

        public void SetParts(Transform[] value)
        {
            parts = value;
        }

        private void Awake()
        {
            if (parts == null)
                return;
            _offset = new Vector3[parts.Length];
            _rel = new Quaternion[parts.Length];
            var inv = Quaternion.Inverse(transform.rotation);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                _offset[i] = inv * (parts[i].position - transform.position);
                _rel[i] = inv * parts[i].rotation;
            }
        }

        private void LateUpdate()
        {
            if (_offset == null)
                return;
            var rot = transform.rotation;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                parts[i].SetPositionAndRotation(transform.position + rot * _offset[i], rot * _rel[i]);
            }
        }
    }
}

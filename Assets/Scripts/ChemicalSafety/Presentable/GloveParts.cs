using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Moves the glove meshes with the poke handle. The meshes stay inside the imported model,
    /// because a prefab instance will not allow them to be reparented.
    /// </summary>
    public sealed class GloveParts : MonoBehaviour
    {
        [SerializeField] private Transform[] parts;

        private Vector3[] _offset;

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
            for (var i = 0; i < parts.Length; i++)
                if (parts[i] != null)
                    _offset[i] = parts[i].position - transform.position;
        }

        private void LateUpdate()
        {
            if (_offset == null)
                return;
            for (var i = 0; i < parts.Length; i++)
                if (parts[i] != null)
                    parts[i].position = transform.position + _offset[i];
        }
    }
}

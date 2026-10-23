using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Dims and brightens the AC readout about once a second so the unit reads as switched on.
    /// </summary>
    public sealed class Sc04DisplayPulse : MonoBehaviour
    {
        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_renderer == null)
                return;
            var k = 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f));
            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", new Color(k, k, k, 1f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}

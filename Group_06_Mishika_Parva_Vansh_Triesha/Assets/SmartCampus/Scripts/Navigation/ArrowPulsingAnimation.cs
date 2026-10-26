using UnityEngine;

namespace SmartCampus.Navigation
{
    public class ArrowPulsingAnimation : MonoBehaviour
    {
        [SerializeField] private float pulseSpeed = 3.5f;
        [SerializeField] private float minAlpha = 0.55f;
        [SerializeField] private float maxAlpha = 1.0f;
        [SerializeField] private float bobHeight = 0.02f;

        private Vector3 initialLocalPos;
        private MaterialPropertyBlock propBlock;
        private Renderer meshRenderer;
        private static readonly int ColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");
        private Color baseColor = new Color(0.1f, 0.85f, 1f, 1f);

        public void Initialize(Color color, int sequenceIndex)
        {
            baseColor = color;
            initialLocalPos = transform.localPosition;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            propBlock = new MaterialPropertyBlock();
            meshRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (meshRenderer == null) return;

            float timeOffset = transform.position.x * 0.5f + transform.position.z * 0.5f;
            float wave = Mathf.Sin((Time.time * pulseSpeed) - timeOffset);
            float normalized = (wave + 1f) * 0.5f; // 0 to 1

            // Subtle vertical hover
            transform.localPosition = initialLocalPos + Vector3.up * (wave * bobHeight);

            // Dynamic color/alpha pulse
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, normalized);
            Color c = baseColor;
            c.a = alpha;

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorPropId, c);
            propBlock.SetColor(EmissionColorPropId, baseColor * (0.8f + normalized * 0.6f));
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}

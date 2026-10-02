using UnityEngine;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// Linha de onda desenhada como malha de UI (recurso Ressonância, sublinhado do menu).
    /// A amplitude e a "porção acesa" são animáveis; a parte apagada fica como traço fino.
    /// Gera a malha só quando algo muda (SetVerticesDirty), não todo frame à toa.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIWaveform : MaskableGraphic
    {
        public int segments = 96;
        public float thickness = 3f;
        [Range(0, 1)] public float amplitude = 0.5f;   // 0..1 da metade da altura
        public float cycles = 6f;
        public float phase;
        [Range(0, 1)] public float fill = 1f;         // fração acesa (da esquerda)
        public Color dimColor = new Color(1, 1, 1, 0.18f);
        public float jitter;                           // ruído (corrupção/carga)
        public bool envelope = true;                   // pontas afinam

        public void Set(float amp, float fillFrac, float ph, float jit = 0f)
        {
            if (Mathf.Abs(amp - amplitude) < 0.001f && Mathf.Abs(fillFrac - fill) < 0.001f && Mathf.Abs(ph - phase) < 0.001f && jit == jitter) return;
            amplitude = amp; fill = fillFrac; phase = ph; jitter = jit;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float half = r.height * 0.5f;
            Vector2 prev = Vector2.zero;
            int n = Mathf.Max(8, segments);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float env = envelope ? Mathf.Sin(t * Mathf.PI) : 1f;
                float j = jitter > 0 ? (Mathf.PerlinNoise(t * 20f, phase * 3f) - 0.5f) * jitter : 0f;
                float y = (Mathf.Sin((t * cycles + phase) * Mathf.PI * 2f) * amplitude * env + j) * half;
                Vector2 p = new Vector2(r.xMin + t * r.width, r.center.y + y);
                if (i > 0)
                {
                    Vector2 d = (p - prev).normalized;
                    Vector2 nrm = new Vector2(-d.y, d.x) * thickness * 0.5f;
                    Color c = t <= fill ? color : dimColor;
                    int k = vh.currentVertCount;
                    vh.AddVert(prev - nrm, c, Vector2.zero);
                    vh.AddVert(prev + nrm, c, Vector2.zero);
                    vh.AddVert(p + nrm, c, Vector2.zero);
                    vh.AddVert(p - nrm, c, Vector2.zero);
                    vh.AddTriangle(k, k + 1, k + 2);
                    vh.AddTriangle(k, k + 2, k + 3);
                }
                prev = p;
            }
        }
    }
}

using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Escala de renderização + pós-processamento leve. A cena 3D é desenhada numa textura
    /// (menor que a tela na Média: 80% da resolução desenha 64% dos pixels) e esticada para a
    /// tela pelo shader Hidden/Aren/PostUber, que já aplica a correção de cor, a vinheta e,
    /// na qualidade Alta, o bloom — o blit existia de qualquer jeito, então a cor sai de graça.
    /// A UI (Screen Space Overlay) continua na resolução nativa e nítida.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RenderScaler : MonoBehaviour
    {
        public static float Scale = 1f;
        /// <summary>Correção de cor + vinheta (todas as qualidades exceto Baixa).</summary>
        public static bool PostColor = true;
        /// <summary>Bloom (só na Alta: 3 passadas em 1/4 e 1/8 da tela).</summary>
        public static bool PostBloom = false;

        // grade do pôr do sol de Campanula (ajustada olhando capturas do executável)
        public static float Contrast = 0.32f, Saturation = 1.12f, Vignette = 0.28f, Exposure = 1.04f;
        public static float BloomIntensity = 0.55f, BloomThreshold = 0.72f;
        public static Color ShadowTint = new Color(0.9f, 0.93f, 1.08f), HighTint = new Color(1.06f, 1.0f, 0.9f);

        Camera cam;
        RenderTexture rt;
        static Material mat;
        static readonly int IdBloomTex = Shader.PropertyToID("_BloomTex");

        void OnEnable() { cam = GetComponent<Camera>(); }

        bool Active => Scale < 0.999f || PostColor || PostBloom;

        // OnPreCull: trocar o alvo aqui ainda vale para o frame atual (no OnPreRender é tarde)
        void OnPreCull()
        {
            if (!Active) { cam.targetTexture = null; return; }
            int w = Mathf.Max(320, Mathf.RoundToInt(Screen.width * Scale));
            int h = Mathf.Max(180, Mathf.RoundToInt(Screen.height * Scale));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "RenderScaler", filterMode = FilterMode.Bilinear };
                rt.Create();
            }
            cam.targetTexture = rt;
        }

        void OnPostRender()
        {
            if (cam.targetTexture == null || rt == null) return;
            cam.targetTexture = null;
            if (!PostColor && !PostBloom) { Graphics.Blit(rt, (RenderTexture)null); return; }   // só a escala
            if (mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/ArenPostUber");
                if (sh == null || !sh.isSupported) { PostColor = PostBloom = false; Graphics.Blit(rt, (RenderTexture)null); return; }
                mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            mat.SetFloat("_Contrast", PostColor ? Contrast : 0f);
            mat.SetFloat("_Saturation", PostColor ? Saturation : 1f);
            mat.SetFloat("_Vignette", PostColor ? Vignette : 0f);
            mat.SetFloat("_Exposure", PostColor ? Exposure : 1f);
            mat.SetColor("_ShadowTint", PostColor ? ShadowTint : Color.white);
            mat.SetColor("_HighTint", PostColor ? HighTint : Color.white);

            RenderTexture b1 = null, b2 = null;
            if (PostBloom)
            {
                mat.SetFloat("_Threshold", BloomThreshold);
                mat.SetFloat("_Bloom", BloomIntensity);
                b1 = RenderTexture.GetTemporary(rt.width / 4, rt.height / 4, 0, RenderTextureFormat.Default);
                b2 = RenderTexture.GetTemporary(rt.width / 8, rt.height / 8, 0, RenderTextureFormat.Default);
                b1.filterMode = b2.filterMode = FilterMode.Bilinear;
                Graphics.Blit(rt, b1, mat, 1);   // pré-filtro (claros) em 1/4
                Graphics.Blit(b1, b2, mat, 2);   // desfoque em 1/8
                Graphics.Blit(b2, b1, mat, 2);   // desfoque de volta em 1/4 (mais largo)
                mat.SetTexture(IdBloomTex, b1);
                mat.EnableKeyword("ARENPOST_BLOOM");
            }
            else mat.DisableKeyword("ARENPOST_BLOOM");
            Graphics.Blit(rt, (RenderTexture)null, mat, 0);
            if (b1 != null) RenderTexture.ReleaseTemporary(b1);
            if (b2 != null) RenderTexture.ReleaseTemporary(b2);
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }
    }
}

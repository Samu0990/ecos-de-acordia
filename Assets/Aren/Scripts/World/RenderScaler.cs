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

        /// <summary>
        /// Abertura: cena em HDR + curva de filme, bloom em escalas, rastro anamórfico, raios de luz,
        /// grão e aberração sutil (mais caro: só na cinemática, Média ou acima).
        /// </summary>
        public static bool Cinematic;
        public static float CineBloom = 0.55f, CineThreshold = 0.95f, Streak = 0.22f, Grain = 0.03f, Aberration = 0.012f, Knee = 0.78f, Lift = 0.06f;
        public static Vector2 ShaftPos = new Vector2(0.5f, 0.6f);
        public static float ShaftIntensity;
        public static Color ShaftColor = new Color(0.8f, 0.7f, 1f), StreakColor = new Color(0.62f, 0.7f, 1f);

        /// <summary>HDR em 32 bits por pixel (R11G11B10) quando a placa aceita: mesma banda de memória do LDR.</summary>
        static RenderTextureFormat HdrFormat => SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.DefaultHDR;

        Camera cam;
        RenderTexture rt;
        static Material mat;
        static readonly int IdBloomTex = Shader.PropertyToID("_BloomTex");

        void OnEnable() { cam = GetComponent<Camera>(); }

        bool Active => Scale < 0.999f || PostColor || PostBloom || Cinematic;

        // OnPreCull: trocar o alvo aqui ainda vale para o frame atual (no OnPreRender é tarde)
        void OnPreCull()
        {
            if (!Active) { cam.targetTexture = null; return; }
            int w = Mathf.Max(320, Mathf.RoundToInt(Screen.width * Scale));
            int h = Mathf.Max(180, Mathf.RoundToInt(Screen.height * Scale));
            var fmt = Cinematic ? HdrFormat : RenderTextureFormat.Default;
            if (rt == null || rt.width != w || rt.height != h || rt.format != fmt)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24, fmt) { name = "RenderScaler", filterMode = FilterMode.Bilinear };
                rt.Create();
            }
            cam.allowHDR = Cinematic;
            cam.targetTexture = rt;
        }

        void OnPostRender()
        {
            if (cam.targetTexture == null || rt == null) return;
            cam.targetTexture = null;
            Composite(rt, null);
        }

        /// <summary>Correção de cor, vinheta e bloom de 'src' para 'dst' (null = tela). Também usado pelas prévias do editor.</summary>
        public static void Composite(RenderTexture src, RenderTexture dst)
        {
            if (!PostColor && !PostBloom && !Cinematic) { Graphics.Blit(src, dst); return; }   // só a escala
            if (mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/ArenPostUber");
                if (sh == null || !sh.isSupported) { PostColor = PostBloom = false; Graphics.Blit(src, dst); return; }
                mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            mat.SetFloat("_Contrast", PostColor ? Contrast : 0f);
            mat.SetFloat("_Saturation", PostColor ? Saturation : 1f);
            mat.SetFloat("_Vignette", PostColor ? Vignette : 0f);
            mat.SetFloat("_Exposure", PostColor ? Exposure : 1f);
            mat.SetColor("_ShadowTint", PostColor ? ShadowTint : Color.white);
            mat.SetColor("_HighTint", PostColor ? HighTint : Color.white);

            if (Cinematic) { CompositeCinematic(src, dst); return; }
            mat.DisableKeyword("ARENPOST_HDR"); mat.DisableKeyword("ARENPOST_CINE");
            RenderTexture b1 = null, b2 = null;
            if (PostBloom)
            {
                mat.SetFloat("_Threshold", BloomThreshold);
                mat.SetFloat("_Bloom", BloomIntensity);
                b1 = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0, src.format);
                b2 = RenderTexture.GetTemporary(src.width / 8, src.height / 8, 0, src.format);
                b1.filterMode = b2.filterMode = FilterMode.Bilinear;
                Graphics.Blit(src, b1, mat, 1);   // pré-filtro (claros) em 1/4
                Graphics.Blit(b1, b2, mat, 2);   // desfoque em 1/8
                Graphics.Blit(b2, b1, mat, 2);   // desfoque de volta em 1/4 (mais largo)
                mat.SetTexture(IdBloomTex, b1);
                mat.EnableKeyword("ARENPOST_BLOOM");
            }
            else mat.DisableKeyword("ARENPOST_BLOOM");
            Graphics.Blit(src, dst, mat, 0);
            if (b1 != null) RenderTexture.ReleaseTemporary(b1);
            if (b2 != null) RenderTexture.ReleaseTemporary(b2);
        }

        static readonly int IdLow = Shader.PropertyToID("_LowTex"), IdStreak = Shader.PropertyToID("_StreakTex"), IdShaft = Shader.PropertyToID("_ShaftTex");

        static void CompositeCinematic(RenderTexture src, RenderTexture dst)
        {
            var fmt = src.format;
            int w = src.width, h = src.height;
            RenderTexture T(int div) { var t = RenderTexture.GetTemporary(Mathf.Max(8, w / div), Mathf.Max(8, h / div), 0, fmt); t.filterMode = FilterMode.Bilinear; return t; }
            mat.SetFloat("_Threshold", CineThreshold);
            mat.SetFloat("_Bloom", CineBloom);
            mat.SetFloat("_Streak", Streak);
            mat.SetFloat("_Grain", Grain);
            mat.SetFloat("_Aberration", Aberration);
            mat.SetFloat("_Knee", Knee);
            mat.SetFloat("_Lift", Lift);
            mat.SetColor("_StreakColor", StreakColor);
            mat.SetColor("_ShaftColor", ShaftColor);
            mat.SetFloat("_ShaftIntensity", ShaftIntensity);
            mat.SetVector("_ShaftPos", ShaftPos);
            // bloom: 1/2 (pré-filtro) → 1/4 → 1/8 → 1/16 e de volta, somando os níveis
            var L0 = T(2); Graphics.Blit(src, L0, mat, 3);
            var L1 = T(4); Graphics.Blit(L0, L1, mat, 4);
            var L2 = T(8); Graphics.Blit(L1, L2, mat, 4);
            var L3 = T(16); Graphics.Blit(L2, L3, mat, 4);
            RenderTexture L4 = null, U3 = null;
            var U2 = T(8); mat.SetTexture(IdLow, L2); Graphics.Blit(L3, U2, mat, 5);
            var U1 = T(4); mat.SetTexture(IdLow, L1); Graphics.Blit(U2, U1, mat, 5);
            // rastro anamórfico (1/8 da tela, uma passada larga)
            RenderTexture S1 = null;
            var S2 = T(8); Graphics.Blit(L2, S2, mat, 6);
            // raios de luz
            RenderTexture SH = null;
            if (ShaftIntensity > 0.001f) { SH = T(4); Graphics.Blit(L1, SH, mat, 7); }
            mat.SetTexture(IdBloomTex, U1);
            mat.SetTexture(IdStreak, S2);
            mat.SetTexture(IdShaft, SH != null ? (Texture)SH : Texture2D.blackTexture);
            mat.EnableKeyword("ARENPOST_BLOOM"); mat.EnableKeyword("ARENPOST_HDR"); mat.EnableKeyword("ARENPOST_CINE");
            Graphics.Blit(src, dst, mat, 0);
            foreach (var t in new[] { L0, L1, L2, L3, L4, U3, U2, U1, S1, S2, SH }) if (t != null) RenderTexture.ReleaseTemporary(t);
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }
    }
}

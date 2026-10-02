using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Escala de renderização: a cena 3D é desenhada numa textura menor e esticada para a
    /// tela; a UI (Screen Space Overlay) continua na resolução nativa e nítida.
    /// No Intel UHD 620 a 1920×1080 o custo é quase todo de pixel — 80% da resolução
    /// desenha 64% dos pixels.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RenderScaler : MonoBehaviour
    {
        public static float Scale = 1f;
        Camera cam;
        RenderTexture rt;

        void OnEnable() { cam = GetComponent<Camera>(); }

        // OnPreCull: trocar o alvo aqui ainda vale para o frame atual (no OnPreRender é tarde)
        void OnPreCull()
        {
            if (Scale >= 0.999f) { cam.targetTexture = null; return; }
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
            Graphics.Blit(rt, (RenderTexture)null);   // para a tela; a UI overlay vem por cima
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }
    }
}

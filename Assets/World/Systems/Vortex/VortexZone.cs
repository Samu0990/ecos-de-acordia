using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// VÓRTICE: quando vários seres vivos corrompidos compartilham o mesmo Motivo, o lugar inteiro passa a
    /// obedecer a uma REGRA. Este componente transforma uma área normal em área de Vórtice SEM reconstruir a
    /// cena: liga/desliga pela condição do WorldState e, conforme a intensidade, puxa a neblina e a luz para
    /// a cor da distorção, acende partículas que mostram QUAL distorção acontece (Loop repete, Inversão cai
    /// para cima, Saturação incha, Roubo apaga a cor, Estouro pulsa, Ausência tira o som, Fenda duplica) e
    /// publica _ElyVortexPos/_ElyVortexK para shaders. O Contramotivo quebra a regra (BreakWithContramotivo).
    /// </summary>
    public class VortexZone : MonoBehaviour
    {
        [Tooltip("Id do Vórtice no cânone (WorldCanon.Vortices)")] public string vortexId = "";
        public float radius = 40f;
        [Tooltip("Quando o Vórtice existe (ex.: \"fase:VorticeAtivo\")")] public string activeWhen = "";
        public Transform core;                       // o Núcleo (hospedeiro vivo central)
        public ParticleSystem motes;
        public GameObject transformedDressing;       // objetos que só existem com o Vórtice (raízes, sinos tortos…)
        public Color tint = new Color(0.5f, 0.3f, 0.65f);
        public float fogBoost = 1.8f;

        public VortexDef Def => WorldCanon.Vortex(vortexId);
        public float Intensity { get; private set; }
        public bool PlayerInside { get; private set; }
        bool exists;
        Color baseFog; float baseDensity; bool hasBase;
        float shownToast = -100f;
        static readonly int IdPos = Shader.PropertyToID("_ElyVortexPos");
        static readonly int IdK = Shader.PropertyToID("_ElyVortexK");

        void OnEnable() { WorldState.OnChanged += Refresh; Refresh(); }
        void OnDisable() { WorldState.OnChanged -= Refresh; Shader.SetGlobalFloat(IdK, 0f); }

        void Refresh()
        {
            exists = WorldState.Check(activeWhen) && !WorldState.VortexBroken(vortexId);
            if (transformedDressing != null) transformedDressing.SetActive(exists);
            if (motes != null) { var em = motes.emission; em.enabled = exists; }
        }

        void Update()
        {
            var p = RegionFlow.Instance != null ? RegionFlow.Instance.Player : (Camera.main != null ? Camera.main.transform : null);
            float d = p != null ? Vector3.Distance(p.position, transform.position) : 1e9f;
            float target = exists ? Mathf.Clamp01(1f - (d - radius * 0.6f) / (radius * 0.6f)) : 0f;
            Intensity = Mathf.MoveTowards(Intensity, target, Time.deltaTime * 0.5f);
            bool inside = exists && d < radius;
            if (inside && !PlayerInside && Time.time - shownToast > 30f && Def != null)
            {
                shownToast = Time.time;
                Aren.UI.ArenHUD.Instance?.ShowArea(Def.name, "Vórtice · " + Def.regra);
            }
            PlayerInside = inside;
            if (!hasBase) { baseFog = RenderSettings.fogColor; baseDensity = RenderSettings.fogDensity; hasBase = true; }
            if (Intensity > 0.001f || target > 0f)
            {
                // o RegionRoot reescreve a neblina quando a Corrupção muda; aqui só somamos por cima
                var rr = RegionRoot.Current;
                Color fogNow = rr != null && rr.profile != null ? Color.Lerp(rr.profile.fogColor, rr.profile.corruptionFog, WorldState.Corruption(rr.region) * 0.55f) : baseFog;
                float densNow = rr != null && rr.profile != null ? rr.profile.fogDensity * (1f + WorldState.Corruption(rr.region) * 0.35f) : baseDensity;
                RenderSettings.fogColor = Color.Lerp(fogNow, tint * 0.6f, Intensity * 0.6f);
                RenderSettings.fogDensity = densNow * Mathf.Lerp(1f, fogBoost, Intensity);
            }
            Shader.SetGlobalVector(IdPos, new Vector4(transform.position.x, transform.position.y, transform.position.z, radius));
            Shader.SetGlobalFloat(IdK, Intensity);
        }

        /// <summary>O Contramotivo foi cumprido: a regra deixa de ser absoluta e o lugar volta a responder.</summary>
        public void BreakWithContramotivo()
        {
            if (string.IsNullOrEmpty(vortexId)) return;
            WorldState.BreakVortex(vortexId);
            var def = Def;
            Aren.UI.ArenHUD.Instance?.ShowArea("Vórtice quebrado", def != null ? def.contramotivo : "");
            Aren.World.Notas.Add(500);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

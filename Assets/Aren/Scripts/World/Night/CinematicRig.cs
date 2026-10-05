using Aren.UI;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Luz e lente da abertura (só enquanto ela toca; tudo volta ao fim):
    ///   · recorte: luz fria de lua sempre ATRÁS do Aren em relação à câmera (silhueta nos planos);
    ///   · Fenda: luz violeta vinda da direção dela no rosto/peito dele, acompanhando a abertura e o pulso;
    ///   · impacto: luz quente vinda do leste quando a nota cai (e a brasa que fica);
    ///   · pós-processamento cinematográfico (HDR, bloom em escalas, raios de luz da Fenda/impacto);
    ///   · sombras da lua ligadas na abertura (Média ou acima), com distância curta.
    /// Luzes pontuais de alcance curto: só o Aren e o chão perto dele pagam o custo.
    /// </summary>
    public class CinematicRig : MonoBehaviour
    {
        public static CinematicRig Instance { get; private set; }
        public Transform aren;
        public Camera cam;
        public float rimLevel = 1f, fendaKick = 1f;
        /// <summary>Ponto do mundo para os raios de luz (null = escolhe Fenda/impacto sozinho).</summary>
        public System.Func<Vector3> shaftSource;
        public float shaftLevel = 0f;
        Light rim, fendaL, impactL;
        Transform head;
        // estado salvo
        ShadowQuality savedShadows; float savedShadowDist; LightShadows savedMoonShadows; float savedExposure;
        bool savedCine; Light moon;

        public static CinematicRig Begin(Transform aren, Camera cam)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("Luz da abertura");
            var r = go.AddComponent<CinematicRig>();
            Instance = r;
            r.aren = aren; r.cam = cam;
            var an = aren.GetComponentInChildren<Animator>();
            r.head = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            r.rim = r.Point("Recorte (lua)", new Color(0.55f, 0.65f, 1f), 3.2f);
            r.fendaL = r.Point("Luz da Fenda", new Color(0.62f, 0.42f, 1f), 4f);
            r.impactL = r.Point("Luz do impacto (rosto)", new Color(1f, 0.68f, 0.38f), 5f);
            // pós e sombras
            r.savedCine = RenderScaler.Cinematic;
            r.savedExposure = RenderScaler.Exposure;
            RenderScaler.Cinematic = GameSettings.Quality >= 1;
            RenderScaler.Exposure = r.savedExposure * 1.12f;   // na abertura um pouco mais aberto (o ombro de filme segura os claros)
            r.savedShadows = QualitySettings.shadows; r.savedShadowDist = QualitySettings.shadowDistance;
            r.moon = RenderSettings.sun;
            if (r.moon != null) r.savedMoonShadows = r.moon.shadows;
            if (GameSettings.Quality >= 1 && r.moon != null)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 45f;
                r.moon.shadows = LightShadows.Soft;
                r.moon.shadowStrength = 0.72f;
            }
            return r;
        }

        Light Point(string n, Color c, float range)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        void LateUpdate()
        {
            if (aren == null) return;
            Vector3 h = head != null ? head.position : aren.position + Vector3.up * 1.6f;
            Vector3 camF = cam != null ? cam.transform.forward : aren.forward;
            camF.y = 0f; camF = camF.sqrMagnitude > 1e-4f ? camF.normalized : aren.forward;
            // recorte: atrás dele (do ponto de vista da câmera), um pouco acima e para o lado da lua
            Vector3 moonSide = Vector3.ProjectOnPlane(NightSetup.MoonDir, Vector3.up).normalized;
            rim.transform.position = h + camF * 1.3f + Vector3.up * 0.55f + moonSide * 0.5f;
            rim.intensity = 1.25f * rimLevel;
            // Fenda
            float open = RuptureSky.Instance != null ? RuptureSky.Instance.fendaOpen : 0f;
            float pulse = RuptureSky.Instance != null ? RuptureSky.Instance.fendaPulse : 0f;
            Vector3 fd = NightSetup.Dir(NightSetup.FendaAz, 0.25f);
            fendaL.transform.position = h + fd * 1.7f;
            fendaL.intensity = open * (0.9f + 0.35f * pulse) * fendaKick * 1.4f;
            // impacto
            var fx = ImpactFX.Instance;
            if (fx != null)
            {
                Vector3 id = (fx.Point - h); id.y = 0f; id = id.normalized + Vector3.up * 0.12f;
                impactL.transform.position = h + id.normalized * 2.0f;
                impactL.intensity = Mathf.Min(fx.Glow, 4f) * 0.55f;
                RenderScaler.Exposure = savedExposure * 1.12f * (1f + 0.22f * fx.Flash);
            }
            else impactL.intensity = 0f;
            // raios de luz: da Fenda (aberta) ou do impacto (clarão/brasa), se estiverem na tela
            if (cam != null && RenderScaler.Cinematic)
            {
                Vector3 src; float k;
                if (shaftSource != null) { src = shaftSource(); k = shaftLevel; }
                else if (fx != null && fx.T > 0f) { src = fx.Point + Vector3.up * 150f; k = Mathf.Clamp01(fx.Glow * 0.4f) * shaftLevel; }
                else { src = aren.position + NightSetup.Dir(NightSetup.FendaAz, 0.2f) * 3000f; k = open * 0.55f * shaftLevel; }
                var vp = cam.WorldToViewportPoint(src);
                float on = vp.z > 0f ? Mathf.Clamp01(1.4f - Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f)) * 1.6f) : 0f;
                RenderScaler.ShaftPos = new Vector2(vp.x, vp.y);
                RenderScaler.ShaftIntensity = k * on;
                RenderScaler.ShaftColor = fx != null && fx.T > 0f ? new Color(1f, 0.75f, 0.45f) : new Color(0.7f, 0.6f, 1f);
            }
        }

        /// <summary>Fim da abertura: desliga as luzes e devolve pós, exposição e sombras.</summary>
        public void End()
        {
            RenderScaler.Cinematic = savedCine;
            RenderScaler.Exposure = savedExposure;
            RenderScaler.ShaftIntensity = 0f;
            QualitySettings.shadows = savedShadows; QualitySettings.shadowDistance = savedShadowDist;
            if (moon != null) moon.shadows = savedMoonShadows;
            Destroy(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}

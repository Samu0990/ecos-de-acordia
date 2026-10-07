using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Raiz de uma cena de reino (ou masmorra). Aplica a identidade visual do RegionProfile ao carregar
    /// (luz, ambiente, neblina, céu) e mistura a COR DA CORRUPÇÃO conforme o WorldState: o mesmo reino
    /// fica mais pesado/frio quando a Corrupção sobe e clareia quando uma Nota é reafinada — sem
    /// reconstruir a cena. Também publica _ElyCorruption / _ElyCorruptionTint para os shaders.
    /// </summary>
    public class RegionRoot : MonoBehaviour
    {
        public RegionId region;
        public RegionProfile profile;
        public bool isDungeon;
        public string dungeonId = "";
        [Tooltip("Onde o Aren aparece se não chegou por um portão nem tem ponto de retorno")]
        public Transform defaultSpawn;
        public Light sun;

        public static RegionRoot Current { get; private set; }
        public RegionDef Def => WorldCanon.Region(region);
        float shownCorruption = -1f;

        static readonly int IdCorr = Shader.PropertyToID("_ElyCorruption");
        static readonly int IdCorrTint = Shader.PropertyToID("_ElyCorruptionTint");

        void Awake() { Current = this; Apply(true); }
        void OnEnable() { WorldState.OnChanged += OnWorld; }
        void OnDisable() { WorldState.OnChanged -= OnWorld; Shader.SetGlobalFloat(IdCorr, 0f); }
        void OnWorld() { }

        void Update()
        {
            // a Corrupção muda devagar quando o estado do mundo muda (ex.: Vórtice quebrado)
            float target = WorldState.Corruption(region);
            if (Mathf.Abs(target - shownCorruption) > 0.001f)
            {
                shownCorruption = shownCorruption < 0 ? target : Mathf.MoveTowards(shownCorruption, target, Time.deltaTime * 0.15f);
                Apply(false);
            }
        }

        public void Apply(bool full)
        {
            if (profile == null) return;
            float c = shownCorruption < 0 ? WorldState.Corruption(region) : shownCorruption;
            if (full)
            {
                if (sun != null)
                {
                    sun.color = profile.sunColor;
                    sun.intensity = profile.sunIntensity;
                    sun.transform.rotation = Quaternion.Euler(profile.sunAngles.x, profile.sunAngles.y, 0f);
                    RenderSettings.sun = sun;
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                ApplyGrade();
            }
            float k = c * 0.55f;   // a Corrupção puxa a neblina e o ambiente para a cor do reino corrompido
            RenderSettings.fogColor = Color.Lerp(profile.fogColor, profile.corruptionFog, k);
            RenderSettings.fogDensity = profile.fogDensity * (1f + c * 0.35f);
            RenderSettings.ambientSkyColor = Color.Lerp(profile.ambientSky, profile.corruptionFog, k * 0.6f);
            RenderSettings.ambientEquatorColor = Color.Lerp(profile.ambientEquator, profile.corruptionTint * 0.6f, k * 0.5f);
            RenderSettings.ambientGroundColor = profile.ambientGround;
            Shader.SetGlobalFloat(IdCorr, c);
            Shader.SetGlobalColor(IdCorrTint, profile.corruptionTint);
        }

        /// <summary>Imagem do reino (contraste, saturação, vinheta, tons): substitui o que veio de Campânula
        /// (a noite de Campânula liga Purkinje e outra curva; sem isto ela vazaria para os reinos).</summary>
        void ApplyGrade()
        {
            Aren.World.RenderScaler.Contrast = profile.gradeContrast;
            Aren.World.RenderScaler.Saturation = profile.gradeSaturation;
            Aren.World.RenderScaler.Vignette = profile.gradeVignette;
            Aren.World.RenderScaler.Exposure = profile.gradeExposure;
            Aren.World.RenderScaler.Purkinje = profile.purkinje;
            Aren.World.RenderScaler.ShadowTint = profile.shadowTint;
            Aren.World.RenderScaler.HighTint = profile.highTint;
            // de dia o limiar sobe: só lanternas, janelas e brilhos florescem (antes a grama ao sol de Orvalume virava um borrão amarelo)
            Aren.World.RenderScaler.BloomThreshold = profile.sunIntensity >= 0.8f ? 1.05f : 0.7f; Aren.World.RenderScaler.BloomIntensity = 0.6f;
            if (profile.bloom && Aren.UI.GameSettings.Quality >= 1) Aren.World.RenderScaler.PostBloom = true;
        }
    }
}

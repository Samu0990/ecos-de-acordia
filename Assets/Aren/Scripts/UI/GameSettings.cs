using Cinemachine;
using UnityEngine;

namespace Aren.UI
{
    /// <summary>Configurações persistentes (PlayerPrefs) e aplicação no jogo.</summary>
    public static class GameSettings
    {
        public static float Master = 0.9f, Music = 0.6f, Effects = 1f, Sensitivity = 1f;
        public static int Quality = 1;          // 0 Baixa · 1 Média · 2 Alta
        public static CombatInstrument Instrument = CombatInstrument.Flute;
        public static bool Fullscreen = true, Shake = true, ShowFps = false, Shadows = false;   // sombras em tempo real: desligadas por padrão (custam ~10 FPS no Intel UHD)
        public static int ResolutionIndex = -1;
        public static float RenderScale = 0.8f;   // 3D a 80% na Média (UI sempre nativa)
        public static readonly float[] RenderScales = { 0.6f, 0.7f, 0.8f, 0.9f, 1f };
        static bool loaded;
        static float baseX = -1f, baseY = -1f;

        public static readonly string[] QualityNames = { "Baixa", "Média", "Alta" };

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Master = PlayerPrefs.GetFloat("eda_master", Master);
            Music = PlayerPrefs.GetFloat("eda_music", Music);
            Effects = PlayerPrefs.GetFloat("eda_fx", Effects);
            Sensitivity = PlayerPrefs.GetFloat("eda_sens", Sensitivity);
            Quality = PlayerPrefs.GetInt("eda_quality", Quality);
            Fullscreen = PlayerPrefs.GetInt("eda_full", Fullscreen ? 1 : 0) == 1;
            Shake = PlayerPrefs.GetInt("eda_shake", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("eda_fps", 0) == 1;
            Shadows = PlayerPrefs.GetInt("eda_shadows", 0) == 1;
            ResolutionIndex = PlayerPrefs.GetInt("eda_res", -1);
            RenderScale = PlayerPrefs.GetFloat("eda_rscale", RenderScale);
            Instrument = (CombatInstrument)Mathf.Clamp(PlayerPrefs.GetInt("eda_instrument", (int)Instrument), 0, 1);
            ApplyCommandLineOverrides();
        }

        /// <summary>
        /// Sobrescreve qualidade/escala pela linha de comando (-eda-quality 0|1|2, -eda-scale 0.8)
        /// sem gravar nada — o benchmark mede uma configuração conhecida.
        /// </summary>
        /// <summary>-eda-uncapped: sem limite de FPS (medir a folga real no benchmark).</summary>
        public static bool Uncapped;
        static int ForceDetails = -1, ForcePost = -1;

        public static void ApplyCommandLineOverrides()
        {
            var args = System.Environment.GetCommandLineArgs();
            foreach (var a in args) if (a == "-eda-uncapped") Uncapped = true;
            for (int i = 0; i < args.Length - 1; i++)
            {
                // A/B de custo no benchmark: -eda-details 0|1, -eda-post 0|1
                if (args[i] == "-eda-details") ForceDetails = args[i + 1] != "0" ? 1 : 0;
                if (args[i] == "-eda-post") ForcePost = args[i + 1] != "0" ? 1 : 0;
            }
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-eda-quality" && int.TryParse(args[i + 1], out int q)) Quality = Mathf.Clamp(q, 0, 2);
                if (args[i] == "-eda-shadows") Shadows = args[i + 1] != "0";
                if (args[i] == "-eda-scale" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sc)) RenderScale = Mathf.Clamp(sc, 0.5f, 1f);
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("eda_master", Master);
            PlayerPrefs.SetFloat("eda_music", Music);
            PlayerPrefs.SetFloat("eda_fx", Effects);
            PlayerPrefs.SetFloat("eda_sens", Sensitivity);
            PlayerPrefs.SetInt("eda_quality", Quality);
            PlayerPrefs.SetInt("eda_full", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("eda_shake", Shake ? 1 : 0);
            PlayerPrefs.SetInt("eda_fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("eda_shadows", Shadows ? 1 : 0);
            PlayerPrefs.SetInt("eda_res", ResolutionIndex);
            PlayerPrefs.SetFloat("eda_rscale", RenderScale);
            PlayerPrefs.SetInt("eda_instrument", (int)Instrument);
            PlayerPrefs.Save();
        }

        public static void Apply(bool display = false)
        {
            ArenAudio.Master = Master; ArenAudio.Music = Music; ArenAudio.Effects = Effects; ArenAudio.UI = Mathf.Min(1f, Effects * 0.9f);
            ArenAudio.Instrument = Instrument;
            ArenAudio.ApplyVolumes();
            Combat.GameFeel.ShakeMultiplier = Shake ? 1f : 0f;
            ArenVFX.HighQuality = Quality >= 1;      // distorção de tela (GrabPass)
            ArenVFX.FlashLights = Quality >= 2;      // luz de flash nos impactos (luz por pixel extra)
            Aren.World.RenderScaler.Scale = RenderScale;
            // pós-processamento: cor e vinheta a partir da Média (embutidos no blit da escala),
            // bloom só na Alta
            Aren.World.RenderScaler.PostColor = ForcePost >= 0 ? ForcePost == 1 : Quality >= 1;
            Aren.World.RenderScaler.PostBloom = ForcePost >= 0 ? ForcePost == 1 && Quality >= 2 : Quality >= 2;
            // capim e trigo do terreno: desligados na Baixa, curtos na Média
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                bool details = ForceDetails >= 0 ? ForceDetails == 1 : Quality >= 1;
                terrain.detailObjectDistance = !details ? 0f : Quality <= 1 ? 30f : 60f;
                terrain.detailObjectDensity = !details ? 0f : Quality <= 1 ? 0.45f : 1f;
                // normal map nas camadas do terreno só na Alta (cobre metade da tela)
            }
            bool adaptiveDetails = ForceDetails >= 0 ? ForceDetails == 1 : Quality >= 1;
            Aren.World.AdaptivePerformance.Configure(RenderScale, adaptiveDetails, Quality);

            // sombras e luzes por nível (o alvo é Intel UHD 620)
            switch (Quality)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 28f;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.lodBias = 0.7f;
                    break;
                case 1:
                    // sombras são o maior custo no Intel UHD (benchmark: +10 FPS sem elas);
                    // na Média ficam curtas (personagens e o entorno imediato)
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 20f;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.lodBias = 1f;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 60f;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.pixelLightCount = 2;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.lodBias = 1.5f;
                    break;
            }
            // sem VSync: com VSync um frame de 17 ms vira 33 ms (cai de 60 direto para 30);
            // o limite de 60 evita esquentar o notebook à toa
            // sem sombra em tempo real: construções usam as sombras assadas (lightmap) e os
            // personagens a sombra blob
            bool realtimeShadows = Shadows || Quality >= 2;
            if (!realtimeShadows) QualitySettings.shadows = ShadowQuality.Disable;
            BlobShadow.Enabled = !realtimeShadows;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Uncapped ? -1 : 60;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.softParticles = Quality >= 2;

            var fl = Object.FindAnyObjectByType<CinemachineFreeLook>();
            if (fl != null)
            {
                if (baseX < 0f) { baseX = fl.m_XAxis.m_MaxSpeed; baseY = fl.m_YAxis.m_MaxSpeed; }
                fl.m_XAxis.m_MaxSpeed = baseX * Sensitivity;
                fl.m_YAxis.m_MaxSpeed = baseY * Sensitivity;
            }

            if (display)
            {
                var res = Screen.resolutions;
                if (ResolutionIndex >= 0 && ResolutionIndex < res.Length)
                {
                    var r = res[ResolutionIndex];
                    Screen.SetResolution(r.width, r.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                }
                else Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }

        public static void StepRenderScale(int dir)
        {
            int i = System.Array.IndexOf(RenderScales, RenderScale);
            if (i < 0) i = 2;
            i = Mathf.Clamp(i + dir, 0, RenderScales.Length - 1);
            RenderScale = RenderScales[i];
        }

        public static string ResolutionLabel()
        {
            var res = Screen.resolutions;
            if (ResolutionIndex < 0 || ResolutionIndex >= res.Length) return Screen.width + " × " + Screen.height;
            return res[ResolutionIndex].width + " × " + res[ResolutionIndex].height;
        }
    }
}

using Cinemachine;
using UnityEngine;

namespace Aren.UI
{
    /// <summary>Configurações persistentes (PlayerPrefs) e aplicação no jogo.</summary>
    public static class GameSettings
    {
        public static float Master = 0.9f, Music = 0.6f, Effects = 1f, Sensitivity = 1f;
        public static int Quality = 1;          // 0 Baixa · 1 Média · 2 Alta
        public static bool Fullscreen = true, Shake = true, ShowFps = false;
        public static int ResolutionIndex = -1;
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
            ResolutionIndex = PlayerPrefs.GetInt("eda_res", -1);
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
            PlayerPrefs.SetInt("eda_res", ResolutionIndex);
            PlayerPrefs.Save();
        }

        public static void Apply(bool display = false)
        {
            ArenAudio.Master = Master; ArenAudio.Music = Music; ArenAudio.Effects = Effects; ArenAudio.UI = Mathf.Min(1f, Effects * 0.9f);
            ArenAudio.ApplyVolumes();
            Combat.GameFeel.ShakeMultiplier = Shake ? 1f : 0f;
            ArenVFX.HighQuality = Quality >= 1;

            // sombras e luzes por nível (o alvo é Intel UHD 620)
            switch (Quality)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 28f;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.lodBias = 0.7f;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 42f;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.pixelLightCount = 1;
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
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;

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

        public static string ResolutionLabel()
        {
            var res = Screen.resolutions;
            if (ResolutionIndex < 0 || ResolutionIndex >= res.Length) return Screen.width + " × " + Screen.height;
            return res[ResolutionIndex].width + " × " + res[ResolutionIndex].height;
        }
    }
}

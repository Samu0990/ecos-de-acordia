using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Guarda de frame pacing para o notebook alvo. Depois de dois segundos sustentados
    /// abaixo de ~42 FPS, reduz em passos pequenos apenas o custo escalavel (RT 3D e
    /// detalhes do terreno). Recupera lentamente quando existe folga. UI, gameplay,
    /// resolucao da janela e sombras configuradas pelo jogador nao sao alterados.
    /// Também age nas cenas (abertura e Corrupção na vila); fica desligado no benchmark e nas gravações.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class AdaptivePerformance : MonoBehaviour
    {
        public static AdaptivePerformance Instance { get; private set; }
        public static string DebugStatus => Instance == null ? "adaptativo=off" : Instance.Status;

        float baseScale = 0.8f;
        float baseDetailDistance = 30f;
        float baseDetailDensity = 0.45f;
        float ema = 1f / 60f;
        float windowTime;
        int slowWindows, stableWindows, level;
        bool configured;

        string Status => "adaptativo=" + level + " escala=" + RenderScaler.Scale.ToString("0.00")
            + " fpsEMA=" + (1f / Mathf.Max(0.001f, ema)).ToString("0.0");

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Configure(float renderScale, bool details, int quality)
        {
            if (Instance == null) return;
            Instance.baseScale = renderScale;
            Instance.baseDetailDistance = !details ? 0f : quality <= 1 ? 30f : 60f;
            Instance.baseDetailDensity = !details ? 0f : quality <= 1 ? 0.45f : 1f;
            Instance.level = 0;
            Instance.slowWindows = Instance.stableWindows = 0;
            Instance.configured = true;
            Instance.ApplyLevel();
        }

        void Update()
        {
            if (!configured || DemoBenchmark.Requested || DemoIntroShots.Requested) return; // benchmark e gravações ficam em 80% fixos
            var flow = GameFlow.Instance;
            // vale no jogo E nas cenas (abertura, Corrupção na vila: são as partes mais pesadas)
            bool live = flow != null && (flow.Current == GameFlow.State.Playing || flow.Current == GameFlow.State.Cutscene);
            if (!live || Time.timeScale < 0.9f)
            {
                windowTime = 0f;
                slowWindows = stableWindows = 0;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 0.12f) return; // teleporte/loading nao decide qualidade
            ema += (dt - ema) * (1f - Mathf.Exp(-dt * 2.2f));
            windowTime += dt;
            if (windowTime < 1f) return;
            windowTime = 0f;

            if (ema > 1f / 42f)
            {
                slowWindows++;
                stableWindows = 0;
                if (slowWindows >= 2 && level < 2)
                {
                    level++;
                    slowWindows = 0;
                    ApplyLevel();
                }
            }
            else if (ema < 1f / 54f)
            {
                stableWindows++;
                slowWindows = 0;
                if (stableWindows >= 8 && level > 0)
                {
                    level--;
                    stableWindows = 0;
                    ApplyLevel();
                }
            }
            else slowWindows = stableWindows = 0;
        }

        void ApplyLevel()
        {
            RenderScaler.Scale = Mathf.Max(0.6f, baseScale - level * 0.05f);
            var terrain = Terrain.activeTerrain;
            if (terrain == null || baseDetailDistance <= 0f) return;
            terrain.detailObjectDistance = baseDetailDistance * (level == 0 ? 1f : level == 1 ? 0.82f : 0.68f);
            terrain.detailObjectDensity = baseDetailDensity * (level == 0 ? 1f : level == 1 ? 0.74f : 0.52f);
        }
    }
}

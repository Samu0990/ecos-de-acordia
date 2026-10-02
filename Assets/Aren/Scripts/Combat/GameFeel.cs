using Cinemachine;
using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Dono único do Time.timeScale (pausa, hitstop, câmera lenta) e dos efeitos de câmera
    /// (tremor por "trauma", soco de FOV). Ninguém mais mexe no timeScale — senão pausa e
    /// hitstop brigam (um despausa o outro).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameFeel : MonoBehaviour
    {
        static GameFeel instance;
        public static GameFeel Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<GameFeel>();
                    if (instance == null) instance = new GameObject("GameFeel").AddComponent<GameFeel>();
                }
                return instance;
            }
        }

        public static bool Paused { get; private set; }
        public static float ShakeMultiplier = 1f;   // configurações (0 = desligado)
        /// <summary>Só para testes/capturas: câmera lenta global (1 = normal).</summary>
        public static float DebugScale = 1f;

        float hitstopUntil, hitstopScale = 1f;
        float slowUntil, slowScale = 1f, slowFadeIn;
        float baseFixedDelta = 0.02f;
        ArenCameraFX camFX;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            baseFixedDelta = Time.fixedDeltaTime;
        }

        public static void SetPaused(bool p)
        {
            Paused = p;
            // não cria o singleton durante o descarregamento da cena (OnDestroy de outros)
            if (instance != null) instance.Apply();
            else Time.timeScale = p ? 0f : 1f;
        }

        /// <summary>Congela o jogo quase todo por 'duration' segundos reais.</summary>
        public static void Hitstop(float duration, float scale = 0.02f)
        {
            var i = Instance;
            float until = Time.unscaledTime + duration;
            if (until > i.hitstopUntil) { i.hitstopUntil = until; i.hitstopScale = scale; }
            i.Apply();
        }

        /// <summary>Câmera lenta (esquiva perfeita, counter, Contracanto).</summary>
        public static void SlowMo(float duration, float scale = 0.3f)
        {
            var i = Instance;
            i.slowUntil = Time.unscaledTime + duration;
            i.slowScale = scale;
            i.Apply();
        }

        public static void Shake(float trauma, Vector3 direction = default)
        {
            var fx = Instance.GetCamFX();
            if (fx != null) fx.AddTrauma(trauma * ShakeMultiplier, direction);
        }

        public static void FovPunch(float degrees, float duration = 0.35f)
        {
            var fx = Instance.GetCamFX();
            if (fx != null) fx.FovPunch(degrees, duration);
        }

        ArenCameraFX GetCamFX()
        {
            if (camFX != null) return camFX;
            var fl = FindAnyObjectByType<CinemachineFreeLook>();
            if (fl == null) return null;
            camFX = fl.GetComponent<ArenCameraFX>();
            if (camFX == null) camFX = fl.gameObject.AddComponent<ArenCameraFX>();
            return camFX;
        }

        void Update() => Apply();

        void Apply()
        {
            float now = Time.unscaledTime;
            float s = 1f;
            if (now < slowUntil)
            {
                // sai da câmera lenta suavemente nos últimos 30%
                float left = slowUntil - now;
                s = Mathf.Lerp(1f, slowScale, Mathf.Clamp01(left / 0.12f));
            }
            if (now < hitstopUntil) s = Mathf.Min(s, hitstopScale);
            if (Paused) s = 0f;
            s *= DebugScale;
            Time.timeScale = s;
            // física acompanha a câmera lenta sem ficar "travando" em passos grandes
            Time.fixedDeltaTime = s > 0.05f ? baseFixedDelta * Mathf.Min(1f, s) : baseFixedDelta;
        }

        void OnDestroy()
        {
            if (instance == this) { Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; Paused = false; }
        }
    }

    /// <summary>
    /// Extensão do Cinemachine: tremor por trauma (Perlin, decai com o quadrado) com
    /// componente direcional (o golpe "empurra" a câmera na direção do impacto) e soco
    /// de FOV. Roda no estágio Noise, depois de Body/Aim — não briga com o FreeLook.
    /// </summary>
    public class ArenCameraFX : CinemachineExtension
    {
        public float maxAngle = 1.35f;    // graus: impacto legível sem desorientar
        public float maxOffset = 0.075f;  // metros
        public float decay = 2.8f;        // trauma/s
        float trauma;
        Vector3 kickDir;
        float fovPunch, fovPunchTime, fovPunchDur = 0.3f;
        float seed;

        protected override void Awake() { base.Awake(); seed = Random.value * 100f; }

        public void AddTrauma(float t, Vector3 dir)
        {
            trauma = Mathf.Clamp01(trauma + t);
            if (dir.sqrMagnitude > 0.001f) kickDir = dir.normalized * Mathf.Clamp01(t * 2f);
        }

        public void FovPunch(float deg, float dur)
        {
            fovPunch = deg; fovPunchDur = Mathf.Max(0.05f, dur); fovPunchTime = Time.unscaledTime;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise) return;
            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.Max(0f, trauma - decay * dt);
            float k = trauma * trauma;
            if (k > 0.0001f)
            {
                float t = Time.unscaledTime * 22f;
                float px = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f;
                float py = (Mathf.PerlinNoise(seed + 10f, t) - 0.5f) * 2f;
                float pr = (Mathf.PerlinNoise(seed + 20f, t) - 0.5f) * 2f;
                var rot = Quaternion.Euler(py * maxAngle * k, px * maxAngle * k, pr * maxAngle * 0.6f * k);
                state.OrientationCorrection = state.OrientationCorrection * rot;
                Vector3 off = new Vector3(px, py, 0) * maxOffset * k;
                state.PositionCorrection += state.FinalOrientation * off + kickDir * (maxOffset * 1.5f * k);
            }
            kickDir = Vector3.MoveTowards(kickDir, Vector3.zero, dt * 4f);

            float e = (Time.unscaledTime - fovPunchTime) / fovPunchDur;
            if (e < 1f)
            {
                var lens = state.Lens;
                // sobe rápido, volta com ease-out
                float curve = e < 0.15f ? e / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (e - 0.15f) / 0.85f);
                lens.FieldOfView += fovPunch * curve;
                state.Lens = lens;
            }
        }
    }
}

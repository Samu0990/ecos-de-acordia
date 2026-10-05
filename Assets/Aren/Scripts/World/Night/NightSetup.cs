using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// A noite da Ruptura: o jogo começa à noite (storyboard da abertura) e a transição para o
    /// gameplay é contínua, então a noite vale para o começo do jogo inteiro. Troca o céu
    /// (Hidden/Aren/NightSky), transforma o sol em lua, ajusta ambiente, névoa e a gradação de cor,
    /// acende halos nos lampiões e tochas e esconde a Fenda antiga do pôr do sol (a nova fica no
    /// céu, muito longe, a nor-nordeste). Para voltar ao pôr do sol: Enabled = false.
    /// </summary>
    public static class NightSetup
    {
        public static bool Enabled = true;
        public static bool Applied { get; private set; }

        // direções no céu (azimute 0 = norte/+z, +π/2 = leste/+x)
        public const float FendaAz = 0.30f;          // nor-nordeste, atrás da vila
        public const float FendaBaseEl = 0.10f;      // seno da elevação de onde os brilhos saem
        public const float ImpactAz = 1.31f;         // leste: serra onde o brilho cai
        public static readonly Vector3 MoonDir = Dir(-2.0f, 0.55f);

        public static Vector3 Dir(float az, float sinEl)
        {
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - sinEl * sinEl));
            return new Vector3(Mathf.Sin(az) * c, sinEl, Mathf.Cos(az) * c);
        }

        static float WrapPi(float a) => a - 6.2831853f * Mathf.Floor((a + 3.14159265f) / 6.2831853f);

        /// <summary>Cópia exata de Ridge() do NightSky.shader: altura da serra (seno da elevação).</summary>
        public static float Ridge(float az)
        {
            float r = 0.032f + 0.012f * Mathf.Sin(3 * az + 0.8f) + 0.008f * Mathf.Sin(7 * az + 2.1f) + 0.005f * Mathf.Sin(13 * az + 4.4f)
                    + 0.0025f * Mathf.Sin(29 * az + 1.3f) + 0.0012f * Mathf.Sin(61 * az + 2.9f);
            float e = WrapPi(az - 1.31f); r += 0.04f * Mathf.Exp(-e * e / 0.3f);
            float f = WrapPi(az - 0.30f); r += 0.018f * Mathf.Exp(-f * f / 0.4f);
            return r;
        }

        /// <summary>Um ponto do mundo visto de 'eye' está abaixo da linha das serras?</summary>
        public static bool BehindRidge(Vector3 eye, Vector3 p, out float margin)
        {
            var d = (p - eye).normalized;
            float az = Mathf.Atan2(d.x, d.z);
            margin = d.y - Ridge(az);
            return margin < 0f;
        }

        public static Material Sky { get; private set; }
        static Material glowMat;
        public static Material GlowMat
        {
            get
            {
                if (glowMat == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/WorldGlow");
                    if (sh != null) glowMat = new Material(sh) { enableInstancing = true, hideFlags = HideFlags.DontSave };
                }
                return glowMat;
            }
        }

        public static void Apply()
        {
            // (a cena recarrega ao voltar ao menu principal: reaplica se o céu não for o nosso)
            if (!Enabled || (Applied && Sky != null && RenderSettings.skybox == Sky)) return;
            var sh = Resources.Load<Shader>("Shaders/NightSky");
            if (sh == null || !sh.isSupported) { Debug.LogWarning("[Noite] NightSky indisponível; fica o pôr do sol"); return; }
            Applied = true;

            Sky = new Material(sh) { hideFlags = HideFlags.DontSave };
            Sky.SetTexture("_Stars", Resources.Load<Texture2D>("VFX/Space/space_stars_dense"));
            var noise = Resources.Load<Texture2D>("VFX/noise_perlin");
            Sky.SetTexture("_Clouds", noise);
            Sky.SetTexture("_Noise", noise);
            Sky.SetVector("_MoonDir", MoonDir);
            Sky.SetFloat("_FendaAz", FendaAz);
            Sky.SetFloat("_FlashAz", ImpactAz);
            RenderSettings.skybox = Sky;

            // a luz direcional vira a lua (fria, baixa, a oés-sudoeste)
            var sun = RenderSettings.sun;
            if (sun == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                sun.name = "Lua";
                sun.color = new Color(0.62f, 0.68f, 0.9f);
                sun.intensity = 0.42f;
                sun.transform.rotation = Quaternion.LookRotation(-MoonDir);
                sun.shadowStrength = 0.6f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.125f, 0.135f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.095f, 0.095f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.042f, 0.055f);
            RenderSettings.fogColor = new Color(0.062f, 0.066f, 0.1f);
            RenderSettings.fogDensity = 0.0052f;
            RenderSettings.reflectionIntensity = 0.25f;

            // gradação: sombras azuladas, luzes (janelas, tochas) quentes, um pouco mais de vinheta
            RenderScaler.ShadowTint = new Color(0.93f, 0.96f, 1.06f);
            RenderScaler.HighTint = new Color(1.08f, 1.0f, 0.88f);
            RenderScaler.Contrast = 0.3f; RenderScaler.Saturation = 0.9f;
            RenderScaler.Vignette = 0.34f; RenderScaler.Exposure = 1.2f;

            // a Fenda antiga (quad do pôr do sol) sai: a nova é do céu
            var old = GameObject.Find("A Fenda (Ruptura)");
            if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = false;

            AddHalos();
            new GameObject("RupturaCeu").AddComponent<RuptureSky>();
        }

        /// <summary>Halos quentes nos lampiões e tochas (cartazes aditivos instanciados, sem luz real).</summary>
        static void AddHalos()
        {
            if (GlowMat == null) return;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var parent = new GameObject("Halos da noite").transform;
            int n = 0;
            var mpb = new MaterialPropertyBlock();
            void Halo(Vector3 p, float size, Color c)
            {
                var go = new GameObject("Halo");
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GlowMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
                go.AddComponent<Flicker>().baseColor = c;
                n++;
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "TorchFlame") Halo(t.position + Vector3.up * 0.05f, 2.6f, new Color(1f, 0.55f, 0.22f, 0.55f));
                else if (t.name.StartsWith("LampPost") && t.GetComponentInChildren<Renderer>() != null)
                {
                    var b = new Bounds(t.position, Vector3.zero);
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    Halo(new Vector3(b.center.x, b.max.y - 0.35f, b.center.z), 3.2f, new Color(1f, 0.68f, 0.32f, 0.5f));
                }
            }
            Debug.Log("[Noite] halos: " + n);
        }
    }

    /// <summary>Tremulação de chama num halo (cor por instância, sem quebrar o instancing).</summary>
    public class Flicker : MonoBehaviour
    {
        public Color baseColor;
        MaterialPropertyBlock mpb; Renderer r; float seed;
        void Awake() { r = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); seed = Random.value * 50f; }
        void Update()
        {
            float f = 0.82f + 0.18f * Mathf.PerlinNoise(Time.time * 7f + seed, seed);
            var c = baseColor; c.a *= f;
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// O céu da Ruptura em tempo de jogo: abertura da Fenda, pulso e clarão do impacto, levados ao
    /// material do céu. A cinemática anima os valores; depois da abertura a Fenda fica aberta.
    /// </summary>
    public class RuptureSky : MonoBehaviour
    {
        public static RuptureSky Instance { get; private set; }
        public float fendaOpen, fendaPulse, flash;
        float flashT = -100f;
        static readonly int IdOpen = Shader.PropertyToID("_FendaOpen"), IdPulse = Shader.PropertyToID("_FendaPulse"), IdFlash = Shader.PropertyToID("_Flash");

        void Awake() { Instance = this; }

        /// <summary>Clarão atrás da serra: sobe rápido, segura um instante e se apaga em ~1,6 s.</summary>
        public void Flash() => flashT = Time.time;

        void Update()
        {
            float a = Time.time - flashT;
            float f = a < 0f || a > 3f ? 0f : a < 0.08f ? a / 0.08f : a < 0.25f ? 1f : Mathf.Exp(-(a - 0.25f) / 0.55f);
            flash = Mathf.Max(f, 0f);
            var m = NightSetup.Sky;
            if (m == null) return;
            m.SetFloat(IdOpen, fendaOpen);
            m.SetFloat(IdPulse, Mathf.Clamp01(fendaPulse + 0.15f * Mathf.Sin(Time.time * 0.9f) * fendaOpen));
            m.SetFloat(IdFlash, flash);
        }
    }
}

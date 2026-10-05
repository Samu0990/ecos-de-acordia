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
        public static bool Enabled = !System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-eda-day");   // -eda-day: pôr do sol (medir A/B)
        public static bool Applied { get; private set; }

        // direções no céu (azimute 0 = norte/+z, +π/2 = leste/+x)
        public const float FendaAz = 0.30f;          // nor-nordeste, atrás da vila
        public const float FendaBaseEl = 0.10f;      // seno da elevação de onde os brilhos saem
        public const float ImpactAz = 1.31f;         // leste: serra onde o brilho cai
        public static readonly Vector3 MoonDir = Dir(-0.78f, 0.5f);   // noroeste, alta: contraluz nas montanhas do norte

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

        /// <summary>'preview' = prévia do editor (NightPreview): não mexe nas câmeras da cena.</summary>
        public static void Apply(bool preview = false)
        {
            // (a cena recarrega ao voltar ao menu principal: reaplica se o céu não for o nosso)
            if (!Enabled || (Applied && Sky != null && RenderSettings.skybox == Sky)) return;
            var sh = Resources.Load<Shader>("Shaders/NightSky");
            if (sh == null || !sh.isSupported) { Debug.LogWarning("[Noite] NightSky indisponível; fica o pôr do sol"); return; }
            Applied = true;

            Sky = new Material(sh) { hideFlags = HideFlags.DontSave };
            Sky.SetTexture("_Stars", Resources.Load<Texture2D>("VFX/Space/space_stars_dense"));
            var noise = Resources.Load<Texture2D>("VFX/noise_perlin");
            Sky.SetTexture("_Clouds", Resources.Load<Texture2D>("VFX/noise_night"));
            Sky.SetTexture("_Noise", noise);
            Sky.SetTexture("_Space", Resources.Load<Texture2D>("VFX/Space/space_purple_stars"));
            Sky.SetVector("_MoonDir", MoonDir);
            Sky.SetFloat("_FendaAz", FendaAz);
            RenderSettings.skybox = Sky;

            // a luz direcional vira a lua (fria, a noroeste: recorta a vila e as serras do norte)
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
            RenderScaler.HighTint = new Color(1.04f, 1.0f, 0.94f);
            RenderScaler.Contrast = 0.3f; RenderScaler.Saturation = 0.9f;
            RenderScaler.Vignette = 0.34f; RenderScaler.Exposure = 1.2f;

            // a Fenda antiga (quad do pôr do sol) sai: a nova é do céu
            var old = GameObject.Find("A Fenda (Ruptura)");
            if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = false;

            AddHalos();
            VillageLights.Build();
            Cathedral.Build();   // a silhueta gótica do storyboard atrás da vila
            SetGlobals();
            FarLands.Build();
            new GameObject("RupturaCeu").AddComponent<RuptureSky>();
            if (preview) return;
            NightCrops();
            // a câmera de jogo enxerga a paisagem distante (o Cinemachine aplica a lente todo quadro)
            var fl = Object.FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (fl != null) fl.m_Lens.FarClipPlane = FarClip;
            if (Camera.main != null) Camera.main.farClipPlane = FarClip;
        }

        /// <summary>
        /// À noite o capim e o trigo do terreno não podem brilhar amarelos como no pôr do sol: cores
        /// das plantas mais frias e escuras (só em tempo de jogo — no editor isso gravaria no asset).
        /// </summary>
        static void NightCrops()
        {
            if (!Application.isPlaying) return;
            var terr = Terrain.activeTerrain;
            if (terr == null) return;
            var td = terr.terrainData;
            if (td == nightCrops) return;   // a cena recarrega mas o asset na memória é o mesmo: não escurece duas vezes
            nightCrops = td;
            var protos = td.detailPrototypes;
            foreach (var p in protos)
            {
                p.healthyColor = Night(p.healthyColor);
                p.dryColor = Night(p.dryColor);
            }
            td.detailPrototypes = protos;
            td.wavingGrassTint = Night(td.wavingGrassTint);
        }

        static TerrainData nightCrops;

        static Color Night(Color c)
        {
            float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            var d = Color.Lerp(new Color(l, l, l), c, 0.55f);       // menos saturado
            return new Color(d.r * 0.62f, d.g * 0.66f, d.b * 0.78f, c.a);   // mais escuro e frio
        }

        /// <summary>Desfaz os objetos criados (prévia do editor). As configurações de luz quem restaura é quem chamou.</summary>
        public static void Teardown()
        {
            foreach (var n in new[] { "Halos da noite", "RupturaCeu", "Luzes da vila", "Catedral dos Sinos" })
            {
                var g = GameObject.Find(n);
                if (g != null) Object.DestroyImmediate(g);
            }
            FarLands.Destroy();
            var old = GameObject.Find("A Fenda (Ruptura)");
            if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = true;
            Applied = false;
        }

        /// <summary>Alcance das câmeras à noite: a paisagem vai até ~11 km.</summary>
        public const float FarClip = 12500f;

        // cores do céu perto do horizonte (iguais às do NightSky.shader) — a névoa da paisagem usa as mesmas
        public static readonly Color SkyHorizon = new Color(0.085f, 0.085f, 0.135f), SkyMid = new Color(0.028f, 0.033f, 0.065f);
        public static readonly Color MoonLight = new Color(0.62f, 0.68f, 0.9f) * 0.42f;
        public static readonly Color FendaGlow = new Color(0.55f, 0.32f, 1.0f);

        /// <summary>Valores globais dos shaders da noite (NightCommon.cginc).</summary>
        public static void SetGlobals()
        {
            Shader.SetGlobalVector("_NightMoonDir", MoonDir);
            Shader.SetGlobalColor("_NightMoonCol", MoonLight * 1.25f);
            Shader.SetGlobalColor("_NightAmbSky", RenderSettings.ambientSkyColor);
            Shader.SetGlobalColor("_NightAmbGround", RenderSettings.ambientGroundColor);
            Shader.SetGlobalColor("_NightSkyHorizon", SkyHorizon);
            Shader.SetGlobalColor("_NightSkyMid", SkyMid);
            Shader.SetGlobalColor("_NightHaze", new Color(0.105f, 0.112f, 0.15f));
            Shader.SetGlobalVector("_NightFogParams", new Vector4(1.7e-4f, 1f / 750f, 0f, 0.93f));
            Shader.SetGlobalVector("_NightMist", new Vector4(7e-4f, 1f / 38f, -30f, 0f));
            Shader.SetGlobalVector("_FendaDirW", Dir(FendaAz, 0.12f));
            Shader.SetGlobalColor("_FendaLight", Color.black);
            Shader.SetGlobalColor("_ImpactLight", Color.black);
            Shader.SetGlobalVector("_Shock", Vector4.zero);
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
            // poças de luz no chão (quad deitado, aditivo) debaixo das tochas e lampiões
            var poolSh = Resources.Load<Shader>("Shaders/LightPool");
            Material poolMat = null;
            if (poolSh != null && poolSh.isSupported) { poolMat = new Material(poolSh) { enableInstancing = true, hideFlags = HideFlags.DontSave }; poolMat.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_night")); }
            int pools = 0;
            void Pool(Vector3 from, float size, Color c)
            {
                if (poolMat == null) return;
                if (!Physics.Raycast(from, Vector3.down, out var hit, 8f, ~((1 << 10) | (1 << 8) | (1 << 2)), QueryTriggerInteraction.Ignore)) return;
                var go = new GameObject("Poca de luz");
                go.transform.SetParent(parent, false);
                go.transform.position = hit.point + hit.normal * 0.02f;
                go.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, 0);
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = poolMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
                pools++;
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "TorchFlame") { Halo(t.position + Vector3.up * 0.05f, 2.6f, new Color(1f, 0.55f, 0.22f, 0.55f)); Pool(t.position, 5.5f, new Color(1f, 0.5f, 0.2f, 0.32f)); }
                else if (t.name.StartsWith("LampPost") && t.GetComponentInChildren<Renderer>() != null)
                {
                    var b = new Bounds(t.position, Vector3.zero);
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    Halo(new Vector3(b.center.x, b.max.y - 0.35f, b.center.z), 3.2f, new Color(1f, 0.68f, 0.32f, 0.5f));
                    Pool(new Vector3(b.center.x, b.max.y - 0.5f, b.center.z), 7f, new Color(1f, 0.6f, 0.28f, 0.35f));
                }
            }
            Debug.Log("[Noite] halos: " + n + ", poças de luz: " + pools);
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
            // com o Contracanto a chama "bate" como duas notas desafinadas (batimento), fora do ritmo
            float corr = OpeningSound.Instance != null ? OpeningSound.Instance.CurrentCorruption : 0f;
            if (corr > 0.01f)
            {
                float tt = Time.time;
                float beat = 0.5f + 0.5f * Mathf.Sin(tt * 6.2832f * (1.1f + seed * 0.013f)) * Mathf.Sin(tt * 6.2832f * 0.31f + seed);
                f *= 1f - corr * 0.45f * beat;
            }
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
        /// <summary>Contração antes dos sete (0..1), lampejo de saída (decai sozinho), fase da onda de pulso.</summary>
        public float inhale, burst;
        float waveT = -100f;
        float flashT = -100f;
        static readonly int IdOpen = Shader.PropertyToID("_FendaOpen"), IdPulse = Shader.PropertyToID("_FendaPulse"), IdFlash = Shader.PropertyToID("_Flash");
        static readonly int IdInhale = Shader.PropertyToID("_FendaInhale"), IdBurst = Shader.PropertyToID("_FendaBurst"), IdWave = Shader.PropertyToID("_FendaWave");
        static readonly int IdFendaLight = Shader.PropertyToID("_FendaLight");

        void Awake() { Instance = this; }

        /// <summary>Clarão atrás da serra (versão antiga, sem o ImpactFX): sobe rápido e se apaga em ~1,6 s.</summary>
        public void Flash() => flashT = Time.time;
        /// <summary>Lampejo no rasgo (cada brilho que sai, o estilhaço).</summary>
        public void Burst(float amount = 1f) => burst = Mathf.Max(burst, amount);
        /// <summary>Uma onda de pulso sai da Fenda e atravessa o céu (~3,5 s).</summary>
        public void Wave() => waveT = Time.time;

        void Update()
        {
            float a = Time.time - flashT;
            float f = a < 0f || a > 3f ? 0f : a < 0.08f ? a / 0.08f : a < 0.25f ? 1f : Mathf.Exp(-(a - 0.25f) / 0.55f);
            if (ImpactFX.Instance == null) flash = Mathf.Max(f, 0f);
            burst = Mathf.MoveTowards(burst, 0f, Time.deltaTime * 3.5f);
            float wv = (Time.time - waveT) / 3.5f;
            var m = NightSetup.Sky;
            if (m == null) return;
            m.SetFloat(IdOpen, fendaOpen);
            float pulse = Mathf.Clamp01(fendaPulse + 0.15f * Mathf.Sin(Time.time * 0.9f) * fendaOpen);
            m.SetFloat(IdPulse, pulse);
            m.SetFloat(IdFlash, flash);
            m.SetFloat(IdInhale, inhale);
            m.SetFloat(IdBurst, burst);
            m.SetFloat(IdWave, wv > 0f && wv < 1f ? wv : 0f);
            // a luz da Fenda na paisagem e na névoa acompanha a abertura (e respira com o pulso)
            float glow = fendaOpen * (0.85f + 0.15f * pulse) * (1f + 0.8f * burst);
            Shader.SetGlobalColor(IdFendaLight, NightSetup.FendaGlow * glow * 0.55f);
        }
    }
}

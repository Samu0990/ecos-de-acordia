using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Os cacos da realidade em volta da Fenda (storyboard: rochas escuras flutuando num losango em volta
    /// do feixe, com a borda acesa pela luz de dentro). ~200 cacos (malhas do Blender, ArtSource/Fenda) a
    /// ~12 km da câmera, atrás da paisagem como o rasgo do céu; o grupo acompanha a câmera (é "céu": não
    /// tem paralaxe). Luz falsa no shader (Hidden/Aren/FendaShard).
    /// Animação: <see cref="Shatter"/> — quando o céu estilhaça, os cacos NASCEM do rasgo como luz, são
    /// arremessados para fora girando e freiam até uma deriva lenta (explosão congelada), esfriando de
    /// luz para pedra (as faces de fratura em brasa por último). Depois respiram com a Fenda: a inspiração
    /// antes dos sete os puxa para dentro, cada lampejo os empurra para fora.
    /// </summary>
    public class FendaShards : MonoBehaviour
    {
        public static FendaShards Instance { get; private set; }

        const float R = 12300f;         // distância da câmera (a paisagem vai até ~11,2 km; o far clip da noite é 14 km)
        const float VC = 0.14f;         // centro do rasgo (seno da elevação), igual ao NightSky
        const float YB = -0.012f, YT = 0.456f;   // pontas do rasgo aberto

        class Shard
        {
            public Transform t; public MeshRenderer r;
            public float u, y, depth, size;      // casa (ângulo horizontal × cos el, seno da elevação, metros, raio em metros)
            public float u0, y0;                 // de onde nasce (no rasgo)
            public float delay, tau, seed;
            public Vector3 axis; public float spin0, spinDrift;
            public Quaternion rot;
            public float heat = -1f;
        }

        readonly List<Shard> shards = new List<Shard>();
        MaterialPropertyBlock mpb;
        Material mat;
        float shatterT = -1f;
        float kick, kickV, lastBurst;
        static readonly int IdHeat = Shader.PropertyToID("_Heat"), IdAxis = Shader.PropertyToID("_FendaAxis"), IdK = Shader.PropertyToID("_FendaShardK");

        public static FendaShards Create()
        {
            if (Instance != null) return Instance;
            var sh = Resources.Load<Shader>("Shaders/FendaShard");
            var meshes = Resources.LoadAll<Mesh>("Fenda/FendaShards");
            if (sh == null || !sh.isSupported || meshes == null || meshes.Length == 0) { Debug.LogWarning("[Fenda] cacos indisponíveis"); return null; }
            var go = new GameObject("Cacos da Fenda");
            var f = go.AddComponent<FendaShards>();
            Instance = f; if (f.mpb == null) f.mpb = new MaterialPropertyBlock();   // (no editor, prévia sem Play, o Awake não roda)
            f.mat = new Material(sh) { enableInstancing = true, hideFlags = HideFlags.DontSave };
            f.mat.SetTexture("_Cracks", Resources.Load<Texture2D>("VFX/noise_cracks"));
            f.Build(meshes);
            go.SetActive(false);   // aparece no estilhaço (ou já aberto, se a abertura foi pulada)
            return f;
        }

        void Awake() { Instance = this; mpb = new MaterialPropertyBlock(); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static float Prof(float y)
        {
            float tv = Mathf.Clamp01((y - YB) / (YT - YB));
            return Mathf.Pow(Mathf.Sin(Mathf.PI * tv), 0.65f);
        }

        void Build(Mesh[] meshes)
        {
            var rnd = new System.Random(1307);
            float Rn() => (float)rnd.NextDouble();
            // 3 tamanhos: grandes (poucos, longe do feixe), médios, e poeira de cacos (muitos, perto do feixe)
            void Add(int n, float sMin, float sMax, float uMin, float uMax, float uPow)
            {
                for (int i = 0; i < n; i++)
                {
                    var s = new Shard();
                    // altura: mais cacos no meio do rasgo; largura máxima do losango naquela altura
                    float y = Mathf.Lerp(YB + 0.02f, YT - 0.02f, Mathf.Lerp(Rn(), 0.5f + (Rn() - 0.5f) * 0.7f, 0.45f));
                    float w = 0.3f * Prof(y) + 0.02f;
                    float side = Rn() < 0.5f ? -1f : 1f;
                    float a = Mathf.Lerp(uMin, uMax, Mathf.Pow(Rn(), uPow));
                    s.u = side * Mathf.Max(0.006f, a * w);
                    s.y = y + (Rn() - 0.5f) * 0.02f;
                    s.depth = R + (Rn() - 0.5f) * 800f;
                    float out01 = Mathf.Clamp01(Mathf.Abs(s.u) / Mathf.Max(w, 1e-3f));
                    s.size = Mathf.Lerp(sMin, sMax, Rn()) * (0.7f + 0.6f * out01) * s.depth;   // raio angular → metros
                    s.u0 = side * (0.002f + Rn() * 0.006f);
                    s.y0 = Mathf.Lerp(s.y, VC, 0.15f + 0.2f * Rn());
                    s.delay = Rn() * 0.35f + out01 * 0.12f;
                    s.tau = 0.45f + 0.9f * Rn() + out01 * 0.5f;
                    s.seed = Rn() * 100f;
                    s.axis = new Vector3(Rn() - 0.5f, Rn() - 0.5f, Rn() - 0.5f).normalized;
                    s.spin0 = (120f + 260f * Rn()) * (Rn() < 0.5f ? -1f : 1f);
                    s.spinDrift = (1.5f + 5f * Rn()) * Mathf.Sign(s.spin0);
                    s.rot = Quaternion.Euler(Rn() * 360f, Rn() * 360f, Rn() * 360f);
                    var go = new GameObject("Caco");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[rnd.Next(meshes.Length)];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    mr.allowOcclusionWhenDynamic = false;
                    s.t = go.transform; s.r = mr;
                    shards.Add(s);
                }
            }
            Add(22, 0.02f, 0.034f, 0.4f, 1.05f, 0.7f);     // grandes
            Add(70, 0.009f, 0.018f, 0.1f, 1.1f, 1.1f);    // médios
            Add(180, 0.0025f, 0.006f, 0.04f, 1.2f, 1.3f);  // estilhaços pequenos (mais perto do feixe)
            Debug.Log($"[Fenda] {shards.Count} cacos ({meshes.Length} formas)");
        }

        /// <summary>O céu estilhaça: os cacos nascem do rasgo e são arremessados para fora.</summary>
        public void Shatter()
        {
            shatterT = Time.time;
            gameObject.SetActive(true);
            foreach (var s in shards) s.heat = -1f;
        }

        /// <summary>Estado final (abertura pulada / gameplay): todos na casa, frios.</summary>
        public void Settle()
        {
            gameObject.SetActive(true);
            shatterT = Time.time - 30f;
        }

        public bool Shattered => shatterT >= 0f;

        static Vector3 Dir(float u, float y)
        {
            float cosEl = Mathf.Sqrt(Mathf.Max(1e-4f, 1f - y * y));
            return NightSetup.Dir(NightSetup.FendaAz + u / cosEl, y);
        }

        static Camera ActiveCam()
        {
            var c = Camera.main;
            if (c != null && c.isActiveAndEnabled) return c;
            Camera best = null;
            foreach (var x in Camera.allCameras) if (x.targetTexture == null && (best == null || x.depth > best.depth)) best = x;
            return best;
        }

        void LateUpdate()
        {
            var cam = ActiveCam();
            Tick(shatterT >= 0f ? Time.time - shatterT : 0f, Time.deltaTime, cam != null ? cam.transform.position : transform.position);
        }

        /// <summary>Prévia do editor (NightPreview, sem Play mode): simula T segundos depois do estilhaço.</summary>
        public void PreviewAt(float T, Vector3 camPos)
        {
            gameObject.SetActive(true);
            foreach (var s in shards) s.heat = -1f;
            for (float t = 0f; t < T; t += 1f / 30f) Tick(t, 1f / 30f, camPos);
            Tick(T, 0f, camPos);
        }

        void Tick(float t, float dt, Vector3 camPos)
        {
            transform.position = camPos;   // "céu": sem paralaxe
            var sky = RuptureSky.Instance;
            float inhale = sky != null ? sky.inhale : 0f;
            float burst = sky != null ? sky.burst : 0f;
            float open = sky != null ? sky.fendaOpen : 1f;
            // cada lampejo novo (um dos sete saindo) empurra os cacos para fora; mola devolve
            if (burst > lastBurst + 0.3f) kickV += 0.11f * burst;
            lastBurst = burst;
            kickV += (-kick * 30f - kickV * 6f) * dt; kick += kickV * dt;
            float squeeze = 1f - 0.12f * inhale + kick;
            float drift = Mathf.Min(t, 25f) * 0.0009f;   // a explosão nunca para de todo: deriva lenta para fora

            foreach (var s in shards)
            {
                float ts = t - s.delay;
                if (ts <= 0f) { if (s.r.enabled) s.r.enabled = false; continue; }
                if (!s.r.enabled) s.r.enabled = true;
                float p = 1f - Mathf.Exp(-ts / s.tau);
                float u = Mathf.Lerp(s.u0, s.u, p) * squeeze;
                u += Mathf.Sign(s.u) * drift * (0.4f + Mathf.Abs(s.u) * 4f);
                float y = Mathf.Lerp(s.y0, s.y, p) + Mathf.Sin(t * 0.13f + s.seed) * 0.0012f;
                y = Mathf.Lerp(VC, y, squeeze);
                float depth = s.depth * (0.98f + 0.02f * p);
                // gira rápido ao ser arremessado, depois só deriva
                float w = s.spinDrift + s.spin0 * Mathf.Exp(-ts / 0.7f);
                s.rot = Quaternion.AngleAxis(w * dt, s.axis) * s.rot;
                float pop = Mathf.SmoothStep(0f, 1f, ts / 0.22f);
                s.t.localPosition = Dir(u, y) * depth;
                s.t.localRotation = s.rot;
                s.t.localScale = Vector3.one * (s.size * pop);
                // luz → pedra (um lampejo reacende as fraturas um pouco)
                float heat = Mathf.Exp(-ts / 0.85f) + burst * 0.25f;
                if (Mathf.Abs(heat - s.heat) > 0.004f)
                {
                    s.heat = heat;
                    mpb.SetFloat(IdHeat, Mathf.Clamp01(heat));
                    s.r.SetPropertyBlock(mpb);
                }
            }
            // eixo de luz (no mundo) e brilho geral para o shader
            Vector3 c0 = transform.position;
            Vector3 b = c0 + Dir(0f, YB) * R, top = c0 + Dir(0f, YT) * R;
            float pulse = sky != null ? sky.fendaPulse : 0f;
            mat.SetVector(IdAxis, new Vector4(b.x, b.y, b.z, top.y));
            mat.SetVector(IdK, new Vector4(Mathf.Clamp01(open) * (0.85f + 0.3f * pulse), burst, 1f, 0f));
        }
    }
}

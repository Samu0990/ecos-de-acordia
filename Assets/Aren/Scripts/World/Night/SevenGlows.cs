using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Os sete brilhos que saem da Fenda (o jogo não explica o que são). Cada um é uma "nota":
    /// cabeça em HDR (núcleo branco, coroa na cor dela, raios de difração) que TREME na frequência
    /// própria, rastro com cintilação e faíscas soltas. Voam em posições reais, a quilômetros
    /// (a câmera da abertura enxerga até 12,5 km), e somem atrás das montanhas de verdade (teste
    /// de profundidade). Saem num ritmo irregular — 1, 2, 3… 7, contáveis — cada saída com um
    /// lampejo no rasgo. Seis se perdem em direções diferentes; o dourado sobe por cima de Campanula
    /// e desce, pesado, no planalto do leste, a ~2,5 km: lá dispara o impacto (ImpactFX).
    /// </summary>
    public class SevenGlows : MonoBehaviour
    {
        public class Glow
        {
            public int index;
            public Transform root; public Renderer head; public TrailRenderer trail; public ParticleSystem sparks;
            public Color color; public float delay, dur, freq, size;
            public Vector3 dir0, dir1; public float r0, r1, hump;      // os que se perdem: direção/raio
            public Vector3 p0, p1, p2;                                   // o dourado: Bézier até o impacto
            public bool falls, born, gone; public float alpha, u;
            public Vector3 pos;
        }

        public static SevenGlows Instance { get; private set; }
        public readonly List<Glow> glows = new List<Glow>();
        public Glow Fallen { get; private set; }
        /// <summary>Uma nota saiu da Fenda (índice) — lampejo no rasgo e a assinatura dela.</summary>
        public System.Action<int> onBirth;
        /// <summary>O dourado tocou o chão (ponto do impacto).</summary>
        public System.Action<Vector3> onImpact;
        Vector3 eye;
        float t0 = -1f;
        MaterialPropertyBlock mpb;
        static Material trailMat, sparkMat;
        static readonly int IdColor = Shader.PropertyToID("_Color"), IdParams = Shader.PropertyToID("_Params");
        static readonly int IdNoteLight = Shader.PropertyToID("_NoteLight"), IdNoteLightPos = Shader.PropertyToID("_NoteLightPos");
        static readonly int IdGlowPos = Shader.PropertyToID("_GlowPosW"), IdGlowLight = Shader.PropertyToID("_GlowLight");

        public static readonly Color[] Colors =
        {
            new Color(1f, 0.74f, 0.32f), new Color(1f, 0.3f, 0.28f), new Color(0.36f, 0.5f, 1f), new Color(0.38f, 0.95f, 1f),
            new Color(0.42f, 1f, 0.52f), new Color(0.76f, 0.42f, 1f), new Color(0.95f, 0.96f, 1f),
        };

        /// <summary>Ordem em que saem (o dourado é o segundo) e o ritmo irregular das saídas (s).</summary>
        static readonly int[] Order = { 6, 0, 2, 5, 3, 1, 4 };
        static readonly float[] BirthAt = { 0f, 0.36f, 0.62f, 1.02f, 1.22f, 1.63f, 1.95f };

        /// <summary>Distância da Fenda de onde eles "saem" (na frente dela; ela é infinita, no céu).</summary>
        public const float Origin = 6800f;
        /// <summary>Seno da elevação do centro do rasgo (igual ao VC do NightSky.shader).</summary>
        public const float FendaCenterEl = 0.14f;

        /// <summary>Solta os sete a partir da Fenda. 'eye' = de onde a cena é vista (o Aren).</summary>
        public static SevenGlows Launch(Vector3 eye)
        {
            var go = new GameObject("Os sete brilhos");
            var s = go.AddComponent<SevenGlows>();
            Instance = s;
            s.eye = eye; s.t0 = Time.time;
            s.mpb = new MaterialPropertyBlock();
            float a = NightSetup.FendaAz;
            Vector3 D(float az, float el) => NightSetup.Dir(az, el);
            var origin = D(a, FendaCenterEl);
            // (índice, direção final, raio final, corcova de elevação, duração, frequência do tremor)
            s.AddLost(1, origin, D(a - 1.05f, 0.03f), 9800f, 0.10f, 6.5f, 2.4f);    // vermelho: oeste, some atrás do maciço
            s.AddLost(2, origin, D(a + 0.95f, 0.025f), 9800f, 0.08f, 6.8f, 1.2f);   // azul: nordeste, atrás da serra
            s.AddLost(3, origin, D(a - 0.42f, 0.78f), 9000f, 0.05f, 6.0f, 1.7f);    // ciano: alto, à esquerda
            s.AddLost(4, origin, D(a + 0.38f, 0.72f), 9000f, 0.05f, 6.2f, 3.1f);    // verde: alto, à direita
            s.AddLost(5, origin, D(a - 1.75f, 0.02f), 10500f, 0.16f, 7.4f, 3.8f);   // violeta: longe, a oeste
            s.AddLost(6, origin, D(a + 0.08f, 0.96f), 9500f, 0.0f, 5.6f, 4.5f);     // branco: sobe reto
            // o dourado: sobe por cima de Campanula (passa a ~1,4 km, alto) e desce no planalto do leste
            var p0 = eye + origin * Origin;
            var p2 = FarLands.Root != null ? FarLands.ImpactPoint : eye + D(NightSetup.ImpactAz, 0.06f) * 2500f;
            var p1 = eye + new Vector3(650f, 2300f, 1300f);
            s.AddFaller(0, p0, p1, p2, 12.4f, 0.9f);
            for (int i = 0; i < 7; i++) s.glows[i].delay = BirthAt[System.Array.IndexOf(Order, s.glows[i].index)];
            return s;
        }

        void AddLost(int i, Vector3 d0, Vector3 d1, float r1, float hump, float dur, float freq)
        {
            var g = Make(i, freq, 70f);
            g.dir0 = d0; g.dir1 = d1; g.r0 = Origin; g.r1 = r1; g.hump = hump; g.dur = dur;
        }

        void AddFaller(int i, Vector3 p0, Vector3 p1, Vector3 p2, float dur, float freq)
        {
            var g = Make(i, freq, 95f);
            g.p0 = p0; g.p1 = p1; g.p2 = p2; g.dur = dur; g.falls = true;
            Fallen = g;
            g.trail.time = 3.2f;
        }

        Glow Make(int i, float freq, float size)
        {
            var g = new Glow { index = i, color = Colors[i], freq = freq, size = size };
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            g.root = new GameObject("Nota_" + i).transform;
            g.root.SetParent(transform, false);
            var head = new GameObject("Cabeca");
            head.transform.SetParent(g.root, false);
            head.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = head.AddComponent<MeshRenderer>();
            mr.sharedMaterial = NoteMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            g.head = mr;
            // rastro
            g.trail = g.root.gameObject.AddComponent<TrailRenderer>();
            g.trail.time = 2.0f;
            g.trail.widthCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.3f, 0.55f), new Keyframe(1, 0f));
            g.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.trail.receiveShadows = false;
            g.trail.numCapVertices = 2;
            if (trailMat == null) { var ts = Resources.Load<Shader>("Shaders/WorldTrail"); if (ts != null) trailMat = new Material(ts) { hideFlags = HideFlags.DontSave }; if (trailMat != null) trailMat.SetFloat("_Boost", 2.2f); }
            g.trail.sharedMaterial = trailMat;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.Lerp(g.color, Color.white, 0.55f), 0), new GradientColorKey(g.color, 0.25f), new GradientColorKey(g.color * 0.8f, 1) },
                         new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.55f, 0.3f), new GradientAlphaKey(0f, 1) });
            g.trail.colorGradient = grad;
            g.trail.emitting = false;
            // faíscas soltas pelo caminho
            g.sparks = MakeSparks(g.root, g.color);
            glows.Add(g);
            return g;
        }

        static Material noteMat;
        public static Material NoteMat
        {
            get
            {
                if (noteMat == null) { var sh = Resources.Load<Shader>("Shaders/NoteGlow"); if (sh != null) noteMat = new Material(sh) { enableInstancing = true, hideFlags = HideFlags.DontSave }; }
                return noteMat;
            }
        }

        public static Material SparkMat
        {
            get
            {
                if (sparkMat == null) { var sh = Resources.Load<Shader>("Shaders/WorldParticle"); if (sh != null) sparkMat = new Material(sh) { hideFlags = HideFlags.DontSave }; }
                return sparkMat;
            }
        }

        static ParticleSystem MakeSparks(Transform parent, Color c)
        {
            var go = new GameObject("Faiscas");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 30f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 14f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(c, Color.white, 0.4f), c);
            main.gravityModifier = 0.5f;
            main.maxParticles = 200;
            main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 0.06f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0f, 1) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SparkMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, float u) { float v = 1f - u; return v * v * a + 2f * v * u * b + u * u * c; }

        void Place(Glow g, float u)
        {
            if (g.falls)
            {
                // sai rápido, desacelera no alto (por cima da vila) e cai acelerando, pesado
                float k = u < 0.48f ? 0.5f * (1f - Mathf.Pow(1f - u / 0.48f, 2.2f)) : 0.5f + 0.5f * Mathf.Pow((u - 0.48f) / 0.52f, 1.7f);
                g.pos = Bez(g.p0, g.p1, g.p2, k);
            }
            else
            {
                float k = 1f - Mathf.Pow(1f - u, 1.8f);     // ejetado e perdendo velocidade
                var d = Vector3.Slerp(g.dir0, g.dir1, k);
                d = (d + Vector3.up * g.hump * Mathf.Sin(Mathf.PI * k)).normalized;
                // um leve espiral (cada nota oscila em torno do próprio caminho)
                var side = Vector3.Cross(d, Vector3.up).normalized;
                d = (d + side * 0.012f * Mathf.Sin(u * 9f + g.index) * (1f - u)).normalized;
                g.pos = eye + d * Mathf.Lerp(g.r0, g.r1, k);
            }
            g.root.position = g.pos;
        }

        void Update()
        {
            if (t0 < 0f) return;
            float t = Time.time - t0;
            Vector3 camPos = eye;   // a câmera fica a poucos metros dele; os brilhos estão a quilômetros
            bool any = false;
            foreach (var g in glows)
            {
                if (g.gone) continue;
                any = true;
                float u = (t - g.delay) / g.dur;
                if (u < 0f) { g.head.enabled = false; continue; }
                if (!g.born)
                {
                    g.born = true; g.head.enabled = true;
                    Place(g, 0f);
                    g.trail.Clear(); g.trail.emitting = true;
                    g.sparks.Play();
                    onBirth?.Invoke(g.index);
                }
                g.u = u;
                Place(g, Mathf.Min(u, 1f));
                float dist = Vector3.Distance(camPos, g.pos);
                // tamanho: nunca menor que ~0,3° na tela; nasce com um lampejo; os perdidos se apagam no fim
                float ang = Mathf.Max(g.size, dist * 0.012f);
                float birth = 1f + 2.2f * Mathf.Exp(-(t - g.delay) * 5f);
                float a = Mathf.Clamp01((t - g.delay) * 8f);
                if (!g.falls) a *= 1f - Mathf.Clamp01((u - 0.72f) / 0.28f);
                float tremor = 1f + 0.12f * Mathf.Sin((t - g.delay) * g.freq * 6.2832f) + 0.05f * Mathf.Sin((t - g.delay) * g.freq * 15.1f);
                g.alpha = a;
                g.root.localScale = Vector3.one * ang * 2.4f * birth * tremor;
                var c = g.color; c.a = a * (g.falls ? 3.0f : 2.6f) * birth;
                mpb.SetColor(IdColor, c);
                mpb.SetVector(IdParams, new Vector4(1f, 0.75f, t * 0.15f + g.index, 0f));
                g.head.SetPropertyBlock(mpb);
                g.trail.widthMultiplier = dist * 0.0045f * (g.falls ? 1.4f : 1f);
                g.trail.minVertexDistance = Mathf.Max(2f, dist * 0.004f);
                var main = g.sparks.main;
                main.startSizeMultiplier = Mathf.Max(4f, dist * 0.0018f);
                if (g.falls)
                {
                    // a luz da nota: acende as nuvens quando passa e o chão quando desce
                    var lc = g.color * (1.4f * a);
                    Shader.SetGlobalVector(IdNoteLightPos, g.pos);
                    float h = g.pos.y - g.p2.y;
                    Shader.SetGlobalColor(IdNoteLight, lc * Mathf.Clamp01(1.4f - h / 1800f));
                    if (NightSetup.Sky != null) { NightSetup.Sky.SetVector(IdGlowPos, g.pos); NightSetup.Sky.SetColor(IdGlowLight, lc * 0.5f); }
                    if (u >= 1f)
                    {
                        g.gone = true; g.trail.emitting = false; g.head.enabled = false; g.sparks.Stop();
                        Shader.SetGlobalColor(IdNoteLight, Color.black);
                        if (NightSetup.Sky != null) NightSetup.Sky.SetColor(IdGlowLight, Color.black);
                        onImpact?.Invoke(g.p2);
                    }
                }
                else if (u >= 1f) { g.gone = true; g.trail.emitting = false; g.head.enabled = false; g.sparks.Stop(); }
            }
            if (!any && t > 20f) Destroy(gameObject, 4f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalColor(IdNoteLight, Color.black);
            if (NightSetup.Sky != null) NightSetup.Sky.SetColor(IdGlowLight, Color.black);
        }
    }
}

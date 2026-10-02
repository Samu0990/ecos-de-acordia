using System.Collections.Generic;
using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Fachada de VFX (doc de remake §18). Tudo é gerado em código: malhas de arco,
    /// anéis procedurais, afterimages assados da malha do Aren e três sistemas de
    /// partícula globais (faíscas, glifos, poeira) com Emit manual — nada de instanciar
    /// prefab por golpe (o notebook alvo é fraco). Linguagem visual: ar comprimido,
    /// anéis de ressonância, listras de frequência.
    /// </summary>
    public static class ArenVFX
    {
        public static readonly Color FluteColor = new Color(0.55f, 0.95f, 1f);
        public static readonly Color GoldColor = new Color(1f, 0.76f, 0.34f);
        public static readonly Color EchoColor = new Color(0.72f, 0.45f, 1f);
        public static readonly Color CorruptColor = new Color(0.9f, 0.12f, 0.32f);
        public static readonly Color VoidColor = new Color(0.35f, 0.08f, 0.45f);

        /// <summary>Qualidade Alta: distorção de tela e luz de flash.</summary>
        public static bool HighQuality = true;
        /// <summary>Luzes de flash (só na qualidade Alta: cada uma é uma luz por pixel a mais).</summary>
        public static bool FlashLights = false;

        static VFXRunner R => VFXRunner.Instance;

        public static void Slash(Vector3 center, Quaternion facing, float roll, float radius, Color color, float duration)
            => R.SpawnSlash(center, facing, roll, radius, color, duration);

        public static void Impact(Vector3 point, Vector3 dir, Color color, float scale)
            => R.SpawnImpact(point, dir, color, scale);

        public static void Ring(Vector3 center, float r0, float r1, float duration, Color color, float width, bool ground)
            => R.SpawnRing(center, r0, r1, duration, color, width, ground);

        public static void Afterimage(GameObject root, Color color, float life, Vector3 offset, float startAlpha)
            => R.SpawnAfterimage(root, color, life, offset, startAlpha);

        public static void Distortion(Vector3 center, float radius, float duration, float strength)
        { if (HighQuality) R.SpawnDistortion(center, radius, duration, strength); }

        public static void DodgeBurst(Transform t, Vector3 dir) => R.DodgeBurst(t, dir);
        public static void PulseWave(Vector3 c, float radius) => R.PulseWave(c, radius);
        public static Transform CreateBladeVisual(Transform parent) => R.CreateBlade(parent);
        public static void DetachAndFade(Transform t, float time) => R.DetachAndFade(t, time);
        public static void BeginCharge(Transform t) => R.BeginCharge(t);
        public static void UpdateCharge(Transform t, float progress, int level) => R.UpdateCharge(t, progress, level);
        public static void ChargeLevel(Transform t, int level) => R.ChargeLevel(t, level);
        public static void EndCharge() => R.EndCharge();
        public static void ContracantoWave(Vector3 origin, Vector3 dir, float length, int level) => R.ContracantoWave(origin, dir, length, level);
        public static void Sparks(Vector3 p, Vector3 dir, Color c, int n, float speed, float spread = 50f) => R.EmitSparks(p, dir, c, n, speed, spread);
        public static void Glyphs(Vector3 p, Color c, int n, float speed = 1.5f) => R.EmitGlyphs(p, c, n, speed);
        public static void Dust(Vector3 p, Vector3 vel, Color c, int n, float size = 0.6f) => R.EmitDust(p, vel, c, n, size);
        public static void Flash(Vector3 p, Color c, float intensity, float range, float time) { if (FlashLights) R.FlashLight(p, c, intensity, range, time); }
        public static void CorruptionBurst(Vector3 p, float scale) => R.CorruptionBurst(p, scale);
        public static Material GetGhostMaterial() => R.ghostMat;
        /// <summary>Portal de nebulosa no chão (nascimento dos Ecos).</summary>
        public static void SpawnPortal(Vector3 groundPos, float radius, float duration) => R.SpawnPortal(groundPos, radius, duration);
        /// <summary>Almas subindo em direção à Fenda (morte de um possuído).</summary>
        public static void SoulMotes(Vector3 p, int n) => R.EmitMotes(p, n);
    }

    /// <summary>Runtime dos efeitos: pools, animação de propriedades e partículas.</summary>
    [DefaultExecutionOrder(500)]
    public class VFXRunner : MonoBehaviour
    {
        static VFXRunner instance;
        public static VFXRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ArenVFX");
                    instance = go.AddComponent<VFXRunner>();
                    instance.Init();
                }
                return instance;
            }
        }

        enum Kind { Slash, Ring, Flash, Distortion, Ghost, Sigil, Wave, Shell, Fade, Portal, Star }

        class FX
        {
            public GameObject go; public MeshRenderer mr; public MeshFilter mf;
            public Kind kind; public float t, dur;
            public Color color; public Vector4 a; public Transform follow; public Vector3 vel;
            public bool billboard; public bool active; public Mesh ownMesh;
            public Material[] origMats;
        }

        Shader sAdd, sSlash, sRing, sDist, sGhost, sSigil, sAlpha, sPortal;
        Material slashMat, ringMat, flashMat, distMat, sigilMat, shellMat, sparkMat, glyphMat, dustMat, portalMat, starMat, moteMat;
        Texture2D texPurpleStars, texStarsDense, texBlueStars, texStar;
        ParticleSystem motes, ambient;
        static readonly Vector3 RiftDir = new Vector3(0.78f, 0.36f, 0.5f).normalized;   // mesma do CampanulaBuilder
        public Material ghostMat;
        Mesh quad, arcMesh, bladeMesh, waveMesh, shellMesh;
        Texture2D texSoft, texStreak, texGlyphs, texSmoke, texNoise;
        readonly List<FX> pool = new List<FX>(64);
        readonly List<FX> active = new List<FX>(64);
        MaterialPropertyBlock mpb;
        ParticleSystem sparks, glyphs, dust;
        Light flashLight; float flashT, flashDur, flashI;
        FX sigil; Transform sigilFollow; float chargeEmitAcc;
        Camera cam;

        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdFade = Shader.PropertyToID("_Fade");
        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdRadius = Shader.PropertyToID("_Radius");
        static readonly int IdWidth = Shader.PropertyToID("_Width");
        static readonly int IdStrength = Shader.PropertyToID("_Strength");
        static readonly int IdInflate = Shader.PropertyToID("_Inflate");
        static readonly int IdCharge = Shader.PropertyToID("_Charge");
        static readonly int IdSpin = Shader.PropertyToID("_Spin");
        static readonly int IdUniform = Shader.PropertyToID("_Uniform");
        static readonly int IdTail = Shader.PropertyToID("_Tail");
        static readonly int IdCore = Shader.PropertyToID("_Core");

        void Init()
        {
            mpb = new MaterialPropertyBlock();
            sAdd = Resources.Load<Shader>("Shaders/ArenFXAdditive");
            sSlash = Resources.Load<Shader>("Shaders/ArenFXSlash");
            sRing = Resources.Load<Shader>("Shaders/ArenFXRing");
            sDist = Resources.Load<Shader>("Shaders/ArenFXDistortion");
            sGhost = Resources.Load<Shader>("Shaders/ArenFXGhost");
            sSigil = Resources.Load<Shader>("Shaders/ArenFXSigil");
            sAlpha = Resources.Load<Shader>("Shaders/ArenFXAlpha");
            texSoft = Resources.Load<Texture2D>("VFX/fx_soft");
            texStreak = Resources.Load<Texture2D>("VFX/fx_streak");
            texGlyphs = Resources.Load<Texture2D>("VFX/fx_glyphs");
            texSmoke = Resources.Load<Texture2D>("VFX/fx_smoke");
            texNoise = Resources.Load<Texture2D>("VFX/noise_perlin");
            // fundos espaciais (Screaming Brain Studios, CC0): o outro lado da Fenda
            sPortal = Resources.Load<Shader>("Shaders/ArenFXPortal");
            texPurpleStars = Resources.Load<Texture2D>("VFX/Space/space_purple_stars");
            texStarsDense = Resources.Load<Texture2D>("VFX/Space/space_stars_dense");
            texBlueStars = Resources.Load<Texture2D>("VFX/Space/space_blue_stars");
            texStar = MakeStarTexture(64);

            slashMat = new Material(sSlash); slashMat.SetTexture("_Noise", texNoise); slashMat.SetTexture("_Stars", texStarsDense);
            ringMat = new Material(sRing); ringMat.SetTexture("_Space", texBlueStars);
            flashMat = new Material(sAdd); flashMat.mainTexture = texSoft;
            distMat = new Material(sDist);
            ghostMat = new Material(sGhost); ghostMat.SetTexture("_Space", texStarsDense);
            portalMat = new Material(sPortal); portalMat.SetTexture("_Space", texPurpleStars);
            starMat = new Material(sAdd); starMat.mainTexture = texStar;
            moteMat = new Material(sAdd); moteMat.mainTexture = texSoft;
            sigilMat = new Material(sSigil);
            shellMat = new Material(sAdd);
            sparkMat = new Material(sAdd); sparkMat.mainTexture = texStreak;
            glyphMat = new Material(sAdd); glyphMat.mainTexture = texGlyphs;
            dustMat = new Material(sAlpha); dustMat.mainTexture = texSmoke;

            quad = MakeQuad();
            arcMesh = MakeArc(150f, 28, 0.5f, 1f);
            bladeMesh = MakeArc(120f, 20, 0.72f, 1f);
            waveMesh = MakeWall(110f, 24, 2.2f);
            shellMesh = MakeShell(32);

            sparks = MakePS("Sparks", sparkMat, ParticleSystemRenderMode.Stretch, 500, 0.6f);
            glyphs = MakePS("Glyphs", glyphMat, ParticleSystemRenderMode.Billboard, 200, -0.15f);
            var tsa = glyphs.textureSheetAnimation;
            tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            var noise = glyphs.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.8f; noise.quality = ParticleSystemNoiseQuality.Low;
            dust = MakePS("Dust", dustMat, ParticleSystemRenderMode.Billboard, 300, -0.05f);
            var sol = dust.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.6f));
            var vol = dust.limitVelocityOverLifetime; vol.enabled = true; vol.dampen = 0.12f; vol.limit = 0.5f;
            motes = MakePS("SoulMotes", moteMat, ParticleSystemRenderMode.Billboard, 300, -0.02f);
            var mn = motes.noise; mn.enabled = true; mn.strength = 0.5f; mn.frequency = 0.6f; mn.quality = ParticleSystemNoiseQuality.Low;
            ambient = MakeAmbient();

            var lg = new GameObject("FlashLight"); lg.transform.SetParent(transform);
            flashLight = lg.AddComponent<Light>(); flashLight.type = LightType.Point; flashLight.enabled = false;
            flashLight.shadows = LightShadows.None; flashLight.renderMode = LightRenderMode.ForcePixel;
        }

        ParticleSystem MakePS(string n, Material m, ParticleSystemRenderMode mode, int max, float gravity)
        {
            var go = new GameObject(n); go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity; main.startSpeed = 0; main.startLifetime = 1f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m; r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = 0.035f; r.lengthScale = 2f; }
            r.maxParticleSize = 0.6f;
            ps.Play();
            return ps;
        }

        /// <summary>Estrela de 4 pontas (brilho do impacto), gerada em código.</summary>
        static Texture2D MakeStarTexture(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "fx_star", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float rays = Mathf.Exp(-Mathf.Abs(u) * 26f) * (1f - Mathf.Abs(v)) + Mathf.Exp(-Mathf.Abs(v) * 26f) * (1f - Mathf.Abs(u));
                    float core = Mathf.Exp(-r * r * 30f);
                    float a = Mathf.Clamp01(rays * 0.9f + core) * Mathf.Clamp01(1f - r);
                    byte b = (byte)(a * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, b);
                }
            t.SetPixels32(px); t.Apply(true, true);
            return t;
        }

        /// <summary>
        /// Partículas de ambiente em volta da câmera: brasas e poeira dourada na vila ao pôr do
        /// sol, poeira roxa subindo para a Fenda no campo do leste. ~50 vivas, aditivas e
        /// pequenas (custo baixo no UHD 620).
        /// </summary>
        ParticleSystem MakeAmbient()
        {
            var ps = MakePS("Ambient", moteMat, ParticleSystemRenderMode.Billboard, 90, -0.01f);
            var main = ps.main;
            main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            var em = ps.emission; em.enabled = true; em.rateOverTime = 10f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(22f, 7f, 22f);
            var n = ps.noise; n.enabled = true; n.strength = 0.25f; n.frequency = 0.3f; n.quality = ParticleSystemNoiseQuality.Low;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            return ps;
        }

        float ambientVoid = -1f;
        void UpdateAmbient()
        {
            if (ambient == null || cam == null) return;
            Vector3 p = cam.transform.position + cam.transform.forward * 6f;
            ambient.transform.position = p;
            // no Campo da Fenda (leste da ponte) a poeira fica roxa e sobe
            float target = p.x > 45f ? 1f : 0f;
            if (Mathf.Abs(target - ambientVoid) > 0.01f)
            {
                ambientVoid = target;
                var main = ambient.main;
                main.startColor = target > 0.5f
                    ? new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.3f, 1f, 0.9f), new Color(0.4f, 0.15f, 0.8f, 0.7f))
                    : new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.38f, 0.75f), new Color(1f, 0.45f, 0.2f, 0.55f));
                main.gravityModifier = target > 0.5f ? -0.03f : -0.005f;
                var em = ambient.emission; em.rateOverTime = target > 0.5f ? 16f : 9f;
            }
        }

        // ------------------------------------------------------------ malhas

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "fx_quad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Arco no plano XZ à frente (+Z), de +X (cauda) a −X (cabeça).</summary>
        static Mesh MakeArc(float deg, int segs, float inner, float outer)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float th = Mathf.Deg2Rad * (deg * 0.5f - deg * u);
                Vector3 d = new Vector3(Mathf.Sin(th), 0, Mathf.Cos(th));
                // espessura afina nas pontas (corte, não faixa)
                float w = Mathf.Sin(u * Mathf.PI) * 0.75f + 0.25f;
                float rin = Mathf.Lerp(outer, inner, w);
                v.Add(d * rin); uv.Add(new Vector2(u, 0));
                v.Add(d * outer); uv.Add(new Vector2(u, 1));
                if (i < segs) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            var m = new Mesh { name = "fx_arc" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>Faixa curva vertical (frente de onda do Contracanto). UV.y = 1 no meio da altura.</summary>
        static Mesh MakeWall(float deg, int segs, float height)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            int rows = 4;
            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float th = Mathf.Deg2Rad * (deg * 0.5f - deg * u);
                Vector3 d = new Vector3(Mathf.Sin(th), 0, Mathf.Cos(th));
                for (int j = 0; j <= rows; j++)
                {
                    float h = j / (float)rows;
                    v.Add(d + Vector3.up * (h - 0.4f) * height);
                    uv.Add(new Vector2(u, 1f - Mathf.Abs(h * 2f - 1f)));
                }
                if (i < segs)
                    for (int j = 0; j < rows; j++)
                    {
                        int a = i * (rows + 1) + j, b = a + rows + 1;
                        tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
            }
            var m = new Mesh { name = "fx_wall" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>Cilindro aberto raio 1, altura 1, alfa do vértice some para cima.</summary>
        static Mesh MakeShell(int segs)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float th = i / (float)segs * Mathf.PI * 2;
                Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                v.Add(d); c.Add(new Color(1, 1, 1, 1)); uv.Add(new Vector2(0.5f, 0.5f));
                v.Add(d + Vector3.up); c.Add(new Color(1, 1, 1, 0)); uv.Add(new Vector2(0.5f, 0.5f));
                if (i < segs) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            var m = new Mesh { name = "fx_shell" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------ pool

        FX Get(Kind kind, Mesh mesh, Material mat)
        {
            FX fx = null;
            for (int i = 0; i < pool.Count; i++) if (!pool[i].active && pool[i].ownMesh == null) { fx = pool[i]; break; }
            if (fx == null)
            {
                fx = new FX();
                fx.go = new GameObject("fx");
                fx.go.transform.SetParent(transform, false);
                fx.mf = fx.go.AddComponent<MeshFilter>();
                fx.mr = fx.go.AddComponent<MeshRenderer>();
                fx.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fx.mr.receiveShadows = false;
                fx.mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                fx.mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                pool.Add(fx);
            }
            fx.kind = kind; fx.t = 0; fx.active = true; fx.follow = null; fx.billboard = false; fx.vel = Vector3.zero;
            fx.mf.sharedMesh = mesh;
            fx.mr.sharedMaterial = mat;
            fx.go.SetActive(true);
            fx.go.transform.localScale = Vector3.one;
            active.Add(fx);
            return fx;
        }

        void Release(FX fx)
        {
            fx.active = false;
            fx.go.SetActive(false);
            if (fx.kind == Kind.Fade && fx.origMats != null) { fx.origMats = null; }
        }

        // ------------------------------------------------------------ efeitos

        public void SpawnSlash(Vector3 center, Quaternion facing, float roll, float radius, Color color, float duration)
        {
            var fx = Get(Kind.Slash, arcMesh, slashMat);
            fx.go.transform.SetPositionAndRotation(center, facing * Quaternion.Euler(0, 0, roll));
            fx.go.transform.localScale = Vector3.one * radius;
            fx.dur = duration; fx.color = color; fx.a = new Vector4(radius, 0, 0, 0);
            // harmônico interno, menor e atrasado (a "segunda voz" do corte)
            var h = Get(Kind.Slash, arcMesh, slashMat);
            h.go.transform.SetPositionAndRotation(center, facing * Quaternion.Euler(0, 0, roll + 8f));
            h.go.transform.localScale = Vector3.one * radius * 0.78f;
            h.dur = duration * 1.15f; h.color = color * 0.7f; h.t = -0.03f; h.a = new Vector4(radius * 0.78f, 1, 0, 0);
            EmitSparks(center + facing * Vector3.forward * radius * 0.8f, facing * Vector3.forward, color, 3, 5f, 70f);
        }

        public void SpawnImpact(Vector3 point, Vector3 dir, Color color, float scale)
        {
            var f = Get(Kind.Flash, quad, flashMat);
            f.go.transform.position = point; f.billboard = true; f.dur = 0.09f; f.color = color * 1.1f; f.a = new Vector4(0.55f * scale, 0, 0, 0);
            var r = Get(Kind.Ring, quad, ringMat);
            r.go.transform.position = point; r.billboard = true; r.dur = 0.18f; r.color = color * 1.2f;
            r.a = new Vector4(0.1f * scale, 0.6f * scale, 0.07f, 0);
            EmitSparks(point, dir, color, Mathf.RoundToInt(9 * scale), 8f * scale, 55f);
            EmitGlyphs(point + Vector3.up * 0.1f, color, scale > 1.2f ? 3 : 1, 1.6f);
            // brilho em estrela de 4 pontas (lê o contato mesmo em luta cheia)
            var st = Get(Kind.Star, quad, starMat);
            st.go.transform.position = point - (cam != null ? cam.transform.forward * 0.2f : Vector3.zero);
            st.dur = 0.14f + 0.04f * scale; st.color = Color.Lerp(color, Color.white, 0.55f) * 1.6f;
            st.a = new Vector4(0.9f * scale, Random.Range(-25f, 25f), 0, 0);
            if (scale > 1.2f)
            {
                // golpe pesado: onda no chão e poeira
                Vector3 g = new Vector3(point.x, point.y - 1.05f, point.z);
                SpawnRing(g + Vector3.up * 0.06f, 0.2f, 1.6f * scale, 0.3f, color * 0.8f, 0.06f, true);
                EmitDust(g + Vector3.up * 0.15f, Vector3.up * 0.6f, new Color(0.55f, 0.5f, 0.45f, 0.4f), 4, 0.7f);
            }
        }

        public void SpawnPortal(Vector3 groundPos, float radius, float duration)
        {
            var p = Get(Kind.Portal, quad, portalMat);
            p.go.transform.SetPositionAndRotation(groundPos + Vector3.up * 0.04f, Quaternion.Euler(90, Random.Range(0f, 360f), 0));
            p.dur = duration; p.a = new Vector4(radius, 0, 0, 0);
            EmitMotes(groundPos + Vector3.up * 0.2f, 10);
        }

        public void EmitMotes(Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 d = (RiftDir * 0.6f + Vector3.up * 0.8f + Random.insideUnitSphere * 0.5f).normalized;
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + Random.insideUnitSphere * 0.45f, velocity = d * Random.Range(1.2f, 3.2f),
                    startLifetime = Random.Range(1.2f, 2.4f), startSize = Random.Range(0.06f, 0.14f),
                    startColor = Color.Lerp(new Color(1f, 0.35f, 0.75f, 1f), new Color(0.6f, 0.4f, 1f, 1f), Random.value)
                };
                motes.Emit(ep, 1);
            }
        }

        public void SpawnRing(Vector3 center, float r0, float r1, float duration, Color color, float width, bool ground)
        {
            var r = Get(Kind.Ring, quad, ringMat);
            r.go.transform.position = center;
            if (ground) r.go.transform.rotation = Quaternion.Euler(90, 0, 0); else r.billboard = true;
            r.dur = duration; r.color = color * 1.5f; r.a = new Vector4(r0, r1, width, 0);
        }

        public void SpawnDistortion(Vector3 center, float radius, float duration, float strength)
        {
            var d = Get(Kind.Distortion, quad, distMat);
            d.go.transform.position = center; d.billboard = true; d.dur = duration;
            d.a = new Vector4(radius, strength, 0, 0);
        }

        public void SpawnAfterimage(GameObject root, Color color, float life, Vector3 offset, float startAlpha)
        {
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                var fx = GetGhost();
                smr.BakeMesh(fx.ownMesh, true);
                fx.go.transform.SetPositionAndRotation(smr.transform.position + offset, smr.transform.rotation);
                fx.go.transform.localScale = Vector3.one;
                fx.dur = life; fx.color = color; fx.a = new Vector4(startAlpha, 0, 0, 0);
            }
            // flauta (malha rígida presa no osso)
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.name.StartsWith("Aren_Flute")) continue;
                var fx = Get(Kind.Ghost, mf.sharedMesh, ghostMat);
                fx.go.transform.SetPositionAndRotation(mf.transform.position + offset, mf.transform.rotation);
                fx.go.transform.localScale = mf.transform.lossyScale;
                fx.dur = life; fx.color = color; fx.a = new Vector4(startAlpha, 0, 0, 0);
            }
        }

        readonly List<FX> ghostPool = new List<FX>(12);
        FX GetGhost()
        {
            FX fx = null;
            foreach (var g in ghostPool) if (!g.active) { fx = g; break; }
            if (fx == null)
            {
                if (ghostPool.Count >= 12)
                {
                    // reaproveita o mais velho (limite de memória/CPU do BakeMesh)
                    fx = ghostPool[0]; foreach (var g in ghostPool) if (g.t > fx.t) fx = g;
                    active.Remove(fx);
                }
                else
                {
                    fx = new FX();
                    fx.go = new GameObject("ghost"); fx.go.transform.SetParent(transform, false);
                    fx.mf = fx.go.AddComponent<MeshFilter>(); fx.mr = fx.go.AddComponent<MeshRenderer>();
                    fx.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fx.mr.receiveShadows = false;
                    fx.ownMesh = new Mesh { name = "ghost_bake" };
                    fx.ownMesh.MarkDynamic();
                    fx.mf.sharedMesh = fx.ownMesh;
                    fx.mr.sharedMaterial = ghostMat;
                    ghostPool.Add(fx);
                }
            }
            fx.kind = Kind.Ghost; fx.t = 0; fx.active = true; fx.follow = null; fx.billboard = false;
            fx.go.SetActive(true);
            active.Add(fx);
            return fx;
        }

        public void DodgeBurst(Transform t, Vector3 dir)
        {
            StartCoroutine(DodgeRoutine(t, dir));
            EmitDust(t.position + Vector3.up * 0.1f, -dir * 1.5f, new Color(0.55f, 0.5f, 0.45f, 0.5f), 5, 0.5f);
        }

        System.Collections.IEnumerator DodgeRoutine(Transform t, Vector3 dir)
        {
            for (int i = 0; i < 3; i++)
            {
                SpawnAfterimage(t.gameObject, ArenVFX.FluteColor, 0.28f, Vector3.zero, 0.55f - i * 0.12f);
                EmitSparks(t.position + Vector3.up * (0.6f + i * 0.35f), -dir, ArenVFX.FluteColor * 0.7f, 2, 6f, 10f);
                yield return new WaitForSeconds(0.07f);
            }
        }

        public void PulseWave(Vector3 c, float radius)
        {
            SpawnRing(c + Vector3.up * 0.08f, 0.4f, radius, 0.42f, ArenVFX.FluteColor, 0.14f, true);
            StartCoroutine(Delayed(0.07f, () => SpawnRing(c + Vector3.up * 0.1f, 0.3f, radius * 0.82f, 0.45f, Color.white * 0.8f, 0.05f, true)));
            StartCoroutine(Delayed(0.05f, () => SpawnRing(c + Vector3.up * 0.06f, radius * 0.85f, radius * 0.95f, 1.4f, ArenVFX.FluteColor * 0.45f, 0.03f, true)));
            var s = Get(Kind.Shell, shellMesh, shellMat);
            s.go.transform.position = c; s.dur = 0.38f; s.color = ArenVFX.FluteColor * 0.9f; s.a = new Vector4(0.5f, radius, 1.6f, 0);
            if (ArenVFX.HighQuality) SpawnDistortion(c + Vector3.up * 1f, radius * 1.1f, 0.42f, 0.05f);
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                EmitDust(c + d * 0.6f + Vector3.up * 0.15f, d * 7f + Vector3.up * 0.4f, new Color(0.6f, 0.55f, 0.5f, 0.45f), 1, 0.8f);
            }
            EmitSparks(c + Vector3.up * 0.8f, Vector3.up, ArenVFX.FluteColor, 24, 9f, 180f);
            EmitGlyphs(c + Vector3.up * 1.2f, ArenVFX.FluteColor, 6, 2.5f);
            FlashLight(c + Vector3.up, ArenVFX.FluteColor, 4f, 9f, 0.25f);
        }

        System.Collections.IEnumerator Delayed(float t, System.Action a)
        {
            yield return new WaitForSeconds(t);
            a();
        }

        public Transform CreateBlade(Transform parent)
        {
            var root = new GameObject("BladeVisual").transform;
            root.SetParent(parent, false);
            void Part(float scale, float yOff, float zOff, Color c, float roll)
            {
                var g = new GameObject("arc"); g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(0, yOff, zOff - 0.65f * scale);
                g.transform.localRotation = Quaternion.Euler(0, 0, roll);
                g.transform.localScale = Vector3.one * scale;
                g.AddComponent<MeshFilter>().sharedMesh = bladeMesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = slashMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var b = new MaterialPropertyBlock();
                b.SetColor(IdColor, c); b.SetColor(IdCore, Color.white * 2.5f);
                b.SetFloat(IdUniform, 1f); b.SetFloat(IdFade, 1f); b.SetFloat(IdProgress, 1f); b.SetFloat(IdTail, 1f);
                mr.SetPropertyBlock(b);
                g.AddComponent<BladeWobble>().Setup(yOff, roll);
            }
            Part(0.95f, 0f, 0f, new Color(0.6f, 1.6f, 1.7f), 0f);
            Part(0.55f, 0.32f, -0.45f, new Color(0.5f, 1.1f, 1.4f) * 0.8f, 18f);    // harmônicos
            Part(0.55f, -0.3f, -0.6f, new Color(0.5f, 1.1f, 1.4f) * 0.8f, -18f);
            var tr = root.gameObject.AddComponent<TrailRenderer>();
            tr.sharedMaterial = flashMat; tr.time = 0.16f; tr.minVertexDistance = 0.2f;
            tr.widthCurve = AnimationCurve.EaseInOut(0, 0.9f, 1, 0f);
            tr.colorGradient = MakeGradient(new Color(0.6f, 1f, 1f, 0.7f), new Color(0.6f, 1f, 1f, 0f));
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.gameObject.AddComponent<BladeSparkEmitter>();
            return root;
        }

        static Gradient MakeGradient(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        public void DetachAndFade(Transform t, float time)
        {
            t.SetParent(transform, true);
            Destroy(t.gameObject, time);
            var tr = t.GetComponent<TrailRenderer>(); if (tr != null) tr.emitting = false;
            foreach (var w in t.GetComponentsInChildren<BladeWobble>()) w.FadeOut(time);
        }

        // ------------------------------------------------------------ Contracanto

        public void BeginCharge(Transform t)
        {
            if (sigil != null && sigil.active) Release(sigil);
            sigil = Get(Kind.Sigil, quad, sigilMat);
            sigilFollow = t;
            sigil.go.transform.rotation = Quaternion.Euler(90, 0, 0);
            sigil.go.transform.localScale = Vector3.one * 3.2f;
            sigil.dur = 9999f; sigil.color = new Color(1.6f, 1.1f, 0.45f); sigil.a = Vector4.zero;
        }

        public void UpdateCharge(Transform t, float progress, int level)
        {
            if (sigil == null || !sigil.active) return;
            sigil.a.x = Mathf.Clamp01(progress);
            // partículas convergindo para o Aren (o ar "alinha" com a nota)
            chargeEmitAcc += Time.deltaTime * (12f + level * 16f);
            while (chargeEmitAcc >= 1f)
            {
                chargeEmitAcc -= 1f;
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f;
                float dist = Random.Range(2.2f, 3.6f);
                Vector3 p = t.position + Vector3.up * 1.1f + d * dist;
                float speed = Random.Range(5f, 8f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = p, velocity = -d * speed, startLifetime = dist / speed,
                    startSize = Random.Range(0.04f, 0.08f), startColor = level >= 3 ? Color.white : ArenVFX.GoldColor
                };
                sparks.Emit(ep, 1);
            }
        }

        public void ChargeLevel(Transform t, int level)
        {
            SpawnRing(t.position + Vector3.up * 0.07f, 0.3f, 1.2f + level * 0.5f, 0.3f, ArenVFX.GoldColor, 0.1f, true);
            EmitGlyphs(t.position + Vector3.up * 1.6f, ArenVFX.GoldColor, 2 + level, 1.8f);
            FlashLight(t.position + Vector3.up * 1.2f, ArenVFX.GoldColor, 1.5f + level, 5f, 0.2f);
        }

        public void EndCharge()
        {
            if (sigil != null && sigil.active) { sigil.kind = Kind.Sigil; sigil.dur = sigil.t + 0.25f; sigil.a.y = 1; }
            sigilFollow = null;
        }

        public void ContracantoWave(Vector3 origin, Vector3 dir, float length, int level)
        {
            dir.y = 0; dir.Normalize();
            var w = Get(Kind.Wave, waveMesh, slashMat);
            w.go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            w.dur = 0.42f; w.color = new Color(1.4f, 1.0f, 0.45f); w.vel = dir * (length / 0.42f);
            w.a = new Vector4(1.2f, 1.2f + length * 0.45f, 0, 0);
            SpawnSlash(origin + Vector3.up * 0.2f, Quaternion.LookRotation(dir), -70f, 2.3f + level * 0.3f, ArenVFX.GoldColor, 0.26f);
            Vector3 ground = new Vector3(origin.x, origin.y - 1f, origin.z);
            SpawnRing(ground + Vector3.up * 0.08f, 0.5f, 4f + level, 0.45f, ArenVFX.GoldColor, 0.16f, true);
            if (ArenVFX.HighQuality)
            {
                SpawnDistortion(origin + dir * 1.5f, 3.5f, 0.35f, 0.06f);
                StartCoroutine(Delayed(0.12f, () => SpawnDistortion(origin + dir * (length * 0.6f), 3f, 0.3f, 0.045f)));
            }
            for (int i = 0; i < 26 + level * 8; i++)
            {
                float f = Random.value * length;
                Vector3 p = ground + dir * f + Vector3.Cross(Vector3.up, dir) * Random.Range(-1f, 1f) * (0.8f + f * 0.4f);
                EmitDust(p + Vector3.up * 0.2f, dir * Random.Range(2f, 5f) + Vector3.up * Random.Range(1f, 3f), new Color(0.55f, 0.48f, 0.4f, 0.55f), 1, 0.9f);
            }
            EmitSparks(origin + dir * 1.2f, dir, ArenVFX.GoldColor, 30 + level * 10, 14f, 35f);
            EmitGlyphs(origin + dir * 2f, ArenVFX.GoldColor, 6 + level * 2, 3f);
            FlashLight(origin + dir * 1.5f, ArenVFX.GoldColor, 6f + level * 2f, 12f, 0.3f);
        }

        // ------------------------------------------------------------ partículas

        public void EmitSparks(Vector3 p, Vector3 dir, Color c, int n, float speed, float spread)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
            dir.Normalize();
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.AngleAxis(Random.Range(-spread, spread), Random.onUnitSphere) * dir;
                var ep = new ParticleSystem.EmitParams
                {
                    position = p, velocity = d * speed * Random.Range(0.5f, 1.1f),
                    startLifetime = Random.Range(0.12f, 0.28f), startSize = Random.Range(0.05f, 0.1f),
                    startColor = Color.Lerp(c, Color.white, Random.Range(0.2f, 0.6f))
                };
                sparks.Emit(ep, 1);
            }
        }

        public void EmitGlyphs(Vector3 p, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) + 0.5f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + Random.insideUnitSphere * 0.2f, velocity = d.normalized * speed * Random.Range(0.4f, 1f),
                    startLifetime = Random.Range(0.5f, 0.9f), startSize = Random.Range(0.12f, 0.22f),
                    startColor = Color.Lerp(c, Color.white, 0.35f), rotation = Random.Range(-20f, 20f)
                };
                glyphs.Emit(ep, 1);
            }
        }

        public void EmitDust(Vector3 p, Vector3 vel, Color c, int n, float size)
        {
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + Random.insideUnitSphere * 0.15f, velocity = vel + Random.insideUnitSphere * 0.5f,
                    startLifetime = Random.Range(0.6f, 1.1f), startSize = size * Random.Range(0.7f, 1.2f), startColor = c,
                    rotation = Random.Range(0f, 360f)
                };
                dust.Emit(ep, 1);
            }
        }

        public void CorruptionBurst(Vector3 p, float scale)
        {
            EmitDust(p, Vector3.up * 1.2f, new Color(0.06f, 0.02f, 0.08f, 0.75f), Mathf.RoundToInt(10 * scale), 0.9f * scale);
            EmitSparks(p, Vector3.up, ArenVFX.CorruptColor, Mathf.RoundToInt(14 * scale), 5f, 120f);
            EmitGlyphs(p, ArenVFX.VoidColor * 2f, 3, 1.2f);
            SpawnRing(p, 0.2f, 1.6f * scale, 0.35f, ArenVFX.CorruptColor, 0.08f, false);
        }

        public void FlashLight(Vector3 p, Color c, float intensity, float range, float time)
        {
            if (!ArenVFX.FlashLights) return;
            flashLight.transform.position = p; flashLight.color = c; flashLight.range = range;
            flashI = intensity; flashT = 0; flashDur = time; flashLight.intensity = intensity; flashLight.enabled = true;
        }

        // ------------------------------------------------------------ animação

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var fx = active[i];
                fx.t += dt;
                if (fx.t < 0) { fx.mr.enabled = false; continue; }
                fx.mr.enabled = true;
                float e = fx.dur > 0 ? Mathf.Clamp01(fx.t / fx.dur) : 1f;
                if (fx.t >= fx.dur) { Release(fx); active.RemoveAt(i); continue; }
                if (fx.kind == Kind.Star && cam != null) fx.go.transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, fx.a.y + e * 40f);
                else if (fx.billboard && cam != null) fx.go.transform.rotation = cam.transform.rotation;
                mpb.Clear();
                switch (fx.kind)
                {
                    case Kind.Slash:
                    {
                        float k = 1f - Mathf.Pow(1f - e, 3f);
                        mpb.SetFloat(IdProgress, k * 1.45f);
                        mpb.SetFloat(IdFade, 1f - Mathf.SmoothStep(0.65f, 1f, e));
                        mpb.SetColor(IdColor, fx.color * 1.6f);
                        mpb.SetColor(IdCore, Color.white * 2.2f);
                        mpb.SetFloat(IdTail, 0.75f);
                        fx.go.transform.localScale = Vector3.one * fx.a.x * (0.92f + 0.12f * k);
                        break;
                    }
                    case Kind.Ring:
                    {
                        float k = 1f - Mathf.Pow(1f - e, 2.2f);
                        float r = Mathf.Lerp(fx.a.x, fx.a.y, k);
                        float outer = Mathf.Max(fx.a.x, fx.a.y) * 1.08f;
                        fx.go.transform.localScale = Vector3.one * outer * 2f;
                        mpb.SetFloat(IdRadius, r / outer);
                        mpb.SetFloat(IdWidth, Mathf.Max(0.004f, fx.a.z * (1f - 0.5f * e)));
                        mpb.SetFloat(IdFade, 1f - Mathf.SmoothStep(0.35f, 1f, e));
                        mpb.SetColor(IdColor, fx.color);
                        break;
                    }
                    case Kind.Flash:
                        fx.go.transform.localScale = Vector3.one * fx.a.x * (0.6f + e * 0.8f);
                        mpb.SetColor(IdColor, fx.color);
                        mpb.SetFloat(IdFade, 1f - e);
                        break;
                    case Kind.Distortion:
                    {
                        float k = 1f - Mathf.Pow(1f - e, 2f);
                        fx.go.transform.localScale = Vector3.one * fx.a.x * 2f;
                        mpb.SetFloat(IdRadius, Mathf.Lerp(0.1f, 0.9f, k));
                        mpb.SetFloat(IdStrength, fx.a.y);
                        mpb.SetFloat(IdFade, 1f - e);
                        break;
                    }
                    case Kind.Ghost:
                        mpb.SetColor(IdColor, fx.color * 0.75f);
                        mpb.SetFloat(IdFade, fx.a.x * (1f - e) * (1f - e));
                        mpb.SetFloat(IdInflate, e * 0.03f);
                        break;
                    case Kind.Sigil:
                        if (sigilFollow != null) fx.go.transform.position = sigilFollow.position + Vector3.up * 0.06f;
                        mpb.SetFloat(IdCharge, fx.a.x);
                        mpb.SetFloat(IdSpin, Time.time * (0.6f + fx.a.x * 2.5f));
                        mpb.SetFloat(IdFade, fx.a.y > 0 ? 1f - Mathf.Clamp01((fx.t - (fx.dur - 0.25f)) / 0.25f) : 1f);
                        mpb.SetColor(IdColor, fx.color);
                        break;
                    case Kind.Wave:
                    {
                        fx.go.transform.position += fx.vel * dt;
                        float s = Mathf.Lerp(fx.a.x, fx.a.y, e);
                        fx.go.transform.localScale = new Vector3(s, 1f + e * 0.4f, s);
                        mpb.SetColor(IdColor, fx.color * 1.5f);
                        mpb.SetColor(IdCore, Color.white * 2.5f);
                        mpb.SetFloat(IdUniform, 1f);
                        mpb.SetFloat(IdProgress, 1f); mpb.SetFloat(IdTail, 1f);
                        mpb.SetFloat(IdFade, 1f - Mathf.SmoothStep(0.55f, 1f, e));
                        break;
                    }
                    case Kind.Star:
                    {
                        // pop rápido: cresce em 20% do tempo e some
                        float k = e < 0.2f ? e / 0.2f : 1f - (e - 0.2f) / 0.8f;
                        fx.go.transform.localScale = Vector3.one * fx.a.x * (0.5f + 0.7f * Mathf.Min(1f, e * 5f));
                        mpb.SetColor(IdColor, fx.color);
                        mpb.SetFloat(IdFade, Mathf.Clamp01(k));
                        break;
                    }
                    case Kind.Portal:
                    {
                        // abre rápido, fica, fecha no fim
                        float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / 0.18f)) * (1f - Mathf.SmoothStep(0.7f, 1f, e));
                        fx.go.transform.localScale = Vector3.one * fx.a.x * 2f;
                        mpb.SetFloat(IdRadius, 0.15f + 0.7f * open);
                        mpb.SetFloat(IdFade, open);
                        break;
                    }
                    case Kind.Shell:
                    {
                        float k = 1f - Mathf.Pow(1f - e, 2.5f);
                        float r = Mathf.Lerp(fx.a.x, fx.a.y, k);
                        fx.go.transform.localScale = new Vector3(r, fx.a.z * (1f - e * 0.5f), r);
                        mpb.SetColor(IdColor, fx.color);
                        mpb.SetFloat(IdFade, (1f - e) * 0.6f);
                        break;
                    }
                }
                fx.mr.SetPropertyBlock(mpb);
            }

            UpdateAmbient();

            if (flashLight.enabled)
            {
                flashT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(flashT / Mathf.Max(0.01f, flashDur));
                flashLight.intensity = flashI * (1f - k) * (1f - k);
                if (k >= 1f) flashLight.enabled = false;
            }
        }
    }

    /// <summary>Oscilação dos harmônicos da Lâmina (seno), e fade quando ela some.</summary>
    public class BladeWobble : MonoBehaviour
    {
        float baseY, baseRoll, fadeT = -1, fadeDur, seed;
        MeshRenderer mr; MaterialPropertyBlock b;
        public void Setup(float y, float roll) { baseY = y; baseRoll = roll; seed = Random.value * 10f; mr = GetComponent<MeshRenderer>(); b = new MaterialPropertyBlock(); }
        public void FadeOut(float t) { fadeT = 0; fadeDur = t; }
        void Update()
        {
            var p = transform.localPosition;
            p.y = baseY + (baseY == 0 ? 0 : Mathf.Sin(Time.time * 30f + seed) * 0.06f);
            transform.localPosition = p;
            transform.localRotation = Quaternion.Euler(0, 0, baseRoll + Mathf.Sin(Time.time * 22f + seed) * 4f);
            if (fadeT >= 0)
            {
                fadeT += Time.deltaTime;
                mr.GetPropertyBlock(b);
                b.SetFloat("_Fade", 1f - Mathf.Clamp01(fadeT / fadeDur));
                mr.SetPropertyBlock(b);
            }
        }
    }

    /// <summary>Faíscas soltas pelo caminho da Lâmina.</summary>
    public class BladeSparkEmitter : MonoBehaviour
    {
        float acc;
        void Update()
        {
            acc += Time.deltaTime * 40f;
            while (acc >= 1f)
            {
                acc -= 1f;
                ArenVFX.Sparks(transform.position + Random.insideUnitSphere * 0.4f, -transform.forward, ArenVFX.FluteColor, 1, 3f, 40f);
            }
        }
    }
}

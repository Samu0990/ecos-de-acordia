using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// Relógio das animações da tela inicial: tempo real (sem timeScale), mas que respeita
    /// Time.captureDeltaTime — assim a gravação em vídeo (-eda-title-video) sai na velocidade certa.
    /// </summary>
    public static class TitleClock
    {
        static int frame = -1; static float now = -1f, delta;
        public static float Now { get { Tick(); return now; } }
        public static float Delta { get { Tick(); return delta; } }
        static void Tick()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            delta = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            now = now < 0f ? Time.unscaledTime : now + delta;
        }
    }

    /// <summary>
    /// Estado do fundo em movimento, o mesmo que o shader UITitleMotion usa: câmera (passeio
    /// automático + mouse) com paralaxe e zoom proporcionais à profundidade p, os três pêndulos,
    /// a badalada (onda que sai do brasão, empurra o cenário, acende o fundo de violeta e sacode
    /// as lanternas) e a revelação da abertura (a luz se espalha a partir do brasão).
    /// Forward() diz onde um ponto da arte aparece na tela agora.
    /// </summary>
    public static class TitleMotion
    {
        public static Vector2 Cam, MouseCam; public static float Zoom; public static Vector3 Angles;
        public static Vector2 Center = new Vector2(836f, 470.5f);
        public static Vector2 P1 = new Vector2(340, 40), P2 = new Vector2(1275, 0), P3 = new Vector2(1421, 0);
        // mouse em px da arte (para as brasas desviarem); x < 0 = sem mouse
        public static Vector2 Mouse = new Vector2(-9999, -9999);

        public const float TollSpeed = 760f;
        public static Vector2 TollCenter = new Vector2(836, 116);
        public static float TollStart = -100f;
        public static float TollAge => TitleClock.Now - TollStart;
        public static float TollRadius => TollAge * TollSpeed;
        public static float TollStrength => TollAge < 4.5f ? Mathf.Exp(-TollAge / 1.15f) : 0f;
        /// <summary>Clarão violeta no fundo distante logo depois da badalada.</summary>
        public static float Flash
        {
            get
            {
                float a = TollAge;
                if (a < 0f || a > 3f) return 0f;
                return a < 0.18f ? a / 0.18f : Mathf.Exp(-(a - 0.18f) / 0.5f);
            }
        }

        public static Vector2 RevealCenter = new Vector2(836, 116);
        public static float RevealRadius = 1e5f;
        public static float Reveal(Vector2 p, float soft = 80f) => Mathf.Clamp01((RevealRadius - Vector2.Distance(p, RevealCenter)) / soft);

        /// <summary>Quanto o anel da badalada está passando por este ponto agora (0..1).</summary>
        public static float TollRing(Vector2 p, float width = 55f)
        {
            float s = TollStrength;
            if (s <= 0.001f) return 0f;
            float x = (Vector2.Distance(p, TollCenter) - TollRadius) / width;
            return Mathf.Exp(-x * x) * s;
        }

        public static void Toll(Vector2 center) { TollCenter = center; TollStart = TitleClock.Now; }

        public static Vector2 Forward(Vector2 a, float p, float g1 = 0f, float g2 = 0f, float g3 = 0f)
        {
            if (g1 > 0f) a = Rot(a, P1, Angles.x * g1);
            if (g2 > 0f) a = Rot(a, P2, Angles.y * g2);
            if (g3 > 0f) a = Rot(a, P3, Angles.z * g3);
            return a + p * (Cam + (a - Center) * Zoom);
        }

        public static Vector2 Rot(Vector2 q, Vector2 pivot, float ang)
        {
            float s = Mathf.Sin(ang), c = Mathf.Cos(ang);
            var d = q - pivot;
            return pivot + new Vector2(c * d.x - s * d.y, s * d.x + c * d.y);
        }

        /// <summary>Balanço extra de um pêndulo depois que a onda da badalada chega nele.</summary>
        public static float Kick(Vector2 at, float amp, float w)
        {
            float k = TollAge - Vector2.Distance(at, TollCenter) / TollSpeed;
            if (k < 0f || k > 9f) return 0f;
            return amp * TollStrengthAt(k) * Mathf.Sin(k * w);
        }
        static float TollStrengthAt(float k) => Mathf.Exp(-k / 2.4f);

        /// <summary>Passeio lento em Lissajous + mouse, zoom respirando, pêndulos (+ badalada).</summary>
        public static void Step(float t)
        {
            Cam = new Vector2(Mathf.Sin(t * 0.48f) * 12f + Mathf.Sin(t * 0.97f + 1.1f) * 1.5f, Mathf.Sin(t * 0.33f + 0.7f) * 5f) + MouseCam;
            // zoom-base nas camadas próximas: com a câmera no extremo (~23 px) a borda da tela
            // ainda lê dentro da imagem (836 px × 0,028 ≥ 23), nunca a coluna da borda repetida
            Zoom = 0.031f + 0.003f * Mathf.Sin(t * 0.23f);
            Angles = new Vector3(0.027f * Mathf.Sin(t * 1.61f) + 0.005f * Mathf.Sin(t * 3.7f + 0.5f) + Kick(new Vector2(340, 240), 0.06f, 1.61f),
                                 0.015f * Mathf.Sin(t * 1.23f + 1f) + Kick(new Vector2(1275, 380), 0.035f, 1.23f),
                                 0.045f * Mathf.Sin(t * 2.4f + 2f) + Kick(new Vector2(1421, 60), 0.09f, 2.4f));
        }
    }

    /// <summary>Base: um Graphic que desenha muitos quads em coordenadas da arte (y para baixo)
    /// numa malha só — um draw call para todas as chamas/partículas.</summary>
    public abstract class TitleQuads : MaskableGraphic
    {
        public Texture tex;
        public override Texture mainTexture => tex != null ? tex : Texture2D.whiteTexture;

        protected static void Quad(VertexHelper vh, float x, float y, float w, float h, Color32 c)
            => QuadUV(vh, x, y, w, h, c, 0f, 0f, 1f, 1f);

        protected static void QuadUV(VertexHelper vh, float x, float y, float w, float h, Color32 c, float u0, float v0, float u1, float v1)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x, -y - h), c, new Vector2(u0, v0));
            vh.AddVert(new Vector3(x, -y), c, new Vector2(u0, v1));
            vh.AddVert(new Vector3(x + w, -y), c, new Vector2(u1, v1));
            vh.AddVert(new Vector3(x + w, -y - h), c, new Vector2(u1, v0));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        protected static float Snap(float v) => Mathf.Round(v / TitleScreen.ArtPixel) * TitleScreen.ArtPixel;

        void Update() { Tick(Mathf.Min(TitleClock.Delta, 0.05f)); SetVerticesDirty(); }
        protected virtual void Tick(float dt) { }
    }

    /// <summary>Halos das velas, lanternas, brasas, brasão e olhos da estátua: tremulam com ruído,
    /// em degraus; acendem quando a luz da abertura chega neles e reagem à badalada.</summary>
    public class TitleGlows : TitleQuads
    {
        public const int Flame = 0, Ember = 1, Gold = 2, Eye = 3;
        struct L { public float x, y, r, i, seed, p, g1, g2, g3, lit; public int t; }
        readonly List<L> lights = new List<L>();
        public float fade = 1f;
        static readonly Color CFlame = new Color(1f, 0.56f, 0.24f), CEmber = new Color(0.95f, 0.2f, 0.14f),
            CGold = new Color(1f, 0.78f, 0.42f), CEye = new Color(1f, 0.12f, 0.1f);

        public void Add(float x, float y, float r, float i, int t, float p, float g1, float g2, float g3)
            => lights.Add(new L { x = x, y = y, r = r, i = i, t = t, p = p, g1 = g1, g2 = g2, g3 = g3, seed = lights.Count * 7.31f + 3.7f, lit = -1f });

        float Flicker(in L l, float t)
        {
            switch (l.t)
            {
                case Flame:   // ruído rápido + rajadas de vento que passam pelas velas próximas juntas
                    float n = Mathf.PerlinNoise(t * 6.5f + l.seed, l.seed * 0.37f);
                    float n2 = Mathf.PerlinNoise(t * 17f + l.seed * 2f, 3.1f);
                    float gust = Mathf.PerlinNoise(t * 0.55f + l.x * 0.004f, 9.2f);
                    return Mathf.Clamp01((0.55f + 0.45f * n + 0.15f * (n2 - 0.5f)) * (0.75f + 0.35f * gust));
                case Ember:   // brasa distante: respira devagar
                    return 0.45f + 0.55f * Mathf.PerlinNoise(t * 0.9f + l.seed, 1.7f);
                case Eye:     // olhos da estátua: quase apagados, acendem de vez em quando
                    float e = 0.5f + 0.5f * Mathf.Sin(t * 0.45f + l.seed * 0.1f);
                    return 0.12f + 0.88f * e * e * e * e;
                default:      // brasão dourado
                    return 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.15f + l.seed));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = TitleClock.Now;
            for (int k = 0; k < lights.Count; k++)
            {
                var l = lights[k];
                var at = TitleMotion.Forward(new Vector2(l.x, l.y), l.p, l.g1, l.g2, l.g3);   // anda com o cenário
                float rv = TitleMotion.Reveal(new Vector2(l.x, l.y), 50f);
                if (rv > 0.5f && l.lit < 0f) { l.lit = t; lights[k] = l; }                   // a vela "acende"
                float ignite = l.lit >= 0f ? 1f + 1.4f * Mathf.Exp(-(t - l.lit) * 4f) : 1f;
                float ring = TitleMotion.TollRing(new Vector2(l.x, l.y));
                float f = Mathf.Floor(Flicker(l, t) * 8f) / 8f * fade * rv * ignite;     // degraus de paleta
                if (l.t == Gold) f *= 1f + 3f * Mathf.Exp(-Mathf.Max(0f, TitleMotion.TollAge) * 2.2f) * (TitleMotion.TollAge >= 0f ? 1f : 0f);
                else f *= 1f + (l.t == Eye ? 4f : 1.1f) * ring;
                var c = l.t == Flame ? CFlame : l.t == Ember ? CEmber : l.t == Gold ? CGold : CEye;
                float halo = Snap(l.r * (l.t == Gold ? 2.2f : 2.6f));
                c.a = Mathf.Clamp01((l.t == Ember ? 0.16f : l.t == Gold ? 0.16f : l.t == Eye ? 0.22f : 0.2f) * l.i * f);
                Quad(vh, Snap(at.x) - halo, Snap(at.y) - halo, halo * 2, halo * 2, c);
                if (l.t == Flame || l.t == Eye)
                {
                    float core = l.t == Eye ? 2f : Snap(Mathf.Max(4f, l.r * 0.55f));
                    c.a = Mathf.Clamp01((l.t == Eye ? 0.9f : 0.55f) * l.i * f);
                    Quad(vh, Snap(at.x) - core * 0.5f * (l.t == Eye ? 1f : 2f), Snap(at.y) - core * 0.5f * (l.t == Eye ? 1f : 2f), core * (l.t == Eye ? 1f : 2f), core * (l.t == Eye ? 1f : 2f), c);
                }
            }
        }
    }

    /// <summary>Chamas dançando em pixel art por cima das velas principais: base clara, corpo
    /// laranja e ponta vermelha que mudam de altura e balançam a cada ~80 ms.</summary>
    public class TitleFlames : TitleQuads
    {
        struct F { public float x, y, p, g1, g2, g3, i, seed, next; public int body, tipX; public bool tip; }
        readonly List<F> flames = new List<F>();

        public void Add(float x, float y, float i, float p, float g1, float g2, float g3)
            => flames.Add(new F { x = x, y = y, i = i, p = p, g1 = g1, g2 = g2, g3 = g3, seed = flames.Count * 13.7f, body = 2 });

        protected override void Tick(float dt)
        {
            float t = TitleClock.Now;
            for (int k = 0; k < flames.Count; k++)
            {
                var f = flames[k];
                if (t < f.next) continue;
                f.next = t + Random.Range(0.06f, 0.11f);
                float gust = Mathf.PerlinNoise(t * 0.55f + f.x * 0.004f, 9.2f);   // mesma rajada dos halos
                f.body = Mathf.Clamp(Mathf.RoundToInt(1 + Random.value * 2.4f + gust * 1.2f), 1, 4);
                f.tipX = Random.value < 0.5f ? 0 : (Random.value < 0.5f ? -1 : 1);
                f.tip = Random.value < 0.75f;
                flames[k] = f;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float px = TitleScreen.ArtPixel;
            foreach (var f in flames)
            {
                float rv = TitleMotion.Reveal(new Vector2(f.x, f.y), 50f);
                if (rv <= 0.01f) continue;
                var at = TitleMotion.Forward(new Vector2(f.x, f.y), f.p, f.g1, f.g2, f.g3);
                float x = Snap(at.x) - px * 0.5f, y = Snap(at.y);
                float a = f.i * rv;
                Quad(vh, x, y + px, px, px, new Color(1f, 0.88f, 0.55f, 0.5f * a));                         // base
                Quad(vh, x, y + px - f.body * px, px, f.body * px, new Color(1f, 0.55f, 0.2f, 0.42f * a));   // corpo
                if (f.tip) Quad(vh, x + f.tipX * px, y - f.body * px, px, px, new Color(1f, 0.32f, 0.14f, 0.38f * a));  // ponta
            }
        }
    }

    /// <summary>Partículas quadradas em pixel art: brasas que sobem, poeira e faíscas das joias.
    /// Desviam do mouse, são empurradas pela onda da badalada e só aparecem onde já há luz.</summary>
    public class TitleSparks : TitleQuads
    {
        public class Emitter
        {
            public Rect area; public float rate; public Vector2 velMin, velMax, life;
            public Color a, b; public int kind; public bool on = true; internal float acc;
            public float parallax; public int size;   // size 0 = sorteia 1 ou 2 pixels
        }
        struct P { public Vector2 pos, vel; public float age, life, seed, par; public Color a, b; public int kind, size; }
        public readonly List<Emitter> emitters = new List<Emitter>();
        public Rect avoid;   // painel central: as brasas do fundo apagam ao entrar nele
        public bool reactive = true;
        readonly List<P> parts = new List<P>();
        const int Max = 300;

        void Spawn(Emitter e, Vector2? at = null)
        {
            if (parts.Count >= Max) return;
            var p = new P
            {
                pos = at ?? new Vector2(Random.Range(e.area.xMin, e.area.xMax), Random.Range(e.area.yMin, e.area.yMax)),
                vel = new Vector2(Random.Range(e.velMin.x, e.velMax.x), Random.Range(e.velMin.y, e.velMax.y)),
                life = Random.Range(e.life.x, e.life.y), seed = Random.value * 100f,
                a = e.a, b = e.b, kind = e.kind, size = e.size > 0 ? e.size : (e.kind == 1 || Random.value < 0.75f ? 1 : 2), par = e.parallax,
            };
            if (avoid.width > 0 && avoid.Contains(p.pos)) return;
            parts.Add(p);
        }

        public void Burst(Vector2 at, int n)
        {
            var e = emitters.Count > 0 ? emitters[0] : null;
            if (e == null) return;
            for (int i = 0; i < n && parts.Count < Max; i++)
            {
                parts.Add(new P
                {
                    pos = at + Random.insideUnitCircle * 4f, vel = Random.insideUnitCircle.normalized * Random.Range(20f, 55f) + new Vector2(0, -12f),
                    life = Random.Range(0.45f, 1.1f), seed = Random.value * 100f, a = e.a, b = e.b, kind = 2, size = Random.value < 0.45f ? 1 : 2,
                });
            }
        }

        public void Prewarm(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.05f) Tick(0.05f);
        }

        protected override void Tick(float dt)
        {
            float t = TitleClock.Now;
            foreach (var e in emitters)
            {
                if (!e.on) continue;
                e.acc += e.rate * dt;
                while (e.acc >= 1f) { e.acc -= 1f; Spawn(e); }
            }
            var mouse = TitleMotion.Mouse;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.age += dt;
                switch (p.kind)
                {
                    case 0:   // brasa: sobe balançando e desacelera
                        p.pos += p.vel * dt + new Vector2(Mathf.Sin(t * 1.9f + p.seed) * 7f * dt, 0);
                        p.vel *= 1f - 0.12f * dt;
                        break;
                    case 1:   // poeira: deriva lenta
                        p.vel += new Vector2(Mathf.PerlinNoise(t * 0.3f, p.seed) - 0.5f, Mathf.PerlinNoise(p.seed, t * 0.3f) - 0.5f) * 6f * dt;
                        p.vel *= 1f - 0.4f * dt;
                        p.pos += p.vel * dt;
                        break;
                    default:  // faísca da joia: espalha e cai um pouco
                        p.pos += p.vel * dt;
                        p.vel *= 1f - 2.4f * dt;
                        p.vel.y += 10f * dt;
                        break;
                }
                if (reactive && p.kind != 2)
                {
                    var seen = p.par != 0f ? TitleMotion.Forward(p.pos, p.par) : p.pos;
                    // o cursor sopra as brasas e a poeira para longe
                    var d = seen - mouse;
                    float dm = d.magnitude;
                    if (dm < 75f && dm > 0.01f) p.vel += d / dm * (75f - dm) * 9f * dt;
                    // a onda da badalada empurra tudo para fora do brasão
                    float ring = TitleMotion.TollRing(p.pos, 70f);
                    if (ring > 0.01f) p.vel += (p.pos - TitleMotion.TollCenter).normalized * ring * 260f * dt;
                }
                if (avoid.width > 0 && p.kind != 2 && avoid.Contains(p.pos)) p.life = Mathf.Min(p.life, p.age + 0.15f);
                if (p.age >= p.life) { parts.RemoveAt(i); continue; }
                parts[i] = p;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = TitleClock.Now;
            float px = TitleScreen.ArtPixel;
            foreach (var p in parts)
            {
                float k = p.age / p.life;
                var c = Color.Lerp(p.a, p.b, k);
                float fade = Mathf.Min(1f, p.age / 0.35f) * Mathf.Min(1f, (p.life - p.age) / 0.5f) * TitleMotion.Reveal(p.pos);
                if (p.kind == 0) fade *= 0.75f + 0.25f * Mathf.Sign(Mathf.Sin(t * 13f + p.seed));   // cintila
                c.a *= Mathf.Floor(fade * 5f) / 5f;
                if (c.a <= 0.01f) continue;
                float s = p.size * px;
                var at = p.par != 0f ? TitleMotion.Forward(p.pos, p.par) : p.pos;
                Quad(vh, Snap(at.x), Snap(at.y), s, s, c);
            }
        }
    }

    /// <summary>Bandos de corvos (silhuetas de 13x7 pixels, 3 quadros de asa; cânone de Elyndra: a linha
    /// aérea é de corvos, corujas e abutres — sem morcegos nem andorinhas) saindo da escuridão e sumindo
    /// no teto, sempre pelas laterais (nunca na frente do painel).</summary>
    public class TitleCrows : TitleQuads
    {
        struct Bat { public float start, dur, offset, phase, freq, scale; }
        readonly List<Bat> bats = new List<Bat>();
        Vector2[] path; float nextFlock = 5.5f;
        bool leftNext = true;
        static readonly int[] Cycle = { 0, 1, 2, 1 };
        static readonly Vector2[] LeftPath = { new Vector2(-40, 360), new Vector2(150, 250), new Vector2(330, 190), new Vector2(460, 80), new Vector2(400, -50) };
        static readonly Vector2[] RightPath = { new Vector2(1712, 440), new Vector2(1520, 310), new Vector2(1340, 230), new Vector2(1220, 110), new Vector2(1300, -50) };

        public void Begin(float firstDelay) => nextFlock = TitleClock.Now + firstDelay;

        static Vector2 CatmullRom(Vector2[] p, float u)
        {
            u = Mathf.Clamp01(u) * (p.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(u), p.Length - 2);
            float t = u - i;
            Vector2 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Length - 1)];
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        }

        protected override void Tick(float dt)
        {
            float t = TitleClock.Now;
            bats.RemoveAll(b => t > b.start + b.dur + 0.1f);
            if (bats.Count == 0 && t >= nextFlock && TitleMotion.RevealRadius > 5000f)
            {
                path = leftNext ? LeftPath : RightPath;
                leftNext = !leftNext;
                int n = Random.Range(3, 7);
                float dur = Random.Range(4.6f, 6.2f);
                for (int i = 0; i < n; i++)
                    bats.Add(new Bat
                    {
                        start = t + Random.Range(0f, 0.9f), dur = dur * Random.Range(0.9f, 1.1f), offset = Random.Range(-30f, 30f),
                        phase = Random.value * 4f, freq = Random.Range(9f, 12.5f), scale = Random.value < 0.35f ? 1f : 2f,
                    });
                nextFlock = t + Random.Range(11f, 19f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (path == null) return;
            float t = TitleClock.Now;
            foreach (var b in bats)
            {
                float u = (t - b.start) / b.dur;
                if (u < 0f || u > 1f) continue;
                var pos = CatmullRom(path, u);
                var ahead = CatmullRom(path, Mathf.Min(1f, u + 0.01f));
                var dir = (ahead - pos).normalized;
                pos += new Vector2(-dir.y, dir.x) * b.offset + new Vector2(0, Mathf.Sin(t * 3.1f + b.phase) * 6f);
                pos = TitleMotion.Forward(pos, 0.25f);
                int f = Cycle[Mathf.FloorToInt((t + b.phase) * b.freq) % Cycle.Length];
                float w = 13f * b.scale, h = 7f * b.scale;
                float a = Mathf.Min(1f, Mathf.Min(u, 1f - u) * 8f);
                QuadUV(vh, Snap(pos.x - w * 0.5f), Snap(pos.y - h * 0.5f), w, h, new Color(1, 1, 1, a), f / 3f, 0f, (f + 1) / 3f, 1f);
            }
        }
    }

    /// <summary>Ecos fantasmas: luzes violeta e turquesa que vagam devagar pela arquitetura ao
    /// fundo deixando um rastro, somem e reaparecem; brilham mais quando a badalada passa.</summary>
    public class TitleWisps : TitleQuads
    {
        class W { public Vector2 pos, vel; public float age, life, seed; public Color c; public readonly Vector2[] trail = new Vector2[12]; public float trailT; }
        readonly List<W> wisps = new List<W>();
        float nextSpawn = 2.5f;
        static readonly Rect LeftArea = new Rect(230, 230, 250, 330), RightArea = new Rect(1170, 170, 160, 300);

        protected override void Tick(float dt)
        {
            float t = TitleClock.Now;
            if (t >= nextSpawn && wisps.Count < 3)
            {
                var area = Random.value < 0.5f ? LeftArea : RightArea;
                var w = new W
                {
                    pos = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)),
                    vel = Random.insideUnitCircle.normalized * Random.Range(6f, 12f), life = Random.Range(10f, 16f), seed = Random.value * 50f,
                    c = Random.value < 0.6f ? new Color(0.72f, 0.45f, 1f) : new Color(0.45f, 0.92f, 1f),
                };
                for (int i = 0; i < w.trail.Length; i++) w.trail[i] = w.pos;
                wisps.Add(w);
                nextSpawn = t + Random.Range(4.5f, 8.5f);
            }
            for (int i = wisps.Count - 1; i >= 0; i--)
            {
                var w = wisps[i];
                w.age += dt;
                // vagueia: rumo que gira devagar + ondulação
                float ang = Mathf.PerlinNoise(t * 0.12f, w.seed) * 6.28f * 2f;
                w.vel = Vector2.Lerp(w.vel, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 10f, dt * 0.6f);
                w.pos += (w.vel + new Vector2(0, Mathf.Sin(t * 1.3f + w.seed) * 8f)) * dt;
                w.trailT += dt;
                if (w.trailT > 0.07f)
                {
                    w.trailT = 0f;
                    for (int k = w.trail.Length - 1; k > 0; k--) w.trail[k] = w.trail[k - 1];
                    w.trail[0] = w.pos;
                }
                if (w.age > w.life) wisps.RemoveAt(i);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (var w in wisps)
            {
                float fade = Mathf.Min(1f, w.age / 1.6f) * Mathf.Min(1f, (w.life - w.age) / 2f) * TitleMotion.Reveal(w.pos);
                fade *= 1f + 2.5f * TitleMotion.TollRing(w.pos, 80f);
                fade *= 0.75f + 0.25f * Mathf.Sin(TitleClock.Now * 2.3f + w.seed);
                if (fade <= 0.01f) continue;
                for (int k = w.trail.Length - 1; k >= 0; k--)
                {
                    float s = Snap(Mathf.Lerp(16f, 4f, k / (float)(w.trail.Length - 1)));
                    var p = TitleMotion.Forward(w.trail[k], -0.3f);
                    var c = w.c; c.a = Mathf.Clamp01(0.3f * fade * (1f - k / (float)w.trail.Length));
                    Quad(vh, Snap(p.x) - s * 0.5f, Snap(p.y) - s * 0.5f, s, s, c);
                }
                var h = TitleMotion.Forward(w.pos, -0.3f);
                var core = new Color(0.95f, 0.9f, 1f, Mathf.Clamp01(0.55f * fade));
                Quad(vh, Snap(h.x) - 3f, Snap(h.y) - 3f, 6f, 6f, core);
            }
        }
    }

    /// <summary>Gotas que se formam nas pontas das estalactites, caem acelerando como um risco
    /// claro e respingam no chão (gotinhas + anel que abre).</summary>
    public class TitleDrips : TitleQuads
    {
        class D { public float x, y, land, p, wait, form, vy, fallY, splashT; public int state; public Vector2[] drops = new Vector2[5], dropV = new Vector2[5]; }
        readonly List<D> drips = new List<D>();

        public void Add(float x, float y, float land, float p)
            => drips.Add(new D { x = x, y = y, land = land, p = p, wait = Random.Range(1f, 7f) });

        protected override void Tick(float dt)
        {
            foreach (var d in drips)
            {
                switch (d.state)
                {
                    case 0: d.wait -= dt; if (d.wait <= 0f) { d.state = 1; d.form = 0f; } break;      // esperando
                    case 1: d.form += dt; if (d.form > 1.6f) { d.state = 2; d.vy = 0f; d.fallY = d.y; } break;  // gota crescendo
                    case 2:                                                                            // caindo
                        d.vy += 980f * dt; d.fallY += d.vy * dt;
                        if (d.fallY >= d.land)
                        {
                            d.state = 3; d.splashT = 0f;
                            for (int i = 0; i < d.drops.Length; i++) { d.drops[i] = new Vector2(d.x, d.land); d.dropV[i] = new Vector2(Random.Range(-45f, 45f), Random.Range(-110f, -50f)); }
                        }
                        break;
                    case 3:                                                                            // respingo
                        d.splashT += dt;
                        for (int i = 0; i < d.drops.Length; i++) { d.dropV[i].y += 600f * dt; d.drops[i] += d.dropV[i] * dt; }
                        if (d.splashT > 0.4f) { d.state = 0; d.wait = Random.Range(2.5f, 9f); }
                        break;
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float px = TitleScreen.ArtPixel;
            var col = new Color(0.66f, 0.76f, 0.95f, 0.6f);
            foreach (var d in drips)
            {
                float rv = TitleMotion.Reveal(new Vector2(d.x, d.y));
                if (rv <= 0.01f || d.state == 0) continue;
                var c = col; c.a *= rv;
                if (d.state == 1)
                {
                    var at = TitleMotion.Forward(new Vector2(d.x, d.y), d.p);
                    c.a *= Mathf.Clamp01(d.form / 0.8f);
                    float h = d.form > 1.1f ? 2f * px : px;   // estica antes de soltar
                    Quad(vh, Snap(at.x), Snap(at.y), px, h, c);
                }
                else if (d.state == 2)
                {
                    var at = TitleMotion.Forward(new Vector2(d.x, d.fallY), d.p);
                    float len = Snap(Mathf.Clamp(2f + d.vy * 0.012f, 2f, 10f));
                    Quad(vh, Snap(at.x), Snap(at.y) - len, px, len, c);
                }
                else
                {
                    float k = d.splashT / 0.4f;
                    c.a *= 1f - k;
                    foreach (var dr in d.drops)
                    {
                        var at = TitleMotion.Forward(dr, d.p);
                        Quad(vh, Snap(at.x), Snap(at.y), px, px, c);
                    }
                    var b = TitleMotion.Forward(new Vector2(d.x, d.land), d.p);
                    float r = Snap(2f + k * 10f);
                    Quad(vh, Snap(b.x) - r, Snap(b.y), px, px, c);
                    Quad(vh, Snap(b.x) + r, Snap(b.y), px, px, c);
                }
            }
        }
    }

    /// <summary>Correntes penduradas BEM perto da câmera, nas bordas: silhuetas escuras com brilho
    /// de vela, que andam muito mais que o cenário quando a câmera passeia (profundidade) e
    /// balançam — a badalada também as sacode.</summary>
    public class TitleChains : MonoBehaviour
    {
        public class C { public RectTransform rt; public RawImage img; public Vector2 anchor; public float phase, amp, w; }
        public readonly List<C> chains = new List<C>();
        public const float Depth = 2.4f;

        void LateUpdate()
        {
            float t = TitleClock.Now;
            foreach (var c in chains)
            {
                var at = TitleMotion.Forward(c.anchor, Depth);
                c.rt.anchoredPosition = new Vector2(at.x, -at.y);
                float ang = c.amp * Mathf.Sin(t * c.w + c.phase) + TitleMotion.Kick(c.anchor + new Vector2(0, 200), 0.05f, c.w);
                c.rt.localRotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
                c.img.color = new Color(1, 1, 1, TitleMotion.Reveal(c.anchor + new Vector2(0, 200), 120f));
            }
        }
    }
}

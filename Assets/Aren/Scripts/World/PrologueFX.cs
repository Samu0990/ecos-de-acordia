using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// Prólogo da abertura em motion graphics procedural (técnicas do BlumeApp do autor recriadas
    /// aqui: halftone por tamanho de ponto em células de 3 px, stop-motion a 12 q/s, glitch de
    /// fatias, queima com bordas incandescentes e partículas douradas aditivas). Tudo é desenhado
    /// por software numa textura de 480x270 ampliada em pixel art.
    ///
    /// Roteiro (24 s):
    ///   0.0  o olho de luz se abre
    ///   0.8  A NOTA — a primeira nota procura a segunda; elas se encontram e vibram
    ///   6.8  OS DOZE SINOS — o sino balança, doze marcas acendem no ritmo das badaladas
    ///  12.4  A DÉCIMA TERCEIRA — primeira cor (violeta), glitch pesado, o sino racha
    ///  14.0  A QUEIMA — a imagem pega fogo em ouro e violeta, brasas espiralam ao centro
    ///  16.0  A CANÇÃO — onda de choque, esfera de luz dourada girando
    ///  20.5  A FENDA — um rasgo negro se abre e apaga a luz
    ///  23.75 clarão → Campanula (a cutscene 3D continua)
    /// </summary>
    public class PrologueFX : MonoBehaviour
    {
        public const int W = 480, H = 270;
        const int CELL = 3, LEVELS = 7;
        // a Ruptura em si acontece na cena 3D (o som desafinando, nunca o silêncio); o prólogo fica
        // só com a harmonia: a nota, a segunda nota, a canção e os doze sinos — e se dissolve.
        public const float Duration = 12.9f;
        const float T_DISSOLVE = 12.25f;
        // o final antigo (13ª com glitch, queima, esfera e rasgo que apagava) fica desligado
        const bool ExtendedEnding = false;
        const float T_NOTE = 0.8f, T_BELL = 6.8f, T_13 = 12.4f, T_BURN = 14.0f, T_SONG = 16.0f, T_SHOCK = 17.5f, T_RIFT = 20.5f, T_COLLAPSE = 23.0f, T_FLASH = 23.75f;

        public bool Running { get; private set; }
        RawImage img; Texture2D tex;
        Color32[] px, act1Frame;
        float[] lum, tint, R, G, B;
        readonly Color32[] rowTmp = new Color32[W];
        Text line, lineR, lineC;
        float t, lastStop = -1f;
        bool stopped;

        static readonly int[] DotSide = BuildDotSide();
        static int[] BuildDotSide()
        {
            var d = new int[LEVELS + 1];
            for (int q = 0; q <= LEVELS; q++) d[q] = Mathf.RoundToInt(CELL * Mathf.Sqrt(q / (float)LEVELS));
            return d;
        }

        // ------------------------------------------------------------ montagem

        public static PrologueFX Create(Transform canvasRoot)
        {
            var go = new GameObject("Prologo", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var fx = go.AddComponent<PrologueFX>();
            fx.Build(rt);
            go.SetActive(false);
            return fx;
        }

        void Build(RectTransform root)
        {
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Prologo" };
            px = new Color32[W * H]; act1Frame = new Color32[W * H];
            lum = new float[W * H]; tint = new float[W * H];
            R = new float[W * H]; G = new float[W * H]; B = new float[W * H];
            var holder = UI.UIKit.Rect("Tela", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = W / (float)H;
            img = holder.gameObject.AddComponent<RawImage>();
            img.texture = tex; img.raycastTarget = false;
            var sharp = UI.TitleScreen.ArtMat;    // pixel nítido em qualquer resolução
            if (sharp != null) img.material = sharp;
            lineR = MakeLine(root, "TextoR", new Color(1f, 0.25f, 0.3f, 0f), false);
            lineC = MakeLine(root, "TextoC", new Color(0.3f, 0.95f, 1f, 0f), false);
            line = MakeLine(root, "Texto", new Color(UI.UIKit.Bone.r, UI.UIKit.Bone.g, UI.UIKit.Bone.b, 0f), true);
        }

        static Text MakeLine(Transform root, string name, Color c, bool shadow)
        {
            var t = UI.UIKit.Label(name, root, "", UI.UIKit.Serif, 40, c, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1700, 70), false);
            if (shadow)
            {
                // contorno escuro: legível por cima dos pontos do halftone
                var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        // ------------------------------------------------------------ roteiro

        struct Cue { public float at, dur; public string text; }
        static readonly Cue[] Lines =
        {
            new Cue { at = 1.4f, dur = 2.0f, text = "Antes da pedra, antes do mar e antes do primeiro nome," },
            new Cue { at = 3.6f, dur = 2.7f, text = "havia uma nota procurando outra nota para não ficar sozinha." },
            new Cue { at = 7.3f, dur = 4.5f, text = "Da canção nasceram os doze sinos de Campanula." },

        };

        struct Snd { public float at; public Sfx sfx; public float vol, pitch; public bool sting; }
        Snd[] sounds; bool[] played;

        static float[] TollTimes = BuildTolls();
        static float[] BuildTolls()
        {
            // doze badaladas acelerando entre 7.1 s e 12.1 s
            var r = new float[12];
            for (int k = 0; k < 12; k++) { float u = k / 11f; r[k] = 7.1f + 5.0f * (1f - (1f - u) * (1f - u)) * 0.5f + 5.0f * u * 0.5f; }
            return r;
        }

        void BuildSounds()
        {
            var list = new System.Collections.Generic.List<Snd>
            {
                new Snd { at = 0.02f, sfx = Sfx.Whoosh, vol = 0.5f, pitch = 0.45f },
                new Snd { at = 1.0f, sfx = Sfx.AbilityReady, vol = 0.45f, pitch = 0.5f },
                new Snd { at = 3.8f, sfx = Sfx.AbilityReady, vol = 0.45f, pitch = 0.6f },
                new Snd { at = 4.8f, sfx = Sfx.ChargeLevel, vol = 0.4f, pitch = 0.7f },
                new Snd { at = 6.4f, sfx = Sfx.UIConfirm, vol = 0.6f, pitch = 0.5f },

            };
            for (int k = 0; k < 12; k++) list.Add(new Snd { at = TollTimes[k], sfx = Sfx.Bell, vol = 0.28f, pitch = 0.55f + k * 0.006f });
            sounds = list.ToArray(); played = new bool[sounds.Length];
        }

        /// <summary>Toca o prólogo inteiro (24 s). Pode ser interrompido com Stop().</summary>
        public IEnumerator Play()
        {
            gameObject.SetActive(true);
            Running = true; stopped = false; t = 0f; lastStop = -1f;
            BuildSounds();
            InitParticles();
            System.Array.Clear(R, 0, R.Length); System.Array.Clear(G, 0, G.Length); System.Array.Clear(B, 0, B.Length);
            while (!stopped && t < Duration)
            {
                yield return null;
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                t += dt;
                for (int i = 0; i < sounds.Length; i++)
                    if (!played[i] && t >= sounds[i].at)
                    {
                        played[i] = true;
                        if (sounds[i].sting) ArenAudio.PlaySting(Sting.Mystery, 0.8f);
                        else ArenAudio.PlayUI(sounds[i].sfx, sounds[i].vol, sounds[i].pitch);
                    }
                RenderFrame(dt);
                UpdateText();
            }
            Stop();
        }

        public void Stop()
        {
            stopped = true; Running = false;
            if (gameObject != null) gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ quadro

        void RenderFrame(float dt)
        {
            if (t < T_BURN)
            {
                // ato 1 em stop-motion: só redesenha a cada 1/12 s (poses seguradas)
                float tq = Mathf.Floor(t * 12f) / 12f;
                if (tq != lastStop)
                {
                    lastStop = tq;
                    RenderAct1(tq);
                    if (!ExtendedEnding && tq > T_DISSOLVE)
                    {
                        // dissolve: os pontos do halftone encolhem até sumir (o som do 12º sino continua)
                        float k = 1f - Smooth((tq - T_DISSOLVE) / (Duration - T_DISSOLVE - 0.1f));
                        for (int i = 0; i < lum.Length; i++) lum[i] *= k;
                    }
                    Halftone(tq);
                    if (t < T_NOTE) Iris(t);
                    System.Array.Copy(px, act1Frame, px.Length);   // base da queima (sem glitch)
                    Glitch(GlitchAmount(tq), (int)(tq * 12f) * 7919 + 13);
                    Upload();
                }
            }
            else if (t < T_FLASH)
            {
                StepParticles(dt);
                RenderAct2(dt);
                if (t < T_SONG) Burn();
                else Compose();
                if (t < T_BURN + 0.5f) Glitch(0.6f * (1f - (t - T_BURN) / 0.5f), (int)(t * 24f) * 31 + 5);
                Upload();
            }
            else
            {
                var white = new Color32(255, 250, 240, 255);
                for (int i = 0; i < px.Length; i++) px[i] = white;
                Upload();
            }
        }

        void Upload()
        {
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        // ------------------------------------------------------------ utilidades

        static readonly float[] noiseTab = BuildNoise();
        static float[] BuildNoise()
        {
            var r = new System.Random(1234);
            var n = new float[128 * 128];
            for (int i = 0; i < n.Length; i++) n[i] = (float)r.NextDouble();
            return n;
        }
        static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = xi & 127, y0 = yi & 127, x1 = (xi + 1) & 127, y1 = (yi + 1) & 127;
            float a = noiseTab[y0 * 128 + x0], b = noiseTab[y0 * 128 + x1], c = noiseTab[y1 * 128 + x0], d = noiseTab[y1 * 128 + x1];
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }
        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3 - 2 * u); }

        uint rs = 2463534242;
        float Rnd() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs & 0xFFFFFF) / 16777216f; }

        void Glow(float cx, float cy, float sigma, float inten, float tintV = 0f)
        {
            int r = Mathf.CeilToInt(sigma * 3f);
            int x0 = Mathf.Max(0, (int)cx - r), x1 = Mathf.Min(W - 1, (int)cx + r);
            int y0 = Mathf.Max(0, (int)cy - r), y1 = Mathf.Min(H - 1, (int)cy + r);
            float k = -1f / (2f * sigma * sigma);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float v = inten * Mathf.Exp((dx * dx + dy * dy) * k);
                    int i = y * W + x;
                    lum[i] += v;
                    if (tintV > 0f) tint[i] = Mathf.Max(tint[i], tintV * Mathf.Min(1f, v * 2f));
                }
            }
        }

        void Ring(float cx, float cy, float rad, float width, float inten)
        {
            if (inten <= 0.005f) return;
            int r = Mathf.CeilToInt(rad + width * 3f);
            int x0 = Mathf.Max(0, (int)cx - r), x1 = Mathf.Min(W - 1, (int)cx + r);
            int y0 = Mathf.Max(0, (int)cy - r), y1 = Mathf.Min(H - 1, (int)cy + r);
            float inner = Mathf.Max(0f, rad - width * 3f);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < inner * inner) continue;
                    float e = (Mathf.Sqrt(d2) - rad) / width;
                    if (e > 3f || e < -3f) continue;
                    lum[y * W + x] += inten * Mathf.Exp(-e * e);
                }
            }
        }

        void Disc(float cx, float cy, float rad, float val, float tintV = 0f)
        {
            int r = Mathf.CeilToInt(rad + 1);
            for (int y = Mathf.Max(0, (int)cy - r); y <= Mathf.Min(H - 1, (int)cy + r); y++)
                for (int x = Mathf.Max(0, (int)cx - r); x <= Mathf.Min(W - 1, (int)cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(rad + 0.5f - d);
                    if (a <= 0f) continue;
                    int i = y * W + x;
                    lum[i] = Mathf.Lerp(lum[i], val, a);
                    if (tintV > 0f) tint[i] = Mathf.Max(tint[i], tintV * a);
                }
        }

        // ------------------------------------------------------------ ato 1 (halftone)

        static readonly float[] BellV = { 0f, .05f, .12f, .30f, .55f, .80f, .93f, 1f };
        static readonly float[] BellHW = { 0f, 22f, 30f, 34f, 38f, 52f, 64f, 68f };
        const float BellH = 150f;
        static readonly Vector2 BellPivot = new Vector2(240, 36);
        static readonly Vector2 TickCenter = new Vector2(240, 125);
        static readonly Vector2[] Crack = { new Vector2(-6, 14), new Vector2(9, 42), new Vector2(-5, 70), new Vector2(13, 98), new Vector2(1, 124), new Vector2(17, 152) };

        static float BellHalfWidth(float v)
        {
            if (v <= 0f) return 0f;
            if (v >= 1f) return BellHW[BellHW.Length - 1];
            for (int i = 1; i < BellV.Length; i++)
                if (v <= BellV[i])
                {
                    float u = (v - BellV[i - 1]) / (BellV[i] - BellV[i - 1]);
                    if (i == 1) return BellHW[1] * Mathf.Sqrt(u);          // coroa arredondada
                    return Mathf.Lerp(BellHW[i - 1], BellHW[i], u * u * (3 - 2 * u));
                }
            return BellHW[BellHW.Length - 1];
        }

        float BellAngle(float tq)
        {
            if (ExtendedEnding && tq >= T_13) return 0.05f;   // congela torto depois da 13ª
            // balanço que acompanha as badaladas (cada badalada = um extremo)
            float a = 0f;
            for (int k = 0; k < 12; k++) if (tq >= TollTimes[k]) a = (k % 2 == 0 ? 1f : -1f);
            float swing = 0.22f * Mathf.Sin((tq - T_BELL) * 2.4f);
            return Mathf.Lerp(swing, a * 0.26f, tq >= TollTimes[0] ? 0.6f : 0f);
        }

        int TollsDone(float tq) { int n = 0; for (int k = 0; k < 12; k++) if (tq >= TollTimes[k]) n++; return n; }

        float GlitchAmount(float tq)
        {
            float g = 0f;
            foreach (var c in Lines) if (tq >= c.at && tq < c.at + 0.25f) g = Mathf.Max(g, 0.35f * (1f - (tq - c.at) / 0.25f));
            for (int k = 0; k < 12; k++) if (tq >= TollTimes[k] && tq < TollTimes[k] + 0.12f) g = Mathf.Max(g, 0.18f);
            if (ExtendedEnding && tq >= T_13) g = Mathf.Max(g, Mathf.Max(0.25f, 1.1f * Mathf.Exp(-(tq - T_13) / 0.5f)));
            if (tq >= 6.3f && tq < 6.9f) g = Mathf.Max(g, 0.3f);
            return g;
        }

        void RenderAct1(float tq)
        {
            // fundo: vinheta + grão que respira + campo de som concêntrico durante os sinos
            bool bellAct = tq >= T_BELL - 0.2f;
            float field = bellAct ? Smooth((tq - T_BELL + 0.2f) / 1f) : 0f;
            for (int y = 0; y < H; y++)
            {
                float dy = y - 135f;
                for (int x = 0; x < W; x++)
                {
                    float dx = x - 240f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // escuro de verdade: só o miolo acende uns pontos (senão o halftone vira grade)
                    float v = 0.008f + 0.07f * Mathf.Exp(-d * d / (2f * 140f * 140f)) + 0.04f * (Noise(x * 0.05f + tq * 0.3f, y * 0.05f) - 0.35f);
                    if (field > 0f) v += field * 0.045f * Mathf.Max(0f, Mathf.Sin(d * 0.18f - tq * 5f)) * Mathf.Exp(-d / 180f);
                    int i = y * W + x;
                    lum[i] = v; tint[i] = 0f;
                }
            }
            if (tq < T_BELL) RenderNotes(tq);
            else RenderBell(tq);
        }

        void RenderNotes(float tq)
        {
            var C = new Vector2(240, 128);
            Vector2 A = new Vector2(150 + 20f * Mathf.Sin(tq * 0.9f), 150 + 10f * Mathf.Sin(tq * 1.3f));
            Vector2 Bp = new Vector2(330 + 16f * Mathf.Sin(tq * 1.1f + 1f), 112 + 9f * Mathf.Sin(tq * 1.5f));
            bool hasB = tq >= 3.6f;
            if (tq >= 4.8f)
            {
                // as duas se aproximam e orbitam uma à outra até virar uma só
                float k = Smooth((tq - 4.8f) / 1.6f);
                float ph = (tq - 4.8f) * 2.6f;
                float r = Mathf.Lerp(80f, 3f, k);
                var o = new Vector2(Mathf.Cos(ph), Mathf.Sin(ph) * 0.6f) * r;
                A = Vector2.Lerp(A, C + o, k); Bp = Vector2.Lerp(Bp, C - o, k);
            }
            float appearA = Smooth((tq - T_NOTE - 0.1f) / 0.4f);
            // ondas que cada nota emite (procurando a outra)
            for (float e = 1.0f; e <= tq; e += 0.9f)
            {
                float age = tq - e;
                if (age < 1.8f) Ring(A.x, A.y, 6f + age * 70f, 1.6f, 0.55f * (1f - age / 1.8f) * appearA);
            }
            if (hasB)
                for (float e = 3.8f; e <= tq; e += 0.9f)
                {
                    float age = tq - e;
                    if (age < 1.8f) Ring(Bp.x, Bp.y, 6f + age * 70f, 1.6f, 0.55f * (1f - age / 1.8f));
                }
            // a corda que se forma entre as duas (onda estacionária)
            if (hasB && tq >= 4.6f)
            {
                float amp = 9f * Smooth((tq - 4.6f) / 0.5f) * (1f - Smooth((tq - 6.2f) / 0.3f));
                var d = Bp - A; var n = new Vector2(-d.y, d.x).normalized;
                for (int s = 0; s <= 48; s++)
                {
                    float u = s / 48f;
                    var p = A + d * u + n * Mathf.Sin(u * Mathf.PI * 3f) * amp * Mathf.Sin(tq * 9f);
                    Disc(p.x, p.y, 1.1f, 0.85f);
                }
            }
            float pulse = 0.85f + 0.15f * Mathf.Sin(tq * 6f);
            Glow(A.x, A.y, 7f, 0.9f * pulse * appearA); Disc(A.x, A.y, 2.2f, appearA);
            if (hasB) { float ab = Smooth((tq - 3.6f) / 0.4f); Glow(Bp.x, Bp.y, 7f, 0.9f * pulse * ab); Disc(Bp.x, Bp.y, 2.2f, ab); }
            if (tq >= 6.3f) Glow(C.x, C.y, 10f + (tq - 6.3f) * 60f, 1.6f * (1f - Smooth((tq - 6.4f) / 0.4f)));   // fusão: clarão
        }

        void RenderBell(float tq)
        {
            float appear = Smooth((tq - T_BELL) / 0.35f);
            float th = BellAngle(tq);
            float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            int done = TollsDone(tq);
            bool cracked = ExtendedEnding && tq >= T_13 + 0.05f;
            float crackLen = cracked ? Smooth((tq - T_13 - 0.05f) / 0.25f) : 0f;
            // ondas das badaladas saindo da boca do sino
            var mouth = BellPivot + Rot(new Vector2(0, 14 + BellH), th);
            for (int k = 0; k < 12; k++)
            {
                float age = tq - TollTimes[k];
                if (age >= 0f && age < 1.8f) Ring(mouth.x, mouth.y, 10f + age * 160f, 2.2f, 0.55f * (1f - age / 1.8f));
            }
            if (ExtendedEnding && tq >= T_13)
            {
                float age = tq - T_13;
                if (age < 1.6f) Ring(240, 20, 6f + age * 230f, 3f, 0.9f * (1f - age / 1.6f));
            }
            // o sino (silhueta com sombreamento cilíndrico, faixas, boca e badalo)
            int bx0 = 240 - 125, bx1 = 240 + 125, by0 = 18, by1 = 215;
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    float dx = x - BellPivot.x, dy = y - BellPivot.y;
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    float by = ly - 14f, bxx = lx;
                    int i = y * W + x;
                    float v = -1f;
                    if (by >= -14f && by < 0f && Mathf.Abs(bxx) < 9f) v = Mathf.Abs(bxx) > 7.5f ? 0.15f : 0.55f;   // alça
                    else if (by >= 0f && by <= BellH)
                    {
                        float hw = BellHalfWidth(by / BellH);
                        float edge = hw - Mathf.Abs(bxx);
                        if (edge > -0.5f)
                        {
                            float u = bxx / Mathf.Max(hw, 1f), vv = by / BellH;
                            // tons médios (pontos de tamanho 1-2) para o halftone desenhar o volume
                            float s = 0.16f + 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                            s += 0.42f * Mathf.Exp(-((u + 0.42f) / 0.11f) * ((u + 0.42f) / 0.11f));          // brilho
                            s += 0.2f * Mathf.Exp(-((vv - 0.28f) / 0.012f) * ((vv - 0.28f) / 0.012f));       // faixas
                            s += 0.2f * Mathf.Exp(-((vv - 0.86f) / 0.012f) * ((vv - 0.86f) / 0.012f));
                            if (vv > 0.93f) s += 0.18f;                                                       // lábio
                            s *= 0.55f + 0.45f * Smooth((1f - Mathf.Abs(u)) / 0.18f);
                            v = Mathf.Lerp(lum[i], s, Mathf.Clamp01(edge + 0.5f));
                        }
                    }
                    // boca (elipse escura com aro claro) e badalo
                    float ey = (by - BellH - 1f) / 8f, ex = bxx / 68f;
                    float e = ex * ex + ey * ey;
                    if (e < 1f && by > BellH - 8f) v = by > BellH + 1f || e < 0.75f ? 0.05f : 0.85f;
                    float clx = Mathf.Sin(-th * 2f) * 10f, cly = BellH + 4f;
                    if ((bxx - clx) * (bxx - clx) + (by - cly) * (by - cly) < 36f) v = 0.5f;
                    if (v >= 0f) lum[i] = Mathf.Lerp(lum[i], v, appear);
                    // a rachadura da 13ª (núcleo preto, borda violeta)
                    if (crackLen > 0f && by > 0f && by < BellH + 2f)
                    {
                        float dc = CrackDist(bxx, by, crackLen);
                        if (dc < 1.3f) lum[i] = 0f;
                        else if (dc < 3.6f) { lum[i] = 0.95f; tint[i] = 1f; }
                    }
                }
            // as doze marcas em volta (acendem a cada badalada) e a 13ª, violeta, fora do anel
            for (int k = 0; k < 12; k++)
            {
                float a = -Mathf.PI / 2f + k * Mathf.PI / 6f;
                var p = TickCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 112f;
                if (k < done) { Disc(p.x, p.y, 3.6f, appear); if (tq - TollTimes[k] < 0.25f) Glow(p.x, p.y, 5f, 0.8f); }
                else Ring(p.x, p.y, 3.4f, 0.6f, 0.5f * appear);
            }
            if (ExtendedEnding && tq >= T_13)
            {
                var p13 = TickCenter + new Vector2(0, -128f);
                float pl = 0.75f + 0.25f * Mathf.Sin(tq * 14f);
                Glow(p13.x, p13.y, 7f, 1.2f * pl, 1f);
                Disc(p13.x, p13.y, 4.2f, 1f, 1f);
            }
        }

        static Vector2 Rot(Vector2 v, float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }

        static float CrackDist(float x, float y, float len)
        {
            float best = 1e9f;
            int segs = Crack.Length - 1;
            float upto = len * segs;
            for (int k = 0; k < segs; k++)
            {
                if (k >= upto) break;
                Vector2 a = Crack[k], b = Crack[k + 1];
                float part = Mathf.Clamp01(upto - k);
                b = Vector2.Lerp(a, b, part);
                Vector2 ab = b - a, ap = new Vector2(x, y) - a;
                float u = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float d = (ap - ab * u).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        void Halftone(float tq)
        {
            var bone = new Color32(236, 228, 212, 255);
            var violet = new Color32(178, 96, 255, 255);
            var bg = new Color32(7, 6, 10, 255);
            uint seed = (uint)(tq * 1000f) + 77u;
            for (int cy = 0; cy < H; cy += CELL)
                for (int cx = 0; cx < W; cx += CELL)
                {
                    int sx = Mathf.Min(cx + 1, W - 1), sy = Mathf.Min(cy + 1, H - 1);
                    int si = sy * W + sx;
                    seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                    float n = ((seed & 0xFFFF) / 65535f - 0.5f) * 0.06f;
                    float L = Mathf.Clamp01(lum[si] * 1.05f + n);
                    int q = Mathf.RoundToInt(L * LEVELS);
                    int side = DotSide[q];
                    int off = (CELL - side) >> 1;
                    var dot = tint[si] > 0.05f ? Color32.Lerp(bone, violet, tint[si]) : bone;
                    int yMax = Mathf.Min(cy + CELL, H), xMax = Mathf.Min(cx + CELL, W);
                    for (int y = cy; y < yMax; y++)
                    {
                        int row = (H - 1 - y) * W;   // textura cresce para cima
                        for (int x = cx; x < xMax; x++)
                        {
                            bool inDot = side > 0 && x >= cx + off && x < cx + off + side && y >= cy + off && y < cy + off + side;
                            px[row + x] = inDot ? dot : bg;
                        }
                    }
                    if (q == 0 && (seed & 0xFF) < 5)   // faísca solta no escuro, como o halftone do BlumeApp
                    {
                        int x = cx + (int)((seed >> 8) % CELL), y = cy + (int)((seed >> 12) % CELL);
                        if (x < W && y < H) px[(H - 1 - y) * W + x] = new Color32(120, 116, 110, 255);
                    }
                }
        }

        /// <summary>Abertura: um ponto de luz vira uma linha e se abre como um olho.</summary>
        void Iris(float tt)
        {
            float w = Mathf.Lerp(2f, W * 0.62f, Smooth(tt / 0.28f));
            float h = Mathf.Lerp(0.6f, H * 0.75f, Smooth((tt - 0.18f) / 0.6f));
            var rim = new Color32(255, 248, 232, 255);
            var black = new Color32(0, 0, 0, 255);
            for (int y = 0; y < H; y++)
            {
                float dy = Mathf.Abs(y - 135f);
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - 240f) / w;
                    float lim = dx * dx < 1f ? h * Mathf.Sqrt(1f - dx * dx) : -1f;
                    int i = (H - 1 - y) * W + x;
                    if (dy > lim + 1.2f) px[i] = black;
                    else if (dy > lim - 1.2f || tt < 0.2f) px[i] = rim;
                }
            }
        }

        void Glitch(float k, int seed)
        {
            if (k < 0.02f) return;
            rs = (uint)(seed * 2654435761u) | 1u;
            int n = 2 + (int)(k * 8f);
            for (int s = 0; s < n; s++)
            {
                int y0 = (int)(Rnd() * H), h = 2 + (int)(Rnd() * (4 + 22 * k));
                int dx = (int)((Rnd() - 0.5f) * 2f * k * 28f);
                if (dx == 0) continue;
                for (int y = y0; y < Mathf.Min(H, y0 + h); y++)
                {
                    int row = y * W;
                    System.Array.Copy(px, row, rowTmp, 0, W);
                    for (int x = 0; x < W; x++) px[row + x] = rowTmp[((x - dx) % W + W) % W];
                }
            }
            int m = (int)(k * 10f);
            for (int b = 0; b < m; b++)
            {
                int bw = 4 + (int)(Rnd() * 22), bh = 2 + (int)(Rnd() * 9);
                int bx = (int)(Rnd() * (W - bw)), by = (int)(Rnd() * (H - bh));
                var c = Rnd() > 0.45f ? new Color32(0, 0, 0, 255) : new Color32(236, 228, 212, 255);
                for (int y = by; y < by + bh; y++) for (int x = bx; x < bx + bw; x++) px[y * W + x] = c;
            }
            if (k > 0.75f && Rnd() < 0.35f)
                for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color32((byte)(255 - c.r), (byte)(255 - c.g), (byte)(255 - c.b), 255); }
        }

        // ------------------------------------------------------------ ato 2 (partículas)

        const int N = 2200, NDust = 120;
        float[] qx, qy, qvx, qvy, qEat, qDelay, qBright, qfx, qfy;
        byte[] qState;   // 0 morto, 1 espiral, 2 indo para a esfera, 3 esfera, 4 sendo engolido
        float[] sxU, syU, szU;   // pontos da esfera unitária em anéis de latitude
        float[] dx_, dy_;
        static readonly Vector2 C2 = new Vector2(240, 132);
        float[] kernel; const int KR = 20;

        void InitParticles()
        {
            qx = new float[N]; qy = new float[N]; qvx = new float[N]; qvy = new float[N]; qfx = new float[N]; qfy = new float[N];
            qEat = new float[N]; qDelay = new float[N]; qBright = new float[N]; qState = new byte[N];
            dx_ = new float[NDust]; dy_ = new float[NDust];
            for (int i = 0; i < NDust; i++) { dx_[i] = Rnd() * W; dy_[i] = Rnd() * H; }
            sxU = new float[N]; syU = new float[N]; szU = new float[N];
            int rings = 17, idx = 0;
            float total = 0f;
            for (int r = 0; r < rings; r++) total += Mathf.Cos(Mathf.Lerp(-1.35f, 1.35f, r / (float)(rings - 1)));
            for (int r = 0; r < rings && idx < N; r++)
            {
                float lat = Mathf.Lerp(-1.35f, 1.35f, r / (float)(rings - 1));
                int count = Mathf.RoundToInt(N * Mathf.Cos(lat) / total);
                for (int k = 0; k < count && idx < N; k++, idx++)
                {
                    float lon = k / (float)count * Mathf.PI * 2f;
                    sxU[idx] = Mathf.Cos(lat) * Mathf.Cos(lon); syU[idx] = Mathf.Sin(lat); szU[idx] = Mathf.Cos(lat) * Mathf.Sin(lon);
                }
            }
            for (; idx < N; idx++) { sxU[idx] = 0; syU[idx] = 1; szU[idx] = 0; }
            nextSpawn = 0;
            for (int i = 0; i < N; i++)
            {
                qState[i] = 0;
                qDelay[i] = 0.1f + Rnd() * 0.6f;
                qEat[i] = Mathf.Lerp(T_RIFT + 0.4f, T_COLLAPSE, Mathf.Pow(Rnd(), 0.8f));
                qBright[i] = 0.6f + Rnd() * 0.4f;
            }
            if (kernel == null)
            {
                kernel = new float[(2 * KR + 1) * (2 * KR + 1)];
                for (int y = -KR; y <= KR; y++) for (int x = -KR; x <= KR; x++)
                        kernel[(y + KR) * (2 * KR + 1) + x + KR] = Mathf.Exp(-(x * x + y * y) / (2f * 5.5f * 5.5f));
            }
        }

        void SpawnSwirl(int i, float x, float y)
        {
            qState[i] = 1; qx[i] = x; qy[i] = y; qvx[i] = 0; qvy[i] = 0;
        }

        int nextSpawn;
        void StepParticles(float dt)
        {
            // queima: brasas nascem nas bordas incandescentes (Burn() chama SpawnSwirl); no ato 2
            // completa o enxame num anel ao redor do centro
            if (t >= T_SONG)
                while (nextSpawn < N)
                {
                    float a = Rnd() * 6.283f, r = 40f + Rnd() * 190f;
                    SpawnSwirl(nextSpawn++, C2.x + Mathf.Cos(a) * r, C2.y + Mathf.Sin(a) * r * 0.75f);
                }
            float th = t * 0.7f, ca = Mathf.Cos(th), sa = Mathf.Sin(th);
            float tilt = 0.35f + 0.4f * Smooth((t - 18f) / 4f), ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            float Rs = 76f + 4f * Mathf.Sin(t * 2f);
            float riftA = RiftA();
            for (int i = 0; i < N; i++)
            {
                byte s = qState[i];
                if (s == 0) continue;
                // posição na esfera (girando, eixo tombando — os anéis vão se abrindo)
                float X = sxU[i] * ca - szU[i] * sa, Z = sxU[i] * sa + szU[i] * ca, Y = syU[i];
                float Y2 = Y * ct - Z * st, Z2 = Y * st + Z * ct;
                float f = 260f / (260f + Z2 * Rs);
                float tx = C2.x + X * Rs * f, ty = C2.y - Y2 * Rs * f;
                if (s == 1)
                {
                    float rx = qx[i] - C2.x, ry = qy[i] - C2.y;
                    float r = Mathf.Sqrt(rx * rx + ry * ry) + 0.001f;
                    float w = 2.6f / (0.4f + r / 90f);
                    float a = Mathf.Atan2(ry, rx) + w * dt;
                    r = Mathf.Max(6f + (i % 13), r - r * 1.1f * dt);
                    qx[i] = C2.x + Mathf.Cos(a) * r; qy[i] = C2.y + Mathf.Sin(a) * r;
                    if (t >= T_SHOCK)
                    {
                        qState[i] = 2;
                        qfx[i] = qx[i]; qfy[i] = qy[i];
                        qvx[i] = rx / r * 160f; qvy[i] = ry / r * 160f;
                    }
                }
                else if (s == 2)
                {
                    // voo livre empurrado pela onda de choque, misturando até o lugar na esfera
                    qfx[i] += qvx[i] * dt; qfy[i] += qvy[i] * dt;
                    qvx[i] *= 1f - 3f * dt; qvy[i] *= 1f - 3f * dt;
                    float m = Smooth((t - T_SHOCK - qDelay[i]) / 1.2f);
                    qx[i] = Mathf.Lerp(qfx[i], tx, m); qy[i] = Mathf.Lerp(qfy[i], ty, m);
                    if (m >= 1f) qState[i] = 3;
                }
                else if (s == 3)
                {
                    qx[i] = tx; qy[i] = ty;
                    if (t >= qEat[i] && riftA > 1f) { qState[i] = 4; qvx[i] = 0; qvy[i] = 0; }
                }
                else if (s == 4)
                {
                    // sugado para o rasgo, girando, e apagado ao entrar
                    float rx = C2.x - qx[i], ry = C2.y - qy[i];
                    float r = Mathf.Sqrt(rx * rx + ry * ry) + 0.001f;
                    qvx[i] += (rx / r * 260f - ry / r * 90f) * dt; qvy[i] += (ry / r * 260f + rx / r * 90f) * dt;
                    qx[i] += qvx[i] * dt; qy[i] += qvy[i] * dt;
                    if (InRift(qx[i], qy[i], riftA) || r < 3f) qState[i] = 0;
                }
                if (t >= T_COLLAPSE + 0.3f) qState[i] = 0;
            }
        }

        float RiftA()
        {
            if (t < T_RIFT) return 0f;
            float open = 150f * (1f - Mathf.Pow(1f - Mathf.Clamp01((t - T_RIFT) / 1.1f), 3f));
            open *= 1f + 0.05f * Mathf.Sin(t * 7f);
            if (t > T_COLLAPSE) open *= 1f - Smooth((t - T_COLLAPSE) / 0.6f);
            return open;
        }

        const float RiftAngle = -0.2f;
        static bool InRift(float x, float y, float a)
        {
            if (a < 1f) return false;
            float b = 1.5f + a * 0.07f;
            float c = Mathf.Cos(RiftAngle), s = Mathf.Sin(RiftAngle);
            float dx = x - C2.x, dy = y - C2.y;
            float lx = dx * c + dy * s, ly = -dx * s + dy * c;
            return (lx / a) * (lx / a) + (ly / b) * (ly / b) < 1f;
        }

        void Add(int x, int y, float r, float g, float b)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            R[i] += r; G[i] += g; B[i] += b;
        }

        void AddKernel(float cx, float cy, float inten, float r, float g, float b, int rad = KR)
        {
            int ix = Mathf.RoundToInt(cx), iy = Mathf.RoundToInt(cy);
            for (int y = -rad; y <= rad; y++)
            {
                int yy = iy + y; if (yy < 0 || yy >= H) continue;
                for (int x = -rad; x <= rad; x++)
                {
                    int xx = ix + x; if (xx < 0 || xx >= W) continue;
                    float k = kernel[(y + KR) * (2 * KR + 1) + x + KR] * inten;
                    int i = yy * W + xx;
                    R[i] += k * r; G[i] += k * g; B[i] += k * b;
                }
            }
        }

        void RenderAct2(float dt)
        {
            // rastro: o quadro anterior esmaece (as partículas desenham riscos ao girar)
            float decay = Mathf.Exp(-dt * (t > T_COLLAPSE ? 14f : 7f));
            for (int i = 0; i < R.Length; i++)
            {
                if (R[i] < 0f) { R[i] = G[i] = B[i] = 0f; continue; }   // o "apagado" da Fenda é redesenhado a cada quadro
                R[i] *= decay; G[i] *= decay; B[i] *= decay;
            }
            // poeira dourada de fundo
            if (t >= T_BURN + 0.3f)
                for (int i = 0; i < NDust; i++)
                {
                    dx_[i] += Mathf.Sin(i * 1.7f + t * 0.4f) * 6f * dt; dy_[i] -= (3f + (i % 5)) * dt;
                    if (dy_[i] < 0) dy_[i] += H;
                    Add((int)dx_[i], (int)dy_[i], 0.05f, 0.032f, 0.012f);
                }
            float riftA = RiftA();
            for (int i = 0; i < N; i++)
            {
                byte s = qState[i];
                if (s == 0) continue;
                float br = qBright[i];
                if (s == 3 || s == 2)
                {
                    // na esfera: a frente brilha mais que o fundo
                    float th = t * 0.7f;
                    float Z = sxU[i] * Mathf.Sin(th) + szU[i] * Mathf.Cos(th);
                    br *= 0.4f + 0.6f * (0.5f - Z * 0.5f);
                }
                if (s == 4) br *= 1.3f;
                float r = 0.55f * br, g = 0.36f * br, b = 0.12f * br;
                if (s == 4) { r = 0.42f * br; g = 0.22f * br; b = 0.6f * br; }   // violeta ao ser sugada
                int ix = (int)qx[i], iy = (int)qy[i];
                Add(ix, iy, r, g, b);
                if (br > 0.75f) { Add(ix + 1, iy, r * 0.3f, g * 0.3f, b * 0.3f); Add(ix - 1, iy, r * 0.3f, g * 0.3f, b * 0.3f); Add(ix, iy + 1, r * 0.3f, g * 0.3f, b * 0.3f); Add(ix, iy - 1, r * 0.3f, g * 0.3f, b * 0.3f); }
            }
            // núcleo da Canção, espigão vertical e satélites
            if (t >= T_SHOCK)
            {
                float core = Smooth((t - T_SHOCK) / 0.8f) * (1f - Smooth((t - 22.5f) / 0.25f)) * (0.85f + 0.15f * Mathf.Sin(t * 5f));
                if (core > 0.01f)
                {
                    AddKernel(C2.x, C2.y, 0.22f * core, 1f, 0.82f, 0.5f);
                    float L = 95f * Smooth((t - 18f) / 2f);
                    for (int y = -(int)L; y <= (int)L; y++)
                    {
                        float k = (1f - Mathf.Abs(y) / Mathf.Max(L, 1f)); k *= k * 0.16f * core;
                        Add((int)C2.x, (int)C2.y + y, k, k * 0.85f, k * 0.6f);
                    }
                    float ang = t * 0.5f;
                    for (int s = -70; s <= 70; s++)
                    {
                        float k = (1f - Mathf.Abs(s) / 70f) * 0.05f * core;
                        Add((int)(C2.x + Mathf.Cos(ang) * s), (int)(C2.y + Mathf.Sin(ang) * s * 0.35f), k, k * 0.8f, k * 0.5f);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float a = t * 0.9f + k * 2.09f;
                        AddKernel(C2.x + Mathf.Cos(a) * 118f, C2.y + Mathf.Sin(a) * 34f, 0.05f * core, 1f, 0.8f, 0.5f, 6);
                    }
                }
                // onda de choque
                float sa = t - T_SHOCK;
                if (sa < 0.8f)
                {
                    float rr = sa * 300f, inten = 0.5f * (1f - sa / 0.8f);
                    for (int k = 0; k < 360; k++)
                    {
                        float a = k * Mathf.Deg2Rad;
                        Add((int)(C2.x + Mathf.Cos(a) * rr), (int)(C2.y + Mathf.Sin(a) * rr * 0.8f), inten, inten * 0.8f, inten * 0.5f);
                    }
                }
                if (t >= 22.5f && t < 22.8f) AddKernel(C2.x, C2.y, 0.5f * (1f - (t - 22.5f) / 0.3f), 0.6f, 0.3f, 1f);   // núcleo engolido: lampejo violeta
            }
            // a Fenda: um rasgo que não brilha — apaga (o que estiver dentro vira preto)
            if (riftA > 1f)
            {
                float b = 1.5f + riftA * 0.07f;
                float c = Mathf.Cos(RiftAngle), s = Mathf.Sin(RiftAngle);
                int ext = (int)riftA + 6;
                for (int y = (int)C2.y - 30; y <= (int)C2.y + 30; y++)
                {
                    if (y < 0 || y >= H) continue;
                    for (int x = (int)C2.x - ext; x <= (int)C2.x + ext; x++)
                    {
                        if (x < 0 || x >= W) continue;
                        float dx = x - C2.x, dy = y - C2.y;
                        float lx = dx * c + dy * s, ly = -dx * s + dy * c;
                        float e = (lx / riftA) * (lx / riftA) + (ly / b) * (ly / b);
                        int i = y * W + x;
                        if (e < 1f) { R[i] = G[i] = B[i] = -0.2f; }
                        else if (e < 1.7f)
                        {
                            float k = (1.7f - e) / 0.7f * (0.35f + 0.15f * Mathf.Sin(t * 23f + x * 0.3f));
                            R[i] += 0.3f * k; G[i] += 0.12f * k; B[i] += 0.55f * k;
                        }
                    }
                }
            }
        }

        // curva de tom (1 - e^-1.5v) em tabela: 3 exponenciais por pixel custariam caro em Mono
        static readonly byte[] ToneR = BuildTone(6), ToneG = BuildTone(4), ToneB = BuildTone(3);
        static byte[] BuildTone(int floor)
        {
            var lut = new byte[1024];
            for (int i = 0; i < 1024; i++) lut[i] = (byte)(floor + (255 - floor) * (1f - Mathf.Exp(-(i / 256f) * 1.5f)));
            return lut;
        }

        Color32 Tone(int i)
        {
            if (R[i] < 0f) return new Color32(0, 0, 0, 255);
            int r = Mathf.Min(1023, (int)(R[i] * 256f)), g = Mathf.Min(1023, (int)(G[i] * 256f)), b = Mathf.Min(1023, (int)(B[i] * 256f));
            return new Color32(ToneR[r], ToneG[g], ToneB[b], 255);
        }

        void Compose()
        {
            float fadeOut = t > T_COLLAPSE + 0.55f ? 0f : 1f;
            for (int y = 0; y < H; y++)
            {
                int row = (H - 1 - y) * W, src = y * W;
                for (int x = 0; x < W; x++)
                    px[row + x] = fadeOut > 0f ? Tone(src + x) : new Color32(0, 0, 0, 255);
            }
        }

        /// <summary>A imagem do sino rachado pega fogo a partir da rachadura: buracos com borda
        /// incandescente (ouro → violeta) mostram o ato 2 por baixo; as bordas soltam brasas.</summary>
        void Burn()
        {
            float th = Mathf.Lerp(-0.5f, 1.15f, Smooth((t - T_BURN) / 1.9f));
            var gold = new Color32(255, 196, 96, 255);
            var vio = new Color32(176, 90, 255, 255);
            for (int y = 0; y < H; y++)
            {
                int row = (H - 1 - y) * W, src = y * W;
                // a rachadura (sino parado) passa perto de x≈240 + desvio da linha
                float cxLine = 240f + CrackX(y);
                for (int x = 0; x < W; x++)
                {
                    float f = 0.6f * Noise(x * 0.035f, y * 0.035f) + 0.4f * Noise(x * 0.11f + 7f, y * 0.11f) - 0.45f * Mathf.Exp(-Mathf.Abs(x - cxLine) / 26f);
                    int i = row + x;
                    if (f < th - 0.07f) px[i] = Tone(src + x);
                    else if (f < th)
                    {
                        float k = (th - f) / 0.07f;
                        px[i] = Color32.Lerp(gold, vio, k);
                        if (nextSpawn < N && Rnd() < 0.018f) SpawnSwirl(nextSpawn++, x, y);
                    }
                    else
                    {
                        var c = act1Frame[i];
                        float heat = Mathf.Clamp01(1f - (f - th) / 0.12f);   // escurece e esquenta perto do fogo
                        px[i] = heat > 0f ? Color32.Lerp(c, new Color32(90, 40, 20, 255), heat * 0.6f) : c;
                    }
                }
            }
        }

        static float CrackX(int y)
        {
            // rachadura no espaço do sino (parado num ângulo leve) projetada na tela
            float by = y - BellPivot.y - 14f;
            if (by < 0f) return 0f;
            for (int k = 0; k < Crack.Length - 1; k++)
                if (by <= Crack[k + 1].y)
                {
                    float u = Mathf.InverseLerp(Crack[k].y, Crack[k + 1].y, by);
                    return Mathf.Lerp(Crack[k].x, Crack[k + 1].x, u) + by * 0.05f;
                }
            return Crack[Crack.Length - 1].x;
        }

        // ------------------------------------------------------------ texto (decodifica com glitch)

        const string Glyphs = "#%&*+=<>/\\|ΔΞΛΣΩ§¤";
        readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(96);

        void UpdateText()
        {
            Cue cur = default; bool has = false;
            foreach (var c in Lines) if (t >= c.at && t < c.at + c.dur + 0.35f) { cur = c; has = true; }
            if (!has) { SetLineAlpha(0f, 0f); return; }
            float age = t - cur.at;
            float alpha = Mathf.Clamp01(age / 0.15f) * (1f - Mathf.Clamp01((age - cur.dur) / 0.35f));
            // decodifica: cada letra embaralha antes de assentar
            sb.Length = 0;
            uint h = (uint)(Mathf.FloorToInt(t * 30f) * 2654435761u);
            for (int i = 0; i < cur.text.Length; i++)
            {
                char ch = cur.text[i];
                float reveal = i * 0.011f + ((i * 37) % 11) * 0.014f;
                if (ch != ' ' && age < reveal)
                {
                    h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                    sb.Append(Glyphs[(int)(h % (uint)Glyphs.Length)]);
                }
                else sb.Append(ch);
            }
            string s = sb.ToString();
            line.text = lineR.text = lineC.text = s;
            float g = Mathf.Clamp01(1f - age / 0.5f) + (ExtendedEnding && t >= T_13 && t < T_13 + 1.2f ? 0.6f : 0f);
            SetLineAlpha(alpha, g);
            float jx = g > 0.05f ? (Mathf.PerlinNoise(t * 40f, 3f) - 0.5f) * 10f * g : 0f;
            line.rectTransform.anchoredPosition = new Vector2(jx, 140);
            lineR.rectTransform.anchoredPosition = new Vector2(jx - 4f * g, 140 + 1f);
            lineC.rectTransform.anchoredPosition = new Vector2(jx + 4f * g, 140 - 1f);
        }

        void SetLineAlpha(float a, float glitch)
        {
            UI.UIKit.SetAlpha(line, a);
            UI.UIKit.SetAlpha(lineR, a * 0.6f * Mathf.Clamp01(glitch));
            UI.UIKit.SetAlpha(lineC, a * 0.6f * Mathf.Clamp01(glitch));
        }
    }
}

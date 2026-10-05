using System;

namespace Aren
{
    /// <summary>
    /// Síntese offline (thread de fundo) da paisagem sonora da noite da Ruptura. Regra de lore:
    /// o assustador NÃO é o silêncio — é o som ficar errado. Nada de bitcrush, stutter, rádio ou
    /// VHS: a corrupção é musical e física (parciais que saem da série, caudas que oscilam numa
    /// frequência que não deveria existir, batimentos, notas que soam tempo demais, ecos que
    /// chegam ANTES do som, sinos invertidos). Tudo mono 44,1 kHz; reverb de Schroeder embutido.
    /// </summary>
    public static class RuptureSynth
    {
        public const int SR = 44100;
        const double TAU = Math.PI * 2.0;

        public static float[] New(float sec) => new float[Math.Max(1, (int)(sec * SR))];

        // ------------------------------------------------------------ utilidades

        public static void Gain(float[] x, float g) { for (int i = 0; i < x.Length; i++) x[i] *= g; }

        public static void Normalize(float[] x, float peak = 0.9f)
        {
            float m = 1e-6f;
            for (int i = 0; i < x.Length; i++) { float a = Math.Abs(x[i]); if (a > m) m = a; }
            Gain(x, peak / m);
        }

        public static void Lowpass(float[] x, float hz)
        {
            float a = (float)(1.0 - Math.Exp(-TAU * hz / SR)), y = 0f;
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = y; }
        }

        public static void Highpass(float[] x, float hz)
        {
            float a = (float)(1.0 - Math.Exp(-TAU * hz / SR)), y = 0f;
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] -= y; }
        }

        /// <summary>Passa-banda ressonante (biquad RBJ, ganho de pico constante).</summary>
        public static float[] Bandpass(float[] x, float hz, float q)
        {
            double w = TAU * hz / SR, alpha = Math.Sin(w) / (2 * q), cs = Math.Cos(w);
            double b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * cs, a2 = 1 - alpha;
            b0 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            var y = new float[x.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double o = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x[i]; y2 = y1; y1 = o; y[i] = (float)o;
            }
            return y;
        }

        public static void SoftClip(float[] x, float drive = 1.5f)
        {
            float n = (float)Math.Tanh(drive);
            for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Tanh(x[i] * drive) / n;
        }

        public static void Fade(float[] x, float inSec, float outSec)
        {
            int a = (int)(inSec * SR), b = (int)(outSec * SR);
            for (int i = 0; i < a && i < x.Length; i++) x[i] *= i / (float)a;
            for (int i = 0; i < b && i < x.Length; i++) x[x.Length - 1 - i] *= i / (float)b;
        }

        public static void Mix(float[] dst, float[] src, float atSec, float gain)
        {
            int o = (int)(atSec * SR);
            for (int i = 0; i < src.Length; i++) { int j = o + i; if (j >= 0 && j < dst.Length) dst[j] += src[i] * gain; }
        }

        /// <summary>Emenda o fim no começo (crossfade) para virar loop sem clique.</summary>
        public static float[] MakeLoop(float[] x, float xfadeSec)
        {
            int n = (int)(xfadeSec * SR);
            var y = new float[x.Length - n];
            Array.Copy(x, y, y.Length);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                y[i] = y[i] * k + x[x.Length - n + i] * (1 - k);
            }
            return y;
        }

        /// <summary>Reverb de Schroeder/Freeverb reduzido (6 pentes + 3 passa-tudo).</summary>
        public static void Reverb(float[] x, float wet, float room = 0.84f, float damp = 0.3f)
        {
            int[] cl = { 1557, 1617, 1491, 1422, 1277, 1356 };
            int[] al = { 225, 556, 441 };
            var acc = new float[x.Length];
            foreach (int L in cl)
            {
                var buf = new float[L]; int idx = 0; float filt = 0f;
                for (int n = 0; n < x.Length; n++)
                {
                    float y = buf[idx];
                    filt = y * (1 - damp) + filt * damp;
                    buf[idx] = x[n] * 0.015f + filt * room;
                    acc[n] += y;
                    if (++idx >= L) idx = 0;
                }
            }
            foreach (int L in al)
            {
                var buf = new float[L]; int idx = 0;
                for (int n = 0; n < x.Length; n++)
                {
                    float bo = buf[idx];
                    buf[idx] = acc[n] + bo * 0.5f;
                    acc[n] = bo - acc[n];
                    if (++idx >= L) idx = 0;
                }
            }
            for (int n = 0; n < x.Length; n++) x[n] = x[n] * (1 - wet * 0.4f) + acc[n] * wet * 3f;
        }

        static float Rand(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }

        // ------------------------------------------------------------ sinos

        // parciais de sino de bronze (hum, prime, tierce menor, quinta, nominal, ...)
        static readonly float[] BR = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.51f, 3.01f, 4.07f, 5.2f, 6.39f };
        static readonly float[] BA = { 0.45f, 0.7f, 0.42f, 0.28f, 0.55f, 0.22f, 0.18f, 0.12f, 0.08f, 0.05f };
        static readonly float[] BD = { 4.2f, 3.4f, 2.6f, 2.0f, 1.9f, 1.3f, 1.1f, 0.7f, 0.45f, 0.3f };

        public struct BellOpts
        {
            public float detuneCents;     // o sino inteiro fora de afinação
            public float wobbleDepth;     // a cauda começa a oscilar (fração de frequência)
            public float wobbleRate;      // Hz da oscilação
            public float wrongPartial;    // amplitude de uma parcial que não deveria existir (trítono)
            public float beatHz;          // batimento: cópia desafinada da nominal
            public float freezeAt, freezeFor;   // nota que soa tempo demais
            public float strike;          // ataque (0 = sem golpe: ressonância por simpatia)
            public float attack;          // s (subida lenta = vibrando sozinho)
            public float decayScale;
            public float preEcho;         // eco que chega ANTES do golpe (s)
        }

        public static BellOpts Tuned => new BellOpts { strike = 1f, attack = 0.002f, decayScale = 1f };

        public static float[] Bell(float f, float dur, BellOpts o)
        {
            float pre = o.preEcho > 0 ? o.preEcho : 0f;
            var x = New(dur + pre);
            int off = (int)(pre * SR);
            double fr = f * Math.Pow(2, o.detuneCents / 1200.0);
            int n = x.Length - off;
            for (int p = 0; p < BR.Length; p++)
            {
                double ph = 0, ph2 = 0;
                double pf = fr * BR[p];
                if (pf > SR * 0.45) continue;
                float tau = BD[p] * Math.Max(0.1f, o.decayScale);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SR;
                    // nota que soa tempo demais: o decaimento para durante freezeFor
                    float te = t;
                    if (o.freezeFor > 0 && t > o.freezeAt) te = t < o.freezeAt + o.freezeFor ? o.freezeAt : t - o.freezeFor;
                    float env = (float)Math.Exp(-te / tau) * (o.attack > 0.01f ? Math.Min(1f, t / o.attack) : Math.Min(1f, t / 0.002f));
                    // a cauda oscila numa frequência que não deveria existir (cresce depois do golpe)
                    double wob = 1.0;
                    if (o.wobbleDepth > 0) wob += o.wobbleDepth * Math.Min(1.0, Math.Max(0, (t - 0.35) / 1.6)) * Math.Sin(TAU * o.wobbleRate * t + p * 0.7) * (0.6 + 0.4 * p / BR.Length);
                    ph += TAU * pf * wob / SR;
                    float v = (float)Math.Sin(ph) * BA[p] * env;
                    if (o.beatHz > 0 && p == 4) { ph2 += TAU * (pf + o.beatHz) / SR; v += (float)Math.Sin(ph2) * BA[p] * 0.8f * env * Math.Min(1f, t / 0.6f); }
                    x[off + i] += v;
                }
            }
            if (o.wrongPartial > 0)
            {
                double ph = 0, pf = fr * 1.414;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SR;
                    float env = (float)(Math.Min(1.0, Math.Max(0, (t - 0.25) / 1.4)) * Math.Exp(-t / 3.8));
                    ph += TAU * pf * (1 + 0.004 * Math.Sin(TAU * 0.7 * t)) / SR;
                    x[off + i] += (float)Math.Sin(ph) * o.wrongPartial * env;
                }
            }
            if (o.strike > 0)
            {
                uint s = 0x9E3779B9u ^ (uint)(f * 100);
                int sn = (int)(0.014f * SR);
                var hit = new float[sn];
                for (int i = 0; i < sn; i++) hit[i] = (Rand(ref s) * 2 - 1) * (1 - i / (float)sn) * 0.35f * o.strike;
                Highpass(hit, 1800);
                for (int i = 0; i < sn; i++) x[off + i] += hit[i];
            }
            if (pre > 0)
            {
                // o eco temporalmente errado: um fantasma fraco do golpe chega antes dele
                int gn = Math.Min((int)(0.5f * SR), n);
                for (int i = 0; i < gn; i++) x[i] += x[off + i] * 0.28f * (1 - i / (float)gn);
            }
            Normalize(x, 0.8f);
            return x;
        }

        /// <summary>Sino tocado ao contrário (incha até o ataque): "levemente invertido".</summary>
        public static float[] Reverse(float[] x) { var y = (float[])x.Clone(); Array.Reverse(y); Fade(y, 0.4f, 0.01f); return y; }

        public static float[] Distant(float[] x, float lp, float wet)
        {
            Lowpass(x, lp); Reverb(x, wet, 0.88f, 0.45f); Normalize(x, 0.8f); return x;
        }

        // ------------------------------------------------------------ carrilhão, grilos, vento, melodia

        static readonly float[] CR = { 1f, 2.76f, 5.40f, 8.93f };
        static readonly float[] CRw = { 1f, 2.61f, 5.71f, 9.4f };   // parciais fora da série
        static readonly float[] CA = { 1f, 0.5f, 0.25f, 0.12f };
        static readonly float[] CD = { 2.6f, 1.3f, 0.6f, 0.35f };

        public static float[] Chime(float f, bool corrupt)
        {
            var x = New(corrupt ? 5.5f : 3.6f);
            for (int p = 0; p < 4; p++)
            {
                double ph = 0;
                float r = corrupt ? CRw[p] : CR[p];
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    float te = corrupt ? (t < 0.6f ? t : 0.6f + (t - 0.6f) * 0.35f) : t;   // a nota se recusa a morrer
                    double glide = corrupt ? 1 - 0.018 * Math.Min(1.0, t / 3.0) : 1;     // escorrega para baixo
                    ph += TAU * f * r * glide / SR;
                    x[i] += (float)Math.Sin(ph) * CA[p] * (float)Math.Exp(-te / CD[p]) * Math.Min(1f, t / 0.001f);
                }
            }
            Reverb(x, 0.35f);
            Normalize(x, 0.6f);
            return x;
        }

        public static float[] Crickets(float sec, int seed)
        {
            var x = New(sec);
            uint s = (uint)(seed * 7919 + 13);
            float[] carriers = { 4150f, 4620f };
            for (int c = 0; c < 2; c++)
            {
                float t = 0.2f + Rand(ref s) * 0.5f;
                while (t < sec - 0.4f)
                {
                    int pulses = 3 + (int)(Rand(ref s) * 2);
                    for (int k = 0; k < pulses; k++)
                    {
                        int o = (int)((t + k * 0.03f) * SR), len = (int)(0.018f * SR);
                        for (int i = 0; i < len && o + i < x.Length; i++)
                        {
                            float e = (float)Math.Sin(Math.PI * i / len);
                            x[o + i] += (float)Math.Sin(TAU * carriers[c] * (o + i) / SR) * e * 0.18f;
                        }
                    }
                    t += 0.55f + Rand(ref s) * 0.45f;
                }
            }
            Reverb(x, 0.25f);
            return x;
        }

        /// <summary>Vento que ganha componentes tonais estranhos (dois tons em trítono).</summary>
        public static float[] WindTone(float sec)
        {
            uint s = 12345u;
            var n = New(sec + 1f);
            for (int i = 0; i < n.Length; i++) n[i] = Rand(ref s) * 2 - 1;
            var a = Bandpass(n, 523.25f, 38f);
            var b = Bandpass(n, 739.99f, 45f);
            var c = Bandpass(n, 1108.7f, 60f);
            var x = New(sec + 1f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float am1 = 0.6f + 0.4f * (float)Math.Sin(TAU * 0.13 * t), am2 = 0.6f + 0.4f * (float)Math.Sin(TAU * 0.09 * t + 1.7);
                x[i] = a[i] * am1 * 1.2f + b[i] * am2 + c[i] * 0.5f * am1 * am2;
            }
            Reverb(x, 0.4f);
            Normalize(x, 0.5f);
            return MakeLoop(x, 1f);
        }

        /// <summary>Corda dedilhada (Karplus-Strong).</summary>
        static float[] Pluck(float f, float sec, uint seed, float sustainFor = 0f, float vibrato = 0f)
        {
            var x = New(sec);
            int L = Math.Max(2, (int)(SR / f));
            var buf = new float[L];
            uint s = seed;
            for (int i = 0; i < L; i++) buf[i] = Rand(ref s) * 2 - 1;
            int idx = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                int j = (idx + 1) % L;
                float decay = sustainFor > 0 && t < sustainFor ? 0.9995f : 0.996f;
                float v = (buf[idx] + buf[j]) * 0.5f * decay;
                buf[idx] = v;
                x[i] = v;
                idx = j;
                if (vibrato > 0 && i % 64 == 0)
                {
                    // vibrato "doente": a corda vai encolhendo e esticando
                    int nl = Math.Max(2, (int)(SR / (f * (1 + vibrato * Math.Sin(TAU * 4.5 * t)))));
                    if (nl != L) { var nb = new float[nl]; for (int k = 0; k < nl; k++) nb[k] = buf[(idx + k) % L]; buf = nb; L = nl; idx = 0; }
                }
            }
            Lowpass(x, 2600);
            Fade(x, 0.002f, 0.05f);
            return x;
        }

        static readonly float[] Mel = { 440f, 587.33f, 523.25f, 440f, 392f, 440f, 349.23f, 293.66f, 440f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 440f, 392f };

        /// <summary>Melodia distante de alaúde vinda da vila (afinada ou já doente).</summary>
        public static float[] Melody(bool corrupt)
        {
            float step = 0.667f;
            var x = New(Mel.Length * step + 2f);
            for (int k = 0; k < Mel.Length; k++)
            {
                float f = Mel[k];
                float sus = 0f, vib = 0f;
                if (corrupt)
                {
                    if (k % 5 == 2) f *= (float)Math.Pow(2, -32 / 1200.0);       // alguns cents abaixo
                    if (k == 6) { sus = 2.6f; vib = 0.012f; }                     // essa nota não para
                }
                var p = Pluck(f, sus > 0 ? 3.6f : 1.6f, (uint)(k * 2654435761u + 7), sus, vib);
                Mix(x, p, k * step + 0.3f, 0.45f);
                if (corrupt && k == 9) Mix(x, p, k * step + 0.3f - 0.28f, 0.14f);   // eco antes da nota
            }
            Distant(x, 1800, 0.45f);
            Normalize(x, 0.55f);
            return MakeLoop(x, 1.2f);
        }

        // ------------------------------------------------------------ a Fenda e os sete

        static readonly float[] FA = { 1f, 2.13f, 3.07f, 4.41f, 5.62f, 6.93f, 8.18f, 9.71f };
        static readonly float[] FB = { 1f, 2.08f, 3.15f, 4.33f, 5.71f, 7.02f, 8.30f, 9.55f };
        static readonly float[] FAmp = { 1f, 0.6f, 0.45f, 0.35f, 0.25f, 0.18f, 0.12f, 0.08f };
        public const float FendaF0 = 55f;

        /// <summary>A nota impossível: fundamental grave, harmônicos que não fecham, duas séries
        /// incompatíveis entrando e saindo de fase (duas músicas ocupando o mesmo espaço).</summary>
        public static float[] ImpossibleNote(float sec, float fadeIn)
        {
            var x = New(sec);
            for (int p = 0; p < FA.Length; p++)
            {
                double pa = 0, pb = 0, pc = 0;
                double fa = FendaF0 * FA[p], fb = FendaF0 * 1.006 * FB[p];
                double beat = 0.3 + p * 0.17;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    float wa = 0.5f + 0.5f * (float)Math.Sin(TAU * t / 7.0 + p * 0.4);
                    pa += TAU * fa / SR; pb += TAU * fb / SR; pc += TAU * (fa + beat) / SR;
                    x[i] += (float)((Math.Sin(pa) * wa + Math.Sin(pc) * wa * 0.6 + Math.Sin(pb) * (1 - wa)) * FAmp[p] * 0.35);
                }
            }
            // ar fino por cima (bandas estreitas que respiram)
            uint s = 777u;
            var n = New(sec);
            for (int i = 0; i < n.Length; i++) n[i] = Rand(ref s) * 2 - 1;
            var hi = Bandpass(n, 1760f * 1.02f, 30f); var hi2 = Bandpass(n, 2637f * 0.985f, 35f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                x[i] += (hi[i] + hi2[i]) * 0.5f * (0.5f + 0.5f * (float)Math.Sin(TAU * 0.11 * t));
            }
            SoftClip(x, 1.8f);   // harmônicos que deixam o grave audível em caixinha de notebook
            Reverb(x, 0.35f, 0.9f, 0.5f);
            for (int i = 0; i < x.Length; i++) { float t = i / (float)SR; x[i] *= Math.Min(1f, t / fadeIn); }
            Normalize(x, 0.8f);
            return x;
        }

        public static readonly float[] SignatureHz = { 242.6f, 117.2f, 168.9f, 309.1f, 381.2f, 449.9f, 534.1f };
        static readonly float[] SigRate = { 4.3f, 3.1f, 5.2f, 6.1f, 7.4f, 8.2f, 9.5f };

        /// <summary>Assinatura de um dos sete: tom vítreo da mesma série da Fenda, com tremor próprio.
        /// Nada de dó-ré-mi: as frequências vêm da série inarmônica.</summary>
        public static float[] Signature(int i, float sec, bool doppler = false)
        {
            var x = New(sec);
            double ph1 = 0, ph2 = 0, ph3 = 0;
            float f = SignatureHz[i], r = SigRate[i];
            float peak = sec * 0.42f;
            for (int k = 0; k < x.Length; k++)
            {
                float t = k / (float)SR;
                double dop = doppler ? 1 + 0.035 * -Math.Tanh((t - peak) * 2.2) : 1;   // passa pelo céu
                double vib = 1 + 0.003 * Math.Sin(TAU * 5.3 * t + i);
                ph1 += TAU * f * dop * vib / SR; ph2 += TAU * f * 2.76 * dop / SR; ph3 += TAU * f * 5.4 * dop / SR;
                float am = 0.65f + 0.35f * (float)Math.Sin(TAU * r * t);
                float env = doppler ? (float)Math.Exp(-Math.Pow((t - peak) / (sec * 0.32), 2)) : Math.Min(1f, t / 0.08f) * (float)Math.Exp(-t / (sec * 0.45f));
                x[k] = (float)(Math.Sin(ph1) + 0.4 * Math.Sin(ph2) + 0.18 * Math.Sin(ph3)) * am * env * 0.4f;
            }
            if (doppler)
            {
                // ar deslocado pela passagem
                uint s = 4242u;
                var n = New(sec);
                for (int k = 0; k < n.Length; k++) n[k] = Rand(ref s) * 2 - 1;
                Lowpass(n, 900);
                for (int k = 0; k < x.Length; k++) { float t = k / (float)SR; x[k] += n[k] * 0.6f * (float)Math.Exp(-Math.Pow((t - peak) / (sec * 0.2), 2)); }
            }
            Reverb(x, 0.45f, 0.9f, 0.4f);
            Normalize(x, 0.7f);
            return x;
        }

        /// <summary>A ressonância grave do impacto, que atravessa o vale segundos depois do clarão.</summary>
        public static float[] Impact(float sec)
        {
            var x = New(sec);
            double ph = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                double f = 38 + 32 * Math.Exp(-t / 0.35);
                ph += TAU * f / SR;
                x[i] += (float)(Math.Sin(ph) * Math.Exp(-t / 0.9)) * 0.9f;
            }
            uint s = 99u;
            var n = New(sec);
            for (int i = 0; i < n.Length; i++) n[i] = Rand(ref s) * 2 - 1;
            Lowpass(n, 140); Lowpass(n, 140);
            for (int i = 0; i < x.Length; i++) { float t = i / (float)SR; x[i] += n[i] * 3.2f * (float)Math.Exp(-t / 2.8) * Math.Min(1f, t / 0.05f); }
            // corpo da ressonância (audível em caixinha de notebook): parciais graves inarmônicas
            float[] body = { 82f, 131f, 187f, 263f, 349f };
            for (int p = 0; p < body.Length; p++)
            {
                double pp = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    pp += TAU * body[p] * (1 - 0.02 * Math.Min(1.0, t / 3)) / SR;
                    x[i] += (float)(Math.Sin(pp) * Math.Exp(-t / (2.4 - p * 0.3)) * Math.Min(1.0, t / 0.01)) * (0.55f / (1 + p * 0.35f));
                }
            }
            // a onda rolando pelo vale: ruído em banda média que vai e volta (ecos nas colinas)
            var roll = New(sec);
            for (int i = 0; i < roll.Length; i++) roll[i] = Rand(ref s) * 2 - 1;
            var mid = Bandpass(roll, 260f, 1.2f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float echoes = (float)(Math.Exp(-t / 1.2) + 0.45 * Math.Exp(-Math.Pow((t - 1.1) / 0.35, 2)) + 0.25 * Math.Exp(-Math.Pow((t - 2.3) / 0.5, 2)));
                x[i] += mid[i] * 1.4f * echoes * Math.Min(1f, t / 0.02f);
            }
            // o anel inarmônico (a mesma série da Fenda), escorregando para baixo
            for (int p = 1; p < 7; p++)
            {
                double pp = 0, pf = 110 * FA[p];
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    pp += TAU * pf * (1 - 0.015 * Math.Min(1.0, t / 6)) / SR;
                    x[i] += (float)(Math.Sin(pp) * Math.Exp(-t / (5.5 - p * 0.5)) * Math.Min(1.0, t / 0.4)) * FAmp[p] * 0.45f;
                }
            }
            SoftClip(x, 1.6f);
            Reverb(x, 0.5f, 0.92f, 0.55f);
            Normalize(x, 0.95f);
            return x;
        }

        /// <summary>Estrutura metálica ressoando com a onda (parciais altas inarmônicas, incha e treme).</summary>
        public static float[] MetalShimmer(float sec)
        {
            float[] fr = { 2217f, 2789f, 3301f, 4127f, 4655f, 5503f };
            var x = New(sec);
            for (int p = 0; p < fr.Length; p++)
            {
                double ph = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += TAU * fr[p] / SR;
                    float env = Math.Min(1f, t / 0.6f) * (float)Math.Exp(-t / 2.6f) * (0.7f + 0.3f * (float)Math.Sin(TAU * 11 * t + p));
                    x[i] += (float)Math.Sin(ph) * env * (0.25f / (1 + p * 0.4f));
                }
            }
            Reverb(x, 0.4f);
            Normalize(x, 0.4f);
            return x;
        }

        /// <summary>Correntes e a lanterna batendo de leve (tiques metálicos).</summary>
        public static float[] Clinks(float sec)
        {
            var x = New(sec);
            uint s = 31337u;
            float t = 0.05f;
            while (t < sec - 0.1f)
            {
                float f = 2800 + Rand(ref s) * 2400;
                int o = (int)(t * SR), len = (int)(0.06f * SR);
                for (int i = 0; i < len && o + i < x.Length; i++)
                    x[o + i] += (float)(Math.Sin(TAU * f * i / SR) * Math.Exp(-i / (0.012 * SR))) * (0.3f + Rand(ref s) * 0.3f);
                t += 0.08f + Rand(ref s) * 0.3f;
            }
            Reverb(x, 0.25f);
            return x;
        }

        /// <summary>Coruja distante (vida noturna no começo, antes de tudo ficar errado).</summary>
        public static float[] Owl()
        {
            var x = New(3f);
            for (int h = 0; h < 2; h++)
            {
                double ph = 0;
                int o = (int)((0.2f + h * 0.75f) * SR), len = (int)(0.42f * SR);
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len;
                    ph += TAU * (425 - 40 * u) / SR;
                    x[o + i] += (float)(Math.Sin(ph) * Math.Sin(Math.PI * u)) * 0.5f;
                }
            }
            Lowpass(x, 1200);
            Distant(x, 1100, 0.5f);
            Normalize(x, 0.4f);
            return x;
        }
    }
}

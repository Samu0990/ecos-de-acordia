using System;

namespace Aren
{
    /// <summary>
    /// Síntese procedural de todos os sons da demo (licença limpa, zero arquivos).
    /// Funções puras sobre float[] — rodam numa thread de fundo no carregamento.
    /// </summary>
    public static class ArenSynth
    {
        public const int SR = 44100;
        const float TAU = (float)(Math.PI * 2);

        // Lá menor pentatônica a partir de A4: qualquer sequência de golpes soa consonante
        public static readonly float[] Scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.66f, 1318.51f, 1567.98f };

        public class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }
            public float Signed() => Next() * 2f - 1f;
        }

        static float[] Buf(float seconds) => new float[(int)(seconds * SR)];

        // ---------------------------------------------------------------- filtros simples

        /// <summary>Passa-baixa de 1 polo (in-place).</summary>
        public static void LowPass(float[] x, float cutoff)
        {
            float a = 1f - (float)Math.Exp(-TAU * cutoff / SR), y = 0;
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = y; }
        }

        public static void HighPass(float[] x, float cutoff)
        {
            float a = 1f - (float)Math.Exp(-TAU * cutoff / SR), lp = 0;
            for (int i = 0; i < x.Length; i++) { lp += a * (x[i] - lp); x[i] -= lp; }
        }

        /// <summary>Passa-banda estado-variável com centro variando no tempo (fc(t) em Hz).</summary>
        public static void BandPassSweep(float[] x, Func<float, float> fc, float q)
        {
            float low = 0, band = 0;
            // O filtro de estado (Chamberlin) só é estável com f < −q + √(q² + 4). Com q = 2 o
            // limite é ~6 kHz: o chocalho dos tambores (5→7 kHz) explodia para Inf/NaN e, quando
            // a música de combate subia, o NaN calava TODO o áudio do jogo até o fim.
            float fMax = 0.95f * (-q + (float)Math.Sqrt(q * q + 4f));
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc(t), SR * 0.24f) / SR);
                if (f > fMax) f = fMax;
                low += f * band;
                float high = x[i] - low - q * band;
                band += f * high;
                x[i] = band;
            }
        }

        public static void Normalize(float[] x, float peak = 0.9f)
        {
            float m = 0;
            for (int i = 0; i < x.Length; i++) m = Math.Max(m, Math.Abs(x[i]));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
        }

        public static void Mix(float[] dst, float[] src, float gain, int offset = 0)
        {
            for (int i = 0; i < src.Length && i + offset < dst.Length; i++)
                if (i + offset >= 0) dst[i + offset] += src[i] * gain;
        }

        /// <summary>Reverb de Schroeder barato (4 combs + 2 allpass), mistura wet.</summary>
        public static float[] Reverb(float[] dry, float tail, float wet, float damp = 0.35f, float size = 1f)
        {
            int n = dry.Length + (int)(tail * SR);
            var output = new float[n];
            int[] combLen = { (int)(1557 * size), (int)(1617 * size), (int)(1491 * size), (int)(1422 * size) };
            float fb = 0.80f;
            var acc = new float[n];
            foreach (int L in combLen)
            {
                var buf = new float[L]; int idx = 0; float filt = 0;
                for (int i = 0; i < n; i++)
                {
                    float input = i < dry.Length ? dry[i] : 0f;
                    float o = buf[idx];
                    filt = o * (1 - damp) + filt * damp;
                    buf[idx] = input + filt * fb;
                    idx = (idx + 1) % L;
                    acc[i] += o * 0.25f;
                }
            }
            foreach (int L in new[] { 556, 225 })
            {
                var buf = new float[L]; int idx = 0;
                for (int i = 0; i < n; i++)
                {
                    float b = buf[idx];
                    float y = -acc[i] + b;
                    buf[idx] = acc[i] + b * 0.5f;
                    idx = (idx + 1) % L;
                    acc[i] = y;
                }
            }
            for (int i = 0; i < n; i++) output[i] = (i < dry.Length ? dry[i] : 0f) + acc[i] * wet;
            return output;
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / attack;
            return (float)Math.Exp(-(t - attack) / decay);
        }

        // ---------------------------------------------------------------- flauta

        /// <summary>Nota de flauta: "chiff" de sopro, fundamental + harmônicos, vibrato e ar.</summary>
        public static float[] FluteNote(float freq, float dur, uint seed, float breath = 0.09f)
        {
            var x = Buf(dur);
            var r = new Rng(seed);
            float ph = 0;
            var noise = new float[x.Length];
            for (int i = 0; i < x.Length; i++) noise[i] = r.Signed();
            BandPassSweep(noise, t => freq * 3.2f, 0.9f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float vib = t > 0.12f ? 1f + 0.0045f * (float)Math.Sin(TAU * 5.6f * t) * Math.Min(1f, (t - 0.12f) * 5f) : 1f;
                ph += TAU * freq * vib / SR;
                float amp = t < 0.025f ? t / 0.025f * 1.15f : (t < 0.11f ? 1.15f - (t - 0.025f) / 0.085f * 0.4f : 0.75f * (float)Math.Exp(-(t - 0.11f) / (dur * 0.45f)));
                float tone = (float)(Math.Sin(ph) + 0.33 * Math.Sin(2 * ph + 0.3) + 0.12 * Math.Sin(3 * ph) + 0.04 * Math.Sin(4 * ph));
                float chiff = t < 0.035f ? (1f - t / 0.035f) * 0.55f : 0f;
                x[i] = tone * amp * 0.6f + noise[i] * (breath * amp + chiff);
            }
            var o = Reverb(x, 0.55f, 0.28f, 0.4f, 0.9f);
            Normalize(o, 0.8f);
            return o;
        }

        // ---------------------------------------------------------------- impactos e ar

        public static float[] Whoosh(float dur, float f0, float f1, uint seed, float q = 1.2f)
        {
            var x = Buf(dur); var r = new Rng(seed);
            for (int i = 0; i < x.Length; i++) x[i] = r.Signed();
            BandPassSweep(x, t => f0 + (f1 - f0) * (t / dur), q);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR / dur;
                x[i] *= (float)Math.Sin(Math.PI * Math.Pow(t, 0.7)) ;
            }
            Normalize(x, 0.7f);
            return x;
        }

        public static float[] Impact(float dur, float fStart, float fEnd, float crack, float ring, float ringFreq, uint seed)
        {
            var x = Buf(dur); var r = new Rng(seed);
            float ph = 0, ph2 = 0, ph3 = 0, ph4 = 0;
            var n = new float[x.Length];
            for (int i = 0; i < n.Length; i++) n[i] = r.Signed();
            HighPass(n, 1800);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float f = fEnd + (fStart - fEnd) * (float)Math.Exp(-t * 28);
                ph += TAU * f / SR;
                float body = (float)Math.Sin(ph) * Env(t, 0.002f, 0.07f);
                float cr = n[i] * Env(t, 0.0005f, 0.018f) * crack;
                ph2 += TAU * ringFreq / SR; ph3 += TAU * ringFreq * 2.76f / SR; ph4 += TAU * ringFreq * 5.4f / SR;
                float rg = (float)(Math.Sin(ph2) * Env(t, 0.001f, 0.25f) + 0.5 * Math.Sin(ph3) * Env(t, 0.001f, 0.12f) + 0.25 * Math.Sin(ph4) * Env(t, 0.001f, 0.06f)) * ring;
                x[i] = body + cr + rg * 0.5f;
            }
            var o = Reverb(x, 0.35f, 0.18f, 0.5f, 0.8f);
            Normalize(o, 0.9f);
            return o;
        }

        public static float[] Boom(float dur, float fStart, float fEnd, uint seed, float noiseAmt = 0.4f)
        {
            var x = Buf(dur); var r = new Rng(seed); float ph = 0;
            var n = new float[x.Length];
            for (int i = 0; i < n.Length; i++) n[i] = r.Signed();
            LowPass(n, 900);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float f = fEnd + (fStart - fEnd) * (float)Math.Exp(-t * 9);
                ph += TAU * f / SR;
                x[i] = (float)Math.Sin(ph) * Env(t, 0.004f, dur * 0.35f) + n[i] * noiseAmt * Env(t, 0.002f, dur * 0.2f);
            }
            Normalize(x, 0.95f);
            return x;
        }

        /// <summary>Acorde (frequências) com envelope e leve desafinação (coro).</summary>
        public static float[] Chord(float[] freqs, float dur, float attack, float decay, float detune = 0.003f, bool saw = false)
        {
            var x = Buf(dur);
            foreach (var f in freqs)
            {
                for (int v = -1; v <= 1; v++)
                {
                    float ff = f * (1f + v * detune); float ph = v * 1.3f;
                    for (int i = 0; i < x.Length; i++)
                    {
                        float t = i / (float)SR;
                        ph += TAU * ff / SR;
                        float s = saw ? (float)(Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.33 * Math.Sin(3 * ph) + 0.25 * Math.Sin(4 * ph)) * 0.6f : (float)Math.Sin(ph);
                        x[i] += s * Env(t, attack, decay) / freqs.Length / 3f;
                    }
                }
            }
            return x;
        }

        /// <summary>Sino: parciais inarmônicos de sino de igreja (hum, prima, terça, quinta, nominal...).</summary>
        public static float[] Bell(float fNominal, float dur, float wet, uint seed, bool corrupt = false)
        {
            float[] ratios = { 0.5f, 1f, 1.2f, 1.5f, 2f, 2.5f, 2.67f, 3f, 4.07f };
            float[] amps = { 0.55f, 0.4f, 0.32f, 0.18f, 0.5f, 0.12f, 0.1f, 0.14f, 0.06f };
            float[] decays = { 3.2f, 2.2f, 1.6f, 1.1f, 1.4f, 0.6f, 0.5f, 0.4f, 0.25f };
            var x = Buf(dur); var r = new Rng(seed);
            for (int k = 0; k < ratios.Length; k++)
            {
                float f = fNominal * 0.5f * ratios[k] * (corrupt ? 1f + (r.Next() - 0.5f) * 0.06f : 1f);
                float ph = r.Next() * TAU;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += TAU * f / SR;
                    x[i] += (float)Math.Sin(ph) * amps[k] * Env(t, 0.003f, decays[k] * dur * 0.3f);
                }
            }
            // batida do badalo
            for (int i = 0; i < Math.Min(x.Length, SR / 50); i++) x[i] += r.Signed() * (1f - i / (SR / 50f)) * 0.3f;
            if (corrupt)
            {
                // a 13ª badalada: crepita e "anda para trás" no fim
                for (int i = 0; i < x.Length; i++) if ((i / 900) % 7 == 3) x[i] *= 0.15f;
                int half = x.Length / 2;
                for (int i = 0; i < half / 2; i++) { float a = x[half + i]; x[half + i] = x[x.Length - 1 - i] * 0.8f; x[x.Length - 1 - i] = a; }
            }
            var o = Reverb(x, 1.2f, wet, 0.3f, 1.25f);
            Normalize(o, 0.85f);
            return o;
        }

        public static float[] Glitch(float dur, float baseFreq, uint seed, float growl = 0.6f)
        {
            var x = Buf(dur); var r = new Rng(seed);
            float held = 0; int hold = 0; float ph = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                if (hold-- <= 0) { held = r.Signed(); hold = 40 + (int)(r.Next() * 400); }
                ph += TAU * baseFreq * (1f + 0.3f * (float)Math.Sin(TAU * 13 * t)) / SR;
                float saw = (ph % TAU) / TAU * 2f - 1f;
                x[i] = (held * 0.6f + saw * growl) * Env(t, 0.005f, dur * 0.4f);
            }
            LowPass(x, 2800);
            Normalize(x, 0.8f);
            return x;
        }

        /// <summary>Aviso de golpe inimigo: tom sujo subindo + "tic" no fim (leitura do counter).</summary>
        public static float[] Telegraph(float dur, uint seed)
        {
            var x = Buf(dur); float ph = 0, ph2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR, k = t / dur;
                float f = 220f + 520f * k * k;
                ph += TAU * f / SR; ph2 += TAU * f * 1.5f / SR;
                float s = (float)(Math.Sin(ph) + 0.6 * Math.Sign(Math.Sin(ph2)) * 0.4);
                s = (float)Math.Round(s * 6) / 6f;   // bitcrush leve
                x[i] = s * (0.25f + 0.75f * k) * 0.6f;
            }
            int tick = (int)((dur - 0.04f) * SR);
            float tp = 0;
            for (int i = tick; i < x.Length; i++) { tp += TAU * 2400 / SR; x[i] += (float)Math.Sin(tp) * Env((i - tick) / (float)SR, 0.001f, 0.02f) * 0.9f; }
            Normalize(x, 0.75f);
            return x;
        }

        public static float[] Pluck(float freq, float dur, float decay)
        {
            var x = Buf(dur); float ph = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR; ph += TAU * freq / SR;
                x[i] = (float)(Math.Sin(ph) + 0.3 * Math.Sin(2 * ph)) * Env(t, 0.002f, decay);
            }
            Normalize(x, 0.6f);
            return x;
        }

        public static float[] Concat(params float[][] parts)
        {
            int n = 0; foreach (var p in parts) n += p.Length;
            var o = new float[n]; int k = 0;
            foreach (var p in parts) { Array.Copy(p, 0, o, k, p.Length); k += p.Length; }
            return o;
        }

        /// <summary>Loop de vento: ruído filtrado com cutoff/amplitude lentos, emenda sem clique.</summary>
        public static float[] WindLoop(float dur, uint seed)
        {
            var x = Buf(dur + 0.6f); var r = new Rng(seed);
            for (int i = 0; i < x.Length; i++) x[i] = r.Signed();
            BandPassSweep(x, t => 380f + 260f * (float)Math.Sin(TAU * t / dur * 2) + 120f * (float)Math.Sin(TAU * t / dur * 5 + 1), 1.6f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                x[i] *= 0.6f + 0.4f * (float)Math.Sin(TAU * t / dur * 3 + 0.5);
            }
            var y = LoopCrossfade(x, (int)(dur * SR), (int)(0.6f * SR));
            Normalize(y, 0.5f);
            return y;
        }

        /// <summary>Loop sem clique para som contínuo: crossfade do trecho após loopLen sobre o início.</summary>
        public static float[] LoopCrossfade(float[] x, int loopLen, int fade)
        {
            fade = Math.Min(fade, x.Length - loopLen);
            var y = new float[loopLen];
            Array.Copy(x, y, loopLen);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                y[i] = x[i] * k + x[loopLen + i] * (1 - k);
            }
            return y;
        }

        /// <summary>Loop para som rítmico: o que passou do fim volta somado ao começo.</summary>
        public static float[] Fold(float[] x, int loopLen)
        {
            var y = new float[loopLen];
            for (int i = 0; i < x.Length; i++) y[i % loopLen] += x[i];
            return y;
        }

        // ---------------------------------------------------------------- música (camadas)

        /// <summary>Drone de ambiente: Lá e Mi graves respirando (camada 0, sempre).</summary>
        public static float[] MusicDrone(float bars, float bpm, uint seed)
        {
            float dur = bars * 4 * 60f / bpm;
            var x = Buf(dur + 1.5f);
            float[] f = { 110f, 164.81f, 220f, 329.63f };
            float[] g = { 0.5f, 0.35f, 0.25f, 0.1f };
            for (int k = 0; k < f.Length; k++)
            {
                float ph = k;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += TAU * f[k] * (1f + 0.002f * (float)Math.Sin(TAU * 0.13f * t + k)) / SR;
                    float breath = 0.6f + 0.4f * (float)Math.Sin(TAU * t / dur * (k + 1) + k);
                    x[i] += (float)Math.Sin(ph) * g[k] * breath;
                }
            }
            LowPass(x, 1200);
            var o = Reverb(x, 0f, 0.4f, 0.5f, 1.3f);
            var y = LoopCrossfade(o, (int)(dur * SR), (int)(1.5f * SR));
            Normalize(y, 0.5f);
            return y;
        }

        /// <summary>Percussão de combate (tambor de moldura + chocalho) a 'bpm'.</summary>
        public static float[] MusicDrums(float bars, float bpm, uint seed)
        {
            float beat = 60f / bpm; float dur = bars * 4 * beat;
            var x = Buf(dur + 1f); var r = new Rng(seed);
            var kick = Boom(0.35f, 120f, 48f, seed + 1, 0.15f);
            var tom = Impact(0.25f, 220f, 140f, 0.2f, 0.15f, 300f, seed + 2);
            var shaker = Whoosh(0.07f, 5000f, 7000f, seed + 3, 2f);
            int steps = (int)(bars * 16);
            // padrão em semicolcheias: tensão sem ser marcha
            string kp = "x.....x...x.....x.....x...x..x..";
            string tp = "....x.......x.......x.......x.x.";
            for (int s = 0; s < steps; s++)
            {
                int off = (int)(s * beat / 4f * SR);
                int p = s % 32;
                if (kp[p] == 'x') Mix(x, kick, 0.9f, off);
                if (tp[p] == 'x') Mix(x, tom, 0.55f, off);
                if (s % 2 == 1) Mix(x, shaker, 0.18f + (s % 4 == 3 ? 0.1f : 0), off);
            }
            var y = Fold(x, (int)(dur * SR));
            Normalize(y, 0.7f);
            return y;
        }

        /// <summary>Ostinato de baixo + contramelodia em flauta grave (camada intensa).</summary>
        public static float[] MusicOstinato(float bars, float bpm, uint seed)
        {
            float beat = 60f / bpm; float dur = bars * 4 * beat;
            var x = Buf(dur + 1.5f);
            float[] bass = { 55f, 55f, 65.41f, 55f, 73.42f, 55f, 82.41f, 73.42f };
            int eighths = (int)(bars * 8);
            for (int s = 0; s < eighths; s++)
            {
                float f = bass[s % bass.Length];
                var n = Pluck(f, beat * 0.5f, 0.12f);
                Mix(x, n, 0.5f, (int)(s * beat / 2f * SR));
            }
            // frase de flauta (Lá menor pentatônico), uma vez a cada 2 compassos
            int[] phrase = { 4, 3, 2, 3, 1, 0, 1, 2 };
            for (int b = 0; b < bars; b += 2)
                for (int k = 0; k < phrase.Length; k++)
                {
                    var n = FluteNote(Scale[phrase[k]] * 0.5f, beat * 0.9f, seed + (uint)(b * 10 + k), 0.06f);
                    Mix(x, n, 0.22f, (int)((b * 4 + k * 0.5f + 0.5f) * beat * SR));
                }
            var y = Fold(x, (int)(dur * SR));
            Normalize(y, 0.6f);
            return y;
        }
    }
}

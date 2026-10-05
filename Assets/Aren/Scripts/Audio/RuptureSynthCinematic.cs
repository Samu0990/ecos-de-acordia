using System;

namespace Aren
{
    /// <summary>
    /// Sons da abertura v2 (síntese offline, numa thread, como RuptureSynth). Mesma regra de lore:
    /// a corrupção é MUSICAL e física — notas que escorregam, parciais fora da série, batimentos,
    /// vidro sob tensão — nunca glitch digital, bitcrush, estática ou rádio.
    ///   · a flauta do Aren (a frase afinada e a frase em que uma nota "entorta");
    ///   · a Fenda: o primeiro ponto (tom fino), a rachadura (vidro sob tensão), o estilhaço;
    ///   · a inspiração antes dos sete e o nascimento de cada um (a assinatura dele);
    ///   · o zumbido agudo depois do clarão (o som ainda não chegou);
    ///   · o impacto em camadas (sub, pressão, corpo, textura, o sino gigante desafinado, cauda),
    ///     a rajada da onda de pressão e o chacoalhar de madeira e metal da vila.
    /// </summary>
    public static class RuptureSynthCinematic
    {
        const int SR = RuptureSynth.SR;
        const double TAU = Math.PI * 2.0;
        static float Rnd(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }
        static float[] Noise(float sec, uint seed) { var n = RuptureSynth.New(sec); uint s = seed; for (int i = 0; i < n.Length; i++) n[i] = Rnd(ref s) * 2 - 1; return n; }

        // ------------------------------------------------------------ a flauta do Aren

        struct Note { public float hz, beats, centsEnd, ghost; public Note(float h, float b, float ce = 0f, float g = 0f) { hz = h; beats = b; centsEnd = ce; ghost = g; } }

        // ré dórico, calmo (72 bpm): uma frase que pergunta e responde, terminando numa nota longa
        static readonly Note[] PhraseA =
        {
            new Note(587.33f, 1f), new Note(659.25f, 0.5f), new Note(698.46f, 0.5f), new Note(880f, 1.5f), new Note(783.99f, 0.5f),
            new Note(698.46f, 1f), new Note(659.25f, 1f), new Note(587.33f, 2f),
            new Note(523.25f, 1f), new Note(587.33f, 0.5f), new Note(659.25f, 0.5f), new Note(523.25f, 1f), new Note(440f, 1f),
            new Note(493.88f, 0.5f), new Note(523.25f, 0.5f), new Note(587.33f, 3.5f),
        };
        // a frase seguinte começa igual... e a segunda nota entorta para baixo, com um fantasma em trítono
        static readonly Note[] PhraseWrong =
        {
            new Note(587.33f, 1f), new Note(880f, 2.6f, -95f, 0.35f),
        };

        static float[] Flute(Note[] notes, float bpm, bool wrong)
        {
            float beat = 60f / bpm;
            float total = 0f; foreach (var n in notes) total += n.beats * beat;
            var x = RuptureSynth.New(total + 1.6f);
            var breath = Noise(total + 1.6f, 9001u);
            var air = RuptureSynth.Bandpass(breath, 1900f, 1.4f);
            double ph = 0, phg = 0;
            float t0 = 0.05f;
            float prevHz = notes[0].hz;
            for (int k = 0; k < notes.Length; k++)
            {
                var n = notes[k];
                float dur = n.beats * beat;
                int a = (int)(t0 * SR), len = (int)(dur * SR);
                bool last = k == notes.Length - 1;
                for (int i = 0; i < len + (last ? (int)(0.5f * SR) : 0) && a + i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    float u = Math.Min(1f, t / dur);
                    // portamento curto vindo da nota anterior; a nota "errada" escorrega até centsEnd
                    float glide = (float)Math.Exp(-t / 0.035);
                    double hz = n.hz * (1 - glide) + prevHz * glide;
                    if (n.centsEnd != 0f) hz *= Math.Pow(2, n.centsEnd * Math.Pow(Math.Max(0, u - 0.25f) / 0.75f, 1.6) / 1200.0);
                    // vibrato que entra devagar; na frase errada vira dois vibratos batendo
                    float vibK = Math.Min(1f, Math.Max(0f, (t - 0.25f) / 0.4f));
                    double vib = 0.0045 * vibK * Math.Sin(TAU * 5.1 * t);
                    if (wrong && k > 0) vib += 0.006 * vibK * Math.Sin(TAU * 6.7 * t + 1.3);
                    ph += TAU * hz * (1 + vib) / SR;
                    float env = Math.Min(1f, t / 0.07f);
                    if (!last) env *= Math.Min(1f, (dur - t) / 0.06f + 0.25f);
                    else env *= (float)Math.Exp(-Math.Max(0, t - dur * 0.7f) / 0.45f);
                    if (wrong && k == notes.Length - 1) env *= 1f - Math.Max(0f, (u - 0.8f) / 0.2f);   // ele para de tocar
                    env = Math.Max(env, 0f);
                    float s = (float)(Math.Sin(ph) + 0.22 * Math.Sin(2 * ph + 0.3) + 0.07 * Math.Sin(3 * ph + 1.1));
                    // sopro: ruído na região do 3º harmônico + "chiff" no ataque
                    float chiff = (float)Math.Exp(-t / 0.03) * 0.5f;
                    float b = air[Math.Min(a + i, air.Length - 1)] * (0.06f + chiff);
                    x[a + i] += (s * 0.32f + b) * env;
                    if (n.ghost > 0f)
                    {
                        // fantasma em trítono, crescendo junto com a nota que entorta
                        phg += TAU * hz * 0.7071 / SR;
                        x[a + i] += (float)Math.Sin(phg) * 0.32f * n.ghost * env * Math.Min(1f, u * 2f);
                    }
                }
                prevHz = n.hz;
                t0 += dur;
            }
            RuptureSynth.Reverb(x, 0.22f, 0.8f, 0.4f);
            RuptureSynth.Normalize(x, 0.7f);
            return x;
        }

        public static float[] FlutePhrase() => Flute(PhraseA, 72f, false);
        public static float[] FluteWrong() => Flute(PhraseWrong, 72f, true);

        // ------------------------------------------------------------ a Fenda abrindo

        /// <summary>O primeiro ponto de luz: um tom fino, alto, em trítono consigo mesmo, e um grave subindo.</summary>
        public static float[] FendaPin(float sec = 3f)
        {
            var x = RuptureSynth.New(sec);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR, u = t / sec;
                p1 += TAU * 2960.0 / SR; p2 += TAU * 2093.0 * 1.003 / SR; p3 += TAU * (41.0 + 14 * u) / SR;
                float env = (float)Math.Sin(Math.PI * Math.Min(1, u * 1.2)) ;
                x[i] = (float)(Math.Sin(p1) * 0.12 + Math.Sin(p2) * 0.09) * env * u + (float)Math.Sin(p3) * 0.5f * u * u;
            }
            RuptureSynth.Reverb(x, 0.5f, 0.92f, 0.4f);
            RuptureSynth.Normalize(x, 0.5f);
            return x;
        }

        /// <summary>A rachadura correndo: vidro sob tensão (parciais deslizando para cima) e estalos ressonantes.</summary>
        public static float[] FendaCrack(float sec = 4f)
        {
            var x = RuptureSynth.New(sec);
            float[] base0 = { 330f, 523f, 761f, 1093f, 1518f };
            for (int p = 0; p < base0.Length; p++)
            {
                double ph = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR, u = t / sec;
                    double f = base0[p] * (1 + 0.35 * u * u + 0.004 * Math.Sin(TAU * (3 + p) * t));
                    ph += TAU * f / SR;
                    x[i] += (float)Math.Sin(ph) * 0.12f / (1 + p * 0.3f) * u * (0.6f + 0.4f * (float)Math.Sin(TAU * (0.7 + p * 0.31) * t));
                }
            }
            // estalos: impulsos com ressonância aguda, cada vez mais frequentes
            uint s = 555u;
            float tc = 0.2f;
            while (tc < sec - 0.05f)
            {
                float f = 1800 + Rnd(ref s) * 3500;
                int o = (int)(tc * SR), len = (int)(0.08f * SR);
                float amp = 0.25f + Rnd(ref s) * 0.35f;
                for (int i = 0; i < len && o + i < x.Length; i++)
                    x[o + i] += (float)(Math.Sin(TAU * f * i / SR) * Math.Exp(-i / (0.008 * SR))) * amp;
                tc += 0.35f * (1 - tc / sec) + 0.03f + Rnd(ref s) * 0.12f;
            }
            RuptureSynth.Reverb(x, 0.45f, 0.9f, 0.35f);
            RuptureSynth.Normalize(x, 0.6f);
            return x;
        }

        /// <summary>O céu estilhaçando: um baque grave, um acorde inarmônico enorme e cacos de vidro caindo.</summary>
        public static float[] FendaShatter(float sec = 7f)
        {
            var x = RuptureSynth.New(sec);
            // baque
            double pb = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                pb += TAU * (34 + 40 * Math.Exp(-t / 0.15)) / SR;
                x[i] += (float)(Math.Sin(pb) * Math.Exp(-t / 0.7)) * 0.8f;
            }
            // acorde da série da Fenda (inarmônico), ataque rápido, cauda que escorrega
            float[] fa = { 1f, 2.13f, 3.07f, 4.41f, 5.62f, 6.93f, 8.18f, 9.71f, 11.3f, 13.6f };
            for (int p = 0; p < fa.Length; p++)
            {
                double ph = 0, ph2 = 0;
                double f = 110 * fa[p];
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += TAU * f * (1 - 0.01 * t / sec) / SR; ph2 += TAU * (f + 1.7 + p * 0.4) / SR;
                    float env = Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t / (3.2 - p * 0.22));
                    x[i] += (float)(Math.Sin(ph) + Math.Sin(ph2) * 0.6) * env * 0.16f / (1 + p * 0.25f);
                }
            }
            // cacos: pings agudos que caem e se espalham no tempo
            uint s = 2024u;
            for (int k = 0; k < 46; k++)
            {
                float tc = 0.02f + (float)Math.Pow(Rnd(ref s), 1.8) * (sec * 0.6f);
                float f = 2200 + Rnd(ref s) * 5200;
                int o = (int)(tc * SR), len = (int)(0.5f * SR);
                float amp = (0.1f + Rnd(ref s) * 0.2f) * (float)Math.Exp(-tc / 2f);
                for (int i = 0; i < len && o + i < x.Length; i++)
                {
                    double ff = f * (1 - 0.02 * i / (double)len);
                    x[o + i] += (float)(Math.Sin(TAU * ff * i / SR) * Math.Exp(-i / (0.06 * SR))) * amp;
                }
            }
            RuptureSynth.SoftClip(x, 1.5f);
            RuptureSynth.Reverb(x, 0.55f, 0.93f, 0.4f);
            RuptureSynth.Normalize(x, 0.85f);
            return x;
        }

        /// <summary>A Fenda "inspira" antes de soltar os sete: a nota impossível ao contrário, crescendo e cortando.</summary>
        public static float[] Inhale(float sec = 1.7f)
        {
            var src = RuptureSynth.ImpossibleNote(sec + 0.5f, 0.05f);
            var x = RuptureSynth.New(sec);
            for (int i = 0; i < x.Length; i++)
            {
                float u = i / (float)x.Length;
                x[i] = src[src.Length - 1 - i] * u * u * u;
            }
            var hiss = Noise(sec, 77u);
            var swept = RuptureSynth.New(sec);
            float y = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float u = i / (float)x.Length;
                float a = (float)(1.0 - Math.Exp(-TAU * (300 + 4000 * u * u) / SR));
                y += a * (hiss[i] - y);
                x[i] += y * 0.5f * u * u;
            }
            RuptureSynth.Fade(x, 0.05f, 0.01f);
            RuptureSynth.Normalize(x, 0.7f);
            return x;
        }

        /// <summary>Nascimento de um dos sete: um ping vítreo na frequência dele + um baque curto.</summary>
        public static float[] SigBirth(int i, float sec = 2.2f)
        {
            var x = RuptureSynth.New(sec);
            float f = RuptureSynth.SignatureHz[i];
            double p1 = 0, p2 = 0, p3 = 0, pb = 0;
            for (int k = 0; k < x.Length; k++)
            {
                float t = k / (float)SR;
                p1 += TAU * f / SR; p2 += TAU * f * 2.76 / SR; p3 += TAU * f * 5.4 / SR; pb += TAU * (f * 0.25 + 30 * Math.Exp(-t / 0.05)) / SR;
                float env = Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t / 0.7f);
                x[k] = (float)(Math.Sin(p1) + 0.5 * Math.Sin(p2) * Math.Exp(-t / 0.25) + 0.25 * Math.Sin(p3) * Math.Exp(-t / 0.12)) * env * 0.5f
                     + (float)(Math.Sin(pb) * Math.Exp(-t / 0.12)) * 0.5f;
            }
            RuptureSynth.Reverb(x, 0.5f, 0.9f, 0.4f);
            RuptureSynth.Normalize(x, 0.7f);
            return x;
        }

        /// <summary>Depois do clarão, antes do som: um zumbido fino que bate (o ouvido "sabe" que vem algo).</summary>
        public static float[] RingHigh(float sec = 5f)
        {
            var x = RuptureSynth.New(sec);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                p1 += TAU * 3520.0 / SR; p2 += TAU * 3527.3 / SR;
                float env = Math.Min(1f, t / 0.3f) * (float)Math.Exp(-t / 3.2f);
                x[i] = (float)(Math.Sin(p1) + Math.Sin(p2)) * 0.2f * env;
            }
            RuptureSynth.Reverb(x, 0.3f);
            RuptureSynth.Normalize(x, 0.35f);
            return x;
        }

        // ------------------------------------------------------------ o impacto, a rajada

        /// <summary>
        /// O som do impacto chegando à vila: sub (queda grave), pressão (o "empurrão" de ar), corpo
        /// inarmônico, textura (estalos de detritos e ar rasgando, ao longe), o rolar nas colinas, e o
        /// SINO GIGANTE DESAFINADO (a nota batendo no chão, duas cópias batendo, escorregando para baixo),
        /// com cauda longa no vale.
        /// </summary>
        public static float[] Impact(float sec = 13f)
        {
            var x = RuptureSynth.New(sec);
            // sub
            double ps = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                ps += TAU * (26 + 30 * Math.Exp(-t / 0.4)) / SR;
                x[i] += (float)(Math.Sin(ps) * Math.Exp(-t / 1.6) * Math.Min(1.0, t / 0.03)) * 1.0f;
            }
            // pressão: ruído muito grave com subida de 0,25 s
            var pr = Noise(sec, 101u);
            RuptureSynth.Lowpass(pr, 90); RuptureSynth.Lowpass(pr, 90); RuptureSynth.Lowpass(pr, 110);
            for (int i = 0; i < x.Length; i++) { float t = i / (float)SR; x[i] += pr[i] * 9f * Math.Min(1f, t / 0.25f) * (float)Math.Exp(-t / 1.4f); }
            // corpo (audível em caixinha de notebook)
            float[] body = { 73f, 117f, 166f, 241f, 331f, 452f };
            for (int p = 0; p < body.Length; p++)
            {
                double pp = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    pp += TAU * body[p] * (1 - 0.025 * Math.Min(1.0, t / 4)) / SR;
                    x[i] += (float)(Math.Sin(pp) * Math.Exp(-t / (2.6 - p * 0.3)) * Math.Min(1.0, t / 0.015)) * (0.5f / (1 + p * 0.4f));
                }
            }
            // textura: estalos de detritos e ar rasgando (banda média/alta, decaindo), ao longe
            uint s = 4321u;
            for (int k = 0; k < 160; k++)
            {
                float tc = (float)Math.Pow(Rnd(ref s), 1.5) * 3.5f + 0.05f;
                float f = 600 + Rnd(ref s) * 2600;
                int o = (int)(tc * SR), len = (int)(0.05f * SR);
                float amp = (0.08f + Rnd(ref s) * 0.12f) * (float)Math.Exp(-tc / 1.4f);
                for (int i = 0; i < len && o + i < x.Length; i++)
                    x[o + i] += (float)(Math.Sin(TAU * f * i / SR) * Math.Exp(-i / (0.006 * SR))) * amp;
            }
            var tear = RuptureSynth.Bandpass(Noise(sec, 202u), 900f, 0.8f);
            for (int i = 0; i < x.Length; i++) { float t = i / (float)SR; x[i] += tear[i] * 0.9f * Math.Min(1f, t / 0.05f) * (float)Math.Exp(-t / 0.9f); }
            // rolando pelo vale (ecos nas colinas)
            var mid = RuptureSynth.Bandpass(Noise(sec, 303u), 230f, 1.1f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float echoes = (float)(Math.Exp(-t / 1.5) + 0.5 * Math.Exp(-Math.Pow((t - 1.3) / 0.4, 2)) + 0.3 * Math.Exp(-Math.Pow((t - 2.7) / 0.6, 2)) + 0.18 * Math.Exp(-Math.Pow((t - 4.4) / 0.8, 2)));
                x[i] += mid[i] * 1.5f * echoes * Math.Min(1f, t / 0.03f);
            }
            // o sino gigante desafinado (série da Fenda), duas cópias batendo, escorregando para baixo
            float[] fa = { 1f, 2.13f, 3.07f, 4.41f, 5.62f, 6.93f, 8.18f };
            for (int p = 0; p < fa.Length; p++)
            {
                double pa = 0, pb2 = 0;
                double f = 65.4 * fa[p];
                for (int i = 0; i < x.Length; i++)
                {
                    float t = i / (float)SR;
                    double slide = 1 - 0.018 * Math.Min(1.0, t / 7);
                    pa += TAU * f * slide / SR; pb2 += TAU * (f * slide * 1.0085) / SR;
                    float env = (float)(Math.Min(1.0, t / 0.35) * Math.Exp(-t / (6.5 - p * 0.6)));
                    x[i] += (float)(Math.Sin(pa) + Math.Sin(pb2) * 0.8) * env * 0.22f / (1 + p * 0.3f);
                }
            }
            RuptureSynth.SoftClip(x, 1.7f);
            RuptureSynth.Reverb(x, 0.55f, 0.94f, 0.55f);
            RuptureSynth.Normalize(x, 0.97f);
            return x;
        }

        /// <summary>A rajada da onda de pressão atravessando a vila (sopro que sobe de repente e tremula).</summary>
        public static float[] Gust(float sec = 4.5f)
        {
            var n = Noise(sec, 606u);
            var x = RuptureSynth.New(sec);
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float fc = 250 + 1100 * (float)Math.Exp(-Math.Pow((t - 0.5) / 0.5, 2)) + 300 * (float)Math.Exp(-t / 2f);
                float a = (float)(1.0 - Math.Exp(-TAU * fc / SR));
                y1 += a * (n[i] - y1); y2 += a * (y1 - y2);
                float flutter = 0.75f + 0.25f * (float)Math.Sin(TAU * (9 + 4 * Math.Sin(t * 1.3)) * t);
                float env = Math.Min(1f, t / 0.18f) * (float)Math.Exp(-t / 1.5f);
                x[i] = (y1 - y2 * 0.5f) * env * flutter * 3.2f;
            }
            RuptureSynth.Reverb(x, 0.25f);
            RuptureSynth.Normalize(x, 0.8f);
            return x;
        }

        /// <summary>Madeira e metal da vila chacoalhando com a onda (janelas, telhas, correntes).</summary>
        public static float[] Rattle(float sec = 3f)
        {
            var x = RuptureSynth.New(sec);
            uint s = 7070u;
            float t = 0.02f;
            while (t < sec - 0.1f)
            {
                bool wood = Rnd(ref s) < 0.65f;
                float f = wood ? 160 + Rnd(ref s) * 260 : 2400 + Rnd(ref s) * 2600;
                int o = (int)(t * SR), len = (int)((wood ? 0.09f : 0.05f) * SR);
                float amp = (wood ? 0.5f : 0.25f) * (0.4f + Rnd(ref s) * 0.6f) * (float)Math.Exp(-t / 1.1f);
                for (int i = 0; i < len && o + i < x.Length; i++)
                    x[o + i] += (float)(Math.Sin(TAU * f * i / SR) * Math.Exp(-i / ((wood ? 0.018 : 0.01) * SR))) * amp;
                t += 0.015f + Rnd(ref s) * 0.06f + t * 0.06f;
            }
            RuptureSynth.Reverb(x, 0.2f);
            RuptureSynth.Normalize(x, 0.6f);
            return x;
        }
    }
}

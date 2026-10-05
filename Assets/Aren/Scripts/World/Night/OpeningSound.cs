using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Paisagem sonora da noite da Ruptura. Antes da Fenda, Campanula soa harmônica (sinos
    /// afinados, carrilhão, grilos, coruja, uma melodia de alaúde vinda da vila, vento). Com o
    /// Contracanto (Corruption 0→1) os MESMOS sons adoecem: os loops perdem a afinação devagar, o
    /// carrilhão toca parciais fora da série e notas que não morrem, a melodia escorrega e uma
    /// nota fica presa, o vento ganha dois tons em trítono, os grilos saem de sincronia. Nunca
    /// silêncio: depois da abertura a vila continua viva — porém musicalmente doente.
    /// Os clipes são sintetizados (RuptureSynth) numa thread quando o jogo abre.
    /// </summary>
    public class OpeningSound : MonoBehaviour
    {
        public static OpeningSound Instance { get; private set; }
        public bool Ready { get; private set; }
        public float Corruption;          // alvo; o valor real anda devagar
        public float ambientLevel = 1f;   // 1 na abertura; menor no gameplay
        public bool chimesOn = true;
        /// <summary>Silêncio RELATIVO (0..1): grilos, vento e carrilhão abaixam — nunca somem de vez.</summary>
        public float duck = 1f;
        /// <summary>O alaúde distante da vila (cala enquanto o Aren toca a flauta).</summary>
        public float lute = 1f;
        float duckNow = 1f, luteNow = 1f;
        float c;                           // corrupção atual
        public float CurrentCorruption => c;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource crA, crB, wind, mel, melBad, fenda;
        float nextChime = 2f, nextOwl = 3f, nextTowerToll = -1f;
        AudioListener listener; float listenerCheck;
        bool begun, gameplay;

        class Tracked { public AudioSource src; public System.Func<Vector3> pos; public System.Func<float> gain; public float vol; }
        readonly List<Tracked> tracked = new List<Tracked>();

        public static OpeningSound Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Paisagem sonora (Ruptura)");
            Instance = go.AddComponent<OpeningSound>();
            Instance.StartSynth();
            return Instance;
        }

        // síntese em paralelo, em ordem de necessidade (a flauta e os sinos primeiro); cada clipe é
        // entregue assim que fica pronto — a abertura pode começar sem esperar o banco inteiro
        readonly System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, float[]>> done = new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, float[]>>();
        int total, finished, failed;

        /// <summary>
        /// Todos os sons da noite, em ordem de necessidade (a flauta e os sinos primeiro). O editor grava
        /// cada um como WAV em Resources/Audio/Rupture (RuptureBake); o jogo carrega de lá e só sintetiza
        /// o que faltar.
        /// </summary>
        public static List<KeyValuePair<string, System.Func<float[]>>> Generators()
        {
            var gen = new List<KeyValuePair<string, System.Func<float[]>>>();
            void G(string id, System.Func<float[]> f) => gen.Add(new KeyValuePair<string, System.Func<float[]>>(id, f));
            var tuned = RuptureSynth.Tuned;
            G("flute_a", RuptureSynthCinematic.FlutePhrase);
            G("bell_sympathy", () => RuptureSynth.Bell(196f, 8f, new RuptureSynth.BellOpts { strike = 0f, attack = 1.1f, decayScale = 1.5f, beatHz = 0.7f }));
            G("crickets_a", () => RuptureSynth.Crickets(7.3f, 1));
            G("crickets_b", () => RuptureSynth.Crickets(6.1f, 2));
            G("windtone", () => RuptureSynth.WindTone(9f));
            G("tower_tuned", () =>
            {
                var tw = RuptureSynth.New(7f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(392f, 6f, tuned), 0f, 0.5f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(493.88f, 6f, tuned), 0.38f, 0.42f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(587.33f, 6f, tuned), 0.74f, 0.38f);
                return RuptureSynth.Distant(tw, 2600, 0.55f);
            });
            float[] pent = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f };
            for (int i = 0; i < pent.Length; i++) { float f = pent[i]; G("chime" + i, () => RuptureSynth.Chime(f, false)); }
            G("owl", RuptureSynth.Owl);
            G("melody", () => RuptureSynth.Melody(false));
            G("flute_wrong", RuptureSynthCinematic.FluteWrong);
            G("bell", () => RuptureSynth.Bell(196f, 7f, tuned));
            G("bell_wobble", () => { var wob = tuned; wob.wobbleDepth = 0.03f; wob.wobbleRate = 5.7f; wob.wrongPartial = 0.32f; wob.beatHz = 2.3f; wob.decayScale = 1.3f; return RuptureSynth.Bell(196f, 8f, wob); });
            G("tower_detuned", () =>
            {
                var td = RuptureSynth.New(7.5f);
                var o1 = tuned; o1.preEcho = 0.32f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(392f, 6f, o1), 0f, 0.5f);
                var o2 = tuned; o2.detuneCents = -70f; o2.beatHz = 3.1f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(493.88f, 6f, o2), 0.7f, 0.42f);
                var o3 = tuned; o3.detuneCents = 45f; o3.wobbleDepth = 0.02f; o3.wobbleRate = 3.3f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(587.33f, 6f, o3), 1.06f, 0.38f);
                return RuptureSynth.Distant(td, 2600, 0.55f);
            });
            G("tower_freeze", () => { var fr = tuned; fr.freezeAt = 0.9f; fr.freezeFor = 3.2f; fr.detuneCents = -18f; return RuptureSynth.Distant(RuptureSynth.Bell(329.6f, 9f, fr), 2400, 0.6f); });
            for (int i = 0; i < pent.Length; i++) { float f = pent[i]; G("chime_bad" + i, () => RuptureSynth.Chime(f, true)); }
            G("melody_bad", () => RuptureSynth.Melody(true));
            G("bell_reverse", () => RuptureSynth.Reverse(RuptureSynth.Bell(196f, 4f, tuned)));
            G("fenda_pin", () => RuptureSynthCinematic.FendaPin());
            G("fenda_crack", () => RuptureSynthCinematic.FendaCrack());
            G("fenda_shatter", () => RuptureSynthCinematic.FendaShatter());
            G("fenda", () => RuptureSynth.ImpossibleNote(18f, 5f));
            G("inhale", () => RuptureSynthCinematic.Inhale());
            for (int i = 0; i < 7; i++) { int k = i; G("birth" + k, () => RuptureSynthCinematic.SigBirth(k)); G("sig" + k, () => RuptureSynth.Signature(k, 6f)); }
            G("sig_pass", () => RuptureSynth.Signature(0, 5.5f, true));
            G("ring", () => RuptureSynthCinematic.RingHigh());
            G("impact2", () => RuptureSynthCinematic.Impact());
            G("gust", () => RuptureSynthCinematic.Gust());
            G("rattle", () => RuptureSynthCinematic.Rattle());
            G("metal", () => RuptureSynth.MetalShimmer(5f));
            G("clinks", () => RuptureSynth.Clinks(2.2f));
            G("fenda_loop", () => RuptureSynth.MakeLoop(RuptureSynth.ImpossibleNote(14f, 0.05f), 2f));
            G("impact", () => RuptureSynth.Impact(10f));
            return gen;
        }

        void StartSynth()
        {
            // sons pré-gerados no projeto (instantâneo); o que faltar é sintetizado com prioridade baixa
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Rupture")) clips[c.name] = c;
            var gen = Generators();
            gen.RemoveAll(g => clips.ContainsKey(g.Key));
            total = gen.Count;
            if (total == 0) { Ready = true; Debug.Log($"[Ruptura] {clips.Count} sons pré-gerados carregados"); return; }
            Debug.Log($"[Ruptura] {clips.Count} pré-gerados; sintetizando {total}");
            int next = -1;
            int workers = 2;   // pouco: o prólogo é desenhado na CPU e não pode engasgar
            for (int w = 0; w < workers; w++)
                new System.Threading.Thread(() =>
                {
                    while (true)
                    {
                        int i = System.Threading.Interlocked.Increment(ref next);
                        if (i >= gen.Count) break;
                        float[] a = null;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            a = gen[i].Value();
                            if (sw.ElapsedMilliseconds > 400) UnityEngine.Debug.Log($"[Ruptura] {gen[i].Key}: {sw.ElapsedMilliseconds} ms");
                            for (int k = 0; k < a.Length; k++) if (float.IsNaN(a[k]) || float.IsInfinity(a[k])) a[k] = 0f;
                        }
                        catch (System.Exception e) { System.Threading.Interlocked.Increment(ref failed); UnityEngine.Debug.LogError("[Ruptura] síntese de " + gen[i].Key + " falhou: " + e.Message); }
                        done.Enqueue(new KeyValuePair<string, float[]>(gen[i].Key, a));
                    }
                }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "RupturaSintese" }.Start();
        }

        /// <summary>O clipe já existe? (a abertura espera os primeiros antes de começar)</summary>
        public bool Has(string id) => clips.ContainsKey(id);

        void Awake()
        {
            for (int i = 0; i < 18; i++) pool.Add(NewSource("voz" + i));
            crA = NewSource("grilos_a", true); crB = NewSource("grilos_b", true);
            wind = NewSource("vento_tonal", true); mel = NewSource("melodia", true); melBad = NewSource("melodia_doente", true);
            fenda = NewSource("fenda", true);
            crA.panStereo = -0.55f; crB.panStereo = 0.6f;
        }

        AudioSource NewSource(string n, bool loop = false)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f; s.dopplerLevel = 0f; s.priority = 40;
            return s;
        }

        static float Fx => ArenAudio.Effects;
        static float Mu => ArenAudio.Music;

        public AudioClip Clip(string id) => clips.TryGetValue(id, out var c) ? c : null;

        AudioSource Free()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            return pool[0];
        }

        /// <summary>Som sem posição (pan manual).</summary>
        public AudioSource Play(string id, float vol, float pan = 0f, float pitch = 1f, float delay = 0f)
        {
            var clip = Clip(id);
            if (clip == null) return null;
            var s = Free();
            s.transform.localPosition = Vector3.zero;
            s.spatialBlend = 0f; s.clip = clip; s.volume = vol * Fx; s.panStereo = pan; s.pitch = pitch; s.loop = false;
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
            return s;
        }

        /// <summary>Som com posição no mundo (o sino ao lado do Aren, os sinos da torre).</summary>
        public AudioSource PlayAt(string id, Vector3 pos, float vol, float minDist = 3f, float maxDist = 80f, float pitch = 1f)
        {
            var clip = Clip(id);
            if (clip == null) return null;
            var s = Free();
            s.transform.position = pos;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = minDist; s.maxDistance = maxDist;
            s.clip = clip; s.volume = vol * Fx; s.panStereo = 0f; s.pitch = pitch; s.loop = false;
            s.Play();
            return s;
        }

        /// <summary>Som que segue algo no céu: pan e volume calculados pela direção vista de quem ouve.</summary>
        public AudioSource PlayTracked(string id, System.Func<Vector3> pos, System.Func<float> gain, float vol, float pitch = 1f)
        {
            var s = Play(id, vol, 0f, pitch);
            if (s != null) tracked.Add(new Tracked { src = s, pos = pos, gain = gain, vol = vol });
            return s;
        }

        /// <summary>Começa a paisagem da noite (chamado no começo da abertura).</summary>
        public void Begin()
        {
            begun = true;
            StartLoop(crA, "crickets_a"); StartLoop(crB, "crickets_b");
            StartLoop(wind, "windtone"); StartLoop(mel, "melody"); StartLoop(melBad, "melody_bad");
            if (melBad.clip != null && mel.clip != null) melBad.timeSamples = Mathf.Min(mel.timeSamples, melBad.clip.samples - 1);
        }

        void StartLoop(AudioSource s, string id)
        {
            var clip = Clip(id);
            if (clip == null || (s.isPlaying && s.clip == clip)) return;
            s.clip = clip; s.volume = 0f; s.Play();
        }

        /// <summary>A Fenda abriu: a nota impossível entra (e fica, baixinha, no gameplay).</summary>
        public void OpenFenda()
        {
            Play("fenda", 0.5f, 0.25f);   // presente, mas sem cobrir os sinos que desafinam
            Invoke(nameof(StartFendaLoop), 15f);
        }

        /// <summary>Abertura v2: a nota impossível sustentada entra agora (depois do estilhaço).</summary>
        public void SustainFenda(float vol = 0.42f) { Play("fenda", vol, 0.2f); CancelInvoke(nameof(StartFendaLoop)); Invoke(nameof(StartFendaLoop), 14f); }

        void StartFendaLoop() { StartLoop(fenda, "fenda_loop"); if (fendaLoopVol <= 0f) fendaLoopVol = 0.32f; }
        float fendaLoopVol;

        /// <summary>Depois da abertura: a vila continua soando, mais baixa, e doente.</summary>
        public void EnterGameplay()
        {
            gameplay = true;
            ambientLevel = 0.6f;
            Corruption = Mathf.Max(Corruption, 0.6f);
            nextTowerToll = Time.time + Random.Range(18f, 30f);
            fendaLoopVol = 0.22f;   // a nota impossível fica, baixinha, por baixo do jogo
            if (!fenda.isPlaying) StartFendaLoop();
        }

        void Update()
        {
            while (done.TryDequeue(out var kv))
            {
                finished++;
                if (kv.Value == null) continue;
                var clip = AudioClip.Create(kv.Key, kv.Value.Length, 1, RuptureSynth.SR, false);
                clip.SetData(kv.Value, 0);
                clips[kv.Key] = clip;
                if (kv.Key == "flute_a" || kv.Key == "crickets_a" || kv.Key == "bell_sympathy") Debug.Log($"[Ruptura] {kv.Key} pronto em {Time.realtimeSinceStartup:0.0} s");
                if (begun && (kv.Key.StartsWith("crickets") || kv.Key == "windtone" || kv.Key.StartsWith("melody"))) Begin();
            }
            if (!Ready && total > 0 && finished >= total)
            {
                Ready = true;
                Debug.Log($"[Ruptura] {clips.Count} sons sintetizados em {Time.realtimeSinceStartup:0.0} s desde o início" + (failed > 0 ? $" ({failed} falharam)" : ""));
                if (begun) Begin();
            }
            if (clips.Count == 0) return;
            float dt = Time.deltaTime, t = Time.time;
            c = Mathf.MoveTowards(c, Corruption, dt * 0.18f);
            duckNow = Mathf.MoveTowards(duckNow, duck, dt * (duck < duckNow ? 0.9f : 0.25f));
            luteNow = Mathf.MoveTowards(luteNow, lute, dt * 0.5f);
            float lvl = begun ? ambientLevel : 0f;
            float lvlD = lvl * duckNow;

            // os loops perdem a afinação devagar (deriva orgânica, não aleatória por quadro)
            float Drift(float seed, float amt) => 1f + c * amt * (Mathf.PerlinNoise(t * 0.17f, seed) - 0.5f);
            crA.pitch = Drift(1.3f, 0.07f); crB.pitch = Drift(5.1f, 0.09f) * (1f + 0.03f * c);
            crA.volume = Mathf.MoveTowards(crA.volume, 0.32f * lvlD * Fx, dt * 0.4f);
            crB.volume = Mathf.MoveTowards(crB.volume, 0.26f * lvlD * Fx, dt * 0.4f);
            wind.pitch = Drift(8.7f, 0.04f);
            wind.volume = Mathf.MoveTowards(wind.volume, 0.42f * Mathf.SmoothStep(0f, 1f, (c - 0.15f) / 0.6f) * Mathf.Lerp(0.5f, 1f, duckNow) * lvl * Fx, dt * 0.3f);
            float bad = Mathf.SmoothStep(0f, 1f, (c - 0.25f) / 0.35f);
            mel.pitch = melBad.pitch = Drift(11.2f, 0.05f);
            mel.volume = Mathf.MoveTowards(mel.volume, 0.2f * (1f - bad) * lvlD * luteNow * Mu, dt * 0.4f);
            melBad.volume = Mathf.MoveTowards(melBad.volume, 0.2f * bad * lvlD * luteNow * Mu, dt * 0.4f);
            if (fenda.isPlaying) fenda.volume = Mathf.MoveTowards(fenda.volume, fendaLoopVol * Fx, dt * 0.2f);

            if (begun && chimesOn && t >= nextChime)
            {
                // carrilhão ao vento: afinado no começo; com o Contracanto, parciais erradas
                int k = Random.Range(0, 5);
                bool wrong = Random.value < c;
                Play((wrong ? "chime_bad" : "chime") + k, 0.22f * lvlD, Random.Range(-0.4f, 0.4f), wrong ? Drift(k, 0.06f) : 1f);
                nextChime = t + Random.Range(gameplay ? 6f : 2.2f, gameplay ? 14f : 5f);
            }
            if (begun && c < 0.1f && t >= nextOwl) { Play("owl", 0.35f * lvlD, -0.5f); nextOwl = t + Random.Range(9f, 15f); }
            if (gameplay && nextTowerToll > 0f && t >= nextTowerToll)
            {
                var tower = GameObject.Find("BellTower");
                Vector3 p = tower != null ? tower.transform.position + Vector3.up * 21f : new Vector3(0, 21, 36);
                PlayAt(Random.value < 0.5f ? "tower_detuned" : "tower_freeze", p, 0.55f, 40f, 320f);
                nextTowerToll = t + Random.Range(25f, 45f);
            }

            // sons do céu: pan/volume pela direção vista de quem ouve
            if (t >= listenerCheck) { listenerCheck = t + 0.5f; listener = null; foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) { listener = l; break; } }
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                var tr = tracked[i];
                if (tr.src == null || !tr.src.isPlaying) { tracked.RemoveAt(i); continue; }
                if (listener == null) continue;
                var lt = listener.transform;
                var d = (tr.pos() - lt.position).normalized;
                tr.src.panStereo = Mathf.Clamp(Vector3.Dot(lt.right, d) * 0.9f, -0.95f, 0.95f);
                tr.src.volume = tr.vol * Fx * Mathf.Clamp01(tr.gain()) * (0.75f + 0.25f * Vector3.Dot(lt.forward, d));
            }
        }

        /// <summary>Pan de uma direção no mundo, visto de quem ouve agora (para sons de uma vez só).</summary>
        public float PanTo(Vector3 worldPos)
        {
            if (listener == null) foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) { listener = l; break; }
            if (listener == null) return 0f;
            var d = (worldPos - listener.transform.position).normalized;
            return Mathf.Clamp(Vector3.Dot(listener.transform.right, d) * 0.9f, -0.95f, 0.95f);
        }
    }
}

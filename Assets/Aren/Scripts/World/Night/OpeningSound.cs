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
        float c;                           // corrupção atual
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        Task<Dictionary<string, float[]>> job;
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

        void StartSynth()
        {
            job = Task.Run(() =>
            {
                var d = new Dictionary<string, float[]>();
                var tuned = RuptureSynth.Tuned;
                d["bell"] = RuptureSynth.Bell(196f, 7f, tuned);
                var wob = tuned; wob.wobbleDepth = 0.03f; wob.wobbleRate = 5.7f; wob.wrongPartial = 0.32f; wob.beatHz = 2.3f; wob.decayScale = 1.3f;
                d["bell_wobble"] = RuptureSynth.Bell(196f, 8f, wob);
                var sym = new RuptureSynth.BellOpts { strike = 0f, attack = 1.1f, decayScale = 1.5f, beatHz = 0.7f };
                d["bell_sympathy"] = RuptureSynth.Bell(196f, 8f, sym);
                d["bell_reverse"] = RuptureSynth.Reverse(RuptureSynth.Bell(196f, 4f, tuned));
                // os sinos da torre respondendo de longe: acorde afinado / depois desafinado
                var tw = RuptureSynth.New(7f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(392f, 6f, tuned), 0f, 0.5f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(493.88f, 6f, tuned), 0.38f, 0.42f);
                RuptureSynth.Mix(tw, RuptureSynth.Bell(587.33f, 6f, tuned), 0.74f, 0.38f);
                d["tower_tuned"] = RuptureSynth.Distant(tw, 2600, 0.55f);
                var td = RuptureSynth.New(7.5f);
                var o1 = tuned; o1.preEcho = 0.32f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(392f, 6f, o1), 0f, 0.5f);
                var o2 = tuned; o2.detuneCents = -70f; o2.beatHz = 3.1f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(493.88f, 6f, o2), 0.7f, 0.42f);
                var o3 = tuned; o3.detuneCents = 45f; o3.wobbleDepth = 0.02f; o3.wobbleRate = 3.3f;
                RuptureSynth.Mix(td, RuptureSynth.Bell(587.33f, 6f, o3), 1.06f, 0.38f);
                d["tower_detuned"] = RuptureSynth.Distant(td, 2600, 0.55f);
                var fr = tuned; fr.freezeAt = 0.9f; fr.freezeFor = 3.2f; fr.detuneCents = -18f;
                d["tower_freeze"] = RuptureSynth.Distant(RuptureSynth.Bell(329.6f, 9f, fr), 2400, 0.6f);
                float[] pent = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f };
                for (int i = 0; i < pent.Length; i++) { d["chime" + i] = RuptureSynth.Chime(pent[i], false); d["chime_bad" + i] = RuptureSynth.Chime(pent[i], true); }
                d["crickets_a"] = RuptureSynth.Crickets(7.3f, 1);
                d["crickets_b"] = RuptureSynth.Crickets(6.1f, 2);
                d["windtone"] = RuptureSynth.WindTone(9f);
                d["melody"] = RuptureSynth.Melody(false);
                d["melody_bad"] = RuptureSynth.Melody(true);
                d["fenda"] = RuptureSynth.ImpossibleNote(18f, 5f);
                d["fenda_loop"] = RuptureSynth.MakeLoop(RuptureSynth.ImpossibleNote(14f, 0.05f), 2f);
                for (int i = 0; i < 7; i++) d["sig" + i] = RuptureSynth.Signature(i, 6f);
                d["sig_pass"] = RuptureSynth.Signature(0, 5.5f, true);
                d["impact"] = RuptureSynth.Impact(10f);
                d["metal"] = RuptureSynth.MetalShimmer(5f);
                d["clinks"] = RuptureSynth.Clinks(2.2f);
                d["owl"] = RuptureSynth.Owl();
                foreach (var k in new List<string>(d.Keys))
                {
                    var a = d[k];
                    for (int i = 0; i < a.Length; i++) if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) a[i] = 0f;
                }
                return d;
            });
        }

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
            if (!Ready && job != null && job.IsCompleted)
            {
                if (job.Exception == null)
                    foreach (var kv in job.Result)
                    {
                        var clip = AudioClip.Create(kv.Key, kv.Value.Length, 1, RuptureSynth.SR, false);
                        clip.SetData(kv.Value, 0);
                        clips[kv.Key] = clip;
                    }
                else Debug.LogError("[Ruptura] síntese falhou: " + job.Exception);
                Ready = true; job = null;
                if (begun) Begin();
            }
            if (!Ready) return;
            float dt = Time.deltaTime, t = Time.time;
            c = Mathf.MoveTowards(c, Corruption, dt * 0.18f);
            float lvl = begun ? ambientLevel : 0f;

            // os loops perdem a afinação devagar (deriva orgânica, não aleatória por quadro)
            float Drift(float seed, float amt) => 1f + c * amt * (Mathf.PerlinNoise(t * 0.17f, seed) - 0.5f);
            crA.pitch = Drift(1.3f, 0.07f); crB.pitch = Drift(5.1f, 0.09f) * (1f + 0.03f * c);
            crA.volume = Mathf.MoveTowards(crA.volume, 0.32f * lvl * Fx, dt * 0.4f);
            crB.volume = Mathf.MoveTowards(crB.volume, 0.26f * lvl * Fx, dt * 0.4f);
            wind.pitch = Drift(8.7f, 0.04f);
            wind.volume = Mathf.MoveTowards(wind.volume, 0.42f * Mathf.SmoothStep(0f, 1f, (c - 0.15f) / 0.6f) * lvl * Fx, dt * 0.3f);
            float bad = Mathf.SmoothStep(0f, 1f, (c - 0.25f) / 0.35f);
            mel.pitch = melBad.pitch = Drift(11.2f, 0.05f);
            mel.volume = Mathf.MoveTowards(mel.volume, 0.2f * (1f - bad) * lvl * Mu, dt * 0.4f);
            melBad.volume = Mathf.MoveTowards(melBad.volume, 0.2f * bad * lvl * Mu, dt * 0.4f);
            if (fenda.isPlaying) fenda.volume = Mathf.MoveTowards(fenda.volume, fendaLoopVol * Fx, dt * 0.2f);

            if (begun && chimesOn && t >= nextChime)
            {
                // carrilhão ao vento: afinado no começo; com o Contracanto, parciais erradas
                int k = Random.Range(0, 5);
                bool wrong = Random.value < c;
                Play((wrong ? "chime_bad" : "chime") + k, 0.22f * lvl, Random.Range(-0.4f, 0.4f), wrong ? Drift(k, 0.06f) : 1f);
                nextChime = t + Random.Range(gameplay ? 6f : 2.2f, gameplay ? 14f : 5f);
            }
            if (begun && c < 0.1f && t >= nextOwl) { Play("owl", 0.35f * lvl, -0.5f); nextOwl = t + Random.Range(9f, 15f); }
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

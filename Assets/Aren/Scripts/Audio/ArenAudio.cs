using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Aren
{
    public enum Sfx
    {
        Whoosh, ImpactLight, ImpactHeavy, Dodge, PerfectDodge, CounterHit, ParryWhiff, Hurt, Denied,
        PulseCast, PulseBoom, BladeCast, BladeHit, EchoCast, EchoGhost, ChargeLevel, ContracantoRelease,
        EnemyTelegraph, EnemyAttack, EnemyHurt, EnemyDeath, EnemySpawn, DeerGrowl, DeerCharge, DeerStep,
        UIMove, UIConfirm, UIBack, AbilityReady, Bell, BellCorrupt, Checkpoint, Footstep,
        Equip, Unequip, BodyFall, UIPage,
        Count
    }

    /// <summary>Vinhetas curtas (cravo/metais) que pontuam o roteiro.</summary>
    public enum Sting { Start, Clear, Defeat, Mystery, Boss, Chime }

    /// <summary>Tipo de chão para os passos.</summary>
    public enum Surface { Stone, Grass, Wood, Dirt }

    /// <summary>Timbre seco usado pelos quatro golpes basicos.</summary>
    public enum CombatInstrument { Flute, Ukulele }

    /// <summary>
    /// Fachada de áudio. Os sons são sintetizados (ArenSynth) numa thread de fundo quando
    /// o jogo abre; até ficarem prontos, Play() é ignorado. Volumes por categoria vêm das
    /// configurações (sem AudioMixer: menos uma dependência de asset de editor).
    /// </summary>
    public static class ArenAudio
    {
        public static float Master = 1f, Music = 0.65f, Effects = 1f, UI = 0.8f;
        public static CombatInstrument Instrument = CombatInstrument.Flute;
        public static bool Ready => AudioRunner.Instance.ready;
        public static void Preload() { var _ = AudioRunner.Instance; }

        public static void Play(Sfx s, Vector3 pos, float vol = 1f, float pitch = 1f) => AudioRunner.Instance.Play(s, pos, vol, pitch, false);
        public static void PlayUI(Sfx s, float vol = 1f, float pitch = 1f) => AudioRunner.Instance.Play(s, Vector3.zero, vol, pitch, true);
        public static void Note(int index, float vol, float brightness = 1f) => AudioRunner.Instance.Note(index, vol, brightness);
        public static void BeginChargeTone(Transform t) => AudioRunner.Instance.BeginCharge(t);
        public static void UpdateChargeTone(float p) => AudioRunner.Instance.UpdateCharge(p);
        public static void EndChargeTone() => AudioRunner.Instance.EndCharge();
        /// <summary>0 = exploração, 0.5 = combate, 1 = combate intenso.</summary>
        public static void SetIntensity(float v) => AudioRunner.Instance.targetIntensity = Mathf.Clamp01(v);
        public static void SetMusicEnabled(bool on) => AudioRunner.Instance.musicOn = on;
        public static void SetMenuMusic(bool on) => AudioRunner.Instance.SetMenuMusic(on);
        public static void ApplyVolumes() => AudioRunner.Instance.ApplyVolumes();
        public static string DebugStatus => AudioRunner.Instance.DebugStatus;
        public static void PlaySting(Sting s, float vol = 1f) => AudioRunner.Instance.PlaySting(s, vol);
        public static void Footstep(Vector3 pos, Surface surf, float vol) => AudioRunner.Instance.Footstep(pos, surf, vol);
        /// <summary>Laço ambiente posicional (riacho, por exemplo) com uma amostra de Resources/Audio/Samples.</summary>
        public static void AmbientLoop(string sample, Vector3 pos, float minDist, float maxDist, float vol) => AudioRunner.Instance.AmbientLoop(sample, pos, minDist, maxDist, vol);
    }

    [DefaultExecutionOrder(-150)]
    public class AudioRunner : MonoBehaviour
    {
        static AudioRunner instance;
        public static AudioRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ArenAudio");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioRunner>();
                    instance.Begin();
                }
                return instance;
            }
        }

        public bool ready;
        public float targetIntensity;
        public bool musicOn = true;
        float intensity;

        // banco gerado na thread
        class Bank
        {
            public Dictionary<int, float[][]> sfx = new Dictionary<int, float[][]>();
            public float[][] fluteNotes, ukuleleNotes;
            public float[] charge, wind, drone, drums, ostinato;
        }
        Bank bank;
        Task genTask;

        readonly Dictionary<int, AudioClip[]> clips = new Dictionary<int, AudioClip[]>();
        AudioClip[] fluteNoteClips, ukuleleNoteClips;
        readonly List<AudioSource> sources = new List<AudioSource>(24);
        readonly Dictionary<AudioSource, double> voiceEnds = new Dictionary<AudioSource, double>();
        readonly float[] lastPlay = new float[(int)Sfx.Count];
        AudioSource chargeSrc, windSrc, droneSrc, drumsSrc, ostSrc, stingSrc, menuMusicSrc;
        bool menuMusicRequested;

        // Amostras gravadas (400 Sounds Pack, Chequered Ink — uso comercial livre): tocam por
        // cima do som sintetizado para dar corpo ao golpe (o tom musical continua do synth).
        struct Layer { public AudioClip[] clips; public float gain; public bool replace; }
        readonly Dictionary<int, Layer> layers = new Dictionary<int, Layer>();
        readonly Dictionary<string, AudioClip> samples = new Dictionary<string, AudioClip>();
        AudioClip[][] steps;
        readonly int[] lastStep = new int[4];
        AudioClip[] stings;
        readonly List<(AudioSource src, float vol)> ambients = new List<(AudioSource, float)>();
        Transform chargeFollow;
        float chargeTarget;
        float nextAudioHealthCheck;

        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

        void Begin()
        {
            // 32 vozes reais no projeto. Reservamos 16 para efeitos; música, ambientes,
            // vinhetas e UI ficam com folga e não somem quando uma onda de inimigos nasce.
            for (int i = 0; i < 16; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.dopplerLevel = 0; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f; s.maxDistance = 45f; s.priority = 96;
                sources.Add(s);
            }
            chargeSrc = NewLoop(48); windSrc = NewLoop(72);
            droneSrc = NewLoop(32); drumsSrc = NewLoop(32); ostSrc = NewLoop(32);
            stingSrc = NewLoop(24); stingSrc.loop = false;
            menuMusicSrc = NewLoop(8);
            menuMusicSrc.clip = Resources.Load<AudioClip>("Audio/Music/BardOfBrokenBells");
            LoadSamples();
            if (samples.TryGetValue("amb_wind", out var wind0)) { windSrc.clip = wind0; windSrc.volume = ArenAudio.Effects * 0.3f; windSrc.Play(); }
            genTask = Task.Run(Generate);
        }

        void LoadSamples()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Samples")) samples[c.name] = c;
            void L(Sfx s, float gain, params string[] names) => AddLayer(s, gain, false, names);
            L(Sfx.Whoosh, 0.45f, "swing_swipe", "swing_whoosh", "swing_light");
            L(Sfx.ImpactLight, 0.75f, "hit_punch1", "hit_punch2", "hit_punch3", "hit_kick");
            L(Sfx.ImpactHeavy, 0.9f, "hit_thud", "hit_crunch");
            L(Sfx.Dodge, 0.55f, "whoosh_long");
            L(Sfx.CounterHit, 0.8f, "clash1", "clash2");
            L(Sfx.Hurt, 0.65f, "hit_punch2", "hit_punch3");
            L(Sfx.PulseBoom, 0.9f, "air_burst");
            L(Sfx.BladeCast, 0.45f, "unsheath");
            L(Sfx.BladeHit, 0.6f, "slice");
            L(Sfx.EchoCast, 0.35f, "ghost");
            L(Sfx.ContracantoRelease, 1f, "air_burst");
            L(Sfx.EnemyAttack, 0.35f, "swing_whoosh", "swing_swipe");
            L(Sfx.EnemyDeath, 0.6f, "body_fall");
            L(Sfx.EnemySpawn, 0.45f, "ghost");
            L(Sfx.DeerStep, 0.45f, "hit_thud");
            L(Sfx.DeerCharge, 0.5f, "whoosh_long");
            AddLayer(Sfx.Equip, 0.5f, true, "equip");
            AddLayer(Sfx.Unequip, 0.45f, true, "unequip");
            AddLayer(Sfx.BodyFall, 0.7f, true, "body_fall");
            AddLayer(Sfx.UIPage, 0.55f, true, "ui_page");
            string[] kinds = { "stone", "grass", "wood", "dirt" };
            steps = new AudioClip[kinds.Length][];
            for (int k = 0; k < kinds.Length; k++)
            {
                var list = new List<AudioClip>();
                for (int i = 1; i <= 4; i++) if (samples.TryGetValue("fs_" + kinds[k] + i, out var c)) list.Add(c);
                steps[k] = list.ToArray();
            }
            string[] st = { "sting_start", "sting_clear", "sting_defeat", "sting_mystery", "sting_boss", "sting_chime" };
            stings = new AudioClip[st.Length];
            for (int i = 0; i < st.Length; i++) samples.TryGetValue(st[i], out stings[i]);
        }

        void AddLayer(Sfx s, float gain, bool replace, params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names) if (samples.TryGetValue(n, out var c)) list.Add(c);
            if (list.Count > 0) layers[(int)s] = new Layer { clips = list.ToArray(), gain = gain, replace = replace };
        }

        AudioSource NewLoop(int priority)
        {
            var go = new GameObject("loop"); go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            s.priority = priority; s.ignoreListenerPause = true;
            return s;
        }

        void Generate()
        {
            // as faixas de música são as mais pesadas (~920 mil amostras com reverb cada):
            // geradas em paralelo com o banco de efeitos (o notebook tem 4 núcleos)
            var tWind = Task.Run(() => ArenSynth.WindLoop(9f, 261));
            var tDrone = Task.Run(() => ArenSynth.MusicDrone(8, 92, 271));
            var tDrums = Task.Run(() => ArenSynth.MusicDrums(8, 92, 281));
            var tOst = Task.Run(() => ArenSynth.MusicOstinato(8, 92, 291));
            var b = new Bank();
            void Add(Sfx s, params float[][] v) => b.sfx[(int)s] = v;
            uint seed = 1;
            b.fluteNotes = new float[ArenSynth.AttackScale.Length][];
            b.ukuleleNotes = new float[ArenSynth.AttackScale.Length][];
            for (int i = 0; i < b.fluteNotes.Length; i++)
            {
                float duration = i == 2 ? 0.19f : (i == 0 || i == 1 ? 0.16f : 0.14f);
                b.fluteNotes[i] = ArenSynth.AttackFluteNote(ArenSynth.AttackScale[i], duration, seed++);
                b.ukuleleNotes[i] = ArenSynth.AttackUkuleleNote(ArenSynth.AttackScale[i], duration, seed++);
            }

            Add(Sfx.Whoosh, ArenSynth.Whoosh(0.2f, 500, 2600, 11), ArenSynth.Whoosh(0.22f, 700, 3000, 12), ArenSynth.Whoosh(0.18f, 400, 2200, 13));
            Add(Sfx.ImpactLight, ArenSynth.Impact(0.3f, 190, 60, 0.6f, 0.35f, 1180, 21), ArenSynth.Impact(0.3f, 210, 65, 0.6f, 0.35f, 1320, 22));
            Add(Sfx.ImpactHeavy, ArenSynth.Impact(0.6f, 150, 40, 0.9f, 0.7f, 660, 23));
            Add(Sfx.Dodge, ArenSynth.Whoosh(0.32f, 300, 1800, 31, 0.8f));
            {
                var shimmer = ArenSynth.Chord(new[] { 1760f, 2637f, 3520f }, 0.7f, 0.2f, 0.25f, 0.004f);
                System.Array.Reverse(shimmer);
                ArenSynth.Mix(shimmer, ArenSynth.Bell(1760f, 0.8f, 0.3f, 32), 0.6f, (int)(0.45f * ArenSynth.SR));
                ArenSynth.Normalize(shimmer, 0.7f);
                Add(Sfx.PerfectDodge, shimmer);
            }
            {
                var clang = ArenSynth.Impact(0.7f, 260, 70, 1f, 1f, 880, 41);
                ArenSynth.Mix(clang, b.fluteNotes[7], 0.6f, 600);
                ArenSynth.Normalize(clang, 0.9f);
                Add(Sfx.CounterHit, clang);
            }
            Add(Sfx.ParryWhiff, ArenSynth.FluteNote(ArenSynth.Scale[2], 0.3f, 51, 0.2f));
            {
                var h = ArenSynth.Impact(0.4f, 120, 40, 0.5f, 0f, 100, 61);
                ArenSynth.Mix(h, ArenSynth.Chord(new[] { 92.5f, 98f, 138.6f }, 0.4f, 0.01f, 0.12f, 0.01f, true), 0.6f);
                ArenSynth.Normalize(h, 0.85f);
                Add(Sfx.Hurt, h);
            }
            Add(Sfx.Denied, ArenSynth.Chord(new[] { 110f, 116.5f }, 0.15f, 0.005f, 0.06f, 0.01f, true));
            {
                var inhale = ArenSynth.Whoosh(0.35f, 200, 900, 71, 0.7f);
                ArenSynth.Mix(inhale, ArenSynth.FluteNote(ArenSynth.Scale[0] * 0.5f, 0.35f, 72, 0.15f), 0.5f);
                ArenSynth.Normalize(inhale, 0.7f);
                Add(Sfx.PulseCast, inhale);
            }
            {
                var boom = ArenSynth.Boom(1.0f, 90, 32, 81, 0.5f);
                ArenSynth.Mix(boom, ArenSynth.Chord(new[] { 110f, 164.8f, 220f, 329.6f }, 1.0f, 0.004f, 0.35f, 0.004f), 0.9f);
                var o = ArenSynth.Reverb(boom, 0.8f, 0.35f, 0.4f, 1.2f);
                ArenSynth.Normalize(o, 0.95f);
                Add(Sfx.PulseBoom, o);
            }
            {
                var w = ArenSynth.Whoosh(0.3f, 900, 2600, 91, 2.5f);
                ArenSynth.Mix(w, ArenSynth.FluteNote(ArenSynth.Scale[6], 0.3f, 92, 0.25f), 0.7f);
                ArenSynth.Normalize(w, 0.8f);
                Add(Sfx.BladeCast, w);
            }
            Add(Sfx.BladeHit, ArenSynth.Impact(0.35f, 240, 90, 0.7f, 0.6f, 1760, 101));
            {
                var e = ArenSynth.Chord(new[] { 440f, 523.25f, 659.25f, 880f }, 0.9f, 0.5f, 0.2f, 0.008f);
                System.Array.Reverse(e);
                var o = ArenSynth.Reverb(e, 0.6f, 0.5f, 0.3f, 1.3f);
                ArenSynth.Normalize(o, 0.8f);
                Add(Sfx.EchoCast, o);
            }
            {
                var g = ArenSynth.Bell(1318.5f, 0.5f, 0.5f, 111);
                ArenSynth.LowPass(g, 3500);
                ArenSynth.Normalize(g, 0.6f);
                Add(Sfx.EchoGhost, g);
            }
            Add(Sfx.ChargeLevel, ArenSynth.Concat(ArenSynth.Pluck(880f, 0.08f, 0.05f), ArenSynth.Bell(1318.5f, 0.6f, 0.4f, 121)));
            {
                var rel = ArenSynth.Boom(1.6f, 80, 28, 131, 0.7f);
                ArenSynth.Mix(rel, ArenSynth.Chord(new[] { 440f, 554.37f, 659.25f, 880f, 1108.7f }, 1.6f, 0.003f, 0.6f, 0.005f), 0.9f);
                ArenSynth.Mix(rel, ArenSynth.Whoosh(0.5f, 300, 4000, 132, 0.6f), 0.6f);
                var o = ArenSynth.Reverb(rel, 1.4f, 0.45f, 0.35f, 1.4f);
                ArenSynth.Normalize(o, 0.98f);
                Add(Sfx.ContracantoRelease, o);
            }
            Add(Sfx.EnemyTelegraph, ArenSynth.Telegraph(0.55f, 141));
            Add(Sfx.EnemyAttack, ArenSynth.Whoosh(0.25f, 200, 900, 151, 1f));
            Add(Sfx.EnemyHurt, ArenSynth.Glitch(0.22f, 90, 161), ArenSynth.Glitch(0.25f, 75, 162));
            {
                var d = ArenSynth.Glitch(0.9f, 60, 171, 0.4f);
                ArenSynth.Mix(d, ArenSynth.Chord(new[] { 146.8f, 155.6f, 207.6f }, 0.9f, 0.01f, 0.3f, 0.02f, true), 0.5f);
                var o = ArenSynth.Reverb(d, 0.8f, 0.4f, 0.4f, 1.1f);
                ArenSynth.Normalize(o, 0.85f);
                Add(Sfx.EnemyDeath, o);
            }
            {
                var sp = ArenSynth.Whoosh(0.7f, 1500, 150, 181, 0.6f);
                System.Array.Reverse(sp);
                ArenSynth.Normalize(sp, 0.6f);
                Add(Sfx.EnemySpawn, sp);
            }
            {
                var gr = ArenSynth.Glitch(1.0f, 48, 191, 0.9f);
                ArenSynth.BandPassSweep(gr, t => 260f + 120f * Mathf.Sin(t * 9f), 0.7f);
                ArenSynth.Normalize(gr, 0.85f);
                Add(Sfx.DeerGrowl, gr);
            }
            {
                var ch = ArenSynth.Glitch(0.7f, 70, 201, 0.8f);
                ArenSynth.Mix(ch, ArenSynth.Telegraph(0.7f, 202), 0.5f);
                ArenSynth.Normalize(ch, 0.9f);
                Add(Sfx.DeerCharge, ch);
            }
            Add(Sfx.DeerStep, ArenSynth.Impact(0.2f, 110, 50, 0.3f, 0f, 100, 211));
            Add(Sfx.Footstep, ArenSynth.Impact(0.12f, 140, 70, 0.4f, 0f, 100, 212), ArenSynth.Impact(0.12f, 160, 75, 0.4f, 0f, 100, 213));
            Add(Sfx.UIMove, ArenSynth.Pluck(1318.5f, 0.12f, 0.05f));
            Add(Sfx.UIConfirm, ArenSynth.Concat(ArenSynth.Pluck(880f, 0.07f, 0.04f), ArenSynth.Pluck(1318.5f, 0.25f, 0.1f)));
            Add(Sfx.UIBack, ArenSynth.Concat(ArenSynth.Pluck(1318.5f, 0.07f, 0.04f), ArenSynth.Pluck(880f, 0.2f, 0.08f)));
            Add(Sfx.AbilityReady, ArenSynth.Bell(2637f, 0.45f, 0.3f, 221));
            Add(Sfx.Bell, ArenSynth.Bell(392f, 4.5f, 0.35f, 231), ArenSynth.Bell(523.25f, 4f, 0.35f, 232), ArenSynth.Bell(659.25f, 3.5f, 0.35f, 233));
            Add(Sfx.BellCorrupt, ArenSynth.Bell(370f, 5f, 0.5f, 241, true));
            Add(Sfx.Checkpoint, ArenSynth.Concat(ArenSynth.FluteNote(ArenSynth.Scale[0], 0.25f, 251), ArenSynth.FluteNote(ArenSynth.Scale[4], 0.6f, 252)));

            // tom do Contracanto: loop com quinta e oitava (o pitch sobe em tempo real)
            {
                var c = ArenSynth.Chord(new[] { 220f, 330f, 440f, 660f }, 2.6f, 0.01f, 999f, 0.004f, true);
                ArenSynth.LowPass(c, 2500);
                b.charge = ArenSynth.LoopCrossfade(c, (int)(2f * ArenSynth.SR), (int)(0.5f * ArenSynth.SR));
                ArenSynth.Normalize(b.charge, 0.5f);
            }
            b.wind = tWind.Result;
            b.drone = tDrone.Result;
            b.drums = tDrums.Result;
            b.ostinato = tOst.Result;
            bank = b;
        }

        AudioClip Make(string name, float[] data, bool loop = false)
        {
            // Um único NaN/Inf numa fonte envenena a mistura inteira (silêncio total e permanente).
            int bad = 0;
            for (int i = 0; i < data.Length; i++)
                if (float.IsNaN(data[i]) || float.IsInfinity(data[i])) { data[i] = 0f; bad++; }
            if (bad > 0) Debug.LogWarning("[ArenAudio] som sintetizado '" + name + "' tinha " + bad + " amostras NaN/Inf (zeradas)");
            var c = AudioClip.Create(name, data.Length, 1, ArenSynth.SR, false);
            c.SetData(data, 0);
            return c;
        }

        void Update()
        {
            if (!ready && genTask != null && genTask.IsCompleted)
            {
                if (genTask.IsFaulted) { Debug.LogException(genTask.Exception); genTask = null; return; }
                foreach (var kv in bank.sfx)
                {
                    var arr = new AudioClip[kv.Value.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = Make(((Sfx)kv.Key) + "_" + i, kv.Value[i]);
                    clips[kv.Key] = arr;
                }
                fluteNoteClips = new AudioClip[bank.fluteNotes.Length];
                ukuleleNoteClips = new AudioClip[bank.ukuleleNotes.Length];
                for (int i = 0; i < fluteNoteClips.Length; i++)
                {
                    fluteNoteClips[i] = Make("flute_attack_" + i, bank.fluteNotes[i]);
                    ukuleleNoteClips[i] = Make("ukulele_attack_" + i, bank.ukuleleNotes[i]);
                }
                chargeSrc.clip = Make("charge", bank.charge);
                // vento gravado (laço com emenda cruzada no preparo); o sintetizado fica de reserva
                if (windSrc.clip == null) windSrc.clip = Make("wind", bank.wind);
                droneSrc.clip = Make("music_drone", bank.drone);
                drumsSrc.clip = Make("music_drums", bank.drums);
                ostSrc.clip = Make("music_ostinato", bank.ostinato);
                bank = null;
                ready = true;
                if (!windSrc.isPlaying) windSrc.Play();
                double start = AudioSettings.dspTime + 0.2;
                droneSrc.PlayScheduled(start); drumsSrc.PlayScheduled(start); ostSrc.PlayScheduled(start);
                ApplyVolumes();
            }
            UpdateMenuMusic();
            if (!ready) return;

            intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.unscaledDeltaTime * 0.35f);
            float m = musicOn && !menuMusicRequested ? ArenAudio.Music : 0f;
            droneSrc.volume = m * 0.55f;
            drumsSrc.volume = m * 0.6f * Mathf.SmoothStep(0f, 1f, intensity * 2f);
            ostSrc.volume = m * 0.45f * Mathf.SmoothStep(0f, 1f, (intensity - 0.5f) * 2f);
            windSrc.volume = ArenAudio.Effects * 0.3f;
            ambients.RemoveAll(a => a.src == null);
            for (int i = 0; i < ambients.Count; i++) ambients[i].src.volume = ambients[i].vol * ArenAudio.Effects;
            if (Time.unscaledTime >= nextAudioHealthCheck)
            {
                nextAudioHealthCheck = Time.unscaledTime + 0.5f;
                EnsurePersistentAudio();
            }

            if (chargeFollow != null)
            {
                chargeSrc.volume = Mathf.MoveTowards(chargeSrc.volume, chargeTarget * ArenAudio.Effects, Time.unscaledDeltaTime * 3f);
            }
            else if (chargeSrc.isPlaying)
            {
                chargeSrc.volume = Mathf.MoveTowards(chargeSrc.volume, 0f, Time.unscaledDeltaTime * 6f);
                if (chargeSrc.volume <= 0f) chargeSrc.Stop();
            }
        }

        public void ApplyVolumes()
        {
            AudioListener.volume = ArenAudio.Master;
        }

        public void SetMenuMusic(bool on)
        {
            menuMusicRequested = on;
            if (on && menuMusicSrc != null && menuMusicSrc.clip != null && !menuMusicSrc.isPlaying)
            {
                menuMusicSrc.volume = 0f;
                menuMusicSrc.Play();
            }
        }

        void UpdateMenuMusic()
        {
            if (menuMusicSrc == null || menuMusicSrc.clip == null) return;
            float target = menuMusicRequested && musicOn ? ArenAudio.Music * 0.72f : 0f;
            float speed = target > menuMusicSrc.volume ? 0.3f : 1.5f;
            menuMusicSrc.volume = Mathf.MoveTowards(menuMusicSrc.volume, target, Time.unscaledDeltaTime * speed);
            if (!menuMusicRequested && menuMusicSrc.volume <= 0.001f && menuMusicSrc.isPlaying)
                menuMusicSrc.Stop();
        }

        AudioSource FreeSource()
        {
            AudioSource best = null;
            double earliestEnd = double.MaxValue;
            foreach (var s in sources)
            {
                if (!s.isPlaying) return s;
                double end = voiceEnds.TryGetValue(s, out var known) ? known : AudioSettings.dspTime + Mathf.Max(0f, s.clip.length - s.time);
                if (end < earliestEnd) { earliestEnd = end; best = s; }
            }
            return best ?? sources[0];
        }

        public void Play(Sfx s, Vector3 pos, float vol, float pitch, bool ui)
        {
            // limite de repetição por som (vários inimigos apanhando no mesmo frame)
            if (Time.unscaledTime - lastPlay[(int)s] < 0.03f) { vol *= 0.4f; }
            lastPlay[(int)s] = Time.unscaledTime;
            if (layers.TryGetValue((int)s, out var layer))
            {
                PlayClip(layer.clips[Random.Range(0, layer.clips.Length)], pos, vol * layer.gain, pitch * Random.Range(0.94f, 1.06f), ui);
                if (layer.replace) return;
            }
            if (!ready || !clips.TryGetValue((int)s, out var arr) || arr.Length == 0) return;
            PlayClip(arr[Random.Range(0, arr.Length)], pos, vol, pitch, ui);
        }

        void PlayClip(AudioClip clip, Vector3 pos, float vol, float pitch, bool ui, float spatial = 0.45f)
        {
            var src = FreeSource();
            src.transform.position = pos;
            src.clip = clip;
            src.pitch = pitch * (ui ? 1f : Mathf.Lerp(1f, Time.timeScale, 0.25f));   // câmera lenta "pesa" o som
            src.spatialBlend = ui ? 0f : spatial;
            src.volume = Mathf.Clamp01(vol * (ui ? ArenAudio.UI : ArenAudio.Effects));
            src.priority = ui ? 40 : 96;
            src.Play();
            voiceEnds[src] = AudioSettings.dspTime + clip.length / Mathf.Max(0.1f, Mathf.Abs(src.pitch));
        }

        public void Footstep(Vector3 pos, Surface surf, float vol)
        {
            var set = steps != null ? steps[(int)surf] : null;
            if (set == null || set.Length == 0) return;
            int k = (int)surf;
            int i = Random.Range(0, set.Length);
            if (set.Length > 1 && i == lastStep[k]) i = (i + 1) % set.Length;   // sem repetir o mesmo passo
            lastStep[k] = i;
            PlayClip(set[i], pos, vol, Random.Range(0.93f, 1.07f), false, 0.7f);
        }

        public void PlaySting(Sting s, float vol)
        {
            var c = stings != null ? stings[(int)s] : null;
            if (c == null) return;
            stingSrc.Stop();
            stingSrc.clip = c;
            stingSrc.pitch = 1f;
            stingSrc.volume = Mathf.Clamp01(vol * Mathf.Max(0.35f, ArenAudio.Music) * 0.9f);
            stingSrc.Play();
        }

        public void AmbientLoop(string sample, Vector3 pos, float minDist, float maxDist, float vol)
        {
            if (!samples.TryGetValue(sample, out var c)) return;
            // fica na cena (não no objeto persistente do áudio): recarregar a cena limpa os laços
            var go = new GameObject("amb_" + sample);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = c; a.loop = true; a.playOnAwake = false; a.dopplerLevel = 0f;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = minDist; a.maxDistance = maxDist;
            a.priority = 180;
            a.volume = vol * ArenAudio.Effects;
            a.time = Random.Range(0f, c.length * 0.9f);
            a.Play();
            ambients.Add((a, vol));
        }

        public void Note(int index, float vol, float brightness)
        {
            var noteClips = ArenAudio.Instrument == CombatInstrument.Ukulele ? ukuleleNoteClips : fluteNoteClips;
            if (!ready || noteClips == null) return;
            int i = Mathf.Clamp(index, 0, noteClips.Length - 1);
            var src = FreeSource();
            src.clip = noteClips[i];
            src.pitch = 1f;
            src.spatialBlend = 0f;
            src.volume = Mathf.Clamp01(vol * ArenAudio.Effects * 0.75f * Mathf.Lerp(0.85f, 1.1f, brightness - 0.5f));
            src.priority = 64;
            src.Play();
            voiceEnds[src] = AudioSettings.dspTime + noteClips[i].length;
        }

        public void BeginCharge(Transform t)
        {
            if (!ready) return;
            chargeFollow = t; chargeTarget = 0.25f;
            chargeSrc.pitch = 1f; chargeSrc.volume = 0f;
            if (!chargeSrc.isPlaying) chargeSrc.Play();
        }

        public void UpdateCharge(float p)
        {
            if (!ready) return;
            chargeSrc.pitch = Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(p));
            chargeTarget = Mathf.Lerp(0.2f, 0.55f, Mathf.Clamp01(p));
        }

        public void EndCharge() { chargeFollow = null; }

        void EnsurePersistentAudio()
        {
            if (windSrc.clip != null && !windSrc.isPlaying) windSrc.Play();
            bool musicStopped = (droneSrc.clip != null && !droneSrc.isPlaying)
                             || (drumsSrc.clip != null && !drumsSrc.isPlaying)
                             || (ostSrc.clip != null && !ostSrc.isPlaying);
            if (musicStopped)
            {
                droneSrc.Stop(); drumsSrc.Stop(); ostSrc.Stop();
                double start = AudioSettings.dspTime + 0.08;
                droneSrc.PlayScheduled(start); drumsSrc.PlayScheduled(start); ostSrc.PlayScheduled(start);
            }
            for (int i = 0; i < ambients.Count; i++)
                if (ambients[i].src != null && ambients[i].src.clip != null && !ambients[i].src.isPlaying)
                    ambients[i].src.Play();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!ready) return;
            nextAudioHealthCheck = 0f;
            EnsurePersistentAudio();
        }

        public string DebugStatus
        {
            get
            {
                int active = 0;
                for (int i = 0; i < sources.Count; i++) if (sources[i].isPlaying) active++;
                return string.Format("ready={0} efeitos={1}/{2} música={3}/{4}/{5} vento={6} ambientes={7}",
                    ready, active, sources.Count, droneSrc != null && droneSrc.isPlaying,
                    drumsSrc != null && drumsSrc.isPlaying, ostSrc != null && ostSrc.isPlaying,
                    windSrc != null && windSrc.isPlaying, ambients.Count);
            }
        }
    }
}

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
        Count
    }

    /// <summary>
    /// Fachada de áudio. Os sons são sintetizados (ArenSynth) numa thread de fundo quando
    /// o jogo abre; até ficarem prontos, Play() é ignorado. Volumes por categoria vêm das
    /// configurações (sem AudioMixer: menos uma dependência de asset de editor).
    /// </summary>
    public static class ArenAudio
    {
        public static float Master = 1f, Music = 0.65f, Effects = 1f, UI = 0.8f;
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
        public static void ApplyVolumes() => AudioRunner.Instance.ApplyVolumes();
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
        class Bank { public Dictionary<int, float[][]> sfx = new Dictionary<int, float[][]>(); public float[][] notes; public float[] charge, wind, drone, drums, ostinato; }
        Bank bank;
        Task genTask;

        readonly Dictionary<int, AudioClip[]> clips = new Dictionary<int, AudioClip[]>();
        AudioClip[] noteClips;
        readonly List<AudioSource> sources = new List<AudioSource>(24);
        readonly float[] lastPlay = new float[(int)Sfx.Count];
        AudioSource chargeSrc, windSrc, droneSrc, drumsSrc, ostSrc;
        Transform chargeFollow;
        float chargeTarget;

        void Begin()
        {
            for (int i = 0; i < 22; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.dopplerLevel = 0; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f; s.maxDistance = 45f;
                sources.Add(s);
            }
            chargeSrc = NewLoop(); windSrc = NewLoop(); droneSrc = NewLoop(); drumsSrc = NewLoop(); ostSrc = NewLoop();
            genTask = Task.Run(Generate);
        }

        AudioSource NewLoop()
        {
            var go = new GameObject("loop"); go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        void Generate()
        {
            var b = new Bank();
            void Add(Sfx s, params float[][] v) => b.sfx[(int)s] = v;
            uint seed = 1;
            b.notes = new float[ArenSynth.Scale.Length][];
            for (int i = 0; i < b.notes.Length; i++) b.notes[i] = ArenSynth.FluteNote(ArenSynth.Scale[i], 0.65f, seed++);

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
                ArenSynth.Mix(clang, b.notes[7], 0.6f, 600);
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
            b.wind = ArenSynth.WindLoop(9f, 261);
            b.drone = ArenSynth.MusicDrone(8, 92, 271);
            b.drums = ArenSynth.MusicDrums(8, 92, 281);
            b.ostinato = ArenSynth.MusicOstinato(8, 92, 291);
            bank = b;
        }

        AudioClip Make(string name, float[] data, bool loop = false)
        {
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
                noteClips = new AudioClip[bank.notes.Length];
                for (int i = 0; i < noteClips.Length; i++) noteClips[i] = Make("note_" + i, bank.notes[i]);
                chargeSrc.clip = Make("charge", bank.charge);
                windSrc.clip = Make("wind", bank.wind);
                droneSrc.clip = Make("music_drone", bank.drone);
                drumsSrc.clip = Make("music_drums", bank.drums);
                ostSrc.clip = Make("music_ostinato", bank.ostinato);
                bank = null;
                ready = true;
                windSrc.Play();
                double start = AudioSettings.dspTime + 0.2;
                droneSrc.PlayScheduled(start); drumsSrc.PlayScheduled(start); ostSrc.PlayScheduled(start);
                ApplyVolumes();
            }
            if (!ready) return;

            intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.unscaledDeltaTime * 0.35f);
            float m = musicOn ? ArenAudio.Music : 0f;
            droneSrc.volume = m * 0.55f;
            drumsSrc.volume = m * 0.6f * Mathf.SmoothStep(0f, 1f, intensity * 2f);
            ostSrc.volume = m * 0.45f * Mathf.SmoothStep(0f, 1f, (intensity - 0.5f) * 2f);
            windSrc.volume = ArenAudio.Effects * 0.3f;

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

        AudioSource FreeSource()
        {
            AudioSource best = null; float bestTime = -1;
            foreach (var s in sources)
            {
                if (!s.isPlaying) return s;
                float t = s.time;
                if (t > bestTime) { bestTime = t; best = s; }   // rouba a voz mais antiga
            }
            return best;
        }

        public void Play(Sfx s, Vector3 pos, float vol, float pitch, bool ui)
        {
            if (!ready || !clips.TryGetValue((int)s, out var arr) || arr.Length == 0) return;
            // limite de repetição por som (vários inimigos apanhando no mesmo frame)
            if (Time.unscaledTime - lastPlay[(int)s] < 0.03f) { vol *= 0.4f; }
            lastPlay[(int)s] = Time.unscaledTime;
            var src = FreeSource();
            src.transform.position = pos;
            src.clip = arr[Random.Range(0, arr.Length)];
            src.pitch = pitch * (ui ? 1f : Mathf.Lerp(1f, Time.timeScale, 0.25f));   // câmera lenta "pesa" o som
            src.spatialBlend = ui ? 0f : 0.45f;
            src.volume = Mathf.Clamp01(vol * (ui ? ArenAudio.UI : ArenAudio.Effects));
            src.Play();
        }

        public void Note(int index, float vol, float brightness)
        {
            if (!ready || noteClips == null) return;
            int i = Mathf.Clamp(index, 0, noteClips.Length - 1);
            var src = FreeSource();
            src.clip = noteClips[i];
            src.pitch = 1f;
            src.spatialBlend = 0f;
            src.volume = Mathf.Clamp01(vol * ArenAudio.Effects * 0.75f * Mathf.Lerp(0.85f, 1.1f, brightness - 0.5f));
            src.Play();
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
    }
}

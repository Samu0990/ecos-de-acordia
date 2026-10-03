using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aren.DebugTools;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Diagnóstico do executável (argumento -eda-diag): percorre estrada → mercado → praça →
    /// chefe com o robô atacando e grava, a cada 0.5 s, o nível REAL do áudio que sai
    /// (AudioListener.GetOutputData), as vozes tocando, o estado do mixer do jogo e se o
    /// jogador está visível quando deveria estar na tela. Resultado em ~/EcosBench/diag.txt.
    /// Existe para os bugs "som some quando os monstros aparecem" e "jogador sumindo".
    /// </summary>
    public class DemoDiag : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-diag") return true;
                return false;
            }
        }

        readonly StringBuilder log = new StringBuilder();
        readonly float[] buf = new float[1024];
        GameObject player;
        SkinnedMeshRenderer[] smrs;
        Camera cam;
        string phase = "";
        float phaseStart;
        int invisibleFrames, inViewFrames, totalInvisible;
        float rmsAcc; int rmsN;
        readonly List<string> events = new List<string>();
        int nanSamples; bool dumped;
        static int PlayingCount() { int n = 0; foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying) n++; return n; }

        IEnumerator Start()
        {
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(4f);
            var flow = GameFlow.Instance;
            player = FindAnyObjectByType<Climbing.ThirdPersonController>().gameObject;
            smrs = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hp = player.GetComponent<Combat.ArenHealth>();
            log.AppendLine("renderers do jogador: " + smrs.Length + " | config de áudio: " + AudioSettings.GetConfiguration().numRealVoices + " vozes reais, buffer " + AudioSettings.GetConfiguration().dspBufferSize);
            log.AppendLine("t | fase | saída dB | vozes tocando | " + "jogo | listener | inimigos | jogador pos | câmera dist | visível");

            yield return Phase("estrada", 5f, () => flow.DebugJump(1, new Vector3(0, 0, -88f), 0f), null, hp);
            yield return Phase("mercado", 14f, () => flow.DebugJump(2, new Vector3(0, 0, -33f), 0f), Attack(14f), hp);
            yield return Phase("praca", 22f, () => flow.DebugJump(4, new Vector3(0, 0, 2f), 0f), Attack(22f), hp);
            yield return Phase("chefe", 14f, () => flow.DebugJump(9, new Vector3(57f, 0, 10f), 90f), Attack(14f), hp);

            log.AppendLine();
            log.AppendLine("quadros com o jogador na tela mas invisível: " + totalInvisible);
            foreach (var e in events) log.AppendLine("  " + e);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "diag.txt"), log.ToString());
            Debug.Log("DIAG fim\n" + log);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        /// <summary>Linha do tempo do robô: anda e ataca (W + clique a cada 0.45 s).</summary>
        static string Attack(float dur)
        {
            var sb = new StringBuilder("0:W");
            for (float t = 1f; t < dur - 0.5f; t += 0.45f)
                sb.Append(";" + t.ToString("F2", CultureInfo.InvariantCulture) + ":LMB;" + (t + 0.12f).ToString("F2", CultureInfo.InvariantCulture) + (t > 4f && t < 5f ? ":W" : ":"));
            return sb.ToString();
        }

        IEnumerator Phase(string name, float seconds, System.Action enter, string timeline, Combat.ArenHealth hp)
        {
            enter();
            phase = name; phaseStart = Time.unscaledTime;
            yield return null;
            ArenTestProbe probe = timeline != null ? ArenTestProbe.Run(player, timeline, seconds) : null;
            float nextLine = 0f;
            while (Time.unscaledTime - phaseStart < seconds)
            {
                yield return new WaitForEndOfFrame();
                if (hp != null && hp.Health < hp.maxHealth * 0.5f) hp.Heal(100f);
                Sample();
                float t = Time.unscaledTime - phaseStart;
                if (t >= nextLine) { nextLine = t + 0.5f; Line(t); }
            }
            if (probe != null && !probe.Done) { Destroy(probe); ArenTestProbe.EndIsolatedInput(); }
        }

        void Sample()
        {
            // nível real da saída (mistura final): média dos dois canais
            float sum = 0f; int nan = 0;
            AudioListener.GetOutputData(buf, 0);
            for (int i = 0; i < buf.Length; i++) { if (float.IsNaN(buf[i]) || float.IsInfinity(buf[i])) nan++; else sum += buf[i] * buf[i]; }
            AudioListener.GetOutputData(buf, 1);
            for (int i = 0; i < buf.Length; i++) { if (float.IsNaN(buf[i]) || float.IsInfinity(buf[i])) nan++; else sum += buf[i] * buf[i]; }
            rmsAcc += Mathf.Sqrt(sum / (buf.Length * 2)); rmsN++;
            nanSamples += nan;
            if (!dumped && Time.unscaledTime - phaseStart > 0.3f && (nan > 0 || sum < 1e-12f) && ArenAudio.Ready && PlayingCount() > 3)
            {
                dumped = true;
                events.Add("SILÊNCIO/NaN em [" + phase + "] t=" + (Time.unscaledTime - phaseStart).ToString("F2", CultureInfo.InvariantCulture) + " amostras NaN=" + nan + " — fontes tocando:");
                foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (a.isPlaying)
                        events.Add(string.Format(CultureInfo.InvariantCulture, "    {0} clip={1} vol={2} pitch={3} pos={4} blend={5} t={6:F2}",
                            a.gameObject.name, a.clip != null ? a.clip.name : "-", a.volume, a.pitch, a.transform.position, a.spatialBlend, a.time));
            }

            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null || player == null) return;
            Vector3 vp = cam.WorldToViewportPoint(player.transform.position + Vector3.up * 1f);
            bool inView = vp.z > 0.3f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
            bool visible = false;
            foreach (var r in smrs) if (r != null && r.isVisible) { visible = true; break; }
            if (inView)
            {
                inViewFrames++;
                if (!visible)
                {
                    invisibleFrames++; totalInvisible++;
                    if (events.Count < 25)
                    {
                        var r0 = smrs.Length > 0 ? smrs[0] : null;
                        events.Add(string.Format(CultureInfo.InvariantCulture,
                            "[{0}] t={1:F2} jogador={2} câmera={3} dist={4:F2} renderer.enabled={5} ativo={6} forceOff={7} bounds={8}",
                            phase, Time.unscaledTime - phaseStart, player.transform.position, cam.transform.position,
                            Vector3.Distance(cam.transform.position, player.transform.position + Vector3.up),
                            r0 != null && r0.enabled, r0 != null && r0.gameObject.activeInHierarchy, r0 != null && r0.forceRenderingOff,
                            r0 != null ? r0.bounds.ToString() : "-"));
                    }
                }
            }
        }

        void Line(float t)
        {
            float rms = rmsN > 0 ? rmsAcc / rmsN : 0f; rmsAcc = 0f; rmsN = 0;
            float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-6f));
            int voices = 0;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (s.isPlaying) voices++;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, player.transform.position + Vector3.up) : -1f;
            log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,5:F1} | {1,-8} | {2,6:F1} nan={12} | {3,2} | {4} | vol={5:F2} pause={6} | {7} | {8} | {9:F2} | invisível {10}/{11}",
                t, phase, db, voices, ArenAudio.DebugStatus, AudioListener.volume, AudioListener.pause,
                Combat.CombatRegistry.AliveEnemyCount(), player.transform.position, dist, invisibleFrames, inViewFrames, nanSamples));
            nanSamples = 0;
            invisibleFrames = 0; inViewFrames = 0;
        }
    }
}

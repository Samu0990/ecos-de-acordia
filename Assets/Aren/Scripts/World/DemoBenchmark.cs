using System.Collections;
using System.Text;
using Aren.Combat;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Benchmark do executável (argumento -eda-benchmark): passa pelos trechos pesados da
    /// demo, mede FPS médio e 1% baixo por trecho, tira uma captura de cada um e fecha.
    /// Resultados no Player.log (linhas "BENCH") e capturas em ~/EcosBench/.
    /// O jogador fica invulnerável durante o teste (é medição, não gameplay).
    /// </summary>
    public class DemoBenchmark : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-benchmark") return true;
                return false;
            }
        }

        readonly StringBuilder report = new StringBuilder();
        string dir;

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(4f);   // áudio gerado, cena estável
            var flow = GameFlow.Instance;
            var player = FindAnyObjectByType<Climbing.ThirdPersonController>().gameObject;
            var hp = player.GetComponent<ArenHealth>();

            yield return Segment("menu", 6f, null);
            flow.DebugJump(1, new Vector3(0, 0, -88f), 0f);
            yield return Segment("estrada", 8f, () => Orbit(player.transform.position));
            flow.DebugJump(2, new Vector3(0, 0, -34f), 0f);
            yield return Segment("mercado_luta", 10f, () => Heal(hp));
            flow.DebugJump(4, new Vector3(0, 0, 2f), 0f);
            yield return Segment("praca_ondas", 16f, () => Heal(hp));
            flow.DebugJump(9, new Vector3(56f, 0, 10f), 90f);
            yield return Segment("campo_chefe", 14f, () => Heal(hp));
            Debug.Log("BENCH RESUMO\n" + report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "bench.txt"), report.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        void Heal(ArenHealth hp) { if (hp != null && hp.Health < hp.maxHealth * 0.6f) hp.Heal(100f); }

        void Orbit(Vector3 p) { }

        IEnumerator Segment(string name, float seconds, System.Action each)
        {
            // descarta o primeiro segundo (carregamento/teleporte)
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime; each?.Invoke(); yield return null; }
            var frames = new System.Collections.Generic.List<float>(2048);
            t = 0f;
            bool shot = false;
            while (t < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                frames.Add(dt);
                each?.Invoke();
                if (!shot && t > seconds * 0.5f)
                {
                    shot = true;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
                }
                yield return null;
            }
            frames.Sort();
            float sum = 0; foreach (var f in frames) sum += f;
            float avg = frames.Count / Mathf.Max(0.001f, sum);
            int idx = Mathf.Clamp(Mathf.FloorToInt(frames.Count * 0.99f), 0, frames.Count - 1);
            float low = 1f / Mathf.Max(0.0001f, frames[idx]);
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "BENCH {0,-14} avg={1,5:F1} fps  1%low={2,5:F1} fps  frames={3}  inimigos={4}  res={5}x{6}",
                name, avg, low, frames.Count, CombatRegistry.AliveEnemyCount(), Screen.width, Screen.height);
            Debug.Log(line);
            report.AppendLine(line);
        }
    }
}

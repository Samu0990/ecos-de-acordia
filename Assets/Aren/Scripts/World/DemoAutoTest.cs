using System.Collections;
using System.Text;
using Aren.DebugTools;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Testes automáticos dentro do executável (argumento -eda-test): o robô de input
    /// (teclado/mouse virtuais) percorre as rotas de parkour de Campanula e o log de cada
    /// uma vai para ~/EcosBench/tests.txt. Mesma lógica dos testes do editor, mas em
    /// velocidade real — o que é testado é exatamente o jogo que vai para o jogador.
    /// </summary>
    public class DemoAutoTest : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-test") return true;
                return false;
            }
        }

        static readonly string[] StateNames = { "Idle", "Walk", "Run_Stop", "Start Running", "Run", "Fall", "Fall Idle", "Predicted Jump", "Jumping Crouch", "Fall A Land To Run Forward", "Jump Down Slow", "Aren Jump", "Aren Land Heavy", "Aren Fall", "Vaulting", "Deep Jump", "Running Slide", "Reach", "Reach High", "Idle To Braced Hang", "Jump From Wall", "Hanging Movement", "Free Hang To Braced", "Braced Hang Hop Left", "Braced Hang Hop Right", "Braced Hang Hop Up", "Braced Hang Hop Down", "Braced Hang To Crouch", "Crouched To Standing", "Drop To Bracedhang", "Idle To Freehang", "Freehang Drop", "Hanging Movement", "Braced To FreeHang", "Freehang Climb", "Drop To Freehang", "Aren Atk1", "Aren Atk2", "Aren Atk3", "Aren Atk4", "Aren Counter", "Aren Dodge", "Aren Parry", "Aren Hurt", "Aren Death", "Aren Revive", "Aren Cast Pulse", "Aren Cast Blade", "Aren Cast Echo", "Aren Charge", "Aren Release" };

        struct T { public string name; public int step; public Vector3 pos; public float yaw; public string timeline; public float dur; }

        /// <summary>Anda até a parede, agarra e aperta W+Espaço n vezes (1.6 s entre saltos).</summary>
        static string Hops(int n, out float dur)
        {
            var sb = new StringBuilder("0:W;0.5:W+Space;0.65:W;");
            float t = 2.6f;
            for (int i = 0; i < n; i++) { sb.Append(t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ":W+Space;" + (t + 0.2f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ":W;"); t += 1.9f; }
            t += 2.5f;   // anda para frente depois de subir
            sb.Append(t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ":");
            dur = t + 0.5f;
            return sb.ToString();
        }

        IEnumerator Start()
        {
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            var outp = new StringBuilder();
            yield return new WaitForSecondsRealtime(4f);
            var flow = GameFlow.Instance;
            var player = FindAnyObjectByType<Climbing.ThirdPersonController>().gameObject;

            var tests = new[]
            {
                new T { name = "muro_baixo_vault", step = 1, pos = new Vector3(0, 0, -86), yaw = 0, timeline = "0:W+LeftShift;1.0:W+LeftShift+Space;1.15:W+LeftShift;3.0:", dur = 3.2f },
                new T { name = "fardos", step = 1, pos = new Vector3(0.8f, 0, -76), yaw = 0, timeline = "0:W+LeftShift;0.9:W+LeftShift+Space;1.05:W+LeftShift;3.0:", dur = 3.2f },
                new T { name = "viga_slide", step = 1, pos = new Vector3(0, 0, -68), yaw = 0, timeline = "0:W+LeftShift;0.9:W+LeftShift+C;1.2:W+LeftShift;3.2:", dur = 3.4f },
                new T { name = "muralha_escalada", step = 1, pos = new Vector3(-18.5f, 0, -45.6f), yaw = 0, timeline = Hops(7, out float dw), dur = dw },
                new T { name = "torre_pedras", step = 7, pos = new Vector3(-1.9f, 0, 30.3f), yaw = 0, timeline = Hops(17, out float dt), dur = dt },
            };
            foreach (var t in tests)
            {
                flow.DebugJump(t.step, t.pos, t.yaw);
                yield return new WaitForSeconds(1.2f);
                var probe = ArenTestProbe.Run(player, t.timeline, t.dur, StateNames);
                probe.extra = () =>
                {
                    var cc = player.GetComponent<Climbing.ClimbController>();
                    return "climb=" + (cc != null ? cc.CurrentClimbState.ToString() : "?");
                };
                while (!probe.Done) yield return null;
                outp.AppendLine("=== " + t.name);
                outp.AppendLine(probe.log.ToString());
                Debug.Log("AUTOTEST " + t.name + " fim em " + player.transform.position);
                yield return new WaitForSeconds(0.5f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "tests.txt"), outp.ToString());
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}

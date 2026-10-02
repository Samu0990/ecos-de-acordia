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
            var thirdPerson = FindAnyObjectByType<Climbing.ThirdPersonController>();
            var player = thirdPerson.gameObject;
            var freeLook = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            ArenThirdPersonSetup.Audit(thirdPerson, freeLook, out string thirdPersonLine);
            outp.AppendLine(thirdPersonLine);
            Debug.Log("AUTOTEST " + thirdPersonLine);

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
            // orientação no combate: o tronco de quem ataca aponta para o alvo?
            yield return FacingTest(flow, player, outp, dir);

            // encontros: os inimigos nascem quando o jogador chega?
            var encounterHealth = player.GetComponent<Combat.ArenHealth>();
            foreach (var (nome, step, pos) in new[] { ("mercado", 2, new Vector3(0, 0, -33f)), ("praca", 5, new Vector3(0, 0, 0f)), ("campo", 9, new Vector3(57f, 0, 10f)) })
            {
                flow.DebugJump(step, pos, 0f);
                float observeUntil = Time.time + (nome == "praca" ? 7f : 5f);
                while (Time.time < observeUntil)
                {
                    if (encounterHealth != null && encounterHealth.Health < encounterHealth.maxHealth * 0.65f) encounterHealth.Heal(100f);
                    yield return null;
                }
                int n = Combat.CombatRegistry.AliveEnemyCount();
                bool boss = false; int ranged = 0;
                foreach (var e in Combat.CombatRegistry.Enemies)
                {
                    if (e is Enemies.EnemyBase eb && eb.isBoss && eb.Alive) boss = true;
                    if (e is Enemies.EnemyEco eco && eco.Alive && eco.IsRangedVariant) ranged++;
                }
                string line = "ENCONTRO " + nome + ": inimigos vivos=" + n + " cantores=" + ranged
                    + " projéteis=" + Enemies.CorruptNoteProjectile.SpawnedTotal + (boss ? " (chefe presente)" : "");
                outp.AppendLine(line); Debug.Log("AUTOTEST " + line);
                foreach (var e in new System.Collections.Generic.List<Combat.IDamageable>(Combat.CombatRegistry.Enemies))
                    if (e is Enemies.EnemyBase eb2) Destroy(eb2.gameObject);
                yield return new WaitForSeconds(0.5f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "tests.txt"), outp.ToString());
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
            /// <summary>Direção do tronco no mundo (depois da trava), ou o frente do objeto sem trava.</summary>
        static Vector3 TorsoDir(Transform t)
        {
            var lk = t.GetComponent<Combat.TorsoFacingLock>();
            float yaw = lk != null && lk.enabled ? lk.TorsoYaw() : 0f;
            return Quaternion.Euler(0f, yaw, 0f) * t.forward;
        }

        class FaceStat
        {
            public int frames, back, side; public float sum, max;
            public void Add(float a) { frames++; sum += a; if (a > max) max = a; if (a > 90f) back++; else if (a > 50f) side++; }
            public override string ToString() => frames == 0 ? "sem amostras" : string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "quadros={0} média={1:F0}° máx={2:F0}° lado(50-90°)={3:P0} costas(>90°)={4:P0}", frames, sum / frames, max, (float)side / frames, (float)back / frames);
        }

        /// <summary>Telemetria do enquadramento real no executável durante golpes no mercado.</summary>
        class CameraStat
        {
            readonly Transform player;
            readonly RaycastHit[] hits = new RaycastHit[24];
            int frames, bothVisible, close, playerBlocked, targetBlocked;
            float sumDistance, minDistance = float.MaxValue;

            public CameraStat(Transform player) { this.player = player; }

            public void Add(Camera cam, Combat.IDamageable target)
            {
                if (cam == null || !Combat.CombatRegistry.IsValid(target)) return;
                frames++;
                Vector3 playerAim = player.position + Vector3.up * 1.15f;
                float distance = Vector3.Distance(cam.transform.position, playerAim);
                sumDistance += distance; minDistance = Mathf.Min(minDistance, distance);
                if (distance < 2f) close++;
                if (InView(cam, playerAim) && InView(cam, target.AimPoint)) bothVisible++;
                if (Blocked(cam.transform.position, playerAim, target.transform)) playerBlocked++;
                if (Blocked(cam.transform.position, target.AimPoint, target.transform)) targetBlocked++;
            }

            bool Blocked(Vector3 origin, Vector3 destination, Transform target)
            {
                Vector3 d = destination - origin;
                float len = d.magnitude;
                if (len < 0.05f) return false;
                int n = Physics.RaycastNonAlloc(origin, d / len, hits, len - 0.03f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c == null) continue;
                    var t = c.transform;
                    if (t.IsChildOf(player) || t.IsChildOf(target)) continue;
                    return true;
                }
                return false;
            }

            static bool InView(Camera cam, Vector3 point)
            {
                Vector3 p = cam.WorldToViewportPoint(point);
                return p.z > 0f && p.x > 0.12f && p.x < 0.88f && p.y > 0.10f && p.y < 0.90f;
            }

            public override string ToString() => frames == 0 ? "sem quadros de golpe" : string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "quadros={0} ambos_no_quadro={1:P0} camera<2m={2:P0} Aren_oculto={3:P0} alvo_oculto={4:P0} dist média/mín={5:F2}/{6:F2}m",
                frames, (float)bothVisible / frames, (float)close / frames, (float)playerBlocked / frames,
                (float)targetBlocked / frames, sumDistance / frames, minDistance);
        }

        /// <summary>
        /// Luta no mercado com o robô apertando o combo; mede, a cada quadro de golpe, o ângulo
        /// entre o tronco de quem ataca e o alvo (Aren → alvo do combo; Eco → Aren).
        /// Roda duas vezes: sem a trava de tronco e com ela.
        /// </summary>
        IEnumerator FacingTest(GameFlow flow, GameObject player, StringBuilder outp, string dir)
        {
            var combat = player.GetComponent<Combat.ArenCombat>();
            var hp = player.GetComponent<Combat.ArenHealth>();
            var anim = player.GetComponent<Animator>();
            foreach (bool locked in new[] { false, true })
            {
                flow.DebugJump(2, new Vector3(0, 0, -33f), 0f);
                yield return new WaitForSeconds(3.5f);
                foreach (var lk in FindObjectsByType<Combat.TorsoFacingLock>(FindObjectsSortMode.None)) lk.strength = locked ? 1f : 0f;
                var aren = new FaceStat(); var eco = new FaceStat();
                var camera = new CameraStat(player.transform);
                var sb = new StringBuilder("0:");
                for (float t = 5f; t < 12f; t += 0.42f)   // 5 s iniciais: os Ecos atacam
                    sb.Append(";" + t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":LMB;" + (t + 0.12f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":");
                var probe = ArenTestProbe.Run(player, sb.ToString(), 12.5f, StateNames);
                int shots = 0; float nextShot = Time.time + 2f;
                while (!probe.Done)
                {
                    yield return new WaitForEndOfFrame();
                    if (hp != null && hp.Health < hp.maxHealth * 0.7f) hp.Heal(100f);
                    var st = anim.GetCurrentAnimatorStateInfo(0);
                    if (st.IsTag("Combat") && combat.State == Combat.CombatState.Attack && Combat.CombatRegistry.IsValid(combat.Target))
                    {
                        Vector3 to = combat.Target.transform.position - player.transform.position; to.y = 0f;
                        if (to.sqrMagnitude > 0.04f) aren.Add(Vector3.Angle(TorsoDir(player.transform), to));
                        if (locked) camera.Add(Camera.main, combat.Target);
                    }
                    foreach (var e in Combat.CombatRegistry.Enemies)
                    {
                        if (!(e is Enemies.EnemyBase eb) || !eb.Alive) continue;
                        if (eb.State != Enemies.EnemyState.Telegraph && eb.State != Enemies.EnemyState.Attack) continue;
                        Vector3 to = player.transform.position - eb.transform.position; to.y = 0f;
                        if (to.sqrMagnitude > 0.04f) eco.Add(Vector3.Angle(TorsoDir(eb.transform), to));
                    }
                    if (locked && shots < 4 && Time.time > nextShot && combat.State == Combat.CombatState.Attack)
                    {
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "golpe_" + shots + ".png"));
                        shots++; nextShot = Time.time + 2.2f;
                    }
                }
                string line = "ORIENTACAO " + (locked ? "com trava" : "sem trava") + " | Aren→alvo: " + aren + " | Eco→Aren: " + eco;
                outp.AppendLine(line); Debug.Log("AUTOTEST " + line);
                if (locked)
                {
                    string cameraLine = "CAMERA combate mercado | " + camera;
                    outp.AppendLine(cameraLine); Debug.Log("AUTOTEST " + cameraLine);
                    string audioLine = "AUDIO após surgimento dos Ecos | " + ArenAudio.DebugStatus;
                    outp.AppendLine(audioLine); Debug.Log("AUTOTEST " + audioLine);
                    var buffs = player.GetComponent<ArenBuffOrbs>();
                    string vfxLine = "VFX combate | " + ArenVFX.DebugStatus
                        + " orbes_buff=" + (buffs != null ? buffs.ActiveCount : -1)
                        + " projéteis_ativos=" + Enemies.CorruptNoteProjectile.ActiveCount;
                    outp.AppendLine(vfxLine); Debug.Log("AUTOTEST " + vfxLine);
                }
                foreach (var e in new System.Collections.Generic.List<Combat.IDamageable>(Combat.CombatRegistry.Enemies))
                    if (e is Enemies.EnemyBase eb2) Destroy(eb2.gameObject);
                yield return new WaitForSeconds(0.5f);
            }
            foreach (var lk in FindObjectsByType<Combat.TorsoFacingLock>(FindObjectsSortMode.None)) lk.strength = 1f;
        }
    }
}

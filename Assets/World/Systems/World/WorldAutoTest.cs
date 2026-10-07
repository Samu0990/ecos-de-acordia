using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aren.Combat;
using Aren.DebugTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elyndra.World
{
    /// <summary>
    /// "-eda-world-test": teste do mundo dentro do executável. (1) Valtéria: o Aren ANDA a rota principal inteira
    /// com input virtual (W+Shift, câmera apontando para o próximo ponto) — da chegada de Campânula até a borda
    /// da Cratera; (2) portões: trancado não deixa passar; com Dó reafinado, Valtéria → Velária → Valtéria chega
    /// no portão certo; Valtéria → masmorra → placas de ritmo → atalho → saída; mapa do continente; Valtéria →
    /// Campânula → Valtéria; (3) cada um dos 13 reinos e das 13 masmorras carrega, tem jogador no chão, mede FPS
    /// e salva uma captura. Resultado em ~/EcosBench/world.txt (+ world_*.png).
    /// </summary>
    public class WorldAutoTest : MonoBehaviour
    {
        public static bool Requested { get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-world-test") return true; return false; } }
        static bool Quick { get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-world-quick") return true; return false; } }
        static bool DungeonsOnly { get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-world-dungeons") return true; return false; } }
        public static WorldAutoTest Instance { get; private set; }
        readonly StringBuilder outp = new StringBuilder();
        string dir;
        int fails;

        void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }

        void Log(string s) { outp.AppendLine(s); Debug.Log("WORLDTEST " + s); }
        void Fail(string s) { fails++; Log("FALHA: " + s); }

        IEnumerator WaitScene(string scene, float timeout = 90f)
        {
            float t = 0;
            while (t < timeout)
            {
                t += Time.unscaledDeltaTime;
                if (SceneManager.GetActiveScene().name == scene && !RegionTravel.Busy)
                {
                    if (scene == WorldCanon.CampanulaScene) { var gf = Aren.World.GameFlow.Instance; if (gf != null && gf.Loaded) yield break; }
                    else if (scene == WorldCanon.WorldMapScene) { if (FindAnyObjectByType<WorldMapController>() != null) yield break; }
                    else if (RegionFlow.Instance != null && RegionFlow.Instance.Ready) yield break;
                }
                yield return null;
            }
            Fail("cena não ficou pronta a tempo: " + scene + " (ativa: " + SceneManager.GetActiveScene().name + ")");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "world_" + name + ".png"));
            yield return null; yield return null;
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            // espera Campânula terminar de carregar (a tela de carregamento dela fica por cima até o fim)
            float w = 0; while ((Aren.World.GameFlow.Instance == null || !Aren.World.GameFlow.Instance.Loaded) && w < 60f) { w += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(1.5f);
            WorldState.ResetAll();
            WorldState.AdvancePhase(ValteriaPhase.BrilhoCaiu);
            EnemySpawnZone.Suppress = true; BossArena.Suppress = true;

            // só as 13 masmorras: mecânica, atalho, corredores a pé e a saída de volta ao reino
            if (DungeonsOnly)
            {
                foreach (var d in WorldCanon.Dungeons)
                {
                    string sc = WorldCanon.DungeonScene(d.id), back = WorldCanon.Region(d.region).scene;
                    RegionTravel.Go(sc, "entrada");
                    yield return WaitScene(sc);
                    yield return DungeonRun(d.name);
                    var exit = Gate("saida");
                    if (exit == null) { Fail(d.name + ": sem saída"); continue; }
                    yield return EnterGate(exit);
                    yield return WaitScene(back);
                    CheckArrival("masmorra", d.name + " → " + back);
                }
                yield return Finish();
                yield break;
            }

            // ---------------------------------------------------------- 1. travessia de Valtéria
            RegionTravel.Go("Valteria", "portao_campanula");
            yield return WaitScene("Valteria");
            Log($"Valtéria pronta: jogador em {Pos()} (chegou pelo portão de Campânula)");
            yield return Shot("valteria_chegada");
            yield return WalkRoute("Valtéria");
            yield return Shot("valteria_cratera");

            // inimigos de verdade: liga as zonas e confere que a mais próxima nasce
            EnemySpawnZone.Suppress = false;
            var flow = RegionFlow.Instance;
            var zones = FindObjectsByType<EnemySpawnZone>(FindObjectsSortMode.None);
            EnemySpawnZone first = null; foreach (var z in zones) if (z.id == "v_primeiros") first = z;
            if (first != null)
            {
                flow.Teleport(first.transform.position + Vector3.back * 12f, 0f);
                yield return new WaitForSeconds(3f);
                int n = 0; foreach (var e in CombatRegistry.Enemies) if (e is Aren.Enemies.EnemyBase eb && eb.Alive) n++;
                if (n > 0) Log($"zona 'Primeiros Corrompidos': {n} inimigo(s) nasceram ao chegar perto"); else Fail("zona de inimigos não gerou ninguém");
                yield return Shot("valteria_primeiros_corrompidos");
            }
            EnemySpawnZone.ResetAllNear(Vector3.zero, 1e6f);
            EnemySpawnZone.Suppress = true;

            // ---------------------------------------------------------- 2. portões
            var gToVel = Gate("rota_Velaria");
            if (gToVel == null) Fail("Valtéria sem portão para Velária");
            else
            {
                yield return EnterGate(gToVel);
                yield return new WaitForSecondsRealtime(2f);
                if (SceneManager.GetActiveScene().name == "Valteria") Log("portão trancado segurou o Aren (rota abre depois de Dó Partido)"); else Fail("portão trancado deixou passar");
                WorldState.ReafinarNote("do");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return EnterGate(gToVel);
                yield return WaitScene("Velaria");
                CheckArrival("rota_Valteria", "Velária");
                yield return Shot("velaria_chegada");
                yield return EnterGate(Gate("rota_Valteria"));
                yield return WaitScene("Valteria");
                CheckArrival("rota_Velaria", "Valtéria (volta)");
            }
            // masmorra de Valtéria
            var gD = Gate("masmorra");
            if (gD != null)
            {
                yield return EnterGate(gD);
                yield return WaitScene("D_valteria");
                CheckArrival("entrada", "Galerias do Aqueduto");
                yield return Shot("masmorra_valteria");
                yield return DungeonRun();
                var exit = Gate("saida");
                if (exit != null) { yield return EnterGate(exit); yield return WaitScene("Valteria"); CheckArrival("masmorra", "Valtéria (saída da masmorra)"); }
            }
            else Fail("Valtéria sem entrada de masmorra");
            // mapa do continente e volta
            RegionFlow.Instance.OpenWorldMap();
            yield return WaitScene(WorldCanon.WorldMapScene);
            yield return Shot("mapa_elyndra");
            Log("mapa de Elyndra carregou");
            RegionTravel.Go(RegionFlow.ResumeScene, "");
            yield return WaitScene("Valteria");
            Log($"voltou do mapa para onde estava: {Pos()}");
            // Campânula ida e volta
            var gC = Gate("portao_campanula");
            if (gC != null)
            {
                yield return EnterGate(gC);
                yield return WaitScene(WorldCanon.CampanulaScene);
                yield return new WaitForSecondsRealtime(3f);
                var gf = Aren.World.GameFlow.Instance;
                var p = gf != null ? FindAnyObjectByType<Climbing.ThirdPersonController>().transform.position : Vector3.zero;
                if (gf != null && gf.Current == Aren.World.GameFlow.State.Playing && Vector3.Distance(p, CampanulaLink.ArrivePos) < 12f) Log($"Campânula: chegou no Portão Leste jogando (sem menu) em {p}");
                else Fail($"Campânula: chegada errada (estado {(gf != null ? gf.Current.ToString() : "-")}, pos {p})");
                yield return Shot("campanula_portao_leste");
                WorldState.SetFlag("campanula_cervo");
                var cg = FindAnyObjectByType<CampanulaGate>();
                if (cg != null)
                {
                    var tpc = FindAnyObjectByType<Climbing.ThirdPersonController>();
                    ArenTestProbe.ResetPlayer(tpc.gameObject, cg.transform.position - cg.transform.forward * 0.5f + Vector3.down * 2.3f, 90f);
                    yield return WaitScene("Valteria");
                    CheckArrival("portao_campanula", "Valtéria (vindo de Campânula)");
                }
                else Fail("Campânula sem o Portão Leste");
            }

            // ---------------------------------------------------------- 3. todos os reinos e masmorras
            if (!Quick)
            {
                foreach (var r in WorldCanon.Regions)
                {
                    RegionTravel.Go(r.scene, "");
                    yield return WaitScene(r.scene);
                    yield return Survey(r.name, r.scene);
                }
                foreach (var d in WorldCanon.Dungeons)
                {
                    string sc = WorldCanon.DungeonScene(d.id);
                    RegionTravel.Go(sc, "entrada");
                    yield return WaitScene(sc);
                    yield return Survey(d.name, sc);
                }
            }
            yield return Finish();
        }

        IEnumerator Finish()
        {
            Log($"RESULTADO: {(fails == 0 ? "OK" : fails + " falha(s)")}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "world.txt"), outp.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        string Pos() => RegionFlow.Instance != null && RegionFlow.Instance.Player != null ? RegionFlow.Instance.Player.position.ToString("F0") : "-";

        RegionGate Gate(string id)
        {
            foreach (var g in FindObjectsByType<RegionGate>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (g.id == id) return g;
            return null;
        }

        IEnumerator EnterGate(RegionGate g)
        {
            if (g == null) { Fail("portão inexistente"); yield break; }
            var flow = RegionFlow.Instance;
            var tpc = flow.Player.GetComponent<Climbing.ThirdPersonController>();
            // começa no chão do lado de dentro, ~6 m antes da borda do gatilho (fora do arco), e anda até ele
            var bc = g.GetComponent<BoxCollider>();
            var fwd = g.transform.forward; fwd.y = 0; fwd.Normalize();
            var edge = g.transform.position - fwd * (bc != null ? bc.size.z * 0.5f : 1.5f);
            var from = g.arrival != null ? g.arrival.position : edge - fwd * 12f;
            var flat = edge - from; flat.y = 0;
            var p = from + flat.normalized * Mathf.Max(0f, flat.magnitude - 6f);
            p.y = from.y + 0.3f;
            flow.Teleport(p, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.3f);
            var probe = ArenTestProbe.Run(flow.Player.gameObject, "0:W+LeftShift;6:", 6.1f);
            var aim = g.transform.position + fwd * 4f;   // a câmera aponta para o portão (W anda para onde ela olha)
            float t = 0; while (t < 6.3f && !RegionTravel.Busy) { Steer(flow.Player.gameObject, aim); t += Time.deltaTime; yield return null; }
            if (probe != null) Destroy(probe);
            ArenTestProbe.EndIsolatedInput();
        }

        void CheckArrival(string gateId, string where)
        {
            var g = Gate(gateId);
            var p = RegionFlow.Instance != null && RegionFlow.Instance.Player != null ? RegionFlow.Instance.Player.position : Vector3.zero;
            if (g != null && g.arrival != null && Vector3.Distance(p, g.arrival.position) < 6f) Log($"{where}: chegou no portão certo ({gateId}) em {p:F0}");
            else Fail($"{where}: chegada longe do portão {gateId} (pos {p:F0}, esperado {(g != null && g.arrival != null ? g.arrival.position.ToString("F0") : "?")})");
        }

        static void Steer(GameObject player, Vector3 target)
        {
            var d = target - player.transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.01f) return;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var fl = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (fl != null && fl.m_BindingMode == Cinemachine.CinemachineTransposer.BindingMode.WorldSpace) fl.m_XAxis.Value = yaw;
            else
            {
                var rb = player.GetComponent<Rigidbody>();
                var rot = Quaternion.RotateTowards(player.transform.rotation, Quaternion.Euler(0, yaw, 0), 360f * Time.deltaTime);
                if (rb != null) rb.MoveRotation(rot); else player.transform.rotation = rot;
                if (fl != null) fl.m_XAxis.Value = 0f;
            }
        }

        IEnumerator WalkRoute(string label)
        {
            var route = FindAnyObjectByType<RegionRoute>();
            var flow = RegionFlow.Instance;
            if (route == null || route.points.Length < 2) { Fail(label + ": sem rota"); yield break; }
            var player = flow.Player.gameObject;
            flow.Teleport(route.points[0], Mathf.Atan2(route.points[1].x - route.points[0].x, route.points[1].z - route.points[0].z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.5f);
            int reached = 0; float walked = 0f; float t0 = Time.time;
            for (int i = 1; i < route.points.Length; i++)
            {
                var target = route.points[i];
                float dist = Vector3.Distance(player.transform.position, target);
                float maxT = dist / 3.2f + 12f;
                var probe = ArenTestProbe.Run(player, "0:W+LeftShift;" + maxT.ToString("0.0", CultureInfo.InvariantCulture) + ":", maxT + 0.5f);
                float tt = 0, still = 0; bool ok = false;
                var last = player.transform.position;
                while (tt < maxT)
                {
                    Steer(player, target);
                    var pos = player.transform.position;
                    var flat = target - pos; flat.y = 0;
                    if (flat.magnitude < 4.5f) { ok = true; break; }
                    walked += Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(last.x, 0, last.z));
                    still = (pos - last).sqrMagnitude < 0.0004f ? still + Time.deltaTime : 0f;
                    last = pos;
                    if (still > 4f) break;
                    if (pos.y < -100f) break;
                    tt += Time.deltaTime;
                    yield return null;
                }
                if (probe != null) Destroy(probe);
                ArenTestProbe.EndIsolatedInput();
                yield return null;
                if (ok) reached++;
                else
                {
                    Fail($"{label}: trecho {i} não foi alcançado a pé (parou em {player.transform.position:F0}, alvo {target:F0})");
                    flow.Teleport(target, 0f);
                    yield return new WaitForSeconds(0.4f);
                }
            }
            Log($"{label}: rota principal a pé {reached}/{route.points.Length - 1} trechos, {walked:0} m andados em {Time.time - t0:0} s");
        }

        IEnumerator DungeonRun(string label = "Masmorra")
        {
            if (!SceneManager.GetActiveScene().name.StartsWith("D_")) { Fail("masmorra não carregou (teste da masmorra pulado)"); yield break; }
            var flow = RegionFlow.Instance;
            var plates = FindAnyObjectByType<RhythmPlates>();
            if (plates != null)
            {
                foreach (var p in plates.plates) { flow.Teleport(p.position + Vector3.up * 0.2f, 0f); yield return new WaitForSeconds(0.45f); }
                yield return new WaitForSeconds(0.4f);
                bool open = plates.door == null || !plates.door.activeSelf;
                if (open) Log(label + ": placas de ritmo resolvidas na ordem → porta abriu"); else Fail(label + ": placas não abriram a porta");
            }
            else Fail(label + ": sem mecânica");
            var sd = FindAnyObjectByType<ShortcutDoor>();
            if (sd != null && sd.openFrom != null)
            {
                flow.Teleport(sd.openFrom.position, 0f);
                yield return new WaitForSeconds(0.6f);
                flow.Teleport(sd.portalA.position + Vector3.up * 0.1f, 0f);
                yield return new WaitForSeconds(0.8f);
                if (Vector3.Distance(flow.Player.position, sd.portalB.position) < 6f) Log(label + ": atalho aberto e a passagem de Eco leva de volta à entrada");
                else Fail(label + ": passagem de Eco não levou à entrada");
            }
            var route = FindAnyObjectByType<RegionRoute>();
            if (route != null && route.points.Length > 1) { yield return WalkRoute(label + " (corredores)"); }
        }

        IEnumerator Survey(string label, string scene)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            var flow = RegionFlow.Instance;
            bool grounded = flow != null && flow.Player != null && Physics.Raycast(flow.Player.position + Vector3.up, Vector3.down, 3f, ~((1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore);
            int frames = 0; float t = 0;
            while (t < 3f) { t += Time.unscaledDeltaTime; frames++; yield return null; }
            int gates = FindObjectsByType<RegionGate>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int zones = FindObjectsByType<EnemySpawnZone>(FindObjectsSortMode.None).Length;
            int arenas = FindObjectsByType<BossArena>(FindObjectsSortMode.None).Length;
            int pois = FindObjectsByType<PointOfInterest>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int ph = FindObjectsByType<PlaceholderTag>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int vort = FindObjectsByType<VortexZone>(FindObjectsSortMode.None).Length;
            Log($"{label} ({scene}): {frames / t:0} FPS · jogador no chão: {(grounded ? "sim" : "NÃO")} · portões {gates} · zonas {zones} · arenas {arenas} · vórtices {vort} · POIs {pois} · placeholders {ph}");
            if (!grounded) Fail(label + ": jogador sem chão embaixo");
            yield return Shot(scene.ToLowerInvariant());
        }
    }
}

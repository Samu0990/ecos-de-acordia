using Elyndra.World;
using UnityEditor;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>Peças de jogo do construtor: portões, pontos de retorno, zonas, arenas, Vórtices, POIs, áreas futuras, masmorras.</summary>
    public static class Gameplay
    {
        static Vector3 G(Vector2 p, float lift = 0f) => RegionBuilder.G(p, lift);
        static Vector3 Fwd(float yaw) { var d = RegionBuilder.Dir(yaw); return new Vector3(d.x, 0, d.y); }

        static Material StoneM => WorldMats.Stone("camp:ashlar", new Color(0.78f, 0.74f, 0.7f), 2.2f);
        static Material Veil => WorldMats.Crystal(new Color(0.25f, 0.08f, 0.35f, 0.35f), new Color(1.1f, 0.4f, 1.6f), new Color(0.4f, 0.1f, 0.6f), 0.55f);
        static Material Lantern => WorldMats.Glow(new Color(1.9f, 1.1f, 0.5f), 0.15f, 1.2f);

        public static string LockedText(string requires, string route)
        {
            if (string.IsNullOrEmpty(requires)) return "";
            if (requires.StartsWith("nota:do")) return route + ": a Ressonância da estrada responde errado desde a queda do brilho. Abre depois de Dó Partido.";
            if (requires.StartsWith("nota:si")) return route + ": a Fronteira Muda só se abre depois da última Nota (Si Infinito, em Nereth).";
            if (requires.StartsWith("flag:barco")) return route + ": é preciso um barco com rota cantada (Mar de Vidro).";
            if (requires.StartsWith("flag:")) return route + ": passagem escondida — algo ainda precisa ser descoberto.";
            return route + ": o caminho ainda está fechado.";
        }

        public static void Gate(GateSpec s, Transform parent)
        {
            var go = new GameObject("Portão — " + (string.IsNullOrEmpty(s.routeName) ? s.targetScene : s.routeName));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(s.pos), Quaternion.Euler(0, s.yaw, 0));
            var visual = new GameObject("Arco").transform; visual.SetParent(go.transform, false);
            if (!s.secret)
            {
                RegionBuilder.Object("Arco do caminho", ProcMesh.Arch(8f, 9f, 2.2f, 1.6f), StoneM, go.transform.position, go.transform.rotation, Vector3.one, visual);
                foreach (int sg in new[] { -1, 1 })
                {
                    var p = go.transform.position + go.transform.right * sg * 5.4f;
                    RegionBuilder.Object("Lanterna", ProcMesh.Sphere(8), Lantern, p + Vector3.up * 5.2f, Quaternion.identity, Vector3.one * 0.5f, visual, false);
                }
            }
            var locked = new GameObject("Trancado (véu + escombros)"); locked.transform.SetParent(go.transform, false);
            RegionBuilder.Object("Véu de Ressonância errada", ProcMesh.Box(8f, 9f, 0.15f), Veil, go.transform.position + Vector3.up * 0.1f, go.transform.rotation, Vector3.one, locked.transform, true);
            var trig = new GameObject("Gatilho"); trig.transform.SetParent(go.transform, false);
            trig.transform.localPosition = new Vector3(0, 2.5f, 2.5f);
            var bc = trig.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(9f, 6f, 3f);
            trig.layer = 2;
            var gate = trig.AddComponent<RegionGate>();
            gate.id = s.id; gate.targetScene = s.targetScene; gate.targetGate = s.targetGate; gate.routeName = s.routeName;
            gate.requires = s.requires; gate.lockedMessage = string.IsNullOrEmpty(s.lockedMessage) ? LockedText(s.requires, s.routeName) : s.lockedMessage;
            gate.lockedVisual = locked;
            var arr = new GameObject("Chegada").transform; arr.SetParent(go.transform, false);
            arr.position = G(s.pos - RegionBuilder.Dir(s.yaw) * 14f, 0.1f);
            arr.rotation = Quaternion.Euler(0, s.yaw + 180f, 0);
            gate.arrival = arr;
            // a estrada continua para fora (sinal de que o mundo segue além do limite)
            RegionBuilder.Tag(go, "Portão de rota provisório (arco procedural + véu) — trocar por posto de fronteira/portal no estilo do reino", "Construção");
        }

        public static void CheckpointAt(Vector2 pos, string id, Transform parent) => CheckpointAt(G(pos), id, parent);

        /// <summary>Ponto de retorno numa altura dada (masmorras: o chão da sala, não o terreno de reino nenhum).</summary>
        public static void CheckpointAt(Vector3 pos, string id, Transform parent)
        {
            var go = new GameObject("Ponto de retorno " + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            RegionBuilder.Object("Base", ProcMesh.Prism(8, 1.3f, 1.1f, 0.5f), StoneM, go.transform.position - Vector3.up * 0.1f, Quaternion.identity, Vector3.one, go.transform);
            RegionBuilder.Object("Haste", ProcMesh.Prism(4, 0.18f, 0.14f, 2.6f), WorldMats.Stone("camp:timber", new Color(0.55f, 0.45f, 0.38f), 1.5f), go.transform.position + Vector3.up * 0.4f, Quaternion.identity, Vector3.one, go.transform);
            RegionBuilder.Object("Sino pequeno", ProcMesh.Sphere(10, 0f, 0, true, false), WorldMats.Stone("camp:stone_dark", new Color(0.75f, 0.6f, 0.35f), 1f, 0.5f), go.transform.position + Vector3.up * 3.1f, Quaternion.Euler(180, 0, 0), new Vector3(0.45f, 0.6f, 0.45f), go.transform, false);
            var flame = RegionBuilder.Object("Chama (acesa ao ativar)", ProcMesh.Sphere(8), Lantern, go.transform.position + Vector3.up * 2.4f, Quaternion.identity, Vector3.one * 0.35f, go.transform, false);
            var lgo = new GameObject("Luz"); lgo.transform.SetParent(go.transform, false); lgo.transform.localPosition = Vector3.up * 2.4f;
            var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 1.6f; l.color = new Color(1f, 0.7f, 0.4f); l.renderMode = LightRenderMode.ForceVertex; l.shadows = LightShadows.None;
            var sc = go.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 3.2f; sc.center = Vector3.up;
            go.layer = 2;
            var cp = go.AddComponent<Checkpoint>(); cp.id = id; cp.glow = l; cp.flame = flame.GetComponent<Renderer>();
            var sp = new GameObject("Reaparecer").transform; sp.SetParent(go.transform, false); sp.localPosition = new Vector3(0, 0, -2.5f);
            cp.spawn = sp;
        }

        public static void Zone(ZoneSpec z, Transform parent)
        {
            var go = new GameObject("Zona de inimigos — " + z.label + " (nível " + z.tier + ")");
            go.transform.SetParent(parent, false);
            go.transform.position = G(z.pos);
            var ez = go.AddComponent<EnemySpawnZone>();
            ez.id = z.id; ez.label = z.label; ez.enemyIds = z.ids; ez.count = z.count; ez.tier = z.tier; ez.requires = z.requires; ez.radius = z.radius; ez.respawn = z.respawn;
        }

        public static void Arena(ArenaSpec a, Transform parent, Transform cams)
        {
            var note = WorldCanon.Note(a.noteId);
            string title = note != null ? "Arena — " + note.name + " (" + note.arena + ")" : "Arena de miniboss — " + a.miniboss;
            var go = new GameObject(title);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(a.pos), Quaternion.Euler(0, a.yaw, 0));
            var arena = go.AddComponent<BossArena>();
            arena.noteId = a.noteId; arena.minibossName = a.miniboss; arena.minibossEnemyId = a.enemyId; arena.radius = a.radius; arena.requires = a.requires;
            Transform Pt(string n, Vector2 local, float yaw)
            {
                var t = new GameObject(n).transform; t.SetParent(go.transform, false);
                var w = a.pos + Rot(local, a.yaw);
                t.position = G(w, 0.1f); t.rotation = Quaternion.Euler(0, a.yaw + yaw, 0);
                return t;
            }
            arena.entrance = Pt("Entrada", new Vector2(0, -a.radius), 0f);
            arena.exit = Pt("Saída", new Vector2(0, a.radius), 0f);
            arena.bossSpawn = Pt("Ponto do chefe", new Vector2(0, a.radius * 0.35f), 180f);
            arena.vfxAnchor = Pt("Âncora de VFX", Vector2.zero, 0f);
            var camList = new System.Collections.Generic.List<Transform>();
            foreach (var (n, local, h) in new[] { ("Câmera — entrada", new Vector2(-a.radius * 0.5f, -a.radius * 1.1f), 9f), ("Câmera — revelação", new Vector2(a.radius * 0.9f, -a.radius * 0.2f), 5f), ("Câmera — alto", new Vector2(0, -a.radius * 0.4f), a.radius * 0.9f) })
            {
                var cp = new GameObject(n).AddComponent<CinematicCameraPoint>();
                cp.transform.SetParent(go.transform, false);
                var w = a.pos + Rot(local, a.yaw);
                var at = arena.bossSpawn.position + Vector3.up * 4f;
                var from = RegionBuilder.FixCamera(G(w, h), at);
                cp.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from)); cp.lookAt = arena.bossSpawn; cp.label = n;
                camList.Add(cp.transform);
            }
            arena.cameraPoints = camList.ToArray();
            // anel de pedras e a névoa de luta (fecha a arena enquanto o chefe vive)
            // anel de penedos do kit (irregular, com falhas) em volta da arena — entrada e saída livres
            int n2 = Mathf.Clamp(Mathf.RoundToInt(a.radius / 3f), 10, 22);
            string[] ringRocks = { "Cliff_Rock_A", "Cliff_Rock_B", "Cliff_Rock_C", "Rock_B" };
            for (int i = 0; i < n2; i++)
            {
                float ang = i * 360f / n2 + (Mathf.PerlinNoise(i * 1.3f, a.radius) - 0.5f) * 10f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, 180f)) < 16f || Mathf.Abs(Mathf.DeltaAngle(ang, 0f)) < 16f) continue;
                if (Mathf.PerlinNoise(i * 0.9f, 7.3f) < 0.28f) continue;   // falhas no anel
                var w = a.pos + Rot(RegionBuilder.Dir(ang) * (a.radius + 2f), a.yaw);
                float sc = 1.1f + Mathf.PerlinNoise(i * 0.7f, a.radius) * 1.4f;
                if (RegionBuilder.NatureKit)
                {
                    string nm = i % 3 == 0 ? "Nature_Outcrop_A" : i % 3 == 1 ? "Nature_Boulder_B" : "Nature_Outcrop_C";
                    RegionBuilder.NatureRock(nm, w, ang + a.yaw + 90f, sc * 0.8f, 3.4f * sc, 0.6f, 0.2f, true, go.transform);
                    continue;
                }
                var rk = RegionBuilder.Kit(ringRocks[i % ringRocks.Length], new Vector3(w.x, -0.8f, w.y), ang + a.yaw + 90f, go.transform, sc);
                if (rk != null) rk.transform.rotation = Quaternion.Euler(Mathf.PerlinNoise(i, 3f) * 14f - 7f, rk.transform.eulerAngles.y, Mathf.PerlinNoise(i, 5f) * 14f - 7f);
            }
            var fog = new GameObject("Névoa de luta (fecha a arena)");
            fog.transform.SetParent(go.transform, false);
            var fogMesh = ProcMesh.Prism(28, a.radius + 3f, a.radius + 3f, 14f, false);
            RegionBuilder.Object("Parede de névoa", fogMesh, Veil, G(a.pos, -1f), Quaternion.identity, Vector3.one, fog.transform, true);
            fog.SetActive(false);
            arena.fightFog = fog;
            // corpo provisório do chefe
            if (note != null) arena.bossPlaceholder = NotePlaceholder(note, arena.bossSpawn, go.transform);
            RegionBuilder.Tag(go, note != null ? $"Arena pronta para {note.name}: falta o chefe (regra: {note.rule} · Contramotivo: {note.contramotivo})" : "Arena de miniboss pronta", "Chefe");
            // ponto de retorno antes da entrada
            CheckpointAt(a.pos + Rot(new Vector2(0, -a.radius - 10f), a.yaw), (note != null ? "arena_" + note.id : "mini_" + a.miniboss.GetHashCode().ToString("x")), parent);
        }

        static Vector2 Rot(Vector2 v, float yaw) { float r = -yaw * Mathf.Deg2Rad; return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r)); }

        /// <summary>Silhueta provisória de cada Nota (forma que lembra o Fundamento dela), até o chefe existir.</summary>
        static GameObject NotePlaceholder(NoteDef n, Transform at, Transform parent)
        {
            var root = new GameObject("PLACEHOLDER — corpo de " + n.name);
            root.transform.SetParent(parent, false);
            root.transform.position = at.position + Vector3.up * 5f;
            Color tint = n.id switch { "do" => new Color(0.9f, 0.6f, 0.3f), "re" => new Color(0.5f, 0.8f, 1f), "mi" => new Color(0.85f, 0.85f, 0.95f), "fa" => new Color(0.5f, 1f, 0.45f), "sol" => new Color(1.2f, 1f, 0.5f), "la" => new Color(1f, 0.4f, 0.8f), _ => new Color(0.6f, 0.6f, 0.8f) };
            var coreM = WorldMats.Glow(tint * 1.6f, 0.6f, 1.5f);
            var core = RegionBuilder.Object("Núcleo", ProcMesh.Sphere(12, 0.12f, 7), coreM, root.transform.position, Quaternion.identity, Vector3.one * 2.2f, root.transform, false);
            if (n.id == "do")
            {
                // Dó em Forma de Queda: pedra, madeira e metal de Valtéria girando, ainda sem aceitar uma forma
                var stone = WorldMats.Stone("camp:stone_wall", new Color(0.7f, 0.66f, 0.62f), 2f);
                var wood = WorldMats.Stone("camp:timber", new Color(0.6f, 0.5f, 0.4f), 1.5f);
                var iron = WorldMats.Stone("darkrock", new Color(0.35f, 0.35f, 0.4f), 1.5f, 0.5f);
                var r = new System.Random(5);
                for (int i = 0; i < 26; i++)
                {
                    float ang = i * 137.5f, rad = 3.5f + (float)r.NextDouble() * 6f, y = ((float)r.NextDouble() - 0.4f) * 9f;
                    var p = root.transform.position + new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * rad, y, Mathf.Cos(ang * Mathf.Deg2Rad) * rad);
                    int k = i % 3;
                    Mesh m = k == 0 ? ProcMesh.Sphere(6, 0.3f, i) : k == 1 ? ProcMesh.Box(0.5f, 0.5f, 3.5f) : ProcMesh.Prism(6, 0.25f, 0.25f, 2.5f);
                    float s = k == 0 ? 1f + (float)r.NextDouble() * 1.5f : 1f;
                    RegionBuilder.Object("Fragmento", m, k == 0 ? stone : k == 1 ? wood : iron, p, Quaternion.Euler(r.Next(0, 360), r.Next(0, 360), r.Next(0, 360)), Vector3.one * s, root.transform, false);
                }
                var fb = root.AddComponent<FormingBody>(); fb.core = core.transform; fb.spin = 7f;
            }
            else
            {
                var cm = WorldMats.Crystal(new Color(tint.r * 0.3f, tint.g * 0.3f, tint.b * 0.3f, 0.5f), tint * 1.4f, tint * 0.6f);
                for (int i = 0; i < 9; i++)
                {
                    float ang = i * 40f;
                    var p = root.transform.position + new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 3.5f, -3f, Mathf.Cos(ang * Mathf.Deg2Rad) * 3.5f);
                    RegionBuilder.Object("Faceta", ProcMesh.Crystal(i), cm, p, Quaternion.Euler(20f * Mathf.Sin(i), ang, 25f), new Vector3(3f, 9f + i % 3 * 2f, 3f), root.transform, false);
                }
                var fb = root.AddComponent<FormingBody>(); fb.core = core.transform; fb.spin = 4f; fb.bob = 0.3f;
            }
            RegionBuilder.Tag(root, $"Corpo provisório de {n.name} ({n.epithet}) — substituir pelo chefe", "Chefe");
            return root;
        }

        public static void Vortex(VortexSpec v, Transform parent)
        {
            var def = WorldCanon.Vortex(v.id);
            var go = new GameObject("Vórtice — " + (def != null ? def.name : v.id));
            go.transform.SetParent(parent, false);
            go.transform.position = G(v.pos);
            var vz = go.AddComponent<VortexZone>();
            vz.vortexId = v.id; vz.radius = v.radius; vz.activeWhen = v.activeWhen;
            Color tint = def == null ? new Color(0.5f, 0.3f, 0.65f) : def.distortion switch
            {
                Distortion.Loop => new Color(0.4f, 0.55f, 0.9f), Distortion.Inversao => new Color(0.7f, 0.45f, 0.9f), Distortion.Saturacao => new Color(0.8f, 0.5f, 0.3f),
                Distortion.Roubo => new Color(0.55f, 0.55f, 0.6f), Distortion.Estouro => new Color(1f, 0.45f, 0.25f), Distortion.Ausencia => new Color(0.35f, 0.35f, 0.4f), _ => new Color(0.75f, 0.3f, 0.8f)
            };
            vz.tint = tint;
            // partículas que mostram a distorção (Inversão cai para cima; Loop gira; Estouro pulsa...)
            var pgo = new GameObject("Partículas da distorção"); pgo.transform.SetParent(go.transform, false); pgo.transform.localPosition = Vector3.up * 2f;
            var ps = pgo.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = 6f; main.startSpeed = 0.4f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new Color(tint.r * 1.5f, tint.g * 1.5f, tint.b * 1.5f, 0.8f); main.maxParticles = 500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = def != null && def.distortion == Distortion.Inversao ? -0.08f : 0.01f;
            var em = ps.emission; em.rateOverTime = 45f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = v.radius * 0.8f; sh.rotation = new Vector3(90, 0, 0);
            if (def != null && def.distortion == Distortion.Loop)
            {
                // todas as curvas no mesmo modo (constante) — senão o Unity reclama a cada quadro
                var vel = ps.velocityOverLifetime; vel.enabled = true;
                vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(0.4f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            }
            pgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = WorldMats.Motes();
            vz.motes = ps;
            // o lugar transformado (só existe com o Vórtice): cada distorção muda o lugar do seu jeito, irradiando
            // do Núcleo (um hospedeiro VIVO) — o lugar em si não é hospedeiro
            var dressing = new GameObject("Lugar transformado pelo Vórtice"); dressing.transform.SetParent(go.transform, false);
            VortexDressing(def != null ? def.distortion : Distortion.Fenda, v, tint, dressing.transform);
            vz.transformedDressing = dressing;
            var core = new GameObject("Núcleo (hospedeiro vivo central)").transform; core.SetParent(go.transform, false); core.position = G(v.pos, 0.5f);
            vz.core = core;
            if (v.offbeatBell)
            {
                var bellHolder = RegionBuilder.Kit("Bell_Pavilion", new Vector3(v.pos.x, 0, v.pos.y), 0f, go.transform);
                var bell = bellHolder != null ? FindBell(bellHolder.transform) : null;
                var cb = go.AddComponent<ContramotivoOffbeatBell>();
                cb.vortex = vz; cb.bell = bell != null ? bell : bellHolder != null ? bellHolder.transform : go.transform;
            }
            RegionBuilder.Tag(go, def != null ? $"Vórtice {def.name}: Motivo «{def.motivo}» · Regra «{def.regra}» · Núcleo «{def.nucleo}» · Contramotivo «{def.contramotivo}»" : "Vórtice", "VFX");
        }

        static void VortexDressing(Distortion d, VortexSpec v, Color tint, Transform parent)
        {
            var r = new System.Random(v.id.GetHashCode());
            float R() => (float)r.NextDouble();
            float RR(float a, float b) => a + (b - a) * R();
            Vector2 Ring(float k0, float k1) { float ang = RR(0, 360), rad = v.radius * RR(k0, k1); return v.pos + RegionBuilder.Dir(ang) * rad; }
            var glow = WorldMats.Glow(new Color(tint.r * 1.8f, tint.g * 1.8f, tint.b * 1.8f), 0.35f, 1f);
            var crystal = WorldMats.Crystal(new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 0.6f), tint * 1.6f, tint * 0.8f, 0.7f);
            string[] rocks = { "Rock_A", "Rock_B", "Cliff_Rock_C" };
            switch (d)
            {
                case Distortion.Inversao:
                {
                    // o peso cai para cima: pedras e destroços suspensos, girando devagar acima do chão rachado
                    var up = new GameObject("Pedras que caem para cima").transform; up.SetParent(parent, false); up.position = G(v.pos, 0f);
                    for (int i = 0; i < 16; i++)
                    {
                        var p = Ring(0.15f, 0.85f);
                        var rk = RegionBuilder.Kit(rocks[i % rocks.Length], new Vector3(p.x, RR(3f, 16f), p.y), RR(0, 360), up, RR(0.4f, 1.3f), false);
                        if (rk != null) rk.transform.rotation = Quaternion.Euler(RR(0, 360), RR(0, 360), RR(0, 360));
                    }
                    var fb = up.gameObject.AddComponent<FormingBody>(); fb.spin = 0.25f; fb.bob = 1.2f;
                    for (int i = 0; i < 8; i++) { var p = Ring(0.1f, 0.7f); RegionBuilder.Object("Rachadura que sobe", ProcMesh.Box(0.35f, 0.1f, RR(6f, 14f)), glow, G(p, 0.05f), Quaternion.Euler(0, RR(0, 360), 0), Vector3.one, parent, false); }
                    break;
                }
                case Distortion.Loop:
                {
                    // o mesmo passo repetido: fileiras de cópias fantasmas do mesmo cristal, em espiral
                    for (int i = 0; i < 24; i++)
                    {
                        float ang = i * 28f, rad = v.radius * (0.15f + i * 0.03f);
                        var p = v.pos + RegionBuilder.Dir(ang) * rad;
                        RegionBuilder.Object("Eco repetido", ProcMesh.Crystal(3), crystal, G(p, -0.3f), Quaternion.Euler(0, ang, 12f), new Vector3(0.8f, 2.6f, 0.8f), parent, false);
                    }
                    break;
                }
                case Distortion.Saturacao:
                {
                    // excesso: cristais da cor do Vórtice incham em cachos em volta do Núcleo
                    for (int i = 0; i < 14; i++)
                    {
                        var p = Ring(0.1f, 0.8f); int n = r.Next(3, 7);
                        for (int k = 0; k < n; k++) { float h = RR(1.5f, 6f); RegionBuilder.Object("Inchaço de Ressonância", ProcMesh.Crystal(r.Next(0, 12)), crystal, G(p + new Vector2(RR(-2, 2), RR(-2, 2)), -0.4f), Quaternion.Euler(RR(-30, 30), RR(0, 360), RR(-30, 30)), new Vector3(h * 0.45f, h, h * 0.45f), parent, k == 0); }
                    }
                    break;
                }
                case Distortion.Estouro:
                {
                    // tudo guardado e devolvido: chão rachado em brasa e pedras estouradas para fora
                    for (int i = 0; i < 14; i++) { float ang = i * 360f / 14f + RR(-8, 8); var p = v.pos + RegionBuilder.Dir(ang) * RR(4f, v.radius * 0.6f); RegionBuilder.Object("Rachadura de estouro", ProcMesh.Box(0.5f, 0.1f, RR(8f, 18f)), glow, G(p, 0.05f), Quaternion.Euler(0, ang, 0), Vector3.one, parent, false); }
                    for (int i = 0; i < 10; i++) { var p = Ring(0.4f, 0.95f); RegionBuilder.Kit(rocks[i % rocks.Length], new Vector3(p.x, -0.3f, p.y), RR(0, 360), parent, RR(0.6f, 1.6f)); }
                    break;
                }
                case Distortion.Ausencia:
                {
                    // o que falta: estilhaços escuros que não refletem nada, cor e som sumindo
                    var voidM = WorldMats.Crystal(new Color(0.02f, 0.02f, 0.03f, 0.9f), new Color(0.3f, 0.3f, 0.35f), new Color(0.05f, 0.05f, 0.06f), 0.92f);
                    for (int i = 0; i < 18; i++) { var p = Ring(0.1f, 0.9f); float h = RR(2f, 9f); RegionBuilder.Object("Estilhaço de Ausência", ProcMesh.Crystal(r.Next(0, 12)), voidM, G(p, RR(-0.5f, 4f)), Quaternion.Euler(RR(-40, 40), RR(0, 360), RR(-40, 40)), new Vector3(h * 0.3f, h, h * 0.3f), parent, false); }
                    break;
                }
                case Distortion.Roubo:
                {
                    // o que foi tirado: molduras vazias e pedestais sem nada (o lugar perde o reconhecimento)
                    var wood = WorldMats.Stone("camp:timber", new Color(0.4f, 0.38f, 0.37f), 1.2f);
                    for (int i = 0; i < 12; i++) { var p = Ring(0.2f, 0.85f); float yaw = RR(0, 360); RegionBuilder.Object("Moldura vazia", ProcMesh.Arch(1.4f, 2.4f, 0.15f, 0.18f), wood, G(p, -0.1f), Quaternion.Euler(RR(-8, 8), yaw, RR(-12, 12)), Vector3.one, parent, false); }
                    break;
                }
                default:
                {
                    // Fenda: frestas de luz verticais e o mesmo marco duplicado em dois lugares
                    var slit = WorldMats.Glow(new Color(1.2f, 0.5f, 1.8f), 0.4f, 0.7f);
                    for (int i = 0; i < 10; i++) { var p = Ring(0.2f, 0.9f); float h = RR(4f, 12f); RegionBuilder.Object("Fresta da Fenda", ProcMesh.Box(0.25f, h, 0.05f), slit, G(p, RR(0.5f, 3f)), Quaternion.Euler(0, RR(0, 360), RR(-6, 6)), Vector3.one, parent, false); }
                    for (int i = 0; i < 6; i++) { var p = Ring(0.3f, 0.8f); float yaw = RR(0, 360); foreach (float sgn in new[] { -1f, 1f }) RegionBuilder.Object("Pedra partida em duas", ProcMesh.Crystal(i), crystal, G(p + RegionBuilder.Dir(yaw + 90f) * sgn * 1.4f, -0.3f), Quaternion.Euler(0, yaw, sgn * 10f), new Vector3(1.2f, 3.5f, 1.2f), parent, false); }
                    break;
                }
            }
        }

        static Transform FindBell(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>()) if (c.name.ToLower().Contains("bell") && c != t) return c;
            return null;
        }

        public static void Poi(PoiSpec p, Transform parent)
        {
            var go = new GameObject($"POI — {p.kind}: {p.title}");
            go.transform.SetParent(parent, false);
            go.transform.position = G(p.pos);
            var poi = go.AddComponent<PointOfInterest>();
            poi.kind = p.kind; poi.title = p.title; poi.description = p.desc; poi.canonId = p.canonId; poi.role = p.role;
            GameObject vis = null;
            switch (p.kind)
            {
                case PoiKind.Reliquia:
                    vis = new GameObject("Santuário da relíquia");
                    vis.transform.SetParent(go.transform, false);
                    RegionBuilder.Object("Pedestal", ProcMesh.Prism(8, 1.6f, 1.2f, 1.4f), WorldMats.Stone("camp:ashlar", new Color(0.85f, 0.8f, 0.72f), 1.5f), go.transform.position, Quaternion.identity, Vector3.one, vis.transform);
                    RegionBuilder.Object("Faceta de Vael (brilho)", ProcMesh.Crystal(3), WorldMats.Crystal(new Color(0.5f, 0.45f, 0.3f, 0.6f), new Color(2f, 1.7f, 1f), new Color(1f, 0.8f, 0.4f)), go.transform.position + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.9f, 1.6f, 0.9f), vis.transform, false);
                    foreach (int i in new[] { 0, 1, 2, 3 })
                        RegionBuilder.Object("Coluna", ProcMesh.Prism(6, 0.4f, 0.35f, 5f), WorldMats.Stone("camp:ashlar", new Color(0.8f, 0.76f, 0.7f), 1.5f), go.transform.position + Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward * 4f, Quaternion.identity, Vector3.one, vis.transform);
                    RegionBuilder.Tag(vis, "Santuário provisório da Relíquia de Vael — modelo final do artefato e do Custódio", "Prop");
                    break;
                case PoiKind.Artefato:
                case PoiKind.Recompensa:
                case PoiKind.Encantamento:
                    vis = new GameObject("Altar / baú");
                    vis.transform.SetParent(go.transform, false);
                    RegionBuilder.Object("Altar", ProcMesh.Box(1.2f, 0.9f, 0.8f), WorldMats.Stone("camp:stone_dark", new Color(0.7f, 0.65f, 0.6f), 1.2f), go.transform.position, Quaternion.identity, Vector3.one, vis.transform);
                    RegionBuilder.Object("Brilho", ProcMesh.Sphere(8), WorldMats.Glow(new Color(1.2f, 0.9f, 0.5f), 0.4f, 2f), go.transform.position + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.4f, vis.transform, false);
                    RegionBuilder.Tag(vis, "Altar provisório — trocar pelo modelo do artefato/baú", "Prop");
                    break;
                case PoiKind.Segredo:
                    vis = RegionBuilder.Object("Esconderijo", ProcMesh.Box(0.9f, 0.6f, 0.6f), WorldMats.Stone("camp:planks", new Color(0.5f, 0.4f, 0.32f), 1f), go.transform.position, Quaternion.Euler(0, 23, 0), Vector3.one, go.transform);
                    RegionBuilder.Object("Fresta de luz", ProcMesh.Sphere(6), WorldMats.Glow(new Color(0.6f, 0.4f, 1.1f), 0.3f, 3f), go.transform.position + Vector3.up * 0.7f, Quaternion.identity, Vector3.one * 0.18f, vis.transform, false);
                    break;
                case PoiKind.NPC:
                case PoiKind.Custodio:
                {
                    // morador/Custódio provisório: um aldeão de Campânula aparece aqui quando o Aren chega perto
                    var tf = go.AddComponent<TownsfolkSpot>();
                    tf.Pick(p.title + p.pos, string.IsNullOrEmpty(p.role) ? p.title : p.role);
                    go.transform.rotation = Quaternion.Euler(0, Mathf.Abs((p.title + p.pos).GetHashCode()) % 360, 0);
                    break;
                }
                case PoiKind.Missao:
                    vis = new GameObject("Quadro de avisos"); vis.transform.SetParent(go.transform, false);
                    RegionBuilder.Object("Poste", ProcMesh.Box(0.2f, 2.2f, 0.2f), WorldMats.Stone("camp:timber", new Color(0.55f, 0.45f, 0.38f), 1f), go.transform.position, Quaternion.identity, Vector3.one, vis.transform);
                    RegionBuilder.Object("Quadro", ProcMesh.Box(1.6f, 1.1f, 0.08f), WorldMats.Stone("camp:planks", new Color(0.7f, 0.6f, 0.48f), 0.8f), go.transform.position + Vector3.up * 1.2f, Quaternion.identity, Vector3.one, vis.transform);
                    break;
            }
            poi.visual = vis;
            poi.placeholder = true;
        }

        public static void Locked(LockedSpec lk, Transform parent)
        {
            var go = new GameObject("Área futura bloqueada — " + lk.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(lk.pos), Quaternion.Euler(0, lk.yaw, 0));
            var barrier = new GameObject("Bloqueio"); barrier.transform.SetParent(go.transform, false);
            RegionBuilder.Object("Véu", ProcMesh.Box(lk.width, 10f, 0.2f), Veil, go.transform.position, go.transform.rotation, Vector3.one, barrier.transform, true);
            var r = new System.Random(lk.name.GetHashCode());
            for (int i = 0; i < 6; i++)
            {
                var p = go.transform.position + go.transform.right * ((float)r.NextDouble() - 0.5f) * lk.width + go.transform.forward * ((float)r.NextDouble() * 3f - 1.5f);
                if (RegionBuilder.NatureKit && RegionBuilder.HasModel("Nature_Rubble_A")) RegionBuilder.NatureRock(i % 2 == 0 ? "Nature_Rubble_A" : "Nature_Rubble_B", new Vector2(p.x, p.z), r.Next(0, 360), 1f + (float)r.NextDouble() * 0.6f, 3f, 0.8f, 0.1f, true, barrier.transform);
                else RegionBuilder.Kit(i % 2 == 0 ? "Rock_A" : "Rock_B", new Vector3(p.x, -0.5f, p.z), r.Next(0, 360), barrier.transform, 1.2f + (float)r.NextDouble());
            }
            var sw = go.AddComponent<WorldStateSwitch>();
            sw.condition = "nunca"; sw.whenFalse = barrier;
            var poi = go.AddComponent<PointOfInterest>(); poi.kind = PoiKind.AreaFutura; poi.title = lk.name; poi.description = lk.reason;
            RegionBuilder.Tag(go, "Área futura: " + lk.reason + " — trocar a condição \"nunca\" quando o conteúdo existir", "Ambiente");
        }

        public static void DungeonEntrance(DungeonDef dd, Vector2 pos, float yaw, Transform parent)
        {
            var gs = new GateSpec { id = "masmorra", targetScene = WorldCanon.DungeonScene(dd.id), targetGate = "entrada", routeName = dd.name, pos = pos, yaw = yaw, secret = true };
            Gate(gs, parent);
            var dark = WorldMats.Stone("darkrock", new Color(0.45f, 0.42f, 0.45f), 3f);
            var go = new GameObject("Entrada da masmorra — " + dd.name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(G(pos), Quaternion.Euler(0, yaw, 0));
            RegionBuilder.Object("Boca de pedra", ProcMesh.Arch(9f, 10f, 4f, 3f), dark, go.transform.position + go.transform.forward * 1f, go.transform.rotation, Vector3.one, go.transform);
            RegionBuilder.Object("Escuridão", ProcMesh.Box(8.5f, 9f, 0.2f), WorldMats.Stone("darkrock", new Color(0.03f, 0.025f, 0.035f), 3f), go.transform.position + go.transform.forward * 3.4f, go.transform.rotation, Vector3.one, go.transform, false);
        }
    }
}

using System.Collections.Generic;
using Elyndra.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Masmorras (uma por reino, cena própria): entrada → exploração → combate → mecânica (placas de ritmo)
    /// → atalho (passagem de Eco de volta à entrada) → miniboss → sala final (recompensa) + saída. Cada uma
    /// tem uma FORMA diferente (descida, torre, anel, galhos, labirinto, eixo) e o tema do reino (materiais,
    /// luz, objetos). Salas octogonais ligadas por corredores retos (rampas quando muda a altura).
    /// </summary>
    public static class DungeonBuilder
    {
        public const string SceneDir = "Assets/World/Scenes/Dungeons";
        static Transform root;
        static System.Random rng;
        static float R01() => (float)rng.NextDouble();
        static float RR(float a, float b) => a + (b - a) * R01();

        class Room { public DungeonRole role; public Vector3 c; public float R; public List<float> doors = new List<float>(); public Transform t; }

        struct Theme { public Material wall, floor, accent; public Color light; public string prop; }

        static Theme ThemeFor(RegionId id)
        {
            Material S(string t, Color c, float sc = 3f, float gl = 0.12f) => WorldMats.Stone(t, c, sc, gl);
            switch (id)
            {
                case RegionId.Valteria: return new Theme { wall = S("camp:stone_wall", new Color(0.7f, 0.72f, 0.7f)), floor = S("camp:cobble", new Color(0.7f, 0.7f, 0.68f), 2f), accent = S("camp:stone_dark", new Color(0.75f, 0.6f, 0.35f), 1f, 0.5f), light = new Color(0.6f, 0.75f, 0.85f), prop = "agua" };
                case RegionId.Velaria: return new Theme { wall = S("camp:ashlar", new Color(0.9f, 0.8f, 0.65f)), floor = S("dryground", new Color(0.9f, 0.82f, 0.7f)), accent = S("camp:timber", new Color(0.6f, 0.45f, 0.35f), 1.5f), light = new Color(1f, 0.8f, 0.55f), prop = "trilhos" };
                case RegionId.Miralume: return new Theme { wall = S("monastery", new Color(0.85f, 0.88f, 0.95f)), floor = S("monastery", new Color(0.75f, 0.78f, 0.85f), 2f), accent = WorldMats.Crystal(new Color(0.55f, 0.65f, 0.75f, 0.5f), new Color(1.1f, 1.3f, 1.5f), new Color(0.4f, 0.5f, 0.7f)), light = new Color(0.75f, 0.85f, 1f), prop = "vidro" };
                case RegionId.Orvalume: return new Theme { wall = S("camp:timber", new Color(0.55f, 0.48f, 0.4f), 2.5f), floor = S("forestfloor", new Color(0.8f, 0.9f, 0.7f)), accent = WorldMats.Glow(new Color(0.5f, 1.4f, 0.6f), 0.3f, 1.5f), light = new Color(0.7f, 1f, 0.6f), prop = "raizes" };
                case RegionId.Helion: return new Theme { wall = S("whitecliff", new Color(1f, 0.97f, 0.9f)), floor = S("monastery", new Color(1f, 0.95f, 0.85f), 2f), accent = WorldMats.Glow(new Color(2f, 1.6f, 0.8f), 0.2f, 2f), light = new Color(1f, 0.95f, 0.8f), prop = "espelhos" };
                case RegionId.Sefra: return new Theme { wall = S("camp:stone_dark", new Color(0.6f, 0.45f, 0.7f)), floor = S("monastery", new Color(0.55f, 0.45f, 0.6f), 2f), accent = WorldMats.Glow(new Color(1.6f, 0.5f, 1.4f), 0.4f, 1.4f), light = new Color(0.9f, 0.55f, 1f), prop = "vitrines" };
                case RegionId.Nereth: return new Theme { wall = S("monastery", new Color(0.55f, 0.58f, 0.62f)), floor = S("darkrock", new Color(0.5f, 0.5f, 0.52f), 3f), accent = WorldMats.Glow(new Color(1.8f, 1f, 0.45f), 0.6f, 1.5f), light = new Color(0.8f, 0.7f, 0.55f), prop = "velas" };
                case RegionId.Granith: return new Theme { wall = S("darkrock", new Color(0.62f, 0.64f, 0.7f), 4f), floor = S("monastery", new Color(0.6f, 0.62f, 0.66f), 2f), accent = S("camp:stone_dark", new Color(0.5f, 0.5f, 0.55f), 1f, 0.5f), light = new Color(0.8f, 0.85f, 0.95f), prop = "blocos" };
                case RegionId.CoroaDeCinza: return new Theme { wall = S("darkrock", new Color(0.45f, 0.38f, 0.36f), 4f), floor = S("ash", new Color(0.55f, 0.5f, 0.48f), 3f), accent = WorldMats.Glow(new Color(2.6f, 0.8f, 0.2f), 0.4f, 1.2f), light = new Color(1f, 0.55f, 0.3f), prop = "brasas" };
                case RegionId.MarDeVidro: return new Theme { wall = S("whitecliff", new Color(0.75f, 0.9f, 0.92f), 4f), floor = S("sand", new Color(0.85f, 0.92f, 0.9f)), accent = WorldMats.Crystal(new Color(0.1f, 0.35f, 0.38f, 0.6f), new Color(0.6f, 1.6f, 1.5f), new Color(0.2f, 0.7f, 0.7f)), light = new Color(0.55f, 0.95f, 0.95f), prop = "coral" };
                case RegionId.Caliria: return new Theme { wall = S("whitecliff", new Color(1.05f, 1.05f, 1.05f), 3f), floor = S("monastery", new Color(1f, 1f, 1f), 2f), accent = S("whitecliff", new Color(1f, 1f, 1f), 1f), light = new Color(0.95f, 1f, 1f), prop = "leitos" };
                case RegionId.Sombrafonte: return new Theme { wall = S("darkrock", new Color(0.45f, 0.5f, 0.55f), 4f), floor = S("darkrock", new Color(0.4f, 0.45f, 0.48f), 3f), accent = WorldMats.Crystal(new Color(0.08f, 0.25f, 0.3f, 0.7f), new Color(0.5f, 1.4f, 1.7f), new Color(0.2f, 0.7f, 0.9f)), light = new Color(0.4f, 0.9f, 1f), prop = "cristais" };
                default: return new Theme { wall = S("cracked", new Color(0.6f, 0.6f, 0.62f), 4f), floor = S("darkrock", new Color(0.5f, 0.5f, 0.52f), 3f), accent = WorldMats.Glow(new Color(0.8f, 0.4f, 1.2f), 0.2f, 1f), light = new Color(0.6f, 0.55f, 0.7f), prop = "ausencia" };
            }
        }

        static List<(DungeonRole role, Vector3 c, float size)> Plan(DungeonLayout l)
        {
            var roles = new[] { DungeonRole.Entrada, DungeonRole.Exploracao, DungeonRole.Combate, DungeonRole.Mecanica, DungeonRole.Atalho, DungeonRole.Miniboss, DungeonRole.Final };
            float[] sizes = { 16f, 20f, 24f, 20f, 16f, 28f, 20f };
            Vector3[] p;
            switch (l)
            {
                case DungeonLayout.Descida: p = new[] { new Vector3(0, 0, 0), new Vector3(36, -4, 0), new Vector3(74, -8, 8), new Vector3(76, -12, 48), new Vector3(38, -14, 54), new Vector3(0, -18, 62), new Vector3(-6, -22, 104) }; break;
                case DungeonLayout.Torre: p = new[] { new Vector3(0, 0, 0), new Vector3(36, 4, 0), new Vector3(38, 8, 40), new Vector3(0, 12, 42), new Vector3(-36, 16, 40), new Vector3(-40, 20, 0), new Vector3(-40, 24, -42) }; break;
                case DungeonLayout.Anel:
                    p = new Vector3[7];
                    for (int i = 0; i < 7; i++) { float a = i * 360f / 7f * Mathf.Deg2Rad; p[i] = new Vector3(Mathf.Sin(a) * 62f, 0, Mathf.Cos(a) * 62f); }
                    break;
                case DungeonLayout.Galhos: p = new[] { new Vector3(0, 0, 0), new Vector3(0, 3, 40), new Vector3(-40, 6, 40), new Vector3(-40, 9, 80), new Vector3(0, 9, 82), new Vector3(42, 12, 82), new Vector3(42, 15, 124) }; break;
                case DungeonLayout.Labirinto: p = new[] { new Vector3(0, 0, 0), new Vector3(38, 0, 0), new Vector3(38, 0, 40), new Vector3(78, 0, 40), new Vector3(78, 0, 0), new Vector3(120, 0, 0), new Vector3(120, 0, 44) }; break;
                default: p = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 38), new Vector3(0, 0, 78), new Vector3(0, 0, 116), new Vector3(0, 0, 154), new Vector3(0, 0, 196), new Vector3(0, 0, 240) }; break;
            }
            var o = new List<(DungeonRole, Vector3, float)>();
            for (int i = 0; i < 7; i++) o.Add((roles[i], p[i], sizes[i]));
            return o;
        }

        static float Az(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        static Vector3 D3(float az) { var d = RegionBuilder.Dir(az); return new Vector3(d.x, 0, d.y); }

        public static string Build(DungeonDef dd, RegionProfile regionProfile, RegionProfile dungeonProfile)
        {
            rng = new System.Random(dd.id.GetHashCode());
            var log = new System.Text.StringBuilder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var region = WorldCanon.Region(dd.region);
            root = new GameObject("Masmorra — " + dd.name + " (" + region.name + ")").transform;
            var th = ThemeFor(dd.region);
            var plan = Plan(dd.layout);
            var rooms = new List<Room>();
            foreach (var (role, c, size) in plan) rooms.Add(new Room { role = role, c = c, R = size / 2f });
            for (int i = 0; i + 1 < rooms.Count; i++) { float az = Az(rooms[i].c, rooms[i + 1].c); rooms[i].doors.Add(az); rooms[i + 1].doors.Add(az + 180f); }
            // a sala final tem a saída (para fora, longe da última porta)
            var fin = rooms[rooms.Count - 1];
            float exitAz = Az(rooms[rooms.Count - 2].c, fin.c);
            fin.doors.Add(exitAz);
            rooms[0].doors.Add(Az(rooms[1].c, rooms[0].c));   // porta da entrada (volta ao reino)
            var navs = new List<(Vector3 c, float r)>();

            for (int i = 0; i < rooms.Count; i++) BuildRoom(rooms[i], th, i, dd, navs);
            for (int i = 0; i + 1 < rooms.Count; i++) Corridor(rooms[i], rooms[i + 1], th, dd.region);

            // luz e atmosfera
            var sun = new GameObject("Luz fraca de fora").AddComponent<Light>();
            sun.transform.SetParent(root, false); sun.type = LightType.Directional; sun.color = th.light; sun.intensity = 0.18f; sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(60, 30, 0);
            var p = dungeonProfile;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky; RenderSettings.ambientEquatorColor = p.ambientEquator; RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = p.fogColor; RenderSettings.fogDensity = p.fogDensity;
            RenderSettings.skybox = null;
            var ls = new LightingSettings { name = "D_" + dd.id + "_Luz", bakedGI = false, realtimeGI = false };
            string lp = $"Assets/World/Data/Lighting/D_{dd.id}.lighting";
            System.IO.Directory.CreateDirectory("Assets/World/Data/Lighting");
            AssetDatabase.DeleteAsset(lp); AssetDatabase.CreateAsset(ls, lp); Lightmapping.lightingSettings = ls;
            var rr = root.gameObject.AddComponent<RegionRoot>();
            rr.region = dd.region; rr.profile = dungeonProfile; rr.isDungeon = true; rr.dungeonId = dd.id; rr.sun = sun;

            // jogador + fluxo
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dynamic Parkour System/Prefabs/Player.prefab"));
            var model = player.GetComponentInChildren<Climbing.ThirdPersonController>().transform;
            var e0 = rooms[0];
            model.position = e0.c + Vector3.up * 0.1f;
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null) { cam.farClipPlane = 300f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
            var flow = new GameObject("Fluxo da masmorra (RegionFlow)").AddComponent<RegionFlow>();
            flow.transform.SetParent(root, false); flow.root = rr;
            var spawn = new GameObject("Chegada padrão").transform; spawn.SetParent(root, false);
            spawn.SetPositionAndRotation(e0.c + Vector3.up * 0.1f, Quaternion.Euler(0, rooms[0].doors[0], 0));
            rr.defaultSpawn = spawn;

            // portões: entrada (volta ao reino) e saída da sala final
            DoorGate(e0, e0.doors[e0.doors.Count - 1], "entrada", region.scene, "masmorra", "Saída para " + region.name, th);
            DoorGate(fin, exitAz, "saida", region.scene, "masmorra", "Saída para " + region.name, th);

            // NavMesh nas salas de luta
            int k = 0;
            System.IO.Directory.CreateDirectory("Assets/World/Data/NavMesh");
            foreach (var (c, r) in navs)
            {
                var go = new GameObject("NavMesh sala " + k); go.transform.SetParent(root, false); go.transform.position = c;
                var surf = go.AddComponent<NavMeshSurface>();
                surf.collectObjects = CollectObjects.Volume; surf.size = new Vector3(r * 2 + 6, 12, r * 2 + 6);
                surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surf.layerMask = ~((1 << 8) | (1 << 10) | (1 << 2));
                surf.BuildNavMesh();
                if (surf.navMeshData != null) { string np = $"Assets/World/Data/NavMesh/D_{dd.id}_{k}.asset"; AssetDatabase.DeleteAsset(np); AssetDatabase.CreateAsset(surf.navMeshData, np); }
                k++;
            }
            var route = new GameObject("Rota principal").AddComponent<RegionRoute>(); route.transform.SetParent(root, false);
            var pts = new List<Vector3>(); foreach (var r in rooms) pts.Add(r.c + Vector3.up * 0.2f); route.points = pts.ToArray();

            System.IO.Directory.CreateDirectory(SceneDir);
            string path = $"{SceneDir}/{WorldCanon.DungeonScene(dd.id)}.unity";
            EditorSceneManager.SaveScene(scene, path);
            log.Append($"masmorra {dd.name} ({dd.layout}): {rooms.Count} salas, {k} navmesh — {path}\n");
            return log.ToString();
        }

        static void BuildRoom(Room r, Theme th, int index, DungeonDef dd, List<(Vector3, float)> navs)
        {
            var t = new GameObject($"Sala {index + 1} — {r.role}").transform; t.SetParent(root, false); t.position = r.c;
            r.t = t;
            var dr = t.gameObject.AddComponent<DungeonRoom>(); dr.role = r.role; dr.size = new Vector3(r.R * 2, 7, r.R * 2);
            float H = r.role == DungeonRole.Miniboss ? 10f : 7f;
            RegionBuilder.Object("Chão", ProcMesh.Prism(16, r.R + 0.6f, r.R + 0.6f, 0.6f), th.floor, r.c - Vector3.up * 0.6f, Quaternion.identity, Vector3.one, t);
            RegionBuilder.Object("Teto", ProcMesh.Prism(16, r.R + 0.6f, r.R + 0.6f, 0.6f), th.wall, r.c + Vector3.up * H, Quaternion.identity, Vector3.one, t, false);
            float ap = r.R * Mathf.Cos(22.5f * Mathf.Deg2Rad), seg = 2f * r.R * Mathf.Sin(22.5f * Mathf.Deg2Rad) + 0.4f;
            for (int k = 0; k < 8; k++)
            {
                float az = k * 45f;
                bool door = false;
                foreach (var d in r.doors) if (Mathf.Abs(Mathf.DeltaAngle(d, az)) < 22.6f) door = true;
                var center = r.c + D3(az) * ap;
                if (!door) RegionBuilder.Object("Parede", ProcMesh.Box(seg, H, 0.9f), th.wall, center, Quaternion.Euler(0, az, 0), Vector3.one, t);
                else
                {
                    // vão do corredor (4,6 m) + preenchimento dos lados + verga
                    float fill = (seg - 4.8f) / 2f;
                    var right = Quaternion.Euler(0, az, 0) * Vector3.right;
                    if (fill > 0.05f) foreach (int sg in new[] { -1, 1 }) RegionBuilder.Object("Parede (lado da porta)", ProcMesh.Box(fill, H, 0.9f), th.wall, center + right * sg * (2.4f + fill / 2f), Quaternion.Euler(0, az, 0), Vector3.one, t);
                    RegionBuilder.Object("Verga", ProcMesh.Box(4.8f, H - 4.6f, 0.9f), th.wall, center + Vector3.up * 4.6f, Quaternion.Euler(0, az, 0), Vector3.one, t);
                }
            }
            // pilares + luz da sala
            for (int k = 0; k < 4; k++)
            {
                var pp = r.c + D3(k * 90f + 45f) * (r.R * 0.62f);
                RegionBuilder.Object("Pilar", ProcMesh.Prism(8, 0.7f, 0.6f, H), th.wall, pp, Quaternion.identity, Vector3.one, t);
            }
            var lgo = new GameObject("Luz da sala"); lgo.transform.SetParent(t, false); lgo.transform.position = r.c + Vector3.up * (H - 1.5f);
            var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.color = th.light; l.range = r.R * 2.2f; l.intensity = 1.4f; l.shadows = LightShadows.None;
            Props(r, th, t);
            var region = WorldCanon.Region(dd.region);
            switch (r.role)
            {
                case DungeonRole.Entrada:
                    Gameplay.CheckpointAt(new Vector2(r.c.x, r.c.z) + RegionBuilder.Dir(r.doors[0] + 90f) * (r.R * 0.5f), "D_" + dd.id + "_entrada", t);
                    break;
                case DungeonRole.Exploracao:
                    PoiAt(new PoiSpec(PoiKind.Segredo, "Nicho escondido", Vector2.zero, "algo que o tema de " + dd.name + " guardou"), r.c + D3(r.doors[0] + 120f) * (r.R * 0.75f), t);
                    PoiAt(new PoiSpec(PoiKind.Encantamento, "Inscrição encantada", Vector2.zero, dd.mechanic), r.c + D3(r.doors[0] - 120f) * (r.R * 0.7f), t);
                    break;
                case DungeonRole.Combate:
                {
                    var z = new GameObject("Zona de inimigos da masmorra").AddComponent<EnemySpawnZone>();
                    z.transform.SetParent(t, false); z.transform.position = r.c;
                    z.id = "D_" + dd.id + "_combate"; z.label = "Combate — " + dd.name; z.enemyIds = region.enemies.Length > 0 ? new[] { region.enemies[0], region.enemies[Mathf.Min(1, region.enemies.Length - 1)] } : new[] { "sussurrante" };
                    z.count = 3; z.tier = region.dangerTier; z.radius = r.R * 0.5f; z.activation = r.R + 6f; z.despawn = 60f; z.respawn = false;
                    navs.Add((r.c, r.R));
                    break;
                }
                case DungeonRole.Mecanica:
                {
                    var rp = new GameObject("Mecânica — " + dd.mechanic).AddComponent<RhythmPlates>();
                    rp.transform.SetParent(t, false); rp.transform.position = r.c; rp.id = dd.id;
                    for (int k = 0; k < 4; k++)
                    {
                        var pp = r.c + D3(k * 90f + 20f) * (r.R * 0.4f);
                        var plate = RegionBuilder.Object("Placa " + (k + 1), ProcMesh.Prism(8, 1.1f, 1.1f, 0.15f), WorldMats.Stone("camp:stone_dark", new Color(0.4f, 0.37f, 0.34f), 1f, 0.4f), pp, Quaternion.identity, Vector3.one, rp.transform, false);
                        rp.plates.Add(plate.transform);
                    }
                    // a porta para a próxima sala fica fechada até resolver
                    float az = r.doors[r.doors.Count > 1 ? 1 : 0];
                    var door = RegionBuilder.Object("Porta que responde ao ritmo", ProcMesh.Box(4.8f, 4.6f, 0.6f), th.accent, r.c + D3(az) * (r.R * Mathf.Cos(22.5f * Mathf.Deg2Rad)), Quaternion.Euler(0, az, 0), Vector3.one, rp.transform, true);
                    rp.door = door;
                    break;
                }
                case DungeonRole.Atalho:
                {
                    var sd = new GameObject("Atalho (passagem de Eco)").AddComponent<ShortcutDoor>();
                    sd.transform.SetParent(t, false); sd.transform.position = r.c; sd.id = dd.id;
                    var open = new GameObject("Abrir daqui").transform; open.SetParent(sd.transform, false); open.position = r.c + D3(r.doors[0] + 180f) * (r.R * 0.4f);
                    sd.openFrom = open;
                    sd.portalA = PortalMark(r.c + D3(r.doors[0] + 90f) * (r.R * 0.6f), r.doors[0] - 90f, sd.transform);
                    sd.portalVisualA = PortalVisual(sd.portalA, th);
                    var e = root.Find("Sala 1 — Entrada");
                    var ePos = e != null ? e.position : Vector3.zero;
                    sd.portalB = PortalMark(ePos + D3(45f) * 4.5f, 225f, sd.transform);
                    sd.portalVisualB = PortalVisual(sd.portalB, th);
                    break;
                }
                case DungeonRole.Miniboss:
                {
                    var a = new GameObject("Arena do miniboss — " + dd.miniboss).AddComponent<BossArena>();
                    a.transform.SetParent(t, false); a.transform.position = r.c;
                    a.minibossName = dd.miniboss; a.minibossEnemyId = MinibossId(dd); a.radius = r.R * 0.85f;
                    var bs = new GameObject("Ponto do chefe").transform; bs.SetParent(a.transform, false); bs.position = r.c + D3(r.doors[r.doors.Count - 1]) * (r.R * 0.4f); bs.rotation = Quaternion.Euler(0, r.doors[0], 0);
                    a.bossSpawn = bs; a.entrance = new GameObject("Entrada").transform; a.entrance.SetParent(a.transform, false); a.entrance.position = r.c + D3(r.doors[0]) * r.R;
                    navs.Add((r.c, r.R));
                    break;
                }
                case DungeonRole.Final:
                    PoiAt(new PoiSpec(PoiKind.Artefato, dd.reward, Vector2.zero, "recompensa de " + dd.name), r.c, t);
                    break;
            }
        }

        static string MinibossId(DungeonDef dd)
        {
            switch (dd.region)
            {
                case RegionId.Valteria: return "sussurrante_loop";
                case RegionId.Orvalume: return "cervo_contratempo_alfa";
                default: return "";
            }
        }

        static Transform PortalMark(Vector3 p, float yaw, Transform parent)
        {
            var t = new GameObject("Portal").transform; t.SetParent(parent, false); t.position = p; t.rotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        static GameObject PortalVisual(Transform at, Theme th)
        {
            var g = new GameObject("Passagem de Eco (visual)"); g.transform.SetParent(at, false);
            RegionBuilder.Object("Anel", ProcMesh.Prism(12, 1.3f, 1.3f, 0.12f), WorldMats.Glow(new Color(0.8f, 0.6f, 1.6f), 0.5f, 1f), at.position + Vector3.up * 0.05f, Quaternion.identity, Vector3.one, g.transform, false);
            RegionBuilder.Object("Coluna de luz", ProcMesh.Prism(10, 0.9f, 0.6f, 3f, false), WorldMats.Glow(new Color(0.4f, 0.3f, 0.9f), 0.3f, 0.7f), at.position, Quaternion.identity, Vector3.one, g.transform, false);
            return g;
        }

        static void PoiAt(PoiSpec s, Vector3 p, Transform parent)
        {
            var go = new GameObject($"POI — {s.kind}: {s.title}"); go.transform.SetParent(parent, false); go.transform.position = p;
            var poi = go.AddComponent<PointOfInterest>(); poi.kind = s.kind; poi.title = s.title; poi.description = s.desc;
            var vis = new GameObject("Altar"); vis.transform.SetParent(go.transform, false);
            RegionBuilder.Object("Pedestal", ProcMesh.Box(1.2f, 0.9f, 0.8f), WorldMats.Stone("camp:stone_dark", new Color(0.7f, 0.65f, 0.6f), 1.2f), p, Quaternion.identity, Vector3.one, vis.transform);
            RegionBuilder.Object("Brilho", ProcMesh.Sphere(8), WorldMats.Glow(s.kind == PoiKind.Segredo ? new Color(0.6f, 0.4f, 1.1f) : new Color(1.2f, 0.9f, 0.5f), 0.4f, 2f), p + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.4f, vis.transform, false);
            poi.visual = vis;
        }

        static void Corridor(Room a, Room b, Theme th, RegionId region)
        {
            float az = Az(a.c, b.c);
            float apA = a.R * Mathf.Cos(22.5f * Mathf.Deg2Rad), apB = b.R * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            var s = a.c + D3(az) * (apA - 0.3f); var e = b.c - D3(az) * (apB - 0.3f);
            var mid = (s + e) / 2f; var d = e - s;
            float len = new Vector2(d.x, d.z).magnitude, rise = d.y;
            float pitch = Mathf.Atan2(rise, len) * Mathf.Rad2Deg;
            float full = Mathf.Sqrt(len * len + rise * rise);
            var rot = Quaternion.Euler(-pitch, az, 0);
            var t = new GameObject("Corredor").transform; t.SetParent(root, false); t.position = mid;
            RegionBuilder.Object("Piso", ProcMesh.Box(4.8f, 0.5f, full + 0.6f), th.floor, mid - Vector3.up * 0.5f, rot, Vector3.one, t);
            var right = rot * Vector3.right;
            foreach (int sg in new[] { -1, 1 }) RegionBuilder.Object("Parede do corredor", ProcMesh.Box(0.7f, 5f, full + 0.6f), th.wall, mid + right * sg * 2.75f - Vector3.up * 0.3f, rot, Vector3.one, t);
            RegionBuilder.Object("Teto do corredor", ProcMesh.Box(6.2f, 0.4f, full + 0.6f), th.wall, mid + Vector3.up * 4.6f, rot, Vector3.one, t, false);
            int n = Mathf.Max(1, Mathf.RoundToInt(full / 10f));
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(s, e, (i + 0.5f) / n);
                RegionBuilder.Object("Lume do corredor", ProcMesh.Sphere(6), th.accent.shader.name == "Elyndra/Glow" ? th.accent : WorldMats.Glow(th.light * 1.4f, 0.2f, 1.4f), p + Vector3.up * 3.6f + right * 2.2f, Quaternion.identity, Vector3.one * 0.3f, t, false);
            }
        }

        static void Props(Room r, Theme th, Transform t)
        {
            int n = Mathf.RoundToInt(r.R * 0.6f);
            for (int i = 0; i < n; i++)
            {
                float az = RR(0, 360);
                bool nearDoor = false; foreach (var d in r.doors) if (Mathf.Abs(Mathf.DeltaAngle(d, az)) < 30f) nearDoor = true;
                if (nearDoor) continue;
                var p = r.c + D3(az) * (r.R * RR(0.72f, 0.88f));
                switch (th.prop)
                {
                    case "cristais": case "coral": case "vidro":
                        RegionBuilder.Object("Cristal", ProcMesh.Crystal(i % 12), th.accent.shader.name == "Elyndra/Crystal" ? th.accent : WorldMats.Crystal(new Color(0.3f, 0.4f, 0.5f, 0.6f), th.light * 1.4f, th.light * 0.6f), p, Quaternion.Euler(RR(-20, 20), RR(0, 360), RR(-20, 20)), new Vector3(1.2f, RR(2f, 4.5f), 1.2f), t, false);
                        break;
                    case "raizes":
                        RegionBuilder.Object("Raiz", ProcMesh.Prism(5, 0.6f, 0.08f, RR(3f, 6f), true, 60f), th.wall, p, Quaternion.Euler(RR(-40, 40), az, RR(-40, 40)), Vector3.one, t, false);
                        RegionBuilder.Object("Fungo aceso", ProcMesh.Sphere(6), th.accent, p + Vector3.up * 0.4f, Quaternion.identity, Vector3.one * 0.25f, t, false);
                        break;
                    case "velas": case "brasas":
                        RegionBuilder.Object(th.prop == "velas" ? "Urna" : "Braseiro", ProcMesh.Prism(8, 0.45f, 0.35f, 1f), WorldMats.Stone("camp:stone_dark", new Color(0.5f, 0.45f, 0.4f), 1f), p, Quaternion.identity, Vector3.one, t);
                        RegionBuilder.Object("Chama", ProcMesh.Sphere(6), th.accent, p + Vector3.up * 1.15f, Quaternion.identity, Vector3.one * 0.22f, t, false);
                        break;
                    case "trilhos":
                        RegionBuilder.Object("Caixote", ProcMesh.Box(1.2f, 1f, 1.2f), th.accent, p, Quaternion.Euler(0, RR(0, 90), 0), Vector3.one, t);
                        break;
                    case "blocos":
                        RegionBuilder.Object("Bloco de basalto afinado", ProcMesh.Box(RR(1.4f, 2.4f), RR(1f, 2.5f), RR(1.4f, 2.4f)), th.wall, p, Quaternion.Euler(0, RR(0, 90), 0), Vector3.one, t);
                        break;
                    case "agua":
                        RegionBuilder.Object("Canal", ProcMesh.Box(1.4f, 0.1f, RR(3f, 6f)), WorldMats.Water("D_valteria", new Color(0.03f, 0.08f, 0.1f, 0.85f), new Color(0.5f, 0.6f, 0.7f)), p + Vector3.up * 0.02f, Quaternion.Euler(0, az, 0), Vector3.one, t, false);
                        break;
                    case "espelhos":
                        RegionBuilder.Object("Espelho", ProcMesh.Box(1.6f, 3f, 0.12f), WorldMats.Crystal(new Color(0.85f, 0.85f, 0.8f, 0.6f), new Color(1.6f, 1.5f, 1.2f), new Color(0.6f, 0.55f, 0.4f)), p, Quaternion.Euler(0, az, 0), Vector3.one, t);
                        break;
                    case "vitrines":
                        RegionBuilder.Object("Vitrine de sonho", ProcMesh.Box(1.2f, 2.2f, 1.2f), WorldMats.Crystal(new Color(0.5f, 0.25f, 0.5f, 0.45f), new Color(1.6f, 0.6f, 1.5f), new Color(0.7f, 0.3f, 0.7f)), p, Quaternion.Euler(0, az, 0), Vector3.one, t);
                        break;
                    case "leitos":
                        RegionBuilder.Object("Leito", ProcMesh.Box(1f, 0.6f, 2.1f), th.accent, p, Quaternion.Euler(0, az, 0), Vector3.one, t);
                        break;
                    default:
                        RegionBuilder.Object("Entulho flutuando", ProcMesh.Sphere(6, 0.3f, i), th.wall, p + Vector3.up * RR(1.5f, 4f), Quaternion.Euler(RR(0, 360), RR(0, 360), 0), Vector3.one * RR(0.6f, 1.4f), t, false);
                        break;
                }
            }
        }

        static void DoorGate(Room r, float az, string id, string targetScene, string targetGate, string routeName, Theme th)
        {
            float ap = r.R * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            var go = new GameObject("Portão — " + routeName + " (" + id + ")");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(r.c + D3(az) * (ap + 1.5f), Quaternion.Euler(0, az, 0));
            var trig = new GameObject("Gatilho"); trig.transform.SetParent(go.transform, false); trig.transform.localPosition = new Vector3(0, 2.3f, 0.5f);
            var bc = trig.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(4.6f, 4.4f, 2f); trig.layer = 2;
            var gate = trig.AddComponent<RegionGate>(); gate.id = id; gate.targetScene = targetScene; gate.targetGate = targetGate; gate.routeName = routeName;
            var arr = new GameObject("Chegada").transform; arr.SetParent(go.transform, false);
            arr.position = r.c + D3(az) * (ap - 5f) + Vector3.up * 0.1f; arr.rotation = Quaternion.Euler(0, az + 180f, 0);
            gate.arrival = arr;
            RegionBuilder.Object("Luz do lado de fora", ProcMesh.Box(4.6f, 4.4f, 0.1f), WorldMats.Glow(new Color(0.9f, 0.85f, 0.7f) * 1.2f, 0f, 0.4f), go.transform.position + go.transform.forward * 2.2f + Vector3.up * 2.2f, go.transform.rotation, Vector3.one, go.transform, false);
            // pequeno corredor até a luz
            RegionBuilder.Object("Piso da saída", ProcMesh.Box(4.8f, 0.5f, 4f), th.floor, go.transform.position - Vector3.up * 0.5f + go.transform.forward * 0.6f, go.transform.rotation, Vector3.one, go.transform);
        }
    }
}

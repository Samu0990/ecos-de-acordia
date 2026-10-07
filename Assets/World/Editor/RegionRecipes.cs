using System.Collections.Generic;
using Elyndra.World;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Receitas dos 13 reinos de Elyndra (Bíblia v2): relevo, chão, cidades, marcos, rotas, inimigos por
    /// nível, Vórtices, arenas das Notas e minibosses, relíquias, segredos e áreas futuras. Valtéria é a
    /// rota jogável do Ato I (Campânula → arredores → floresta → primeiros Corrompidos → Ermida do Sino
    /// (Vórtice) → Vale Partido → Cratera do Primeiro Peso / Dó Partido). Os outros reinos têm a fundação
    /// completa (cultura, estrutura e ganchos) para o conteúdo entrar depois.
    /// </summary>
    public static class RegionRecipes
    {
        static Vector2 V(float x, float z) => new Vector2(x, z);

        // ---------------------------------------------------------------- ruído
        public static float N(float x, float z, float scale, int oct = 4, float seed = 0f)
        {
            float s = 0, a = 1, tot = 0, f = 1f / scale;
            for (int o = 0; o < oct; o++) { s += (Mathf.PerlinNoise(x * f + seed + o * 17.3f, z * f - seed + o * 9.1f) - 0.5f) * a; tot += a; a *= 0.5f; f *= 2.03f; }
            return s / tot * 2f;   // ~[-1,1]
        }
        public static float Ridge(float x, float z, float scale, int oct = 4, float seed = 0f)
        {
            float s = 0, a = 1, tot = 0, f = 1f / scale;
            for (int o = 0; o < oct; o++) { float v = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + seed + o * 5.7f, z * f + o * 3.3f) * 2f - 1f); s += v * v * a; tot += a; a *= 0.5f; f *= 2.1f; }
            return s / tot;
        }
        public static float Bump(float x, float z, float cx, float cz, float r, float h) { float d2 = ((x - cx) * (x - cx) + (z - cz) * (z - cz)) / (r * r); return h * Mathf.Exp(-d2); }
        public static float SS(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
        public static float Crater(float x, float z, float cx, float cz, float r, float depth, float rim)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / r;
            if (d < 1f) return -depth * (1f - d * d) + rim * d * d * d * d;
            return rim * Mathf.Exp(-(d - 1f) * (d - 1f) * 6f);
        }
        static float DistToPolyline(Vector2 p, List<Vector2> pl)
        {
            float best = 1e9f;
            for (int i = 0; i + 1 < pl.Count; i++)
            {
                var a = pl[i]; var b = pl[i + 1]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>Portões para os reinos vizinhos (pelas rotas do cânone) na borda, na direção certa, com estrada até o centro.</summary>
        static void AutoGates(RegionLayout L, Vector2 hub, params RegionId[] skip)
        {
            var me = WorldCanon.Region(L.id);
            var placed = new List<Vector2>();
            foreach (var g in L.gates) placed.Add(g.pos);
            foreach (var r in WorldCanon.RoutesOf(L.id))
            {
                var other = WorldCanon.Other(r, L.id);
                if (System.Array.IndexOf(skip, other) >= 0) continue;
                var od = WorldCanon.Region(other);
                var d3 = WorldCanon.Direction(L.id, other); var d = new Vector2(d3.x, d3.z);
                float t = (L.size / 2 - 50f) / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                var pos = L.center + d * t;
                foreach (var q in placed)
                    if (Vector2.Distance(q, pos) < 140f) { var tang = new Vector2(-d.y, d.x); pos += tang * 160f; pos = L.center + Vector2.ClampMagnitude(pos - L.center, 1e6f); pos.x = Mathf.Clamp(pos.x, L.center.x - L.size / 2 + 50, L.center.x + L.size / 2 - 50); pos.y = Mathf.Clamp(pos.y, L.center.y - L.size / 2 + 50, L.center.y + L.size / 2 - 50); }
                placed.Add(pos);
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                L.gates.Add(new GateSpec { id = "rota_" + od.scene, targetScene = od.scene, targetGate = "rota_" + me.scene, routeName = r.name, requires = r.requires ?? "", pos = pos, yaw = yaw, secret = r.secret });
                var style = r.kind == RouteKind.Estrada || r.kind == RouteKind.PonteSuspensa ? PathStyle.Estrada : PathStyle.Trilha;
                var mid = Vector2.Lerp(hub, pos, 0.5f) + new Vector2(-d.y, d.x) * (Mathf.PerlinNoise(pos.x * 0.01f, pos.y * 0.01f) - 0.5f) * 160f;
                L.paths.Add(new PathSpec("Rumo a " + od.name + " — " + r.name, style, style == PathStyle.Estrada ? 6f : 4f, hub, mid, pos - d * 12f, pos) { lamps = style == PathStyle.Estrada });
            }
        }

        public static RegionLayout For(RegionId id)
        {
            switch (id)
            {
                case RegionId.Valteria: return Valteria();
                case RegionId.Velaria: return Velaria();
                case RegionId.Miralume: return Miralume();
                case RegionId.Orvalume: return Orvalume();
                case RegionId.Helion: return Helion();
                case RegionId.Sefra: return Sefra();
                case RegionId.Nereth: return Nereth();
                case RegionId.Granith: return Granith();
                case RegionId.CoroaDeCinza: return Coroa();
                case RegionId.MarDeVidro: return MarDeVidro();
                case RegionId.Caliria: return Caliria();
                case RegionId.Sombrafonte: return Sombrafonte();
                default: return Fronteira();
            }
        }

        // ================================================================ VALTÉRIA — Ato I
        static RegionLayout Valteria()
        {
            var L = new RegionLayout { id = RegionId.Valteria, center = V(1650, 300), size = 3000, heightRes = 1025 };
            var river = new List<Vector2> { V(2800, 1700), V(2350, 1250), V(1950, 820), V(1625, 470), V(1350, 60), V(1050, -500), V(900, -1150) };
            L.rivers.Add(river); L.riverWidth = 10f;
            L.height = (x, z) =>
            {
                float h = 22f + 26f * N(x, z, 700f, 4, 3f) + 8f * N(x, z, 140f, 3, 11f);
                float north = SS(900f, 1650f, z);                                   // serras ao norte (rumo a Miralume e Granith)
                h += north * (60f + 190f * Ridge(x, z, 520f, 4, 7f));
                float south = SS(-500f, -1200f, z);                                 // morros ao sul (descida para Calíria)
                h += south * 55f * Ridge(x, z, 380f, 3, 2f);
                h += Bump(x, z, 1900f, 1040f, 170f, 46f);                           // morro da Ermida do Sino
                h += Bump(x, z, 2600f, 240f, 240f, 38f);
                h += Crater(x, z, 2380f, 770f, 62f, 20f, 9f);                       // Cratera do Primeiro Peso
                float dv = DistToPolyline(V(x, z), river);                          // vale do Rio Claro
                h -= 18f * (1f - SS(20f, 160f, dv));
                h -= 30f * SS(220f, 150f, x);                                       // descida para o vale de Campânula (oeste)
                return h;
            };
            L.special = (x, z, y, slope) =>
            {
                // campos de trigo perto da Vila do Moinho (bordas irregulares, nunca subindo morros)
                float fe = N(x, z, 60f, 2, 5f) * 40f;
                float field = (Vector2.Distance(V(x, z), V(560, -150)) < 260f + fe && slope < 9f && z < 120f) ? 1f : 0f;
                return field * (0.6f + 0.4f * SS(-0.2f, 0.4f, N(x, z, 25f, 2, 1f)));
            };
            L.tex.field = "";   // o shader usa o campo (palha) de Campânula
            L.veg.trees = 0.55f; L.veg.wheat = 1f; L.veg.grass = 0.8f; L.veg.rocks = 0.3f;
            L.veg.density = (x, z) => SS(850f, 1050f, x) * SS(1800f, 1550f, x) * SS(-200f, 0f, z) + 0.6f * SS(950f, 1400f, z);
            L.windowGlow = false;   // dia claro (prancha do autor)
            L.waterDeep = new Color(0.04f, 0.12f, 0.15f, 0.85f); L.waterSky = new Color(0.62f, 0.72f, 0.82f);

            // Campânula a oeste: o portão leste da cidade continua aqui
            L.gates.Add(new GateSpec { id = "portao_campanula", targetScene = WorldCanon.CampanulaScene, targetGate = "valteria", routeName = "Portão Leste de Campânula", pos = V(205, 12), yaw = 270f });
            L.spawn = V(330, 12); L.spawnYaw = 90f;
            var road = new PathSpec("Estrada de Campânula", PathStyle.Estrada, 6.5f, V(205, 12), V(420, 5), V(620, -40), V(820, -10), V(1050, 110), V(1300, 250), V(1460, 380), V(1590, 448)) { lamps = true };
            var bridge = new PathSpec("Ponte Velha do Rio Claro", PathStyle.Ponte, 6f, V(1590, 448), V(1660, 492));
            var road2 = new PathSpec("Estrada da Ermida", PathStyle.Estrada, 5.5f, V(1660, 492), V(1760, 640), V(1830, 860), V(1880, 990)) { lamps = true };
            var road3 = new PathSpec("Descida do Vale Partido", PathStyle.Trilha, 5f, V(1880, 990), V(2020, 900), V(2150, 760), V(2270, 720), V(2318, 735));
            L.paths.Add(road); L.paths.Add(bridge); L.paths.Add(road2); L.paths.Add(road3);
            L.route = new List<Vector2> { V(330, 12), V(620, -40), V(820, -10), V(1050, 110), V(1300, 250), V(1460, 380), V(1580, 444), V(1670, 498), V(1760, 640), V(1830, 860), V(1880, 990), V(2020, 900), V(2150, 760), V(2300, 728) };

            var vila = new SettlementSpec("Vila do Moinho", SettlementStyle.Gotica, V(760, -40), 58f, 14) { exits = new[] { 75f, 260f, 170f } };
            L.settlements.Add(vila);
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CidadeDistante, "Campânula (vista da estrada leste)", V(75, 10), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Moinhos, "Moinhos do Vale", V(520, -260), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Aqueduto, "Aqueduto do Vale (ramal do Grande Aqueduto)", V(1150, -260), 1f, 30f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Ermida, "Ermida do Sino", V(1905, 1045), 1f, 200f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cratera, "Cratera do Primeiro Peso", V(2380, 770), 0.9f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Ruina, "Torre caída do vau", V(1400, 560), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Obelisco, "Pedra-marco das caravanas", V(2700, 420), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cachoeira, "Cachoeira do Rio Claro", V(2440, 1340), 1.2f, 225f));

            L.zones.Add(new ZoneSpec("v_primeiros", "Primeiros Corrompidos (borda da floresta)", V(1230, 215), 1, 2, "sussurrante"));
            L.zones.Add(new ZoneSpec("v_lenhadores", "Clareira dos Lenhadores (o corpo reage antes do passo)", V(1440, 395), 1, 1, "passante_invertido"));
            L.zones.Add(new ZoneSpec("v_ponte", "Ponte Velha (moradores que fugiram de Campânula)", V(1700, 545), 1, 3, "sussurrante", "morador_sem_palavra", "sussurrante"));
            L.zones.Add(new ZoneSpec("v_ermida", "Estrada da Ermida", V(1800, 800), 2, 3, "sussurrante"));
            L.zones.Add(new ZoneSpec("v_vale", "Vale Partido", V(2150, 745), 2, 3, "sussurrante", "sussurrante", "sussurrante_loop"));
            L.zones.Add(new ZoneSpec("v_borda", "Borda da Cratera", V(2265, 660), 2, 4, "sussurrante_loop", "sussurrante", "passante_invertido"));
            L.zones.Add(new ZoneSpec("v_bosque", "Bosque do Norte (caçador perdido — segredo)", V(1320, 1120), 2, 1, "passante_invertido"));
            L.checkpoints.AddRange(new[] { V(345, 30), V(735, -6), V(1425, 352), V(1860, 950) });
            L.arenas.Add(new ArenaSpec { noteId = "do", pos = V(2380, 770), radius = 44f, yaw = 70f, dressing = LandmarkKind.Cratera });
            L.arenas.Add(new ArenaSpec { miniboss = "Regente Desfeito de Campânula", enemyId = "regente_desfeito", pos = V(1960, 1110), radius = 22f, yaw = 200f });
            L.vortices.Add(new VortexSpec { id = "sino_amanhas", pos = V(1905, 1045), radius = 75f, activeWhen = "fase:BrilhoCaiu", offbeatBell = true });
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Esconderijo da Oficina Clandestina", V(805, 5), "uma flauta inacabada e páginas da Primeira Oficina"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Marco com Nota de Farol", V(1090, 135), "Farol: revela o caminho quando alguém passa em silêncio"));
            L.pois.Add(new PoiSpec(PoiKind.Recompensa, "Carroça de um mascate de Campânula", V(1712, 560), "ele fugiu pela ponte antes da Corrupção alcançá-lo"));
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Afinadora de Campo", V(2235, 705), "", "", "Afinadores de Campo — estuda a queda e os Contramotivos"));
            L.pois.Add(new PoiSpec(PoiKind.Marco, "Mirante da Fenda", V(1600, 1450), "daqui a Fenda aparece entre as serras — muito, muito longe"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Gruta atrás das pedras (Gruta do Eco Longo)", V(1180, 1320), "o eco aqui dura tempo demais"));
            L.locked.Add(new LockedSpec { name = "Pedreira desabada", pos = V(2700, 980), yaw = 30f, reason = "conteúdo futuro: pedreira de Valtéria" });
            L.locked.Add(new LockedSpec { name = "Estrada do Moinho Velho", pos = V(420, -620), yaw = 0f, reason = "conteúdo futuro: fazendas do sul" });
            L.dungeonPos = V(1110, -230); L.dungeonYaw = 210f;
            AutoGates(L, V(1660, 492));
            L.stoneTint = new Color(1.12f, 1.1f, 1.06f); L.roofTint = new Color(0.82f, 0.88f, 1f);
            return L;
        }

        // ================================================================ VELÁRIA
        static RegionLayout Velaria()
        {
            var L = new RegionLayout { id = RegionId.Velaria };
            L.height = (x, z) =>
            {
                float h = 30f + 22f * N(x, z, 600f, 4, 21f);
                float canyon = Mathf.Abs(z - 40f * N(x, 0, 300f, 2, 3f));          // cânion leste-oeste cortando as planícies
                h -= 65f * (1f - SS(25f, 90f, canyon));
                h += Bump(x, z, 250f, 380f, 160f, 22f);                            // platô da cidade-caravana
                h += 90f * SS(500f, 780f, Mathf.Abs(x)) * Ridge(x, z, 300f, 3, 9f);
                h += Bump(x, z, -420f, -330f, 70f, 85f);                           // pináculo do Mirante dos Ventos Cruzados
                return h;
            };
            L.special = (x, z, y, slope) => slope < 12f ? SS(0.1f, 0.4f, N(x, z, 90f, 3, 4f)) * 0.7f : 0f;
            L.tex = new LayoutTextures { baseNear = "dryground", baseFar = "sand", path = "camp:gnd_path", field = "sand", rock = "camp:gnd_rock", baseTint = new Color(1.15f, 0.88f, 0.62f), farTint = new Color(1.15f, 0.85f, 0.58f), dryTint = new Color(1.15f, 0.9f, 0.65f) };
            L.veg = new VegSpec { trees = 0.12f, rocks = 0.5f, grass = 0.55f, treeKinds = new[] { "Tree_Pine", "Tree_Oak2" } };
            L.settlements.Add(new SettlementSpec("Cidade-Caravana de Passo Largo", SettlementStyle.Caravana, V(250, 380), 60f, 0) { exits = new[] { 180f, 90f, 270f } });
            L.settlements.Add(new SettlementSpec("Pedágio do Compasso", SettlementStyle.Gotica, V(-180, -420), 45f, 8) { exits = new[] { 0f, 90f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.EstradaSuspensa, "Estradas Suspensas", V(40, 40), 1f, 0f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CidadeCaravana, "Círculo das carroças", V(250, 380), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Obelisco, "Mirante dos Ventos Cruzados", V(-420, -330), 1.2f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Pinaculos, "Pináculos do Vento", V(-330, 120), 1.1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Pinaculos, "Agulhas da Planície", V(560, 60), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Pinaculos, "Torres de Arenito", V(180, -600), 1.2f));
            L.paths.Add(new PathSpec("Estrada das Caravanas (norte)", PathStyle.Estrada, 7f, V(40, -240), V(40, -120), V(40, -75)) { lamps = true });
            L.paths.Add(new PathSpec("Estrada das Caravanas (sul)", PathStyle.Estrada, 7f, V(40, 155), V(60, 260), V(250, 330)) { lamps = true });
            L.paths.Add(new PathSpec("Subida do Mirante", PathStyle.Trilha, 3.5f, V(-180, -420), V(-330, -380), V(-410, -340)));
            L.spawn = V(40, -240); L.spawnYaw = 0f;
            L.zones.Add(new ZoneSpec("ve_planicie", "Planície dos Passos", V(-80, -200), 2, 3, "passante_invertido", "passante_invertido", "sussurrante"));
            L.zones.Add(new ZoneSpec("ve_canion", "Borda do cânion (peregrinos)", V(260, -120), 2, 2, "peregrino_estouro", "passante_invertido"));
            L.zones.Add(new ZoneSpec("ve_caravaneiros", "Caravaneiros partidos", V(420, 230), 3, 3, "partido_em_dois", "passante_invertido"));
            L.arenas.Add(new ArenaSpec { noteId = "re", pos = V(520, -380), radius = 38f, yaw = 300f });
            L.arenas.Add(new ArenaSpec { miniboss = "Cavaleiro do Passo Repetido", enemyId = "cavaleiro_passo", pos = V(-480, 260), radius = 24f });
            L.vortices.Add(new VortexSpec { id = "ponte_margens", pos = V(40, 40), radius = 70f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Agulha do Vendaval", V(-420, -330), "", "agulha"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Koru, o guerreiro cego", V(-410, -315), "Custódio da Agulha do Vendaval"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Pedágio de Nota de Chave", V(-180, -380), "a cancela só responde a uma cadência"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Carroça abandonada no cânion", V(300, -30), "rastros de uma caravana que chegou antes de partir"));
            L.locked.Add(new LockedSpec { name = "Rota das cidades móveis do leste", pos = V(700, 620), yaw = 45f, reason = "conteúdo futuro: cidades móveis" });
            L.dungeonPos = V(-120, -470); L.dungeonYaw = 180f;
            L.checkpoints.AddRange(new[] { V(40, -200), V(230, 330), V(-160, -380) });
            L.route = new List<Vector2> { V(40, -240), V(40, -120), V(40, 160), V(250, 330) };
            AutoGates(L, V(40, -140));
            L.stoneTint = new Color(1.05f, 0.95f, 0.82f); L.roofTint = new Color(1.1f, 0.8f, 0.6f);
            return L;
        }

        // ================================================================ MIRALUME
        static RegionLayout Miralume()
        {
            var L = new RegionLayout { id = RegionId.Miralume, seaLevel = 6f };
            L.height = (x, z) =>
            {
                float h = 24f + 20f * N(x, z, 500f, 4, 31f);
                h += 70f * SS(0.15f, 0.6f, Ridge(x, z, 420f, 3, 2f)) * SS(200f, 700f, Mathf.Abs(z + 100f));
                h -= Bump(x, z, -300f, 250f, 160f, 30f) + Bump(x, z, 380f, -320f, 130f, 26f);   // lagos
                h += Bump(x, z, 60f, 60f, 200f, 18f);                                            // terraço da cidade
                return h;
            };
            L.special = (x, z, y, slope) => y < 9f ? 0.8f : 0f;
            L.tex = new LayoutTextures { baseNear = "mudleaves", baseFar = "camp:gnd_meadow", path = "camp:gnd_path", cobble = "monastery", field = "sand", rock = "camp:gnd_rock", baseTint = new Color(0.75f, 0.8f, 0.78f), farTint = new Color(0.7f, 0.78f, 0.75f) };
            L.veg = new VegSpec { trees = 0.25f, rocks = 0.3f, grass = 0.45f, treeKinds = new[] { "Tree_Pine" }, crystals = 0.12f, crystalColor = new Color(0.75f, 0.85f, 1f) };
            L.windowGlow = true;
            L.settlements.Add(new SettlementSpec("Miralume (cidade dos arquivos)", SettlementStyle.Vidro, V(60, 60), 70f, 18) { exits = new[] { 0f, 120f, 240f } });
            L.settlements.Add(new SettlementSpec("Vila dos Copistas", SettlementStyle.Gotica, V(-420, -260), 42f, 9) { exits = new[] { 45f, 200f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.TorreVidro, "Torres de Vidro Fosco", V(170, 230), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Ruina, "Casa do Mesmo Corredor (ruína)", V(-150, -380), 1.2f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cachoeira, "Queda do Lago Esquecido", V(-420, 330), 1f, 124f));
            L.paths.Add(new PathSpec("Calçada dos Arquivos", PathStyle.Calcada, 6f, V(60, -60), V(60, -300), V(-200, -330), V(-420, -260)) { lamps = true });
            L.spawn = V(60, -300);
            L.zones.Add(new ZoneSpec("mi_lago", "Margem do lago esquecido", V(-260, 140), 2, 3, "morador_sem_palavra", "sussurrante_loop"));
            L.zones.Add(new ZoneSpec("mi_pinhal", "Pinhal dos que esqueceram o nome", V(380, 300), 2, 2, "morador_sem_palavra", "confessor_sem_eco"));
            L.zones.Add(new ZoneSpec("mi_copistas", "Copistas da Fenda", V(300, -120), 3, 2, "copista_fenda"));
            L.arenas.Add(new ArenaSpec { noteId = "mi", pos = V(170, 330), radius = 34f, yaw = 180f });
            L.arenas.Add(new ArenaSpec { miniboss = "Bibliotecário Sem Nome", enemyId = "bibliotecario", pos = V(-150, -380), radius = 22f });
            L.vortices.Add(new VortexSpec { id = "mesmo_corredor", pos = V(-150, -380), radius = 55f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Prisma do Encanto", V(120, 200), "", "prisma"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Mair Selen, a arquivista", V(110, 185), "Custódia do Prisma — sua sombra registra o que a voz não repete"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Porta de Nota de Chave", V(30, 120), "não reconhece o Aren (ele não tem assinatura)"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Registro sem nome", V(-470, -300), "um registro civil com uma lacuna no lugar de um nome"));
            L.locked.Add(new LockedSpec { name = "Arquivo Profundo", pos = V(260, 420), yaw = 0f, reason = "conteúdo futuro: arquivos selados pelo Conservatório" });
            L.dungeonPos = V(220, 150); L.dungeonYaw = 30f;
            L.checkpoints.AddRange(new[] { V(60, -260), V(40, 20), V(-400, -230) });
            L.route = new List<Vector2> { V(60, -300), V(60, -60), V(60, 60) };
            AutoGates(L, V(60, -60));
            L.stoneTint = new Color(0.92f, 0.95f, 1.02f); L.roofTint = new Color(0.75f, 0.82f, 0.95f);
            return L;
        }

        // ================================================================ ORVALUME
        static RegionLayout Orvalume()
        {
            var L = new RegionLayout { id = RegionId.Orvalume };
            var river = new List<Vector2> { V(600, 700), V(250, 300), V(-50, 50), V(-300, -250), V(-700, -650) };
            L.rivers.Add(river); L.riverWidth = 14f;
            L.height = (x, z) =>
            {
                float h = 28f + 30f * N(x, z, 450f, 4, 41f) + 6f * N(x, z, 60f, 2, 3f);
                h += 60f * SS(450f, 780f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
                h -= 14f * (1f - SS(25f, 140f, DistToPolyline(V(x, z), river)));
                return h;
            };
            L.special = (x, z, y, slope) => SS(0.15f, 0.5f, N(x, z, 50f, 3, 9f)) * 0.6f;
            L.tex = new LayoutTextures { baseNear = "forestfloor", baseFar = "camp:gnd_near", path = "camp:gnd_path", field = "mossyrock", rock = "mossyrock", baseTint = new Color(0.85f, 0.95f, 0.75f), farTint = new Color(0.65f, 0.85f, 0.55f), nearTile = 3.5f };
            L.veg = new VegSpec { trees = 0.9f, rocks = 0.25f, grass = 1f, treeKinds = new[] { "Tree_Oak", "Tree_Oak2", "Tree_Oak" }, giantTrees = true, density = (x, z) => 0.8f };
            L.settlements.Add(new SettlementSpec("Aldeia Suspensa de Orvalume", SettlementStyle.AldeiaArvore, V(-150, 180), 55f, 10) { exits = new[] { 90f, 270f, 180f } });
            L.settlements.Add(new SettlementSpec("Clareira dos Cultivadores", SettlementStyle.Acampamento, V(320, -260), 38f, 0) { exits = new[] { 300f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.ArvoreCatedral, "Árvore-Catedral do Norte", V(-380, 460), 1.2f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.ArvoreCatedral, "Árvore-Catedral do Rio", V(160, 260), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.ArvoreCatedral, "Raiz-Mãe", V(-460, -300), 1.35f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.ArvoreCatedral, "Árvore do Jardim", V(500, 420), 0.9f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cachoeira, "Cachoeira das Raízes", V(570, 670), 1.1f, 221f));
            L.paths.Add(new PathSpec("Trilha viva", PathStyle.Trilha, 4.5f, V(-150, 100), V(-60, -80), V(120, -200), V(320, -260)));
            L.paths.Add(new PathSpec("Ponte de cipós", PathStyle.Ponte, 4f, V(-40, 70), V(-75, 30)));
            L.spawn = V(-60, -80);
            L.zones.Add(new ZoneSpec("or_cultivadores", "Cultivadores que não param de cantar", V(60, 120), 2, 3, "corista_suspenso", "sussurrante"));
            L.zones.Add(new ZoneSpec("or_cacadores", "Caçadores da mata", V(-320, -60), 2, 3, "passante_invertido", "sussurrante_loop"));
            L.zones.Add(new ZoneSpec("or_afinadores", "Afinadores profanos na trilha", V(380, 80), 3, 2, "afinador_profano", "sussurrante"));
            L.arenas.Add(new ArenaSpec { noteId = "fa", pos = V(500, 420), radius = 40f, yaw = 225f });
            L.arenas.Add(new ArenaSpec { miniboss = "Cervo-Raiz", enemyId = "cervo_raiz", pos = V(-380, 380), radius = 26f });   // animal: arena pronta, corpo entra depois
            L.vortices.Add(new VortexSpec { id = "bosque_escuta", pos = V(140, 380), radius = 85f });
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Árvore com Nota de Laço", V(-200, 30), "galhos ligados por uma promessa antiga"));
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Cultivadora de canto", V(310, -240), "", "", "harmonias com o ecossistema"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Muda que não morre", V(560, 360), "nem toda mutação é hostil"));
            L.locked.Add(new LockedSpec { name = "Floresta Funda", pos = V(-640, 600), yaw = 315f, reason = "conteúdo futuro: aldeias móveis" });
            L.dungeonPos = V(-440, -270); L.dungeonYaw = 210f;
            L.checkpoints.AddRange(new[] { V(-60, -60), V(-130, 140), V(300, -230) });
            L.route = new List<Vector2> { V(-60, -80), V(120, -200), V(320, -260) };
            AutoGates(L, V(-60, -80));
            L.woodTint = new Color(0.9f, 0.95f, 0.8f); L.roofTint = new Color(0.8f, 0.95f, 0.75f);
            return L;
        }

        // ================================================================ HELION
        static RegionLayout Helion()
        {
            var L = new RegionLayout { id = RegionId.Helion };
            L.height = (x, z) =>
            {
                float h = 25f + 18f * N(x, z, 500f, 4, 51f);
                float mesa = SS(330f, 250f, Vector2.Distance(V(x, z), V(40, 80)));     // a mesa da cidade (penhascos brancos)
                h += mesa * 48f;
                h += 70f * SS(560f, 790f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))) * Ridge(x, z, 300f, 3, 3f);
                return h;
            };
            L.special = (x, z, y, slope) => slope > 18f ? 0f : SS(60f, 72f, y) * 0.8f;
            L.tex = new LayoutTextures { baseNear = "dryground", baseFar = "camp:gnd_meadow", path = "camp:gnd_path", cobble = "monastery", field = "sand", rock = "whitecliff", baseTint = new Color(0.95f, 0.92f, 0.8f), farTint = new Color(0.92f, 0.9f, 0.76f) };
            L.veg = new VegSpec { trees = 0.15f, rocks = 0.25f, grass = 0.5f, treeKinds = new[] { "Tree_Oak2" } };
            L.settlements.Add(new SettlementSpec("Helion, a Cidade do Meio-Dia", SettlementStyle.Solar, V(40, 80), 85f, 22) { exits = new[] { 180f, 60f, 300f }, walls = true });
            L.settlements.Add(new SettlementSpec("Bairro das Sacadas", SettlementStyle.Solar, V(-380, -300), 45f, 10) { exits = new[] { 30f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.TorreSolar, "Torres Solares", V(260, 260), 1f, 30f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Teatro, "Teatro do Aplauso", V(-200, 300), 1f, 160f));
            L.paths.Add(new PathSpec("Rampa da Mesa", PathStyle.Estrada, 7f, V(40, -400), V(40, -230), V(40, -10)) { lamps = true });
            L.paths.Add(new PathSpec("Avenida do Teatro", PathStyle.Calcada, 7f, V(-20, 150), V(-120, 240), V(-180, 280)));
            L.spawn = V(40, -400);
            L.zones.Add(new ZoneSpec("he_coristas", "Coristas suspensos", V(-120, -150), 3, 3, "corista_suspenso"));
            L.zones.Add(new ZoneSpec("he_afinadores", "Afinadores profanos", V(260, -80), 3, 2, "afinador_profano", "sussurrante"));
            L.zones.Add(new ZoneSpec("he_plateia", "Plateia oca", V(400, 380), 3, 2, "sussurrante_oco", "corista_suspenso"));
            L.arenas.Add(new ArenaSpec { noteId = "sol", pos = V(-200, 300), radius = 34f, yaw = 160f, dressing = LandmarkKind.Teatro });
            L.arenas.Add(new ArenaSpec { miniboss = "Ídolo de Vidro", enemyId = "idolo_vidro", pos = V(260, 140), radius = 22f });
            L.vortices.Add(new VortexSpec { id = "teatro_aplauso", pos = V(-200, 300), radius = 70f });
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Estátua com Nota de Farol", V(80, 30), "acende quando ninguém a observa"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Bastidor esquecido", V(-260, 360), "cartazes de heróis que nunca existiram"));
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Pregoeira da resistência", V(-360, -280), "", "", "símbolos sem culto à imagem"));
            L.locked.Add(new LockedSpec { name = "Palácio da Reputação", pos = V(120, 200), yaw = 0f, reason = "conteúdo futuro: política de Helion" });
            L.dungeonPos = V(-150, 250); L.dungeonYaw = 160f;
            L.checkpoints.AddRange(new[] { V(40, -360), V(40, 20), V(-140, 220) });
            L.route = new List<Vector2> { V(40, -400), V(40, -230), V(40, -10) };
            AutoGates(L, V(40, -400));
            L.stoneTint = new Color(1.18f, 1.08f, 0.9f); L.roofTint = new Color(1.4f, 1.08f, 0.5f);
            return L;
        }

        // ================================================================ SEFRA
        static RegionLayout Sefra()
        {
            var L = new RegionLayout { id = RegionId.Sefra, seaLevel = 2f };
            L.height = (x, z) =>
            {
                float h = 18f + 26f * N(x, z, 420f, 4, 61f) + 40f * SS(-250f, 300f, z);
                h -= 30f * SS(-300f, -600f, z);                                     // porto ao sul
                h += Bump(x, z, 0f, 150f, 260f, 18f);
                return h;
            };
            L.special = (x, z, y, slope) => y < 5f ? 0.9f : 0f;
            L.tex = new LayoutTextures { baseNear = "mudleaves", baseFar = "camp:gnd_meadow", path = "camp:gnd_path", cobble = "monastery", field = "sand", rock = "darkrock", baseTint = new Color(0.7f, 0.68f, 0.78f), farTint = new Color(0.65f, 0.62f, 0.75f) };
            L.veg = new VegSpec { trees = 0.12f, rocks = 0.15f, grass = 0.4f, treeKinds = new[] { "Tree_Oak2", "Tree_Pine" } };
            L.windowGlow = true;
            L.settlements.Add(new SettlementSpec("Sefra, a Cidade que Não Dorme", SettlementStyle.Noturna, V(0, 150), 90f, 24) { exits = new[] { 180f, 90f, 270f, 0f } });
            L.settlements.Add(new SettlementSpec("Porto das Lanternas", SettlementStyle.Noturna, V(150, -430), 50f, 10) { exits = new[] { 0f, 180f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.MercadoNoturno, "Mercado Noturno", V(-260, 40), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Farol, "Farol do porto", V(330, -560), 0.8f));
            L.paths.Add(new PathSpec("Escadaria do Porto", PathStyle.Calcada, 6f, V(150, -380), V(100, -200), V(20, 50)) { lamps = true });
            L.paths.Add(new PathSpec("Rua dos Sonhos", PathStyle.Calcada, 6f, V(-80, 140), V(-200, 70), V(-260, 40)) { lamps = true });
            L.spawn = V(150, -380); L.spawnYaw = 0f;
            L.zones.Add(new ZoneSpec("se_peregrinos", "Peregrinos de Estouro", V(-200, -150), 3, 3, "peregrino_estouro"));
            L.zones.Add(new ZoneSpec("se_partidos", "Partidos em Dois", V(260, 120), 3, 2, "partido_em_dois"));
            L.zones.Add(new ZoneSpec("se_estivadores", "Estivadores do porto", V(300, -330), 3, 2, "portador_estouro", "confessor_sem_eco"));
            L.arenas.Add(new ArenaSpec { noteId = "la", pos = V(60, 380), radius = 36f, yaw = 180f });
            L.arenas.Add(new ArenaSpec { miniboss = "Colecionador de Promessas", enemyId = "colecionador", pos = V(-340, 260), radius = 22f });
            L.vortices.Add(new VortexSpec { id = "mercado_desejo", pos = V(-260, 40), radius = 60f });
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Contrato com Nota de Juramento", V(-60, 200), "quebrar a promessa tem consequência definida"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Sonho vendido de alguém", V(-300, -20), "a memória futura de uma criança"));
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Mercadora de sonhos", V(-240, 60), "", "", "vende experiências — e cobra depois"));
            L.locked.Add(new LockedSpec { name = "Bairro dos Desejos Herdados", pos = V(-500, 380), yaw = 300f, reason = "conteúdo futuro: side quests de desejo" });
            L.dungeonPos = V(220, 260); L.dungeonYaw = 60f;
            L.checkpoints.AddRange(new[] { V(150, -350), V(40, 60), V(-220, 20) });
            L.route = new List<Vector2> { V(150, -380), V(100, -200), V(20, 50) };
            AutoGates(L, V(20, 50));
            L.stoneTint = new Color(0.72f, 0.7f, 0.82f); L.roofTint = new Color(0.65f, 0.45f, 0.75f); L.woodTint = new Color(0.75f, 0.6f, 0.75f);
            return L;
        }

        // ================================================================ NERETH
        static RegionLayout Nereth()
        {
            var L = new RegionLayout { id = RegionId.Nereth, seaLevel = 0f };
            L.height = (x, z) =>
            {
                float h = 60f + 25f * N(x, z, 380f, 4, 71f);
                h += 48f * SS(80f, 140f, z);                                        // degrau de penhasco: terraço baixo → planalto alto
                h -= 110f * SS(-380f, -560f, z);                                    // penhascos sobre o mar escuro
                h += 30f * Ridge(x, z, 200f, 3, 4f) * SS(400f, 700f, Mathf.Abs(x));
                return h;
            };
            L.special = (x, z, y, slope) => SS(0.2f, 0.5f, N(x, z, 40f, 3, 6f)) * 0.7f;
            L.tex = new LayoutTextures { baseNear = "withered", baseFar = "mudleaves", path = "camp:gnd_path", cobble = "monastery", field = "mudleaves", rock = "darkrock", baseTint = new Color(0.62f, 0.66f, 0.66f), farTint = new Color(0.55f, 0.6f, 0.6f) };
            L.veg = new VegSpec { trees = 0.3f, rocks = 0.35f, grass = 0.35f, deadTrees = true, treeKinds = new[] { "Tree_Pine" } };
            L.windowGlow = true;
            L.settlements.Add(new SettlementSpec("Mosteiro do Último Toque", SettlementStyle.Mosteiro, V(-250, 330), 60f, 8) { exits = new[] { 180f, 90f }, walls = true });
            L.settlements.Add(new SettlementSpec("Vila dos Enlutados", SettlementStyle.Gotica, V(220, -150), 50f, 12) { exits = new[] { 0f, 270f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cemiterio, "Terraços das Despedidas", V(60, -60), 1f, 10f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Escadaria, "Escadaria Depois do Fim", V(-20, 40), 1f, 0f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Mosteiro, "Claustro do Último Toque", V(-250, 330), 1f));
            L.paths.Add(new PathSpec("Caminho dos Enlutados", PathStyle.Trilha, 4.5f, V(220, -150), V(120, -80), V(30, 10), V(-20, 30)));
            L.spawn = V(220, -200);
            L.zones.Add(new ZoneSpec("ne_ocos", "Sussurrantes ocos", V(150, 30), 4, 3, "sussurrante_oco"));
            L.zones.Add(new ZoneSpec("ne_enlutados", "Enlutados que não partem", V(-350, -120), 4, 2, "sussurrante_oco", "confessor_sem_eco"));
            L.zones.Add(new ZoneSpec("ne_vozes", "Vozes de Vharos", V(-120, 260), 4, 2, "voz_vharos", "sussurrante_oco"));
            L.arenas.Add(new ArenaSpec { noteId = "si", pos = V(-20, 200), radius = 32f, yaw = 0f });
            L.arenas.Add(new ArenaSpec { miniboss = "Monge que Não Termina", enemyId = "monge", pos = V(-250, 230), radius = 20f });
            L.vortices.Add(new VortexSpec { id = "escadaria_fim", pos = V(-20, 120), radius = 70f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Sino Mudo", V(-250, 345), "", "sino"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Sibil, o monge sem voz", V(-235, 340), "abriu mão da voz para estudar o silêncio sem domínio"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Túmulo com Nota de Sono", V(80, -40), "mantém um morto em baixa resposta"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Carta de despedida nunca entregue", V(260, -110), "o fim que uma família não quer"));
            L.locked.Add(new LockedSpec { name = "Penhasco dos Mortos que Não Partem", pos = V(450, 420), yaw = 45f, reason = "conteúdo futuro: Ato IV" });
            L.dungeonPos = V(110, -120); L.dungeonYaw = 160f;
            L.checkpoints.AddRange(new[] { V(220, -170), V(30, 0), V(-230, 270) });
            L.route = new List<Vector2> { V(220, -200), V(120, -80), V(30, 10) };
            AutoGates(L, V(120, -80));
            L.stoneTint = new Color(0.78f, 0.8f, 0.84f); L.roofTint = new Color(0.6f, 0.65f, 0.72f);
            return L;
        }

        // ================================================================ GRANITH
        static RegionLayout Granith()
        {
            var L = new RegionLayout { id = RegionId.Granith };
            L.height = (x, z) =>
            {
                float pass = Mathf.Abs(x - 60f * N(0, z, 400f, 2, 5f));             // o passo norte-sul entre as montanhas
                float h = 120f + 30f * N(x, z, 300f, 3, 81f);
                h += 300f * SS(90f, 420f, pass) * (0.5f + 0.5f * Ridge(x, z, 350f, 5, 2f));
                return h;
            };
            L.special = (x, z, y, slope) => SS(135f, 185f, y + 12f * N(x, z, 60f, 2, 3f)) * (slope < 38f ? 1f : 0.45f);   // neve (prancha: Granith nevado)
            L.tex = new LayoutTextures { baseNear = "camp:gnd_near", baseFar = "camp:gnd_meadow", path = "camp:gnd_path", cobble = "monastery", field = "snow", rock = "darkrock", baseTint = new Color(0.72f, 0.8f, 0.72f), farTint = new Color(0.7f, 0.76f, 0.7f) };
            L.veg = new VegSpec { trees = 0.4f, rocks = 0.6f, grass = 0.4f, treeKinds = new[] { "Tree_Pine" } };
            L.windowGlow = false;
            L.settlements.Add(new SettlementSpec("Cidadela de Granith", SettlementStyle.Fortaleza, V(40, 120), 75f, 16) { exits = new[] { 0f, 180f }, walls = true });
            L.settlements.Add(new SettlementSpec("Pedreira Alta", SettlementStyle.Acampamento, V(-60, -450), 40f, 0) { exits = new[] { 0f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Muralha, "Muralha Cantada", V(0, -180), 1f, 90f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cidadela, "Salão do Conselho de Pedra", V(40, 120), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Pedreira, "Pedreira Afinada", V(-140, -470), 1f, 0f));
            L.paths.Add(new PathSpec("Estrada do Passo", PathStyle.Estrada, 7f, V(0, -650), V(-30, -450), V(0, -180), V(40, 40)) { lamps = true });
            L.spawn = V(0, -650);
            L.zones.Add(new ZoneSpec("gr_pedreiros", "Pedreiros de carga", V(-80, -330), 3, 3, "peregrino_estouro", "portador_estouro"));
            L.zones.Add(new ZoneSpec("gr_guardas", "Guardas da muralha", V(120, -60), 3, 2, "cantor_corrente", "sussurrante"));
            L.zones.Add(new ZoneSpec("gr_cantores", "Cantores de corrente", V(60, 260), 3, 2, "cantor_corrente", "regente_desfeito"));
            L.arenas.Add(new ArenaSpec { miniboss = "Colosso de Pedra Oca (gigante vivo)", enemyId = "colosso", pos = V(-30, 400), radius = 34f });
            L.arenas.Add(new ArenaSpec { miniboss = "Mestre de Muralha emudecido", enemyId = "mestre_muralha", pos = V(70, -230), radius = 22f });
            L.vortices.Add(new VortexSpec { id = "coro_sem_cantores", pos = V(0, -180), radius = 65f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Nó de Basalto", V(40, 150), "", "no"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Casa de Granith (conselho)", V(60, 140), "a relíquia é guardada por um conselho — disputa sucessória"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Portão com Nota de Juramento", V(0, -160), "só abre para quem jurou a muralha"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Ninho de águias de pedra", V(260, 380), "uma voz presa num bloco afinado"));
            L.locked.Add(new LockedSpec { name = "Picos Altos", pos = V(-420, 520), yaw = 330f, reason = "conteúdo futuro: fortalezas dos picos" });
            L.dungeonPos = V(-170, -430); L.dungeonYaw = 270f;
            L.checkpoints.AddRange(new[] { V(0, -620), V(-30, -420), V(20, -120), V(40, 40) });
            L.route = new List<Vector2> { V(0, -650), V(-30, -450), V(0, -180), V(40, 40) };
            AutoGates(L, V(-30, -450));
            L.stoneTint = new Color(0.75f, 0.78f, 0.84f); L.roofTint = new Color(0.62f, 0.66f, 0.74f);
            return L;
        }

        // ================================================================ COROA DE CINZA
        static RegionLayout Coroa()
        {
            var L = new RegionLayout { id = RegionId.CoroaDeCinza, lavaRivers = true, riverWidth = 8f };
            // rios de lava descendo do vulcão (prancha: lava viva) — sem cruzar estradas nem arenas
            L.rivers.Add(new List<Vector2> { V(430, 420), V(380, 300), V(420, 150), V(520, 20), V(650, -120), V(760, -260) });
            L.rivers.Add(new List<Vector2> { V(-400, 300), V(-450, 100), V(-550, -100), V(-660, -300) });
            L.height = (x, z) =>
            {
                float h = 40f + 30f * N(x, z, 350f, 4, 91f) + 10f * Ridge(x, z, 80f, 3, 2f);
                h += Crater(x, z, -120f, -60f, 190f, 30f, 25f);                      // a caldeira onde fica a cidade
                h += 60f * SS(560f, 790f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
                return h;
            };
            L.special = (x, z, y, slope) => SS(-0.2f, 0.3f, N(x, z, 70f, 3, 4f));
            L.tex = new LayoutTextures { baseNear = "ash", baseFar = "ash", path = "cracked", cobble = "darkrock", field = "darkrock", rock = "darkrock", baseTint = new Color(0.62f, 0.57f, 0.55f), farTint = new Color(0.55f, 0.5f, 0.48f), dryTint = new Color(0.9f, 0.85f, 0.8f), nearTile = 4f };
            L.veg = new VegSpec { trees = 0.08f, rocks = 0.55f, grass = 0f, deadTrees = true, crystals = 0.08f, crystalColor = new Color(1f, 0.45f, 0.2f) };
            L.windowGlow = true;
            L.settlements.Add(new SettlementSpec("Cidade-Cratera de Forja Baixa", SettlementStyle.Forja, V(-120, -60), 75f, 18) { exits = new[] { 90f, 270f, 0f } });
            L.settlements.Add(new SettlementSpec("Acampamento dos Carvoeiros", SettlementStyle.Acampamento, V(380, -420), 38f, 0) { exits = new[] { 300f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Vulcao, "Vulcão da Coroa", V(450, 470), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Forja, "Forja do Último Golpe", V(-110, 120), 1f, 180f));
            L.paths.Add(new PathSpec("Estrada da Cinza", PathStyle.Estrada, 7f, V(380, -420), V(200, -250), V(40, -80), V(-40, -60)) { lamps = true });
            L.spawn = V(380, -460);
            L.zones.Add(new ZoneSpec("co_portadores", "Portadores de Estouro", V(120, -170), 4, 3, "portador_estouro"));
            L.zones.Add(new ZoneSpec("co_carvoeiros", "Carvoeiros de impacto", V(-350, -300), 4, 2, "peregrino_estouro"));
            L.zones.Add(new ZoneSpec("co_encosta", "Encosta do vulcão (servos de Vharos)", V(300, 250), 4, 3, "afinador_profano", "portador_estouro"));
            L.arenas.Add(new ArenaSpec { miniboss = "Ferreiro de Estouro", enemyId = "ferreiro", pos = V(-110, 190), radius = 24f });
            L.arenas.Add(new ArenaSpec { miniboss = "Portador do Braseiro (corrompido)", enemyId = "portador_braseiro", pos = V(300, 330), radius = 26f });
            L.vortices.Add(new VortexSpec { id = "forja_ultimo", pos = V(-110, 120), radius = 60f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Braseiro da Brasa-Mãe", V(-140, -40), "", "braseiro"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Bront da Forja Baixa", V(-125, -30), "construto de bronze e madeira mantido por ferreiros"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Bigorna com Nota de Marca", V(-80, -110), "registra o dono de cada arma forjada"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Veio de metal vivo", V(420, 300), "metal ressonante que responde à brasa"));
            L.locked.Add(new LockedSpec { name = "Garganta Muda (rumo à Fronteira)", pos = V(-500, 560), yaw = 330f, reason = "abre depois de Si Infinito" });
            L.dungeonPos = V(-60, 140); L.dungeonYaw = 90f;
            L.checkpoints.AddRange(new[] { V(380, -430), V(160, -220), V(-60, -70) });
            L.route = new List<Vector2> { V(380, -420), V(200, -250), V(40, -80) };
            AutoGates(L, V(200, -250));
            L.stoneTint = new Color(0.55f, 0.5f, 0.5f); L.roofTint = new Color(0.5f, 0.42f, 0.4f); L.woodTint = new Color(0.6f, 0.5f, 0.45f);
            return L;
        }

        // ================================================================ MAR DE VIDRO
        static RegionLayout MarDeVidro()
        {
            var L = new RegionLayout { id = RegionId.MarDeVidro, seaLevel = 2f, glassSea = true };
            L.height = (x, z) =>
            {
                float h = -6f + 4f * N(x, z, 300f, 3, 101f);
                h += Bump(x, z, -200f, -150f, 170f, 26f) + Bump(x, z, 260f, 210f, 120f, 20f) + Bump(x, z, -380f, 340f, 90f, 14f) + Bump(x, z, 420f, -380f, 80f, 12f);
                return h;
            };
            L.special = (x, z, y, slope) => y < 5f ? 1f : 0f;
            L.tex = new LayoutTextures { baseNear = "camp:gnd_near", baseFar = "camp:gnd_meadow", path = "sand", cobble = "monastery", field = "sand", rock = "whitecliff", baseTint = new Color(0.8f, 0.9f, 0.8f) };
            L.veg = new VegSpec { trees = 0.2f, rocks = 0.2f, grass = 0.5f, treeKinds = new[] { "Tree_Pine", "Tree_Oak2" }, crystals = 0.15f, crystalColor = new Color(0.4f, 1f, 0.95f) };
            L.windowGlow = false;
            L.settlements.Add(new SettlementSpec("Ilha do Farol Velho", SettlementStyle.Ilha, V(-200, -150), 55f, 12) { exits = new[] { 90f, 0f, 200f } });
            L.settlements.Add(new SettlementSpec("Vila dos Cantores de Proa", SettlementStyle.Ilha, V(260, 210), 40f, 8) { exits = new[] { 230f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Farol, "Farol Velho", V(-290, -220), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.FarolSubmerso, "Farol Submerso", V(80, -420), 1f, 30f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Recife, "Recifes de vidro", V(-20, 120), 1.4f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.NaviosPresos, "Frota presa no vidro", V(380, -120), 1.2f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.NaviosPresos, "Navios do canal", V(-420, 80), 1f));
            L.paths.Add(new PathSpec("Trilha sobre o mar cristalizado", PathStyle.Trilha, 5f, V(-130, -120), V(0, 0), V(120, 120), V(220, 190)));
            L.spawn = V(-150, -220);
            L.zones.Add(new ZoneSpec("ma_partidos", "Reflexos partidos", V(40, 40), 3, 2, "partido_em_dois"));
            L.zones.Add(new ZoneSpec("ma_marujos", "Marujos partidos", V(160, -260), 3, 3, "partido_em_dois", "sussurrante_loop"));
            L.zones.Add(new ZoneSpec("ma_copistas", "Copistas no vidro", V(-320, 260), 3, 2, "copista_fenda"));
            L.arenas.Add(new ArenaSpec { miniboss = "Náufrago Prismático", enemyId = "naufrago", pos = V(60, -330), radius = 30f });
            L.vortices.Add(new VortexSpec { id = "farol_submerso", pos = V(80, -420), radius = 75f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Ampulheta das Marés", V(110, -400), "", "ampulheta"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Tessara Venn, cronista e navegadora", V(-190, -120), "mede fluxo, tempo percebido e retorno"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Bóia com Nota de Eco", V(-40, 60), "repete a última canção de um navio"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Reflexo que continua andando", V(300, -100), "a superfície devolve a voz de alguém ausente"));
            L.locked.Add(new LockedSpec { name = "Mar aberto (rotas cantadas)", pos = V(-560, -560), yaw = 225f, reason = "conteúdo futuro: navegação" });
            L.dungeonPos = V(40, -440); L.dungeonYaw = 200f;
            L.checkpoints.AddRange(new[] { V(-160, -190), V(0, 0), V(240, 190) });
            L.route = new List<Vector2> { V(-150, -220), V(-130, -120), V(0, 0), V(120, 120) };
            AutoGates(L, V(-130, -120));
            L.stoneTint = new Color(0.95f, 1f, 1f); L.roofTint = new Color(0.7f, 0.9f, 0.95f);
            return L;
        }

        // ================================================================ CALÍRIA
        static RegionLayout Caliria()
        {
            var L = new RegionLayout { id = RegionId.Caliria, seaLevel = 1.6f };
            L.waterDeep = new Color(0.1f, 0.5f, 0.55f, 0.7f); L.waterSky = new Color(0.8f, 0.9f, 0.95f);
            L.height = (x, z) =>
            {
                float h = -1.5f + 2f * N(x, z, 260f, 3, 111f);
                h += Bump(x, z, 0f, 0f, 220f, 16f) + Bump(x, z, 330f, 280f, 110f, 10f) + Bump(x, z, -340f, 260f, 100f, 9f) + Bump(x, z, 300f, -320f, 120f, 9f);
                return h;
            };
            L.special = (x, z, y, slope) => y < 3.5f ? 1f : SS(0.25f, 0.55f, N(x, z, 30f, 2, 7f)) * 0.5f;
            L.tex = new LayoutTextures { baseNear = "camp:gnd_near", baseFar = "camp:gnd_meadow", path = "sand", cobble = "monastery", field = "snow", rock = "whitecliff", baseTint = new Color(0.82f, 0.88f, 0.78f), farTint = new Color(0.8f, 0.86f, 0.78f) };
            L.veg = new VegSpec { trees = 0.15f, rocks = 0.1f, grass = 0.6f, treeKinds = new[] { "Tree_Oak2" } };
            L.settlements.Add(new SettlementSpec("Clínica-Templo do Sal", SettlementStyle.Clinica, V(0, -20), 70f, 14) { exits = new[] { 45f, 180f, 300f } });
            L.settlements.Add(new SettlementSpec("Jardins de Sal", SettlementStyle.Clinica, V(330, 280), 40f, 6) { exits = new[] { 225f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.TemploSal, "Templo da Sutura", V(40, 60), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.JardimSal, "Terraços de sal", V(300, 340), 1f));
            L.paths.Add(new PathSpec("Ponte das ilhas (nordeste)", PathStyle.Ponte, 4.5f, V(150, 140), V(250, 215)));
            L.paths.Add(new PathSpec("Ponte das ilhas (sudeste)", PathStyle.Ponte, 4.5f, V(150, -180), V(230, -250)));
            L.spawn = V(0, -150);
            L.zones.Add(new ZoneSpec("ca_coro", "Coro sem cicatrizes", V(-120, 60), 2, 3, "sussurrante", "morador_sem_palavra"));
            L.zones.Add(new ZoneSpec("ca_coristas", "Coristas suspensos", V(300, -300), 3, 2, "corista_suspenso"));
            L.arenas.Add(new ArenaSpec { miniboss = "Coro da Carne Perfeita", enemyId = "coro_carne", pos = V(60, 140), radius = 26f });
            L.arenas.Add(new ArenaSpec { miniboss = "Guardião do Cálice", enemyId = "guardiao_calice", pos = V(-320, 260), radius = 22f });
            L.vortices.Add(new VortexSpec { id = "carne_perfeita", pos = V(40, 60), radius = 60f });
            L.pois.Add(new PoiSpec(PoiKind.Reliquia, "Cálice de Sutura", V(40, 40), "", "calice"));
            L.pois.Add(new PoiSpec(PoiKind.Custodio, "Lumen Aris, curadora itinerante", V(-30, -60), "acredita que cura deve preservar autonomia"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Leito com Nota de Pulso", V(20, -40), "estabiliza quem chega em choque"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Diário de uma curadora", V(-80, -110), "quando a cura vira imposição?"));
            L.locked.Add(new LockedSpec { name = "Ilhas de quarentena", pos = V(-520, -520), yaw = 225f, reason = "conteúdo futuro: debate ético da cura" });
            L.dungeonPos = V(-60, 40); L.dungeonYaw = 300f;
            L.checkpoints.AddRange(new[] { V(0, -130), V(80, 30), V(300, 260) });
            L.route = new List<Vector2> { V(0, -150), V(0, -60), V(30, 20) };
            AutoGates(L, V(0, -120));
            L.stoneTint = new Color(1.12f, 1.12f, 1.1f); L.roofTint = new Color(0.85f, 0.95f, 1.05f); L.woodTint = new Color(1.05f, 1.05f, 1f);
            return L;
        }

        // ================================================================ SOMBRAFONTE
        static RegionLayout Sombrafonte()
        {
            var L = new RegionLayout { id = RegionId.Sombrafonte, size = 1300f, underground = true, horizonHeight = 0f };
            L.height = (x, z) =>
            {
                float h = 10f + 14f * N(x, z, 200f, 4, 121f) + 8f * Ridge(x, z, 70f, 3, 4f);
                h -= 35f * (1f - SS(10f, 40f, Mathf.Abs(x + 150f - 60f * N(0, z, 150f, 2, 1f)))) * SS(-400f, -100f, z);   // fenda/abismo
                h += 40f * SS(480f, 640f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
                return h;
            };
            L.special = (x, z, y, slope) => SS(0.1f, 0.5f, N(x, z, 35f, 3, 9f));
            L.tex = new LayoutTextures { baseNear = "darkrock", baseFar = "darkrock", path = "camp:gnd_path", cobble = "monastery", field = "mossyrock", rock = "darkrock", baseTint = new Color(0.5f, 0.55f, 0.58f), farTint = new Color(0.45f, 0.5f, 0.55f), nearTile = 4f, farTile = 18f };
            L.veg = new VegSpec { trees = 0f, rocks = 0.6f, grass = 0f, crystals = 0.45f, crystalColor = new Color(0.4f, 0.9f, 1f) };
            L.windowGlow = true;
            L.settlements.Add(new SettlementSpec("Cidade Baixa de Sombrafonte", SettlementStyle.Caverna, V(80, 80), 65f, 14) { exits = new[] { 180f, 60f, 300f } });
            L.settlements.Add(new SettlementSpec("Posto dos Mineradores", SettlementStyle.Acampamento, V(-300, 300), 32f, 0) { exits = new[] { 120f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CavernaCupula, "Caverna das Mil Vozes", V(0, 0), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cristais, "Cristais de Eco gigantes", V(260, -200), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Cristais, "Coração de cristal", V(-120, 380), 0.8f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Mina, "Mina de Mil Respostas", V(-380, 330), 1f, 45f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CascataLuminosa, "Cascata do Eco Azul", V(300, 100), 1f, 250f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CascataLuminosa, "Cascata das Vozes", V(-210, -160), 0.9f, 60f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.CascataLuminosa, "Queda do Coração", V(-40, 330), 0.8f, 180f));
            L.paths.Add(new PathSpec("Trilho dos mineradores", PathStyle.Trilha, 4.5f, V(80, -200), V(80, -30), V(-120, 180), V(-300, 300)));
            L.spawn = V(80, -220);
            L.zones.Add(new ZoneSpec("so_ocos", "Ecos ocos", V(-60, -80), 3, 3, "sussurrante_oco"));
            L.zones.Add(new ZoneSpec("so_arquivistas", "Arquivistas sem eco", V(220, 260), 3, 2, "confessor_sem_eco", "morador_sem_palavra"));
            L.zones.Add(new ZoneSpec("so_mineradores", "Mineradores perdidos", V(-260, 80), 3, 2, "sussurrante_oco", "sussurrante_loop"));
            L.arenas.Add(new ArenaSpec { miniboss = "Oráculo Repetido", enemyId = "oraculo", pos = V(260, -260), radius = 26f });
            L.vortices.Add(new VortexSpec { id = "mil_respostas", pos = V(-380, 330), radius = 60f });
            L.pois.Add(new PoiSpec(PoiKind.Marco, "Inscrição do Nome Ausente", V(-120, 360), "versões contraditórias da Guerra do Contracanto — e uma lacuna onde caberia um nome"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Rastro do Prisma do Encanto", V(240, -180), "o Prisma passou por aqui em algum momento"));
            L.pois.Add(new PoiSpec(PoiKind.Encantamento, "Cristal com Nota de Véu", V(100, 140), "esconde a assinatura de quem passa"));
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Arquivista das cavernas", V(70, 60), "", "", "guarda ecos que duram dias"));
            L.locked.Add(new LockedSpec { name = "Galerias que repetem para sempre", pos = V(420, 420), yaw = 45f, reason = "conteúdo futuro: mistério do Nome de Aren" });
            L.dungeonPos = V(-400, 350); L.dungeonYaw = 45f;
            L.checkpoints.AddRange(new[] { V(80, -190), V(80, 0), V(-280, 280) });
            L.route = new List<Vector2> { V(80, -220), V(80, -30), V(-120, 180) };
            AutoGates(L, V(80, -120));
            L.stoneTint = new Color(0.7f, 0.75f, 0.8f); L.roofTint = new Color(0.55f, 0.65f, 0.72f);
            return L;
        }

        // ================================================================ FRONTEIRA MUDA
        static RegionLayout Fronteira()
        {
            var L = new RegionLayout { id = RegionId.FronteiraMuda };
            L.height = (x, z) =>
            {
                float h = 30f + 22f * N(x, z, 400f, 4, 131f) + 16f * Ridge(x, z, 90f, 3, 7f);
                h -= 25f * (1f - SS(6f, 30f, Mathf.Abs(N(x, z, 260f, 2, 3f) * 300f)));   // rachaduras no chão
                return h;
            };
            L.special = (x, z, y, slope) => 0.6f + 0.4f * SS(-0.3f, 0.3f, N(x, z, 60f, 2, 1f));
            L.tex = new LayoutTextures { baseNear = "withered", baseFar = "cracked", path = "cracked", cobble = "darkrock", field = "cracked", rock = "darkrock", baseTint = new Color(0.62f, 0.62f, 0.62f), farTint = new Color(0.58f, 0.58f, 0.6f), dryTint = new Color(0.85f, 0.85f, 0.85f) };
            L.veg = new VegSpec { trees = 0.15f, rocks = 0.5f, grass = 0.1f, deadTrees = true };
            L.settlements.Add(new SettlementSpec("Acampamento dos Sinais", SettlementStyle.Acampamento, V(0, -350), 42f, 0) { exits = new[] { 0f, 180f } });
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.FendaRasgo, "A Fenda do Contracanto", V(40, 760), 1f, 180f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.RochasFlutuantes, "Terra sem peso", V(-250, 200), 1.2f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.Ruina, "Ruínas da Guerra do Contracanto", V(260, 50), 1.5f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.TorresEspinhosas, "Espinhos do Vazio (oeste)", V(-170, 520), 1f));
            L.landmarks.Add(new LandmarkSpec(LandmarkKind.TorresEspinhosas, "Espinhos do Vazio (leste)", V(260, 470), 0.9f));
            L.paths.Add(new PathSpec("Trilha dos sinais", PathStyle.Trilha, 4f, V(0, -560), V(0, -350), V(60, -100), V(40, 250)));
            L.spawn = V(0, -560);
            L.zones.Add(new ZoneSpec("fr_vozes", "Vozes de Vharos", V(80, -150), 5, 3, "voz_vharos"));
            L.zones.Add(new ZoneSpec("fr_soldados", "Soldados partidos da Guerra", V(-250, -50), 5, 2, "partido_em_dois", "voz_vharos"));
            L.zones.Add(new ZoneSpec("fr_sobreviventes", "Sobreviventes que pararam de responder", V(250, 250), 5, 3, "sussurrante_oco"));
            L.arenas.Add(new ArenaSpec { miniboss = "A Cicatriz — Elyan Vharos, Regente do Contracanto (final)", enemyId = "vharos", pos = V(40, 420), radius = 50f, requires = "nota:si" });
            L.arenas.Add(new ArenaSpec { miniboss = "Voz de Vharos", enemyId = "voz_vharos_elite", pos = V(-200, 120), radius = 24f });
            L.vortices.Add(new VortexSpec { id = "ausencia", pos = V(40, 200), radius = 140f });
            L.pois.Add(new PoiSpec(PoiKind.NPC, "Sobrevivente que fala por sinais", V(20, -330), "", "", "sinais visuais — a voz já não serve aqui"));
            L.pois.Add(new PoiSpec(PoiKind.Segredo, "Partitura da Composição final", V(-300, 220), "a resposta de Aren não será um discurso"));
            L.locked.Add(new LockedSpec { name = "Dentro da Fenda", pos = V(40, 560), yaw = 0f, width = 40f, reason = "Ato V — o Vazio Mudo" });
            L.dungeonPos = V(150, 300); L.dungeonYaw = 20f;
            L.checkpoints.AddRange(new[] { V(0, -530), V(20, -320), V(60, -80) });
            L.route = new List<Vector2> { V(0, -560), V(0, -350), V(60, -100) };
            AutoGates(L, V(0, -400));
            L.stoneTint = new Color(0.6f, 0.6f, 0.62f); L.roofTint = new Color(0.55f, 0.55f, 0.58f);
            return L;
        }
    }
}

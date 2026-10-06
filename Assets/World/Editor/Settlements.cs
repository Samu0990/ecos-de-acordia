using System.Collections.Generic;
using Elyndra.World;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Cidades e vilas: praça com o elemento do estilo, casas em anéis viradas para a praça (deixando as
    /// ruas das saídas livres), bairros com pontos de NPC (mercado, templo, oficina, taverna, residências,
    /// quadro de missões, loja), arcos de entrada, muralha quando pedida, e as peças que dão o jeito de cada
    /// cultura (cristais de Miralume, cúpulas e discos de Helion, lanternas de Sefra, chaminés da Coroa…).
    /// </summary>
    public static class Settlements
    {
        static System.Random rng;
        static float R01() => (float)rng.NextDouble();
        static float RR(float a, float b) => a + (b - a) * R01();
        static Vector3 G(Vector2 p, float lift = 0f) => RegionBuilder.G(p, lift);
        static Vector2 Dir(float a) => RegionBuilder.Dir(a);
        static float Yaw(Vector2 from, Vector2 to) { var d = to - from; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

        static string[] HouseSet(SettlementStyle s) => s switch
        {
            SettlementStyle.Fortaleza => new[] { "GHouse_B", "GHouse_D", "GTower_A", "GHouse_C" },
            SettlementStyle.Mosteiro => new[] { "GHouse_C", "GHouse_D", "GHouse_A" },
            SettlementStyle.Acampamento or SettlementStyle.Caravana => new string[0],
            SettlementStyle.AldeiaArvore => new[] { "House_A", "House_B", "House_C", "House_D" },
            SettlementStyle.Ilha => new[] { "House_A", "House_B", "GHouse_A", "House_C" },
            SettlementStyle.Clinica => new[] { "GHouse_A", "GHouse_C", "House_B", "GHouse_B" },
            _ => new[] { "GHouse_A", "GHouse_B", "GHouse_C", "GHouse_D", "GHouse_A", "GHouse_B" },
        };

        static bool OnStreet(SettlementSpec s, float ang, float tol) { foreach (var e in s.exits) if (Mathf.Abs(Mathf.DeltaAngle(ang, e)) < tol) return true; return false; }

        public static void Build(SettlementSpec s, Transform parent, System.Random r)
        {
            rng = r;
            var lantern = WorldMats.Glow(new Color(1.9f, 1.1f, 0.5f), 0.15f, 1.2f);
            // ---- praça
            PlazaFeature(s, parent);
            var plaza = new PoiSpec(PoiKind.Praca, "Praça de " + s.name, s.c, "lugar de encontro, festas e anúncios cantados");
            Gameplay.Poi(plaza, parent);
            // ---- casas em dois anéis
            var set = HouseSet(s.style);
            var used = new List<float>();
            float[] rings = { s.radius * 0.5f, s.radius * 0.8f };
            int placed = 0;
            for (int ri = 0; ri < rings.Length && set.Length > 0; ri++)
            {
                float rad = rings[ri];
                int n = Mathf.Min(Mathf.FloorToInt(2 * Mathf.PI * rad / 17f), ri == 0 ? s.houses / 2 : s.houses - placed);
                for (int i = 0; i < n; i++)
                {
                    float ang = i * 360f / Mathf.Max(1, n) + ri * 13f + RR(-4f, 4f);
                    if (OnStreet(s, ang, 9f + 300f / rad)) continue;
                    var p = s.c + Dir(ang) * (rad + RR(-2f, 2f));
                    if (RegionBuilder.NearRoad(p, 6.5f)) continue;   // nenhuma casa em cima de estrada que atravessa a vila
                    string model = set[rng.Next(set.Length)];
                    var h = RegionBuilder.Kit(model, new Vector3(p.x, 0, p.y), Yaw(p, s.c) + RR(-6f, 6f), parent, RR(0.95f, 1.08f));
                    if (h == null) continue;
                    placed++;
                    StyleOnHouse(s, h.transform, p);
                    if (placed % 4 == 1) Gameplay.Poi(new PoiSpec(PoiKind.Residencia, "Casa de moradores", p + (s.c - p).normalized * 7f, "residência"), parent);
                    if (placed % 3 == 0) Gameplay.Poi(new PoiSpec(PoiKind.NPC, "Morador", p + (s.c - p).normalized * 9f + Dir(ang + 90f) * 3f, "", "", Role(s.style, "morador")), parent);
                }
            }
            // ---- bairros
            var free = new List<float>();
            for (float a = 0; a < 360f; a += 45f) if (!OnStreet(s, a, 20f)) free.Add(a);
            int fi = 0;
            float NextAng() => free.Count == 0 ? (fi++ * 90f) : free[(fi++) % free.Count];
            // ângulo livre de estrada para um bairro (a estrada principal pode cruzar a praça)
            float PickAng(float distK, float clearance)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = NextAng();
                    if (!RegionBuilder.NearRoad(s.c + Dir(a) * s.radius * distK, clearance)) return a;
                }
                return NextAng();
            }
            if (s.market)
            {
                float a = PickAng(0.26f, 7f);
                var mc = s.c + Dir(a) * s.radius * 0.26f;
                for (int i = 0; i < 3; i++)
                {
                    var sp = mc + Dir(a + 90f) * (i - 1) * 5.5f;
                    RegionBuilder.Kit(i % 2 == 0 ? "Stall_Red" : "Stall_Blue", new Vector3(sp.x, 0, sp.y), a + 180f, parent, 1f, true, true, RegionBuilder.LayerDetail);
                    if (i != 1) RegionBuilder.Kit(i == 0 ? "Crate" : "Barrel", new Vector3(sp.x + 1.6f, 0, sp.y + 1.2f), RR(0, 360), parent, 1f, true, true, RegionBuilder.LayerDetail);
                }
                Gameplay.Poi(new PoiSpec(PoiKind.Mercado, "Mercado de " + s.name, mc, "bancas, vendedores e pregões cantados"), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.NPC, "Vendedora", mc + Dir(a) * 2.5f, "", "", Role(s.style, "vendedora")), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.Loja, "Loja", mc + Dir(a + 90f) * 9f, "troca de peças, cordas e consertos"), parent);
            }
            if (s.temple)
            {
                float a = PickAng(0.62f, 7f);
                var tc = s.c + Dir(a) * s.radius * 0.62f;
                string tm = s.style == SettlementStyle.Mosteiro ? "BellTower" : s.style == SettlementStyle.Acampamento || s.style == SettlementStyle.Caravana ? null : "GTower_B";
                if (tm != null) RegionBuilder.Kit(tm, new Vector3(tc.x, 0, tc.y), Yaw(tc, s.c), parent, s.style == SettlementStyle.Mosteiro ? 0.8f : 1f);
                Gameplay.Poi(new PoiSpec(PoiKind.Templo, TempleName(s.style), tc + (s.c - tc).normalized * 9f, "coro, ritos e registros cantados"), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.NPC, "Regente do coro", tc + (s.c - tc).normalized * 11f, "", "", Role(s.style, "regente do coro")), parent);
            }
            if (s.workshop)
            {
                float a = PickAng(0.4f, 4f);
                var wc = s.c + Dir(a) * s.radius * 0.4f;
                RegionBuilder.Kit("Cart", new Vector3(wc.x, 0, wc.y), a, parent, 1f, true, true, RegionBuilder.LayerDetail);
                RegionBuilder.Kit("Barrel", new Vector3(wc.x + 2f, 0, wc.y), 0, parent, 1f, true, true, RegionBuilder.LayerDetail);
                Gameplay.Poi(new PoiSpec(PoiKind.Oficina, "Oficina", wc, "reparo de ferramentas — e, escondido, de instrumentos"), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.NPC, "Artesã", wc + Dir(a + 90f) * 2f, "", "", Role(s.style, "artesã")), parent);
            }
            if (s.tavern && set.Length > 0)
            {
                float a = PickAng(0.33f, 8f);
                var tv = s.c + Dir(a) * s.radius * 0.33f;
                RegionBuilder.Kit(s.style == SettlementStyle.AldeiaArvore || s.style == SettlementStyle.Ilha ? "Tavern" : "GTavern", new Vector3(tv.x, 0, tv.y), Yaw(tv, s.c), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.Taverna, "Taverna", tv + (s.c - tv).normalized * 8f, "boatos, canções e trabalho"), parent);
                Gameplay.Poi(new PoiSpec(PoiKind.NPC, "Taverneiro", tv + (s.c - tv).normalized * 9.5f, "", "", "taverneiro"), parent);
            }
            Gameplay.Poi(new PoiSpec(PoiKind.Missao, "Quadro de pedidos", s.c + Dir(NextAng()) * 9f, "missões dos moradores (gancho para side quests)"), parent);
            // ---- entradas (arcos nas ruas) e muralha
            foreach (var e in s.exits)
            {
                var p = s.c + Dir(e) * (s.radius + 6f);
                if (s.style == SettlementStyle.Acampamento) continue;
                RegionBuilder.Object("Entrada de " + s.name, ProcMesh.Arch(6.5f, 7.5f, 1.6f, 1.2f), WorldMats.Stone("camp:ashlar", new Color(0.8f, 0.76f, 0.7f), 2f), G(p), Quaternion.Euler(0, e, 0), Vector3.one, parent);
                foreach (int sg in new[] { -1, 1 })
                    RegionBuilder.Object("Lanterna da entrada", ProcMesh.Sphere(8), lantern, G(p + Dir(e + 90f) * sg * 4.3f, 4.6f), Quaternion.identity, Vector3.one * 0.4f, parent, false, RegionBuilder.LayerDetail);
            }
            if (s.walls)
            {
                float rad = s.radius + 10f;
                int seg = Mathf.FloorToInt(2 * Mathf.PI * rad / 9f);
                for (int i = 0; i < seg; i++)
                {
                    float a = i * 360f / seg;
                    var p = s.c + Dir(a) * rad;
                    if (OnStreet(s, a, 7f) || RegionBuilder.NearRoad(p, 3f)) continue;
                    RegionBuilder.Kit(i % 6 == 0 ? "Wall_Tower" : "Wall_Segment", new Vector3(p.x, -0.2f, p.y), a + 90f, parent);
                }
                foreach (var e in s.exits) { var p = s.c + Dir(e) * (rad + 1f); RegionBuilder.Kit("Gatehouse", new Vector3(p.x, -0.2f, p.y), e, parent); }
            }
            StyleExtras(s, parent);
        }

        static string Role(SettlementStyle s, string basic) => s switch
        {
            SettlementStyle.Clinica => basic == "artesã" ? "curadora (Sutura)" : basic,
            SettlementStyle.Forja => basic == "artesã" ? "ferreira de metal ressonante" : basic,
            SettlementStyle.Vidro => basic == "artesã" ? "arquivista de vozes" : basic,
            SettlementStyle.Mosteiro => basic == "artesã" ? "monge sineiro" : basic,
            SettlementStyle.Noturna => basic == "vendedora" ? "mercadora de sonhos gravados" : basic,
            SettlementStyle.Caravana => basic == "artesã" ? "mestra de caravana" : basic,
            SettlementStyle.Ilha => basic == "artesã" ? "cantora de proa" : basic,
            SettlementStyle.Caverna => basic == "artesã" ? "mineradora de cristais de eco" : basic,
            _ => basic,
        };

        static string TempleName(SettlementStyle s) => s switch
        {
            SettlementStyle.Vidro => "Câmara das Genealogias",
            SettlementStyle.Solar => "Palco do Meio-Dia",
            SettlementStyle.Noturna => "Casa dos Sonhos Guardados",
            SettlementStyle.Mosteiro => "Claustro das Despedidas",
            SettlementStyle.Fortaleza => "Salão do Juramento",
            SettlementStyle.Forja => "Altar da Brasa",
            SettlementStyle.Clinica => "Templo da Sutura",
            SettlementStyle.Caverna => "Gruta do Eco Longo",
            _ => "Templo do Coro",
        };

        static void PlazaFeature(SettlementSpec s, Transform parent)
        {
            var c = s.c;
            switch (s.style)
            {
                case SettlementStyle.Mosteiro: RegionBuilder.Kit("Bell_Pavilion", new Vector3(c.x, 0, c.y), 0, parent); break;
                case SettlementStyle.Vidro:
                    RegionBuilder.Object("Obelisco de vidro (registro cantado)", ProcMesh.Prism(6, 1.4f, 0.4f, 14f), WorldMats.Crystal(new Color(0.55f, 0.65f, 0.75f, 0.55f), new Color(1.2f, 1.4f, 1.6f), new Color(0.4f, 0.5f, 0.7f)), G(c, -0.2f), Quaternion.identity, Vector3.one, parent);
                    break;
                case SettlementStyle.Solar:
                    RegionBuilder.Object("Obelisco solar", ProcMesh.Prism(4, 1.6f, 0.3f, 18f), WorldMats.Stone("whitecliff", new Color(1f, 0.96f, 0.88f), 4f), G(c, -0.2f), Quaternion.Euler(0, 45, 0), Vector3.one, parent);
                    RegionBuilder.Object("Disco de ouro", ProcMesh.Sphere(16), WorldMats.Glow(new Color(2.2f, 1.7f, 0.8f), 0.2f, 2f), G(c, 19f), Quaternion.identity, new Vector3(2.6f, 2.6f, 0.4f), parent, false);
                    break;
                case SettlementStyle.Noturna:
                    RegionBuilder.Object("Árvore de lanternas", ProcMesh.Prism(6, 0.5f, 0.2f, 9f, true, 40f), WorldMats.Stone("camp:timber", new Color(0.35f, 0.28f, 0.35f), 1.5f), G(c), Quaternion.identity, Vector3.one, parent);
                    for (int i = 0; i < 9; i++) RegionBuilder.Object("Lanterna de sonho", ProcMesh.Sphere(8), WorldMats.Glow(i % 2 == 0 ? new Color(1.6f, 0.5f, 1.4f) : new Color(0.5f, 0.9f, 1.8f), 0.3f, 1.4f), G(c + Dir(i * 40f) * 2.5f, 5f + (i % 3) * 1.2f), Quaternion.identity, Vector3.one * 0.5f, parent, false);
                    break;
                case SettlementStyle.Forja:
                    RegionBuilder.Object("Fornalha da praça", ProcMesh.Prism(8, 3f, 2.4f, 2.2f), WorldMats.Stone("darkrock", new Color(0.4f, 0.35f, 0.33f), 2f), G(c), Quaternion.identity, Vector3.one, parent);
                    RegionBuilder.Object("Brasa", ProcMesh.Sphere(10, 0.1f, 3, true), WorldMats.Glow(new Color(2.5f, 0.8f, 0.2f), 0.5f, 1.2f), G(c, 2.1f), Quaternion.identity, new Vector3(2.2f, 1f, 2.2f), parent, false);
                    break;
                case SettlementStyle.Clinica:
                    RegionBuilder.Object("Fonte de sal", ProcMesh.Prism(10, 4f, 4f, 0.8f), WorldMats.Stone("whitecliff", new Color(1f, 1f, 0.98f), 2f), G(c, -0.2f), Quaternion.identity, Vector3.one, parent);
                    break;
                case SettlementStyle.Caverna:
                    RegionBuilder.Object("Cristal-lâmpada", ProcMesh.Crystal(9), WorldMats.Crystal(new Color(0.1f, 0.3f, 0.4f, 0.6f), new Color(0.6f, 1.6f, 1.8f), new Color(0.3f, 0.8f, 1f)), G(c, -0.5f), Quaternion.identity, new Vector3(4f, 10f, 4f), parent);
                    break;
                case SettlementStyle.Acampamento:
                case SettlementStyle.Caravana:
                    RegionBuilder.Object("Fogueira", ProcMesh.Sphere(8, 0.2f, 2, true), WorldMats.Glow(new Color(2.2f, 1f, 0.35f), 0.8f, 1.5f), G(c), Quaternion.identity, new Vector3(1.2f, 0.8f, 1.2f), parent, false);
                    break;
                case SettlementStyle.AldeiaArvore:
                    RegionBuilder.Object("Tronco-mãe da aldeia", ProcMesh.Prism(9, 4.5f, 2.6f, 28f, true, 35f), WorldMats.Stone("camp:timber", new Color(0.5f, 0.42f, 0.35f), 2.5f), G(c, -1f), Quaternion.identity, Vector3.one, parent);
                    break;
                default: RegionBuilder.Kit("Well", new Vector3(c.x, 0, c.y), 0, parent); break;
            }
        }

        static void StyleOnHouse(SettlementSpec s, Transform h, Vector2 p)
        {
            switch (s.style)
            {
                case SettlementStyle.Vidro:
                    if (R01() < 0.6f) RegionBuilder.Object("Espigão de vidro", ProcMesh.Crystal(rng.Next(0, 12)), WorldMats.Crystal(new Color(0.55f, 0.65f, 0.75f, 0.5f), new Color(1.2f, 1.4f, 1.6f), new Color(0.4f, 0.5f, 0.7f)), h.position + Vector3.up * RR(8f, 11f), Quaternion.Euler(0, RR(0, 360), 0), new Vector3(2f, RR(5f, 9f), 2f), h, false);
                    break;
                case SettlementStyle.Solar:
                    if (R01() < 0.5f) RegionBuilder.Object("Cúpula branca", ProcMesh.Sphere(14, 0f, 0, true), WorldMats.Stone("whitecliff", new Color(1f, 0.97f, 0.9f), 3f), h.position + Vector3.up * RR(8.5f, 10f), Quaternion.identity, Vector3.one * RR(3f, 4.5f), h, false);
                    break;
                case SettlementStyle.Forja:
                    RegionBuilder.Object("Chaminé", ProcMesh.Prism(6, 0.8f, 0.6f, 7f), WorldMats.Stone("darkrock", new Color(0.35f, 0.3f, 0.3f), 2f), h.position + h.right * 2.5f + Vector3.up * 5f, Quaternion.identity, Vector3.one, h, false);
                    RegionBuilder.Object("Brasa da chaminé", ProcMesh.Sphere(8), WorldMats.Glow(new Color(2.4f, 0.7f, 0.15f), 0.6f, 1.3f), h.position + h.right * 2.5f + Vector3.up * 12.2f, Quaternion.identity, Vector3.one * 0.6f, h, false, RegionBuilder.LayerDetail);
                    break;
                case SettlementStyle.Ilha:
                case SettlementStyle.AldeiaArvore:
                    foreach (var off in new[] { new Vector3(-3, 0, -3), new Vector3(3, 0, -3), new Vector3(-3, 0, 3), new Vector3(3, 0, 3) })
                        RegionBuilder.Object("Estaca", ProcMesh.Prism(6, 0.3f, 0.3f, 3.5f), WorldMats.Stone("camp:timber", new Color(0.5f, 0.42f, 0.35f), 1f), h.position + h.rotation * off - Vector3.up * 3f, Quaternion.identity, Vector3.one, h, false);
                    h.position += Vector3.up * 1.2f;
                    break;
            }
        }

        static void StyleExtras(SettlementSpec s, Transform parent)
        {
            switch (s.style)
            {
                case SettlementStyle.Noturna:
                    // cordões de lanternas entre as casas (o mercado noturno nunca dorme)
                    for (int i = 0; i < 18; i++)
                    {
                        float a = i * 20f;
                        var p = s.c + Dir(a) * s.radius * 0.66f;
                        RegionBuilder.Object("Lanterna de cordão", ProcMesh.Sphere(6), WorldMats.Glow(i % 3 == 0 ? new Color(1.7f, 0.5f, 1.3f) : new Color(1.8f, 1f, 0.5f), 0.25f, 1.3f), G(p, 6f + Mathf.Sin(i) * 0.6f), Quaternion.identity, Vector3.one * 0.35f, parent, false, RegionBuilder.LayerDetail);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        var p = s.c + Dir(i * 90f + 45f) * s.radius * 0.3f;
                        if (RegionBuilder.NearRoad(p, 5f)) continue;
                        RegionBuilder.Object("Tenda-cúpula do mercado", ProcMesh.Sphere(14, 0f, 0, true), WorldMats.Stone("camp:planks", new Color(0.45f, 0.25f, 0.5f), 2f), G(p, -0.1f), Quaternion.identity, new Vector3(4f, 3.5f, 4f), parent);
                    }
                    break;
                case SettlementStyle.Acampamento:
                case SettlementStyle.Caravana:
                    var tent = WorldMats.Stone("camp:planks", s.style == SettlementStyle.Caravana ? new Color(0.75f, 0.55f, 0.4f) : new Color(0.42f, 0.4f, 0.4f), 1.5f);
                    int n = s.style == SettlementStyle.Caravana ? 10 : 7;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * 360f / n + 10f;
                        if (OnStreet(s, a, 14f)) continue;
                        var p = s.c + Dir(a) * s.radius * 0.6f;
                        if (RegionBuilder.NearRoad(p, 4f)) continue;
                        RegionBuilder.Object("Tenda", ProcMesh.Cone(6, 3f, 3.6f), tent, G(p, -0.1f), Quaternion.Euler(0, a, 0), Vector3.one, parent);
                        if (s.style == SettlementStyle.Caravana) RegionBuilder.Kit("Cart", new Vector3(p.x, 0, p.y) + new Vector3(Dir(a).x, 0, Dir(a).y) * 6f, a + 90f, parent);
                        else RegionBuilder.Object("Mastro de sinais (bandeira)", ProcMesh.Box(0.15f, 7f, 0.15f), WorldMats.Stone("camp:timber", new Color(0.5f, 0.45f, 0.4f), 1f), G(p + Dir(a) * 4f), Quaternion.identity, Vector3.one, parent);
                    }
                    break;
                case SettlementStyle.Clinica:
                    for (int i = 0; i < 6; i++)
                    {
                        var p = s.c + Dir(i * 60f + 30f) * (s.radius + 18f);
                        if (OnStreet(s, i * 60f + 30f, 15f) || RegionBuilder.NearRoad(p, 6f)) continue;
                        RegionBuilder.Object("Tanque de sal", ProcMesh.Box(9f, 0.6f, 6f), WorldMats.Stone("whitecliff", new Color(1f, 1f, 1f), 2f), G(p, -0.3f), Quaternion.Euler(0, i * 60f, 0), Vector3.one, parent);
                    }
                    break;
            }
        }
    }
}

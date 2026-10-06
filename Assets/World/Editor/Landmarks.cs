using Elyndra.World;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Marcos de cada reino — o que torna o lugar reconhecível de longe: a cratera do primeiro peso, as
    /// torres de vidro de Miralume, as árvores-catedral de Orvalume, as torres solares e o teatro de Helion,
    /// o vulcão da Coroa, os faróis do Mar de Vidro, a escadaria sem fim de Nereth, a muralha de Granith, a
    /// cúpula de rocha de Sombrafonte, a Fenda gigante da Fronteira Muda… Formas procedurais com materiais de
    /// foto (PLACEHOLDER marcado: cada um diz o que deve substituí-lo).
    /// </summary>
    public static class Landmarks
    {
        static System.Random r;
        static float R01() => (float)r.NextDouble();
        static float RR(float a, float b) => a + (b - a) * R01();
        static Vector3 G(Vector2 p, float lift = 0f) => RegionBuilder.G(p, lift);
        static Vector2 Dir(float a) => RegionBuilder.Dir(a);
        static GameObject O(string n, Mesh m, Material mat, Vector3 p, Quaternion q, Vector3 s, Transform par, bool col = true, int layer = 0) => RegionBuilder.Object(n, m, mat, p, q, s, par, col, layer);

        public static void Build(LandmarkSpec lm, Transform parent)
        {
            r = new System.Random(lm.name.GetHashCode());
            var go = new GameObject("Marco — " + lm.name + " (" + lm.kind + ")").transform;
            go.SetParent(parent, false);
            go.position = G(lm.pos);
            float s = lm.scale;
            var c = lm.pos;
            Material stone = WorldMats.Stone("camp:stone_wall", new Color(0.75f, 0.72f, 0.7f), 3f);
            Material dark = WorldMats.Stone("darkrock", new Color(0.5f, 0.47f, 0.48f), 5f);
            Material white = WorldMats.Stone("whitecliff", new Color(1f, 0.97f, 0.92f), 5f);
            Material wood = WorldMats.Stone("camp:timber", new Color(0.55f, 0.45f, 0.38f), 1.5f);
            string replace = "Marco provisório (forma procedural) — modelar no Blender no estilo do reino";
            switch (lm.kind)
            {
                case LandmarkKind.Cratera:
                {
                    // bordas quebradas, rachaduras acesas que irradiam e destroços de casas/carros atraídos pelo peso
                    var crack = WorldMats.Glow(new Color(1.6f, 0.75f, 0.35f), 0.25f, 0.8f);
                    for (int i = 0; i < 22; i++)
                    {
                        float a = i * 360f / 22f + RR(-6, 6);
                        var p = c + Dir(a) * 70f * s * RR(0.92f, 1.1f);
                        if (RegionBuilder.NatureKit) RegionBuilder.NatureRock(i % 3 == 0 ? "Nature_Outcrop_C" : i % 3 == 1 ? "Nature_Outcrop_A" : "Nature_Boulder_C", p, a + 90f, RR(1.1f, 2f), 6f, 0.9f, 0.3f, true, go);
                        else RegionBuilder.Kit(i % 3 == 0 ? "Cliff_Rock_A" : i % 3 == 1 ? "Cliff_Rock_B" : "Rock_B", new Vector3(p.x, -2f, p.y), a + 90f, go, RR(1.4f, 2.6f));
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * 30f + RR(-8, 8);
                        var p = c + Dir(a) * RR(8f, 55f) * s;
                        O("Rachadura acesa", ProcMesh.Box(0.5f, 0.15f, RR(10f, 26f)), crack, G(p, 0.05f), Quaternion.Euler(0, a, 0), Vector3.one, go, false);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        var p = c + Dir(RR(0, 360)) * RR(15f, 50f) * s;
                        O("Viga arrancada", ProcMesh.Box(0.5f, 0.5f, RR(4f, 9f)), wood, G(p, 0.2f), Quaternion.Euler(RR(-20, 20), RR(0, 360), RR(-60, 60)), Vector3.one, go);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        // pedras suspensas por correntes de peso (Dó prende o que é estável)
                        var p = c + Dir(i * 45f + 20f) * RR(20f, 40f) * s;
                        var top = G(p, RR(6f, 14f));
                        O("Pedra presa no ar", ProcMesh.Sphere(7, 0.28f, i), stone, top, Quaternion.Euler(RR(0, 360), RR(0, 360), 0), Vector3.one * RR(1.2f, 2.4f), go, false);
                        O("Corrente de peso", ProcMesh.Prism(4, 0.12f, 0.12f, top.y - G(p).y), dark, G(p), Quaternion.identity, Vector3.one, go, false);
                    }
                    break;
                }
                case LandmarkKind.Ermida:
                    RegionBuilder.Kit("GTower_C", new Vector3(c.x, 0, c.y), lm.yaw, go, 0.9f);
                    RegionBuilder.Kit("GHouse_D", new Vector3(c.x + 14f, 0, c.y - 6f), lm.yaw + 90f, go);
                    RegionBuilder.Kit("GHouse_C", new Vector3(c.x - 13f, 0, c.y + 4f), lm.yaw - 80f, go);
                    RegionBuilder.Kit("LowWall", new Vector3(c.x, 0, c.y - 18f), lm.yaw, go);
                    replace = "Ermida do Sino (torre + casas do kit) — capela própria com sino grande";
                    break;
                case LandmarkKind.Aqueduto:
                    for (int i = 0; i < 6; i++) { var p = c + Dir(lm.yaw) * (i * 30f - 75f); RegionBuilder.Kit("Aqueduct", new Vector3(p.x, -1f, p.y), lm.yaw + 90f, go, 1.3f); }
                    break;
                case LandmarkKind.Moinho: RegionBuilder.Kit("Windmill", new Vector3(c.x, 0, c.y), lm.yaw, go, 1.2f * s); break;
                case LandmarkKind.Moinhos:
                    for (int i = 0; i < 3; i++) { var p = c + Dir(lm.yaw + i * 120f) * 40f * s; RegionBuilder.Kit("Windmill", new Vector3(p.x, 0, p.y), RR(0, 360), go, 1.1f); }
                    break;
                case LandmarkKind.EstradaSuspensa:
                {
                    // estrada elevada sobre pilares: um trecho jogável que atravessa o cânion
                    var a = c - Dir(lm.yaw) * 110f * s; var b = c + Dir(lm.yaw) * 110f * s;
                    float ya = RegionBuilder.Y(a.x, a.y), yb = RegionBuilder.Y(b.x, b.y);
                    int n = 14;
                    for (int i = 0; i < n; i++)
                    {
                        float t = (i + 0.5f) / n;
                        var p = Vector2.Lerp(a, b, t);
                        float y = Mathf.Lerp(ya, yb, t) + 2f;
                        O("Tabuleiro suspenso", ProcMesh.Box(7f, 0.8f, 220f * s / n + 0.1f), stone, new Vector3(p.x, y - 0.8f, p.y), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                        float gy = RegionBuilder.Y(p.x, p.y);
                        if (i % 2 == 0 && y - gy > 4f) O("Pilar alto", ProcMesh.Prism(8, 2.2f, 1.6f, y - gy + 3f), stone, new Vector3(p.x, gy - 3f, p.y), Quaternion.identity, Vector3.one, go);
                        O("Mureta", ProcMesh.Box(0.4f, 1.1f, 220f * s / n), stone, new Vector3(p.x, y, p.y) + Quaternion.Euler(0, lm.yaw, 0) * Vector3.right * 3.3f, Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                        O("Mureta", ProcMesh.Box(0.4f, 1.1f, 220f * s / n), stone, new Vector3(p.x, y, p.y) - Quaternion.Euler(0, lm.yaw, 0) * Vector3.right * 3.3f, Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    }
                    replace = "Estrada suspensa de Velária (tabuleiro + pilares) — trecho modelado com pedágio rítmico";
                    break;
                }
                case LandmarkKind.CidadeCaravana:
                    for (int i = 0; i < 12; i++) { var p = c + Dir(i * 30f) * 26f * s; RegionBuilder.Kit("Cart", new Vector3(p.x, 0, p.y), i * 30f + 90f, go, 1.4f); }
                    O("Mastro da caravana", ProcMesh.Prism(6, 0.4f, 0.25f, 22f), wood, G(c), Quaternion.identity, Vector3.one, go);
                    O("Estandarte", ProcMesh.Box(0.1f, 6f, 3.2f), WorldMats.Stone("camp:planks", new Color(0.8f, 0.3f, 0.2f), 1f), G(c, 15f) + Vector3.right * 1.7f, Quaternion.identity, Vector3.one, go, false);
                    break;
                case LandmarkKind.TorreVidro:
                {
                    var glass = WorldMats.Crystal(new Color(0.62f, 0.7f, 0.78f, 0.6f), new Color(1.1f, 1.3f, 1.5f), new Color(0.35f, 0.45f, 0.6f), 0.8f);
                    for (int i = 0; i < 5; i++)
                    {
                        var p = c + Dir(i * 72f + 15f) * (i == 0 ? 0f : RR(22f, 40f)) * s;
                        float h = (i == 0 ? 95f : RR(45f, 75f)) * s;
                        O("Pódio", ProcMesh.Prism(6, 9f, 8f, 4f), stone, G(p, -1f), Quaternion.identity, Vector3.one, go);
                        O("Torre de vidro fosco", ProcMesh.Prism(6, 6f, 2.2f, h, true, 10f), glass, G(p, 2.5f), Quaternion.Euler(0, RR(0, 60), 0), Vector3.one, go);
                        for (int k = 1; k < 6; k++) O("Anel de arquivo", ProcMesh.Prism(6, 6.4f - k * 0.75f, 6.2f - k * 0.75f, 1.2f), stone, G(p, 2.5f + h * k / 6f), Quaternion.identity, Vector3.one, go, false);
                    }
                    replace = "Torres de vidro fosco de Miralume — arquivos de voz modelados";
                    break;
                }
                case LandmarkKind.ArvoreCatedral:
                {
                    // a árvore do kit (com folhas em cartões) ampliada ~7x + raízes enormes saindo do chão
                    var bark = WorldMats.Stone("camp:nat_bark", new Color(0.5f, 0.42f, 0.36f), 4f);
                    var tree = RegionBuilder.Kit(r.Next(2) == 0 ? "Tree_Oak" : "Tree_Oak2", new Vector3(c.x, -1.5f, c.y), RR(0, 360), go, 7f * s, false);
                    if (tree != null) { var cap = tree.AddComponent<CapsuleCollider>(); cap.radius = 0.45f; cap.height = 9f; cap.center = new Vector3(0, 4.5f, 0); }
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 51f + RR(-12, 12);
                        O("Raiz-catedral", ProcMesh.Prism(6, 2.3f * s, 0.25f, RR(18f, 30f) * s, true, RR(-20, 20)), bark, G(c + Dir(a) * 3.5f * s, -1.4f), Quaternion.Euler(RR(74f, 82f), a, 0), Vector3.one, go);
                    }
                    replace = "Árvore-catedral de Orvalume (árvore do kit ampliada + raízes procedurais) — modelar a árvore gigante própria";
                    break;
                }
                case LandmarkKind.TorreSolar:
                    for (int i = 0; i < 3; i++)
                    {
                        var p = c + Dir(lm.yaw + i * 120f) * (i == 0 ? 0 : 50f) * s;
                        float h = (i == 0 ? 70f : 45f) * s;
                        O("Torre solar", ProcMesh.Prism(4, 5f, 1.2f, h), white, G(p, -1f), Quaternion.Euler(0, 45, 0), Vector3.one, go);
                        O("Espelho de ouro", ProcMesh.Sphere(16), WorldMats.Glow(new Color(2.4f, 1.9f, 0.9f), 0.15f, 2.5f), G(p, h + 3f), Quaternion.identity, new Vector3(6f, 6f, 1.2f) * s, go, false);
                    }
                    break;
                case LandmarkKind.Teatro:
                {
                    // anfiteatro: degraus em anel voltados para o palco (a plateia que não para de aplaudir)
                    for (int k = 0; k < 9; k++)
                        O("Degrau da plateia", ProcMesh.Prism(36, 26f + k * 3.2f, 26f + k * 3.2f, 1.1f, true), white, G(c, -0.6f + k * 1.05f) + new Vector3(0, 0, 0), Quaternion.identity, new Vector3(1f, 1f, 1f), go);
                    O("Palco", ProcMesh.Box(30f, 1.4f, 14f), white, G(c + Dir(lm.yaw) * 6f, -0.4f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    for (int i = 0; i < 8; i++) O("Coluna do palco", ProcMesh.Prism(8, 0.9f, 0.8f, 12f), white, G(c + Dir(lm.yaw) * 14f + Dir(lm.yaw + 90f) * (i - 3.5f) * 4f), Quaternion.identity, Vector3.one, go);
                    replace = "Teatro do Aplauso (anéis + palco) — anfiteatro modelado";
                    break;
                }
                case LandmarkKind.MercadoNoturno:
                    for (int i = 0; i < 10; i++)
                    {
                        var p = c + Dir(i * 36f) * RR(10f, 30f) * s;
                        O("Tenda de sonhos", ProcMesh.Sphere(12, 0f, 0, true), WorldMats.Stone("camp:planks", new Color(0.4f, 0.22f, 0.45f), 2f), G(p, -0.2f), Quaternion.identity, new Vector3(4f, 3f, 4f), go);
                        O("Lanterna", ProcMesh.Sphere(8), WorldMats.Glow(i % 2 == 0 ? new Color(1.6f, 0.5f, 1.4f) : new Color(0.5f, 1f, 1.8f), 0.3f, 1.3f), G(p, 3.8f), Quaternion.identity, Vector3.one * 0.6f, go, false);
                    }
                    break;
                case LandmarkKind.Mosteiro:
                    RegionBuilder.Kit("BellTower", new Vector3(c.x, 0, c.y), lm.yaw, go, 1.1f);
                    RegionBuilder.Kit("GTower_A", new Vector3(c.x + 18f, 0, c.y + 6f), lm.yaw, go);
                    RegionBuilder.Kit("GHouse_C", new Vector3(c.x - 16f, 0, c.y + 2f), lm.yaw + 90f, go);
                    for (int i = 0; i < 8; i++) { var p = c + Dir(i * 45f) * 30f; RegionBuilder.Kit("Wall_Segment", new Vector3(p.x, -0.2f, p.y), i * 45f + 90f, go); }
                    break;
                case LandmarkKind.Escadaria:
                {
                    // a Escadaria Depois do Fim: sempre há mais um degrau (aqui, 140 deles até um arco que não leva a nada)
                    int steps = Mathf.RoundToInt(140 * s);
                    var start = G(c);
                    O("Escadaria Depois do Fim", ProcMesh.Stairs(steps, 7f, 0.32f, 0.9f), stone, start, Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    var top = start + Quaternion.Euler(0, lm.yaw, 0) * new Vector3(0, steps * 0.32f, steps * 0.9f);
                    O("Patamar final", ProcMesh.Box(16f, 1f, 16f), stone, top - Vector3.up * 1f + Quaternion.Euler(0, lm.yaw, 0) * Vector3.forward * 8f, Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    O("Arco sem destino", ProcMesh.Arch(6f, 9f, 1.5f, 1.2f), stone, top + Quaternion.Euler(0, lm.yaw, 0) * Vector3.forward * 14f, Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    for (int i = 0; i < steps; i += 10)
                        O("Vela", ProcMesh.Prism(6, 0.08f, 0.08f, 0.4f), white, start + Quaternion.Euler(0, lm.yaw, 0) * new Vector3(3.2f, i * 0.32f + 0.32f, i * 0.9f + 0.4f), Quaternion.identity, Vector3.one, go, false, RegionBuilder.LayerDetail);
                    break;
                }
                case LandmarkKind.Cemiterio:
                    for (int i = 0; i < 48; i++)
                    {
                        var p = c + new Vector2((i % 8 - 3.5f) * 4f, (i / 8 - 2.5f) * 5f) + new Vector2(RR(-0.6f, 0.6f), RR(-0.6f, 0.6f));
                        O("Lápide", ProcMesh.Box(0.9f, RR(1f, 1.8f), 0.25f), WorldMats.Stone("monastery", new Color(0.7f, 0.68f, 0.66f), 1.2f), G(p, -0.2f), Quaternion.Euler(RR(-6, 6), lm.yaw + RR(-5, 5), RR(-8, 8)), Vector3.one, go, true, RegionBuilder.LayerDetail);
                        if (i % 6 == 0) O("Vela acesa", ProcMesh.Sphere(6), WorldMats.Glow(new Color(1.8f, 1f, 0.45f), 0.6f, 1.5f), G(p + new Vector2(0.8f, 0.5f), 0.4f), Quaternion.identity, Vector3.one * 0.18f, go, false, RegionBuilder.LayerDetail);
                    }
                    break;
                case LandmarkKind.Muralha:
                    for (int i = 0; i < 16; i++) { var p = c + Dir(lm.yaw) * (i - 7.5f) * 9f * s; RegionBuilder.Kit(i % 4 == 0 ? "Wall_Tower" : "Wall_Segment", new Vector3(p.x, -0.3f, p.y), lm.yaw + 90f, go, 1.3f); }
                    break;
                case LandmarkKind.Cidadela:
                    O("Torre de menagem", ProcMesh.Prism(8, 14f * s, 11f * s, 55f * s), dark, G(c, -2f), Quaternion.identity, Vector3.one, go);
                    O("Coroa da torre", ProcMesh.Prism(8, 15f * s, 15f * s, 4f), dark, G(c, 53f * s), Quaternion.identity, Vector3.one, go, false);
                    for (int i = 0; i < 4; i++) { var p = c + Dir(i * 90f + 45f) * 28f * s; RegionBuilder.Kit("GTower_A", new Vector3(p.x, 0, p.y), i * 90f, go, 1.3f); }
                    break;
                case LandmarkKind.Vulcao:
                {
                    var ash = WorldMats.Stone("ash", new Color(0.6f, 0.55f, 0.52f), 14f);
                    float h = 300f * s;
                    O("Vulcão da Coroa", ProcMesh.Prism(28, 360f * s, 70f * s, h, false), ash, G(c, -30f), Quaternion.identity, Vector3.one, go);
                    O("Boca do vulcão (lava)", ProcMesh.Prism(20, 66f * s, 66f * s, 2f), WorldMats.Glow(new Color(3f, 0.9f, 0.2f), 0.2f, 0.6f), G(c, h - 34f), Quaternion.identity, Vector3.one, go, false);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f + RR(-10, 10);
                        O("Rio de lava", ProcMesh.Box(5f, 1f, h * 1.1f), WorldMats.Glow(new Color(2.4f, 0.6f, 0.12f), 0.15f, 0.7f), G(c, -30f) + new Vector3(Dir(a).x, 0, Dir(a).y) * (215f * s) + Vector3.up * (h * 0.5f - 10f), Quaternion.Euler(-38f, a + 180f, 0), Vector3.one, go, false);
                    }
                    replace = "Vulcão (cone procedural) — relevo real + fumaça volumétrica";
                    break;
                }
                case LandmarkKind.Forja:
                    O("Salão da forja", ProcMesh.Box(36f, 14f, 24f), dark, G(c, -0.5f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    for (int i = 0; i < 3; i++) { var p = c + Dir(lm.yaw + 90f) * (i - 1) * 11f; O("Chaminé da forja", ProcMesh.Prism(8, 2.5f, 1.8f, 30f), dark, G(p, 12f), Quaternion.identity, Vector3.one, go); O("Brasa", ProcMesh.Sphere(8), WorldMats.Glow(new Color(2.6f, 0.8f, 0.2f), 0.5f, 1.2f), G(p, 42.5f), Quaternion.identity, Vector3.one * 1.8f, go, false); }
                    O("Bigorna", ProcMesh.Box(3f, 1.4f, 1.6f), dark, G(c + Dir(lm.yaw) * 18f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    break;
                case LandmarkKind.Farol:
                    O("Farol", ProcMesh.Prism(10, 5f, 3.4f, 38f), white, G(c, -0.5f), Quaternion.identity, Vector3.one, go);
                    O("Lanterna do farol", ProcMesh.Prism(10, 3.6f, 3.6f, 4f), WorldMats.Glow(new Color(2.4f, 2.1f, 1.4f), 0.3f, 1.5f), G(c, 37.5f), Quaternion.identity, Vector3.one, go, false);
                    var beam = O("Facho (gira)", ProcMesh.Prism(8, 0.2f, 6f, 120f, false), WorldMats.Glow(new Color(0.5f, 0.45f, 0.3f), 0f, 0.6f), G(c, 39.5f), Quaternion.Euler(0, 0, -90f), Vector3.one, go, false);
                    var pivot = new GameObject("Rotação do facho").transform; pivot.SetParent(go, false); pivot.position = G(c, 39.5f);
                    beam.transform.SetParent(pivot, true);
                    pivot.gameObject.AddComponent<Campanula.Spin>();
                    break;
                case LandmarkKind.FarolSubmerso:
                    O("Farol submerso (inclinado)", ProcMesh.Prism(10, 5f, 3.4f, 38f), white, G(c, -10f), Quaternion.Euler(18f, lm.yaw, 9f), Vector3.one, go);
                    goto case LandmarkKind.Recife;
                case LandmarkKind.Recife:
                {
                    var cm = WorldMats.Crystal(new Color(0.1f, 0.35f, 0.38f, 0.6f), new Color(0.6f, 1.6f, 1.5f), new Color(0.2f, 0.7f, 0.7f));
                    for (int i = 0; i < 22; i++)
                    {
                        var p = c + Dir(RR(0, 360)) * RR(6f, 45f) * s;
                        float h = RR(3f, 16f);
                        O("Recife de vidro", ProcMesh.Crystal(i % 12), cm, G(p, -1f), Quaternion.Euler(RR(-30, 30), RR(0, 360), RR(-30, 30)), new Vector3(h * 0.5f, h, h * 0.5f), go, i % 3 == 0);
                    }
                    break;
                }
                case LandmarkKind.TemploSal:
                    O("Base do templo", ProcMesh.Box(30f, 2f, 22f), white, G(c, -0.8f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    for (int i = 0; i < 12; i++) { float a = i * 30f; O("Coluna de sal", ProcMesh.Prism(10, 0.9f, 0.8f, 11f), white, G(c + Dir(a) * 11f, 1f), Quaternion.identity, Vector3.one, go); }
                    O("Cúpula da Sutura", ProcMesh.Sphere(18, 0f, 0, true), white, G(c, 12f), Quaternion.identity, Vector3.one * 12f, go, false);
                    break;
                case LandmarkKind.JardimSal:
                    for (int k = 0; k < 5; k++)
                        for (int i = 0; i < 4; i++)
                            O("Terraço de sal", ProcMesh.Box(14f, 0.8f, 9f), white, G(c + new Vector2((i - 1.5f) * 15f, k * 10f), -0.4f + k * 0.2f), Quaternion.identity, Vector3.one, go);
                    break;
                case LandmarkKind.CavernaCupula:
                {
                    // a cúpula de rocha de Sombrafonte: o "céu" é pedra; cristais no teto fazem as estrelas
                    float rad = RegionBuilder.CurrentSize * 0.62f;
                    O("Cúpula da caverna", ProcMesh.Sphere(28, 0.04f, 2, true, true), dark, G(c, -40f), Quaternion.identity, new Vector3(rad, 320f, rad), go, false);
                    var cm = WorldMats.Crystal(new Color(0.08f, 0.25f, 0.3f, 0.7f), new Color(0.5f, 1.4f, 1.7f), new Color(0.2f, 0.7f, 0.9f));
                    for (int i = 0; i < 40; i++)
                    {
                        float a = RR(0, 360), d = RR(0.1f, 0.85f) * rad;
                        var dir = new Vector3(Dir(a).x, 0, Dir(a).y);
                        float y = Mathf.Sqrt(Mathf.Max(0, 1 - (d / rad) * (d / rad))) * 320f - 40f + G(c).y - 6f;
                        O("Cristal do teto", ProcMesh.Crystal(i % 12), cm, new Vector3(c.x, y, c.y) + dir * d, Quaternion.Euler(180f + RR(-20, 20), RR(0, 360), 0), new Vector3(4f, RR(8f, 20f), 4f), go, false);
                    }
                    replace = "Cúpula da caverna (esfera invertida) — caverna esculpida com estalactites";
                    break;
                }
                case LandmarkKind.Cristais:
                {
                    var cm = WorldMats.Crystal(new Color(0.08f, 0.25f, 0.3f, 0.6f), new Color(0.5f, 1.5f, 1.8f), new Color(0.2f, 0.8f, 1f));
                    for (int i = 0; i < 7; i++)
                    {
                        var p = c + Dir(i * 51f) * RR(0f, 12f) * s;
                        float h = RR(14f, 34f) * s;
                        O("Cristal gigante de Eco", ProcMesh.Crystal(i), cm, G(p, -2f), Quaternion.Euler(RR(-18, 18), RR(0, 360), RR(-18, 18)), new Vector3(h * 0.4f, h, h * 0.4f), go, true);
                    }
                    break;
                }
                case LandmarkKind.Mina:
                    O("Boca da mina", ProcMesh.Arch(7f, 8f, 5f, 2f), wood, G(c), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    for (int i = 0; i < 10; i++) O("Trilho", ProcMesh.Box(2.2f, 0.15f, 3f), wood, G(c - Dir(lm.yaw) * (i * 3f + 3f), 0.05f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go, false, RegionBuilder.LayerDetail);
                    RegionBuilder.Kit("Cart", new Vector3((c - Dir(lm.yaw) * 10f).x, 0, (c - Dir(lm.yaw) * 10f).y), lm.yaw, go);
                    break;
                case LandmarkKind.FendaRasgo:
                {
                    // na Fronteira Muda a Fenda deixa de ser um brilho distante: é uma ferida que ocupa o céu
                    var rift = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    rift.name = "A Fenda do Contracanto (perto)";
                    Object.DestroyImmediate(rift.GetComponent<Collider>());
                    rift.transform.SetParent(go, false);
                    rift.transform.position = G(c, 700f * s);
                    rift.transform.rotation = Quaternion.Euler(0, lm.yaw, 0);
                    rift.transform.localScale = new Vector3(900f * s, 1700f * s, 1f);
                    var mr = rift.GetComponent<MeshRenderer>(); mr.sharedMaterial = WorldMats.Rift(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rift.AddComponent<Campanula.RiftPulse>();
                    goto case LandmarkKind.RochasFlutuantes;
                }
                case LandmarkKind.RochasFlutuantes:
                {
                    var holder = new GameObject("Rochas sem peso (Ausência)").transform; holder.SetParent(go, false); holder.position = G(c, 30f);
                    for (int i = 0; i < 18; i++)
                    {
                        var p = new Vector3(RR(-70, 70), RR(-10, 40), RR(-70, 70)) * s;
                        var rock = RegionBuilder.Kit(i % 2 == 0 ? "Cliff_Rock_C" : "Rock_A", Vector3.zero, RR(0, 360), holder, RR(1f, 3f), false, false);
                        if (rock != null) { rock.transform.localPosition = p; rock.transform.localRotation = Quaternion.Euler(RR(0, 360), RR(0, 360), RR(0, 360)); }
                    }
                    var fb = holder.gameObject.AddComponent<FormingBody>(); fb.spin = 0.6f; fb.bob = 2.5f;
                    break;
                }
                case LandmarkKind.Ruina:
                    for (int i = 0; i < 8; i++)
                    {
                        var p = c + Dir(i * 45f + RR(-10, 10)) * RR(6f, 22f) * s;
                        var w = RegionBuilder.Kit("Wall_Segment", new Vector3(p.x, RR(-2.5f, -0.5f), p.y), RR(0, 360), go, RR(0.8f, 1.2f));
                        if (w != null) w.transform.rotation = Quaternion.Euler(RR(-12, 12), w.transform.eulerAngles.y, RR(-15, 15));
                    }
                    for (int i = 0; i < 5; i++) O("Coluna quebrada", ProcMesh.Prism(8, 0.8f, 0.7f, RR(2f, 7f)), stone, G(c + Dir(i * 72f) * 14f * s, -0.3f), Quaternion.Euler(RR(-6, 6), 0, RR(-6, 6)), Vector3.one, go);
                    if (RegionBuilder.NatureKit && RegionBuilder.HasModel("Nature_Rubble_A"))
                        for (int i = 0; i < 6; i++) RegionBuilder.NatureRock(i % 2 == 0 ? "Nature_Rubble_A" : "Nature_Rubble_B", c + Dir(i * 60f + RR(-15, 15)) * RR(4f, 20f) * s, RR(0, 360), RR(0.9f, 1.5f), 3f, 0.8f, 0.1f, true, go);
                    break;
                case LandmarkKind.Obelisco:
                    O("Obelisco", ProcMesh.Prism(4, 2.2f * s, 0.4f, 26f * s), dark, G(c, -0.5f), Quaternion.Euler(0, 45, 0), Vector3.one, go);
                    O("Glifo aceso", ProcMesh.Box(0.1f, 6f * s, 1.2f), WorldMats.Glow(new Color(0.7f, 0.5f, 1.4f), 0.3f, 1f), G(c, 8f * s) + new Vector3(1.6f * s, 0, 0), Quaternion.identity, Vector3.one, go, false);
                    break;
                case LandmarkKind.CidadeDistante:
                {
                    // silhueta de Campânula atrás do portão: torres, casas e muralha (sem colisão; fora do limite)
                    string[] tall = { "GTower_A", "GTower_B", "GTower_C", "BellTower" };
                    string[] low = { "GHouse_A", "GHouse_B", "GHouse_C", "GHouse_D", "GTavern" };
                    for (int i = 0; i < 26; i++)
                    {
                        var p = c + new Vector2(RR(-70f, 40f), RR(-120f, 120f)) * s;
                        bool t = i % 4 == 0;
                        var m = RegionBuilder.Kit(t ? tall[r.Next(tall.Length)] : low[r.Next(low.Length)], new Vector3(p.x, -0.5f, p.y), RR(0, 360), go, RR(0.95f, 1.15f), false);
                    }
                    for (int i = 0; i < 12; i++) { var p = c + new Vector2(55f * s, (i - 5.5f) * 18f * s); RegionBuilder.Kit(i % 4 == 0 ? "Wall_Tower" : "Wall_Segment", new Vector3(p.x, -0.4f, p.y), 0f, go, 1.2f, false); }
                    replace = "Silhueta de Campânula (kit sem colisão) — a cidade real é a cena Campanula";
                    break;
                }
                case LandmarkKind.Pedreira:
                    for (int k = 0; k < 6; k++) O("Bancada da pedreira", ProcMesh.Box(40f - k * 4f, 4f, 30f - k * 3f), WorldMats.Stone("darkrock", new Color(0.62f, 0.6f, 0.6f), 4f), G(c, -2f + k * 4f), Quaternion.Euler(0, lm.yaw, 0), Vector3.one, go);
                    O("Guindaste", ProcMesh.Box(1f, 22f, 1f), wood, G(c + Dir(lm.yaw + 90f) * 26f), Quaternion.identity, Vector3.one, go);
                    O("Braço do guindaste", ProcMesh.Box(1f, 1f, 18f), wood, G(c + Dir(lm.yaw + 90f) * 26f, 21f), Quaternion.Euler(0, lm.yaw + 90f, 0), Vector3.one, go, false);
                    break;
            }
            RegionBuilder.Tag(go.gameObject, replace, "Construção");
        }
    }
}

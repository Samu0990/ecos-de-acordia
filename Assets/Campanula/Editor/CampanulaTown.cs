using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Campanula.EditorTools
{
    /// <summary>
    /// Campânula v3 — a cidade maior e sem casa repetida (pedido do autor: "refaça a cidade toda, deixe maior e
    /// não genérica"). O miolo jogável fica onde estava (estrada sul, portão, rua do mercado, praça, campanário,
    /// ponte, campo do chefe) e a cidade cresce para oeste e para o norte, até a muralha nova (x −104, z 73):
    /// - Beco do Sino: da praça, pela brecha da muralha velha, até a Rua Principal do Bairro Oeste;
    /// - Bairro Oeste: quadras de sobrados geminados (kit_town.py: 28 casas diferentes, escolhidas sem repetir
    ///   as vizinhas e preferindo as menos usadas) ao longo das ruas do Poço, dos Fundidores e do Muro;
    ///   o Largo da Fonte com a Casa da Guilda dos Sineiros; a muralha velha vira muralha interna, com brechas;
    /// - Rua Velha: entre a rua do mercado e a muralha velha;
    /// - Bairro Norte: a Rua Alta, ao pé do Grande Aqueduto, com os becos que sobem dos lados do campanário;
    /// - muralha nova com torres e o portão oeste (fechado com as portas de castelo escaneadas do Poly Haven);
    /// - objetos de rua escaneados do Poly Haven (barris, caixotes, baldes, cestos, bancos, mesas, estátua).
    /// </summary>
    public static partial class CampanulaBuilder
    {
        public const float CityWest = -104f, CityNorth = 73f;

        /// <summary>Pegada da cidade: chão plano, sem árvores nem penedos espalhados.</summary>
        public static bool InCity(float x, float z) => x > CityWest - 3f && x < 38f && z > -44f && z < CityNorth + 3f;

        static Rect MM(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0, z0, x1, z1);

        /// <summary>Ruas e largos novos (calçamento no terreno).</summary>
        static readonly Rect[] TownStreets =
        {
            MM(-40f, -4.5f, -18f, -0.5f),    // Beco do Sino (praça → brecha da muralha velha)
            MM(-104f, -5f, -40f, 1f),        // Rua Principal (→ portão oeste)
            MM(-94f, 1f, -89f, 63f),         // Rua do Poço (ao sul dela, a catedral)
            MM(-72f, -40f, -66f, 63f),       // Rua dos Fundidores
            MM(-58f, -40f, -53f, 63f),       // Rua do Muro
            MM(-66f, -27f, -40f, -22f),      // Rua do Sul
            MM(-72f, -5f, -66f, 1f),         // cruzamento da Rua Principal com os Fundidores
            MM(-98f, -40f, -72f, -5f),       // adro da catedral (o chão em volta da igreja e do cemitério)
            MM(-94f, 33f, -19f, 38f),        // Rua Norte (atravessa a muralha velha)
            MM(-102f, 58f, 33f, 63f),        // Rua Alta
            MM(-31f, -38f, -23f, -0.5f),     // Rua Velha
            MM(-39f, -40.5f, -14f, -38f),    // passagem ao pé da muralha sul (e do muro de escalada)
            MM(-9.5f, 40f, -5.5f, 58f), MM(5f, 40f, 10f, 58f),   // becos do Campanário
            MM(-5.5f, 41f, 5f, 50f),         // pátio atrás do campanário
            MM(-28f, 27f, -18f, 33f),        // Rua Norte → canto da praça
            MM(-89f, 1f, -72f, 22f),         // Largo da Fonte
            MM(-32f, -0.5f, -29.5f, 27f),    // viela atrás das casas da praça
        };

        public static bool TownCobble(float x, float z)
        {
            foreach (var r in TownStreets) if (r.Contains(new Vector2(x, z))) return true;
            // a cidade é toda calçada (sem faixas de capim entre as casas e as ruas); terra só no cemitério e longe
            // da borda do desfiladeiro
            bool cemetery = x > -102.8f && x < -97.2f && z > -40.8f && z < -6f;
            return !cemetery && x > CityWest + 1.2f && x < 30f && z > -40.6f && z < CityNorth - 1.2f;
        }

        // ------------------------------------------------------------ casas variadas

        const int THouses = 28;
        static readonly Dictionary<string, Vector2> houseDims = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, int> houseUse = new Dictionary<string, int>();
        static System.Random townRng;
        static string lastA, lastB;

        /// <summary>Largura e fundo da casa medidos no soco (y = −1,5 m), no referencial do Place (frente = +Z).</summary>
        static Vector2 HouseDims(string model)
        {
            if (houseDims.TryGetValue(model, out var d)) return d;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (src == null) return houseDims[model] = new Vector2(999f, 999f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 180f, 0) * src.transform.localRotation);
            float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (p.y > -1.2f) continue;
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
                }
            }
            Object.DestroyImmediate(go);
            d = x1 > x0 ? new Vector2(x1 - x0, z1 - z0) : new Vector2(999f, 999f);
            houseDims[model] = d;
            return d;
        }

        /// <summary>
        /// Fileira de sobrados geminados com a frente na linha a→b, virados para <paramref name="front"/> (a rua).
        /// Cada casa cabe no fundo <paramref name="dmax"/>; nunca repete as duas vizinhas e prefere as menos usadas.
        /// </summary>
        static int Row(Vector2 a, Vector2 b, Vector2 front, float dmax, string first = null)
        {
            var dir = b - a; float L = dir.magnitude; dir /= L;
            float yaw = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
            float pos = 0f; int n = 0;
            lastA = lastB = null;
            while (pos < L - 3.5f)
            {
                string m = null;
                if (n == 0 && first != null && HouseDims(first).x <= L && HouseDims(first).y <= dmax + 0.3f) m = first;
                if (m == null)
                {
                    var cands = new List<string>(); int best = int.MaxValue;
                    for (int i = 0; i < THouses; i++)
                    {
                        string h = "THouse_" + i.ToString("00");
                        var hd = HouseDims(h);
                        if (hd.y > dmax + 0.05f || hd.x > L - pos + 0.25f || h == lastA || h == lastB) continue;
                        cands.Add(h);
                        best = Mathf.Min(best, houseUse.TryGetValue(h, out int u) ? u : 0);
                    }
                    cands.RemoveAll(h => (houseUse.TryGetValue(h, out int u) ? u : 0) > best + 1);
                    if (cands.Count == 0) break;
                    m = cands[townRng.Next(cands.Count)];
                }
                var dd = HouseDims(m);
                var c = a + dir * (pos + dd.x / 2f) - front * (dd.y / 2f);
                var holder = Place(m, new Vector3(c.x, 0, c.y), yaw);
                houseUse[m] = (houseUse.TryGetValue(m, out int k) ? k : 0) + 1;
                lastB = lastA; lastA = m;
                if (holder != null && townRng.NextDouble() < 0.4) StreetProps(c + front * (dd.y / 2f + 0.55f), dir, front, dd.x);
                if (holder != null && townRng.NextDouble() < 0.07)
                {
                    // gaiola de ferro pendurada num braço da fachada
                    var cp = c + front * (dd.y / 2f + 0.02f) + dir * (townRng.NextDouble() < 0.5 ? -1f : 1f) * (dd.x / 2f - 0.6f);
                    Place("TCage", new Vector3(cp.x, 0, cp.y), yaw, null, true, false);
                }
                pos += dd.x + 0.03f; n++;
            }
            return n;
        }

        // ------------------------------------------------------------ objetos escaneados (Poly Haven)

        const string PHPrefabs = "Assets/World/Prefabs/PolyHaven/";

        static GameObject PlacePH(string id, Vector3 pos, float yaw, float scale = 1f, bool snap = true, bool collider = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(PHPrefabs + id + ".prefab");
            if (src == null) return null;
            var holder = new GameObject(id);
            holder.transform.SetParent(statics, false);
            if (snap) pos.y += GroundY(pos.x, pos.z);
            holder.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            holder.transform.localScale = Vector3.one * scale;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            if (!collider) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            DetailModels.Add(id);   // camada de distância dos props pequenos
            return holder;
        }

        /// <summary>Um grupinho de objetos de rua encostado na fachada (barris, caixotes, balde, cesto, banco…).</summary>
        static void StreetProps(Vector2 at, Vector2 along, Vector2 front, float width)
        {
            float side = townRng.NextDouble() < 0.5 ? -1f : 1f;
            var p = at + along * side * (width / 2f - 0.9f);
            float yaw = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
            float R(float a, float b) => a + (float)townRng.NextDouble() * (b - a);
            switch (townRng.Next(11))
            {
                case 0:
                    PlacePH("wine_barrel_01", new Vector3(p.x, 0, p.y), R(0, 360));
                    PlacePH("wine_barrel_01", new Vector3(p.x + along.x * 0.85f, 0, p.y + along.y * 0.85f), R(0, 360));
                    break;
                case 1:
                    PlacePH("wine_barrel_01", new Vector3(p.x, 0, p.y), R(0, 360));
                    PlacePH("wooden_bucket_01", new Vector3(p.x + along.x * 0.9f, 0, p.y + along.y * 0.9f), R(0, 360), 1f, true, false);
                    break;
                case 2:
                    PlacePH("wooden_crate_01", new Vector3(p.x, 0, p.y), yaw + R(-12, 12));
                    PlacePH("wooden_crate_02", new Vector3(p.x + along.x * 1.0f, 0, p.y + along.y * 1.0f), yaw + R(-25, 25));
                    break;
                case 3:
                    PlacePH("painted_wooden_bench", new Vector3(p.x, 0, p.y) - new Vector3(front.x, 0, front.y) * 0.1f, yaw + 180f);
                    break;
                case 4:
                    PlacePH("wicker_basket_01", new Vector3(p.x, 0, p.y), R(0, 360), 1f, true, false);
                    PlacePH("wooden_crate_02", new Vector3(p.x + along.x * 0.9f, 0, p.y + along.y * 0.9f), yaw + R(-25, 25));
                    break;
                case 5:
                    PlacePH("wooden_ladder", new Vector3(p.x, 0, p.y) - new Vector3(front.x, 0, front.y) * 0.25f, yaw + 180f + R(-6, 6), 1f, true, false);
                    break;
                case 6:
                    Kit("Barrel_Holder", new Vector3(p.x, 0, p.y), yaw + R(-10, 10));
                    break;
                case 7:
                    Kit("Vase_Rubble_Medium", new Vector3(p.x, 0, p.y), R(0, 360));
                    Kit("Chain_Coil", new Vector3(p.x + along.x * 0.8f, 0, p.y + along.y * 0.8f), R(0, 360), false, false);
                    break;
                case 8:
                    Kit("Cauldron", new Vector3(p.x, 0, p.y), R(0, 360));
                    PlacePH("wooden_bucket_01", new Vector3(p.x + along.x * 0.8f, 0, p.y + along.y * 0.8f), R(0, 360), 1f, true, false);
                    break;
                case 9:
                    Kit("Stall_Cart_Empty", new Vector3(p.x, 0, p.y) + new Vector3(front.x, 0, front.y) * 0.6f, yaw + 90f + R(-8, 8));
                    break;
                default:
                    PlacePH("spinning_wheel_01", new Vector3(p.x, 0, p.y), yaw + R(-40, 40));
                    PlacePH("wooden_stool_01", new Vector3(p.x + along.x * 0.8f, 0, p.y + along.y * 0.8f), R(0, 360));
                    break;
            }
        }

        static void LampRow(Vector2 a, Vector2 b, float step, Vector2 side, float off)
        {
            var dir = b - a; float L = dir.magnitude; dir /= L;
            int k = 0;
            for (float d = step * 0.5f; d < L; d += step, k++)
            {
                float s = k % 2 == 0 ? 1f : -1f;
                var q = a + dir * d + side * s * off;
                var lp = Place("LampPost", new Vector3(q.x, 0, q.y), Mathf.Atan2(-side.x * s, -side.y * s) * Mathf.Rad2Deg, null, true, false);
                if (lp == null) continue;
                var cc = lp.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 2f, 0); cc.radius = 0.15f; cc.height = 4f;
            }
        }

        // ------------------------------------------------------------ a cidade nova

        static void BuildTown(System.Text.StringBuilder log)
        {
            townRng = new System.Random(3303);
            houseDims.Clear(); houseUse.Clear();
            int n = 0;
            var E = new Vector2(1, 0); var W = new Vector2(-1, 0); var Nn = new Vector2(0, 1); var S = new Vector2(0, -1);
            Vector2 P(float x, float z) => new Vector2(x, z);

            // --- Bairro Oeste: fileiras ao longo das ruas norte-sul (as faixas entre as ruas leste-oeste)
            var bands = new[] { (-40f, -27f), (-22f, -5f), (1f, 33f), (38f, 58f) };
            // coluna da muralha oeste (casas viradas para a Rua do Poço, fundos na muralha)
            n += Row(P(-94f, 1f), P(-94f, 58f), E, 8.5f);
            foreach (var (z0, z1) in bands)
            {
                if (z0 == 38f)   // (ao sul do largo fica a catedral; o lado oeste do largo é o próprio largo)
                {
                    n += Row(P(-89f, z0), P(-89f, z1), W, 8.4f);          // Rua do Poço, lado leste
                    n += Row(P(-72f, z0), P(-72f, z1), E, 8.4f);          // Rua dos Fundidores, lado oeste
                }
                n += Row(P(-66f, z0), P(-66f, z1), W, 8f, z0 == 1f ? "TCornerTower" : null);   // Fundidores, lado leste
                n += Row(P(-53f, z0), P(-53f, Mathf.Min(z1, z0 == 38f ? 58f : z1)), W, z0 == 38f ? 9.5f : 10.5f);   // Rua do Muro, fundos na muralha velha
            }
            // Casa da Guilda dos Sineiros no lado norte do Largo da Fonte; o chafariz com a estátua no meio
            {
                var gd = HouseDims("TGuild");
                Place("TGuild", new Vector3(-80.5f, 0, 22f + gd.y / 2f), 180f);
                Place("Well", new Vector3(-80.5f, 0, 11.5f), 0f);
                PlacePH("gothic_statue", new Vector3(-80.5f, 0, 5.2f), 0f, 1f);
                foreach (var (x, z) in new[] { (-87f, 4f), (-74f, 4f), (-87f, 19f), (-74f, 19f) })
                    PlacePH("painted_wooden_bench", new Vector3(x, 0, z), x < -80f ? 90f : -90f);
                Prop("Stall_Red", new Vector3(-86.6f, 0, 12f), 90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
                Prop("Stall_Blue", new Vector3(-74.4f, 0, 9f), -90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
                PlacePH("round_wooden_table_01", new Vector3(-76.5f, 0, 16f), 0f);
                PlacePH("wooden_stool_01", new Vector3(-75.6f, 0, 16.6f), 20f);
                PlacePH("wooden_stool_01", new Vector3(-77.3f, 0, 15.2f), 200f);
                PlacePH("stone_fire_pit", new Vector3(-84.5f, 0, 16.5f), 0f);
                PlacePH("wooden_barrels_01", new Vector3(-86.2f, 0, 2.6f), 90f, 0.8f);
            }

            // --- Catedral dos Sinos: fachada para o Largo da Fonte, abside quase na muralha sul
            {
                Place("TCatedral", new Vector3(-85f, 0, -21f), 0f);
                // cemitério entre a igreja e a muralha oeste: lápides tortas, cruzes, velas e um muro baixo
                var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/CMP_ashlar.mat");
                var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/CMP_stone_dark.mat");
                var graves = new GameObject("Cemitério").transform; graves.SetParent(statics, false);
                for (float z = -20.5f; z <= -9f; z += 2.6f)
                    foreach (float x in new[] { -101.6f, -99.3f })
                    {
                        if (townRng.NextDouble() < 0.15) continue;
                        float gx = x + (float)(townRng.NextDouble() - 0.5) * 0.6f, gz = z + (float)(townRng.NextDouble() - 0.5) * 0.6f;
                        bool cross = townRng.NextDouble() < 0.3;
                        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        g.name = cross ? "Cruz" : "Lápide"; g.transform.SetParent(graves, false);
                        float h = cross ? 1.3f : 0.8f + (float)townRng.NextDouble() * 0.5f;
                        g.transform.localScale = cross ? new Vector3(0.14f, h, 0.14f) : new Vector3(0.65f, h, 0.14f);
                        g.transform.SetPositionAndRotation(new Vector3(gx, GroundY(gx, gz) + h * 0.45f, gz),
                            Quaternion.Euler((float)(townRng.NextDouble() - 0.5) * 16f, 90f + (float)(townRng.NextDouble() - 0.5) * 20f, (float)(townRng.NextDouble() - 0.5) * 14f));
                        g.GetComponent<Renderer>().sharedMaterial = townRng.NextDouble() < 0.5 ? stone : dark;
                        Object.DestroyImmediate(g.GetComponent<Collider>());
                        if (cross)
                        {
                            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            arm.name = "Braço da cruz"; arm.transform.SetParent(g.transform, false);
                            arm.transform.localPosition = new Vector3(0, 0.22f, 0); arm.transform.localScale = new Vector3(4.2f, 0.11f, 1f);
                            arm.GetComponent<Renderer>().sharedMaterial = g.GetComponent<Renderer>().sharedMaterial;
                            Object.DestroyImmediate(arm.GetComponent<Collider>());
                        }
                        // terra revolvida da cova
                        var mound = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        mound.name = "Cova"; mound.transform.SetParent(graves, false);
                        mound.transform.localScale = new Vector3(0.9f, 0.18f, 1.9f);
                        mound.transform.SetPositionAndRotation(new Vector3(gx + 0.95f, GroundY(gx + 0.95f, gz) + 0.04f, gz), Quaternion.Euler(0, 90f + (float)(townRng.NextDouble() - 0.5) * 10f, 0) * Quaternion.Euler(0, 90f, 0));
                        mound.GetComponent<Renderer>().sharedMaterial = dark;
                        Object.DestroyImmediate(mound.GetComponent<Collider>());
                        foreach (var t in new[] { g.transform, mound.transform })
                            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                        if (townRng.NextDouble() < 0.35) Kit(townRng.NextDouble() < 0.5 ? "Candle_1" : "Candle_2", new Vector3(gx + 0.45f, 0, gz + 0.3f), 0f, false, false);
                    }
                for (float z = -38f; z <= -8f; z += 4.2f)
                    Prop("LowWall", new Vector3(-97.5f, 0, z), 90f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
                // tochas no portal (a fachada fica de costas para a lua)
                foreach (float x in new[] { -88.6f, -81.4f, -94.2f, -75.8f })
                    WallTorch(new Vector3(x, 2.9f, -3f), Vector3.back, 8f);
                Kit("CandleStick_Stand", new Vector3(-90.5f, 0, -3.4f), 0f);
                Kit("CandleStick_Stand", new Vector3(-79.5f, 0, -3.4f), 0f);
            }

            // --- arcos e passadiços cruzando as ruas (a cidade ganha altura: casas ligadas por cima)
            // (estandartes sem colisor: antes dava para andar na corda)
            void Arch(string m, float x, float z, float yaw) { if (Place(m, new Vector3(x, 0, z), yaw, null, true, !m.StartsWith("TBanners")) == null) Debug.LogWarning("arco faltando: " + m); }
            Arch("TArch_5", -91.5f, 44f, 0f); Arch("TPassage_5", -91.5f, 52.5f, 0f);
            Arch("TPassage_6", -69f, 47f, 0f); Arch("TArch_5", -69f + 0f, 41f, 0f);
            Arch("TArch_5", -55.5f, -15f, 0f); Arch("TPassage_5", -55.5f, 14f, 0f); Arch("TArch_5", -55.5f, 46f, 0f);
            Arch("TArch_8", -27f, -21f, 0f); Arch("TArch_8", -27f, -11f, 0f);
            Arch("TPassage_5", -14f, 60.5f, 90f); Arch("TArch_5", 14.5f, 60.5f, 90f); Arch("TArch_5", -77f, 60.5f, 90f); Arch("TPassage_5", -45f, 60.5f, 90f);

            // --- estandartes rasgados em cordas atravessando as ruas
            Arch("TBanners_6", -69f, 54f, 0f);
            Arch("TBanners_5", -55.5f, -31f, 0f); Arch("TBanners_5", -55.5f, 27f, 0f); Arch("TBanners_5", -55.5f, 53f, 0f);
            Arch("TBanners_8", -27f, -31f, 0f); Arch("TBanners_8", -27f, -16f, 0f);
            Arch("TBanners_5", -90f, 60.5f, 90f); Arch("TBanners_5", -30f, 60.5f, 90f); Arch("TBanners_5", 0f, 60.5f, 90f); Arch("TBanners_5", 25f, 60.5f, 90f);
            Arch("TBanners_5", -80f, 35.5f, 90f); Arch("TBanners_5", -91.5f, 30f, 0f);

            // --- escada de pedra (forte escaneado) do cemitério até o alto da muralha oeste
            PlacePH("modular_fort_01_p04", new Vector3(-100.9f, 0, -30.5f), 0f);

            // --- o miolo jogável também sem casa repetida: rua do mercado e praça
            n += MarketAndPlazaRows();

            // --- Rua Velha (entre o mercado e a muralha velha) e a viela atrás das casas da praça
            n += Row(P(-31f, -38f), P(-31f, -4.5f), E, 7.3f);          // fundos na muralha velha
            n += Row(P(-23f, -28f), P(-23f, -4.5f), W, 8.2f);          // fundos nas casas do mercado
            n += Row(P(-32f, -0.5f), P(-32f, 27f), E, 6.75f);          // viela

            // --- Bairro Norte: a Rua Alta (norte: fundos na muralha nova; sul: entre as torres do campanário)
            n += Row(P(-100f, 63f), P(-63.5f, 63f), S, 8.4f);
            n += Row(P(-56.5f, 63f), P(-21f, 63f), S, 8.4f);
            n += Row(P(-18f, 63f), P(31f, 63f), S, 8.4f);
            foreach (var (x0, x1) in new[] { (-37f, -27f), (-19f, -9.5f), (-5.5f, 5f), (10f, 19f) })
                n += Row(P(x0, 58f), P(x1, 58f), Nn, 7.8f);

            // --- iluminação das ruas novas
            LampRow(P(-91.5f, -38f), P(-91.5f, 56f), 17f, E, 2.1f);
            LampRow(P(-69f, -38f), P(-69f, 56f), 17f, E, 2.4f);
            LampRow(P(-55.5f, -38f), P(-55.5f, 56f), 19f, E, 2.1f);
            LampRow(P(-100f, -2f), P(-42f, -2f), 16f, Nn, 2.4f);
            LampRow(P(-92f, 35.5f), P(-22f, 35.5f), 18f, Nn, 2.1f);
            LampRow(P(-100f, 60.5f), P(30f, 60.5f), 18f, Nn, 2.1f);
            LampRow(P(-27f, -36f), P(-27f, -6f), 15f, E, 3.4f);

            log.Append("cidade nova ok (" + n + " sobrados variados, " + houseUse.Count + " modelos diferentes)\n");
        }

        /// <summary>Rua do mercado e praça: as frentes ficam onde ficavam as dos sobrados antigos (as barracas continuam
        /// encostadas); as tavernas e as duas casas que emolduram o campanário ficam.</summary>
        static int MarketAndPlazaRows()
        {
            int n = 0;
            Vector2 P(float x, float z) => new Vector2(x, z);
            n += Row(P(-6.4f, -38.5f), P(-6.4f, -4.6f), new Vector2(1, 0), 8.1f);     // mercado, lado oeste
            n += Row(P(6.3f, -38.5f), P(6.3f, -28.6f), new Vector2(-1, 0), 8.1f);     // mercado, lado leste (até a taverna)
            n += Row(P(6.3f, -17.6f), P(6.3f, -4.6f), new Vector2(-1, 0), 8.1f);
            n += Row(P(-20.5f, -0.4f), P(-20.5f, 27f), new Vector2(1, 0), 8.9f);      // praça, lado oeste (fundos na viela)
            n += Row(P(20.6f, 10.6f), P(20.6f, 30f), new Vector2(-1, 0), 8.4f);       // praça, lado leste (depois da taverna)
            return n;
        }

        /// <summary>Muralha nova (oeste e norte), o prolongamento da muralha sul e as torres; portão oeste fechado.</summary>
        static void BuildOuterWalls(System.Text.StringBuilder log)
        {
            // muralha sul até a esquina oeste (yaw 180: frente para fora, ao sul)
            foreach (var x in new[] { -47.5f, -57.5f, -67.5f, -77.5f, -87.5f, -97.5f })
                Place("Wall_Segment", new Vector3(x, 0, -42f), 180f, null, true, true, LayerWall);
            Place("Wall_Tower", new Vector3(-72.5f, 0, -42f), 0f, null, true, true, LayerWall);
            // muralha oeste (yaw −90: frente para oeste) com o portão no fim da Rua Principal
            Place("Wall_Tower", new Vector3(CityWest, 0, -42f), 0f, null, true, true, LayerWall);
            foreach (var z in new[] { -33.6f, -23.6f, -13.6f, 7.7f, 17.7f, 27.7f, 44.4f, 54.4f, 64.4f })
                Place("Wall_Segment", new Vector3(CityWest, 0, z), -90f, null, true, true, LayerWall);
            Place("Wall_Tower", new Vector3(CityWest, 0, 36f), 0f, null, true, true, LayerWall);
            Place("Gatehouse", new Vector3(CityWest, 0, -2f), -90f, null, true, true, LayerWall);
            // o portão oeste fica fechado: as portas de castelo escaneadas (Poly Haven) no vão
            foreach (int s in new[] { -1, 1 })
                PlacePH("large_castle_door", new Vector3(CityWest - 1.2f, 0, -2f + s * 1.25f), 90f + (s > 0 ? 0f : 180f), 1.25f);
            Place("Wall_Tower", new Vector3(CityWest, 0, CityNorth), 0f, null, true, true, LayerWall);
            // muralha norte (yaw 0: frente para o norte), atrás dela o Grande Aqueduto
            for (float x = -96f; x <= 28f; x += 10f)
                Place("Wall_Segment", new Vector3(x, 0, CityNorth), 0f, null, true, true, LayerWall);
            Place("Wall_Tower", new Vector3(-60f, 0, CityNorth), 0f, null, true, true, LayerWall);
            Place("Wall_Tower", new Vector3(33.5f, 0, CityNorth), 0f, null, true, true, LayerWall);
            log.Append("muralha nova ok\n");
        }
    }
}

using System.Collections.Generic;
using Elyndra.World;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    public enum PathStyle { Estrada, Trilha, Calcada, Ponte }

    public class PathSpec
    {
        public string name;
        public List<Vector2> pts = new List<Vector2>();
        public float width = 5f;
        public PathStyle style = PathStyle.Estrada;
        public bool lamps;
        public PathSpec(string n, PathStyle s, float w, params Vector2[] p) { name = n; style = s; width = w; pts.AddRange(p); }
    }

    public enum SettlementStyle { Gotica, Vidro, Solar, Noturna, Mosteiro, Fortaleza, Forja, Ilha, Clinica, Caverna, Acampamento, Caravana, AldeiaArvore }

    public class SettlementSpec
    {
        public string name;
        public Vector2 c;
        public float radius = 55f;
        public SettlementStyle style;
        public int houses = 14;
        public bool walls;
        public float[] exits = new float[0];       // ângulos (graus, 0 = norte) das saídas/ruas
        public bool market = true, temple = true, workshop = true, tavern = true;
        public SettlementSpec(string n, SettlementStyle s, Vector2 center, float r, int h) { name = n; style = s; c = center; radius = r; houses = h; }
    }

    public enum LandmarkKind
    {
        Cratera, Ermida, Aqueduto, EstradaSuspensa, CidadeCaravana, TorreVidro, ArvoreCatedral, TorreSolar, Teatro,
        MercadoNoturno, Mosteiro, Escadaria, Cemiterio, Muralha, Cidadela, Vulcao, Forja, Farol, FarolSubmerso, Recife,
        TemploSal, JardimSal, CavernaCupula, Cristais, Mina, FendaRasgo, RochasFlutuantes, Ruina, Moinho, Obelisco, Pedreira, Moinhos, CidadeDistante,
        Cachoeira, Pinaculos, CascataLuminosa, TorresEspinhosas, NaviosPresos
    }

    public class LandmarkSpec
    {
        public LandmarkKind kind; public Vector2 pos; public float scale = 1f; public float yaw; public string name;
        public LandmarkSpec(LandmarkKind k, string n, Vector2 p, float s = 1f, float y = 0f) { kind = k; name = n; pos = p; scale = s; yaw = y; }
    }

    public class ZoneSpec
    {
        public string id, label; public Vector2 pos; public string[] ids; public int count = 2; public int tier = 1;
        public string requires = ""; public float radius = 7f; public bool respawn = true;
        public ZoneSpec(string id, string label, Vector2 p, int tier, int count, params string[] ids) { this.id = id; this.label = label; pos = p; this.tier = tier; this.count = count; this.ids = ids; }
    }

    public class ArenaSpec
    {
        public string noteId = "", miniboss = "", enemyId = "", requires = "";
        public Vector2 pos; public float radius = 30f; public float yaw;
        public LandmarkKind? dressing;
    }

    public class VortexSpec { public string id; public Vector2 pos; public float radius = 45f; public string activeWhen = ""; public bool offbeatBell; }

    public class PoiSpec
    {
        public PoiKind kind; public string title, desc = "", canonId = "", role = ""; public Vector2 pos;
        public PoiSpec(PoiKind k, string t, Vector2 p, string d = "", string canon = "", string role = "") { kind = k; title = t; pos = p; desc = d; canonId = canon; this.role = role; }
    }

    public class GateSpec
    {
        public string id, targetScene, targetGate, routeName, requires = "", lockedMessage = "";
        public Vector2 pos; public float yaw;   // yaw = direção de SAÍDA (para fora da região)
        public bool secret;
    }

    public class LockedSpec { public string name, reason; public Vector2 pos; public float yaw; public float width = 14f; }

    public class VegSpec
    {
        public float trees = 0.35f, rocks = 0.25f, grass = 0.6f, wheat = 0f;
        public string[] treeKinds = { "Tree_Oak", "Tree_Oak2", "Tree_Pine" };
        public bool deadTrees, giantTrees;
        public float crystals;
        public Color crystalColor = new Color(0.4f, 0.8f, 1f);
        public System.Func<float, float, float> density;   // 0..1 extra por posição (bosques, clareiras)
    }

    public class RegionLayout
    {
        public RegionId id;
        public Vector2 center = Vector2.zero;
        public float size = 1600f;
        public int heightRes = 1025;
        public System.Func<float, float, float> height;          // y do mundo (m)
        public System.Func<float, float, float, float, float> special;   // (x, z, y, inclinação) → camada A do chão
        public LayoutTextures tex = new LayoutTextures();
        public List<PathSpec> paths = new List<PathSpec>();
        public List<SettlementSpec> settlements = new List<SettlementSpec>();
        public List<LandmarkSpec> landmarks = new List<LandmarkSpec>();
        public List<ZoneSpec> zones = new List<ZoneSpec>();
        public List<ArenaSpec> arenas = new List<ArenaSpec>();
        public List<VortexSpec> vortices = new List<VortexSpec>();
        public List<PoiSpec> pois = new List<PoiSpec>();
        public List<GateSpec> gates = new List<GateSpec>();
        public List<LockedSpec> locked = new List<LockedSpec>();
        public List<Vector2> checkpoints = new List<Vector2>();
        public List<Vector2> route = new List<Vector2>();         // rota principal (teste de travessia)
        public List<List<Vector2>> rivers = new List<List<Vector2>>();
        public float riverWidth = 9f;
        public bool lavaRivers;                                    // Coroa de Cinza: os "rios" são de lava
        public float seaLevel = float.NegativeInfinity;
        public bool glassSea;
        public Color waterDeep = new Color(0.05f, 0.12f, 0.14f, 0.85f), waterSky = new Color(0.6f, 0.55f, 0.5f);
        public VegSpec veg = new VegSpec();
        public Vector2 spawn; public float spawnYaw;
        public Vector2 dungeonPos; public float dungeonYaw;
        public Color stoneTint = Color.white, roofTint = Color.white, woodTint = Color.white;
        public bool underground;                                   // Sombrafonte: cúpula de rocha
        public bool windowGlow;                                    // janelas acesas (reinos de noite/crepúsculo)
        public float horizonHeight = 260f;
        public string[] cinematic = new string[0];
    }
}

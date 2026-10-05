using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Campanula
{
    /// <summary>
    /// Capim e trigo de verdade (folhas e caules em 3D) no lugar dos "billboards" do terreno, que eram
    /// figurinhas planas com recorte serrilhado (o "capim low poly" e os riscos escuros no trigo).
    ///
    /// Os mapas de densidade continuam os do terreno (camadas 0 = capim, 1 = trigo, pintadas pelo
    /// builder); este componente lê esses mapas e, em pedaços de 10×10 m em volta de cada câmera, gera
    /// touceiras com posição, giro e tamanho aleatórios (sempre os mesmos: semente pelo pedaço). Cada
    /// touceira é uma malha gerada aqui (28 folhas curvas e afinando de 4 segmentos perto; 14 e 6 folhas
    /// mais longe), desenhada com GPU instancing (Graphics.DrawMeshInstanced, ~1 chamada por pedaço).
    /// O shader (Hidden/Campanula/GrassBlades) faz o vento (rajadas que correm pelo campo), o capim que
    /// se abre quando o jogador passa, a variação de cor (as mesmas manchas secas do terreno), a luz da
    /// lua atravessando as folhas, a luz da cidade e o sumiço gradual na borda (sem "estalo").
    ///
    /// A distância e a densidade continuam vindo de terrain.detailObjectDistance/Density (GameSettings e
    /// AdaptivePerformance já mexem nelas por qualidade); o terreno não desenha mais os detalhes dele.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        public Terrain terrain;
        public Texture2D macro;

        const float ChunkSize = 10f;
        const int MaxBatch = 1023;

        class Chunk
        {
            public readonly List<Matrix4x4[]> grass = new List<Matrix4x4[]>();
            public readonly List<Matrix4x4[]> wheat = new List<Matrix4x4[]>();
            public readonly List<int> grassN = new List<int>(), wheatN = new List<int>();
            public Bounds bounds;
            public int lastUse;
        }

        static readonly int IdDist = Shader.PropertyToID("_GrassDist"), IdWind = Shader.PropertyToID("_GrassWind"),
            IdPush = Shader.PropertyToID("_GrassPush"), IdMacro = Shader.PropertyToID("_GrassMacro");

        Material grassMat, wheatMat;
        Mesh[] grassLod, wheatLod;
        int[,] gMap, wMap;
        int dres;
        float cell;
        Vector3 tPos;
        float tSize;
        int nChunks;
        readonly Dictionary<int, Chunk> cache = new Dictionary<int, Chunk>();
        float builtDensity = -1f;
        readonly Plane[] planes = new Plane[6];
        Transform player;
        float nextPlayerSearch;
        public static GrassField Instance { get; private set; }

        void OnEnable()
        {
            Instance = this;
            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
        }

        void OnDisable()
        {
            Camera.onPreCull -= Draw;
            if (Instance == this) Instance = null;
            cache.Clear();
            gMap = wMap = null;
        }

        bool Init()
        {
            if (gMap != null) return true;
            if (terrain == null) terrain = GetComponent<Terrain>() ?? Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return false;
            var sh = Resources.Load<Shader>("Shaders/GrassBlades");
            if (sh == null || !sh.isSupported) return false;
            var td = terrain.terrainData;
            dres = td.detailResolution;
            if (dres <= 0 || td.detailPrototypes.Length < 2) return false;
            gMap = td.GetDetailLayer(0, 0, dres, dres, 0);
            wMap = td.GetDetailLayer(0, 0, dres, dres, 1);
            tPos = terrain.transform.position;
            tSize = td.size.x;
            cell = tSize / dres;
            nChunks = Mathf.CeilToInt(tSize / ChunkSize);

            grassMat = new Material(sh) { name = "Capim (folhas)", enableInstancing = true, hideFlags = HideFlags.DontSave };
            grassMat.SetColor("_Root", new Color(0.10f, 0.13f, 0.06f));
            grassMat.SetColor("_Tip", new Color(0.42f, 0.5f, 0.24f));
            grassMat.SetColor("_DryTint", new Color(1.25f, 1.05f, 0.62f));
            grassMat.SetFloat("_Sway", 1f);
            wheatMat = new Material(sh) { name = "Trigo (caules)", enableInstancing = true, hideFlags = HideFlags.DontSave };
            wheatMat.SetColor("_Root", new Color(0.32f, 0.27f, 0.12f));
            wheatMat.SetColor("_Tip", new Color(0.78f, 0.64f, 0.34f));
            wheatMat.SetColor("_Ear", new Color(0.86f, 0.7f, 0.38f));
            wheatMat.SetColor("_DryTint", new Color(1.08f, 0.98f, 0.86f));
            wheatMat.SetFloat("_Sway", 1.6f);
            if (macro != null) { grassMat.SetTexture(IdMacro, macro); wheatMat.SetTexture(IdMacro, macro); }

            grassLod = new[] { GrassClump(28, 4, 1f, 11), GrassClump(14, 2, 1.45f, 12), GrassClump(6, 1, 2.1f, 13) };
            wheatLod = new[] { WheatClump(9, true, 21), WheatClump(5, false, 22), WheatClump(3, false, 23) };
            return true;
        }

        // ------------------------------------------------------------ desenho

        void Draw(Camera cam)
        {
            if (cam == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (!Init()) return;
            int layer = gameObject.layer;
            if ((cam.cullingMask & (1 << layer)) == 0) return;
            float R = terrain.detailObjectDistance;
            float density = terrain.detailObjectDensity;
            if (R < 1f || density <= 0.01f) return;
            if (Mathf.Abs(density - builtDensity) > 0.02f) { cache.Clear(); builtDensity = density; }

            // vento e o jogador (globais, lidos pelo shader)
            Vector3 cp = cam.transform.position;
            Shader.SetGlobalVector(IdDist, new Vector4(R, R * 1.25f, 0f, 0f));
            Shader.SetGlobalVector(IdWind, new Vector4(0.8f, 0.6f, 1f, 1f));
            if (Application.isPlaying)
            {
                if (player == null && Time.unscaledTime > nextPlayerSearch)
                {
                    nextPlayerSearch = Time.unscaledTime + 1f;
                    var tpc = FindAnyObjectByType<Climbing.ThirdPersonController>();
                    if (tpc != null) player = tpc.transform;
                }
                var pp = player != null ? player.position : new Vector3(0f, -999f, 0f);
                Shader.SetGlobalVector(IdPush, new Vector4(pp.x, pp.y, pp.z, 1.1f));
            }
            else Shader.SetGlobalVector(IdPush, new Vector4(0f, -999f, 0f, 1f));

            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            float rw = R * 1.25f;   // o trigo é alto: aparece um pouco mais longe
            int c0x = Mathf.Max(0, Mathf.FloorToInt((cp.x - rw - tPos.x) / ChunkSize)), c1x = Mathf.Min(nChunks - 1, Mathf.FloorToInt((cp.x + rw - tPos.x) / ChunkSize));
            int c0z = Mathf.Max(0, Mathf.FloorToInt((cp.z - rw - tPos.z) / ChunkSize)), c1z = Mathf.Min(nChunks - 1, Mathf.FloorToInt((cp.z + rw - tPos.z) / ChunkSize));
            int frame = Time.frameCount;
            for (int cz = c0z; cz <= c1z; cz++)
                for (int cx = c0x; cx <= c1x; cx++)
                {
                    float x0 = tPos.x + cx * ChunkSize, z0 = tPos.z + cz * ChunkSize;
                    float dx = Mathf.Max(0f, Mathf.Max(x0 - cp.x, cp.x - (x0 + ChunkSize)));
                    float dz = Mathf.Max(0f, Mathf.Max(z0 - cp.z, cp.z - (z0 + ChunkSize)));
                    float d2 = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d2 > rw) continue;
                    int key = cz * nChunks + cx;
                    if (!cache.TryGetValue(key, out var ch)) { ch = Build(cx, cz, density); cache[key] = ch; }
                    ch.lastUse = frame;
                    if (ch.grass.Count == 0 && ch.wheat.Count == 0) continue;
                    float dy = Mathf.Max(0f, Mathf.Abs(cp.y - ch.bounds.center.y) - ch.bounds.extents.y);
                    float d = Mathf.Sqrt(d2 * d2 + dy * dy);
                    if (!GeometryUtility.TestPlanesAABB(planes, ch.bounds)) continue;
                    if (d <= R)
                    {
                        int lod = d < 12f ? 0 : d < 26f ? 1 : 2;
                        for (int i = 0; i < ch.grass.Count; i++)
                            Graphics.DrawMeshInstanced(grassLod[lod], 0, grassMat, ch.grass[i], ch.grassN[i], null, ShadowCastingMode.Off, true, layer, cam, LightProbeUsage.Off);
                    }
                    {
                        int lod = d < 14f ? 0 : d < 30f ? 1 : 2;
                        for (int i = 0; i < ch.wheat.Count; i++)
                            Graphics.DrawMeshInstanced(wheatLod[lod], 0, wheatMat, ch.wheat[i], ch.wheatN[i], null, ShadowCastingMode.Off, true, layer, cam, LightProbeUsage.Off);
                    }
                }
            // esquece pedaços que ficaram para trás (memória)
            if (cache.Count > 220)
            {
                var drop = new List<int>();
                foreach (var kv in cache) if (frame - kv.Value.lastUse > 120) drop.Add(kv.Key);
                foreach (var k in drop) cache.Remove(k);
            }
        }

        Chunk Build(int cx, int cz, float density)
        {
            var ch = new Chunk();
            var rnd = new System.Random(cz * 7919 + cx * 104729 + 17);
            float x0 = tPos.x + cx * ChunkSize, z0 = tPos.z + cz * ChunkSize;
            int i0 = Mathf.FloorToInt(cx * ChunkSize / cell), i1 = Mathf.Min(dres, Mathf.CeilToInt((cx + 1) * ChunkSize / cell));
            int j0 = Mathf.FloorToInt(cz * ChunkSize / cell), j1 = Mathf.Min(dres, Mathf.CeilToInt((cz + 1) * ChunkSize / cell));
            var g = new List<Matrix4x4>(); var w = new List<Matrix4x4>();
            float kG = 0.25f + 0.6f * density;    // touceiras por unidade do mapa (Média ~0,5; Alta ~0,85)
            float kW = 0.35f + 0.65f * density;
            float ymin = float.MaxValue, ymax = float.MinValue;
            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                {
                    int ng = gMap[j, i], nw = wMap[j, i];
                    if (ng <= 0 && nw <= 0) continue;
                    int cg = (int)(ng * kG + rnd.NextDouble());
                    int cw = (int)(nw * kW + rnd.NextDouble());
                    for (int k = 0; k < cg + cw; k++)
                    {
                        float x = tPos.x + (i + (float)rnd.NextDouble()) * cell;
                        float z = tPos.z + (j + (float)rnd.NextDouble()) * cell;
                        if (x < x0 || x >= x0 + ChunkSize || z < z0 || z >= z0 + ChunkSize) continue;
                        var p = new Vector3(x, 0f, z);
                        p.y = terrain.SampleHeight(p) + tPos.y - 0.03f;
                        ymin = Mathf.Min(ymin, p.y); ymax = Mathf.Max(ymax, p.y);
                        float s = 0.75f + (float)rnd.NextDouble() * 0.55f;
                        var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(s, s * (0.85f + (float)rnd.NextDouble() * 0.3f), s));
                        if (k < cg) g.Add(m); else w.Add(m);
                    }
                }
            Split(g, ch.grass, ch.grassN);
            Split(w, ch.wheat, ch.wheatN);
            if (ymin > ymax) { ymin = 0f; ymax = 0f; }
            ch.bounds = new Bounds(new Vector3(x0 + ChunkSize * 0.5f, (ymin + ymax) * 0.5f + 0.7f, z0 + ChunkSize * 0.5f),
                                   new Vector3(ChunkSize + 1.5f, ymax - ymin + 2.4f, ChunkSize + 1.5f));
            return ch;
        }

        static void Split(List<Matrix4x4> src, List<Matrix4x4[]> dst, List<int> counts)
        {
            for (int o = 0; o < src.Count; o += MaxBatch)
            {
                int n = Mathf.Min(MaxBatch, src.Count - o);
                var a = new Matrix4x4[n];
                src.CopyTo(o, a, 0, n);
                dst.Add(a); counts.Add(n);
            }
        }

        // ------------------------------------------------------------ malhas

        class MB
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public Mesh Make(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                var b = m.bounds; b.Expand(new Vector3(0.6f, 0.2f, 0.6f)); m.bounds = b;   // o vento empurra para fora
                return m;
            }
        }

        /// <summary>
        /// Uma folha: tira curva que afina até a ponta. Cor do vértice: r = altura relativa (0 raiz → 1
        /// ponta), g = número aleatório da folha, b = altura da folha (m, ÷2), a = 1 na espiga do trigo.
        /// </summary>
        static void Blade(MB b, Vector3 root, Vector3 face, float h, float wid, float bend, int segs, float rnd, float ear = 0f, float lean = 0f)
        {
            var up = Vector3.up;
            var side = Vector3.Cross(up, face).normalized;
            int start = b.v.Count;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                var p = root + up * (h * t) + face * (bend * h * t * t + lean * h * t);
                var tan = (up * h + face * (2f * bend * h * t + lean * h)).normalized;
                var nrm = Vector3.Cross(tan, side).normalized;
                float wt = wid * Mathf.Pow(1f - t, 0.75f);
                var col = new Color(t, rnd, h * 0.5f, ear);
                if (s < segs)
                {
                    b.v.Add(p - side * wt * 0.5f); b.v.Add(p + side * wt * 0.5f);
                    b.n.Add(nrm); b.n.Add(nrm);
                    b.c.Add(col); b.c.Add(col);
                    b.uv.Add(new Vector2(0f, t)); b.uv.Add(new Vector2(1f, t));
                }
                else
                {
                    b.v.Add(p); b.n.Add(nrm); b.c.Add(col); b.uv.Add(new Vector2(0.5f, 1f));
                }
            }
            for (int s = 0; s < segs; s++)
            {
                int a = start + s * 2;
                if (s < segs - 1) { b.t.Add(a); b.t.Add(a + 2); b.t.Add(a + 1); b.t.Add(a + 1); b.t.Add(a + 2); b.t.Add(a + 3); }
                else { b.t.Add(a); b.t.Add(a + 2); b.t.Add(a + 1); }
            }
        }

        /// <summary>Touceira de capim (~0,6 m): folhas mais altas no meio, as de fora curvando para fora.</summary>
        static Mesh GrassClump(int blades, int segs, float widK, int seed)
        {
            var r = new System.Random(seed);
            var b = new MB();
            for (int i = 0; i < blades; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float rr = Mathf.Sqrt((float)r.NextDouble()) * 0.3f;
                var root = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                var outw = rr > 0.02f ? new Vector3(root.x, 0f, root.z).normalized : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // a face da folha gira um pouco em volta da direção para fora (nem todas iguais)
                float twist = (float)(r.NextDouble() - 0.5) * 1.2f;
                var face = Quaternion.AngleAxis(twist * Mathf.Rad2Deg, Vector3.up) * outw;
                float h = (0.3f + (float)r.NextDouble() * 0.32f) * (1.1f - rr * 0.9f);
                float wid = (0.03f + (float)r.NextDouble() * 0.025f) * widK;
                float bend = 0.15f + (float)r.NextDouble() * 0.35f + rr * 0.6f;
                Blade(b, root, face, h, wid, bend, segs, (float)r.NextDouble());
            }
            return b.Make("Touceira de capim (" + blades + ")");
        }

        /// <summary>Touceira de trigo: caules finos, espiga (dois planos cruzados) e folhas na base.</summary>
        static Mesh WheatClump(int stalks, bool leaves, int seed)
        {
            var r = new System.Random(seed);
            var b = new MB();
            for (int i = 0; i < stalks; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float rr = Mathf.Sqrt((float)r.NextDouble()) * 0.22f;
                var root = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                var face = new Vector3(Mathf.Cos(a + 1.3f), 0f, Mathf.Sin(a + 1.3f));
                float h = 0.78f + (float)r.NextDouble() * 0.32f;
                float lean = 0.04f + (float)r.NextDouble() * 0.08f;
                float rnd = (float)r.NextDouble();
                // caule
                Blade(b, root, face, h, 0.012f * (stalks < 6 ? 1.8f : 1f), 0.05f, stalks < 6 ? 2 : 3, rnd, 0f, lean);
                // espiga: espiguetas alternadas (losangos pequenos inclinados para fora) em 1–2 planos
                // cruzados, na direção do topo do caule — de perto lê como grão de trigo, não como folha
                var top = root + Vector3.up * h + face * (0.05f * h + lean * h);
                var dir = (Vector3.up + face * (lean + 0.25f)).normalized;
                int planes = stalks < 4 ? 1 : 2, spk = stalks < 4 ? 2 : stalks < 6 ? 3 : 5;
                float L = 0.12f + (float)r.NextDouble() * 0.04f;
                float sw = (stalks < 6 ? 0.03f : 0.017f), sl = L / spk * 1.7f;
                for (int q = 0; q < planes; q++)
                {
                    var f2 = Quaternion.AngleAxis(q * 90f, dir) * face;
                    var side = Vector3.Cross(dir, f2).normalized;
                    for (int k = 0; k < spk; k++)
                    {
                        float tt = (k + 0.5f) / spk;
                        float sgn = (k & 1) == 0 ? 1f : -1f;
                        var c0 = top + dir * (L * tt * 0.85f);
                        var ax = (dir + side * sgn * 0.45f).normalized;     // a espigueta abre para o lado
                        var sd = Vector3.Cross(ax, f2).normalized;
                        float ww = sw * (1f - 0.35f * tt), ll = sl * (1f - 0.3f * tt);
                        int s0 = b.v.Count;
                        b.v.Add(c0); b.v.Add(c0 + ax * ll * 0.45f - sd * ww * 0.5f); b.v.Add(c0 + ax * ll * 0.45f + sd * ww * 0.5f); b.v.Add(c0 + ax * ll);
                        for (int e = 0; e < 4; e++) { b.n.Add(f2); b.c.Add(new Color(1f, rnd, h * 0.5f, 1f)); }
                        b.uv.Add(new Vector2(0.5f, 0f)); b.uv.Add(new Vector2(0f, 0.45f)); b.uv.Add(new Vector2(1f, 0.45f)); b.uv.Add(new Vector2(0.5f, 1f));
                        b.t.Add(s0); b.t.Add(s0 + 1); b.t.Add(s0 + 2); b.t.Add(s0 + 1); b.t.Add(s0 + 3); b.t.Add(s0 + 2);
                    }
                    // arista (a "barba" do trigo): uma lasca fina saindo da ponta
                    if (stalks >= 6)
                    {
                        int s1 = b.v.Count;
                        var tip = top + dir * L; var sd2 = side * 0.002f;
                        b.v.Add(tip - sd2); b.v.Add(tip + sd2); b.v.Add(tip + (dir + side * 0.15f).normalized * 0.06f);
                        for (int e = 0; e < 3; e++) { b.n.Add(f2); b.c.Add(new Color(1f, rnd, h * 0.5f, 1f)); b.uv.Add(new Vector2(0.5f, 1f)); }
                        b.t.Add(s1); b.t.Add(s1 + 1); b.t.Add(s1 + 2);
                    }
                }
                if (leaves)
                    for (int k = 0; k < 2; k++)
                    {
                        float la = a + k * 3.1f + (float)r.NextDouble();
                        var lf = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la));
                        Blade(b, root + Vector3.up * (0.05f + k * 0.12f), lf, 0.32f + (float)r.NextDouble() * 0.12f, 0.022f, 0.55f, 2, rnd);
                    }
            }
            return b.Make("Touceira de trigo (" + stalks + ")");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// A paisagem além de Campanula (escala da abertura): um anel de terreno procedural de ~11 km
    /// em volta do terreno jogável — morros que continuam os da vila, o vale do leste com o rio,
    /// florestas, vilarejos com luzes, o planalto do leste (onde a nota cai) e as cordilheiras: a do
    /// norte tem um passo exatamente na direção da Fenda, e por ele se vê a serra mais distante
    /// atrás da qual a Fenda nasce. Desenhado com Hidden/Aren/FarLand (luz da lua, neve, névoa de
    /// altura e perspectiva aérea próprias, luz da Fenda e do impacto, onda de choque) — sem o fog
    /// do Unity, que apagaria tudo depois de 400 m. Sem colisor (é só paisagem).
    /// </summary>
    public static class FarLands
    {
        public static Transform Root { get; private set; }
        public static Vector3 ImpactPoint { get; private set; }
        static Material mat;
        static readonly List<Vector3> villageLights = new List<Vector3>();

        // geometria
        const float Edge = 160f;          // meia largura do terreno jogável
        // duas malhas: perto (borda → 2,7 km) e longe (2,6 → 11,2 km, mais divisões no azimute para
        // as cristas das montanhas não ficarem poligonais contra o céu)
        const float Split = 2700f, MaxR = 11200f;

        // impacto da nota: planalto a les-nordeste, ~2,5 km do centro da vila
        public const float ImpactAzimuth = 1.22f, ImpactRadius = 2500f;

        public static void Build()
        {
            if (Root != null) return;
            var sh = Resources.Load<Shader>("Shaders/FarLand");
            if (sh == null || !sh.isSupported) { Debug.LogWarning("[Paisagem] FarLand indisponível"); return; }
            mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            mat.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_night"));
            Root = new GameObject("Paisagem distante").transform;
            var t0 = Time.realtimeSinceStartup;
            BuildBand(8, 360, 0f, Split, 88);
            BuildBand(12, 1080, Split * 0.97f, MaxR, 58);
            var ip = new Vector3(Mathf.Sin(ImpactAzimuth), 0f, Mathf.Cos(ImpactAzimuth)) * ImpactRadius;
            ip.y = Height(ip.x, ip.z);
            ImpactPoint = ip;
            Shader.SetGlobalVector("_ImpactPosW", ip);
            BuildVillageLights();
            Debug.Log($"[Paisagem] 20 setores em {(Time.realtimeSinceStartup - t0) * 1000f:0} ms; impacto em {ip}");
        }

        public static void Destroy()
        {
            if (Root != null) Object.DestroyImmediate(Root.gameObject);
            Root = null;
        }

        // ------------------------------------------------------------ altura

        static float Gauss(float d, float w) => Mathf.Exp(-(d * d) / (w * w));
        static float DAz(float a, float b) { float d = a - b; return d - 6.2831853f * Mathf.Floor((d + 3.14159265f) / 6.2831853f); }
        static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Mesmo GroundY do CampanulaBuilder (o terreno jogável): a paisagem continua dele na borda.</summary>
        public static float GroundY(float x, float z)
        {
            float h = 0f;
            float hill = Campanula.Relief.HillMask(x, z);
            h += Campanula.Relief.Hills(x, z);
            bool village = x > -50f && x < 38f && z > -44f && z < 52f;
            if (!village) h += (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f) * 0.8f * (1f - hill);
            float dx = Mathf.Abs(x - Campanula.StreamMath.Center(z));
            float bed = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 5.5f, dx));
            h -= bed * 1.7f;
            return h;
        }

        static float Fbm(float x, float z, int oct)
        {
            float s = 0f, a = 0.5f, f = 1f;
            for (int o = 0; o < oct; o++) { s += (Mathf.PerlinNoise(x * f + o * 13.7f, z * f + o * 7.3f) - 0.5f) * a; a *= 0.5f; f *= 2.03f; }
            return s;   // ~ -0.5..0.5
        }

        /// <summary>Ruído "multifractal de cristas": picos afiados e vales largos (cordilheiras).</summary>
        static float Ridged(float x, float z)
        {
            float s = 0f, a = 0.55f, f = 1f, prev = 1f;
            for (int o = 0; o < 5; o++)
            {
                float n = Mathf.PerlinNoise(x * f + o * 31.7f, z * f + o * 17.9f);
                n = 1f - Mathf.Abs(2f * n - 1f); n *= n;
                s += n * a * prev; prev = Mathf.Clamp01(n * 1.4f);
                a *= 0.5f; f *= 2.07f;
            }
            return s;
        }

        // o rio do vale do leste: desce do passo norte e segue para o sudeste (x leste, z norte)
        static readonly Vector2[] River =
        {
            new Vector2(1250f, 4600f), new Vector2(820f, 3400f), new Vector2(980f, 2300f), new Vector2(640f, 1500f),
            new Vector2(760f, 700f), new Vector2(1150f, -150f), new Vector2(1600f, -1100f), new Vector2(1380f, -2400f),
            new Vector2(1900f, -3800f), new Vector2(2400f, -5200f),
        };

        static float RiverDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = 1e9f;
            for (int i = 0; i < River.Length - 1; i++)
            {
                Vector2 a = River[i], b = River[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
            }
            // meandro: desloca a distância com ruído para não parecer uma polilinha
            return Mathf.Sqrt(best) + Fbm(x * 0.004f, z * 0.004f, 2) * 70f;
        }

        /// <summary>Altura (y mundo) da paisagem distante. 'detail' guarda as máscaras para a cor.</summary>
        public static float Height(float x, float z) => Height(x, z, out _, out _);

        static float Height(float x, float z, out float riverD, out float plateau)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float az = Mathf.Atan2(x, z);
            float edge = Edge / Mathf.Max(Mathf.Abs(Mathf.Sin(az)), Mathf.Abs(Mathf.Cos(az)));
            float beyond = r - edge;
            float near = GroundY(x, z);

            // planícies baixas e onduladas (o vale)
            float h = -22f + Fbm(x * 0.0011f + 5f, z * 0.0011f + 2f, 4) * 70f;
            // morros perto da vila (continuam os do terreno e vão baixando para o vale)
            float opening = 1f - 0.85f * Gauss(DAz(az, 1.15f), 0.42f);   // a leste o chão desce para o vale (o planalto aparece da estrada)
            h += 40f * (1f - S(300f, 1100f, r)) * (0.6f + Fbm(x * 0.004f, z * 0.004f, 3)) * opening;
            h -= (1f - opening) * 18f * S(250f, 700f, r);
            // planalto do leste (onde a nota cai): sobe ~270 m, bordas suaves, topo ondulado
            plateau = Gauss(DAz(az, ImpactAzimuth), 0.42f) * S(1500f, 2150f, r) * (1f - S(3600f, 4300f, r));
            h += plateau * (330f + Fbm(x * 0.002f, z * 0.002f, 3) * 60f);
            // cordilheiras
            float wx = x + Fbm(x * 0.00021f, z * 0.00021f, 2) * 1400f, wz = z + Fbm(z * 0.00021f + 9f, x * 0.00021f, 2) * 1400f;
            float m = Ridged(wx * 0.00034f, wz * 0.00034f);
            float nRange = S(3200f, 4400f, r) * (1f - S(6800f, 8200f, r));
            float amp = 0f;
            amp += Gauss(DAz(az, -0.45f), 0.42f) * nRange * 1750f;                          // maciço a noroeste (o mais alto)
            amp += Gauss(DAz(az, 0.02f), 0.24f) * nRange * 1250f;                           // norte
            amp += Gauss(DAz(az, 0.72f), 0.28f) * S(4600f, 5800f, r) * (1f - S(7600f, 9000f, r)) * 1050f;   // nordeste, mais longe
            amp += Gauss(DAz(az, 1.55f), 0.5f) * S(4500f, 5800f, r) * (1f - S(8200f, 9600f, r)) * 1250f;    // leste, atrás do planalto
            float southW = 1f - Mathf.Max(Gauss(DAz(az, -0.2f), 0.9f), Gauss(DAz(az, 1.3f), 0.6f));
            amp += southW * S(2700f, 4500f, r) * (1f - S(7000f, 8600f, r)) * 620f;           // serras baixas ao sul/oeste
            amp *= 1f - 0.86f * Gauss(DAz(az, NightSetup.FendaAz), 0.15f);                  // o passo na direção da Fenda
            amp += S(8200f, 9800f, r) * (700f + 500f * Mathf.PerlinNoise(az * 2.2f + 10f, 3.3f));   // a serra mais distante, em volta
            h += amp * m;
            // rio: leito escavado no vale
            riverD = RiverDist(x, z);
            float bed = 1f - S(30f, 160f, riverD);
            h = Mathf.Lerp(h, Mathf.Min(h, -34f), bed * (1f - plateau));
            // junta com o terreno jogável na borda (mesma função lá)
            float k = S(20f, 520f, beyond);
            return Mathf.Lerp(near, h, k);
        }

        // ------------------------------------------------------------ malha

        /// <summary>
        /// Uma faixa da paisagem (anéis de r0 a r1, r0 = 0 começa na borda quadrada do terreno):
        /// a grade inteira é calculada uma vez (uma altura por vértice; normais pelos vizinhos, com a
        /// volta fechada) e depois cortada em setores, para o frustum culling.
        /// </summary>
        static void BuildBand(int sectors, int azSeg, float r0, float r1, int rings)
        {
            var P = new Vector3[azSeg, rings];
            var rdA = new float[azSeg, rings];
            var plA = new float[azSeg, rings];
            var rA = new float[azSeg, rings];
            for (int ia = 0; ia < azSeg; ia++)
            {
                float az = ia / (float)azSeg * 6.2831853f;
                float sa = Mathf.Sin(az), ca = Mathf.Cos(az);
                float edge = Edge / Mathf.Max(Mathf.Abs(sa), Mathf.Abs(ca));
                float inner = r0 <= 0f ? edge : r0;
                for (int ir = 0; ir < rings; ir++)
                {
                    float u = ir / (float)(rings - 1);
                    float r = inner * Mathf.Pow(r1 / inner, u);     // espaçamento geométrico
                    if (r0 <= 0f)
                    {
                        // o anel interno segue a borda quadrada do terreno; os de fora ficam redondos
                        float bl = S(0f, 1f, u / 0.35f);
                        float rc = Edge * Mathf.Pow(r1 / Edge, u);
                        r = Mathf.Lerp(r, rc, bl);
                        if (ir == 0) r = edge;
                    }
                    float x = sa * r, z = ca * r;
                    float h = Height(x, z, out float rd, out float plat);
                    if (r0 <= 0f && ir == 0) h -= 0.6f;     // a primeira volta fica um pouco abaixo do terreno (sem z-fighting)
                    if (r0 > 0f && ir == 0) h -= 3f;        // saia: a malha de longe começa por baixo da de perto
                    P[ia, ir] = new Vector3(x, h, z);
                    rdA[ia, ir] = rd; plA[ia, ir] = plat; rA[ia, ir] = r;
                }
            }
            for (int s = 0; s < sectors; s++)
            {
                int a0 = s * azSeg / sectors, a1 = (s + 1) * azSeg / sectors;
                int na = a1 - a0 + 1;
                var verts = new Vector3[na * rings];
                var norms = new Vector3[na * rings];
                var cols = new Color32[na * rings];
                for (int ia = 0; ia < na; ia++)
                {
                    int ga = (a0 + ia) % azSeg;
                    int gp = (ga + 1) % azSeg, gm = (ga - 1 + azSeg) % azSeg;
                    for (int ir = 0; ir < rings; ir++)
                    {
                        int vi = ia * rings + ir;
                        var p = P[ga, ir];
                        verts[vi] = p;
                        Vector3 dr = P[ga, Mathf.Min(ir + 1, rings - 1)] - P[ga, Mathf.Max(ir - 1, 0)];
                        Vector3 da = P[gp, ir] - P[gm, ir];
                        var n = Vector3.Cross(dr, da).normalized;
                        if (n.y < 0f) n = -n;
                        norms[vi] = n;
                        cols[vi] = Masks(p.x, p.y, p.z, n.y, rdA[ga, ir], plA[ga, ir], rA[ga, ir]);
                    }
                }
                int quads = (na - 1) * (rings - 1);
                var tris = new int[quads * 6];
                int t = 0;
                for (int ia = 0; ia < na - 1; ia++)
                    for (int ir = 0; ir < rings - 1; ir++)
                    {
                        int v00 = ia * rings + ir, v01 = v00 + 1, v10 = v00 + rings, v11 = v10 + 1;
                        tris[t++] = v00; tris[t++] = v01; tris[t++] = v10;
                        tris[t++] = v10; tris[t++] = v01; tris[t++] = v11;
                    }
                var mesh = new Mesh { name = "FarLand_" + r1 + "_" + s, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.vertices = verts; mesh.normals = norms; mesh.colors32 = cols; mesh.triangles = tris;
                mesh.RecalculateBounds();
                var go = new GameObject((r0 > 0f ? "Longe_" : "Perto_") + s);
                go.transform.SetParent(Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        /// <summary>Máscaras na cor do vértice: r floresta, g rocha, b neve, a água.</summary>
        static Color32 Masks(float x, float h, float z, float ny, float riverD, float plateau, float r)
        {
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / Mathf.Max(ny, 0.05f);   // tangente da inclinação
            float n = Mathf.PerlinNoise(x * 0.0021f + 3f, z * 0.0021f + 1f);
            float n2 = Mathf.PerlinNoise(x * 0.009f + 7f, z * 0.009f + 4f);
            float forest = Mathf.Clamp01((n * 0.75f + n2 * 0.5f - 0.42f) * 3.2f) * (1f - S(450f, 750f, h)) * (1f - S(0.9f, 1.6f, slope));
            forest = Mathf.Max(forest, plateau * Mathf.Clamp01((n2 - 0.25f) * 3f));   // o planalto é de floresta
            float rock = Mathf.Clamp01(S(0.55f, 1.1f, slope) + S(600f, 900f, h) * 0.6f);
            float snow = S(720f + n * 260f, 900f + n * 260f, h) * (1f - S(1.5f, 2.6f, slope));
            float water = 1f - S(14f, 34f, riverD);
            if (r < 400f) water = 0f;
            return new Color32((byte)(forest * 255), (byte)(rock * 255), (byte)(snow * 255), (byte)(water * 255));
        }

        // ------------------------------------------------------------ luzes de outros lugares

        static void BuildVillageLights()
        {
            villageLights.Clear();
            var rnd = new System.Random(7);
            void Cluster(float x, float z, int n, float spread)
            {
                for (int i = 0; i < n; i++)
                {
                    float px = x + (float)(rnd.NextDouble() - 0.5) * spread, pz = z + (float)(rnd.NextDouble() - 0.5) * spread;
                    villageLights.Add(new Vector3(px, Height(px, pz) + 3f + (float)rnd.NextDouble() * 4f, pz));
                }
            }
            Cluster(-950f, 1850f, 9, 180f);     // vila no vale noroeste
            Cluster(1480f, -900f, 5, 110f);     // fazendas a sudeste, perto do rio
            Cluster(-2300f, -650f, 12, 260f);   // vila a oeste
            Cluster(620f, 3150f, 6, 140f);      // aldeia no caminho do passo norte
            Cluster(2650f, -2450f, 16, 320f);   // cidade distante a sudeste
            Cluster(-1600f, 4400f, 4, 120f);    // sopé ao norte
            Cluster(-3600f, 2600f, 7, 240f);
            Cluster(3900f, -600f, 6, 220f);     // além do planalto
            for (int i = 0; i < 14; i++)        // casas soltas
            {
                float a = (float)rnd.NextDouble() * 6.283f, rr = 700f + (float)rnd.NextDouble() * 2600f;
                if (Mathf.Abs(DAz(a, ImpactAzimuth)) < 0.35f && rr > 1600f) continue;   // ninguém mora onde a nota cai
                Cluster(Mathf.Sin(a) * rr, Mathf.Cos(a) * rr, 1, 0f);
            }
            if (NightSetup.GlowMat == null) return;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var parent = new GameObject("Luzes distantes").transform;
            parent.SetParent(Root, false);
            var mpb = new MaterialPropertyBlock();
            foreach (var p in villageLights)
            {
                var go = new GameObject("Luz");
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                float d = p.magnitude;
                float size = Mathf.Max(5f, d * 0.0042f);   // pelo menos ~1-2 px de longe
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = NightSetup.GlowMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                var c = new Color(1f, 0.62f + (float)rnd.NextDouble() * 0.15f, 0.3f, 0.75f);
                mpb.SetColor("_Color", c);
                mr.SetPropertyBlock(mpb);
                go.AddComponent<Flicker>().baseColor = c;
            }
        }
    }
}

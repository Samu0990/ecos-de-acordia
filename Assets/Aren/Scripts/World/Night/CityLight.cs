using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// "Luz da cidade" (luz falsa, calculada uma vez): cada janela acesa, lanterna e tocha da vila vira
    /// uma mancha gaussiana de luz quente num mapa visto de cima (RGB = luz somada, A = altura média das
    /// fontes). Os materiais da vila (Campanula/CityLit) e uma malha no chão (Hidden/Aren/CityLightGround)
    /// leem esse mapa: a pedra perto das fontes fica dourada e as ruas ganham poças de luz — a cidade
    /// brilhando da referência — sem nenhuma luz em tempo real (no Intel UHD cada luz pontual é uma
    /// passada extra). De dia K = 0.
    /// </summary>
    public static class CityLight
    {
        // área coberta: a vila, a cidade além dos muros, o desfiladeiro, o Grande Aqueduto e a estrada até o
        // sino (0,7 m por texel)
        public static readonly Rect Area = new Rect(-140f, -112f, 210f, 212f);
        const int W = 300, H = 303;
        struct L { public Vector3 p; public Color c; public float r; }
        static readonly List<L> lights = new List<L>();
        static Texture2D tex;
        static GameObject ground;
        public static float Intensity = 1.6f;
        static readonly int IdTex = Shader.PropertyToID("_CityLightTex"), IdRect = Shader.PropertyToID("_CityLightRect"), IdK = Shader.PropertyToID("_CityLightK");

        public static void Begin() { lights.Clear(); }

        /// <summary>Uma fonte: posição, cor×intensidade e raio (m, ~onde a luz cai a 1/e).</summary>
        public static void Add(Vector3 p, Color c, float radius) { lights.Add(new L { p = p, c = c, r = radius }); }

        public static int Count => lights.Count;

        public static void Bake(bool groundMesh = true)
        {
            var rgb = new Vector3[W * H];
            var hw = new float[W * H];
            var wsum = new float[W * H];
            float sx = Area.width / W, sz = Area.height / H;
            foreach (var l in lights)
            {
                int cx = Mathf.RoundToInt((l.p.x - Area.xMin) / sx), cz = Mathf.RoundToInt((l.p.z - Area.yMin) / sz);
                int rr = Mathf.CeilToInt(l.r * 2.2f / sx);
                float inv = 1f / (l.r * l.r);
                for (int z = Mathf.Max(0, cz - rr); z <= Mathf.Min(H - 1, cz + rr); z++)
                    for (int x = Mathf.Max(0, cx - rr); x <= Mathf.Min(W - 1, cx + rr); x++)
                    {
                        float dx = (x - cx) * sx, dz = (z - cz) * sz;
                        float w = Mathf.Exp(-(dx * dx + dz * dz) * inv);
                        if (w < 0.01f) continue;
                        int i = z * W + x;
                        rgb[i] += new Vector3(l.c.r, l.c.g, l.c.b) * w;
                        hw[i] += l.p.y * w; wsum[i] += w;
                    }
            }
            if (tex == null) tex = new Texture2D(W, H, TextureFormat.RGBAHalf, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "Luz da cidade" };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                var v = rgb[i];
                // compressão suave: muitas janelas juntas não estouram (a pedra não vira branco)
                float m = Mathf.Max(v.x, Mathf.Max(v.y, v.z));
                float k = m > 0.0001f ? (1f - Mathf.Exp(-m * 1.2f)) / (m * 1.2f) * 1.2f : 1f;
                px[i] = new Color(v.x * k, v.y * k, v.z * k, wsum[i] > 0.0001f ? hw[i] / wsum[i] : 0f);
            }
            tex.SetPixels(px); tex.Apply(false);
            Shader.SetGlobalTexture(IdTex, tex);
            Shader.SetGlobalVector(IdRect, new Vector4(Area.xMin, Area.yMin, 1f / Area.width, 1f / Area.height));
            Shader.SetGlobalFloat(IdK, Intensity);
            if (groundMesh && ground == null) BuildGround();
            Debug.Log($"[Noite] luz da cidade: {lights.Count} fontes");
        }

        public static void Off()
        {
            Shader.SetGlobalFloat(IdK, 0f);
            if (ground == null) ground = GameObject.Find("Luz da cidade (chão)");
            if (ground != null) { if (Application.isPlaying) Object.Destroy(ground); else Object.DestroyImmediate(ground); }
            ground = null;
        }

        /// <summary>Malha que acompanha o terreno (só o terreno: telhados e pontes não entram) para as poças no chão.</summary>
        static void BuildGround()
        {
            var sh = Resources.Load<Shader>("Shaders/CityLightGround");
            var terrain = Terrain.activeTerrain;
            if (sh == null || !sh.isSupported || terrain == null) return;
            // o terreno v2 (Campanula/Terrain) já soma a luz da cidade no próprio pixel: sem malha extra
            if (terrain.materialTemplate != null && terrain.materialTemplate.shader.name == "Campanula/Terrain") return;
            const int N = 150, M = 152;
            var verts = new Vector3[(N + 1) * (M + 1)];
            for (int j = 0; j <= M; j++)
                for (int i = 0; i <= N; i++)
                {
                    float x = Area.xMin + Area.width * i / N, z = Area.yMin + Area.height * j / M;
                    var p = new Vector3(x, 0f, z);
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.04f;
                    verts[j * (N + 1) + i] = p;
                }
            var tris = new int[N * M * 6];
            int t = 0;
            for (int j = 0; j < M; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i;
                    tris[t++] = a; tris[t++] = a + N + 1; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = a + N + 1; tris[t++] = a + N + 2;
                }
            var mesh = new Mesh { name = "Luz no chão", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.triangles = tris; mesh.RecalculateBounds();
            ground = new GameObject("Luz da cidade (chão)");
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ground.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(sh) { hideFlags = HideFlags.DontSave };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }
    }
}

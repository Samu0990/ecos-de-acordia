using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Malhas procedurais do construtor do mundo (placeholders "inteligentes": formas com silhueta própria
    /// em vez de caixas cruas). Toda malha é salva como asset em Assets/World/Meshes (reaproveitada entre
    /// cenas e fora dos arquivos .unity, que ficam pequenos). Faces com vértices próprios = facetas duras.
    /// </summary>
    public static class ProcMesh
    {
        public const string Dir = "Assets/World/Meshes";
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        class B
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var nn = Vector3.Cross(b - a, c - a).normalized;
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn);
                uv.Add(new Vector2(a.x + a.z, a.y)); uv.Add(new Vector2(b.x + b.z, b.y)); uv.Add(new Vector2(c.x + c.z, c.y));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Mesh Save(string key, B b)
        {
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            System.IO.Directory.CreateDirectory(Dir);
            string path = Dir + "/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var m = b.Build(key);
            if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(m, existing); m = existing; }
            else AssetDatabase.CreateAsset(m, path);
            cache[key] = m;
            return m;
        }

        public static void ClearCache() => cache.Clear();

        /// <summary>Prisma de n lados (raio embaixo/em cima), base em y=0 — torres, pilares, chaminés, troncos.</summary>
        public static Mesh Prism(int sides, float r0, float r1, float h, bool cap = true, float twist = 0f)
        {
            string key = $"prism_{sides}_{r0:0.##}_{r1:0.##}_{h:0.##}_{(cap ? 1 : 0)}_{twist:0.#}";
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var b = new B();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                float tw = twist * Mathf.Deg2Rad;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var q0 = new Vector3(Mathf.Cos(a0 + tw), 0, Mathf.Sin(a0 + tw)); var q1 = new Vector3(Mathf.Cos(a1 + tw), 0, Mathf.Sin(a1 + tw));
                Vector3 A = p0 * r0, Bv = p1 * r0, C = q1 * r1 + Vector3.up * h, D = q0 * r1 + Vector3.up * h;
                b.Quad(A, D, C, Bv);
                if (cap)
                {
                    b.Tri(Vector3.zero, A, Bv);
                    if (r1 > 0.001f) b.Tri(Vector3.up * h, C, D);
                }
            }
            return Save(key, b);
        }

        /// <summary>Caixa (centro na base) — muros, degraus, placas, lajes.</summary>
        public static Mesh Box(float sx, float sy, float sz)
        {
            string key = $"box_{sx:0.##}_{sy:0.##}_{sz:0.##}";
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var b = new B();
            float x = sx / 2, z = sz / 2;
            Vector3 p(float a, float yy, float cc) => new Vector3(a, yy, cc);
            b.Quad(p(-x, 0, -z), p(-x, sy, -z), p(x, sy, -z), p(x, 0, -z));
            b.Quad(p(x, 0, z), p(x, sy, z), p(-x, sy, z), p(-x, 0, z));
            b.Quad(p(-x, 0, z), p(-x, sy, z), p(-x, sy, -z), p(-x, 0, -z));
            b.Quad(p(x, 0, -z), p(x, sy, -z), p(x, sy, z), p(x, 0, z));
            b.Quad(p(-x, sy, -z), p(-x, sy, z), p(x, sy, z), p(x, sy, -z));
            b.Quad(p(-x, 0, z), p(-x, 0, -z), p(x, 0, -z), p(x, 0, z));
            return Save(key, b);
        }

        /// <summary>Cristal: bipirâmide hexagonal alongada e levemente torta (seed muda a forma).</summary>
        public static Mesh Crystal(int seed)
        {
            string key = "crystal_" + seed;
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var r = new System.Random(seed);
            var b = new B();
            int s = 6; float h = 1f, mid = 0.62f + (float)r.NextDouble() * 0.15f;
            var ring = new Vector3[s];
            for (int i = 0; i < s; i++) { float a = i * Mathf.PI * 2 / s; float rr = 0.18f + (float)r.NextDouble() * 0.07f; ring[i] = new Vector3(Mathf.Cos(a) * rr, mid * h, Mathf.Sin(a) * rr); }
            var top = new Vector3(((float)r.NextDouble() - 0.5f) * 0.12f, h, ((float)r.NextDouble() - 0.5f) * 0.12f);
            for (int i = 0; i < s; i++)
            {
                var a = ring[i]; var bb = ring[(i + 1) % s];
                var a0 = new Vector3(a.x * 0.85f, 0, a.z * 0.85f); var b0 = new Vector3(bb.x * 0.85f, 0, bb.z * 0.85f);
                b.Quad(a0, a, bb, b0);
                b.Tri(a, top, bb);
            }
            return Save(key, b);
        }

        /// <summary>Esfera facetada (lat/long) — copas estilizadas, cúpulas, núcleos, pedras grandes.</summary>
        public static Mesh Sphere(int seg, float noise = 0f, int seed = 0, bool hemisphere = false, bool inward = false)
        {
            string key = $"sphere_{seg}_{noise:0.##}_{seed}_{(hemisphere ? 1 : 0)}_{(inward ? 1 : 0)}";
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var r = new System.Random(seed);
            int rings = hemisphere ? seg / 2 : seg;
            var pts = new Vector3[rings + 1, seg + 1];
            for (int y = 0; y <= rings; y++)
                for (int x = 0; x <= seg; x++)
                {
                    float th = (hemisphere ? 0.5f : 1f) * Mathf.PI * y / rings;
                    float ph = 2 * Mathf.PI * (x % seg) / seg;
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    pts[y, x] = p;
                }
            if (noise > 0)
            {
                var disp = new Dictionary<Vector3Int, float>();
                for (int y = 0; y <= rings; y++)
                    for (int x = 0; x <= seg; x++)
                    {
                        var k = Vector3Int.RoundToInt(pts[y, x] * 1000);
                        if (!disp.TryGetValue(k, out float d)) { d = 1f + ((float)r.NextDouble() - 0.5f) * 2f * noise; disp[k] = d; }
                        pts[y, x] *= d;
                    }
            }
            var b = new B();
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    Vector3 a = pts[y, x], bq = pts[y, x + 1], cq = pts[y + 1, x + 1], d = pts[y + 1, x];
                    if (inward) b.Quad(a, d, cq, bq); else b.Quad(a, bq, cq, d);
                }
            return Save(key, b);
        }

        /// <summary>Arco (portal) de largura w, altura h, espessura d e vão — portões de rota, entradas.</summary>
        public static Mesh Arch(float w, float h, float d, float thick)
        {
            string key = $"arch_{w:0.#}_{h:0.#}_{d:0.#}_{thick:0.#}";
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var b = new B();
            float hw = w / 2, z = d / 2, spring = h - hw;   // começo do arco
            int seg = 10;
            // pilares
            void Block(Vector3 mn, Vector3 mx)
            {
                Vector3 p(float x, float y, float zz) => new Vector3(x, y, zz);
                b.Quad(p(mn.x, mn.y, mn.z), p(mn.x, mx.y, mn.z), p(mx.x, mx.y, mn.z), p(mx.x, mn.y, mn.z));
                b.Quad(p(mx.x, mn.y, mx.z), p(mx.x, mx.y, mx.z), p(mn.x, mx.y, mx.z), p(mn.x, mn.y, mx.z));
                b.Quad(p(mn.x, mn.y, mx.z), p(mn.x, mx.y, mx.z), p(mn.x, mx.y, mn.z), p(mn.x, mn.y, mn.z));
                b.Quad(p(mx.x, mn.y, mn.z), p(mx.x, mx.y, mn.z), p(mx.x, mx.y, mx.z), p(mx.x, mn.y, mx.z));
                b.Quad(p(mn.x, mx.y, mn.z), p(mn.x, mx.y, mx.z), p(mx.x, mx.y, mx.z), p(mx.x, mx.y, mn.z));
            }
            Block(new Vector3(-hw - thick, 0, -z), new Vector3(-hw, spring, z));
            Block(new Vector3(hw, 0, -z), new Vector3(hw + thick, spring, z));
            // arco ogival (gótico) em segmentos
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg;
                Vector3 In(float a) => new Vector3(-Mathf.Cos(a) * hw, spring + Mathf.Sin(a) * hw * 1.25f, 0);
                Vector3 Out(float a) => new Vector3(-Mathf.Cos(a) * (hw + thick), spring + Mathf.Sin(a) * (hw * 1.25f + thick), 0);
                Vector3 zf = new Vector3(0, 0, -z), zb = new Vector3(0, 0, z);
                b.Quad(In(a0) + zf, Out(a0) + zf, Out(a1) + zf, In(a1) + zf);
                b.Quad(In(a1) + zb, Out(a1) + zb, Out(a0) + zb, In(a0) + zb);
                b.Quad(Out(a0) + zf, Out(a0) + zb, Out(a1) + zb, Out(a1) + zf);
                b.Quad(In(a1) + zf, In(a1) + zb, In(a0) + zb, In(a0) + zf);
            }
            return Save(key, b);
        }

        /// <summary>Escadaria reta (n degraus) — a Escadaria Depois do Fim, rampas de arena, terraços.</summary>
        public static Mesh Stairs(int steps, float w, float rise, float run)
        {
            string key = $"stairs_{steps}_{w:0.#}_{rise:0.##}_{run:0.##}";
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            var b = new B();
            float hw = w / 2;
            for (int i = 0; i < steps; i++)
            {
                float y0 = i * rise, y1 = (i + 1) * rise, z0 = i * run, z1 = (i + 1) * run;
                b.Quad(new Vector3(-hw, y1, z0), new Vector3(-hw, y1, z1), new Vector3(hw, y1, z1), new Vector3(hw, y1, z0));   // pisada
                b.Quad(new Vector3(-hw, y0, z0), new Vector3(-hw, y1, z0), new Vector3(hw, y1, z0), new Vector3(hw, y0, z0));   // espelho
                b.Quad(new Vector3(-hw, 0, z1), new Vector3(-hw, y1, z1), new Vector3(-hw, y1, z0), new Vector3(-hw, 0, z0));
                b.Quad(new Vector3(hw, 0, z0), new Vector3(hw, y1, z0), new Vector3(hw, y1, z1), new Vector3(hw, 0, z1));
            }
            float top = steps * rise, end = steps * run;
            b.Quad(new Vector3(hw, 0, end), new Vector3(hw, top, end), new Vector3(-hw, top, end), new Vector3(-hw, 0, end));
            return Save(key, b);
        }

        /// <summary>Anel de montanhas no horizonte (silhueta), alturas por azimute (picos onde ficam os reinos vizinhos).</summary>
        public static Mesh HorizonRing(string key, float radius, System.Func<float, float> heightAt, float baseY, int seg = 180)
        {
            var b = new B();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                float h0 = heightAt(a0), h1 = heightAt(a1);
                Vector3 A = d0 * radius + Vector3.up * baseY, Bv = d1 * radius + Vector3.up * baseY;
                Vector3 C = d1 * radius * 0.985f + Vector3.up * (baseY + h1), D = d0 * radius * 0.985f + Vector3.up * (baseY + h0);
                b.Quad(A, D, C, Bv);   // virada para dentro (o jogador está no centro)
            }
            return Save("horizon_" + key, b);
        }

        /// <summary>Tenda / cone baixo de n lados (acampamentos, telhados de mercado noturno).</summary>
        public static Mesh Cone(int sides, float r, float h) => Prism(sides, r, 0.001f, h, true);
    }
}

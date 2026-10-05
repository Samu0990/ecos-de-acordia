using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// A Catedral dos Sinos, ao norte de Campanula (fora da muralha, sem acesso): a silhueta gótica
    /// que o storyboard tem atrás da vila — nave com telhado de duas águas, transepto, abside, torre
    /// do cruzeiro com agulha, duas torres na fachada com agulhas altas e pináculos, contrafortes
    /// com pináculos, vitrais altos acesos, rosácea e o portal com luz quente lá dentro.
    /// Malha procedural (2 malhas: pedra e ardósia, UV em escala de mundo com a repetição dos
    /// materiais do kit) + janelas instanciadas. Fica visível por cima da muralha, da estrada.
    /// </summary>
    public static class Cathedral
    {
        public static readonly Vector3 Center = new Vector3(0f, 0f, 86f);   // fachada virada para o sul (para a vila)

        class MB
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            readonly float tile;
            public MB(float tile) { this.tile = tile; }

            Vector2 UV(Vector3 p, Vector3 nrm)
            {
                var a = new Vector3(Mathf.Abs(nrm.x), Mathf.Abs(nrm.y), Mathf.Abs(nrm.z));
                if (a.y > a.x && a.y > a.z) return new Vector2(p.x, p.z) / tile;
                if (a.x > a.z) return new Vector2(p.z, p.y) / tile;
                return new Vector2(p.x, p.y) / tile;
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var nrm = Vector3.Cross(b - a, d - a).normalized;
                int i = v.Count;
                foreach (var p in new[] { a, b, c, d }) { v.Add(p); n.Add(nrm); uv.Add(UV(p, nrm)); }
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var nrm = Vector3.Cross(b - a, c - a).normalized;
                int i = v.Count;
                foreach (var p in new[] { a, b, c }) { v.Add(p); n.Add(nrm); uv.Add(UV(p, nrm)); }
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            /// <summary>Caixa (centro da base, tamanho), sem a face de baixo.</summary>
            public void Box(Vector3 c, Vector3 s)
            {
                float x0 = c.x - s.x / 2, x1 = c.x + s.x / 2, z0 = c.z - s.z / 2, z1 = c.z + s.z / 2, y0 = c.y, y1 = c.y + s.y;
                Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0));   // sul
                Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1));   // norte
                Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0));   // oeste
                Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1));   // leste
                Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0));   // topo
            }

            /// <summary>Telhado de duas águas sobre uma caixa (cumeeira ao longo de z ou de x).</summary>
            public void Gable(Vector3 c, Vector3 s, float rise, bool alongZ, MB gableWalls)
            {
                float y = c.y;
                if (alongZ)
                {
                    float x0 = c.x - s.x / 2 - 0.4f, x1 = c.x + s.x / 2 + 0.4f, z0 = c.z - s.z / 2, z1 = c.z + s.z / 2;
                    var r0 = new Vector3(c.x, y + rise, z0); var r1 = new Vector3(c.x, y + rise, z1);
                    Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), r1, r0);
                    Quad(new Vector3(x1, y, z1), new Vector3(x1, y, z0), r0, r1);
                    gableWalls.Tri(new Vector3(c.x - s.x / 2, y, z0), r0, new Vector3(c.x + s.x / 2, y, z0));
                    gableWalls.Tri(new Vector3(c.x + s.x / 2, y, z1), r1, new Vector3(c.x - s.x / 2, y, z1));
                }
                else
                {
                    float z0 = c.z - s.z / 2 - 0.4f, z1 = c.z + s.z / 2 + 0.4f, x0 = c.x - s.x / 2, x1 = c.x + s.x / 2;
                    var r0 = new Vector3(x0, y + rise, c.z); var r1 = new Vector3(x1, y + rise, c.z);
                    Quad(new Vector3(x1, y, z0), new Vector3(x0, y, z0), r0, r1);
                    Quad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), r1, r0);
                    gableWalls.Tri(new Vector3(x0, y, c.z + s.z / 2), r0, new Vector3(x0, y, c.z - s.z / 2));
                    gableWalls.Tri(new Vector3(x1, y, c.z - s.z / 2), r1, new Vector3(x1, y, c.z + s.z / 2));
                }
            }

            /// <summary>Agulha (pirâmide de 'sides' lados) com base no centro c.</summary>
            public void Spire(Vector3 c, float radius, float height, int sides = 8)
            {
                var top = c + Vector3.up * height;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = (k + 0.5f) / sides * 6.2832f, a1 = (k + 1.5f) / sides * 6.2832f;
                    var p0 = c + new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * radius;
                    var p1 = c + new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * radius;
                    Tri(p0, p1, top);
                }
            }

            /// <summary>Prisma de 'sides' lados (abside, base de agulha).</summary>
            public void Prism(Vector3 c, float radius, float height, int sides, float a0Deg = 0f, float a1Deg = 360f)
            {
                for (int k = 0; k < sides; k++)
                {
                    float a0 = Mathf.Lerp(a0Deg, a1Deg, k / (float)sides) * Mathf.Deg2Rad, a1 = Mathf.Lerp(a0Deg, a1Deg, (k + 1) / (float)sides) * Mathf.Deg2Rad;
                    var p0 = c + new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * radius;
                    var p1 = c + new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * radius;
                    Quad(p1, p1 + Vector3.up * height, p0 + Vector3.up * height, p0);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }

        static Material FindMat(string name)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith(name)) return m;
            return null;
        }

        public static GameObject Build()
        {
            var stoneMat = FindMat("CMP_stone_wall") ?? FindMat("CMP_stone_dark");
            var roofMat = FindMat("CMP_roof_slate") ?? FindMat("CMP_roof_tiles") ?? stoneMat;
            if (stoneMat == null) return null;
            var root = new GameObject("Catedral dos Sinos");
            float gy = Campanula.GroundHeight.At(Center.x, Center.z, 0f) - 0.5f;
            root.transform.position = new Vector3(Center.x, gy, Center.z);
            var S = new MB(2.4f);   // pedra
            var R = new MB(2.0f);   // ardósia

            // nave (ao longo de z; a fachada fica em z = -22), transepto e abside
            float naveW = 16f, naveH = 22f, naveL = 44f;
            S.Box(new Vector3(0, 0, 0), new Vector3(naveW, naveH, naveL));
            R.Gable(new Vector3(0, naveH, 0), new Vector3(naveW, 0, naveL), 10f, true, S);
            S.Box(new Vector3(0, 0, 8), new Vector3(36f, naveH - 2f, 12f));
            R.Gable(new Vector3(0, naveH - 2f, 8), new Vector3(36f, 0, 12f), 9f, false, S);
            S.Prism(new Vector3(0, 0, naveL / 2), naveW / 2, naveH - 3f, 7, -90f, 90f);
            R.Spire(new Vector3(0, naveH - 3f, naveL / 2), naveW / 2 + 0.3f, 8f, 14);
            // torre do cruzeiro com agulha
            S.Box(new Vector3(0, naveH, 8), new Vector3(9f, 14f, 9f));
            R.Spire(new Vector3(0, naveH + 14f, 8), 6.2f, 24f, 8);
            // fachada: duas torres altas com agulhas e pináculos
            foreach (float sx in new[] { -1f, 1f })
            {
                var tc = new Vector3(sx * 10.5f, 0, -naveL / 2 - 1f);
                S.Box(tc, new Vector3(9f, 48f, 9f));
                S.Box(tc + Vector3.up * 48f, new Vector3(10f, 1.2f, 10f));                 // cornija
                R.Spire(tc + Vector3.up * 49.2f, 5.6f, 30f, 8);
                foreach (var o in new[] { new Vector3(-4.4f, 0, -4.4f), new Vector3(4.4f, 0, -4.4f), new Vector3(-4.4f, 0, 4.4f), new Vector3(4.4f, 0, 4.4f) })
                {
                    S.Box(tc + o + Vector3.up * 49.2f, new Vector3(1.4f, 3f, 1.4f));
                    R.Spire(tc + o + Vector3.up * 52.2f, 1.1f, 6f, 4);
                }
                // contrafortes da torre
                S.Box(tc + new Vector3(sx * 4.8f, 0, -4.8f), new Vector3(1.8f, 30f, 1.8f));
            }
            // empena da fachada entre as torres
            S.Box(new Vector3(0, 0, -naveL / 2 - 0.6f), new Vector3(12f, naveH + 4f, 1.6f));
            S.Tri(new Vector3(-6f, naveH + 4f, -naveL / 2 - 1.4f), new Vector3(0, naveH + 13f, -naveL / 2 - 1.4f), new Vector3(6f, naveH + 4f, -naveL / 2 - 1.4f));
            // contrafortes ao longo da nave, com pináculos
            for (int k = 0; k < 6; k++)
            {
                float z = -naveL / 2 + 4f + k * 6.4f;
                if (Mathf.Abs(z - 8f) < 6.5f) continue;   // transepto
                foreach (float sx in new[] { -1f, 1f })
                {
                    var bc = new Vector3(sx * (naveW / 2 + 1.6f), 0, z);
                    S.Box(bc, new Vector3(3.2f, 15f, 1.6f));
                    S.Box(bc + new Vector3(-sx * 0.6f, 15f, 0), new Vector3(2f, 4f, 1.4f));
                    R.Spire(bc + new Vector3(-sx * 0.6f, 19f, 0), 1.0f, 5f, 4);
                }
            }
            Material Mat(MB mb, Material m, string n)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(n);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return m;
            }
            Mat(S, stoneMat, "Pedra");
            Mat(R, roofMat, "Ardosia");

            // vitrais altos acesos, rosácea e o portal
            var winSh = Resources.Load<Shader>("Shaders/WindowGlow");
            if (winSh != null && winSh.isSupported)
            {
                var wm = new Material(winSh) { enableInstancing = true, hideFlags = HideFlags.DontSave };
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var mpb = new MaterialPropertyBlock();
                void Win(Vector3 local, Vector3 normal, float w, float h, Color c)
                {
                    var go = new GameObject("Vitral");
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = local + normal * 0.05f;
                    go.transform.localRotation = Quaternion.LookRotation(-normal, Vector3.up);
                    go.transform.localScale = new Vector3(w, h, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = quad;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = wm;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                    mpb.SetColor("_Color", c); mr.SetPropertyBlock(mpb);
                }
                var warm = new Color(1f, 0.6f, 0.28f, 1.4f);
                for (int k = 0; k < 6; k++)
                {
                    float z = -naveL / 2 + 7.2f + k * 6.4f;
                    if (Mathf.Abs(z - 8f) < 6.5f) continue;
                    Win(new Vector3(-naveW / 2, 12f, z), Vector3.left, 2.6f, 8f, warm);
                    Win(new Vector3(naveW / 2, 12f, z), Vector3.right, 2.6f, 8f, warm);
                }
                // fachada: rosácea, portal, janelas das torres
                float fz = -naveL / 2 - 1.42f;
                Win(new Vector3(0, 19f, fz), Vector3.back, 5.5f, 5.5f, new Color(1f, 0.55f, 0.32f, 1.7f));
                Win(new Vector3(0, 3.6f, fz), Vector3.back, 4.2f, 7f, new Color(1f, 0.5f, 0.2f, 1.2f));
                foreach (float sx in new[] { -1f, 1f })
                    for (int k = 0; k < 3; k++)
                        Win(new Vector3(sx * 10.5f, 18f + k * 10f, -naveL / 2 - 5.55f), Vector3.back, 1.6f, 5f, k == 2 ? new Color(1f, 0.62f, 0.3f, 1.6f) : warm);
                // a sineira das torres: luz vazando (halo) — é a Catedral dos Sinos
                var glow = NightSetup.GlowMat;
                if (glow != null)
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var hp = root.transform.TransformPoint(new Vector3(sx * 10.5f, 40f, -naveL / 2 - 6.2f));
                        var go = new GameObject("Halo sineira");
                        go.transform.SetParent(root.transform, true);
                        go.transform.position = hp; go.transform.localScale = new Vector3(14f, 14f, 1f);
                        go.AddComponent<MeshFilter>().sharedMesh = quad;
                        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = glow;
                        var c = new Color(1f, 0.6f, 0.28f, 0.35f);
                        mpb.SetColor("_Color", c); mr.SetPropertyBlock(mpb);
                        go.AddComponent<Flicker>().baseColor = c;
                    }
            }

            // as árvores que estavam no terreno da catedral saem
            var area = new Bounds(root.transform.position + new Vector3(0, 20, 2), new Vector3(42f, 80f, 64f));
            int hidden = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.IsChildOf(root.transform)) continue;
                string n = r.name;
                if ((n.StartsWith("Tree_") || n.StartsWith("Bush") || n.StartsWith("Rock_")) && area.Contains(r.bounds.center)) { r.enabled = false; hidden++; }
            }
            Debug.Log($"[Cidade] catedral em {root.transform.position} ({S.v.Count + R.v.Count} vértices), {hidden} árvores/pedras escondidas");
            return root;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Campanula viva à noite (o storyboard tem a cidade cheia de luzes quentes): janelas acesas
    /// nas casas e na taverna (quads com caixilho colados nas fachadas por raio — a maioria acesa,
    /// algumas apagadas), lanternas ao longo do alto da muralha e o campanário aceso por dentro
    /// (o marco que se vê de longe). Tudo instanciado: um material, poucas chamadas de desenho.
    /// </summary>
    public static class VillageLights
    {
        static Material winMat;

        public static void Build()
        {
            var sh = Resources.Load<Shader>("Shaders/WindowGlow");
            if (sh == null || !sh.isSupported) return;
            if (winMat == null) winMat = new Material(sh) { enableInstancing = true, hideFlags = HideFlags.DontSave };
            var root = new GameObject("Luzes da vila").transform;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mpb = new MaterialPropertyBlock();
            var rnd = new System.Random(1234);
            int windows = 0, lit = 0, lanterns = 0;
            int mask = ~((1 << 10) | (1 << 8) | (1 << 2));   // sem jogador, bordas de parkour e "ignore raycast"

            void Window(Vector3 p, Vector3 n, float w, float h, bool on, Vector4 shape = default)
            {
                var go = new GameObject("Janela");
                go.transform.SetParent(root, false);
                go.transform.position = p + n * 0.035f;
                go.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
                go.transform.localScale = new Vector3(w, h, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = winMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                float warm = (float)rnd.NextDouble();
                var c = on ? new Color(1f, 0.55f + 0.15f * warm, 0.24f + 0.12f * warm, 1.5f + (float)rnd.NextDouble() * 0.8f)
                           : new Color(0.12f, 0.14f, 0.2f, 0.5f);
                mpb.SetColor("_Color", c);
                mpb.SetVector("_Shape", shape);
                mr.SetPropertyBlock(mpb);
                windows++; if (on) lit++;
                // a luz que sai da janela: um pouco para fora dela, quente, alcance de ~3 m
                if (on) CityLight.Add(p + n * 0.7f, new Color(1f, 0.58f, 0.26f) * (0.55f + 0.25f * w * h), 2.6f + 0.8f * h);
            }

            // casas góticas (kit_gothic.py): cada janela modelada traz um marcador
            // WIN_<L|D>_<largura cm>_<altura cm>_<nascente %>_<raio×100>_<n> no plano do vidro; a janela acesa entra
            // exatamente ali, recortada no arco. O lado "de fora" sai do próprio marcador (±forward,
            // conferido por raio contra a parede).
            int gothic = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("WIN_")) continue;
                var a = t.name.Split('_');
                if (a.Length < 5 || !int.TryParse(a[2], out int wcm) || !int.TryParse(a[3], out int hcm) || !int.TryParse(a[4], out int sp)) continue;
                Vector3 outward = Vector3.zero; float best = 9f;
                foreach (var d0 in new[] { t.forward, -t.forward, t.up, -t.up, t.right, -t.right })
                {
                    var d = d0; d.y = 0f;
                    if (d.sqrMagnitude < 0.5f) continue;
                    d.Normalize();
                    if (!Physics.Raycast(t.position + d * 0.6f, -d, out var hit, 0.75f, mask, QueryTriggerInteraction.Ignore)) continue;
                    float sc = Mathf.Abs(hit.distance - 0.58f);
                    if (sc < best && Vector3.Dot(hit.normal, d) > 0.6f) { best = sc; outward = d; }
                }
                if (outward == Vector3.zero || best > 0.2f) continue;
                float w = wcm / 100f, h = hcm / 100f;
                bool on = a[1] == "L" ? rnd.NextDouble() < 0.86 : rnd.NextDouble() < 0.22;
                float kr = a.Length >= 7 && int.TryParse(a[5], out int kc) ? kc / 100f : 1.15f;
                Window(t.position, outward, w, h, on, new Vector4(sp / 100f, w / h, kr, 0f));
                gothic++;
            }
            // lanternas da ponte e dos muros do desfiladeiro (marcadores LAMP_)
            int lamps = 0;
            var lampGlow = NightSetup.GlowMat;
            if (lampGlow != null)
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("LAMP_")) continue;
                    // lanternas soltas (fundo do cânion, pés do Grande Aqueduto): um poste de ferro com a lanterna
                    if (t.name.StartsWith("LAMP_canion") || t.name.StartsWith("LAMP_pilar"))
                    {
                        var im = FindMat("CMP_iron") ?? FindMat("CMP_dark");
                        var post = new GameObject("Lanterna solta").transform;
                        post.SetParent(root, false);
                        post.position = t.position + Vector3.down * 1.8f;
                        Cube(post, new Vector3(0, 0.95f, 0), new Vector3(0.09f, 1.9f, 0.09f), im);
                        Cube(post, new Vector3(0, 1.78f, 0), new Vector3(0.24f, 0.32f, 0.24f), im);
                    }
                    Halo(root, t.position, 2.6f, new Color(1f, 0.6f, 0.26f, 0.6f), lampGlow);
                    Halo(root, t.position, 0.4f, new Color(1f, 0.85f, 0.6f, 1f), lampGlow);
                    CityLight.Add(t.position, new Color(1f, 0.6f, 0.28f) * 1.3f, 5.5f);
                    lamps++;
                }
            if (gothic + lamps > 0) Debug.Log($"[Noite] janelas góticas: {gothic}, lanternas da ponte/desfiladeiro: {lamps}");

            var houses = new List<Renderer>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string n = mf.name;
                if (n.StartsWith("House_") || n.StartsWith("Tavern")) { var r = mf.GetComponent<Renderer>(); if (r != null) houses.Add(r); }   // casas antigas (enxaimel), janelas por raio
            }
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (var r in houses)
            {
                var b = r.bounds;
                float wallTop = b.min.y + b.size.y * 0.55f;
                foreach (var d in dirs)
                {
                    var side = Vector3.Cross(Vector3.up, d);
                    float faceW = Mathf.Abs(Vector3.Dot(b.size, side));
                    float ext = Mathf.Abs(Vector3.Dot(b.extents, d));
                    int cols = Mathf.Clamp(Mathf.RoundToInt(faceW / 3.2f), 1, 4);
                    int rows = wallTop - b.min.y > 5.5f ? 2 : 1;
                    for (int row = 0; row < rows; row++)
                        for (int col = 0; col < cols; col++)
                        {
                            if (rnd.NextDouble() < 0.25) continue;   // nem toda parede tem janela ali
                            float u = (col + 0.5f) / cols - 0.5f;
                            float y = b.min.y + 1.7f + row * 2.7f;
                            if (y > wallTop) continue;
                            var start = b.center + d * (ext + 2f) + side * (u * faceW * 0.8f);
                            start.y = y;
                            if (!Physics.Raycast(start, -d, out var hit, ext + 3f, mask, QueryTriggerInteraction.Ignore)) continue;
                            if (!b.Contains(hit.point + d * 0.05f) && !b.Contains(hit.point - d * 0.05f)) continue;
                            if (Vector3.Dot(hit.normal, d) < 0.75f) continue;
                            Window(hit.point, hit.normal, 0.75f, 1.0f, rnd.NextDouble() < 0.72);
                        }
                }
            }
            // lanternas no alto da muralha (uma fileira de luzes quentes sobre as ameias, vista da estrada)
            var glow = NightSetup.GlowMat;
            if (glow != null)
                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (!mf.name.StartsWith("Wall_Segment")) continue;
                    var r = mf.GetComponent<Renderer>(); if (r == null) continue;
                    var b = r.bounds;
                    bool alongX = b.size.x > b.size.z;
                    float len = alongX ? b.size.x : b.size.z;
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 11f));
                    for (int k = 0; k < n; k++)
                    {
                        float u = (k + 0.5f) / n - 0.5f;
                        var p = b.center + (alongX ? Vector3.right : Vector3.forward) * u * len;
                        p.y = b.max.y + 0.5f;
                        Halo(root, p, 2.4f, new Color(1f, 0.6f, 0.26f, 0.55f), glow);
                        Halo(root, p, 0.35f, new Color(1f, 0.85f, 0.6f, 1f), glow);
                        CityLight.Add(p, new Color(1f, 0.6f, 0.28f) * 1.1f, 5.5f);
                        lanterns++;
                    }
                }
            // postes com lampião ao longo da estrada sul (do sino da estrada até o portão)
            int posts = 0;
            var postMat = FindMat("CMP_timber") ?? FindMat("CMP_dark");
            var ironMat = FindMat("CMP_iron") ?? FindMat("CMP_dark");
            var poolSh = Resources.Load<Shader>("Shaders/LightPool");
            Material poolMat = null;
            if (poolSh != null && poolSh.isSupported) { poolMat = new Material(poolSh) { enableInstancing = true, hideFlags = HideFlags.DontSave }; poolMat.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_night")); }
            foreach (var z in new[] { -52f, -64f, -77f })
            {
                float x = (posts % 2 == 0 ? 3.6f : -3.6f) + Mathf.Sin(z * 0.04f) * 1.5f;
                float gy = Campanula.GroundHeight.At(x, z, 0f);
                if (Physics.CheckSphere(new Vector3(x, gy + 1.5f, z), 0.4f, mask, QueryTriggerInteraction.Ignore)) continue;   // não planta poste em cima de nada
                var post = new GameObject("Poste da estrada");
                post.transform.SetParent(root, false);
                post.transform.position = new Vector3(x, gy, z);
                Cube(post.transform, new Vector3(0, 1.6f, 0), new Vector3(0.16f, 3.2f, 0.16f), postMat);
                float side = x > 0 ? -1f : 1f;   // o braço aponta para a estrada
                Cube(post.transform, new Vector3(side * 0.35f, 3.1f, 0), new Vector3(0.8f, 0.1f, 0.1f), postMat);
                Cube(post.transform, new Vector3(side * 0.65f, 2.75f, 0), new Vector3(0.26f, 0.36f, 0.26f), ironMat);
                var lp = post.transform.position + new Vector3(side * 0.65f, 2.75f, 0);
                if (glow != null) { Halo(root, lp, 2.8f, new Color(1f, 0.62f, 0.28f, 0.55f), glow); Halo(root, lp, 0.4f, new Color(1f, 0.85f, 0.6f, 1f), glow); }
                CityLight.Add(lp, new Color(1f, 0.6f, 0.28f) * 1.2f, 6f);
                if (poolMat != null)
                {
                    var pg = new GameObject("Poca de luz");
                    pg.transform.SetParent(root, false);
                    pg.transform.position = new Vector3(lp.x, Campanula.GroundHeight.At(lp.x, lp.z, gy) + 0.03f, lp.z);
                    pg.transform.rotation = Quaternion.LookRotation(Vector3.down);
                    pg.transform.localScale = new Vector3(7f, 7f, 1f);
                    pg.AddComponent<MeshFilter>().sharedMesh = quad;
                    var mr = pg.AddComponent<MeshRenderer>(); mr.sharedMaterial = poolMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mpb.SetColor("_Color", new Color(1f, 0.6f, 0.28f, 0.35f)); mr.SetPropertyBlock(mpb);
                }
                posts++;
            }

            // o campanário aceso por dentro
            var tower = GameObject.Find("BellTower");
            if (tower != null && glow != null)
            {
                // a luz vaza pelas quatro faces da sineira (os halos ficam do lado de fora das paredes)
                var tb = new Bounds(tower.transform.position, Vector3.zero);
                foreach (var r in tower.GetComponentsInChildren<Renderer>()) tb.Encapsulate(r.bounds);
                float hy = tb.min.y + tb.size.y * 0.72f;
                var c0 = new Vector3(tb.center.x, hy, tb.center.z);
                foreach (var d in dirs)
                {
                    float ext = Mathf.Abs(Vector3.Dot(tb.extents, d));
                    Halo(root, c0 + d * (ext * 0.55f + 0.6f), 6f, new Color(1f, 0.6f, 0.28f, 0.4f), glow);
                    Halo(root, c0 + d * (ext * 0.55f + 0.6f), 1.6f, new Color(1f, 0.8f, 0.5f, 0.7f), glow);
                    CityLight.Add(c0 + d * (ext * 0.55f + 1.5f), new Color(1f, 0.6f, 0.28f) * 1.4f, 6f);
                }
                Debug.Log($"[Noite] campanário: {tb.size} sineira a {hy:0.0} m");
            }
            Debug.Log($"[Noite] janelas {windows} ({lit} acesas), lanternas da muralha {lanterns}, postes da estrada {posts}");
            CityLight.Bake();
            // o vidro das casas do kit era um painel amarelo forte (feito para o pôr do sol): à noite vira luz
            // de vela, mais baixa e quente — as janelas com quarto em perspectiva (acima) fazem o resto
            var glass = FindMat("CMP_glass");
            if (glass != null && glass.HasProperty("_EmissionColor"))
                root.gameObject.AddComponent<GlassDim>().Begin(glass, new Color(0.62f, 0.32f, 0.13f));
        }

        /// <summary>Baixa a emissão do vidro do kit enquanto a noite existir (e devolve ao destruir).</summary>
        class GlassDim : MonoBehaviour
        {
            Material m; Color orig;
            public void Begin(Material mat, Color night) { m = mat; orig = mat.GetColor("_EmissionColor"); mat.SetColor("_EmissionColor", night); }
            void OnDestroy() { if (m != null) m.SetColor("_EmissionColor", orig); }
        }

        static Material FindMat(string name)
        {
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith(name)) return m;
            return null;
        }

        static void Cube(Transform parent, Vector3 local, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local; g.transform.localScale = size;
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void Halo(Transform parent, Vector3 p, float size, Color c, Material m)
        {
            var go = new GameObject("Halo");
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Color", c); mr.SetPropertyBlock(mpb);
            go.AddComponent<Flicker>().baseColor = c;
        }
    }
}

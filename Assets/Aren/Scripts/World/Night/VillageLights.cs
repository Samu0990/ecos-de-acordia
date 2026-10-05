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

            void Window(Vector3 p, Vector3 n, float w, float h, bool on)
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
                mr.SetPropertyBlock(mpb);
                windows++; if (on) lit++;
            }

            var houses = new List<Renderer>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                string n = mf.name;
                if (n.StartsWith("House_") || n.StartsWith("Tavern")) { var r = mf.GetComponent<Renderer>(); if (r != null) houses.Add(r); }
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
                        lanterns++;
                    }
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
                }
                Debug.Log($"[Noite] campanário: {tb.size} sineira a {hy:0.0} m");
            }
            Debug.Log($"[Noite] janelas {windows} ({lit} acesas), lanternas da muralha {lanterns}");
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

using System.Collections.Generic;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// Aviso de counter sobre a cabeça do inimigo: um anel externo que FECHA sobre o anel
    /// interno exatamente no instante do golpe — ensina o timing sem texto. Dourado (cor
    /// do counter do Aren), nada parecido com os avisos dos jogos de referência.
    /// </summary>
    public class CounterIndicator : MonoBehaviour
    {
        static readonly List<CounterIndicator> pool = new List<CounterIndicator>();
        static Material ringMat;
        static Mesh quad;

        EnemyBase target;
        MeshRenderer inner, outer;
        MaterialPropertyBlock mpb;
        float fadeOut = -1f;
        static readonly int IdRadius = Shader.PropertyToID("_Radius");
        static readonly int IdWidth = Shader.PropertyToID("_Width");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdFade = Shader.PropertyToID("_Fade");

        public static void Show(EnemyBase e)
        {
            CounterIndicator ci = null;
            foreach (var p in pool) if (p != null && !p.gameObject.activeSelf) { ci = p; break; }
            if (ci == null)
            {
                var go = new GameObject("CounterIndicator");
                ci = go.AddComponent<CounterIndicator>();
                ci.Build();
                pool.Add(ci);
            }
            ci.target = e;
            ci.fadeOut = -1f;
            ci.gameObject.SetActive(true);
            ci.LateUpdate();
        }

        void Build()
        {
            if (ringMat == null) ringMat = new Material(Resources.Load<Shader>("Shaders/ArenFXRing"));
            if (quad == null)
            {
                quad = new Mesh();
                quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            }
            mpb = new MaterialPropertyBlock();
            inner = MakeRing("inner");
            outer = MakeRing("outer");
        }

        MeshRenderer MakeRing(string n)
        {
            var g = new GameObject(n); g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ringMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        void LateUpdate()
        {
            if (target == null) { gameObject.SetActive(false); return; }
            var cam = Camera.main;
            transform.position = target.HeadPoint + Vector3.up * 0.35f;
            if (cam != null) transform.rotation = cam.transform.rotation;
            // tamanho constante na tela (legível de longe)
            float dist = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 6f;
            transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.09f, 0.5f, 2.2f);

            bool open = target.CounterWindowOpen;
            if (!open && fadeOut < 0f) fadeOut = 0f;
            float alpha = 1f;
            if (fadeOut >= 0f)
            {
                fadeOut += Time.deltaTime;
                alpha = 1f - fadeOut / 0.15f;
                if (alpha <= 0f) { gameObject.SetActive(false); return; }
            }
            float p = target.CounterWindowProgress;
            bool perfect = p > 0.6f;
            Color gold = new Color(1.6f, 1.1f, 0.4f);
            Color c = perfect ? gold * 1.5f : gold;

            inner.transform.localScale = Vector3.one;
            mpb.Clear();
            mpb.SetFloat(IdRadius, 0.42f); mpb.SetFloat(IdWidth, perfect ? 0.08f : 0.05f);
            mpb.SetColor(IdColor, c); mpb.SetFloat(IdFade, alpha * (0.6f + 0.4f * Mathf.Sin(Time.time * 30f) * (perfect ? 1 : 0)));
            inner.SetPropertyBlock(mpb);

            float k = Mathf.Lerp(2.6f, 1f, p);   // fecha até coincidir com o interno
            outer.transform.localScale = Vector3.one * k;
            mpb.Clear();
            mpb.SetFloat(IdRadius, 0.42f); mpb.SetFloat(IdWidth, 0.035f / k);
            mpb.SetColor(IdColor, c * 0.8f); mpb.SetFloat(IdFade, alpha * Mathf.Lerp(0.35f, 1f, p));
            outer.SetPropertyBlock(mpb);
        }
    }
}

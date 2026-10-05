using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// O mundo reagindo ao som, perto do Aren:
    ///   · poeira na luz da lanterna do sino que VIBRA quando o som desafina (cada grão oscila numa
    ///     onda estacionária — nós parados, ventres tremendo: o som ficando visível, sutil);
    ///   · a onda de pressão do impacto passando pela vila: rajada de poeira e folhas vinda do leste,
    ///     capim e árvores dobrando, a lanterna e o sino balançando.
    /// </summary>
    public class VillageFX : MonoBehaviour
    {
        public static VillageFX Instance { get; private set; }
        public float vibration;           // 0..1 (o diretor liga quando o sino/a música desafinam)
        ParticleSystem motes, gust;
        ParticleSystem.Particle[] buf;
        Vector3[] home;
        Vector3 center;
        float gustT = -100f; Vector3 gustDir;
        Material foliage; float foliageWind0 = -1f;
        TerrainData td; float wStr0, wAmt0, wSpd0;

        public static VillageFX Create(Vector3 lanternPos)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("Vila reagindo");
            var v = go.AddComponent<VillageFX>();
            Instance = v;
            v.center = lanternPos;
            v.motes = v.MakeMotes();
            v.gust = v.MakeGust();
            // vento das árvores (material compartilhado) e do capim do terreno: guardados para devolver
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.StartsWith("CMP_foliage") && m.HasProperty("_Wind")) { v.foliage = m; v.foliageWind0 = m.GetFloat("_Wind"); break; }
            }
            var terr = Terrain.activeTerrain;
            if (terr != null) { v.td = terr.terrainData; v.wStr0 = v.td.wavingGrassStrength; v.wAmt0 = v.td.wavingGrassAmount; v.wSpd0 = v.td.wavingGrassSpeed; }
            return v;
        }

        ParticleSystem MakeMotes()
        {
            var go = new GameObject("Poeira na luz");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1000f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.5f, 0.55f), new Color(1f, 0.9f, 0.7f, 0.9f));
            main.maxParticles = 90; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SevenGlows.SparkMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            var ep = new ParticleSystem.EmitParams();
            home = new Vector3[90];
            var rnd = new System.Random(3);
            for (int i = 0; i < 90; i++)
            {
                home[i] = center + new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.6f, (float)rnd.NextDouble() - 0.5f) * 1.6f;
                ep.position = home[i];
                ps.Emit(ep, 1);
            }
            buf = new ParticleSystem.Particle[90];
            return ps;
        }

        ParticleSystem MakeGust()
        {
            var go = new GameObject("Rajada");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.5f, 0.45f, 0.5f), new Color(0.35f, 0.3f, 0.25f, 0.8f));
            main.maxParticles = 400; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var sh0 = Resources.Load<Shader>("Shaders/DustPuff");
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (sh0 != null)
            {
                var m = new Material(sh0) { hideFlags = HideFlags.DontSave };
                m.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_night"));
                m.SetColor("_Lit", new Color(1f, 0.6f, 0.3f));
                m.SetFloat("_LitAmount", 0.35f);
                r.sharedMaterial = m;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX });
            return ps;
        }

        /// <summary>A onda de pressão chegou: rajada vinda de 'from' (ponto do impacto), em volta de 'around' (câmera/Aren).</summary>
        public void Gust(Vector3 from, Vector3 around)
        {
            gustT = Time.time;
            gustDir = around - from; gustDir.y = 0f; gustDir.Normalize();
            gust.Play();
            var ep = new ParticleSystem.EmitParams();
            var side = Vector3.Cross(Vector3.up, gustDir);
            var rnd = new System.Random(11);
            for (int i = 0; i < 260; i++)
            {
                float u = (float)rnd.NextDouble();
                ep.position = around - gustDir * (6f + u * 14f) + side * ((float)rnd.NextDouble() - 0.5f) * 18f + Vector3.up * ((float)rnd.NextDouble() * 3.2f);
                ep.velocity = gustDir * (14f + (float)rnd.NextDouble() * 16f) + Vector3.up * ((float)rnd.NextDouble() * 2.5f);
                ep.startLifetime = 1.2f + (float)rnd.NextDouble() * 1.6f;
                gust.Emit(ep, 1);
            }
            BellShrine.Instance?.Resonate(1f);
        }

        void Update()
        {
            float t = Time.time;
            // poeira na luz: deriva lenta + vibração em onda estacionária (ao longo de x local)
            if (motes != null && buf != null)
            {
                int n = motes.GetParticles(buf);
                float vib = vibration;
                for (int i = 0; i < n && i < home.Length; i++)
                {
                    var h = home[i];
                    float drift = Mathf.Sin(t * 0.21f + i) * 0.05f;
                    var p = h + new Vector3(drift, Mathf.Sin(t * 0.17f + i * 1.7f) * 0.05f, Mathf.Cos(t * 0.19f + i) * 0.05f);
                    float x = (h.x - center.x) / 1.6f + 0.5f;
                    float mode = Mathf.Sin(x * Mathf.PI * 3f);                         // 3º harmônico: nós e ventres
                    float beat = Mathf.Sin(t * 2f * Mathf.PI * 7.3f) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 0.9f));
                    p.y += mode * beat * 0.045f * vib;
                    // a rajada também carrega a poeira
                    float g = Mathf.Exp(-Mathf.Max(0f, t - gustT) * 1.5f) * (t > gustT ? 1f : 0f);
                    p += gustDir * g * 0.6f;
                    buf[i].position = p;
                    var c = buf[i].startColor; c.a = (byte)Mathf.Clamp(140 + 100 * Mathf.Abs(mode) * vib, 0, 255);
                    buf[i].startColor = c;
                }
                motes.SetParticles(buf, n);
            }
            // vento: sobe de repente na rajada e volta devagar
            float gk = t > gustT ? Mathf.Exp(-(t - gustT) / 1.6f) * Mathf.Clamp01((t - gustT) * 6f) : 0f;
            if (foliage != null) foliage.SetFloat("_Wind", foliageWind0 + gk * 2.2f);
            if (td != null)
            {
                td.wavingGrassStrength = wStr0 + gk * 0.9f;
                td.wavingGrassAmount = wAmt0 + gk * 0.6f;
                td.wavingGrassSpeed = wSpd0 + gk * 1.4f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (foliage != null && foliageWind0 >= 0f) foliage.SetFloat("_Wind", foliageWind0);
            if (td != null) { td.wavingGrassStrength = wStr0; td.wavingGrassAmount = wAmt0; td.wavingGrassSpeed = wSpd0; }
        }
    }
}

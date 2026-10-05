using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Nuvens volumétricas da noite: ~26 cartões no céu (1,6–4,8 km, 750–1700 m de altura) com nuvens
    /// renderizadas como volume no Cycles (ArtSource/Campanula/scripts/cloud_impostors.py) e iluminadas
    /// pelo shader Hidden/Campanula/CloudImpostor com a técnica de 6 direções (lua, Fenda, cidade por
    /// baixo, impacto). Cada cartão fica virado para a vila (de longe a câmera quase não muda de ângulo);
    /// a direção da Fenda fica livre (o rasgo não é coberto). Sem colisor, sem sombra.
    /// </summary>
    public static class NightClouds
    {
        public static Transform Root { get; private set; }

        public static void Create()
        {
            if (Root != null) return;
            var sh = Resources.Load<Shader>("Shaders/CloudImpostor");
            var ta = Resources.Load<Texture2D>("VFX/clouds_a");
            var tb = Resources.Load<Texture2D>("VFX/clouds_b");
            if (sh == null || !sh.isSupported || ta == null || tb == null) return;
            var mat = new Material(sh) { enableInstancing = true, hideFlags = HideFlags.DontSave };
            mat.SetTexture("_A", ta); mat.SetTexture("_B", tb);
            Root = new GameObject("Nuvens volumétricas").transform;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var rnd = new System.Random(77);
            var mpb = new MaterialPropertyBlock();
            float fendaDeg = NightSetup.FendaAz * Mathf.Rad2Deg;
            int n = 0;
            for (int i = 0; i < 120 && n < 26; i++)
            {
                float az = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
                if (Mathf.Abs(Mathf.DeltaAngle(az * Mathf.Rad2Deg, fendaDeg)) < 16f) continue;   // o rasgo fica à vista
                float r = Mathf.Lerp(1600f, 4800f, Mathf.Pow((float)rnd.NextDouble(), 0.8f));
                float y = Mathf.Lerp(750f, 1500f, (float)rnd.NextDouble()) + r * 0.04f;
                float size = Mathf.Lerp(500f, 1100f, (float)rnd.NextDouble()) * (0.8f + r / 4800f * 0.5f);
                var p = new Vector3(Mathf.Sin(az) * r, y, Mathf.Cos(az) * r);
                var go = new GameObject("Nuvem");
                go.transform.SetParent(Root, false);
                go.transform.position = p;
                // +Z do cartão aponta para longe da vila (o quad da Unity mostra a face −Z: virada para a vila)
                go.transform.rotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z).normalized, Vector3.up);
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                int cell = rnd.Next(4);
                mpb.SetVector("_Cell", new Vector4(cell % 2, cell / 2, rnd.NextDouble() < 0.5 ? 1f : 0f, 0.75f + 0.3f * (float)rnd.NextDouble()));
                mr.SetPropertyBlock(mpb);
                n++;
            }
            Debug.Log("[Noite] nuvens volumétricas: " + n);
        }

        public static void Destroy()
        {
            if (Root != null) { if (Application.isPlaying) Object.Destroy(Root.gameObject); else Object.DestroyImmediate(Root.gameObject); }
            Root = null;
        }
    }
}

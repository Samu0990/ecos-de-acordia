using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// O impacto da nota dourada no planalto do leste, ~2,5 km de Campanula. Camadas, em ordem:
    ///   1. NÚCLEO — a energia se concentra por alguns quadros (encolhe e fica mais quente);
    ///   2. CLARÃO — rápido, sem deixar a tela branca: cartaz enorme + luz na paisagem, no céu e na vila;
    ///   3. ONDA DE CHOQUE — anel que corre pelo chão (no shader da paisagem), cúpula de ar comprimido
    ///      que distorce o que está atrás, cortina de poeira que acompanha a frente;
    ///   4. SEGUNDA ONDA — mais fraca, meio segundo depois (profundidade);
    ///   5. POEIRA, BRASAS E FAGULHAS — coluna de pó acesa por baixo, brasas balísticas, fagulhas subindo;
    ///   6. PILAR DE LUZ — sobe e fica, fraco, depois da abertura (o mistério no horizonte);
    ///   7. ONDA DE PRESSÃO NA VILA — 'soundDelay' segundos depois (a distância): o diretor sacode a
    ///      câmera de forma controlada, o vento passa, o sino responde, o som chega.
    /// Tudo é poucos objetos (malhas + 3 sistemas de partículas); os anéis no chão são do shader.
    /// </summary>
    public class ImpactFX : MonoBehaviour
    {
        public static ImpactFX Instance { get; private set; }
        public Vector3 Point { get; private set; }
        public float T => t0 < 0f ? -1f : Time.time - t0;
        /// <summary>Segundos até a onda de pressão chegar em Campanula (licença: ~2,5 km "parecem" 4 s).</summary>
        public float soundDelay = 4.3f;
        public System.Action onPressureWave;
        /// <summary>0..1: quão forte o clarão está agora (o diretor usa na exposição e nos raios).</summary>
        public float Flash { get; private set; }
        public float Glow { get; private set; }

        public const float CoreTime = 0.22f;   // concentração antes de liberar
        float t0 = -1f;
        bool pressureFired;
        Transform core, flashT;
        Renderer coreR, flashR;
        MeshRenderer dome; Material domeMat;
        MeshRenderer ring1, ring2; Material ringMat1, ringMat2; Mesh ringMesh1, ringMesh2;
        Transform beam; Material beamMat;
        ParticleSystem embers, sparkles, column;
        Light sceneLight;
        MaterialPropertyBlock mpb;
        float[,] heights;          // altura do chão em volta (direções × raios) para a cortina de poeira seguir o relevo
        const int RingSeg = 96, HRad = 40;
        const float HMax = 2600f;
        static readonly int IdColor = Shader.PropertyToID("_Color"), IdParams = Shader.PropertyToID("_Params"), IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdImpactLight = Shader.PropertyToID("_ImpactLight"), IdShock = Shader.PropertyToID("_Shock"), IdScroll = Shader.PropertyToID("_Scroll"), IdLitAmount = Shader.PropertyToID("_LitAmount");
        static readonly Color Warm = new Color(1f, 0.7f, 0.38f);

        /// <summary>Prepara (alguns segundos antes): mede o relevo em volta do ponto, espalhado em quadros.</summary>
        public static ImpactFX Prepare(Vector3 point)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("Impacto da nota");
            var fx = go.AddComponent<ImpactFX>();
            Instance = fx;
            fx.Point = point;
            fx.transform.position = point;
            fx.mpb = new MaterialPropertyBlock();
            fx.StartCoroutine(fx.MeasureGround());
            fx.Build();
            return fx;
        }

        System.Collections.IEnumerator MeasureGround()
        {
            var h = new float[RingSeg, HRad];
            int n = 0;
            for (int a = 0; a < RingSeg; a++)
            {
                float ang = a / (float)RingSeg * 6.2832f;
                var d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                for (int r = 0; r < HRad; r++)
                {
                    float rr = (r + 0.5f) / HRad * HMax;
                    var p = Point + d * rr;
                    h[a, r] = FarLands.Root != null ? FarLands.Height(p.x, p.z) : Point.y;
                    if (++n % 400 == 0) yield return null;
                }
            }
            heights = h;
        }

        float GroundAt(int a, float radius)
        {
            if (heights == null) return Point.y;
            float f = Mathf.Clamp(radius / HMax * HRad - 0.5f, 0f, HRad - 1.001f);
            int i = (int)f; float k = f - i;
            return Mathf.Lerp(heights[a % RingSeg, i], heights[a % RingSeg, Mathf.Min(i + 1, HRad - 1)], k);
        }

        void Build()
        {
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var noise = Resources.Load<Texture2D>("VFX/noise_night");
            Renderer Bill(string n, out Transform t)
            {
                var go = new GameObject(n);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = SevenGlows.NoteMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.enabled = false;
                t = go.transform;
                return mr;
            }
            coreR = Bill("Nucleo", out core);
            core.localPosition = Vector3.up * 25f;
            flashR = Bill("Clarao", out flashT);
            flashT.localPosition = Vector3.up * 60f;

            // cúpula de ar comprimido (meia esfera) com distorção
            var dsh = Resources.Load<Shader>("Shaders/ShockDome");
            if (dsh != null && dsh.isSupported)
            {
                domeMat = new Material(dsh) { hideFlags = HideFlags.DontSave };
                domeMat.SetTexture("_Noise", noise);
                var go = new GameObject("Cupula");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.down * 20f;
                go.AddComponent<MeshFilter>().sharedMesh = Hemisphere();
                dome = go.AddComponent<MeshRenderer>();
                dome.sharedMaterial = domeMat;
                dome.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; dome.receiveShadows = false;
                dome.enabled = false;
            }
            // cortinas de poeira das duas ondas
            var rsh = Resources.Load<Shader>("Shaders/DustRing");
            if (rsh != null && rsh.isSupported)
            {
                ringMat1 = new Material(rsh) { hideFlags = HideFlags.DontSave }; ringMat1.SetTexture("_Noise", noise);
                ringMat2 = new Material(ringMat1);
                ring1 = MakeRing("Poeira_1", ringMat1, out ringMesh1);
                ring2 = MakeRing("Poeira_2", ringMat2, out ringMesh2);
            }
            // pilar de luz
            var bsh = Resources.Load<Shader>("Shaders/LightBeam");
            if (bsh != null && bsh.isSupported)
            {
                beamMat = new Material(bsh) { hideFlags = HideFlags.DontSave };
                beamMat.SetTexture("_Noise", noise);
                beamMat.SetFloat(IdIntensity, 0f);
                var go = new GameObject("Pilar de luz");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(70f, 1700f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = beamMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                beam = go.transform;
            }
            embers = MakeEmbers();
            sparkles = MakeSparkles();
            column = MakeColumn(noise);
            // a luz do clarão na vila (direcional, vinda do leste; só existe enquanto brilha)
            var lg = new GameObject("Luz do impacto");
            lg.transform.SetParent(transform, false);
            sceneLight = lg.AddComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.color = Warm;
            sceneLight.intensity = 0f;
            sceneLight.shadows = LightShadows.None;
            sceneLight.renderMode = LightRenderMode.ForcePixel;
            sceneLight.enabled = false;
        }

        MeshRenderer MakeRing(string n, Material m, out Mesh mesh)
        {
            mesh = new Mesh { name = n };
            var v = new Vector3[(RingSeg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[RingSeg * 6];
            for (int i = 0; i <= RingSeg; i++)
            {
                v[i * 2] = Vector3.zero; v[i * 2 + 1] = Vector3.up;
                uv[i * 2] = new Vector2(i / (float)RingSeg, 0f); uv[i * 2 + 1] = new Vector2(i / (float)RingSeg, 1f);
                if (i < RingSeg)
                {
                    int b = i * 2, t = i * 6;
                    tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                    tris[t + 3] = b + 2; tris[t + 4] = b + 1; tris[t + 5] = b + 3;
                }
            }
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f);
            mesh.MarkDynamic();
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero;   // vértices em coordenadas do mundo
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.enabled = false;
            return mr;
        }

        readonly Vector3[] ringVerts = new Vector3[(RingSeg + 1) * 2];

        void UpdateRing(Mesh mesh, float radius, float height)
        {
            for (int i = 0; i <= RingSeg; i++)
            {
                float ang = i / (float)RingSeg * 6.2832f;
                var d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var p = Point + d * radius;
                p.y = GroundAt(i, radius) - 6f;
                ringVerts[i * 2] = p;
                ringVerts[i * 2 + 1] = p + Vector3.up * height;
            }
            mesh.vertices = ringVerts;
        }

        static Mesh Hemisphere()
        {
            const int lat = 14, lon = 40;
            var m = new Mesh { name = "Meia esfera" };
            var v = new Vector3[(lat + 1) * (lon + 1)];
            var n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            for (int i = 0; i <= lat; i++)
                for (int j = 0; j <= lon; j++)
                {
                    float th = i / (float)lat * Mathf.PI * 0.5f, ph = j / (float)lon * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(th) * Mathf.Sin(ph), Mathf.Sin(th), Mathf.Cos(th) * Mathf.Cos(ph));
                    int k = i * (lon + 1) + j;
                    v[k] = p; n[k] = p; uv[k] = new Vector2(j / (float)lon, i / (float)lat);
                }
            var tris = new int[lat * lon * 6];
            int t = 0;
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b = a + lon + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            m.vertices = v; m.normals = n; m.uv = uv; m.triangles = tris;
            return m;
        }

        ParticleSystem BasePS(string n, Material m, int max)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = max;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        ParticleSystem MakeEmbers()
        {
            var ps = BasePS("Brasas", SevenGlows.SparkMat, 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(60f, 230f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.45f, 0.15f));
            main.gravityModifier = 3.2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 30f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.6f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1) },
                      new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var trails = ps.trails; trails.enabled = false;
            return ps;
        }

        ParticleSystem MakeSparkles()
        {
            var ps = BasePS("Fagulhas", SevenGlows.SparkMat, 300);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f), new Color(1f, 0.65f, 0.3f));
            main.gravityModifier = -0.25f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 260f; sh.rotation = new Vector3(-90f, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            return ps;
        }

        ParticleSystem MakeColumn(Texture noise)
        {
            var sh0 = Resources.Load<Shader>("Shaders/DustPuff");
            Material m = null;
            if (sh0 != null && sh0.isSupported)
            {
                m = new Material(sh0) { hideFlags = HideFlags.DontSave };
                m.SetTexture("_Noise", noise);
                m.SetColor("_Lit", new Color(1f, 0.55f, 0.25f));
                m.SetFloat(IdLitAmount, 1.2f);
            }
            var ps = BasePS("Coluna de po", m, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(25f, 90f);
            main.startSize = new ParticleSystem.MinMaxCurve(140f, 320f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.85f, 0.8f, 0.85f), new Color(0.7f, 0.65f, 0.6f, 0.7f));
            main.gravityModifier = -0.05f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28f; sh.radius = 60f; sh.rotation = new Vector3(-90f, 0, 0);
            var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 30f; vel.dampen = 0.08f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.6f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sortMode = ParticleSystemSortMode.Distance;
            // o UV.z (semente) do DustPuff vem do "stable random"
            r.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX });
            return ps;
        }

        /// <summary>A nota tocou o chão: começa a sequência.</summary>
        public void Fire()
        {
            t0 = Time.time;
            pressureFired = false;
            coreR.enabled = true;
            // a luz da vila vem do impacto
            var dirToVillage = (GameFlow.SpawnPos - Point); dirToVillage.y = 0f;
            sceneLight.transform.rotation = Quaternion.LookRotation((dirToVillage.normalized + Vector3.down * 0.12f).normalized);
        }

        void Update()
        {
            if (t0 < 0f) return;
            float t = Time.time - t0;
            float s = t - CoreTime;                    // tempo desde o clarão
            // 1. núcleo: encolhe e esquenta; some no clarão (volta pequeno como brasa no centro)
            if (t < CoreTime)
            {
                float k = t / CoreTime;
                core.localScale = Vector3.one * Mathf.Lerp(260f, 70f, k * k);
                Bill(coreR, new Color(1f, 0.85f, 0.55f, Mathf.Lerp(2f, 9f, k)), 0.6f, 1.6f, t);
            }
            else
            {
                float k = Mathf.Exp(-s / 2.5f);
                core.localScale = Vector3.one * (90f + 40f * k);
                Bill(coreR, new Color(1f, 0.72f, 0.4f, 0.8f + 3f * k), 0.9f, 0.6f, t);
            }
            // 2. clarão
            float fl = s < 0f ? 0f : s < 0.05f ? s / 0.05f : Mathf.Exp(-(s - 0.05f) / 0.16f);
            Flash = fl;
            flashR.enabled = s > -0.01f && fl > 0.002f;
            if (flashR.enabled)
            {
                flashT.localScale = Vector3.one * (1600f + 900f * (1f - fl));
                Bill(flashR, new Color(1f, 0.9f, 0.75f, 5f * fl), 4f, 1.2f, 0f);
            }
            // luz no mundo: forte no clarão, cai, fica uma brasa que respira
            float glow = s < 0f ? 0f : 0.45f + 3.6f * fl + 1.4f * Mathf.Exp(-s / 4f);
            glow *= 1f + 0.06f * Mathf.Sin(t * 7.3f) * Mathf.Clamp01(s);
            Glow = glow;
            Shader.SetGlobalColor(IdImpactLight, Warm * glow);
            if (RuptureSky.Instance != null) RuptureSky.Instance.flash = Mathf.Max(fl, 0.25f * Mathf.Exp(-s / 2f) * (s > 0 ? 1 : 0));
            sceneLight.enabled = s > 0f && s < 3.5f;
            sceneLight.intensity = 3.2f * fl + 0.55f * Mathf.Exp(-s / 1.2f);   // o clarão acende a vila inteira por alguns quadros
            // 3/4. ondas de choque
            float R1 = 0f, I1 = 0f, R2 = 0f, I2 = 0f;
            if (s > 0f) { R1 = 2300f * (1f - Mathf.Exp(-s / 1.15f)); I1 = Mathf.Exp(-s / 1.7f); }
            if (s > 0.6f) { float s2 = s - 0.6f; R2 = 1900f * (1f - Mathf.Exp(-s2 / 1.0f)); I2 = 0.55f * Mathf.Exp(-s2 / 1.4f); }
            Shader.SetGlobalVector(IdShock, new Vector4(R1, I1, R2, I2));
            if (dome != null)
            {
                // a cúpula de ar comprimido é rápida e pequena perto da frente no chão (some antes de crescer demais)
                dome.enabled = s > 0f && s < 1.6f;
                if (dome.enabled)
                {
                    float dr = 750f * (1f - Mathf.Exp(-s / 0.55f));
                    dome.transform.localScale = new Vector3(dr, dr * 0.7f, dr);
                    domeMat.SetFloat(IdIntensity, Mathf.Exp(-s / 0.45f) * 0.9f);
                }
            }
            if (ring1 != null)
            {
                ring1.enabled = s > 0.03f && s < 9f;
                if (ring1.enabled)
                {
                    UpdateRing(ringMesh1, R1 * 0.97f, 60f + R1 * 0.16f + s * 18f);
                    ringMat1.SetFloat(IdIntensity, Mathf.SmoothStep(0f, 1f, s / 0.2f) * Mathf.Exp(-s / 4.5f) * 1.2f);
                    ringMat1.SetFloat(IdScroll, s);
                    ringMat1.SetFloat(IdLitAmount, 0.4f + glow * 0.5f);
                }
                ring2.enabled = s > 0.65f && s < 9f;
                if (ring2.enabled)
                {
                    float s2 = s - 0.6f;
                    UpdateRing(ringMesh2, R2 * 0.97f, 35f + R2 * 0.1f + s2 * 10f);
                    ringMat2.SetFloat(IdIntensity, 0.75f * Mathf.SmoothStep(0f, 1f, s2 / 0.3f) * Mathf.Exp(-s2 / 3.5f));
                    ringMat2.SetFloat(IdScroll, s2 + 3f);
                    ringMat2.SetFloat(IdLitAmount, 0.3f + glow * 0.4f);
                }
            }
            // 5. poeira, brasas e fagulhas (disparam no clarão)
            if (s >= 0f && !embers.isPlaying)
            {
                if (s < 0.5f) { embers.Emit(260); column.Emit(26); }
                var em = sparkles.emission; em.rateOverTime = 26f;
                sparkles.Play();
                embers.Play();
                column.Play();
                var cem = column.emission; cem.rateOverTime = s < 0.5f ? 3f : 0f;
            }
            if (column != null && column.GetComponent<ParticleSystemRenderer>().sharedMaterial != null)
                column.GetComponent<ParticleSystemRenderer>().sharedMaterial.SetFloat(IdLitAmount, 0.5f + glow * 0.6f);
            if (s > 12f) { var cem = column.emission; cem.rateOverTime = 0f; var em = sparkles.emission; em.rateOverTime = 6f; }
            // 6. pilar de luz: sobe depois do clarão e fica, fraco
            if (beamMat != null)
            {
                float b = s < 0.15f ? 0f : Mathf.SmoothStep(0f, 1f, (s - 0.15f) / 0.6f) * (1.0f + 2.6f * Mathf.Exp(-(s - 0.15f) / 5f));
                beamMat.SetFloat(IdIntensity, b);
                beam.localScale = new Vector3(90f + 80f * fl + 50f * Mathf.Exp(-s / 5f), 1900f * Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(s / 0.8f)), 1f);
            }
            // 7. a onda de pressão chega na vila
            if (!pressureFired && t >= soundDelay + CoreTime) { pressureFired = true; onPressureWave?.Invoke(); }
        }

        void Bill(Renderer r, Color c, float coreK, float rays, float rot)
        {
            mpb.SetColor(IdColor, c);
            mpb.SetVector(IdParams, new Vector4(coreK, rays, rot * 0.3f, 0f));
            r.SetPropertyBlock(mpb);
        }

        /// <summary>Depois da abertura: fica só a brasa, o pilar fraco e as fagulhas (o resto some).</summary>
        public void Settle()
        {
            if (t0 < 0f) return;
            t0 = Mathf.Min(t0, Time.time - 30f);   // pula as fases fortes
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalColor(IdImpactLight, Color.black);
            Shader.SetGlobalVector(IdShock, Vector4.zero);
        }
    }
}

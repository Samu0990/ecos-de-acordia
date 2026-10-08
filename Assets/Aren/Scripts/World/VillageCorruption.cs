using System.Collections;
using System.Collections.Generic;
using Aren.UI;
using Aren.World.Night;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// A Corrupção chega na rua do mercado, na frente do Aren (cena curta, em tempo real, ~17 s):
    ///   A — por cima do ombro dele: o céu pulsa e dois CÍRCULOS de luz se soltam da Fenda e voam em arco até
    ///       duas pessoas no meio da rua; os outros aldeões gritam e fogem (correm para longe — ninguém some);
    ///   B — cada círculo deita em volta da sua vítima, desce aos pés e sobe pelo corpo erguendo-a do chão: a
    ///       corrupção sobe junto com o anel (a pele vira a do Sussurrante), as tatuagens nascem no peito; a
    ///       primeira (F1) termina ali — o anel se fecha no peito dela e ela fica de pé, tomada (o Eco da luta);
    ///   C — perto do rosto do segundo (M1): o anel aperta no peito girando cada vez mais rápido, as tatuagens
    ///       sobem pelo pescoço até o rosto, a pele descasca e o crânio aparece, os olhos acendem, o corpo
    ///       estica (coluna, braços, garras) e a cabeça pende;
    ///   D0 — o anel se fecha nele com um clarão e o corpo esticado cede de baixo para cima enquanto o
    ///       SUSSURRANTE (prancha do autor, 2,85 m) se forma no mesmo lugar e se ergue — e grita;
    ///   D — os dois se viram para o Aren → o jogo volta e eles SÃO os inimigos da luta (mesmo lugar, mesmo
    ///       quadro; o Eco já vem com as tatuagens e o crânio que a cena deixou).
    /// Os aldeões já estão na rua antes (o jogador os vê ao passar o portão). Testes que pulam direto
    /// para o mercado (DebugJump) chamam Cancel(): a rua fica como antes.
    /// </summary>
    public class VillageCorruption : MonoBehaviour
    {
        public static VillageCorruption Instance { get; private set; }
        public bool Running { get; private set; }
        public bool Done { get; private set; }

        enum Role { Eco, Flee }
        class V
        {
            public GameObject go; public Animator anim; public Renderer[] rends; public Transform head, chest;
            public Role role; public string prefab, ecoPrefab; public Vector3 pos; public float yaw;
            public float corrupt, dissolve, dissolveUp, tattoo, skull; public float seed; public Light light; public ParticleSystem ash;
            public Vector3 fleeTo;
            public CorruptionMorph morph; public FendaRing ring;
            public Candle candle;
        }
        readonly List<V> vs = new List<V>();
        MaterialPropertyBlock mpb;
        Canvas canvas; RectTransform barTop, barBottom; float bars;
        static readonly int IdCorrupt = Shader.PropertyToID("_Corrupt"), IdDissolve = Shader.PropertyToID("_Dissolve"), IdSeed = Shader.PropertyToID("_Seed"),
            IdTattoo = Shader.PropertyToID("_Tattoo"), IdSkull = Shader.PropertyToID("_Skull"), IdDissolveUp = Shader.PropertyToID("_DissolveUp");

        public static VillageCorruption Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Corrupção na vila");
            Instance = go.AddComponent<VillageCorruption>();
            Instance.Spawn();
            return Instance;
        }

        AudioSource murmur;

        void Spawn()
        {
            mpb = new MaterialPropertyBlock();
            // o murmúrio preocupado da rua (ouve-se ao passar o portão)
            var clip = Resources.Load<AudioClip>("Audio/Eleven/crowd_murmur_loop");
            if (clip != null)
            {
                var mg = new GameObject("Murmúrio da rua"); mg.transform.SetParent(transform, false); mg.transform.position = new Vector3(0f, 1.6f, -23f);
                murmur = mg.AddComponent<AudioSource>(); murmur.clip = clip; murmur.loop = true; murmur.spatialBlend = 1f; murmur.rolloffMode = AudioRolloffMode.Linear;
                murmur.minDistance = 6f; murmur.maxDistance = 38f; murmur.volume = 0.55f * ArenAudio.Effects; murmur.dopplerLevel = 0f; murmur.Play();
            }
            // posições na rua do mercado (livre entre x -3,5 e 3,5); olham a Fenda (nor-nordeste, longe)
            Add("Villager_F1", "Eco_F1", Role.Eco, new Vector3(-1.5f, 0f, -23.2f), "Idle_Lantern");
            Add("Villager_M1", "Eco_M1", Role.Eco, new Vector3(1.6f, 0f, -22.2f), "Idle_FoldArms");
            // os outros fogem da Fenda, para longe do Aren e das vítimas (ninguém some: correm até sair de vista)
            Add("Villager_M2", null, Role.Flee, new Vector3(0.6f, 0f, -28.5f), "Call").fleeTo = new Vector3(1.4f, 0f, -6.5f);
            Add("Villager_F2", null, Role.Flee, new Vector3(-2.2f, 0f, -17.0f), "Idle_Lantern").fleeTo = new Vector3(-2.8f, 0f, -4f);
            Add("Villager_F2", null, Role.Flee, new Vector3(2.9f, 0f, -26.0f), "Idle_No").fleeTo = new Vector3(2.6f, 0f, -6f);
            Add("Villager_M1", null, Role.Flee, new Vector3(-3.0f, 0f, -29.5f), "Idle_Lantern").fleeTo = new Vector3(-2.4f, 0f, -5f);
        }

        V Add(string prefab, string eco, Role role, Vector3 p, string idle)
        {
            var src = Resources.Load<GameObject>("Villagers/" + prefab);
            var v = new V { prefab = prefab, ecoPrefab = eco, role = role, seed = Random.value * 10f };
            p.y = Ground(p.x, p.z);
            var fd = NightSetup.Dir(NightSetup.FendaAz, 0f);
            v.yaw = Mathf.Atan2(fd.x, fd.z) * Mathf.Rad2Deg + Random.Range(-25f, 25f);
            v.pos = p;
            vs.Add(v);
            if (src == null) return v;
            v.go = Instantiate(src, p, Quaternion.Euler(0f, v.yaw, 0f), transform);
            v.anim = v.go.GetComponent<Animator>();
            v.anim.cullingMode = AnimatorCullingMode.CullCompletely;   // até a cena: só anima se estiver na tela
            v.anim.Play(idle, 0, Random.value);
            v.anim.SetFloat("Speed", Random.Range(0.9f, 1.1f));
            v.rends = v.go.GetComponentsInChildren<Renderer>();
            v.head = v.anim.GetBoneTransform(HumanBodyBones.Head);
            v.chest = v.anim.GetBoneTransform(HumanBodyBones.Chest) ?? v.head;
            if (idle == "Idle_Lantern") v.candle = Candle.Create(v.anim.GetBoneTransform(HumanBodyBones.RightHand), transform);
            Apply(v);
            return v;
        }

        static float Ground(float x, float z)
        {
            float gy = FarLands.GroundY(x, z);
            int mask = ~((1 << 10) | (1 << 8));
            if (Physics.Raycast(new Vector3(x, gy + 2.2f, z), Vector3.down, out var hit, 5f, mask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return gy;
        }

        void Apply(V v)
        {
            if (v.rends == null) return;
            foreach (var r in v.rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IdCorrupt, v.corrupt); mpb.SetFloat(IdDissolve, v.dissolve); mpb.SetFloat(IdSeed, v.seed);
                mpb.SetFloat(IdTattoo, v.tattoo); mpb.SetFloat(IdSkull, v.skull); mpb.SetFloat(IdDissolveUp, v.dissolveUp);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Carregamento: desenha os aldeões uma vez com luz pontual, corrupção e o círculo (compila as variantes
        /// de shader agora — senão o primeiro quadro da cena travava ~140 ms).
        /// </summary>
        public void Warmup()
        {
            var v = vs.Find(x => x.go != null);
            if (v == null) return;
            var rt = new RenderTexture(256, 144, 24, RenderTextureFormat.DefaultHDR);
            var cg = new GameObject("Aquecimento da Corrupção");
            var cam = cg.AddComponent<Camera>();
            cam.targetTexture = rt; cam.allowHDR = true; cam.enabled = false;
            cam.transform.SetPositionAndRotation(v.pos + new Vector3(0f, 1.4f, -3f), Quaternion.LookRotation(Vector3.forward * 3f + Vector3.down * 0.3f));
            var lg = new GameObject("luz"); lg.transform.position = v.pos + new Vector3(0.5f, 1.2f, -0.5f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 6f; l.intensity = 1.5f;
            v.corrupt = 0.5f; v.tattoo = 0.5f; v.skull = 0.5f; Apply(v);
            var ring = FendaRing.Create(v.pos + Vector3.up * 1.2f, null); ring.radius = 0.6f;
            // o Sussurrante (corpo, lâmina, brilhos, lascas) também compila agora
            var sp = Resources.Load<GameObject>("Sussurrante/Sussurrante");
            GameObject sw = null;
            if (sp != null && !Aren.Enemies.EnemySussurrante.Disabled)
            {
                sw = Instantiate(sp, v.pos + new Vector3(1.2f, 0f, 0.4f), Quaternion.Euler(0f, 180f, 0f));
                sw.GetComponent<Aren.Enemies.EnemySussurrante>()?.SetCinematic(true);   // sussurrante_cena
            }
            cam.Render();
            if (sw != null) Destroy(sw);
            ring.Stop();
            v.corrupt = 0f; v.dissolve = 0f; v.tattoo = 0f; v.skull = 0f; Apply(v);
            Destroy(lg); Destroy(cg);
            rt.Release(); Destroy(rt);
        }

        /// <summary>Teste pulou o roteiro: tira os aldeões e não toca a cena.</summary>
        public void Cancel()
        {
            Done = true;
            foreach (var v in vs) if (v.go != null) Destroy(v.go);
            vs.Clear();
            if (murmur != null) murmur.Stop();
        }

        // ------------------------------------------------------------ a cena

        bool collapseNow;
        Vector3 arenPos;
        Aren.Enemies.EnemySussurrante cineSuss;

        /// <summary>Toca a cena com a câmera 'cam' (já ativa). Devolve, no fim, onde nasceram os Ecos.</summary>
        public IEnumerator Play(Camera cam, Transform aren, List<(GameObject prefab, Vector3 pos, float yaw)> ecos)
        {
            Running = true;
            var snd = OpeningSound.Instance;
            if (murmur != null) StartCoroutine(FadeOut(murmur, 1.6f));
            var sky = RuptureSky.Instance;
            BuildBars();
            foreach (var v in vs) if (v.anim != null) v.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bool cine = RenderScaler.Cinematic;
            if (GameSettings.Quality >= 1) RenderScaler.Cinematic = true;
            Vector3 A = aren.position, up = Vector3.up;
            // luz de preenchimento presa na câmera (os rostos não somem no contraluz das lanternas)
            var fill = new GameObject("Luz de preenchimento").AddComponent<Light>();
            fill.transform.SetParent(cam.transform, false); fill.transform.localPosition = new Vector3(-0.6f, 0.4f, 0.3f);
            fill.type = LightType.Point; fill.range = 9f; fill.intensity = 0.7f; fill.color = new Color(0.75f, 0.8f, 1f); fill.shadows = LightShadows.None;
            Vector3 fendaDir = NightSetup.Dir(NightSetup.FendaAz, 0.42f);
            if (snd != null) { snd.droneLevel = 0.3f; snd.Play("x_riser", 0.42f, 0f); snd.Play("tower_detuned", 0.4f, 0.15f); }
            collapseNow = false; arenPos = A;

            var e0 = vs.Find(x => x.role == Role.Eco);
            var e1 = vs.FindLast(x => x.role == Role.Eco);
            var sussPrefab = Resources.Load<GameObject>("Sussurrante/Sussurrante");
            bool toSuss = e1?.go != null && sussPrefab != null && !Aren.Enemies.EnemySussurrante.Disabled;

            // A — o céu pulsa; dois círculos se soltam da Fenda e voam até as vítimas; os outros fogem
            StartCoroutine(At(0.5f, () => { sky?.Burst(1f); sky?.Wave(); snd?.Play("x_sky_tear", 0.35f, 0.25f); }));
            if (e0?.go != null) StartCoroutine(At(0.9f, () => StartCoroutine(Consume(e0, fendaDir, 2.2f, false))));
            if (e1?.go != null) StartCoroutine(At(1.1f, () => StartCoroutine(Consume(e1, fendaDir, 2.2f, toSuss))));
            foreach (var v in vs) if (v.go != null && v.role == Role.Flee) StartCoroutine(Flee(v, Random.Range(1.3f, 2.1f), cam));
            if (snd != null)
            {
                StartCoroutine(At(1.4f, () => snd.Play("x_crowd_panic", 0.5f, 0f)));
                StartCoroutine(At(1.9f, () => snd.PlayAt("x_running_cobble", new Vector3(0f, 1f, -22f), 0.7f, 3f, 40f)));
                StartCoroutine(At(2.6f, () => snd.PlayAt("x_shutters_slam", new Vector3(5.5f, 2f, -20f), 0.8f, 3f, 40f)));
                StartCoroutine(At(3.5f, () => snd.PlayAt("x_shutters_slam", new Vector3(-5.5f, 2f, -15f), 0.6f, 3f, 40f, 0.92f)));
            }
            var look = aren.GetComponent<CinematicLook>();
            look?.LookAt(new Vector3(0f, 1.6f, -21f), 1f);
            yield return Shot(cam, A + new Vector3(0.75f, 1.62f, -2.0f), new Vector3(0.1f, 2.6f, -22f), 48f,
                                   A + new Vector3(0.65f, 1.6f, -1.4f), new Vector3(0.1f, 1.9f, -22f), 42f, 3.4f);

            // B — os anéis deitam em volta das vítimas, descem aos pés e sobem levando a corrupção
            if (snd != null) snd.fireLevel = 0.35f;
            Vector3 M = e0 != null && e1 != null ? (e0.pos + e1.pos) * 0.5f : new Vector3(0, 0, -20);
            yield return Shot(cam, M + new Vector3(-2.3f, 1.5f, -6.6f), M + new Vector3(0f, 1.15f, -0.6f), 44f,
                                   M + new Vector3(-1.9f, 1.55f, -5.8f), M + new Vector3(0f, 1.3f, -0.6f), 41f, 3.6f);

            // C — perto do rosto do segundo: tatuagens no rosto, crânio, olhos, o corpo esticando
            if (e1?.go != null)
            {
                var toA = A - e1.pos; toA.y = 0f; var toward = toA.sqrMagnitude > 0.01f ? toA.normalized : Vector3.back;
                var side = Vector3.Cross(Vector3.up, toward);
                Vector3 P = e1.pos;
                var hd = e1.head;
                yield return ShotFollow(cam, () => hd != null ? hd.position + up * 0.02f : P + up * 1.7f,
                                        P + toward * 1.25f + side * 0.35f + up * 1.45f, 34f,
                                        P + toward * 2.3f + side * 0.6f + up * 1.55f, 40f, 4.2f);
            }

            // D0 — a TRANSFORMAÇÃO: o anel desce aos pés e sobe; onde ele passa o corpo do aldeão vira o do
            // Sussurrante (mesma altura, mesma pose, mesmo lugar) — e no fim ele grita
            Aren.Enemies.EnemySussurrante suss = null;
            if (toSuss && e1?.go != null)
            {
                collapseNow = true;   // encerra o aperto no peito
                yield return null;
                var fwd = BodyForward(e1);
                cineSuss = null;
                StartCoroutine(BecomeSussurrante(e1, sussPrefab, fwd));
                Vector3 sp0 = e1.pos, toward = fwd;
                Vector3 side = Vector3.Cross(Vector3.up, toward);
                // câmera baixa, de frente: o anel subindo e a forma nova aparecendo atrás dele
                yield return Shot(cam, sp0 + toward * 3.4f + side * 0.9f + up * 0.7f, sp0 + up * 0.8f, 46f,
                                       sp0 + toward * 3.0f + side * 0.7f + up * 0.85f, sp0 + up * 1.35f, 42f, 4.6f);
                suss = cineSuss;
            }
            collapseNow = true;

            // D — os dois tomados se viram para o Aren; rosnam; olhos acesos
            foreach (var v in new[] { e0, e1 })
            {
                if (v?.go == null) continue;
                var to = A - v.pos; to.y = 0f;
                StartCoroutine(Turn(v, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0.9f));
                v.anim.CrossFadeInFixedTime("ZombieIdle", 0.3f);
            }
            StartCoroutine(At(0.35f, () => { if (snd != null && e0?.go != null) snd.PlayAt("x_growl_a", e0.pos + up * 1.6f, 1f, 2f, 30f); }));
            StartCoroutine(At(0.8f, () => { if (snd != null && suss != null) snd.PlayAt("x_growl_b", suss.transform.position + up * 2.4f, 1f, 2f, 30f, 0.85f); }));
            yield return Shot(cam, M + new Vector3(0.3f, 1.3f, -6.2f), M + new Vector3(0f, 1.45f, -0.7f), 48f,
                                   M + new Vector3(0.25f, 1.25f, -5.4f), M + new Vector3(0f, 1.5f, -0.7f), 45f, 2.3f);

            // fim: os inimigos são estas pessoas (mesmo modelo, já tomado) — troca no mesmo quadro
            ecos.Clear();
            if (e0 != null)
            {
                var prefab = Resources.Load<GameObject>("Enemies/" + e0.ecoPrefab);
                ecos.Add((prefab, e0.go != null ? e0.go.transform.position : e0.pos, e0.go != null ? e0.go.transform.eulerAngles.y : e0.yaw));
            }
            if (suss != null)
            {
                ecos.Add((sussPrefab, suss.transform.position, suss.transform.eulerAngles.y));
                Destroy(suss.gameObject);   // o da luta nasce no mesmo quadro, no mesmo lugar
            }
            else if (e1 != null)
            {
                var prefab = Resources.Load<GameObject>("Enemies/" + e1.ecoPrefab);
                ecos.Add((prefab, e1.go != null ? e1.go.transform.position : e1.pos, e1.go != null ? e1.go.transform.eulerAngles.y : e1.yaw));
            }
            // só as vítimas saem (viram os inimigos); quem fugiu continua correndo até sair de vista
            foreach (var v in new[] { e0, e1 })
            {
                if (v == null) continue;
                if (v.light != null) Destroy(v.light.gameObject);
                if (v.ring != null) v.ring.Stop();
                if (v.go != null) Destroy(v.go);
            }
            look?.Release();
            if (fill != null) Destroy(fill.gameObject);
            RenderScaler.Cinematic = cine;
            if (snd != null) { snd.droneLevel = 0.18f; snd.fireLevel = 0.22f; }
            Running = false; Done = true;
            StartCoroutine(HideBars());
        }

        // ------------------------------------------------------------ peças

        IEnumerator At(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        IEnumerator FadeOut(AudioSource s, float dur) { float v0 = s.volume, t = 0f; while (t < dur && s != null) { t += Time.deltaTime; s.volume = v0 * (1f - t / dur); yield return null; } if (s != null) s.Stop(); }

        IEnumerator Shot(Camera cam, Vector3 p0, Vector3 l0, float f0, Vector3 p1, Vector3 l1, float f1, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                float tt = Time.time;
                Vector3 p = Vector3.Lerp(p0, p1, k) + new Vector3(Mathf.PerlinNoise(tt * 0.4f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 0.35f) - 0.5f, 0f) * 0.03f;
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.Lerp(l0, l1, k) - p));
                cam.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
        }

        /// <summary>Plano que acompanha um ponto que se mexe (o rosto que estica e sobe).</summary>
        IEnumerator ShotFollow(Camera cam, System.Func<Vector3> look, Vector3 p0, float f0, Vector3 p1, float f1, float dur)
        {
            float t = 0f;
            Vector3 l = look();
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                float tt = Time.time;
                l = Vector3.Lerp(l, look(), 1f - Mathf.Exp(-Time.deltaTime * 6f));
                Vector3 p = Vector3.Lerp(p0, p1, k) + new Vector3(Mathf.PerlinNoise(tt * 0.5f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 0.45f) - 0.5f, 0f) * 0.035f;
                p.y += (l.y - 1.7f) * 0.55f;   // sobe junto com a cabeça
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(l - p));
                cam.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
        }

        IEnumerator Turn(V v, float yaw, float dur)
        {
            float y0 = v.go.transform.eulerAngles.y, t = 0f;
            while (t < dur && v.go != null) { t += Time.deltaTime; v.go.transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(y0, yaw, Mathf.SmoothStep(0f, 1f, t / dur)), 0f); yield return null; }
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) { float u = 1f - t; return u * u * a + 2f * u * t * b + t * t * c; }

        /// <summary>
        /// O círculo da Fenda pega uma pessoa: voa do céu rasgado em arco, deita em volta dela, desce aos pés e
        /// sobe pelo corpo erguendo-a — a corrupção sobe junto com o anel e as tatuagens nascem no peito. O Eco
        /// termina aí (o anel se fecha no peito); quem vira Sussurrante fica com o anel apertando no peito até
        /// <see cref="collapseNow"/>, enquanto as tatuagens chegam ao rosto, o crânio aparece e o corpo estica.
        /// </summary>
        IEnumerator Consume(V v, Vector3 fendaDir, float flight, bool toSussurrante)
        {
            var snd = OpeningSound.Instance;
            var up = Vector3.up;
            Vector3 start = v.chest.position + fendaDir * 30f;   // perto o bastante para ler como um círculo voando
            var side = Vector3.Cross(fendaDir, up).normalized * (v.seed > 5f ? 1f : -1f);
            var ring = v.ring = FendaRing.Create(start, transform);
            ring.faceCamera = true; ring.radius = 1.8f; ring.intensity = 2f;
            snd?.PlayAt("chime_bad2", v.chest.position + fendaDir * 8f, 0.45f, 3f, 60f, 0.6f);
            // voo em arco (mais rápido no fim: ele "mergulha" na pessoa)
            float t = 0f;
            while (t < flight && v.go != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flight), e = u * u * (3f - 2f * u);
                Vector3 end = v.head.position + up * 0.75f;
                Vector3 ctrl = Vector3.Lerp(start, end, 0.6f) + up * 7f + side * 5f;
                ring.center = Bezier(start, ctrl, end, e);
                ring.radius = Mathf.Lerp(1.8f, 0.55f, e * e);
                ring.intensity = 2f - 0.5f * e;
                yield return null;
            }
            if (v.go == null) { ring.Stop(); yield break; }
            // chegou: deita em volta da pessoa; a chama do castiçal fica violeta e cai
            ring.faceCamera = false; ring.normal = up;
            snd?.PlayAt(Random.value < 0.5f ? "chime_bad2" : "chime_bad4", v.chest.position, 0.7f, 2f, 40f, Random.Range(0.55f, 0.7f));
            ArenVFX.Flash(v.head.position, new Color(0.7f, 0.35f, 1f), 2.6f, 5f, 0.35f);
            if (v.candle != null) { v.candle.Taint(); v.candle.Drop(); v.candle = null; }
            bool female = v.prefab.Contains("_F");
            snd?.PlayAt(female ? "x_scream_woman_a" : "x_scream_man_a", v.head.position, 0.8f, 3f, 45f, Random.Range(0.95f, 1.05f));
            v.anim.CrossFadeInFixedTime("Stagger", 0.15f);
            v.anim.SetFloat("Speed", 0.85f);
            var lg = new GameObject("Luz da corrupcao"); lg.transform.SetParent(v.go.transform, false); lg.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
            v.light = lg.AddComponent<Light>(); v.light.type = LightType.Point; v.light.color = new Color(0.65f, 0.3f, 1f); v.light.range = 4.5f; v.light.intensity = 0f; v.light.shadows = LightShadows.None;
            v.morph = v.go.AddComponent<CorruptionMorph>();
            // quem vira Sussurrante não cresce (ele tem a altura do aldeão): garras, ombros e cabeça caindo
            if (toSussurrante) { v.morph.stretch = 1.05f; v.morph.claws = 1.6f; v.morph.hunch = 16f; }
            float H = 1.8f;
            // desce da cabeça aos pés
            Vector3 c0 = ring.center;
            t = 0f;
            while (t < 0.6f && v.go != null)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                ring.center = Vector3.Lerp(c0, v.go.transform.position + up * 0.12f, u);
                ring.radius = Mathf.Lerp(0.5f, 0.62f, u);
                v.morph.hover = 0.08f * u;
                yield return null;
            }
            if (v.go == null) { ring.Stop(); yield break; }
            // sobe: a corrupção vem junto com o anel; a pessoa é erguida e convulsiona; tatuagens no peito
            v.anim.CrossFadeInFixedTime("Convulse", 0.2f);
            v.anim.SetFloat("Speed", 0.7f);
            snd?.PlayAt("x_corrupt_transform", v.chest.position, 0.7f, 2f, 30f);
            t = 0f; float rise = 2.2f;
            while (t < rise && v.go != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / rise), e = Mathf.SmoothStep(0f, 1f, u);
                float hr = Mathf.Lerp(0.12f, H * 0.95f, e);
                ring.center = v.go.transform.position + up * hr;
                ring.radius = 0.62f - 0.12f * Mathf.Sin(u * Mathf.PI);
                ring.spin = 1f + u;
                v.corrupt = Mathf.Clamp01(hr / H / 1.25f + 0.04f);
                v.tattoo = Mathf.Lerp(0f, 0.55f, Mathf.Clamp01((u - 0.35f) / 0.65f));
                v.morph.hover = Mathf.Lerp(0.08f, 0.28f, e) + 0.03f * Mathf.Sin(Time.time * 9f);
                v.light.intensity = (1.2f + 1.6f * Mathf.PerlinNoise(t * 9f, v.seed)) * (0.4f + 0.6f * e);
                if (Random.value < Time.deltaTime * 8f) ArenVFX.Glyphs(v.chest.position + Random.insideUnitSphere * 0.3f, new Color(0.7f, 0.3f, 1f), 1, 0.8f);
                Apply(v);
                yield return null;
            }
            if (v.go == null) { ring.Stop(); yield break; }

            if (!toSussurrante)
            {
                // o Eco: o anel se fecha no peito; ela desce ao chão, tomada
                var chest = v.chest;
                StartCoroutine(ring.Collapse(() => chest != null ? chest.position : v.pos + up * 1.3f, 0.55f));
                v.ring = null;
                t = 0f;
                while (t < 0.9f && v.go != null)
                {
                    t += Time.deltaTime;
                    float u = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                    v.corrupt = Mathf.Lerp(v.corrupt, 1f, u); v.tattoo = Mathf.Lerp(0.55f, 1f, u); v.skull = Mathf.Lerp(0f, 0.55f, u);
                    v.morph.hover = Mathf.Lerp(0.28f, 0f, u * u);
                    Apply(v);
                    yield return null;
                }
                if (v.go == null) yield break;
                v.corrupt = 1f; v.tattoo = 1f; v.skull = 0.55f; Apply(v);
                v.morph.enabled = false;
                v.light.intensity = 1.1f;
                v.anim.CrossFadeInFixedTime("ZombieIdle", 0.4f); v.anim.SetFloat("Speed", 1f);
                yield break;
            }

            // o Sussurrante: o anel aperta no peito e gira cada vez mais rápido; o rosto muda
            snd?.PlayAt(female ? "x_scream_woman_b" : "x_scream_man_b", v.head.position, 0.75f, 3f, 45f, 0.82f);
            StartCoroutine(At(1.6f, () => { if (v.go != null) snd?.PlayAt(Random.value < 0.5f ? "x_scream_choir_a" : "x_scream_choir_b", v.head.position, 0.7f, 3f, 45f, 0.78f); }));
            t = 0f; float hold = 4.6f;
            float r0 = ring.radius;
            var toA = arenPos - v.go.transform.position; toA.y = 0f;
            if (toA.sqrMagnitude > 0.01f) StartCoroutine(Turn(v, Mathf.Atan2(toA.x, toA.z) * Mathf.Rad2Deg + YawFix(v), 1.6f));   // vira o rosto para o Aren
            bool idle = false;
            while (!collapseNow && v.go != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / hold), e = Mathf.SmoothStep(0f, 1f, u);
                ring.center = Vector3.Lerp(ring.center, v.chest.position, 1f - Mathf.Exp(-Time.deltaTime * 4f));
                ring.radius = Mathf.Lerp(r0, 0.36f, e);
                ring.spin = Mathf.Lerp(2f, 5f, e);
                ring.intensity = 1.3f + 0.5f * e + 0.2f * Mathf.Sin(Time.time * 13f);
                v.corrupt = Mathf.Lerp(v.corrupt, 1f, Time.deltaTime * 2f);
                v.tattoo = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(u * 1.3f));
                v.skull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.15f) / 0.7f));
                v.morph.amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.3f) / 0.7f));
                v.morph.hover = 0.28f + 0.04f * Mathf.Sin(Time.time * 11f);
                // no fim fica na postura curvada do Sussurrante (o mesmo clipe que ele usa parado)
                if (!idle && u > 0.82f) { idle = true; v.anim.CrossFadeInFixedTime("ZombieIdle", 0.5f); }
                v.anim.SetFloat("Speed", idle ? 1f : Mathf.Lerp(0.7f, 1.35f, e));
                v.light.intensity = 0.8f + 1.1f * e * Mathf.PerlinNoise(Time.time * 8f, v.seed);   // o rosto (osso, tinta) precisa ler
                Apply(v);
                yield return null;
            }
        }

        /// <summary>Frente do CORPO (pelos ombros — a raiz do modelo nem sempre olha para +Z).</summary>
        static Vector3 BodyForward(V v)
        {
            var l = v.anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); var r = v.anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l == null || r == null) return v.go.transform.forward;
            var right = r.position - l.position; right.y = 0f;
            return right.sqrMagnitude < 1e-6f ? v.go.transform.forward : Vector3.Cross(right.normalized, Vector3.up);
        }

        /// <summary>Quanto girar a raiz para o CORPO olhar para onde a raiz olha (0 se o modelo olha +Z).</summary>
        static float YawFix(V v)
        {
            var f = BodyForward(v); var g = v.go.transform.forward; f.y = 0f; g.y = 0f;
            return Vector3.SignedAngle(f, g, Vector3.up);
        }

        static float BodyHeight(GameObject g)
        {
            float h = 0f;
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>()) h = Mathf.Max(h, smr.bounds.max.y - g.transform.position.y);
            return h;
        }

        /// <summary>
        /// O Sussurrante nasce INVISÍVEL dentro do aldeão (sem nenhum quadro inteiro): mesma altura (escala pela
        /// altura medida — quando o prefab já tiver a altura do aldeão o fator é ~1), _Spawn = 0 já no primeiro
        /// quadro, lâmina escondida, e na mesma pose (o parado curvado, no mesmo instante do clipe).
        /// </summary>
        void PrepareHidden(Aren.Enemies.EnemySussurrante s, V v)
        {
            float hv = BodyHeight(v.go), hs = BodyHeight(s.gameObject);
            if (hv > 0.5f && hs > 0.5f && Mathf.Abs(hs / hv - 1f) > 0.03f) s.transform.localScale *= hv / hs;
            var b = new MaterialPropertyBlock();
            foreach (var r in s.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(b);
                b.SetFloat("_Spawn", 0f);
                b.SetFloat("_Height", hv);   // a frente do _Spawn na mesma escala do dissolve do aldeão
                r.SetPropertyBlock(b);
                if (!(r is SkinnedMeshRenderer)) r.enabled = false;   // lâmina e brilhos: só depois de formado
            }
            var st = v.anim.GetCurrentAnimatorStateInfo(0);
            float sec = (st.normalizedTime - Mathf.Floor(st.normalizedTime)) * st.length;
            s.PlayCinematic("Locomotion", 0f, 1f, sec);
            v.anim.SetFloat("Speed", 1f);
        }

        /// <summary>
        /// A transformação. O anel desce aos pés e sobe pelo corpo; na altura dele o aldeão some (dissolve dos
        /// pés para cima, borda em brasa) e o Sussurrante se forma no MESMO lugar, na MESMA pose e com a MESMA
        /// frente (_Spawn dele e _Dissolve do aldeão usam a mesma fórmula e o mesmo valor). Lá em cima o anel se
        /// fecha na cabeça num clarão, os olhos acendem e ele grita.
        /// </summary>
        IEnumerator BecomeSussurrante(V v, GameObject prefab, Vector3 fwd)
        {
            var snd = OpeningSound.Instance;
            Aren.Enemies.EnemySussurrante s = null;
            var ring = v.ring;
            float H = v.go != null ? Mathf.Max(1.2f, BodyHeight(v.go)) : 1.8f;
            Vector3 basePos = v.go != null ? v.go.transform.position : v.pos;
            // o anel cai aos pés (a pessoa desce ao chão)
            float t = 0f;
            Vector3 c0 = ring != null ? ring.center : basePos + Vector3.up * 1.3f;
            while (t < 0.4f && v.go != null)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.4f);
                if (ring != null) { ring.center = Vector3.Lerp(c0, basePos + Vector3.up * 0.05f, u); ring.radius = Mathf.Lerp(ring.radius, 0.62f, u); ring.spin = 3f; ring.intensity = 1.6f; }
                if (v.morph != null) { v.morph.hover = Mathf.Lerp(0.3f, 0f, u); v.morph.amount = Mathf.Lerp(v.morph.amount, 0.35f, u); }
                yield return null;
            }
            if (v.go == null) yield break;
            // só agora ele existe — e já invisível, na pose e na altura do aldeão; a onda começa neste quadro
            var sg = Instantiate(prefab, basePos, Quaternion.LookRotation(fwd));
            s = cineSuss = sg.GetComponent<Aren.Enemies.EnemySussurrante>();
            if (s == null) { Destroy(sg); yield break; }
            s.SetCinematic(true);
            PrepareHidden(s, v);
            snd?.PlayAt("x_corrupt_transform", basePos + Vector3.up, 1f, 2f, 35f, 0.7f);
            snd?.PlayAt(Random.value < 0.5f ? "x_scream_choir_a" : "x_scream_choir_b", basePos + Vector3.up * 1.6f, 0.85f, 3f, 45f, 0.72f);
            if (v.go != null) { v.ash = MakeAsh(v); var em0 = v.ash.emission; em0.rateOverTime = 180f; }
            // a onda: a mesma curva do Materialize do Sussurrante (SmoothStep no tempo) dirige o aldeão e o anel
            const float wave = 2.0f;
            s.FX.Materialize(wave);
            t = 0f;
            while (t < wave)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / wave));
                float front = Mathf.Clamp(k * 1.18f - 0.09f, 0f, 1.02f) * H;
                if (v.go != null)
                {
                    v.dissolveUp = 1f; v.dissolve = k;
                    foreach (var r in v.rends) if (r != null) { r.GetPropertyBlock(mpb); mpb.SetFloat("_DissolveHeight", H); r.SetPropertyBlock(mpb); }
                    if (v.light != null) { v.light.intensity = 2.6f; v.light.transform.position = basePos + Vector3.up * front + v.go.transform.forward * 0.4f; }
                    Apply(v);
                }
                if (ring != null) { ring.center = basePos + Vector3.up * Mathf.Max(0.05f, front); ring.radius = 0.55f + 0.1f * Mathf.Sin(t * 7f); ring.spin = 3.5f; }
                yield return null;
            }
            if (v.ash != null) { var em = v.ash.emission; em.rateOverTime = 0f; v.ash.transform.SetParent(transform, true); Destroy(v.ash.gameObject, 4f); }
            if (v.go != null) { if (v.light != null) v.light.transform.SetParent(transform, true); Destroy(v.go); }
            // a lâmina e os brilhos voltam (o FX religa a lâmina quando _Spawn passa de 0,6)
            foreach (var r in s.GetComponentsInChildren<Renderer>()) if (!(r is SkinnedMeshRenderer) && !(r is ParticleSystemRenderer)) r.enabled = true;
            // o anel se fecha na cabeça num clarão
            if (ring != null)
            {
                var head = s.FX.Mouth;
                yield return ring.Collapse(() => head != null ? head.position : basePos + Vector3.up * (H - 0.15f), 0.35f);
                v.ring = null;
            }
            if (v.light != null) Destroy(v.light.gameObject, 0.5f);
            yield return new WaitForSeconds(0.25f);
            if (s == null) yield break;
            // de pé: os olhos acendem e ele grita (só a imagem e o som: ainda não há Distorção)
            s.FX.SetEyes(3f);
            s.PlayCinematic("Scream", 0.25f, 1.25f, 0.33f);
            yield return new WaitForSeconds(0.45f);
            if (s == null) yield break;
            s.FX.ScreamRelease(5.5f);
            snd?.PlayAt("x_scream_choir_b", s.FX.Mouth.position, 1f, 3f, 50f, 0.7f);
            yield return new WaitForSeconds(1.1f);
            if (s == null) yield break;
            s.FX.ScreamEnd();
            s.PlayCinematic("Locomotion", 0.4f);
        }

        ParticleSystem MakeAsh(V v)
        {
            var go = new GameObject("Cinza");
            go.transform.SetParent(v.go.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.25f), new Color(0.75f, 0.35f, 1f));
            main.gravityModifier = -0.12f;
            main.maxParticles = 600;
            var sh = ps.shape;
            var smr = v.go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) { sh.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer; sh.skinnedMeshRenderer = smr; sh.meshShapeType = ParticleSystemMeshShapeType.Triangle; }
            else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f; }
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f); vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.8f; noise.scrollSpeed = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.5f), 0f), new GradientColorKey(new Color(0.7f, 0.3f, 1f), 0.45f), new GradientColorKey(new Color(0.15f, 0.12f, 0.16f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SevenGlows.SparkMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// Foge: corre até o ponto de fuga e continua na mesma direção até sair de vista (de qualquer câmera)
        /// — só então sai da cena. Ninguém some na frente do jogador.
        /// </summary>
        static readonly Plane[] planes = new Plane[6];

        IEnumerator Flee(V v, float delay, Camera cam)
        {
            yield return new WaitForSeconds(delay);
            if (v.go == null) yield break;
            var to = v.fleeTo - v.go.transform.position; to.y = 0f;
            var dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
            v.go.transform.rotation = Quaternion.LookRotation(dir);
            v.anim.CrossFadeInFixedTime("Run", 0.15f);
            v.anim.SetFloat("Speed", Random.Range(1.0f, 1.15f));
            if (v.candle != null) { v.candle.Drop(); v.candle = null; }
            var snd = OpeningSound.Instance;
            if (Random.value < 0.7f) snd?.PlayAt(v.prefab.Contains("_F") ? "x_scream_woman_b" : "x_scream_man_b", v.head.position, 0.55f, 3f, 40f, 1.08f);
            float t = 0f, hidden = 0f;
            bool past = false;
            while (v.go != null && t < 40f)
            {
                t += Time.deltaTime;
                Vector3 p = v.go.transform.position;
                if (!past && (p - v.fleeTo).sqrMagnitude < 0.5f) past = true;
                Vector3 target = past ? p + dir * 4f : v.fleeTo;
                p = Vector3.MoveTowards(p, target, 5.2f * Time.deltaTime);
                p.y = Ground(p.x, p.z);
                v.go.transform.position = p;
                // "visto" = dentro do campo da câmera do jogo (isVisible também conta a sombra, não serve)
                bool seen = false;
                var c = Camera.main != null ? Camera.main : cam;
                if (c != null && v.rends != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(c, planes);
                    foreach (var r in v.rends) if (r != null && GeometryUtility.TestPlanesAABB(planes, r.bounds)) { seen = true; break; }
                }
                hidden = seen ? 0f : hidden + Time.deltaTime;
                if (past && hidden > 0.6f) break;
                yield return null;
            }
            if (v.go != null) v.go.SetActive(false);
        }

        void BuildBars()
        {
            if (canvas != null) { canvas.gameObject.SetActive(true); StartCoroutine(ShowBars()); return; }
            canvas = UIKit.MakeCanvas("Corrupção (faixas)", 45);
            canvas.transform.SetParent(transform, false);
            barTop = UIKit.Rect("Topo", canvas.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barTop.gameObject.AddComponent<Image>().color = Color.black;
            barBottom = UIKit.Rect("Base", canvas.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));
            barBottom.gameObject.AddComponent<Image>().color = Color.black;
            StartCoroutine(ShowBars());
        }

        IEnumerator ShowBars() { while (bars < 1f) { bars = Mathf.MoveTowards(bars, 1f, Time.unscaledDeltaTime * 2.5f); SetBars(); yield return null; } }
        IEnumerator HideBars() { while (bars > 0f) { bars = Mathf.MoveTowards(bars, 0f, Time.unscaledDeltaTime * 2f); SetBars(); yield return null; } if (canvas != null) canvas.gameObject.SetActive(false); }
        void SetBars() { float h = 120f * Mathf.SmoothStep(0f, 1f, bars); barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h); }

        /// <summary>Um Eco com corpo de aldeão (variante aleatória); null se os prefabs não existirem.</summary>
        public static GameObject RandomEcoPrefab()
        {
            if (ecoPrefabs == null) ecoPrefabs = Resources.LoadAll<GameObject>("Enemies");
            return ecoPrefabs.Length > 0 ? ecoPrefabs[Random.Range(0, ecoPrefabs.Length)] : null;
        }
        static GameObject[] ecoPrefabs;

        void OnDestroy() { if (Instance == this) Instance = null; }
    }

    /// <summary>
    /// Castiçal na mão de um aldeão (prato de bronze, vela, chama, halo e luz quente que tremula). Quando a
    /// Corrupção chega a chama fica violeta; quando a pessoa é tomada ou foge, ele cai, rola e apaga.
    /// </summary>
    public class Candle : MonoBehaviour
    {
        Transform hand; Light l; Renderer halo; MaterialPropertyBlock mpb; Color col = new Color(1f, 0.62f, 0.32f);
        float baseI = 1.3f, fade = 1f; bool dropped; float seed;
        static Material flameMat, glowMat, waxMat, brassMat;

        public static Candle Create(Transform hand, Transform parent)
        {
            if (hand == null) return null;
            if (flameMat == null) { var tf = GameObject.Find("TorchFlame"); if (tf != null) flameMat = tf.GetComponent<Renderer>().sharedMaterial; }
            if (glowMat == null) glowMat = NightSetup.GlowMat;
            if (waxMat == null) waxMat = FindMat("CMP_plaster") ?? FindMat("CMP_stone");
            if (brassMat == null) brassMat = FindMat("Bronze") ?? FindMat("CMP_iron");
            var go = new GameObject("Castiçal");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Candle>();
            c.hand = hand; c.seed = Random.value * 10f; c.mpb = new MaterialPropertyBlock();
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.006f, 0.1f), brassMat);
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.028f, 0.05f, 0.028f), waxMat);
            if (flameMat != null)
            {
                var f = new GameObject("Chama"); f.transform.SetParent(go.transform, false);
                f.transform.localPosition = new Vector3(0f, 0.125f, 0f); f.transform.localScale = Vector3.one * 0.07f;
                f.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var mr = f.AddComponent<MeshRenderer>(); mr.sharedMaterial = flameMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fm = new MaterialPropertyBlock(); fm.SetFloat("_Size", 0.075f); mr.SetPropertyBlock(fm);   // chama de vela, não de tocha
            }
            if (glowMat != null)
            {
                var h = new GameObject("Halo"); h.transform.SetParent(go.transform, false);
                h.transform.localPosition = new Vector3(0f, 0.13f, 0f); h.transform.localScale = Vector3.one * 0.55f;
                h.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var mr = h.AddComponent<MeshRenderer>(); mr.sharedMaterial = glowMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.halo = mr;
            }
            var lg = new GameObject("Luz"); lg.transform.SetParent(go.transform, false); lg.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            c.l = lg.AddComponent<Light>(); c.l.type = LightType.Point; c.l.range = 3.4f; c.l.color = c.col; c.l.intensity = c.baseI; c.l.shadows = LightShadows.None;
            return c;
        }

        static void Part(Transform p, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); if (m != null) r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material FindMat(string name)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith(name)) return m;
            return null;
        }

        /// <summary>A Corrupção chegou: a chama fica violeta.</summary>
        public void Taint() { col = new Color(0.72f, 0.35f, 1f); baseI = 1.8f; }

        /// <summary>Cai da mão, rola um pouco e apaga.</summary>
        public void Drop()
        {
            if (dropped) return;
            dropped = true;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.3f; rb.linearVelocity = new Vector3(Random.Range(-0.6f, 0.6f), 0.8f, Random.Range(-0.6f, 0.6f)); rb.angularVelocity = Random.insideUnitSphere * 6f;
            var bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.05f, 0f); bc.size = new Vector3(0.1f, 0.12f, 0.1f);
            Destroy(gameObject, 4f);
        }

        void LateUpdate()
        {
            if (!dropped && hand != null)
            {
                // na palma, sempre de pé (a mão balança, a vela não tomba)
                transform.position = hand.position + Vector3.up * 0.02f;
                transform.rotation = Quaternion.Euler(0f, hand.eulerAngles.y, 0f);
            }
            if (dropped) fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * 1.4f);
            float fl = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, seed);
            if (l != null) { l.color = col; l.intensity = baseI * fl * fade; l.enabled = fade > 0.01f; }
            if (halo != null) { mpb.SetColor("_Color", new Color(col.r, col.g, col.b, 0.55f * fl * fade)); halo.SetPropertyBlock(mpb); halo.enabled = fade > 0.01f; }
            var fl0 = transform.Find("Chama"); if (fl0 != null) fl0.gameObject.SetActive(fade > 0.3f);
        }
    }
}
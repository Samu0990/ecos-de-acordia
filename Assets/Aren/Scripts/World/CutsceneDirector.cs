using System.Collections;
using Aren.Combat;
using Aren.UI;
using Aren.World.Night;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// Abertura do jogo (v2, "de jogo de verdade"). Prólogo em motion graphics (a nota → os doze
    /// sinos, em harmonia) e a noite da Ruptura em Campanula, seguindo o storyboard do autor:
    ///   S0 o sino da estrada (corte casado com o fim do prólogo); a flauta do Aren começa
    ///   S1 plano de estabelecimento em grua: a estrada, o Aren tocando, a muralha, a vila, as serras
    ///   S2 o bardo: ele toca para a vila; os sinos da torre respondem afinados
    ///   S3 algo está errado: uma nota da flauta ENTORTA; ele para; o sino ao lado canta sozinho e
    ///      desafinado (a poeira na luz vibra, as chamas batem); a torre responde errada; ele procura
    ///   S4 a Fenda: um ponto de luz muito longe, a rachadura corre, o céu estilhaça (plano sobre o
    ///      ombro → o rosto dele na luz violeta → plano aberto de escala: Aren, vila, vale, serras, Fenda)
    ///   S5 os sete: a Fenda inspira e solta sete notas, contáveis, em ritmo irregular
    ///   S6 um deles passa por cima da vila e desce pesado no planalto do leste (a grua revela o vale);
    ///      os grilos calam (silêncio relativo — a nota impossível e o vento continuam)
    ///   S7 o impacto, MUITO longe: núcleo → clarão → ondas de choque pela paisagem; segundos depois
    ///      a onda de pressão chega: a câmera reage (antecipação → golpe → recuperação), a vila
    ///      chacoalha, o sino responde, ele se protege; depois, o silêncio cheio de som errado
    ///   S8 título; ele se volta para a vila; a câmera pousa na câmera de jogo (sem corte)
    /// Regra de som: a música do mundo DESAFINA — nunca fica muda. Espaço / Esc / Enter / clique pulam.
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        public bool Playing { get; private set; }
        public bool Skipped { get; private set; }
        /// <summary>Teste (-eda-intro-noprologue): começa direto na noite.</summary>
        public static bool DebugSkipPrologue;

        Camera cam;
        AudioListener camListener, mainListener;
        Camera mainCam;
        Canvas canvas;
        RectTransform barTop, barBottom;
        Image black;
        Text subtitle, title, titleSub, skipHint;
        PrologueFX prologue;
        Image flash; float flashA;
        float barK, barTarget = 1f, subAlpha, subTarget, titleAlpha, titleTarget;
        bool skipRequested;
        float shake;
        float fov = 40f;
        float handheld = 1f;
        // mola da reação da câmera (posição em metros no espaço da câmera, rotação em graus)
        Vector3 kickP, kickPV, kickR, kickRV;
        CinematicRig rig;
        Transform arenT;
        Vector3 pinPos; bool pinned;

        void FixedUpdate() { Pin(); }
        void LateUpdate() { Pin(); }
        /// <summary>O Aren fica exatamente no lugar durante a abertura (a animação/física não o arrasta).</summary>
        void Pin()
        {
            if (!pinned || arenT == null) return;
            var rb = arenT.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = pinPos; rb.linearVelocity = Vector3.zero; }
            arenT.position = pinPos;
        }

        public void Setup(Camera cinemaCam)
        {
            cam = cinemaCam;
            camListener = cam.GetComponent<AudioListener>();
            if (camListener == null) camListener = cam.gameObject.AddComponent<AudioListener>();
            camListener.enabled = false;
            BuildUI();
        }

        void BuildUI()
        {
            canvas = UIKit.MakeCanvas("Cutscene", 50);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            barTop = UIKit.Rect("BarTop", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 130));
            barTop.gameObject.AddComponent<Image>().color = Color.black;
            barBottom = UIKit.Rect("BarBottom", root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 130));
            barBottom.gameObject.AddComponent<Image>().color = Color.black;
            var bl = UIKit.Rect("Black", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            black = bl.gameObject.AddComponent<Image>(); black.color = Color.black; black.raycastTarget = false;
            prologue = PrologueFX.Create(root);   // motion graphics do começo (por cima do preto, sob as legendas)
            subtitle = UIKit.Label("Legenda", root, "", UIKit.Serif, 32, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 66), new Vector2(1500, 110));
            subtitle.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            title = UIKit.Label("Titulo", root, "", UIKit.Display, 96, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1700, 140));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            titleSub = UIKit.Label("Subtitulo", root, "", UIKit.Serif, 32, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 66), new Vector2(1600, 60));
            var fl = UIKit.Rect("Clarao", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            flash = fl.gameObject.AddComponent<Image>(); flash.color = new Color(1f, 0.98f, 0.94f, 0f); flash.raycastTarget = false;
            skipHint = UIKit.Label("Pular", root, "Espaço / Esc: pular", UIKit.Sans, 20, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-240, 42), new Vector2(400, 40));   // pivô no centro: -240 deixa a borda direita a 40 px da tela
            canvas.gameObject.SetActive(false);
        }

        public void Skip() { if (Playing) skipRequested = true; }

        /// <summary>Toca a abertura. 'aren' = transform do jogador já posicionado no início da estrada.</summary>
        public IEnumerator Play(Transform aren, BellRinger bells, GameObject deerPrefab, ArenFlute flute)
        {
            Playing = true; Skipped = false; skipRequested = false;
            arenT = aren;
            Debug.Log($"[Abertura] Play em {Time.realtimeSinceStartup:0.00}");
            canvas.gameObject.SetActive(true);
            black.color = Color.black;
            barK = 0f; barTarget = 0f; ApplyBars();   // o prólogo é em tela cheia; as faixas entram na parte 3D
            cam.gameObject.SetActive(true);
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = NightSetup.FarClip;
            mainCam = Camera.main != null && Camera.main != cam ? Camera.main : null;
            mainListener = mainCam != null ? mainCam.GetComponent<AudioListener>() : null;
            if (mainListener != null) mainListener.enabled = false;
            if (mainCam != null) mainCam.enabled = false;   // não desenha a cena duas vezes (o Cinemachine continua posicionando)
            camListener.enabled = true;
            ArenAudio.SetIntensity(0f);
            bool bloom = RenderScaler.PostBloom;
            if (GameSettings.Quality >= 1) RenderScaler.PostBloom = true;
            pinPos = aren.position; pinned = true;
            var routine = StartCoroutine(Sequence(aren, flute));
            while (Playing && !skipRequested)
            {
                if (SkipPressed()) skipRequested = true;
                yield return null;
            }
            if (skipRequested && Playing)
            {
                Skipped = true;
                StopCoroutine(routine);
                prologue.Stop();
                yield return FadeBlack(1f, 0.35f);
            }
            prologue.Stop();
            flashA = 0f; UIKit.SetAlpha(flash, 0f);
            subTarget = 0f; titleTarget = 0f; subAlpha = 0f; titleAlpha = 0f;
            RenderScaler.PostBloom = bloom;
            var look = aren.GetComponent<CinematicLook>();
            if (look != null) look.Release();
            var perf = aren.GetComponentInChildren<ArenFlutePerformancePose>();
            if (perf != null) perf.cinematic = 0f;
            // estado final mesmo se pulou: Fenda aberta, impacto já aconteceu (fica a brasa e o pilar), som doente
            var sky = RuptureSky.Instance;
            if (sky != null) { sky.fendaOpen = 1f; sky.inhale = 0f; }
            if (SevenGlows.Instance != null) Destroy(SevenGlows.Instance.gameObject);
            var fx = ImpactFX.Instance;
            if (fx == null && FarLands.Root != null) { fx = ImpactFX.Prepare(FarLands.ImpactPoint); fx.Fire(); }
            if (fx != null) { fx.onPressureWave = null; fx.Settle(); }
            if (VillageFX.Instance != null) Destroy(VillageFX.Instance.gameObject);
            var snd = OpeningSound.Instance;
            if (snd != null) { snd.Corruption = Mathf.Max(snd.Corruption, 0.6f); snd.duck = 1f; snd.lute = 1f; }
            if (BellShrine.Instance != null) BellShrine.Instance.hum = 0f;
            pinned = false;
            if (Skipped) SetYaw(aren, 0f);
            rig?.End(); rig = null;
            // devolve a câmera ao jogo
            camListener.enabled = false;
            if (mainListener != null) mainListener.enabled = true;
            if (mainCam != null) mainCam.enabled = true;
            cam.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);
            if (flute != null) { flute.KeepDrawn = false; flute.Holster(); }
            Playing = false;
        }

        static bool SkipPressed()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame)
                || (g != null && (g.startButton.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame));
        }

        // ------------------------------------------------------------ roteiro

        IEnumerator Sequence(Transform aren, ArenFlute flute)
        {
            var snd = OpeningSound.Instance;
            var sky = RuptureSky.Instance;
            var shrine = BellShrine.Instance;
            var look = aren.GetComponent<CinematicLook>() ?? aren.gameObject.AddComponent<CinematicLook>();
            var perf = aren.GetComponentInChildren<ArenFlutePerformancePose>();
            Vector3 up = Vector3.up, fwd = Vector3.forward, right = Vector3.right;
            Vector3 A = aren.position;
            // olhos de verdade: o osso da cabeça deste modelo fica a ~1,38 m (os olhos ~8 cm acima)
            var anim = aren.GetComponentInChildren<Animator>();
            var hb = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 head = hb != null ? new Vector3(A.x, hb.position.y + 0.09f, A.z) : A + up * 1.48f;
            float eyeH = head.y - A.y;
            Vector3 BC = shrine != null ? shrine.BellCenter : A + right * 2.6f + up * 2.2f;
            Vector3 LP = shrine != null ? shrine.LanternPos : BC + up * 0.2f;
            var towerGo = GameObject.Find("BellTower");
            Vector3 towerTop = towerGo != null ? towerGo.transform.position + up * 21f : new Vector3(0, 21, 36);
            Vector3 Fd(float el) => NightSetup.Dir(NightSetup.FendaAz, el);
            Vector3 fendaC = A + Fd(SevenGlows.FendaCenterEl) * 3000f;
            Vector3 IP = FarLands.Root != null ? FarLands.ImpactPoint : A + NightSetup.Dir(NightSetup.ImpactAz, 0.06f) * 2500f;
            Vector3 eDir = IP - A; eDir.y = 0f; eDir.Normalize();
            Vector3 eSide = Vector3.Cross(up, eDir);   // à direita de quem olha para o impacto
            float impactYaw = Mathf.Atan2(eDir.x, eDir.z) * Mathf.Rad2Deg;
            var village = VillageFX.Create(LP + Vector3.down * 0.15f);
            if (sky != null) { sky.fendaOpen = 0f; sky.fendaPulse = 0f; }
            if (snd != null) { snd.Corruption = 0f; snd.lute = 0f; snd.duck = 1f; }
            SetYaw(aren, 0f);
            // a flauta vai para a mão durante o prólogo (o som de sacar some debaixo dele)
            if (flute != null) { flute.KeepDrawn = true; flute.Draw(); }
            if (perf != null) perf.cinematic = 1f;
            look.breath = 0.3f;

            // 0 — prólogo: a nota que procura outra nota, e os doze sinos (ainda em harmonia).
            // O prólogo cobre a tela inteira: a câmera 3D só limpa (não desenha a vila escondida atrás).
            cam.transform.SetPositionAndRotation(BC + new Vector3(-0.9f, -0.55f, -1.1f), Quaternion.LookRotation(fwd));
            int mask0 = cam.cullingMask; var clear0 = cam.clearFlags;
            Debug.Log($"[Abertura] prólogo começa em {Time.realtimeSinceStartup:0.00}");
            if (!DebugSkipPrologue)
            {
                cam.cullingMask = 0; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                yield return prologue.Play();
            }
            cam.cullingMask = mask0; cam.clearFlags = clear0;
            rig = CinematicRig.Begin(aren, cam);

            // a flauta e o ambiente precisam existir antes do primeiro plano (no jogo o prólogo já cobre a espera)
            float waitSnd = 0f;
            while (snd != null && !(snd.Has("flute_a") && snd.Has("crickets_a") && snd.Has("bell_sympathy")) && waitSnd < 25f) { waitSnd += Time.unscaledDeltaTime; yield return null; }

            Debug.Log($"[Abertura] S0 em {Time.realtimeSinceStartup:0.0} s (esperou o som {waitSnd:0.0} s)");
            // S0 — O SINO: corte casado com o fim do prólogo; a flauta começa; o sino canta junto, afinado
            black.color = Color.black;
            StartCoroutine(FadeBlack(0f, 1.3f));
            barTarget = 1f;
            AudioSource fluteSrc = null;
            if (snd != null)
            {
                snd.Begin();
                fluteSrc = snd.PlayAt("flute_a", head, 0.8f, 2.5f, 50f);
                StartCoroutine(At(0.6f, () => snd.PlayAt("bell_sympathy", BC, 0.3f, 2.5f, 40f)));
            }
            if (shrine != null) shrine.hum = 0.25f;
            handheld = 0.6f;
            // o sino sob o telhado, a lanterna acesa ao lado; ao fundo a muralha com as tochas e a vila (de baixo, do sudoeste)
            yield return Shot(A + new Vector3(1.6f, 1.3f, -1.6f), BC + new Vector3(0f, -0.19f, -0.1f), 36f,
                              A + new Vector3(1.78f, 1.36f, -1.22f), BC + new Vector3(0f, -0.17f, -0.1f), 32f, 3.4f, Ease.Out);

            // S1 — ESTABELECIMENTO: grua desce do alto (estrada, o Aren tocando, muralha, vila, serras, lua)
            StartCoroutine(At(3.4f, () => snd?.PlayAt("tower_tuned", towerTop, 0.9f, 60f, 420f)));
            handheld = 0.35f;
            yield return Shot(A + new Vector3(-17f, 23f, -36f), A + new Vector3(4f, 8f, 80f), 46f,
                              A + new Vector3(-3.6f, 2.15f, -6.2f), A + new Vector3(0.7f, 1.55f, 4f), 40f, 6.6f, Ease.Crane);

            // S2 — O BARDO: perfil pela frente-esquerda (a lanterna quente atrás dele, a lua recortando)
            handheld = 0.7f;
            yield return Shot(A + new Vector3(-2.1f, eyeH - 0.12f, 1.75f), head + new Vector3(0.18f, -0.1f, 0.1f), 32f,
                              A + new Vector3(-1.65f, eyeH - 0.08f, 2.15f), head + new Vector3(0.22f, -0.08f, 0.05f), 29f, 4.4f, Ease.InOut);

            // S3 — ALGO ESTÁ ERRADO
            // (a) uma nota da flauta entorta; ele para e escuta (close no rosto, empurrando devagar)
            if (snd != null)
            {
                StartCoroutine(FadeSource(fluteSrc, 0.35f));
                snd.PlayAt("flute_wrong", head, 0.85f, 2.5f, 50f);
                snd.Corruption = 0.25f;
            }
            StartCoroutine(Animate(1.7f, 1.6f, k => { if (perf != null) perf.cinematic = Mathf.Lerp(1f, 0.5f, Mathf.SmoothStep(0f, 1f, k)); }));
            StartCoroutine(At(2.0f, () => { look.speed = 1.3f; look.LookAt(BC, 0.75f); look.listenTilt = 9f; village.vibration = 0.35f; }));
            StartCoroutine(At(2.1f, () => StartCoroutine(TurnAren(aren, 22f, 1.6f))));
            yield return Shot(A + new Vector3(0.72f, eyeH - 0.1f, 1.25f), head + new Vector3(0.02f, -0.02f, 0f), 27f,
                              A + new Vector3(0.6f, eyeH - 0.09f, 1.02f), head + new Vector3(0.02f, -0.02f, 0f), 23f, 3.5f, Ease.Linear);
            // (b) o sino ao lado começa a cantar SOZINHO, desafinado: tremor, chama batendo, poeira vibrando
            if (snd != null) { snd.PlayAt("bell_wobble", BC, 0.8f, 2.5f, 60f); snd.Corruption = 0.4f; }
            if (shrine != null) shrine.hum = 1f;
            village.vibration = 1f;
            handheld = 0.45f;
            // sobre o ombro dele: o Aren olha o sino e a lanterna (o sino canta sozinho, errado)
            yield return Shot(A + new Vector3(-1.4f, 1.2f, -0.6f), BC + new Vector3(0f, -0.1f, 0f), 34f,
                              A + new Vector3(-1.18f, 1.24f, -0.5f), BC + new Vector3(0f, -0.08f, 0f), 31f, 2.6f, Ease.Linear);
            // (c) a torre responde errada; ele procura de onde vem (plano por trás, a vila ao fundo)
            StartCoroutine(At(0.15f, () => snd?.PlayAt("tower_detuned", towerTop, 1f, 60f, 420f)));
            StartCoroutine(At(0.35f, () => { look.speed = 2.0f; look.LookAt(towerTop, 1f); look.listenTilt = 3f; if (snd != null) snd.Corruption = 0.55f; }));
            StartCoroutine(At(1.5f, () => { snd?.PlayAt("tower_freeze", towerTop, 0.8f, 60f, 420f); look.LookAt(A - right * 7f + fwd * 6f + up * 2.5f, 1f); }));
            StartCoroutine(At(2.4f, () => look.LookAt(A + right * 6f + fwd * 8f + up * 3.5f, 1f)));
            StartCoroutine(Animate(1.4f, 1.6f, k => { if (perf != null) perf.cinematic = Mathf.Lerp(0.5f, 0f, Mathf.SmoothStep(0f, 1f, k)); }));
            handheld = 0.8f;
            yield return Shot(A + new Vector3(-1.7f, 1.85f, -3.3f), A + new Vector3(0.6f, 1.6f, 12f), 40f,
                              A + new Vector3(-1.35f, 1.8f, -2.8f), A + new Vector3(0.6f, 1.7f, 12f), 38f, 3.4f, Ease.InOut);

            // S4 — A FENDA
            // (a) sobre o ombro direito: um ponto de luz MUITO longe, além das serras; a rachadura corre
            if (perf != null) perf.cinematic = 0f;
            look.speed = 1.1f; look.LookAt(fendaC, 1f); look.listenTilt = 0f;
            StartCoroutine(TurnAren(aren, 10f, 2.2f));
            StartCoroutine(At(0.35f, () =>
            {
                snd?.Play("fenda_pin", 0.65f, 0.2f);
                StartCoroutine(Animate(0f, 1.7f, k => { if (sky != null) sky.fendaOpen = 0.1f * k; }));
            }));
            StartCoroutine(At(2.0f, () =>
            {
                snd?.Play("fenda_crack", 0.75f, 0.2f);
                if (snd != null) snd.Corruption = 0.65f;
                StartCoroutine(Animate(0f, 2.4f, k => { if (sky != null) { sky.fendaOpen = Mathf.Lerp(0.1f, 0.5f, k * k); sky.fendaPulse = k; } }));
            }));
            handheld = 0.5f;
            if (rig != null) { rig.shaftLevel = 0.6f; }
            yield return Shot(head + new Vector3(0.42f, 0.14f, -0.95f), fendaC, 30f,
                              head + new Vector3(0.36f, 0.12f, -0.72f), fendaC, 25f, 4.2f, Ease.Linear);
            // (b) o rosto dele na luz violeta: o céu estilhaça (lampejo, onda), ele recua um pouco
            StartCoroutine(At(0.6f, () =>
            {
                snd?.Play("fenda_shatter", 1f, 0.15f);
                if (snd != null) snd.Corruption = 0.8f;
                sky?.Burst(1f); sky?.Wave();
                look.Flinch(0.35f);
                StartCoroutine(Animate(0f, 1.0f, k => { if (sky != null) sky.fendaOpen = Mathf.Lerp(0.5f, 0.88f, 1f - (1f - k) * (1f - k)); }));
            }));
            StartCoroutine(At(1.7f, () => snd?.SustainFenda(0.42f)));
            Vector3 faceCam = A + Fd(0f) * 1.3f + up * (eyeH - 0.16f) + Vector3.Cross(up, Fd(0f)) * -0.3f;
            handheld = 0.6f;
            yield return Shot(faceCam, head + up * 0.04f, 30f, faceCam + Fd(0f) * -0.12f + up * 0.02f, head + up * 0.04f, 28f, 3.0f, Ease.Linear);
            // (c) ESCALA: o plano aberto — Aren pequeno, a vila, o vale, as serras e a Fenda enorme lá longe
            StartCoroutine(Animate(0f, 3f, k => { if (sky != null) sky.fendaOpen = Mathf.Lerp(0.88f, 1f, k); }));
            StartCoroutine(At(1.2f, () => sky?.Wave()));
            handheld = 0.25f;
            yield return Shot(A + new Vector3(-24f, 30f, -52f), A + Fd(0.07f) * 900f, 50f,
                              A + new Vector3(-21f, 28f, -46f), A + Fd(0.075f) * 900f, 47f, 4.2f, Ease.InOut);

            // S5 — OS SETE: a Fenda inspira; sete notas saem, contáveis, em ritmo irregular
            SevenGlows seven = null;
            ImpactFX impact = ImpactFX.Prepare(IP);
            float contactAt = -1f;
            StartCoroutine(At(0.1f, () =>
            {
                snd?.Play("inhale", 0.85f, 0.2f);
                StartCoroutine(Animate(0f, 1.5f, k => { if (sky != null) sky.inhale = Mathf.SmoothStep(0f, 1f, k); }));
            }));
            StartCoroutine(At(1.65f, () =>
            {
                if (sky != null) sky.inhale = 0f;
                seven = SevenGlows.Launch(A);
                seven.onBirth = i =>
                {
                    sky?.Burst(1f);
                    if (snd == null) return;
                    snd.Play("birth" + i, 0.55f, snd.PanTo(fendaC));
                    var g = seven.glows.Find(x => x.index == i);
                    if (i != 0 && g != null) snd.PlayTracked("sig" + i, () => g.pos, () => g.alpha, 0.38f);
                };
                seven.onImpact = p => { contactAt = Time.time; impact.Fire(); };
            }));
            handheld = 0.3f;
            if (rig != null) rig.shaftLevel = 1f;
            Vector3 teleCam = A + new Vector3(-1.5f, 1.75f, -0.7f);   // do lado oeste (o poste do sino fica fora do quadro)
            yield return Shot(teleCam, A + Fd(0.2f) * 3000f, 24f, teleCam, A + Fd(0.24f) * 3000f, 26f, 4.0f, Ease.Linear);
            // (b) sobre o ombro, de baixo: a silhueta dele e os rastros se abrindo no céu
            StartCoroutine(At(0.2f, () => { if (seven != null && seven.Fallen != null) look.LookAt(seven.Fallen.pos, 1f); }));
            StartCoroutine(At(1.6f, () => { if (snd != null && seven != null) snd.PlayTracked("sig_pass", () => seven.Fallen.pos, () => 1f, 0.85f); }));
            handheld = 0.6f;
            yield return Shot(A + new Vector3(-0.75f, 1.2f, -1.25f), A + Fd(0.5f) * 100f, 52f,
                              A + new Vector3(-0.85f, 1.15f, -1.4f), A + Fd(0.6f) * 100f, 54f, 3.2f, Ease.InOut);

            // S6 — UM DELES CAI LONGE: acompanha o dourado por cima da vila...
            StartCoroutine(TurnAren(aren, Mathf.Lerp(0f, impactYaw, 0.55f), 3.5f));
            handheld = 0.9f;
            var g0 = seven != null ? seven.Fallen : null;
            Vector3 lookSm = g0 != null ? g0.pos : IP;
            float t6 = 0f;
            Vector3 cam6 = head + new Vector3(-0.45f, -0.12f, -0.8f);
            while (g0 != null && !g0.gone && t6 < 9f)
            {
                t6 += Time.deltaTime;
                lookSm = Vector3.Lerp(lookSm, g0.pos, 1f - Mathf.Exp(-Time.deltaTime * 3f));
                look.LookAt(lookSm, 1f);
                fov = Mathf.Lerp(50f, 40f, Mathf.SmoothStep(0f, 1f, t6 / 4f));
                ApplyCam(cam6 + eSide * (-0.25f * Mathf.SmoothStep(0f, 1f, t6 / 4f)), lookSm);
                // quando ele começa a descer de verdade: a vila cala (silêncio relativo)
                if (g0.u > 0.56f && snd != null && snd.duck > 0.2f) snd.duck = 0.12f;
                if (g0.u > 0.64f) break;
                yield return null;
            }
            // ...e a grua sobe atrás dele e revela o vale do leste enquanto a nota desce no planalto
            if (snd != null) snd.duck = 0.12f;
            look.LookAt(IP + up * 80f, 1f);
            StartCoroutine(TurnAren(aren, impactYaw - 18f, 2.5f));
            Vector3 c0 = cam.transform.position;
            Vector3 c1 = A - eDir * 9f - eSide * 3.4f + up * 4.4f;
            // composição: o Aren (com o sino) no terço de baixo, o impacto no terço de cima
            Vector3 lkEnd = c1 + (Vector3.Normalize(head - c1) * 0.42f + Vector3.Normalize(IP + up * 40f - c1) * 0.58f) * 100f;
            Vector3 l0 = lookSm;
            float tc = 0f, craneDur = 3.6f;
            handheld = 0.3f;
            while (contactAt < 0f || Time.time - contactAt < 2.7f)
            {
                tc += Time.deltaTime;
                float k = Mathf.Clamp01(tc / craneDur);
                float kk = k * k * k * (k * (k * 6f - 15f) + 10f);
                Vector3 target = g0 != null && !g0.gone ? g0.pos : IP + up * 60f;
                l0 = Vector3.Lerp(l0, target, 1f - Mathf.Exp(-Time.deltaTime * 2.5f));
                Vector3 lk = Vector3.Lerp(l0, lkEnd, kk);
                fov = Mathf.Lerp(42f, 46f, kk);
                // depois da grua: um empurrão lento para frente (deriva)
                Vector3 drift = eDir * Mathf.Max(0f, tc - craneDur) * 0.8f;
                ApplyCam(Vector3.Lerp(c0, c1, kk) + drift, lk);
                if (tc > 30f) break;
                yield return null;
            }

            // S7 — O IMPACTO (o clarão já foi): plano no chão, a onda de pressão chegando
            bool pressure = false;
            if (impact != null)
                impact.onPressureWave = () =>
                {
                    pressure = true;
                    StartCoroutine(ImpactKick(eDir));
                    if (snd != null)
                    {
                        snd.Play("impact2", 1f, snd.PanTo(IP));
                        snd.Play("gust", 0.75f, snd.PanTo(IP) * 0.7f);
                        snd.PlayAt("rattle", A + eDir * 4f + up * 2f, 0.7f, 3f, 40f);
                        snd.PlayAt("bell_sympathy", BC, 0.85f, 2.5f, 60f);
                        snd.PlayAt("clinks", LP, 0.6f, 2f, 30f);
                        snd.PlayAt("bell_sympathy", towerTop, 0.6f, 60f, 420f, 2f);
                        StartCoroutine(At(0.4f, () => snd.Play("metal", 0.4f, 0.3f)));
                        StartCoroutine(At(2.5f, () => snd.duck = 0.55f));
                    }
                    village.Gust(IP, A);
                    look.Flinch(1f);
                };
            StartCoroutine(At(0.35f, () => snd?.Play("ring", 0.5f, 0f)));
            look.LookAt(IP + up * 120f, 1f);
            handheld = 0.7f;
            float pressAt = impact != null ? impact.soundDelay + ImpactFX.CoreTime : 4.5f;
            float since = contactAt > 0f ? Time.time - contactAt : 2.7f;
            float holdGround = Mathf.Max(1.2f, pressAt - since) + 2.0f;
            yield return Shot(A - eDir * 2.5f - eSide * 0.75f + up * 1.32f, A + eDir * 40f + up * 4.5f, 34f,
                              A - eDir * 2.15f - eSide * 0.65f + up * 1.3f, A + eDir * 40f + up * 4.2f, 31f, holdGround, Ease.Linear);
            // depois: o sino ainda tremendo, errado; a lanterna balançando; a poeira assentando
            village.vibration = 0.7f;
            handheld = 0.5f;
            // o sino e a lanterna balançando, com o céu aceso pelo impacto e o pilar de luz atrás
            yield return Shot(A + new Vector3(0.6f, 1.4f, -1.7f), BC + eDir * 0.6f + up * -0.1f, 36f,
                              A + new Vector3(0.8f, 1.45f, -1.45f), BC + eDir * 0.6f + up * -0.08f, 33f, 2.4f, Ease.Linear);
            // o rosto dele na luz quente distante, respirando; ele não sabe o que foi aquilo
            look.LookAt(IP + up * 100f, 1f); look.listenTilt = -2f;
            Vector3 face7 = A + eDir * 1.1f - eSide * 0.55f + up * (eyeH - 0.36f);   // de baixo: o fundo é céu, não o morro
            handheld = 0.6f;
            yield return Shot(face7, head + up * 0.03f, 29f, face7 - eDir * 0.15f, head + up * 0.03f, 26f, 3.2f, Ease.Linear);

            // S8 — TÍTULO: ele se volta para a vila; a câmera sobe atrás dele e pousa na câmera de jogo
            look.speed = 1.0f;
            look.LookAt(A + fwd * 30f + up * 2f, 1f); look.listenTilt = 0f;
            StartCoroutine(TurnAren(aren, 0f, 2.6f));
            StartCoroutine(Title("ECOS DE ACORDIA", "A Ruptura do Contracanto", 0.9f, 3.4f));
            StartCoroutine(At(3.2f, () => look.Release()));
            village.vibration = 0.3f;
            if (snd != null) snd.duck = 0.8f;
            if (rig != null) rig.shaftLevel = 0.5f;
            handheld = 0.35f;
            yield return Shot(A + right * 0.7f + up * 1.35f - fwd * 2.7f, A + up * 2f + fwd * 60f, 42f,
                              A + right * 0.4f + up * 4.8f - fwd * 7.8f, A + Fd(0.11f) * 150f, 44f, 4.4f, Ease.InOut);
            // transição contínua: interpola até a pose atual da câmera de jogo (o Cinemachine já está atrás dele)
            barTarget = 0f;
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            float tb = 0f, dur = 2.0f;
            while (tb < dur)
            {
                tb += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, tb / dur);
                Vector3 p1 = mainCam != null ? mainCam.transform.position : A - fwd * 4.6f + up * 2.1f;
                Quaternion r1 = mainCam != null ? mainCam.transform.rotation : Quaternion.LookRotation(fwd);
                float f1 = mainCam != null ? mainCam.fieldOfView : 50f;
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                cam.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
            Playing = false;
        }

        // ------------------------------------------------------------ peças

        enum Ease { InOut, Out, Linear, Crane }

        static float Eased(float k, Ease e)
        {
            switch (e)
            {
                case Ease.Out: return 1f - (1f - k) * (1f - k);
                case Ease.Linear: return Mathf.Lerp(k, Mathf.SmoothStep(0f, 1f, k), 0.25f);   // quase linear, sem tranco nas pontas
                case Ease.Crane: return k * k * k * (k * (k * 6f - 15f) + 10f);
                default: return Mathf.SmoothStep(0f, 1f, k);
            }
        }

        IEnumerator At(float delay, System.Action a)
        {
            yield return new WaitForSeconds(delay);
            a?.Invoke();
        }

        IEnumerator Animate(float delay, float dur, System.Action<float> f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; f(Mathf.Clamp01(t / dur)); yield return null; }
            f(1f);
        }

        IEnumerator FadeSource(AudioSource s, float dur)
        {
            if (s == null) yield break;
            float v0 = s.volume, t = 0f;
            while (t < dur && s != null) { t += Time.deltaTime; s.volume = v0 * (1f - t / dur); yield return null; }
            if (s != null) s.Stop();
        }

        static void SetYaw(Transform aren, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            var rb = aren.GetComponent<Rigidbody>();
            if (rb != null) rb.rotation = q;
            aren.rotation = q;
        }

        /// <summary>Vira o corpo devagar (o olhar procedural cuida do resto).</summary>
        IEnumerator TurnAren(Transform aren, float yaw, float dur)
        {
            float y0 = aren.eulerAngles.y, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetYaw(aren, Mathf.LerpAngle(y0, yaw, Mathf.SmoothStep(0f, 1f, t / dur)));
                yield return null;
            }
            SetYaw(aren, yaw);
        }

        /// <summary>
        /// Reação de câmera ao impacto (massa e escala, não tremor aleatório): micro-antecipação
        /// (a câmera "prende o ar" e chega 1 cm para frente) → golpe (empurrada para longe do impacto,
        /// sobe e rola) → mola amortecida devolve com um leve rebote → um ronco fino que morre.
        /// </summary>
        IEnumerator ImpactKick(Vector3 fromDir)
        {
            Vector3 toward = cam.transform.InverseTransformDirection(fromDir);   // direção do impacto, no espaço da câmera
            kickPV += toward * 0.19f;                                            // antecipação: ~1 cm na direção dele
            yield return new WaitForSeconds(0.07f);
            kickPV += -toward * 1.25f + Vector3.down * 0.4f;                     // golpe: ~8 cm para longe, desce
            kickRV += new Vector3(-25f, toward.x * 8f, -toward.x * 18f - 6f);    // sobe ~1,6° e rola ~1,4°
            shake = Mathf.Max(shake, 0.5f);
        }

        /// <summary>Posiciona a câmera: "câmera na mão" sutil (respiração), mola da reação e ronco.</summary>
        void ApplyCam(Vector3 p, Vector3 l)
        {
            float tt = Time.time, dt = Time.deltaTime;
            float hh = handheld;
            p += new Vector3(Mathf.PerlinNoise(tt * 0.35f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 0.3f) - 0.5f, 0f) * 0.035f * hh;
            Quaternion r = Quaternion.LookRotation(l - p);
            r *= Quaternion.Euler((Mathf.PerlinNoise(tt * 0.25f, 5f) - 0.5f) * 0.6f * hh, (Mathf.PerlinNoise(9f, tt * 0.22f) - 0.5f) * 0.6f * hh, (Mathf.PerlinNoise(tt * 0.18f, 3f) - 0.5f) * 0.4f * hh);
            // mola (ω ≈ 11 rad/s, amortecimento ~0,4: volta com um pequeno rebote)
            const float w = 11f, z = 0.4f;
            kickPV += (-kickP * w * w - kickPV * 2f * z * w) * dt; kickP += kickPV * dt;
            kickRV += (-kickR * w * w - kickRV * 2f * z * w) * dt; kickR += kickRV * dt;
            p += r * kickP;
            r *= Quaternion.Euler(kickR);
            if (shake > 0.001f)
            {
                float s = shake * shake;
                p += new Vector3(Mathf.PerlinNoise(tt * 17f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 17f) - 0.5f, 0f) * 0.12f * s;
                r *= Quaternion.Euler((Mathf.PerlinNoise(tt * 14f, 5f) - 0.5f) * 1.6f * s, 0f, (Mathf.PerlinNoise(7f, tt * 14f) - 0.5f) * 1.8f * s);
                shake = Mathf.MoveTowards(shake, 0f, dt * 0.45f);
            }
            cam.transform.SetPositionAndRotation(p, r);
            cam.fieldOfView = fov;
        }

        IEnumerator Shot(Vector3 p0, Vector3 look0, float fov0, Vector3 p1, Vector3 look1, float fov1, float dur, Ease ease = Ease.InOut)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Eased(Mathf.Clamp01(t / dur), ease);
                fov = Mathf.Lerp(fov0, fov1, k);
                ApplyCam(Vector3.Lerp(p0, p1, k), Vector3.Lerp(look0, look1, k));
                yield return null;
            }
        }

        IEnumerator Title(string t, string sub, float delay, float dur)
        {
            yield return new WaitForSeconds(delay);
            title.text = UIKit.Spaced(t, 3); titleSub.text = sub; titleTarget = 1f;
            yield return new WaitForSeconds(dur);
            titleTarget = 0f;
        }

        IEnumerator FadeBlack(float target, float dur)
        {
            float start = black.color.a, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;   // tempo de jogo: casa com a gravação quadro a quadro
                var c = black.color; c.a = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur)); black.color = c;
                yield return null;
            }
            var cc = black.color; cc.a = target; black.color = cc;
        }

        void ApplyBars()
        {
            float h = 130f * barK;
            barTop.sizeDelta = new Vector2(0, h);
            barBottom.sizeDelta = new Vector2(0, h);
        }

        void Update()
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime;
            subAlpha = Mathf.MoveTowards(subAlpha, subTarget, dt * 1.6f);
            titleAlpha = Mathf.MoveTowards(titleAlpha, titleTarget, dt * 0.9f);
            barK = Mathf.MoveTowards(barK, barTarget, dt * 0.8f);
            var c = subtitle.color; c.a = subAlpha; subtitle.color = c;
            var tc = title.color; tc.a = titleAlpha; title.color = tc;
            var sc = titleSub.color; sc.a = titleAlpha; titleSub.color = sc;
            if (flashA > 0f) { flashA = Mathf.MoveTowards(flashA, 0f, dt * 1.1f); UIKit.SetAlpha(flash, flashA * flashA); }
            ApplyBars();
        }
    }
}

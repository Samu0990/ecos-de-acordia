using System.Collections;
using Aren.UI;
using Aren.World.Night;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// Abertura do jogo. Prólogo em motion graphics (a nota → os doze sinos, em harmonia) e a
    /// cena de abertura em Campanula, à noite, seguindo o storyboard do autor:
    ///   01 Campanula (calma, sinos afinados) → 02 algo está errado (o som desafina; Aren percebe
    ///   PELO OUVIDO, para e escuta) → 03 a Fenda (a câmera segue o olhar dele até o horizonte,
    ///   muito longe) → 04 os sete brilhos se separam → 05 um deles passa sobre a vila e cai MUITO
    ///   longe, atrás da serra do leste → 06 o mistério (clarão; segundos depois a ressonância do
    ///   impacto atravessa o vale e o sino ao lado dele vibra sozinho) → 07 título e transição
    ///   contínua para o gameplay (a câmera pousa exatamente na câmera de jogo).
    /// Regra de som: a música do mundo DESAFINA — nunca fica muda. Espaço / Esc / Enter / clique pulam.
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        public bool Playing { get; private set; }
        public bool Skipped { get; private set; }

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
        public IEnumerator Play(Transform aren, BellRinger bells, GameObject deerPrefab, Combat.ArenFlute flute)
        {
            Playing = true; Skipped = false; skipRequested = false;
            canvas.gameObject.SetActive(true);
            black.color = Color.black;
            barK = 0f; barTarget = 0f; ApplyBars();   // o prólogo é em tela cheia; as faixas entram na parte 3D
            cam.gameObject.SetActive(true);
            cam.farClipPlane = 300f;
            mainCam = Camera.main != null && Camera.main != cam ? Camera.main : null;
            mainListener = mainCam != null ? mainCam.GetComponent<AudioListener>() : null;
            if (mainListener != null) mainListener.enabled = false;
            if (mainCam != null) mainCam.enabled = false;   // não desenha a cena duas vezes (o Cinemachine continua posicionando)
            camListener.enabled = true;
            ArenAudio.SetIntensity(0f);
            bool bloom = RenderScaler.PostBloom;
            if (GameSettings.Quality >= 1) RenderScaler.PostBloom = true;   // brilho dos sete e da Fenda

            var routine = StartCoroutine(Sequence(aren));
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
            // estado final mesmo se pulou: Fenda aberta, som doente
            if (RuptureSky.Instance != null) RuptureSky.Instance.fendaOpen = 1f;
            if (OpeningSound.Instance != null) OpeningSound.Instance.Corruption = Mathf.Max(OpeningSound.Instance.Corruption, 0.6f);
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

        IEnumerator Sequence(Transform aren)
        {
            var snd = OpeningSound.Instance;
            var sky = RuptureSky.Instance;
            var shrine = BellShrine.Instance;
            var look = aren.GetComponent<CinematicLook>() ?? aren.gameObject.AddComponent<CinematicLook>();
            Vector3 A = aren.position;
            Vector3 fwd = aren.forward, right = aren.right;
            Vector3 head = A + Vector3.up * 1.58f;
            Vector3 BC = shrine != null ? shrine.BellCenter : A + right * 2.6f + Vector3.up * 2.2f;
            var towerGo = GameObject.Find("BellTower");
            Vector3 towerTop = towerGo != null ? towerGo.transform.position + Vector3.up * 21f : new Vector3(0, 21, 36);
            Vector3 fendaPt = A + NightSetup.Dir(NightSetup.FendaAz, 0.16f) * 200f;
            Vector3 impactPt = A + NightSetup.Dir(NightSetup.ImpactAz, 0.06f) * 200f;
            if (sky != null) sky.fendaOpen = 0f;

            // 0 — prólogo: a nota que procura outra nota, e os doze sinos (ainda em harmonia)
            cam.transform.SetPositionAndRotation(BC + new Vector3(-0.9f, -0.55f, -1.1f), Quaternion.LookRotation(Vector3.forward));
            yield return prologue.Play();

            // 01 — CAMPANULA: noite normal; o sino da estrada toca afinado e a torre responde
            black.color = new Color(0, 0, 0, 1);
            StartCoroutine(FadeBlack(0f, 1.0f));
            barTarget = 1f;
            if (snd != null) { snd.Corruption = 0f; snd.Begin(); }
            StartCoroutine(At(0.45f, () => { shrine?.Strike(1f); snd?.PlayAt("bell", BC, 0.9f, 2.5f, 60f); }));
            StartCoroutine(At(2.9f, () => snd?.PlayAt("tower_tuned", towerTop, 0.95f, 60f, 400f)));
            StartCoroutine(At(4.3f, () => snd?.Play("owl", 0.35f, -0.6f)));
            yield return Shot(BC + new Vector3(-0.95f, -0.55f, -1.15f), BC + new Vector3(0, 0.05f, 0), 34f,
                              BC + new Vector3(-0.78f, -0.45f, -0.88f), BC + new Vector3(0, 0.05f, 0), 30f, 2.6f);
            yield return Shot(BC + new Vector3(-1.4f, -0.3f, -2.0f), BC, 36f,
                              A + right * 1.7f + Vector3.up * 1.75f - fwd * 3.0f, A + Vector3.up * 1.45f + fwd * 8f, 42f, 3.0f);

            // 02 — ALGO ESTÁ ERRADO: o sino toca de novo, mas a cauda oscila; Aren para e escuta
            StartCoroutine(At(0.3f, () => { shrine?.Strike(0.9f); snd?.PlayAt("bell_wobble", BC, 0.95f, 2.5f, 60f); }));
            StartCoroutine(At(0.8f, () => { look.speed = 1.6f; look.LookAt(BC, 1f); look.listenTilt = 9f; if (snd != null) snd.Corruption = 0.28f; }));
            yield return Shot(head + right * 0.95f + fwd * 0.75f + Vector3.up * 0.04f, head, 30f,
                              head + right * 0.85f + fwd * 0.62f + Vector3.up * 0.02f, head, 28f, 2.0f);
            StartCoroutine(At(0.0f, () => snd?.PlayAt("tower_detuned", towerTop, 1f, 60f, 400f)));
            StartCoroutine(At(0.3f, () => { look.LookAt(towerTop, 1f); look.listenTilt = 4f; if (snd != null) snd.Corruption = 0.45f; }));
            StartCoroutine(At(1.3f, () => snd?.PlayAt("tower_freeze", towerTop, 0.8f, 60f, 400f)));
            StartCoroutine(At(1.7f, () => { look.speed = 2.6f; look.LookAt(A - right * 6f + fwd * 4f + Vector3.up * 2f, 1f); look.listenTilt = -5f; }));
            StartCoroutine(At(2.4f, () => look.LookAt(A + right * 5f + fwd * 6f + Vector3.up * 3f, 1f)));
            yield return Shot(A - right * 1.5f + Vector3.up * 1.85f - fwd * 2.6f, A + right * 0.4f + Vector3.up * 1.5f + fwd * 10f, 40f,
                              A - right * 1.2f + Vector3.up * 1.8f - fwd * 2.2f, A + right * 0.4f + Vector3.up * 1.6f + fwd * 10f, 38f, 3.0f);

            // 03 — A FENDA: ele procura a origem; a câmera segue o olhar até o horizonte
            StartCoroutine(At(0.15f, () => { look.speed = 1.4f; look.LookAt(fendaPt, 1f); look.listenTilt = 0f; if (snd != null) snd.Corruption = 0.6f; }));
            StartCoroutine(At(0.6f, () => snd?.OpenFenda()));
            StartCoroutine(Animate(1.0f, 4.4f, k => { if (sky != null) { sky.fendaOpen = Mathf.SmoothStep(0f, 1f, k); sky.fendaPulse = k; } }));
            StartCoroutine(At(4.0f, () => { if (snd != null) snd.Corruption = 0.8f; }));
            yield return Shot(head + fwd * 1.25f + right * 0.35f + Vector3.up * 0.03f, head, 28f,
                              head + fwd * 1.05f + right * 0.3f + Vector3.up * 0.05f, head, 27f, 1.7f);
            yield return Shot(head + right * 0.45f - fwd * 0.35f + Vector3.up * 0.15f, fendaPt, 40f,
                              A - right * 2.6f + Vector3.up * 3.0f - fwd * 7.0f, fendaPt + Vector3.up * 6f, 30f, 4.1f);

            // 04 — OS SETE: saem da Fenda e se separam (cada um com uma ressonância própria, no espaço)
            SevenGlows seven = null;
            StartCoroutine(At(0.2f, () =>
            {
                seven = SevenGlows.Launch(A);
                if (snd != null)
                    for (int i = 0; i < seven.glows.Count; i++)
                    {
                        var g = seven.glows[i];
                        if (i == 0) continue;   // o dourado tem a passagem própria
                        snd.PlayTracked("sig" + i, () => g.pos, () => g.alpha, 0.45f);
                    }
                if (snd != null) snd.PlayTracked("sig0", () => seven.Fallen.pos, () => seven.Fallen.alpha, 0.4f);
            }));
            StartCoroutine(At(1.2f, () => { if (seven != null) look.LookAt(seven.Fallen.pos, 1f); }));
            yield return Shot(A + right * 1.0f + Vector3.up * 1.15f - fwd * 4.2f, A + NightSetup.Dir(NightSetup.FendaAz, 0.2f) * 200f, 36f,
                              A + right * 1.3f + Vector3.up * 1.2f - fwd * 4.6f, A + NightSetup.Dir(NightSetup.FendaAz, 0.24f) * 200f, 36f, 4.0f);

            // 05 — UM DELES CAI LONGE: o dourado passa por cima da vila; a câmera vai atrás dele
            float fellAt = -1f;
            if (seven != null) seven.onFallBehindRidge = () => fellAt = Time.time;
            if (snd != null && seven != null) snd.PlayTracked("sig_pass", () => seven.Fallen.pos, () => 1f, 0.9f);
            Vector3 camPos5 = A + right * 0.8f + Vector3.up * 1.5f - fwd * 3.2f;
            Vector3 lookSm = seven != null ? seven.Fallen.pos : impactPt;
            float t5 = 0f;
            while (t5 < 5.2f)
            {
                t5 += Time.deltaTime;
                Vector3 tgt = seven != null && !seven.Fallen.gone ? seven.Fallen.pos : impactPt + Vector3.up * 4f;
                lookSm = Vector3.Lerp(lookSm, tgt, 1f - Mathf.Exp(-Time.deltaTime * 3.5f));
                look.LookAt(lookSm, 1f);
                fov = Mathf.Lerp(46f, 34f, Mathf.SmoothStep(0f, 1f, t5 / 5.2f));
                ApplyCam(Vector3.Lerp(camPos5, camPos5 - right * 0.6f + Vector3.up * 0.2f, t5 / 5.2f), lookSm);
                yield return null;
            }

            // 06 — O MISTÉRIO: clarão atrás da serra; a ressonância chega depois (distância real)
            look.LookAt(impactPt, 1f);
            StartCoroutine(At(0.35f, () => sky?.Flash()));
            float travel = 3.3f;   // ~1,1 km a 340 m/s
            StartCoroutine(At(0.35f + travel, () =>
            {
                snd?.Play("impact", 1f, snd != null ? snd.PanTo(impactPt) : 0.6f);
                shake = 0.35f;
            }));
            StartCoroutine(At(0.35f + travel + 0.25f, () => { shrine?.Resonate(1f); snd?.PlayAt("bell_sympathy", BC, 0.9f, 2.5f, 60f); snd?.PlayAt("clinks", BC + right * 0.4f, 0.6f, 2f, 30f); }));
            StartCoroutine(At(0.35f + travel + 0.5f, () => { snd?.Play("metal", 0.45f, 0.3f); snd?.PlayAt("bell_sympathy", towerTop, 0.6f, 60f, 400f, 2f); }));
            // por trás do ombro esquerdo dele, olhando para o leste: Aren em primeiro plano, o sino e a serra
            Vector3 east = NightSetup.Dir(NightSetup.ImpactAz, 0f);
            Vector3 side = Vector3.Cross(Vector3.up, east);   // à direita de quem olha para o leste
            yield return Shot(A - east * 2.3f - side * 0.55f + Vector3.up * 1.7f, impactPt, 36f,
                              A - east * 2.0f - side * 0.5f + Vector3.up * 1.68f, impactPt, 35f, 3.9f);
            yield return Shot(A - east * 2.0f - side * 0.5f + Vector3.up * 1.68f, impactPt, 35f,
                              head - east * 0.6f + side * 0.75f + Vector3.up * 0.05f, Vector3.Lerp(head, impactPt, 0.02f), 30f, 2.1f);

            // 07 — TÍTULO: ele se volta para a vila; a câmera sobe atrás dele e pousa na câmera de jogo
            look.speed = 1.2f;
            look.LookAt(A + fwd * 30f + Vector3.up * 2f, 1f);
            StartCoroutine(Title("ECOS DE ACORDIA", "A Ruptura do Contracanto", 0.7f, 3.2f));
            StartCoroutine(At(2.9f, () => look.Release()));
            yield return Shot(A + right * 0.6f + Vector3.up * 1.3f - fwd * 2.8f, A + Vector3.up * 2f + fwd * 60f, 42f,
                              A + right * 0.4f + Vector3.up * 4.6f - fwd * 7.5f, A + NightSetup.Dir(NightSetup.FendaAz, 0.12f) * 150f, 44f, 4.0f);
            // transição contínua: interpola até a pose atual da câmera de jogo (o Cinemachine já está atrás dele)
            barTarget = 0f;
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            float tb = 0f, dur = 1.8f;
            while (tb < dur)
            {
                tb += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, tb / dur);
                Vector3 p1 = mainCam != null ? mainCam.transform.position : A - fwd * 4.6f + Vector3.up * 2.1f;
                Quaternion r1 = mainCam != null ? mainCam.transform.rotation : Quaternion.LookRotation(fwd);
                float f1 = mainCam != null ? mainCam.fieldOfView : 50f;
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                cam.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
            Playing = false;
        }

        // ------------------------------------------------------------ peças

        IEnumerator At(float delay, System.Action a)
        {
            yield return new WaitForSeconds(delay);
            a?.Invoke();
        }

        IEnumerator Animate(float delay, float dur, System.Action<float> f)
        {
            yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; f(Mathf.Clamp01(t / dur)); yield return null; }
            f(1f);
        }

        /// <summary>Posiciona a câmera com "câmera na mão" sutil (respiração) e tremor quando houver.</summary>
        void ApplyCam(Vector3 p, Vector3 l)
        {
            float tt = Time.time;
            p += new Vector3(Mathf.PerlinNoise(tt * 0.35f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 0.3f) - 0.5f, 0f) * 0.03f;
            Quaternion r = Quaternion.LookRotation(l - p);
            r *= Quaternion.Euler((Mathf.PerlinNoise(tt * 0.25f, 5f) - 0.5f) * 0.5f, (Mathf.PerlinNoise(9f, tt * 0.22f) - 0.5f) * 0.5f, 0f);
            if (shake > 0.001f)
            {
                float s = shake * shake;
                p += new Vector3(Mathf.PerlinNoise(tt * 18f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 18f) - 0.5f, 0f) * 0.25f * s;
                r *= Quaternion.Euler((Mathf.PerlinNoise(tt * 15f, 5f) - 0.5f) * 2.5f * s, 0f, (Mathf.PerlinNoise(7f, tt * 15f) - 0.5f) * 3f * s);
                shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.5f);
            }
            cam.transform.SetPositionAndRotation(p, r);
            cam.fieldOfView = fov;
        }

        IEnumerator Shot(Vector3 p0, Vector3 look0, float fov0, Vector3 p1, Vector3 look1, float fov1, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
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

using System.Collections;
using Aren.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// Abertura do jogo segundo a Bíblia de Lore (Folio 06 — A Ruptura do Contracanto):
    /// Festa da Afinação ao pôr do sol em Campanula, Aren tocando por moedas, os doze sinos,
    /// a 13ª badalada que vem do céu, a Fenda negra e o primeiro possuído (um cervo).
    /// Planos de câmera interpolados, faixas de cinema, legendas e eventos (sinos, pulso da
    /// Fenda, tremor). Espaço / Esc / Enter / clique pulam.
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
        float barK, subAlpha, subTarget, titleAlpha, titleTarget;
        bool skipRequested;
        Vector3 shakeSeed;
        float shake;

        public static readonly string[] Quote =
        {
            "Antes da pedra, antes do mar e antes do primeiro nome,",
            "havia uma nota procurando outra nota para não ficar sozinha."
        };

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
            subtitle = UIKit.Label("Legenda", root, "", UIKit.Serif, 32, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 66), new Vector2(1500, 110));
            subtitle.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            title = UIKit.Label("Titulo", root, "", UIKit.Display, 88, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1600, 130));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            titleSub = UIKit.Label("Subtitulo", root, "", UIKit.Serif, 30, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 60));
            skipHint = UIKit.Label("Pular", root, "Espaço / Esc: pular", UIKit.Sans, 20, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-40, 30), new Vector2(400, 40));
            canvas.gameObject.SetActive(false);
        }

        public void Skip() { if (Playing) skipRequested = true; }

        /// <summary>Toca a abertura. 'aren' = transform do jogador já posicionado no início da estrada.</summary>
        public IEnumerator Play(Transform aren, BellRinger bells, GameObject deerPrefab, Combat.ArenFlute flute)
        {
            Playing = true; Skipped = false; skipRequested = false;
            canvas.gameObject.SetActive(true);
            black.color = Color.black;
            barK = 1f; ApplyBars();
            cam.gameObject.SetActive(true);
            mainCam = Camera.main != null && Camera.main != cam ? Camera.main : null;
            mainListener = mainCam != null ? mainCam.GetComponent<AudioListener>() : null;
            if (mainListener != null) mainListener.enabled = false;
            if (mainCam != null) mainCam.enabled = false;   // não desenha a cena duas vezes
            camListener.enabled = true;
            ArenAudio.SetIntensity(0f);

            GameObject deer = null;
            var routine = StartCoroutine(Sequence(aren, bells, deerPrefab, flute, d => deer = d));
            while (Playing && !skipRequested)
            {
                if (SkipPressed()) skipRequested = true;
                yield return null;
            }
            if (skipRequested && Playing)
            {
                Skipped = true;
                StopCoroutine(routine);
                yield return FadeBlack(1f, 0.35f);
            }
            if (deer != null) Destroy(deer);
            // devolve a câmera ao jogo (o GameFlow faz o fade de volta)
            subTarget = 0f; titleTarget = 0f; subAlpha = 0f; titleAlpha = 0f;
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

        IEnumerator Sequence(Transform aren, BellRinger bells, GameObject deerPrefab, Combat.ArenFlute flute, System.Action<GameObject> deerOut)
        {
            Vector3 a = aren.position;
            Vector3 riftDir = new Vector3(0.78f, 0.36f, 0.5f).normalized;
            Vector3 riftPoint = new Vector3(15f, 0f, 12f) + riftDir * 370f;

            // 0 — frase de abertura no escuro
            cam.transform.SetPositionAndRotation(new Vector3(-60f, 38f, -150f), Quaternion.LookRotation(new Vector3(0, 8, -20) - new Vector3(-60f, 38f, -150f)));
            yield return Words(Quote[0] + "\n" + Quote[1], 6.2f, true);

            // 1 — voo sobre os campos até a vila
            StartCoroutine(FadeBlack(0f, 2.2f));
            StartCoroutine(Title("CAMPANULA", "a Vila dos Doze Sinos · o dia da Festa da Afinação", 1.2f, 4.6f));
            yield return Shot(new Vector3(-60f, 38f, -150f), new Vector3(0, 8, -20), new Vector3(-24f, 24f, -104f), new Vector3(0, 6, -10), 7.5f);

            // 2 — a praça e a torre
            Say("Ao pôr do sol, todas as cidades de Acordia tocam uma nota comum.");
            yield return Shot(new Vector3(22f, 7f, 6f), new Vector3(0, 13, 36), new Vector3(-16f, 9f, 8f), new Vector3(0, 15, 36), 6.5f);

            // 3 — Aren na estrada, com a flauta
            if (flute != null) { flute.KeepDrawn = true; flute.Draw(); }
            Say("Aren Vesper, o Bardo Sem-Nome, tocava por moedas e comida.");
            Vector3 head = a + Vector3.up * 1.5f;
            Vector3 chest = a + Vector3.up * 1.25f;
            Vector3 fwd = aren.forward, right = aren.right;
            // plano americano (da cintura para cima) chegando perto, depois um contraplano
            yield return Shot(chest + fwd * 2.6f + right * 1.2f + Vector3.up * 0.15f, chest, head + fwd * 1.55f + right * 0.6f, head - Vector3.up * 0.12f, 3.6f);
            Say("Mestre Odo dizia que ele sabia ouvir quando uma música queria mudar.");
            yield return Shot(head + fwd * 1.55f - right * 0.9f + Vector3.up * 0.05f, head - Vector3.up * 0.1f, head + fwd * 1.25f - right * 0.55f + Vector3.up * 0.12f, head - Vector3.up * 0.06f, 3.6f);

            // 4 — os doze sinos
            Say("Doze badaladas, como em todos os anos.");
            bool thirteenth = false;
            if (bells != null) bells.Toll(12, true, () => thirteenth = true);
            float bellStart = Time.time;
            StartCoroutine(Shot(new Vector3(0f, 3.2f, 13f), new Vector3(0, 19, 36), new Vector3(1.5f, 5f, 19f), new Vector3(0, 21, 36), 11.6f));
            while (!thirteenth && Time.time - bellStart < 13f) yield return null;

            // 5 — a 13ª vem do céu: a câmera procura a Fenda
            shake = 1f;
            ArenAudio.PlaySting(Sting.Mystery);
            Say("A décima terceira não saiu de nenhum sino. Veio do céu.");
            Vector3 p5 = new Vector3(4f, 3f, 8f);
            yield return Shot(p5, new Vector3(0, 21, 36), p5 + new Vector3(0.6f, 0.4f, 0.3f), p5 + (riftPoint - p5).normalized * 50f, 4.2f);
            Campanula.RiftPulse.Instance?.Burst(1f);
            Say("Acima do vale abriu-se uma fenda negra. Ela não brilhava: apagava.");
            Vector3 lp = p5 + (riftPoint - p5).normalized * 50f;
            yield return Shot(p5 + new Vector3(0.6f, 0.4f, 0.3f), lp, p5 + new Vector3(1.4f, 1.2f, 0.9f), lp + Vector3.up * 2f, 5f);

            // 6 — o primeiro possuído: um cervo
            GameObject deer = SpawnDeerVisual(deerPrefab, new Vector3(82f, 0f, 16f), -95f);
            deerOut(deer);
            Say("O primeiro possuído foi um cervo. Seus passos chegavam meio segundo antes das pernas.");
            Vector3 dpos = deer != null ? deer.transform.position : new Vector3(82f, 0f, 16f);
            yield return Shot(dpos + new Vector3(-8.5f, 1.0f, -3f), dpos + Vector3.up * 2.3f, dpos + new Vector3(-4.6f, 0.7f, -1.6f), dpos + Vector3.up * 2.7f, 6f);

            // 7 — de volta ao Aren: a câmera desce para trás dele e o jogo começa
            if (flute != null) { flute.KeepDrawn = false; flute.Holster(); }
            Say("Campanula ainda não sabia. Mas a música já tinha mudado.");
            Vector3 behind = a - fwd * 4.6f + Vector3.up * 2.1f + right * 0.3f;
            yield return Shot(a - fwd * 9f + Vector3.up * 7f, a + fwd * 6f + Vector3.up * 1.5f, behind, a + fwd * 8f + Vector3.up * 1.3f, 4.2f);
            subTarget = 0f;
            Playing = false;
        }

        GameObject SpawnDeerVisual(GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) return null;
            // instancia inativo para remover a IA antes do Awake (o cervo não entra no combate)
            var holder = new GameObject("CutsceneDeerHolder");
            holder.SetActive(false);
            var go = Instantiate(prefab, holder.transform);
            foreach (var e in go.GetComponents<Enemies.EnemyBase>()) DestroyImmediate(e);
            foreach (var ag in go.GetComponents<UnityEngine.AI.NavMeshAgent>()) DestroyImmediate(ag);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var lk in go.GetComponents<Combat.TorsoFacingLock>()) DestroyImmediate(lk);
            go.transform.SetParent(null, false);
            Destroy(holder);
            pos.y = Campanula.GroundHeight.At(pos.x, pos.z, 0f);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.SetActive(true);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                StartCoroutine(DeerAct(anim, go.transform));
            }
            return go;
        }

        IEnumerator DeerAct(Animator anim, Transform deer)
        {
            anim.SetFloat("MoveSpeed", 0f);
            anim.SetFloat("LocoSpeed", 1f);
            anim.SetFloat("StateSpeed", 1f);
            yield return new WaitForSeconds(1.6f);
            if (anim == null) yield break;
            anim.CrossFadeInFixedTime("Roar", 0.25f, 0);
            ArenAudio.Play(Sfx.DeerGrowl, deer.position + Vector3.up * 2f, 1f, 0.8f);
            ArenVFX.CorruptionBurst(deer.position + Vector3.up * 1.2f, 1.6f);
        }

        // ------------------------------------------------------------ peças

        IEnumerator Shot(Vector3 p0, Vector3 look0, Vector3 p1, Vector3 look1, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                Vector3 p = Vector3.Lerp(p0, p1, k);
                Vector3 l = Vector3.Lerp(look0, look1, k);
                Quaternion r = Quaternion.LookRotation(l - p);
                if (shake > 0.001f)
                {
                    float s = shake * shake;
                    p += new Vector3(Mathf.PerlinNoise(Time.time * 18f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, Time.time * 18f) - 0.5f, 0f) * 0.35f * s;
                    r *= Quaternion.Euler((Mathf.PerlinNoise(Time.time * 15f, 5f) - 0.5f) * 3f * s, 0f, (Mathf.PerlinNoise(7f, Time.time * 15f) - 0.5f) * 4f * s);
                    shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.7f);
                }
                cam.transform.SetPositionAndRotation(p, r);
                yield return null;
            }
        }

        IEnumerator Words(string text, float dur, bool onBlack)
        {
            subtitle.fontSize = onBlack ? 38 : 32;
            subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = onBlack ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            subtitle.rectTransform.anchoredPosition = onBlack ? Vector2.zero : new Vector2(0, 66);
            subtitle.text = text; subTarget = 1f;
            yield return new WaitForSeconds(dur - 0.8f);
            subTarget = 0f;
            yield return new WaitForSeconds(0.8f);
            subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0, 66);
            subtitle.fontSize = 32;
        }

        void Say(string text) { subtitle.text = text; subAlpha = 0f; subTarget = 1f; }

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
                t += Time.unscaledDeltaTime;
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
            var c = subtitle.color; c.a = subAlpha; subtitle.color = c;
            var tc = title.color; tc.a = titleAlpha; title.color = tc;
            var sc = titleSub.color; sc.a = titleAlpha; titleSub.color = sc;
            ApplyBars();
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Aren.UI
{
    /// <summary>
    /// Argumento -eda-title: fica na tela inicial, passa a seleção pelos três botões, solta as
    /// faíscas do clique, abre e fecha Configurações e grava capturas em ~/EcosBench/title_NN.png;
    /// mede o FPS da tela inicial em ~/EcosBench/title.txt e fecha o jogo.
    /// Liga sozinho (não depende do GameFlow).
    /// </summary>
    public class DemoTitleShots : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-eda-title") { DontDestroyOnLoad(new GameObject("DemoTitleShots").AddComponent<DemoTitleShots>().gameObject); return; }
                if (a == "-eda-title-video") { var d = new GameObject("DemoTitleShots").AddComponent<DemoTitleShots>(); d.video = true; DontDestroyOnLoad(d.gameObject); return; }
            }
        }

        bool video;

        /// <summary>-eda-title-video: grava ~/EcosBench/video/f_NNNN.jpg a 30 quadros por segundo de
        /// tempo de jogo (o relógio do jogo espera a gravação de cada quadro) — vira MP4 com ffmpeg.</summary>
        IEnumerator Video()
        {
            string vdir = System.IO.Path.Combine(dir, "video");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            float t0 = Time.realtimeSinceStartup;
            while ((GameMenus.Instance == null || GameMenus.Instance.Current != GameMenus.Screen.Main) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            Time.captureDeltaTime = 1f / 30f;   // a partir daqui o relógio da tela espera cada quadro ser gravado
            var log = new System.Text.StringBuilder();
            float u0 = TitleClock.Now;
            var title = FindAnyObjectByType<TitleScreen>();
            var buttons = FindObjectsByType<TitleButton>(FindObjectsSortMode.None);
            System.Array.Sort(buttons, (a, b) => a.index.CompareTo(b.index));
            const int N = 480;   // 16 s: abertura, corvos, troca de botão, badalada
            for (int i = 0; i < N; i++)
            {
                float s = i / 30f;
                // mouse passeando devagar (paralaxe + brasas desviando)
                TitleScreen.FakeMouse = new Vector2(Screen.width * (0.5f + 0.38f * Mathf.Sin(s * 0.55f)), Screen.height * (0.42f + 0.25f * Mathf.Sin(s * 0.37f + 1f)));
                if (i == 150 && title != null) title.DebugBats(0f);
                if (i == 225 && buttons.Length > 1) EventSystem.current.SetSelectedGameObject(buttons[1].gameObject);
                if (i == 270 && buttons.Length > 2) EventSystem.current.SetSelectedGameObject(buttons[2].gameObject);
                if (i == 315 && buttons.Length > 0) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
                if (i == 330 && title != null) title.DebugToll();
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + i.ToString("0000") + ".jpg"), tex.EncodeToJPG(92));
                Destroy(tex);
            }
            TitleScreen.FakeMouse = null;
            log.AppendLine("quadros=" + N + " relógio da tela avançou " + (TitleClock.Now - u0).ToString("0.00") + " s (esperado " + (N / 30f).ToString("0.0") + ")");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "video.txt"), log.ToString());
            Time.captureDeltaTime = 0f;
            Application.Quit();
        }

        string dir;
        int n;

        IEnumerator Shot(string what)
        {
            yield return new WaitForEndOfFrame();
            string f = System.IO.Path.Combine(dir, "title_" + n.ToString("00") + ".png");
            ScreenCapture.CaptureScreenshot(f);
            Debug.Log("TITLE " + n + ": " + what);
            n++;
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            if (video) { yield return Video(); yield break; }
            foreach (var f in System.IO.Directory.GetFiles(dir, "title_*.png")) System.IO.File.Delete(f);   // inclui title_seqNN
            var log = new System.Text.StringBuilder();
            float t0 = Time.realtimeSinceStartup;
            while ((GameMenus.Instance == null || GameMenus.Instance.Current != GameMenus.Screen.Main) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            log.AppendLine("menu apareceu em " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
            // abertura: escuro → sino → a luz se espalha do brasão
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(0.75f);
                yield return Shot("abertura " + i + " (raio da luz " + TitleMotion.RevealRadius.ToString("0") + ")");
            }
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("JOGAR selecionado");
            // sequência curta para conferir a animação (velas, brasas, névoa)
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_seq" + i.ToString("00") + ".png"));
            }

            // quadros espaçados para ver o fundo em movimento (passeio da câmera, pêndulos)
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(2.2f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_mov" + i.ToString("00") + ".png"));
                log.AppendLine("mov" + i + ": câmera=" + TitleMotion.Cam.ToString("F1") + " zoom=" + TitleMotion.Zoom.ToString("F4") + " pêndulos=" + TitleMotion.Angles.ToString("F3"));
            }
            yield return new WaitForSecondsRealtime(0.6f);   // a última captura grava o PNG no quadro seguinte
            // FPS da tela inicial (cenário 3D desligado atrás da arte)
            int frames = 0; float ft = 0f, worst = 0f;
            float m0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - m0 < 5f) { yield return null; frames++; ft += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
            log.AppendLine("FPS médio na tela inicial: " + (frames / ft).ToString("0.0") + "  pior quadro: " + (worst * 1000f).ToString("0.0") + " ms  (vsync/targetFrameRate=" + Application.targetFrameRate + ")");
            var cams = Camera.allCameras;
            foreach (var c in cams) log.AppendLine("câmera " + c.name + " cullingMask=" + c.cullingMask + " clear=" + c.clearFlags);

            var buttons = FindObjectsByType<TitleButton>(FindObjectsSortMode.None);
            System.Array.Sort(buttons, (a, b) => a.index.CompareTo(b.index));
            log.AppendLine("botões: " + buttons.Length);
            for (int i = 1; i < buttons.Length; i++)
            {
                EventSystem.current.SetSelectedGameObject(buttons[i].gameObject);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("selecionado " + buttons[i].name);
            }
            buttons[1].OnSubmit(null);   // só o efeito do clique (faíscas), sem abrir nada
            yield return new WaitForSecondsRealtime(0.12f);
            yield return Shot("faíscas do clique");
            yield return new WaitForSecondsRealtime(0.6f);
            buttons[1].button.onClick.Invoke();   // abre Configurações sobre a arte
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("configurações sobre a arte");
            log.AppendLine("tela: " + GameMenus.Instance.Current);
            GameMenus.Instance.Show(GameMenus.Screen.Main);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("de volta ao menu");
            log.AppendLine("seleção ao voltar: " + (EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "nenhuma"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "title.txt"), log.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}

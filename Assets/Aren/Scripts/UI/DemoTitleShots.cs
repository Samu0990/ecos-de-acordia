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
                if (a == "-eda-title") { DontDestroyOnLoad(new GameObject("DemoTitleShots").AddComponent<DemoTitleShots>().gameObject); return; }
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
            foreach (var f in System.IO.Directory.GetFiles(dir, "title_*.png")) System.IO.File.Delete(f);   // inclui title_seqNN
            var log = new System.Text.StringBuilder();
            float t0 = Time.realtimeSinceStartup;
            while ((GameMenus.Instance == null || GameMenus.Instance.Current != GameMenus.Screen.Main) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            log.AppendLine("menu apareceu em " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
            yield return new WaitForSecondsRealtime(1.85f);
            yield return Shot("reflexo passando no título");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("JOGAR selecionado");
            // sequência curta para conferir a animação (velas, brasas, névoa)
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_seq" + i.ToString("00") + ".png"));
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

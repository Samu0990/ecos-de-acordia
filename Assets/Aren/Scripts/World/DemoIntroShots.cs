using System.Collections;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Argumento -eda-intro: começa o jogo direto (sem o menu), deixa a abertura tocar inteira
    /// e grava uma captura a cada 3.5 s em ~/EcosBench/intro_NN.png; fecha 6 s depois do fim.
    /// </summary>
    public class DemoIntroShots : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro") return true;
                return false;
            }
        }

        IEnumerator Start()
        {
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
            System.IO.Directory.CreateDirectory(dir);
            foreach (var f in System.IO.Directory.GetFiles(dir, "intro_*.png")) System.IO.File.Delete(f);
            yield return new WaitForSecondsRealtime(3f);
            GameFlow.Instance.StartGameFromTest();
            yield return new WaitForSecondsRealtime(1f);
            int n = 0; float playing = 0f;
            while (n < 30)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "intro_" + n.ToString("00") + ".png"));
                n++;
                yield return new WaitForSecondsRealtime(3.5f);
                if (GameFlow.Instance.Current == GameFlow.State.Playing) { playing += 3.5f; if (playing > 6f) break; }
            }
            Debug.Log("INTRO capturas: " + n);
            Application.Quit();
        }
    }
}

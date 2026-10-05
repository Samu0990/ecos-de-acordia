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
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro" || a == "-eda-intro-video" || a == "-eda-intro-live" || a == "-eda-intro-audio") return true;
                return false;
            }
        }

        static bool Video
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro-video") return true;
                return false;
            }
        }

        /// <summary>-eda-intro-video: grava os primeiros 52 s da abertura a 30 q/s em
        /// ~/EcosBench/intro_video/f_NNNN.jpg (o relógio do jogo espera cada quadro).</summary>
        IEnumerator RecordVideo(string dir)
        {
            string vdir = System.IO.Path.Combine(dir, "intro_video");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            yield return new WaitForSecondsRealtime(4f);
            GameFlow.Instance.StartGameFromTest();
            while (GameFlow.Instance.Current != GameFlow.State.Cutscene) yield return null;   // alinha com -eda-intro-audio
            Time.captureDeltaTime = 1f / 30f;
            for (int i = 0; i < 1560; i++)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + i.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
                Destroy(tex);
            }
            Time.captureDeltaTime = 0f;
            Debug.Log("INTRO vídeo: 1560 quadros");
            Application.Quit();
        }

        static bool AudioMode
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro-audio") return true;
                return false;
            }
        }

        static bool Live
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro-live") return true;
                return false;
            }
        }

        IEnumerator Start()
        {
            if (AudioMode)
            {
                // -eda-intro-audio: grava a mixagem do jogo desde o começo da cutscene (tempo real)
                yield return new WaitForSecondsRealtime(4f);
                GameFlow.Instance.StartGameFromTest();
                while (GameFlow.Instance.Current != GameFlow.State.Cutscene) yield return null;
                AudioTap tap = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (l.isActiveAndEnabled) { tap = l.gameObject.AddComponent<AudioTap>(); break; }
                float tt = 0f;
                while (tt < 54f) { yield return null; tt += Time.unscaledDeltaTime; }
                string adir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
                if (tap != null) tap.Save(System.IO.Path.Combine(adir, "intro_audio.wav"));
                Debug.Log("INTRO áudio gravado: " + (tap != null));
                Application.Quit();
                yield break;
            }
            if (Live)
            {
                // -eda-intro-live: só começa a abertura (sem capturas, em tempo real) e fecha 8 s
                // depois do gameplay começar — para gravar tela + áudio por fora (ffmpeg)
                yield return new WaitForSecondsRealtime(4f);
                GameFlow.Instance.StartGameFromTest();
                float livePlaying = 0f;
                while (livePlaying < 8f) { yield return null; if (GameFlow.Instance.Current == GameFlow.State.Playing) livePlaying += Time.unscaledDeltaTime; }
                Application.Quit();
                yield break;
            }
            if (Video)
            {
                yield return RecordVideo(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench"));
                yield break;
            }
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

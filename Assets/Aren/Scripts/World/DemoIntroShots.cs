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

        static int ArgInt(string name, int def)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name && int.TryParse(a[i + 1], out int v)) return v;
            return def;
        }

        static bool Has(string name) { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == name) return true; return false; }

        /// <summary>-eda-intro-video: grava a abertura a 30 q/s em ~/EcosBench/intro_video/f_NNNN.jpg (o relógio
        /// do jogo espera cada quadro) até 3 s depois do gameplay começar. Opções: -eda-intro-every K (salva 1 a
        /// cada K quadros), -eda-intro-noprologue (pula o prólogo em halftone), -eda-intro-from S (começa a salvar
        /// em S segundos).</summary>
        IEnumerator RecordVideo(string dir)
        {
            string vdir = System.IO.Path.Combine(dir, "intro_video");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            yield return new WaitForSecondsRealtime(4f);
            GameFlow.Instance.StartGameFromTest();
            while (GameFlow.Instance.Current != GameFlow.State.Cutscene) yield return null;   // alinha com -eda-intro-audio
            Time.captureDeltaTime = 1f / 30f;
            int every = Mathf.Max(1, ArgInt("-eda-intro-every", 1)), from = ArgInt("-eda-intro-from", 0) * 30;
            int i = 0, saved = 0; float after = 0f;
            float t0 = Time.realtimeSinceStartup;
            while (i < 4200 && after < 3f)
            {
                yield return new WaitForEndOfFrame();
                if (i >= from && i % every == 0)
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + i.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
                    Destroy(tex);
                    saved++;
                }
                i++;
                if (GameFlow.Instance.Current == GameFlow.State.Playing) after += 1f / 30f;
            }
            Time.captureDeltaTime = 0f;
            Debug.Log($"INTRO vídeo: {i} quadros ({saved} salvos) em {Time.realtimeSinceStartup - t0:0} s");
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

        void Awake() { if (Has("-eda-intro-noprologue")) CutsceneDirector.DebugSkipPrologue = true; if (Has("-eda-look-debug")) Night.CinematicLook.Debug = true; }

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
                float tt = 0f, secs = ArgInt("-eda-intro-seconds", 95);
                while (tt < secs) { yield return null; tt += Time.unscaledDeltaTime; }
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

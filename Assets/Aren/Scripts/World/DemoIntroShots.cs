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
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro" || a == "-eda-intro-video" || a == "-eda-intro-live" || a == "-eda-intro-audio" || a == "-eda-perf" || a == "-eda-corruption-video" || a == "-eda-corruption-audio") return true;
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

        void Awake() { UI.GameSettings.NoCursorLock = true; if (Has("-eda-intro-noprologue")) CutsceneDirector.DebugSkipPrologue = true; if (Has("-eda-look-debug")) Night.CinematicLook.Debug = true; }

        /// <summary>-eda-perf: pula a abertura e mede o FPS parado no começo da estrada ligando/desligando as partes da noite.</summary>
        IEnumerator Perf()
        {
            yield return new WaitForSecondsRealtime(4f);
            GameFlow.Instance.StartGameFromTest();
            while (GameFlow.Instance.Current != GameFlow.State.Cutscene) yield return null;
            yield return null;
            FindAnyObjectByType<CutsceneDirector>()?.Skip();
            while (GameFlow.Instance.Current != GameFlow.State.Playing) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            var cam = Camera.main;
            var land = Night.FarLands.Root != null ? Night.FarLands.Root.gameObject : null;
            var halos = GameObject.Find("Halos da noite");
            var sky = RenderSettings.skybox;
            Material plain = null;
            float far = cam != null ? cam.farClipPlane : 0f;
            IEnumerator Measure(string name)
            {
                yield return new WaitForSecondsRealtime(1f);
                int n = 0; float t = 0f;
                while (t < 5f) { yield return null; t += Time.unscaledDeltaTime; n++; }
                Debug.Log($"[PERF] {name}: {n / t:0.0} fps");
            }
            yield return Measure("tudo");
            if (land != null) land.SetActive(false);
            yield return Measure("sem paisagem");
            if (land != null) land.SetActive(true);
            RenderSettings.skybox = plain;
            yield return Measure("ceu simples");
            RenderSettings.skybox = sky;
            if (halos != null) halos.SetActive(false);
            yield return Measure("sem halos");
            if (halos != null) halos.SetActive(true);
            bool pc = RenderScaler.PostColor; RenderScaler.PostColor = false;
            yield return Measure("sem pos");
            RenderScaler.PostColor = pc;
            RenderScaler.Cinematic = true;
            yield return Measure("pos cinematico");
            RenderScaler.Cinematic = false;
            var moon = RenderSettings.sun; var sh = QualitySettings.shadows;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 45f; if (moon != null) moon.shadows = LightShadows.Soft;
            yield return Measure("sombras da lua");
            QualitySettings.shadows = sh; if (moon != null) moon.shadows = LightShadows.None;
            if (land != null) land.SetActive(false); RenderSettings.skybox = plain; if (halos != null) halos.SetActive(false);
            yield return Measure("sem paisagem+ceu+halos");
            Application.Quit();
        }

        /// <summary>-eda-corruption-video: pula para a estrada no portão (noite já aberta), dispara a cena da
        /// Corrupção na rua do mercado e grava a 30 q/s em ~/EcosBench/corruption_video/f_NNNN.jpg até 5 s
        /// depois da luta começar (-eda-intro-every K).</summary>
        IEnumerator RecordCorruption(string dir)
        {
            string vdir = System.IO.Path.Combine(dir, "corruption_video");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            var flow = GameFlow.Instance;
            while (!flow.Loaded) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            flow.DebugJump(1, new Vector3(0f, 0f, -38f), 0f, true);
            // estado de depois da abertura: Fenda aberta, impacto assentado, som doente
            if (Night.RuptureSky.Instance != null) Night.RuptureSky.Instance.fendaOpen = 1f;
            if (Night.FarLands.Root != null) { var fx = Night.ImpactFX.Prepare(Night.FarLands.ImpactPoint); fx.Fire(); fx.Settle(); }
            if (Night.OpeningSound.Instance != null) { Night.OpeningSound.Instance.Begin(); Night.OpeningSound.Instance.EnterGameplay(); }
            yield return new WaitForSecondsRealtime(1.5f);
            flow.TeleportPlayer(new Vector3(0f, 0f, -36.3f), 0f);
            if (Has("-eda-corruption-audio"))
            {
                // tempo real: grava a mixagem da cena (o ouvinte é o da câmera de cinema) num WAV
                while (flow.Current != GameFlow.State.Cutscene) yield return null;
                yield return null;
                AudioTap tap = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) { tap = l.gameObject.AddComponent<AudioTap>(); break; }
                float ta = 0f;
                while (flow.Current == GameFlow.State.Cutscene && ta < 30f) { ta += Time.unscaledDeltaTime; yield return null; }
                if (tap != null) tap.Save(System.IO.Path.Combine(dir, "corruption_audio.wav"));
                Debug.Log($"CORRUPCAO áudio: {ta:0.0} s");
                Application.Quit();
                yield break;
            }
            Time.captureDeltaTime = 1f / 30f;
            int every = Mathf.Max(1, ArgInt("-eda-intro-every", 1));
            int i = 0; float after = 0f; bool sawScene = false;
            while (i < 1500 && after < 5f)
            {
                yield return new WaitForEndOfFrame();
                if (i % every == 0)
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + i.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
                    Destroy(tex);
                }
                i++;
                if (flow.Current == GameFlow.State.Cutscene && !sawScene) { sawScene = true; Debug.Log($"CORRUPCAO cena começa no quadro {i}"); }
                if (sawScene && flow.Current == GameFlow.State.Playing) { if (after == 0f) Debug.Log($"CORRUPCAO cena termina no quadro {i}"); after += 1f / 30f; }
            }
            Time.captureDeltaTime = 0f;
            Debug.Log($"CORRUPCAO vídeo: {i} quadros, cena={sawScene}");
            Application.Quit();
        }

        IEnumerator Start()
        {
            if (Has("-eda-perf")) { yield return Perf(); yield break; }
            if (Has("-eda-corruption-video") || Has("-eda-corruption-audio")) { yield return RecordCorruption(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench")); yield break; }
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
                int frames = 0; float win = 0f, worst = 0f;
                while (tt < secs)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime;
                    tt += dt; win += dt; frames++; worst = Mathf.Max(worst, dt);
                    if (win >= 2f) { Debug.Log($"[FPS] t={tt:0} fps={frames / win:0.0} pior={worst * 1000f:0}ms estado={GameFlow.Instance.Current}"); win = 0f; frames = 0; worst = 0f; }
                }
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

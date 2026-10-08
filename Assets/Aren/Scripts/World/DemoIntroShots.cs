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
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-intro" || a == "-eda-intro-video" || a == "-eda-intro-live" || a == "-eda-intro-audio" || a == "-eda-perf" || a == "-eda-corruption-video" || a == "-eda-corruption-audio" || a == "-eda-combat-video" || a == "-eda-map-video" || a == "-eda-map-audio" || a == "-eda-city-tour") return true;
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
                float ta = 0f, worst = 0f; int nf = 0;
                while (flow.Current == GameFlow.State.Cutscene && ta < 30f) { ta += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime); nf++; if (Time.unscaledDeltaTime > 0.05f) Debug.Log($"CORRUPCAO quadro lento {Time.unscaledDeltaTime * 1000f:0} ms em t={ta:0.00}"); yield return null; }
                if (tap != null) tap.Save(System.IO.Path.Combine(dir, "corruption_audio.wav"));
                Debug.Log($"CORRUPCAO áudio: {ta:0.0} s, {nf / Mathf.Max(ta, 0.01f):0.0} fps (pior quadro {worst * 1000f:0} ms)");
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

        /// <summary>-eda-combat-video: luta no mercado com o robô apertando o combo (vida restaurada) e grava
        /// 20 s a 30 q/s em ~/EcosBench/combat_video/f_NNNN.jpg (-eda-intro-every K).</summary>
        /// <summary>
        /// -eda-map-video / -eda-map-audio: voo de câmera pela Campânula gótica (estrada e sino, portão,
        /// rua do mercado, praça e Torre dos Sinos, ponte-aqueduto, o desfiladeiro e as cachoeiras, a cidade
        /// alta e o Grande Aqueduto, plano aberto com a Fenda). Vídeo: quadros a 30 q/s em
        /// ~/EcosBench/map_video/; áudio: tempo real com o mesmo trajeto em map_audio.wav.
        /// </summary>
        IEnumerator RecordMap(string dir, bool audioOnly)
        {
            string vdir = System.IO.Path.Combine(dir, "map_video");
            System.IO.Directory.CreateDirectory(vdir);
            if (!audioOnly) foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            var flow = GameFlow.Instance;
            while (!flow.Loaded) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            flow.DebugJump(2, new Vector3(0f, 0f, -60f), 0f);
            if (Night.RuptureSky.Instance != null) Night.RuptureSky.Instance.fendaOpen = 1f;
            if (Night.OpeningSound.Instance != null) { Night.OpeningSound.Instance.Begin(); Night.OpeningSound.Instance.EnterGameplay(); }
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var h in FindObjectsByType<UI.ArenHUD>(FindObjectsSortMode.None)) h.SetVisible(false);
            var cam = new GameObject("Camera do voo").AddComponent<Camera>();
            cam.depth = 50; cam.fieldOfView = 55f; cam.nearClipPlane = 0.15f; cam.farClipPlane = Night.NightSetup.FarClip;
            cam.gameObject.AddComponent<RenderScaler>();
            foreach (var c in Camera.allCameras) if (c != cam) c.enabled = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            cam.gameObject.AddComponent<AudioListener>();
            AudioTap tap = audioOnly ? cam.gameObject.AddComponent<AudioTap>() : null;
            var shots = new (Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float fov, float dur)[]
            {
                (new Vector3(7f, 2.4f, -101f), new Vector3(0f, 9f, -40f), new Vector3(3.5f, 2.8f, -80f), new Vector3(0f, 8f, -40f), 50f, 5f),
                (new Vector3(-1.5f, 1.9f, -57f), new Vector3(0f, 5f, -42f), new Vector3(0f, 1.9f, -47.5f), new Vector3(0f, 3.5f, -20f), 55f, 4.5f),
                (new Vector3(0.5f, 1.9f, -38f), new Vector3(0f, 4.5f, 0f), new Vector3(-0.5f, 2.1f, -10f), new Vector3(0f, 6f, 20f), 58f, 6f),
                (new Vector3(-2f, 2.2f, -1f), new Vector3(0f, 9f, 36f), new Vector3(-9f, 9f, 6f), new Vector3(0f, 16f, 36f), 56f, 5.5f),
                (new Vector3(31.5f, 3.4f, 11.2f), new Vector3(60f, 1f, 9.5f), new Vector3(37f, 2.2f, 9.5f), new Vector3(60f, 1f, 9.5f), 58f, 5f),
                (new Vector3(41f, 1.9f, 11.6f), new Vector3(43f, -9f, 40f), new Vector3(44f, 1.9f, 11.6f), new Vector3(44f, -5f, 63f), 55f, 5.5f),
                (new Vector3(43f, -6.5f, -24f), new Vector3(43f, -3f, 15f), new Vector3(43.5f, -5.5f, 26f), new Vector3(44f, -4f, 62f), 58f, 7f),
                (new Vector3(22f, 24f, 18f), new Vector3(-30f, 14f, 78f), new Vector3(-8f, 30f, 6f), new Vector3(-45f, 14f, 78f), 52f, 6.5f),
                (new Vector3(-70f, 46f, -70f), new Vector3(10f, 8f, 30f), new Vector3(-52f, 40f, -48f), new Vector3(20f, 30f, 120f), 50f, 7f),
            };
            if (!audioOnly) Time.captureDeltaTime = 1f / 30f;
            int frame = 0;
            float total = 0f;
            foreach (var sh in shots)
            {
                float t = 0f;
                while (t < sh.dur)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / sh.dur);
                    var p = Vector3.Lerp(sh.p0, sh.p1, k);
                    var l = Vector3.Lerp(sh.l0, sh.l1, k);
                    cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(l - p));
                    cam.fieldOfView = sh.fov;
                    yield return new WaitForEndOfFrame();
                    if (!audioOnly)
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + frame.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
                        Destroy(tex);
                    }
                    frame++;
                    t += audioOnly ? Time.unscaledDeltaTime : 1f / 30f;
                }
                total += sh.dur;
            }
            Time.captureDeltaTime = 0f;
            if (tap != null) tap.Save(System.IO.Path.Combine(dir, "map_audio.wav"));
            Debug.Log($"MAPA voo: {frame} quadros, {total:0.0} s");
            Application.Quit();
        }

        IEnumerator RecordCombat(string dir)
        {
            string vdir = System.IO.Path.Combine(dir, "combat_video");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "f_*.jpg")) System.IO.File.Delete(f);
            var flow = GameFlow.Instance;
            while (!flow.Loaded) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            flow.DebugJump(2, new Vector3(0f, 0f, -31f), 0f);
            if (Night.RuptureSky.Instance != null) Night.RuptureSky.Instance.fendaOpen = 1f;
            if (Night.OpeningSound.Instance != null) { Night.OpeningSound.Instance.Begin(); Night.OpeningSound.Instance.EnterGameplay(); }
            var player = FindAnyObjectByType<Climbing.ThirdPersonController>().gameObject;
            var hp = player.GetComponent<Combat.ArenHealth>();
            yield return new WaitForSecondsRealtime(2.5f);
            var sb = new System.Text.StringBuilder("0:");
            for (float t = 1.5f; t < 19f; t += 0.4f)
                sb.Append(";" + t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":LMB;" + (t + 0.12f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":");
            Aren.DebugTools.ArenTestProbe.Run(player, sb.ToString(), 19.5f);
            Time.captureDeltaTime = 1f / 30f;
            int every = Mathf.Max(1, ArgInt("-eda-intro-every", 1));
            for (int i = 0; i < 600; i++)
            {
                yield return new WaitForEndOfFrame();
                if (hp != null && hp.Health < hp.maxHealth * 0.6f) hp.Heal(100f);
                if (i % every == 0)
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, "f_" + i.ToString("0000") + ".jpg"), tex.EncodeToJPG(88));
                    Destroy(tex);
                }
            }
            Time.captureDeltaTime = 0f;
            Debug.Log("COMBATE vídeo gravado; inimigos vivos=" + Combat.CombatRegistry.AliveEnemyCount());
            Application.Quit();
        }

        /// <summary>-eda-city-tour: passeio pela cidade à noite (Campânula v3): o Aren é levado a cada ponto e a câmera
        /// do jogo (atrás dele) grava ~/EcosBench/city_tour/NN_nome.jpg — a cidade como o jogador vê, com sombras e luz.</summary>
        IEnumerator CityTour(string dir)
        {
            string vdir = System.IO.Path.Combine(dir, "city_tour");
            System.IO.Directory.CreateDirectory(vdir);
            foreach (var f in System.IO.Directory.GetFiles(vdir, "*.jpg")) System.IO.File.Delete(f);
            var flow = GameFlow.Instance;
            while (!flow.Loaded) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            flow.DebugJump(7, new Vector3(0f, 0f, 6f), 0f, true);   // (etapa da torre: mercado e praça já passaram, nada dispara no caminho)
            yield return new WaitForSecondsRealtime(0.5f);
            if (Night.RuptureSky.Instance != null) Night.RuptureSky.Instance.fendaOpen = 1f;
            var pts = new (string n, Vector3 p, float yaw)[]
            {
                ("mercado", new Vector3(0f, 0f, -36f), 0f), ("beco_do_sino", new Vector3(-14f, 0f, -2.5f), 270f),
                ("rua_velha", new Vector3(-27f, 0f, -37f), 0f), ("largo_catedral", new Vector3(-80.5f, 0f, 17f), 180f),
                ("catedral_portal", new Vector3(-85f, 0f, -1f), 180f), ("rua_do_poco", new Vector3(-91.5f, 0f, 30f), 0f),
                ("fundidores", new Vector3(-69f, 0f, 30f), 0f), ("rua_do_muro", new Vector3(-55.5f, 0f, -36f), 0f),
                ("rua_alta", new Vector3(-70f, 0f, 60.5f), 90f), ("praca", new Vector3(0f, 0f, 6f), 0f),
                ("cemiterio", new Vector3(-100.2f, 0f, -6f), 180f), ("rua_norte", new Vector3(-30f, 0f, 35.5f), 270f),
            };
            int k = 0;
            foreach (var (n, p, yaw) in pts)
            {
                flow.TeleportPlayer(p, yaw);
                yield return new WaitForSecondsRealtime(2.2f);
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(vdir, (k++).ToString("00") + "_" + n + ".jpg"), tex.EncodeToJPG(90));
                Destroy(tex);
            }
            Debug.Log("CIDADE passeio gravado: " + k + " quadros");
            Application.Quit();
        }

        IEnumerator Start()
        {
            if (Has("-eda-city-tour")) { yield return CityTour(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench")); yield break; }
            if (Has("-eda-map-video") || Has("-eda-map-audio")) { yield return RecordMap(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench"), Has("-eda-map-audio")); yield break; }
            if (Has("-eda-combat-video")) { yield return RecordCombat(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench")); yield break; }
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

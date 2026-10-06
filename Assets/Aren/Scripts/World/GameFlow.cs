using System.Collections.Generic;
using Aren.Combat;
using Aren.Enemies;
using Aren.UI;
using Climbing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aren.World
{
    /// <summary>
    /// Roteiro da demo em Campanula (Bíblia de Lore: o dia da Ruptura).
    /// Menu sobre a vila → estrada (tutorial de parkour) → 13ª badalada → mercado (primeiro
    /// combate, tutorial) → praça (duas ondas, habilidades) → torre (escalada) → ponte →
    /// Campo da Fenda (o cervo, primeiro possuído) → fim com estatísticas.
    /// Também cuida de pausa, morte/checkpoint, música e dicas contextuais.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameFlow : MonoBehaviour
    {
        public GameObject ecoPrefab;
        public GameObject deerPrefab;
        public Transform bellTower;

        public enum State { Menu, Cutscene, Playing, Paused, Dead, Ending }
        public State Current { get; private set; } = State.Menu;
        public static GameFlow Instance { get; private set; }

        ThirdPersonController tpc;
        ParkourLabHUD labHud;
        GameObject player;
        ArenHealth health; ArenCombat combat; ArenAbilities abilities; ArenInput arenInput;
        InputCharacterController dpsInput;
        ArenHUD hud; GameMenus menus;
        Camera menuCam; Camera playerCam;
        CutsceneDirector cutscene;
        BellRinger bells;
        VillageCorruption corruption;
        int quietSpawns;   // quantos Ecos do mercado nascem "já ali" (eram os aldeões da cena)

        readonly List<Encounter> encounters = new List<Encounter>();
        Encounter market, plaza, field;
        Vector3 checkpointPos; float checkpointYaw;
        int step;          // progresso do roteiro
        float playTime; int maxChain; int deaths; int counters; int perfectDodges;
        float deathTimer;
        readonly HashSet<string> hintsShown = new HashSet<string>();

        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -92f);

        void Awake()
        {
            Instance = this;
            GameSettings.Load();
            ArenAudio.Preload();
            hud = new GameObject("HUD").AddComponent<ArenHUD>();
            menus = new GameObject("Menus").AddComponent<GameMenus>();
            menus.onStart = StartGame;
            menus.onResume = Resume;
            menus.onRestartCheckpoint = () => { Resume(); Respawn(); };
            menus.onMainMenu = () => ReloadScene();
            menus.onPlayAgain = () => ReloadScene(true);
            menus.onQuit = Quit;
            menus.onWorld = () => { GameFeel.SetPaused(false); Elyndra.World.RegionTravel.Go("Valteria", "portao_campanula", "VALTÉRIA", "pelo Portão Leste de Campânula"); };
        }

        void OnDestroy() { GameFeel.SetPaused(false); GameFlowState.InGame = false; ArenAudio.SetMenuMusic(false); }

        static bool skipMenuOnce;

        bool loaded, startRequested;
        public bool Loaded => loaded;

        System.Collections.IEnumerator Start()
        {
            // tela de carregamento: a montagem da noite, os shaders e os sons levam alguns segundos
            // (antes o primeiro quadro congelava); cada etapa avança a barra
            var loading = LoadingScreen.Show();
            loading.Step(0.04f, "Preparando Campanula…");
            float T0 = Time.realtimeSinceStartup;
            // câmera do menu primeiro: se algo abaixo falhar, o menu ainda funciona
            menuCam = new GameObject("MenuCamera").AddComponent<Camera>();
            menuCam.depth = 10; menuCam.farClipPlane = 280f; menuCam.fieldOfView = 50f;
            SetupCulling(menuCam, 65f, 145f);
            menuCam.gameObject.AddComponent<RenderScaler>();
            yield return null;   // espera o Start() do DPS (o Rigidbody do movimento nasce lá)
            yield return null;   // a tela de carregamento aparece antes do trabalho pesado
            try { SetupCore(); }
            catch (System.Exception e) { Debug.LogException(e); }
            loading.Step(0.16f, "Erguendo as serras do vale…");
            yield return null;
            try { Night.NightSetup.Apply(); }
            catch (System.Exception e) { Debug.LogException(e); }
            loading.Step(0.5f, "Acendendo as janelas da vila…");
            yield return null;
            try { SetupStory(); }
            catch (System.Exception e) { Debug.LogException(e); }
            loading.Step(0.62f, "Afinando os sinos…");
            yield return null;
            // compila os shaders da abertura agora, ainda no carregamento (senão o primeiro quadro dela trava)
            try { if (player != null) Night.CinematicRig.Warmup(player.transform); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { corruption?.Warmup(); }
            catch (System.Exception e) { Debug.LogException(e); }
            loading.Step(0.84f, "Preparando os sons…");
            yield return null;
            // o banco de efeitos sintetizados (thread): espera um pouco para o primeiro golpe não sair mudo
            float w = 0f;
            while (!ArenAudio.Ready && w < 6f) { w += Time.unscaledDeltaTime; loading.Step(0.84f + 0.15f * w / 6f, null); yield return null; }
            loading.Step(1f, "Pronto");
            Debug.Log($"[Carregamento] {Time.realtimeSinceStartup - T0:0.0} s (sons prontos: {ArenAudio.Ready})");
            yield return new WaitForSecondsRealtime(0.2f);
            loaded = true;
            if (skipMenuOnce) { skipMenuOnce = false; StartGame(); }
            else if (startRequested) StartGame();
            else if (Current == State.Menu) EnterMenu();   // um teste pode ter pulado direto para o jogo (DebugJump)
            loading.Hide(0.9f);
        }

        void SetupCore()
        {
            tpc = FindAnyObjectByType<ThirdPersonController>();
            player = tpc.gameObject;
            health = player.GetComponent<ArenHealth>();
            combat = player.GetComponent<ArenCombat>();
            abilities = player.GetComponent<ArenAbilities>();
            arenInput = player.GetComponent<ArenInput>();
            dpsInput = player.GetComponent<InputCharacterController>();
            playerCam = Camera.main;
            SetupCulling(playerCam, 65f, 155f);
            if (playerCam != null && playerCam.GetComponent<RenderScaler>() == null) playerCam.gameObject.AddComponent<RenderScaler>();
            var freeLook = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (freeLook != null)
            {
                ArenThirdPersonSetup.Apply(tpc, freeLook);
                var combatCamera = freeLook.GetComponent<ArenCombatCamera>();
                if (combatCamera == null) combatCamera = freeLook.gameObject.AddComponent<ArenCombatCamera>();
                combatCamera.Bind(player.transform, combat);
            }
            hud.Bind(player);
            if (player.GetComponent<BlobShadow>() == null) player.AddComponent<BlobShadow>().radius = 0.5f;
            if (player.GetComponent<ArenFootsteps>() == null) player.AddComponent<ArenFootsteps>();
            if (player.GetComponent<ArenAnimationEvents>() == null) player.AddComponent<ArenAnimationEvents>();
            if (player.GetComponent<ArenFlutePerformancePose>() == null) player.AddComponent<ArenFlutePerformancePose>();
            if (player.GetComponent<ArenBuffOrbs>() == null) player.AddComponent<ArenBuffOrbs>();
            if (GetComponent<AdaptivePerformance>() == null) gameObject.AddComponent<AdaptivePerformance>();
            // riacho a leste: laços de água ao longo do leito (a ponte fica em z 9.5)
            // (no desfiladeiro a água corre 16 m abaixo: os laços ficam no fundo, mais altos e de alcance maior)
            foreach (float z in new[] { -22f, 9.5f, 38f })
                ArenAudio.AmbientLoop("amb_water", new Vector3(Campanula.StreamMath.Center(z), -14f, z), 6f, 40f, 0.7f);
            // a cachoeira da cabeceira do cânion e as que caem do Grande Aqueduto
            ArenAudio.AmbientLoop("amb_water", new Vector3(Campanula.StreamMath.Center(62f), -8f, 61f), 8f, 70f, 1f);
            foreach (var w in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (w.name == "Cachoeira" && w.transform.position.z > 70f) ArenAudio.AmbientLoop("amb_water", w.transform.position + Vector3.down * 12f, 6f, 55f, 0.6f);
            // painel de debug do laboratório de parkour: escondido na demo (F3 alterna)
            labHud = FindAnyObjectByType<ParkourLabHUD>();
            if (labHud != null) labHud.enabled = false;
            GameSettings.Apply(true);
        }

        /// <summary>Depois da noite montada: o sino da estrada, a paisagem sonora, a abertura e os encontros.</summary>
        void SetupStory()
        {
            var shrinePos = SpawnPos + new Vector3(2.6f, 0f, 0.9f);
            shrinePos.y = Campanula.GroundHeight.At(shrinePos.x, shrinePos.z);
            Night.BellShrine.Build(shrinePos, 90f);
            Night.OpeningSound.Create();
            if (player.GetComponent<Night.CinematicLook>() == null) player.AddComponent<Night.CinematicLook>();
            if (bellTower != null) bells = bellTower.gameObject.AddComponent<BellRinger>();
            cutscene = gameObject.AddComponent<CutsceneDirector>();
            cutscene.Setup(menuCam);
            // os aldeões na rua do mercado (a Corrupção os toma na frente do Aren)
            try { corruption = VillageCorruption.Create(); } catch (System.Exception e) { Debug.LogException(e); }

            health.OnDied += OnPlayerDied;
            combat.OnPerfectDodge += () => { perfectDodges++; Notas.Add(25); hud.Toast("Esquiva perfeita", UIKit.Cyan); };
            combat.OnCounter += n => { counters++; Notas.Add(50 * Mathf.Max(1, n)); hud.Toast(n > 1 ? "Contra-ataque ×" + n : "Contra-ataque", UIKit.Gold); };
            abilities.OnCast += id => { if (id == AbilityId.Contracanto) hud.Toast("Contracanto", UIKit.Gold, 0.9f); };

            BuildEncounters();
            if (DemoBenchmark.Requested) gameObject.AddComponent<DemoBenchmark>();
            if (DemoAutoTest.Requested) gameObject.AddComponent<DemoAutoTest>();
            if (DemoDiag.Requested) gameObject.AddComponent<DemoDiag>();
            if (DemoIntroShots.Requested) gameObject.AddComponent<DemoIntroShots>();
            if (DemoUIShots.Requested) gameObject.AddComponent<DemoUIShots>();
            checkpointPos = SpawnPos; checkpointYaw = 0f;
            TeleportPlayer(SpawnPos, 0f);

        }

        /// <summary>Props pequenos (camada 11) e árvores (12) somem de longe; o resto vai até o far clip.</summary>
        static void SetupCulling(Camera c, float detail, float vegetation)
        {
            if (c == null) return;
            var d = new float[32];
            d[11] = detail; d[12] = vegetation;
            c.layerCullDistances = d;
            c.layerCullSpherical = true;
            c.useOcclusionCulling = true;
        }

        // ------------------------------------------------------------ encontros

        Vector3 G(float x, float z) => new Vector3(x, 0.1f, z);

        /// <summary>Os Ecos são as pessoas da vila: cada um nasce com o corpo de um aldeão (variante aleatória).</summary>
        GameObject Eco() { var v = VillageCorruption.RandomEcoPrefab(); return v != null ? v : ecoPrefab; }

        void BuildEncounters()
        {
            market = new Encounter { name = "mercado", center = new Vector3(0, 0, -30f), triggerRadius = 6f };
            market.waves.Add(new List<(GameObject, Vector3)> { (Eco(), G(-1.5f, -21f)), (Eco(), G(2f, -18f)) });
            market.onSpawn = e => { if (quietSpawns > 0) { quietSpawns--; e.quietSpawn = true; } };
            market.onWaveStart = w => { Elyndra.World.WorldState.AdvancePhase(Elyndra.World.ValteriaPhase.CorrupcaoCrescente); ArenAudio.PlaySting(Sting.Start); hud.ShowHint("<b>Clique esquerdo</b>: atacar  ·  aponte com WASD para escolher o alvo", 6f); AudioIntensity(0.6f); };
            market.onComplete = () => { ArenAudio.PlaySting(Sting.Clear); Checkpoint(new Vector3(0, 0, -16f), 0f); NextStep(); };
            encounters.Add(market);

            plaza = new Encounter { name = "praça", center = new Vector3(0, 0, 8f), triggerRadius = 11f };
            plaza.waves.Add(new List<(GameObject, Vector3)> { (Eco(), G(-8f, 18f)), (Eco(), G(8f, 18f)), (Eco(), G(-10f, 4f)), (Eco(), G(10f, 4f)) });
            plaza.waves.Add(new List<(GameObject, Vector3)> { (Eco(), G(0f, 22f)), (Eco(), G(-12f, 12f)), (Eco(), G(12f, 12f)), (Eco(), G(-6f, -1f)), (Eco(), G(6f, -1f)) });
            plaza.onWaveStart = w =>
            {
                AudioIntensity(w == 0 ? 0.7f : 0.9f);
                ArenAudio.PlaySting(w == 0 ? Sting.Start : Sting.Mystery);
                hud.ShowHint(w == 0 ? "<b>Clique direito</b> quando o anel dourado fechar: contra-ataque  ·  <b>Ctrl</b>: esquiva"
                                    : "Orbe vermelho: Eco Cantor à distância · <b>Q</b> Pulso · <b>E</b> Lâmina · <b>R</b> Eco · <b>segure o clique</b>: Contracanto", 8f);
            };
            plaza.onSpawn = e =>
            {
                // Um Cantor por onda: orbe vermelho avisa o projétil e cria variedade
                // sem um prefab/material extra (ainda usa o aldeão possuído existente).
                if (e is EnemyEco eco)
                {
                    Vector3 p = e.transform.position;
                    if ((p.x > 7f && p.z > 16f) || (Mathf.Abs(p.x) < 1f && p.z > 20f))
                        eco.SetRangedVariant();
                }
            };
            plaza.onComplete = () => { ArenAudio.PlaySting(Sting.Clear); Checkpoint(new Vector3(0, 0, 20f), 180f); NextStep(); };
            encounters.Add(plaza);

            field = new Encounter { name = "campo", center = new Vector3(70f, 0, 14f), triggerRadius = 16f };
            var boss = deerPrefab != null ? deerPrefab : ecoPrefab;   // (sem o cervo, o manequim faz o chefe)
            field.waves.Add(new List<(GameObject, Vector3)> { (boss, G(80f, 16f)), (Eco(), G(72f, 26f)), (Eco(), G(74f, 2f)) });
            field.onWaveStart = w =>
            {
                AudioIntensity(1f);
                ArenAudio.PlaySting(Sting.Boss);
                Campanula.RiftPulse.Instance?.Burst(1f);
                hud.ShowHint("Esquive da investida (<b>Ctrl</b>) e contra-ataque o pisão (<b>clique direito</b>)", 7f);
            };
            field.onSpawn = e =>
            {
                if (e is EnemyEco ranged && e.transform.position.z > 20f) ranged.SetRangedVariant();
                if (deerPrefab == null && e is EnemyEco && !hintsShown.Contains("boss"))
                {
                    // sem o cervo: um Eco maior faz o papel de chefe
                    hintsShown.Add("boss");
                    e.transform.localScale = Vector3.one * 1.5f;
                    e.maxHealth = 160; e.isBoss = true; e.displayName = "Cervo de Contratempo"; e.subtitle = "Inversão — o som chega antes do movimento";
                }
            };
            field.onComplete = () =>
            {
                ArenAudio.PlaySting(Sting.Clear);
                // o caminho para Valtéria abre e o Vórtice da Ermida do Sino passa a existir (Elyndra.World)
                Elyndra.World.WorldState.SetFlag("campanula_cervo");
                Elyndra.World.WorldState.AdvancePhase(Elyndra.World.ValteriaPhase.VorticeAtivo);
                Invoke(nameof(BeginEnding), 2.2f);
            };
            encounters.Add(field);
        }

        // ------------------------------------------------------------ menu

        void EnterMenu()
        {
            Current = State.Menu;
            GameFlowState.InGame = false;
            SetPlayerControl(false);
            hud.SetVisible(false);
            menuCam.gameObject.SetActive(true);
            menus.Show(GameMenus.Screen.Main);
            menus.FadeTo(0f, 0.8f);
            AudioIntensity(0f);
            ArenAudio.SetMenuMusic(true);
        }

        /// <summary>Testes do executável: começa o jogo como se o jogador tivesse clicado em Jogar.</summary>
        public void StartGameFromTest() { if (loaded) StartGame(); else startRequested = true; }

        void StartGame()
        {
            Notas.Reset();
            StartCoroutine(StartRoutine());
        }

        System.Collections.IEnumerator StartRoutine()
        {
            float T() => Time.realtimeSinceStartup;
            Debug.Log($"[Início] StartRoutine {T():0.00}");
            ArenAudio.SetMenuMusic(false);
            menus.FadeTo(1f, 3f);
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[Início] após espera {T():0.00}");
            menus.Show(GameMenus.Screen.None);
            Debug.Log($"[Início] menu fechado {T():0.00}");
            menuCam.gameObject.SetActive(false);
            TeleportPlayer(checkpointPos, checkpointYaw);
            Debug.Log($"[Início] teleporte {T():0.00}");
            bool intro = step == 0;
            if (intro)
            {
                // abertura: prólogo + a noite da Ruptura (storyboard do autor)
                Debug.Log($"[Abertura] estado Cutscene em {Time.realtimeSinceStartup:0.00}");
                Current = State.Cutscene;
                GameFlowState.InGame = true;
                hud.SetVisible(false);
                SetPlayerControl(false);
                menus.FadeTo(0f, 0.2f);
                ArenAudio.SetMusicEnabled(false);   // quem fala na abertura é a paisagem sonora de Campanula
                yield return cutscene.Play(player.transform, bells, deerPrefab, player.GetComponent<ArenFlute>());
                step = 1;   // os sinos já tocaram na abertura
                ArenAudio.SetMusicEnabled(true);
                Night.OpeningSound.Instance?.EnterGameplay();
                if (cutscene.Skipped)
                {
                    menus.FadeTo(1f, 0.01f);
                    TeleportPlayer(checkpointPos, checkpointYaw);
                }
            }
            bool continuous = intro && !cutscene.Skipped;   // a câmera da abertura já pousou na de jogo
            Current = State.Playing;
            GameFlowState.InGame = true;
            hud.SetVisible(true);
            SetPlayerControl(true);
            if (!continuous)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                menus.FadeTo(0f, intro ? 0.9f : 1.2f);
            }
            // estado do mundo (Elyndra.World): depois da abertura a Fenda abriu e o brilho caiu em Valtéria
            Elyndra.World.WorldState.AdvancePhase(Elyndra.World.ValteriaPhase.BrilhoCaiu);
            if (intro)
            {
                hud.ShowArea("Estrada de Campanula", "a Fenda se abriu no horizonte");
                hud.ShowObjective("Corra para a vila: algo está errado na rua do mercado");
                hud.ShowHint("<b>WASD</b> mover · <b>Shift</b> correr · <b>Espaço</b> pular", 6f);
            }
        }

        // ------------------------------------------------------------ loop

        void Update()
        {
            if (Current == State.Menu) { OrbitMenuCam(); return; }
            // Esc/Start direto do Input System (o InputCharacterController do DPS fica
            // desligado durante a pausa, então não dá para depender do evento dele)
            bool esc = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (esc) OnEscape();
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame && labHud != null) labHud.enabled = !labHud.enabled;
            if (Current == State.Paused) return;

            if (Current == State.Dead)
            {
                deathTimer += Time.unscaledDeltaTime;
                if (deathTimer > 2.6f) { menus.FadeTo(1f, 3f); if (deathTimer > 3.1f) { Respawn(); menus.FadeTo(0f, 1.5f); menus.Show(GameMenus.Screen.None); Current = State.Playing; } }
                return;
            }
            if (Current != State.Playing) return;

            playTime += Time.deltaTime;
            maxChain = Mathf.Max(maxChain, combat.ChainCount);
            Vector3 p = player.transform.position;
            foreach (var e in encounters) e.Tick(p, this);
            Story(p);
            // caiu no desfiladeiro (Campânula gótica): volta para a borda mais próxima, com um escurecer rápido
            if (p.y < -6f && !rescuing) StartCoroutine(GorgeRescue(p));
            Hints(p);

            // música: combate perto → camadas; nada perto → volta ao ambiente
            bool near = false;
            foreach (var en in CombatRegistry.Enemies)
                if (CombatRegistry.IsValid(en) && (en.transform.position - p).sqrMagnitude < 35f * 35f) { near = true; break; }
            if (!near && Current == State.Playing) AudioIntensity(0f);
        }

        void Story(Vector3 p)
        {
            switch (step)
            {
                case 0:   // perto dos portões: os sinos da vila tocam o fim de tarde — e a Ressonância responde errado
                    if (p.z > -56f)
                    {
                        NextStep();
                        hud.ShowObjective("Atravesse os portões");
                        bells?.Toll(12, false);
                        StartCoroutine(After(10.5f, () =>
                        {
                            hud.ShowHint("Campânula tenta cantar e as vozes falham. Desde a queda do brilho, a Ressonância do vale responde errado.", 7f);
                            ArenAudio.PlaySting(Sting.Mystery);
                            hud.ShowObjective("Algo está errado na rua do mercado");
                        }));
                    }
                    break;
                case 1:
                    if (p.z > -36.5f)   // depois do arco do portão (a cena precisa da rua livre atrás do Aren)
                    {
                        NextStep();
                        Checkpoint(new Vector3(0, 0, -36f), 0f);
                        if (corruption != null && !corruption.Done && !corruption.Running) StartCoroutine(CorruptionScene());
                        else hud.ShowArea("Rua do Mercado", "Campânula · Valtéria");
                    }
                    break;
                case 2: break;   // espera o encontro do mercado (NextStep no onComplete)
                case 3:
                    hud.ShowObjective("Chegue à Praça do Coro");
                    NextStep();
                    break;
                case 4:
                    if (p.z > -3f) { NextStep(); hud.ShowArea("Praça do Coro", "onde Campânula canta junta ao pôr do sol"); }
                    break;
                case 5: break;   // espera a praça
                case 6:
                    hud.ShowObjective("Escale a Torre do Sino Grande pelas pedras salientes (face sul, à esquerda da porta)");
                    hud.ShowHint("Pule para agarrar a pedra · <b>W + Espaço</b> salta para a de cima · <b>C</b> solta", 8f);
                    NextStep();
                    break;
                case 7:
                    if (p.y > 17f && Mathf.Abs(p.x) < 6f && p.z > 28f)
                    {
                        NextStep();
                        hud.ShowArea("Torre do Sino Grande", "daqui se vê a Fenda — muito além das montanhas");
                        bells?.Toll(4, false);
                        Checkpoint(new Vector3(6f, 0, 22f), 90f);
                        hud.ShowObjective("Siga para o leste: atravesse a ponte até o Campo dos Cascos");
                    }
                    else if (p.x > 34f)   // pulou a torre: tudo bem, segue o roteiro
                    {
                        NextStep();
                        hud.ShowObjective("Atravesse a ponte até o Campo dos Cascos");
                    }
                    break;
                case 8:
                    if (p.x > 48f) { NextStep(); hud.ShowArea("Campo dos Cascos", "o som do casco chega antes da pata"); Checkpoint(new Vector3(50f, 0, 9.5f), 90f); }
                    break;
            }
        }

        /// <summary>
        /// A Corrupção toma os aldeões da rua do mercado na frente do Aren (cena curta com a câmera de
        /// cinema); no fim os dois tomados viram os Ecos da luta, no mesmo lugar.
        /// </summary>
        System.Collections.IEnumerator CorruptionScene()
        {
            Current = State.Cutscene;
            market.Locked = true;
            SetPlayerControl(false);
            hud.SetVisible(false);
            var rb = player.GetComponent<Rigidbody>(); if (rb != null) rb.linearVelocity = Vector3.zero;
            var cam = menuCam;
            var camL = cam.GetComponent<AudioListener>(); if (camL == null) camL = cam.gameObject.AddComponent<AudioListener>();
            var mainL = playerCam != null ? playerCam.GetComponent<AudioListener>() : null;
            cam.gameObject.SetActive(true);
            cam.nearClipPlane = 0.12f; cam.farClipPlane = Night.NightSetup.FarClip;
            if (playerCam != null) playerCam.enabled = false;
            if (mainL != null) mainL.enabled = false;
            camL.enabled = true;
            var ecos = new List<(GameObject prefab, Vector3 pos, float yaw)>();
            yield return corruption.Play(cam, player.transform, ecos);
            camL.enabled = false;
            if (mainL != null) mainL.enabled = true;
            if (playerCam != null) playerCam.enabled = true;
            cam.gameObject.SetActive(false);
            if (ecos.Count > 0)
            {
                var wave = new List<(GameObject, Vector3)>();
                var yaws = new List<float>();
                foreach (var e in ecos) { wave.Add((e.prefab != null ? e.prefab : ecoPrefab, e.pos)); yaws.Add(e.yaw); }
                market.waves[0] = wave;
                market.firstWaveYaw = yaws;
                quietSpawns = wave.Count;
            }
            Current = State.Playing;
            SetPlayerControl(true);
            hud.SetVisible(true);
            market.ForceStart(this, true);
            hud.ShowArea("Rua do Mercado", "eram pessoas — a Fenda desafinou a nota delas");
        }

        void Hints(Vector3 p)
        {
            void Once(string id, bool cond, string text, float t = 5f) { if (cond && !hintsShown.Contains(id)) { hintsShown.Add(id); hud.ShowHint(text, t); } }
            Once("vault", p.z > -87f && p.z < -81f && Mathf.Abs(p.x) < 7f, "Corra até o muro baixo e aperte <b>Espaço</b> para saltar por cima");
            Once("hay", p.z > -77f && p.z < -72f && Mathf.Abs(p.x) < 4f, "<b>Espaço</b> perto dos fardos: passar por cima");
            Once("slide", p.z > -68f && p.z < -63f && Mathf.Abs(p.x) < 5f, "Correndo (<b>Shift</b>), aperte <b>C</b> para deslizar por baixo da viga");
            Once("wall", p.z > -50f && p.z < -44.5f && p.x < -15f && p.x > -24f, "As pedras salientes da muralha dão para escalar: pule para agarrar");
        }

        void NextStep() => step++;

        bool rescuing;
        System.Collections.IEnumerator GorgeRescue(Vector3 p)
        {
            rescuing = true;
            menus.FadeTo(1f, 0.25f);
            yield return new WaitForSecondsRealtime(0.35f);
            float cx = Campanula.StreamMath.Center(p.z);
            float z = Mathf.Abs(p.z - 9.5f) < 4f ? p.z + 6f : p.z;   // não reaparece em cima da ponte
            float x = p.x < cx ? cx - Campanula.StreamMath.GorgeHalfTop - 2.5f : cx + Campanula.StreamMath.GorgeHalfTop + 2.5f;
            var safe = new Vector3(x, Campanula.GroundHeight.At(x, z, 0f), z);
            TeleportPlayer(safe, p.x < cx ? -90f : 90f);
            yield return new WaitForSecondsRealtime(0.2f);
            menus.FadeTo(0f, 0.6f);
            rescuing = false;
        }

        void Checkpoint(Vector3 pos, float yaw) { checkpointPos = pos; checkpointYaw = yaw; ArenAudio.PlayUI(Sfx.Checkpoint, 0.4f); }

        void AudioIntensity(float v) => ArenAudio.SetIntensity(v);

        // ------------------------------------------------------------ pausa / morte / fim

#if !UNITY_EDITOR
        // alt-tab no executável: pausa (o jogo continua rodando em segundo plano, mas parado)
        void OnApplicationFocus(bool focus)
        {
            if (!focus && Current == State.Playing && !DemoBenchmark.Requested && !DemoAutoTest.Requested && !DemoDiag.Requested && !DemoIntroShots.Requested && !DemoUIShots.Requested && !Elyndra.World.WorldAutoTest.Requested) Pause();
        }
#endif

        void OnEscape()
        {
            if (Current == State.Cutscene) { cutscene.Skip(); return; }
            if (Current == State.Playing) Pause();
            else if (Current == State.Paused && menus.Current == GameMenus.Screen.Pause && !menus.BackHandledThisFrame) Resume();
        }

        public void PauseFromTest() => Pause();

        void Pause()
        {
            Current = State.Paused;
            GameFeel.SetPaused(true);
            SetPlayerControl(false);
            menus.Show(GameMenus.Screen.Pause);
            ArenAudio.PlayUI(Sfx.UIPage, 0.6f);
        }

        void Resume()
        {
            if (Current != State.Paused) return;
            GameFeel.SetPaused(false);
            menus.Show(GameMenus.Screen.None);
            Current = State.Playing;
            SetPlayerControl(true);
        }

        void OnPlayerDied()
        {
            deaths++;
            Current = State.Dead;
            deathTimer = 0f;
            SetPlayerControl(false);
            GameFeel.SlowMo(1.2f, 0.3f);
            ArenAudio.PlaySting(Sting.Defeat);
            menus.Show(GameMenus.Screen.Death);
        }

        void Respawn()
        {
            foreach (var e in encounters) e.ResetIfActive();
            TeleportPlayer(checkpointPos, checkpointYaw);
            health.Revive(1f);
            combat.PlayGetUp();
            SetPlayerControl(true);
            AudioIntensity(0f);
        }

        void BeginEnding()
        {
            if (Current == State.Ending) return;
            Current = State.Ending;
            GameFlowState.InGame = false;
            SetPlayerControl(false);
            GameFeel.SlowMo(1.5f, 0.35f);
            AudioIntensity(0f);
            int defeated = 0; foreach (var e in encounters) defeated += e.Defeated;
            int min = Mathf.FloorToInt(playTime / 60f), sec = Mathf.FloorToInt(playTime % 60f);
            menus.SetEndStats(
                "Tempo  <b>" + min + ":" + sec.ToString("00") + "</b>\n" +
                "Maior cadência  <b>" + maxChain + "</b>     Ecos dissipados  <b>" + defeated + "</b>\n" +
                "Contra-ataques  <b>" + counters + "</b>     Esquivas perfeitas  <b>" + perfectDodges + "</b>     Quedas  <b>" + deaths + "</b>\n" +
                "Notas  <b>" + GothicUI.Thousands(Notas.Value) + "</b>");
            StartCoroutine(EndRoutine());
        }

        System.Collections.IEnumerator EndRoutine()
        {
            yield return new WaitForSecondsRealtime(1.4f);
            bells?.Toll(12, false);
            menus.Show(GameMenus.Screen.End);
            hud.SetVisible(false);
        }

        static System.Collections.IEnumerator After(float seconds, System.Action a)
        {
            yield return new WaitForSeconds(seconds);
            a?.Invoke();
        }

        void ReloadScene(bool skipMenu = false)
        {
            GameFeel.SetPaused(false);
            skipMenuOnce = skipMenu;
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(s.buildIndex >= 0 ? s.buildIndex : 0);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Chegando de outro reino de Elyndra (Elyndra.World.CampanulaLink): sem menu e sem abertura, com os
        /// encontros de Campânula já vencidos, no Portão Leste.
        /// </summary>
        public void ArriveFromWorld(Vector3 pos, float yaw)
        {
            DebugJump(9, pos, yaw, true);
            foreach (var e in encounters) e.Done = true;
            hud.ShowArea("Campânula", "Portão Leste · de volta de Valtéria");
        }

        /// <summary>Só para testes: pula o menu e começa num ponto do roteiro.</summary>
        public void DebugJump(int stepIndex, Vector3 pos, float yaw, bool keepStoryScenes = false)
        {
            StopAllCoroutines();
            if (!keepStoryScenes) corruption?.Cancel();
            menus.Show(GameMenus.Screen.None);
            menus.FadeTo(0f, 100f);
            menuCam.gameObject.SetActive(false);
            Current = State.Playing;
            GameFlowState.InGame = true;
            hud.SetVisible(true);
            step = stepIndex;
            foreach (var e in encounters) e.ResetFull();
            if (stepIndex > 2) market.Done = true;
            if (stepIndex > 5) plaza.Done = true;
            SetPlayerControl(true);
            TeleportPlayer(pos, yaw);
            checkpointPos = pos; checkpointYaw = yaw;
        }

        // ------------------------------------------------------------ jogador

        void SetPlayerControl(bool on)
        {
            if (arenInput != null) arenInput.SetCombatInputEnabled(on);
            if (dpsInput != null)
            {
                dpsInput.enabled = on;
                dpsInput.movement = Vector2.zero; dpsInput.run = false; dpsInput.jump = false;
            }
            if (!on) combat.ForceFree();
            if (!GameSettings.NoCursorLock) Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        public void TeleportPlayer(Vector3 pos, float yaw)
        {
            pos.y = Campanula.GroundHeight.At(pos.x, pos.z) + 0.05f;
            var rb = player.GetComponent<Rigidbody>();
            var jp = player.GetComponent<JumpPredictionController>();
            if (jp != null) jp.curPoint = null;
            tpc.isVaulting = false; tpc.isJumping = false; tpc.onAir = false;
            tpc.EnableController();
            var rot = Quaternion.Euler(0, yaw, 0);
            rb.position = pos; rb.rotation = rot;
            player.transform.SetPositionAndRotation(pos, rot);
            rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            var anim = player.GetComponent<Animator>();
            anim.SetBool("Land", false); anim.SetBool("onAir", false); anim.SetBool("Hanging", false);
            anim.SetInteger("Climb State", 0);
            anim.Play("Idle", 0, 0f);
            var fl = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (fl != null) { fl.m_XAxis.Value = yaw; fl.m_YAxis.Value = 0.45f; fl.PreviousStateIsValid = false; }
        }

        void OrbitMenuCam()
        {
            float t = Time.unscaledTime * 0.035f;
            Vector3 c = new Vector3(0f, 0f, 10f);
            Vector3 pos = c + new Vector3(Mathf.Sin(t) * 62f, 24f + Mathf.Sin(t * 0.7f) * 4f, -Mathf.Cos(t) * 62f);
            menuCam.transform.position = pos;
            menuCam.transform.LookAt(new Vector3(8f, 11f, 22f));
        }
    }
}

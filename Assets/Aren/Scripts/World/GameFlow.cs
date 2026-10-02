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

        public enum State { Menu, Playing, Paused, Dead, Ending }
        public State Current { get; private set; } = State.Menu;
        public static GameFlow Instance { get; private set; }

        ThirdPersonController tpc;
        ParkourLabHUD labHud;
        GameObject player;
        ArenHealth health; ArenCombat combat; ArenAbilities abilities; ArenInput arenInput;
        InputCharacterController dpsInput;
        ArenHUD hud; GameMenus menus;
        Camera menuCam; Camera playerCam;
        BellRinger bells;

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
        }

        void OnDestroy() { GameFeel.SetPaused(false); GameFlowState.InGame = false; }

        static bool skipMenuOnce;

        System.Collections.IEnumerator Start()
        {
            // câmera do menu primeiro: se algo abaixo falhar, o menu ainda funciona
            menuCam = new GameObject("MenuCamera").AddComponent<Camera>();
            menuCam.depth = 10; menuCam.farClipPlane = 460f; menuCam.fieldOfView = 50f;
            SetupCulling(menuCam, 85f, 200f);
            menuCam.gameObject.AddComponent<RenderScaler>();
            yield return null;   // espera o Start() do DPS (o Rigidbody do movimento nasce lá)
            try { Setup(); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (skipMenuOnce) { skipMenuOnce = false; StartGame(); }
            else EnterMenu();
        }

        void Setup()
        {
            tpc = FindAnyObjectByType<ThirdPersonController>();
            player = tpc.gameObject;
            health = player.GetComponent<ArenHealth>();
            combat = player.GetComponent<ArenCombat>();
            abilities = player.GetComponent<ArenAbilities>();
            arenInput = player.GetComponent<ArenInput>();
            dpsInput = player.GetComponent<InputCharacterController>();
            playerCam = Camera.main;
            SetupCulling(playerCam, 70f, 180f);
            if (playerCam != null && playerCam.GetComponent<RenderScaler>() == null) playerCam.gameObject.AddComponent<RenderScaler>();
            hud.Bind(player);
            // painel de debug do laboratório de parkour: escondido na demo (F3 alterna)
            labHud = FindAnyObjectByType<ParkourLabHUD>();
            if (labHud != null) labHud.enabled = false;
            GameSettings.Apply(true);
            if (bellTower != null) bells = bellTower.gameObject.AddComponent<BellRinger>();

            health.OnDied += OnPlayerDied;
            combat.OnPerfectDodge += () => { perfectDodges++; hud.Toast("Esquiva perfeita", UIKit.Cyan); };
            combat.OnCounter += n => { counters++; hud.Toast(n > 1 ? "Contra-ataque ×" + n : "Contra-ataque", UIKit.Gold); };
            abilities.OnCast += id => { if (id == AbilityId.Contracanto) hud.Toast("Contracanto", UIKit.Gold, 0.9f); };

            BuildEncounters();
            if (DemoBenchmark.Requested) gameObject.AddComponent<DemoBenchmark>();
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

        void BuildEncounters()
        {
            market = new Encounter { name = "mercado", center = new Vector3(0, 0, -30f), triggerRadius = 6f };
            market.waves.Add(new List<(GameObject, Vector3)> { (ecoPrefab, G(-1.5f, -21f)), (ecoPrefab, G(2f, -18f)) });
            market.onWaveStart = w => { hud.ShowHint("<b>Clique esquerdo</b>: atacar  ·  aponte com WASD para escolher o alvo", 6f); AudioIntensity(0.6f); };
            market.onComplete = () => { Checkpoint(new Vector3(0, 0, -16f), 0f); NextStep(); };
            encounters.Add(market);

            plaza = new Encounter { name = "praça", center = new Vector3(0, 0, 8f), triggerRadius = 11f };
            plaza.waves.Add(new List<(GameObject, Vector3)> { (ecoPrefab, G(-8f, 18f)), (ecoPrefab, G(8f, 18f)), (ecoPrefab, G(-10f, 4f)), (ecoPrefab, G(10f, 4f)) });
            plaza.waves.Add(new List<(GameObject, Vector3)> { (ecoPrefab, G(0f, 22f)), (ecoPrefab, G(-12f, 12f)), (ecoPrefab, G(12f, 12f)), (ecoPrefab, G(-6f, -1f)), (ecoPrefab, G(6f, -1f)) });
            plaza.onWaveStart = w =>
            {
                AudioIntensity(w == 0 ? 0.7f : 0.9f);
                hud.ShowHint(w == 0 ? "<b>Clique direito</b> quando o anel dourado fechar: contra-ataque  ·  <b>Ctrl</b>: esquiva"
                                    : "Habilidades: <b>Q</b> Pulso · <b>E</b> Lâmina · <b>R</b> Eco Fantasma · <b>segure o clique</b>: Contracanto", 8f);
            };
            plaza.onComplete = () => { Checkpoint(new Vector3(0, 0, 20f), 180f); NextStep(); };
            encounters.Add(plaza);

            field = new Encounter { name = "campo", center = new Vector3(70f, 0, 14f), triggerRadius = 16f };
            var boss = deerPrefab != null ? deerPrefab : ecoPrefab;
            field.waves.Add(new List<(GameObject, Vector3)> { (boss, G(80f, 16f)), (ecoPrefab, G(72f, 26f)), (ecoPrefab, G(74f, 2f)) });
            field.onWaveStart = w =>
            {
                AudioIntensity(1f);
                Campanula.RiftPulse.Instance?.Burst(1f);
                hud.ShowHint("Esquive da investida (<b>Ctrl</b>) e contra-ataque o pisão (<b>clique direito</b>)", 7f);
            };
            field.onSpawn = e =>
            {
                if (deerPrefab == null && e is EnemyEco && !hintsShown.Contains("boss"))
                {
                    // sem o cervo: um Eco maior faz o papel de chefe
                    hintsShown.Add("boss");
                    e.transform.localScale = Vector3.one * 1.5f;
                    e.maxHealth = 160; e.isBoss = true; e.displayName = "O Primeiro Possuído"; e.subtitle = "o eco que andava antes dos próprios passos";
                }
            };
            field.onComplete = () => Invoke(nameof(BeginEnding), 2.2f);
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
        }

        void StartGame()
        {
            StartCoroutine(StartRoutine());
        }

        System.Collections.IEnumerator StartRoutine()
        {
            menus.FadeTo(1f, 3f);
            yield return new WaitForSecondsRealtime(0.4f);
            menus.Show(GameMenus.Screen.None);
            menuCam.gameObject.SetActive(false);
            TeleportPlayer(checkpointPos, checkpointYaw);
            Current = State.Playing;
            GameFlowState.InGame = true;
            hud.SetVisible(true);
            SetPlayerControl(true);
            yield return new WaitForSecondsRealtime(0.25f);
            menus.FadeTo(0f, 1.2f);
            if (step == 0)
            {
                hud.ShowArea("Estrada de Campanula", "o dia da Festa da Afinação");
                hud.ShowObjective("Siga pela estrada até os portões de Campanula");
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
                case 0:   // perto dos portões: os sinos tocam treze vezes
                    if (p.z > -56f)
                    {
                        NextStep();
                        hud.ShowObjective("Atravesse os portões");
                        bells?.Toll(12, true, () =>
                        {
                            hud.ShowHint("A décima terceira badalada não veio de sino nenhum. Veio do céu.", 6f);
                            hud.ShowObjective("Algo está errado na rua do mercado");
                        });
                    }
                    break;
                case 1:
                    if (p.z > -40f) { NextStep(); hud.ShowArea("Rua do Mercado", "Campanula, Vila dos Doze Sinos"); Checkpoint(new Vector3(0, 0, -36f), 0f); }
                    break;
                case 2: break;   // espera o encontro do mercado (NextStep no onComplete)
                case 3:
                    hud.ShowObjective("Chegue à Praça dos Doze Sinos");
                    NextStep();
                    break;
                case 4:
                    if (p.z > -3f) { NextStep(); hud.ShowArea("Praça dos Doze Sinos", "onde a nota comum é tocada ao pôr do sol"); }
                    break;
                case 5: break;   // espera a praça
                case 6:
                    hud.ShowObjective("Suba na Torre dos Sinos pelo andaime da face sul");
                    hud.ShowHint("Pule perto de uma borda para agarrar · <b>W</b> sobe · <b>C</b> solta", 7f);
                    NextStep();
                    break;
                case 7:
                    if (p.y > 17f && Mathf.Abs(p.x) < 6f && p.z > 28f)
                    {
                        NextStep();
                        hud.ShowArea("Torre dos Doze Sinos", "daqui se vê a Fenda");
                        bells?.Toll(4, false);
                        Checkpoint(new Vector3(6f, 0, 22f), 90f);
                        hud.ShowObjective("Siga para o leste: atravesse a ponte até o Campo da Fenda");
                    }
                    else if (p.x > 34f)   // pulou a torre: tudo bem, segue o roteiro
                    {
                        NextStep();
                        hud.ShowObjective("Atravesse a ponte até o Campo da Fenda");
                    }
                    break;
                case 8:
                    if (p.x > 48f) { NextStep(); hud.ShowArea("Campo da Fenda", "o primeiro possuído espera"); Checkpoint(new Vector3(50f, 0, 9.5f), 90f); }
                    break;
            }
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

        void Checkpoint(Vector3 pos, float yaw) { checkpointPos = pos; checkpointYaw = yaw; ArenAudio.PlayUI(Sfx.Checkpoint, 0.4f); }

        void AudioIntensity(float v) => ArenAudio.SetIntensity(v);

        // ------------------------------------------------------------ pausa / morte / fim

        void OnEscape()
        {
            if (Current == State.Playing) Pause();
            else if (Current == State.Paused && menus.Current == GameMenus.Screen.Pause) Resume();
        }

        void Pause()
        {
            Current = State.Paused;
            GameFeel.SetPaused(true);
            SetPlayerControl(false);
            menus.Show(GameMenus.Screen.Pause);
            ArenAudio.PlayUI(Sfx.UIBack, 0.5f);
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
                "Contra-ataques  <b>" + counters + "</b>     Esquivas perfeitas  <b>" + perfectDodges + "</b>     Quedas  <b>" + deaths + "</b>");
            StartCoroutine(EndRoutine());
        }

        System.Collections.IEnumerator EndRoutine()
        {
            yield return new WaitForSecondsRealtime(1.4f);
            bells?.Toll(12, true);
            menus.Show(GameMenus.Screen.End);
            hud.SetVisible(false);
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

        /// <summary>Só para testes: pula o menu e começa num ponto do roteiro.</summary>
        public void DebugJump(int stepIndex, Vector3 pos, float yaw)
        {
            StopAllCoroutines();
            menus.Show(GameMenus.Screen.None);
            menus.FadeTo(0f, 100f);
            menuCam.gameObject.SetActive(false);
            Current = State.Playing;
            GameFlowState.InGame = true;
            hud.SetVisible(true);
            step = stepIndex;
            for (int i = 0; i < encounters.Count; i++)
                if ((encounters[i] == market && stepIndex > 2) || (encounters[i] == plaza && stepIndex > 5))
                    encounters[i].Done = true;
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
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
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

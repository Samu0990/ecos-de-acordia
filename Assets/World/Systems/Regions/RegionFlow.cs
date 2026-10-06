using Aren;
using Aren.Combat;
using Aren.UI;
using Climbing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elyndra.World
{
    /// <summary>
    /// Jogo dentro de uma cena de reino ou masmorra (o equivalente leve do GameFlow de Campânula): prepara o
    /// Aren que já vem na cena (Player.prefab), coloca-o no portão de chegada / ponto de retorno, cria o
    /// HUD e os menus góticos, pausa (Esc/Start), morte e volta ao último ponto, mapa de Elyndra (M) e o
    /// nome do lugar ao chegar. Não substitui o GameFlow: Campânula continua com o roteiro dela.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class RegionFlow : MonoBehaviour
    {
        public static RegionFlow Instance { get; private set; }
        public RegionRoot root;

        ThirdPersonController tpc;
        GameObject player;
        ArenHealth health; ArenCombat combat; ArenInput arenInput; InputCharacterController dpsInput;
        ArenHUD hud; GameMenus menus;
        Vector3 checkpointPos; float checkpointYaw;
        bool paused, dead, ready; float deathTimer;

        public Transform Player => player != null ? player.transform : null;
        public bool Ready => ready;
        public ArenHUD Hud => hud;

        /// <summary>Volta do mapa do mundo: reaparece onde estava.</summary>
        public static string ResumeScene; public static Vector3 ResumePos; public static float ResumeYaw;

        void Awake()
        {
            Instance = this;
            GameSettings.Load();
            ArenAudio.Preload();
            hud = new GameObject("HUD").AddComponent<ArenHUD>();
            menus = new GameObject("Menus").AddComponent<GameMenus>();
            menus.onResume = Resume;
            menus.onStart = Resume;
            menus.onRestartCheckpoint = () => { Resume(); Respawn(); };
            menus.onMainMenu = () => { GameFeel.SetPaused(false); RegionTravel.Go(WorldCanon.CampanulaScene, "", "CAMPÂNULA", "menu principal"); };
            menus.onPlayAgain = menus.onMainMenu;
            menus.onQuit = () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
        }

        void OnDestroy() { GameFeel.SetPaused(false); GameFlowState.InGame = false; WorldState.Save(); }

        System.Collections.IEnumerator Start()
        {
            if (root == null) root = FindAnyObjectByType<RegionRoot>();
            // segurança: uma tela de carregamento de Campânula que sobrou (cena trocada no meio da carga) não fica por cima
            if (Aren.UI.LoadingScreen.Instance != null) Aren.UI.LoadingScreen.Instance.Hide(0.3f);
            yield return null;   // Start() do DPS (o Rigidbody do movimento nasce lá)
            yield return null;
            try { SetupPlayer(); } catch (System.Exception e) { Debug.LogException(e); }
            PlaceAtArrival();
            GameFlowState.InGame = true;
            ArenAudio.SetMenuMusic(false);
            menus.Show(GameMenus.Screen.None);
            menus.FadeTo(0f, 1.4f);
            hud.SetVisible(true);
            SetPlayerControl(true);
            if (root != null)
            {
                WorldState.Discover(root.region);
                var def = root.Def;
                if (root.isDungeon) { var dd = WorldCanon.Dungeon(root.dungeonId); if (dd != null) hud.ShowArea(dd.name, def.name + " · " + dd.theme); }
                else if (def != null) hud.ShowArea(def.name, def.epithet);
            }
            ready = true;
        }

        void SetupPlayer()
        {
            tpc = FindAnyObjectByType<ThirdPersonController>();
            if (tpc == null) { Debug.LogError("[Reino] cena sem jogador (Player.prefab)"); return; }
            player = tpc.gameObject;
            health = player.GetComponent<ArenHealth>();
            combat = player.GetComponent<ArenCombat>();
            arenInput = player.GetComponent<ArenInput>();
            dpsInput = player.GetComponent<InputCharacterController>();
            var cam = Camera.main;
            if (cam != null)
            {
                var d = new float[32]; d[11] = 70f; d[12] = 190f;
                cam.layerCullDistances = d; cam.layerCullSpherical = true; cam.useOcclusionCulling = true;
                if (cam.GetComponent<Aren.World.RenderScaler>() == null) cam.gameObject.AddComponent<Aren.World.RenderScaler>();
            }
            var freeLook = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (freeLook != null)
            {
                ArenThirdPersonSetup.Apply(tpc, freeLook);
                var cc = freeLook.GetComponent<ArenCombatCamera>();
                if (cc == null) cc = freeLook.gameObject.AddComponent<ArenCombatCamera>();
                cc.Bind(player.transform, combat);
            }
            hud.Bind(player);
            if (player.GetComponent<BlobShadow>() == null) player.AddComponent<BlobShadow>().radius = 0.5f;
            if (player.GetComponent<ArenFootsteps>() == null) player.AddComponent<ArenFootsteps>();
            if (player.GetComponent<ArenAnimationEvents>() == null) player.AddComponent<ArenAnimationEvents>();
            if (player.GetComponent<ArenFlutePerformancePose>() == null) player.AddComponent<ArenFlutePerformancePose>();
            if (health != null) health.OnDied += OnDied;
            combat.OnPerfectDodge += () => { Aren.World.Notas.Add(25); hud.Toast("Esquiva perfeita", UIKit.Cyan); };
            combat.OnCounter += n => { Aren.World.Notas.Add(50 * Mathf.Max(1, n)); hud.Toast(n > 1 ? "Contra-ataque ×" + n : "Contra-ataque", UIKit.Gold); };
        }

        void PlaceAtArrival()
        {
            if (player == null) return;
            Transform at = null;
            string scene = gameObject.scene.name;
            if (RegionTravel.FromScene == WorldCanon.WorldMapScene && ResumeScene == scene)
            {
                Teleport(ResumePos, ResumeYaw);
                SetCheckpoint(ResumePos, ResumeYaw, "");
                return;
            }
            if (!string.IsNullOrEmpty(RegionTravel.ArrivalGate))
                foreach (var g in FindObjectsByType<RegionGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (g.id == RegionTravel.ArrivalGate) { at = g.arrival != null ? g.arrival : g.transform; break; }
            if (at == null && WorldState.LastScene == scene && !string.IsNullOrEmpty(WorldState.LastCheckpoint))
                foreach (var c in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                    if (c.id == WorldState.LastCheckpoint) { at = c.spawn != null ? c.spawn : c.transform; break; }
            if (at == null && root != null) at = root.defaultSpawn;
            if (at == null) return;
            Teleport(at.position, at.eulerAngles.y);
            SetCheckpoint(at.position, at.eulerAngles.y, "");
        }

        /// <summary>Coloca o Aren no chão mais próximo de pos (raio para baixo nos colisores).</summary>
        public void Teleport(Vector3 pos, float yaw)
        {
            if (player == null) return;
            if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var hit, 200f, ~((1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore)) pos.y = hit.point.y + 0.05f;
            var rb = player.GetComponent<Rigidbody>();
            var jp = player.GetComponent<JumpPredictionController>();
            if (jp != null) jp.curPoint = null;
            tpc.isVaulting = false; tpc.isJumping = false; tpc.onAir = false;
            tpc.EnableController();
            var rot = Quaternion.Euler(0, yaw, 0);
            if (rb != null) { rb.position = pos; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
            player.transform.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
            var anim = player.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetBool("Land", false); anim.SetBool("onAir", false); anim.SetBool("Hanging", false);
                anim.SetInteger("Climb State", 0); anim.Play("Idle", 0, 0f);
            }
            var fl = FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (fl != null) { fl.m_XAxis.Value = yaw; fl.m_YAxis.Value = 0.45f; fl.PreviousStateIsValid = false; }
        }

        public void SetCheckpoint(Vector3 pos, float yaw, string id)
        {
            checkpointPos = pos; checkpointYaw = yaw;
            if (!string.IsNullOrEmpty(id)) WorldState.SetCheckpoint(gameObject.scene.name, id);
        }

        void SetPlayerControl(bool on)
        {
            if (arenInput != null) arenInput.SetCombatInputEnabled(on);
            if (dpsInput != null) { dpsInput.enabled = on; dpsInput.movement = Vector2.zero; dpsInput.run = false; dpsInput.jump = false; }
            if (!on && combat != null) combat.ForceFree();
            if (!GameSettings.NoCursorLock) Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void Update()
        {
            if (!ready) return;
            bool esc = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (esc && !dead && !RegionTravel.Busy)
            {
                if (!paused) Pause();
                else if (menus.Current == GameMenus.Screen.Pause && !menus.BackHandledThisFrame) Resume();
            }
            if (!paused && !dead && !RegionTravel.Busy && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) OpenWorldMap();
            if (dead)
            {
                deathTimer += Time.unscaledDeltaTime;
                if (deathTimer > 2.6f) menus.FadeTo(1f, 3f);
                if (deathTimer > 3.2f) { dead = false; Respawn(); menus.FadeTo(0f, 1.5f); menus.Show(GameMenus.Screen.None); }
            }
            // caiu do mundo (penhasco sem fundo): volta ao último ponto
            if (player != null && player.transform.position.y < -120f && !dead) Respawn();
        }

        void Pause()
        {
            paused = true;
            GameFeel.SetPaused(true);
            SetPlayerControl(false);
            menus.Show(GameMenus.Screen.Pause);
            ArenAudio.PlayUI(Sfx.UIPage, 0.6f);
        }

        void Resume()
        {
            if (!paused) return;
            paused = false;
            GameFeel.SetPaused(false);
            menus.Show(GameMenus.Screen.None);
            SetPlayerControl(true);
        }

        public void PauseFromTest() => Pause();

        void OnDied()
        {
            dead = true; deathTimer = 0f;
            SetPlayerControl(false);
            GameFeel.SlowMo(1.2f, 0.3f);
            ArenAudio.PlaySting(Sting.Defeat);
            menus.Show(GameMenus.Screen.Death);
        }

        public void Respawn()
        {
            EnemySpawnZone.ResetAllNear(checkpointPos, 9999f);
            Teleport(checkpointPos, checkpointYaw);
            if (health != null) health.Revive(1f);
            if (combat != null) combat.PlayGetUp();
            SetPlayerControl(true);
        }

        public void OpenWorldMap()
        {
            if (player == null || !RegionTravel.CanLoad(WorldCanon.WorldMapScene)) return;
            ResumeScene = gameObject.scene.name; ResumePos = player.transform.position; ResumeYaw = player.transform.eulerAngles.y;
            RegionTravel.Go(WorldCanon.WorldMapScene, "", "ELYNDRA", "o mapa do continente");
        }
    }
}

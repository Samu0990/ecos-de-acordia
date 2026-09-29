// Parkour Lab HUD - painel de depuracao que aparece sozinho ao dar Play.
// Mostra velocidade, estado de movimento, escalada e a animacao atual.
// F1 liga/desliga o painel, F2 liga/desliga os gizmos de debug (trajetória
// de salto previsto). Nao precisa arrastar nada para a cena.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Climbing
{
    public class ParkourLabHUD : MonoBehaviour
    {
        ThirdPersonController player;
        ClimbController climb;
        JumpPredictionController jumpPrediction;
        Animator animator;
        bool visible = true;
        bool debugGizmos = false;
        GUIStyle box, label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<ParkourLabHUD>() != null) return;
            var go = new GameObject("[Parkour Lab HUD]");
            go.AddComponent<ParkourLabHUD>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;

            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                debugGizmos = !debugGizmos;
                if (climb != null) climb.debug = debugGizmos;
                if (jumpPrediction != null) jumpPrediction.showDebug = debugGizmos;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<ThirdPersonController>();
                if (player != null)
                {
                    climb = player.GetComponent<ClimbController>();
                    jumpPrediction = player.GetComponent<JumpPredictionController>();
                    animator = player.GetComponent<Animator>();
                    if (animator == null) animator = player.GetComponentInChildren<Animator>();
                }
            }
        }

        void OnGUI()
        {
            if (!visible) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box);
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            }

            GUILayout.BeginArea(new Rect(12, 12, 330, 380), box);
            GUILayout.Label($"<b>PARKOUR LAB</b>   (F1 esconde, F2 gizmos: {(debugGizmos ? "<color=lime>on</color>" : "off")})", label);

            if (player == null)
            {
                GUILayout.Label("<color=orange>Nenhum jogador na cena.</color>", label);
                GUILayout.EndArea();
                return;
            }

            Rigidbody rb = player.characterMovement != null ? player.characterMovement.rb : null;
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            float horizontal = new Vector3(v.x, 0, v.z).magnitude;

            GUILayout.Label($"Velocidade: <b>{horizontal:0.0}</b> m/s   Vertical: {v.y:0.0}", label);
            GUILayout.Label($"No chão: {YesNo(player.isGrounded)}   No ar: {YesNo(player.onAir)}", label);
            //isVaulting cobre qualquer ação do VaultingController, incluindo escalar (Climb_Ledge é uma
            //VaultAction). "Escalada" abaixo já mostra esse caso com mais detalhe, então aqui mostramos
            //Vault só quando NÃO é escalada, pra não duplicar/confundir o mesmo estado com dois nomes.
            bool trueVaulting = player.isVaulting && (climb == null || climb.CurrentClimbState == ClimbController.ClimbState.None);
            GUILayout.Label($"Pulando: {YesNo(player.isJumping)}   Vault: {YesNo(trueVaulting)}", label);
            GUILayout.Label($"Rampa: {YesNo(player.inSlope)}", label);

            string climbState = climb != null ? ClimbName(climb.CurrentClimbState) : "—";
            GUILayout.Label($"Escalada: <b>{climbState}</b>", label);

            string anim = "—";
            if (animator != null)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0 && clips[0].clip != null) anim = clips[0].clip.name;
            }
            GUILayout.Label($"Animação: <b>{anim}</b>", label);

            string predicted = "—";
            if (jumpPrediction != null && jumpPrediction.curPoint != null)
                predicted = jumpPrediction.curPoint.type.ToString();
            GUILayout.Label($"Ação prevista: <b>{predicted}</b>", label);

            //Remake Aren (Fase 2): pulo livre, pouso por intensidade, derrapada, inclinação
            var jump = player.GetComponent<Aren.ArenJump>();
            if (jump != null)
            {
                string[] tiers = { "leve", "médio", "pesado" };
                string land = jump.LastLandTier >= 0 ? $"{tiers[jump.LastLandTier]} ({jump.LastLandSpeed:0.0} m/s)" : "—";
                GUILayout.Label($"Pulo livre: {YesNo(jump.IsFreeJumping)}  altura: {jump.MaxHeightThisJump:0.00} m", label);
                GUILayout.Label($"Último pouso: <b>{land}</b>", label);
            }
            var pivot = player.GetComponent<Aren.ArenPivot>();
            var lean = player.GetComponent<Aren.ArenLean>();
            if (pivot != null && lean != null)
                GUILayout.Label($"Derrapada: {YesNo(pivot.IsSkidding)}  Inclinação: {lean.CurrentLean.x:0}°/{lean.CurrentLean.y:0}°", label);

            GUILayout.Label($"FPS: {(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)):0}", label);
            GUILayout.EndArea();
        }

        static string YesNo(bool b) => b ? "<color=lime>sim</color>" : "<color=grey>não</color>";

        static string ClimbName(ClimbController.ClimbState s)
        {
            switch (s)
            {
                case ClimbController.ClimbState.BHanging: return "Pendurado (pés na parede)";
                case ClimbController.ClimbState.FHanging: return "Pendurado (pés soltos)";
                default: return "Não";
            }
        }
    }
}

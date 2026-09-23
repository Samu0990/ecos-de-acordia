// Parkour Lab HUD - painel de depuracao que aparece sozinho ao dar Play.
// Mostra velocidade, estado de movimento, escalada e a animacao atual.
// F1 liga/desliga o painel. Nao precisa arrastar nada para a cena.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Climbing
{
    public class ParkourLabHUD : MonoBehaviour
    {
        ThirdPersonController player;
        ClimbController climb;
        Animator animator;
        bool visible = true;
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

            if (player == null)
            {
                player = FindAnyObjectByType<ThirdPersonController>();
                if (player != null)
                {
                    climb = player.GetComponent<ClimbController>();
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

            GUILayout.BeginArea(new Rect(12, 12, 330, 260), box);
            GUILayout.Label("<b>PARKOUR LAB</b>   (F1 esconde)", label);

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
            GUILayout.Label($"Pulando: {YesNo(player.isJumping)}   Vault: {YesNo(player.isVaulting)}", label);
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

using Cinemachine;
using Climbing;
using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Mantem os fundamentos da camera em terceira pessoa configurados mesmo se o prefab
    /// for reimportado: seguir o jogador, mirar na altura dos ombros e orbitar em WorldSpace.
    /// As orbitas e sensibilidades artisticas do projeto sao preservadas.
    /// </summary>
    public static class ArenThirdPersonSetup
    {
        const string FocusName = "Focus";

        public static void Apply(ThirdPersonController controller, CinemachineFreeLook freeLook)
        {
            if (controller == null || freeLook == null) return;

            freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            freeLook.m_RecenterToTargetHeading.m_enabled = false;
            if (freeLook.Follow == null) freeLook.Follow = controller.transform;
            if (freeLook.LookAt == null) freeLook.LookAt = EnsureShoulderFocus(controller);
            Tune(freeLook);
        }

        /// <summary>
        /// Câmera do jogador (2026-10-02). Antes: FOV 40, órbita de baixo a 1.58 m e colisor com
        /// raio 0.69 m que podia puxar a câmera até 0.3 m do ombro — em lugar apertado (mercado,
        /// escalada da torre) ela entrava no corpo e o jogador "sumia". Agora: órbitas equilibradas,
        /// FOV 50, leve ombro à direita, colisor fino com distância mínima de 1.1 m e menos atraso.
        /// </summary>
        public static void Tune(CinemachineFreeLook freeLook)
        {
            freeLook.m_Orbits[0] = new CinemachineFreeLook.Orbit(3.9f, 2.4f);   // de cima
            freeLook.m_Orbits[1] = new CinemachineFreeLook.Orbit(1.9f, 4.4f);   // meio (padrão)
            freeLook.m_Orbits[2] = new CinemachineFreeLook.Orbit(0.35f, 3.0f);  // de baixo, olhando para cima
            freeLook.m_SplineCurvature = 0.35f;
            freeLook.m_Lens.FieldOfView = 50f;
            freeLook.m_Lens.NearClipPlane = 0.08f;
            freeLook.m_YAxis.Value = Mathf.Clamp(freeLook.m_YAxis.Value, 0.35f, 0.65f);
            for (int i = 0; i < 3; i++)
            {
                var rig = freeLook.GetRig(i);
                var body = rig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                if (body != null) { body.m_XDamping = 0.35f; body.m_YDamping = 0.45f; body.m_ZDamping = 0.35f; }
                var aim = rig.GetCinemachineComponent<CinemachineComposer>();
                if (aim != null)
                {
                    aim.m_HorizontalDamping = 0.25f; aim.m_VerticalDamping = 0.25f;
                    aim.m_ScreenY = i == 0 ? 0.48f : i == 1 ? 0.55f : 0.62f;
                    aim.m_DeadZoneWidth = 0.04f; aim.m_DeadZoneHeight = 0.04f;
                }
            }
            var col = freeLook.GetComponent<CinemachineCollider>();
            if (col != null)
            {
                col.m_AvoidObstacles = true;
                col.m_CameraRadius = 0.22f;
                col.m_MinimumDistanceFromTarget = 1.1f;
                col.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
                col.m_Damping = 0.55f;
                col.m_DampingWhenOccluded = 0.12f;
                col.m_SmoothingTime = 0.08f;
                col.m_IgnoreTag = "Player";
                // as bordas de escalada (layer Ledge) são invisíveis: a câmera não deve se encostar nelas
                // (as sacadas e anexos das casas góticas trazem várias, saindo da fachada)
                col.m_CollideAgainst &= ~(1 << 8);
            }
            var cc = freeLook.GetComponent<CameraController>();
            if (cc != null)
            {
                cc.baseFOV = 50f; cc.runFOV = 62f; // FOV shift agressivo para sensação de corrida
                cc._default = new Vector3(0.32f, 0.05f, 0f);   // por cima do ombro direito
            }
            var off = freeLook.GetComponent<CinemachineCameraOffset>();
            if (off != null) off.m_Offset = new Vector3(0.32f, 0.05f, 0f);
        }

        static Transform EnsureShoulderFocus(ThirdPersonController controller)
        {
            Transform focus = controller.transform.Find(FocusName);
            if (focus != null) return focus;

            var go = new GameObject(FocusName);
            focus = go.transform;
            focus.SetParent(controller.transform, false);
            float height = 1.45f;
            var capsule = controller.normalCapsuleCollider;
            if (capsule != null)
                height = Mathf.Clamp(capsule.center.y + capsule.height * 0.34f, 1.25f, 1.65f);
            focus.localPosition = new Vector3(0.018f, height, 0.07f);
            return focus;
        }

        public static bool Audit(ThirdPersonController controller, CinemachineFreeLook freeLook, out string report)
        {
            bool capsule = controller != null && controller.normalCapsuleCollider != null
                && controller.normalCapsuleCollider.height > controller.normalCapsuleCollider.radius * 2f;
            var animator = controller != null ? controller.GetComponent<Animator>() : null;
            bool animation = animator != null && animator.runtimeAnimatorController != null;
            bool camera = freeLook != null && freeLook.Follow != null && freeLook.LookAt != null
                && freeLook.m_BindingMode == CinemachineTransposer.BindingMode.WorldSpace
                && freeLook.m_Orbits != null && freeLook.m_Orbits.Length == 3;
            bool collision = freeLook != null && freeLook.GetComponent<CinemachineCollider>() != null;

            report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "TERCEIRA_PESSOA: {0} camera={1} follow={2} lookAt={3} worldSpace={4} orbitas={5} colisao={6} capsula={7} animator={8}",
                camera && collision && capsule && animation ? "OK" : "FALHA",
                freeLook != null ? freeLook.name : "ausente",
                freeLook != null && freeLook.Follow != null ? freeLook.Follow.name : "ausente",
                freeLook != null && freeLook.LookAt != null ? freeLook.LookAt.name : "ausente",
                freeLook != null && freeLook.m_BindingMode == CinemachineTransposer.BindingMode.WorldSpace,
                freeLook != null && freeLook.m_Orbits != null ? freeLook.m_Orbits.Length : 0,
                collision, capsule, animation);
            return camera && collision && capsule && animation;
        }
    }
}

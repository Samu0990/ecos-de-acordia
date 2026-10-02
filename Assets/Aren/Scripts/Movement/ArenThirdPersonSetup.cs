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

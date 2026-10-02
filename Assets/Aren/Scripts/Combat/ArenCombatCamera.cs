using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aren.Combat
{
    /// <summary>
    /// Enquadramento de combate para o FreeLook do DPS. Enquanto há um alvo válido, mira no
    /// espaço entre Aren e o inimigo. O colisor nativo da Cinemachine mantém a câmera fora de
    /// paredes e props sem acrescentar consultas físicas extras a cada quadro de combate.
    /// O jogador ainda pode girar a câmera livremente; o reenquadramento só volta depois de um
    /// breve período sem entrada de mouse/analógico direito.
    /// </summary>
    [DefaultExecutionOrder(140)]
    [DisallowMultipleComponent]
    public sealed class ArenCombatCamera : MonoBehaviour
    {
        const int DetailLayer = 11;
        [Header("Combate")]
        [Range(1f, 1.5f)] public float combatRadiusMultiplier = 1.18f;
        [Range(0.1f, 1f)] public float combatCameraRadius = 0.32f;
        [Range(40f, 65f)] public float combatFov = 50f;
        [Range(0.1f, 2f)] public float recenterDelay = 0.95f;
        [Range(45f, 360f)] public float recenterSpeed = 155f;
        [Range(0.15f, 0.4f)] public float horizontalSafeZone = 0.24f;

        CinemachineFreeLook freeLook;
        CinemachineCollider cinemachineCollider;
        ArenCombat combat;
        Transform player;
        Transform restingLookAt;
        Transform combatLookAt;

        readonly float[] baseRadii = new float[3];
        float baseCameraRadius, baseDamping, baseDampingOccluded, baseSmoothing, baseFov;
        LayerMask baseCollisionMask;
        CinemachineCollider.ResolutionStrategy baseStrategy;
        float blend, blendVelocity, wantedYaw, lastManualInput;
        Vector3 focusVelocity;

        public void Bind(Transform aren, ArenCombat arenCombat)
        {
            player = aren;
            combat = arenCombat;
        }

        void Awake()
        {
            freeLook = GetComponent<CinemachineFreeLook>();
            cinemachineCollider = GetComponent<CinemachineCollider>();
            if (freeLook == null) { enabled = false; return; }

            restingLookAt = freeLook.m_LookAt;
            baseFov = freeLook.m_Lens.FieldOfView;
            for (int i = 0; i < freeLook.m_Orbits.Length && i < baseRadii.Length; i++)
                baseRadii[i] = freeLook.m_Orbits[i].m_Radius;

            if (cinemachineCollider != null)
            {
                baseCollisionMask = cinemachineCollider.m_CollideAgainst;
                baseCameraRadius = cinemachineCollider.m_CameraRadius;
                baseDamping = cinemachineCollider.m_Damping;
                baseDampingOccluded = cinemachineCollider.m_DampingWhenOccluded;
                baseSmoothing = cinemachineCollider.m_SmoothingTime;
                baseStrategy = cinemachineCollider.m_Strategy;
            }
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<Climbing.ThirdPersonController>()?.transform;
            if (combat == null && player != null) combat = player.GetComponent<ArenCombat>();
            CreateFocus();
        }

        void CreateFocus()
        {
            if (combatLookAt != null) return;
            var go = new GameObject("CombatCameraFocus") { hideFlags = HideFlags.DontSave };
            combatLookAt = go.transform;
            combatLookAt.position = restingLookAt != null ? restingLookAt.position : transform.position;
        }

        void Update()
        {
            if (player == null || combat == null)
            {
                player ??= FindAnyObjectByType<Climbing.ThirdPersonController>()?.transform;
                combat ??= player != null ? player.GetComponent<ArenCombat>() : null;
                if (player == null || combat == null) return;
            }

            bool active = CombatRegistry.IsValid(combat.Target) && (combat.Busy || combat.InCombatRecently);
            float targetBlend = active ? 1f : 0f;
            blend = Mathf.SmoothDamp(blend, targetBlend, ref blendVelocity, active ? 0.12f : 0.28f, Mathf.Infinity, Time.unscaledDeltaTime);

            UpdateLookAt(active);
            ApplyCameraSettings();

            if (HadManualCameraInput()) lastManualInput = Time.unscaledTime;
            bool canRecenter = active && Time.unscaledTime - lastManualInput >= recenterDelay
                && TargetOutsideSafeZone(combat.Target);
            if (canRecenter)
                wantedYaw = TargetYaw(combat.Target);

            if (canRecenter)
            {
                float current = freeLook.m_XAxis.Value;
                float step = recenterSpeed * Time.unscaledDeltaTime;
                freeLook.m_XAxis.Value = current + Mathf.Clamp(Mathf.DeltaAngle(current, wantedYaw), -step, step);
            }
        }

        void UpdateLookAt(bool active)
        {
            CreateFocus();
            Vector3 playerAim = player.position + Vector3.up * 1.15f;
            Vector3 desired = restingLookAt != null ? restingLookAt.position : playerAim;
            if (active && CombatRegistry.IsValid(combat.Target))
            {
                Vector3 targetAim = combat.Target.AimPoint;
                float distance = Vector3.Distance(playerAim, targetAim);
                // Com inimigos distantes desloca um pouco mais o ponto de mira para que ambos caibam.
                desired = Vector3.Lerp(playerAim, targetAim, Mathf.Clamp01(0.42f + distance * 0.02f));
            }
            combatLookAt.position = Vector3.SmoothDamp(combatLookAt.position, desired, ref focusVelocity,
                active ? 0.075f : 0.18f, Mathf.Infinity, Time.unscaledDeltaTime);

            if (blend > 0.01f) freeLook.m_LookAt = combatLookAt;
            else if (restingLookAt != null) freeLook.m_LookAt = restingLookAt;
        }

        void ApplyCameraSettings()
        {
            for (int i = 0; i < freeLook.m_Orbits.Length && i < baseRadii.Length; i++)
                freeLook.m_Orbits[i].m_Radius = Mathf.Lerp(baseRadii[i], baseRadii[i] * combatRadiusMultiplier, blend);
            // CameraController ainda cuida do FOV da corrida; no combate preservamos o maior dos dois.
            // A lente um pouco mais aberta evita que um golpe lateral corte o alvo na borda.
            freeLook.m_Lens.FieldOfView = Mathf.Max(freeLook.m_Lens.FieldOfView, Mathf.Lerp(baseFov, combatFov, blend));

            if (cinemachineCollider == null) return;
            if (blend <= 0.001f)
            {
                cinemachineCollider.m_CollideAgainst = baseCollisionMask;
                cinemachineCollider.m_CameraRadius = baseCameraRadius;
                cinemachineCollider.m_Damping = baseDamping;
                cinemachineCollider.m_DampingWhenOccluded = baseDampingOccluded;
                cinemachineCollider.m_SmoothingTime = baseSmoothing;
                cinemachineCollider.m_Strategy = baseStrategy;
                return;
            }

            // Props do mercado ficam em Detail. No combate entram no raycast para não cobrir Aren.
            cinemachineCollider.m_CollideAgainst = baseCollisionMask | (1 << DetailLayer);
            cinemachineCollider.m_CameraRadius = Mathf.Lerp(baseCameraRadius, combatCameraRadius, blend);
            cinemachineCollider.m_Damping = Mathf.Lerp(baseDamping, 0.38f, blend);
            cinemachineCollider.m_DampingWhenOccluded = Mathf.Lerp(baseDampingOccluded, 0.05f, blend);
            cinemachineCollider.m_SmoothingTime = Mathf.Lerp(baseSmoothing, 0.06f, blend);
            cinemachineCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
        }

        bool HadManualCameraInput()
        {
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.25f) return true;
            return Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.01f;
        }

        float TargetYaw(IDamageable target)
        {
            Vector3 toTarget = target.transform.position - player.position;
            toTarget.y = 0f;
            float targetYaw = toTarget.sqrMagnitude > 0.02f
                ? Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg
                : freeLook.m_XAxis.Value;
            return targetYaw;
        }

        bool TargetOutsideSafeZone(IDamageable target)
        {
            Camera cam = Camera.main;
            if (cam == null || !CombatRegistry.IsValid(target)) return true;
            Vector3 t = cam.WorldToViewportPoint(target.AimPoint);
            Vector3 p = cam.WorldToViewportPoint(player.position + Vector3.up * 1.15f);
            if (t.z <= 0f || p.z <= 0f) return true;
            float min = horizontalSafeZone;
            float max = 1f - horizontalSafeZone;
            return t.x < min || t.x > max || p.x < 0.08f || p.x > 0.92f;
        }

        void OnDestroy()
        {
            if (freeLook != null)
            {
                if (restingLookAt != null) freeLook.m_LookAt = restingLookAt;
                for (int i = 0; i < freeLook.m_Orbits.Length && i < baseRadii.Length; i++)
                    freeLook.m_Orbits[i].m_Radius = baseRadii[i];
            }
            if (cinemachineCollider != null)
            {
                cinemachineCollider.m_CollideAgainst = baseCollisionMask;
                cinemachineCollider.m_CameraRadius = baseCameraRadius;
                cinemachineCollider.m_Damping = baseDamping;
                cinemachineCollider.m_DampingWhenOccluded = baseDampingOccluded;
                cinemachineCollider.m_SmoothingTime = baseSmoothing;
                cinemachineCollider.m_Strategy = baseStrategy;
            }
            if (combatLookAt != null) Destroy(combatLookAt.gameObject);
        }
    }
}

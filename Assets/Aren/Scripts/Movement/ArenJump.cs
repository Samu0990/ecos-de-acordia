using UnityEngine;
using Climbing;

namespace Aren
{
    /// <summary>
    /// Pulo livre do Aren + pouso por intensidade.
    ///
    /// Prioridade: as ações contextuais do DPS (vault, agarrar borda, pulo previsto em
    /// poste) rodam antes no mesmo frame (este script executa depois, order 100). Se
    /// alguma delas consumiu o Espaço (isVaulting/isJumping), o pulo livre não acontece —
    /// o parkour existente tem prioridade e não muda de comportamento.
    ///
    /// Física: v0 = sqrt(2 g h) (subida com gravidade normal, queda com o fallForce do DPS),
    /// altura variável ao soltar cedo, apex assist (gravidade reduzida perto do topo).
    /// Valores medidos no log (AREN_REMAKE_EXECUTION_LOG.md, Fase 2).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ArenJump : MonoBehaviour
    {
        [Header("Arco")]
        [Tooltip("Altura do pulo com o botão segurado (m).")]
        public float jumpHeight = 1.0f;
        [Tooltip("Multiplica a velocidade vertical ao soltar o botão subindo (pulo curto).")]
        [Range(0.2f, 1f)] public float shortHopCut = 0.5f;
        [Tooltip("Altura mínima garantida mesmo com toque rápido (m).")]
        public float minHopHeight = 0.45f;
        [Tooltip("Faixa de |v.y| considerada 'apex' (m/s).")]
        public float apexBand = 1.2f;
        [Tooltip("Escala de gravidade dentro da faixa do apex (1 = sem assistência).")]
        [Range(0.2f, 1f)] public float apexGravityScale = 0.55f;

        [Header("Fast fall (segurar C no ar)")]
        [Tooltip("Gravidade extra ao segurar C no ar descendo (multiplica g).")]
        public float fastFallExtraGravity = 1.6f;
        [Tooltip("Velocidade máxima de queda com fast fall (m/s).")]
        public float fastFallMaxSpeed = 22f;

        [Header("Forgiveness")]
        public float coyoteTime = 0.1f;
        public float bufferTime = 0.15f;

        [Header("Pouso (velocidade de impacto, m/s)")]
        [Tooltip("Abaixo disso: pouso leve (volta direto à locomoção com afundamento procedural).")]
        public float mediumLandSpeed = 8f;
        [Tooltip("Acima disso: pouso pesado (agachamento fundo + recuperação curta).")]
        public float heavyLandSpeed = 12.5f;
        public float heavyRecoveryTime = 0.38f;
        [Tooltip("Afundamento máximo do quadril no pouso leve (m).")]
        public float softDipDepth = 0.09f;

        [Header("Antecipação de ações contextuais")]
        [Tooltip("Segundos de corrida olhados à frente. Se houver obstáculo de parkour nesse alcance, " +
                 "o Espaço é guardado para o vault/agarrão (buffer contextual) em vez de virar pulo livre.")]
        public float anticipationTime = 0.5f;
        public float anticipationBaseRange = 0.5f;
        public string[] contextualTags = { "Vault", "Deep Jump", "Reach" };
        public LayerMask climbableLayers = 0;   // 0 = usa "Wall" + "Ledge"

        [Header("Animação")]
        public string jumpState = "Aren Jump";
        [Range(0, 0.5f)] public float jumpStateStartOffset = 0.06f;

        public bool IsFreeJumping { get; private set; }
        public bool IsFastFalling { get; private set; }
        public int LastLandTier { get; private set; } = -1;
        public float LastLandSpeed { get; private set; }
        public float LastJumpTime { get; private set; } = -10f;
        public float MaxHeightThisJump { get; private set; }
        public event System.Action<Vector3> OnJump;      // velocidade inicial (áudio/VFX)
        public event System.Action<int, float> OnLand;   // tier (0 leve,1 médio,2 pesado), velocidade

        ThirdPersonController tpc;
        InputCharacterController input;
        MovementCharacterController move;
        ClimbController climb;
        Animator anim;
        Rigidbody rb;
        bool hasGroundedParameter;
        static readonly int GroundedHash = Animator.StringToHash("Grounded");

        float handledPress = -10f;
        bool cutApplied;
        float minVy;
        float jumpStartY;
        float recoveryUntil = -10f;
        float dip, dipVel;

        void Awake()
        {
            tpc = GetComponent<ThirdPersonController>();
            input = GetComponent<InputCharacterController>();
            move = GetComponent<MovementCharacterController>();
            climb = GetComponent<ClimbController>();
            anim = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            if (anim != null)
                foreach (var p in anim.parameters) if (p.nameHash == GroundedHash) { hasGroundedParameter = true; break; }
        }

        void Start()
        {
            move.OnLanded += HandleLanded;
        }

        void OnDestroy()
        {
            if (move != null) move.OnLanded -= HandleLanded;
        }

        void Update()
        {
            float press = input.LastJumpPressedTime;
            if (press > handledPress)
            {
                if (Time.time - press > bufferTime)
                    handledPress = press;               // expirou sem poder pular
                else if (CanJump())
                {
                    handledPress = press;
                    DoJump();
                }
            }

            if (IsFreeJumping)
            {
                // Altura variável: soltou subindo -> corta (respeitando altura mínima).
                if (!cutApplied && !input.jump && rb.linearVelocity.y > 0f)
                {
                    cutApplied = true;
                    float risen = transform.position.y - jumpStartY;
                    float g = -Physics.gravity.y;
                    float vMin = Mathf.Sqrt(Mathf.Max(0f, 2f * g * (minHopHeight - risen)));
                    var v = rb.linearVelocity;
                    v.y = Mathf.Max(v.y * shortHopCut, vMin);
                    rb.linearVelocity = v;
                }
                MaxHeightThisJump = Mathf.Max(MaxHeightThisJump, transform.position.y - jumpStartY);

                // Segurança: pulinho que nunca saiu do raio de chão do DPS (0.4 m) não pode
                // deixar isJumping preso em true.
                if (!tpc.onAir && tpc.isGrounded && Time.time - LastJumpTime > 0.2f && rb.linearVelocity.y <= 0.05f)
                    move.Landed();
            }

            // Queda genérica: o DPS só trata queda quando ele mesmo pôs isJumping=true
            // (nunca deixa cair sem querer). Qualquer outra saída do chão (empurrão,
            // beirada, teleporte) ficaria sem animação de queda nem pouso.
            if (!tpc.isGrounded && !tpc.isJumping && !tpc.dummy && !tpc.isVaulting && !rb.isKinematic
                && rb.linearVelocity.y < -1.5f
                && (climb == null || climb.CurrentClimbState == ClimbController.ClimbState.None))
            {
                tpc.isJumping = true;
                anim.SetBool("Land", false);
                anim.CrossFadeInFixedTime("Aren Fall", 0.2f, 0);
            }

            anim.SetFloat("AirVelY", rb.linearVelocity.y);
            if (hasGroundedParameter) anim.SetBool(GroundedHash, tpc.isGrounded);

            // Recuperação do pouso pesado: trava a entrada de movimento por um instante.
            if (Time.time < recoveryUntil)
            {
                tpc.allowMovement = false;
                move.SetVelocity(Vector3.zero);
            }
            else if (recoveryUntil > 0f)
            {
                recoveryUntil = -10f;
                if (!tpc.dummy) tpc.allowMovement = true;
            }
        }

        void FixedUpdate()
        {
            if (!tpc.isGrounded || IsFreeJumping)
                minVy = Mathf.Min(minVy, rb.linearVelocity.y);

            // Fast fall: C no ar (C só é usado no chão/pendurado no DPS). Só descendo,
            // e não cancela o apex assist na subida.
            bool airborne = !tpc.isGrounded && tpc.isJumping && !tpc.isVaulting && !rb.isKinematic;
            IsFastFalling = airborne && input.drop && rb.linearVelocity.y < 0.5f && rb.linearVelocity.y > -fastFallMaxSpeed;
            if (IsFastFalling)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * fastFallExtraGravity * Time.fixedDeltaTime;

            // Apex assist: devolve parte da gravidade perto do topo.
            if (IsFreeJumping && !IsFastFalling && !rb.isKinematic && Mathf.Abs(rb.linearVelocity.y) < apexBand)
                rb.linearVelocity += Vector3.up * (-Physics.gravity.y) * (1f - apexGravityScale) * Time.fixedDeltaTime;
        }

        bool CanJump()
        {
            if (tpc.dummy || !tpc.allowMovement || tpc.isVaulting || tpc.isJumping) return false;
            if (climb != null && climb.CurrentClimbState != ClimbController.ClimbState.None) return false;
            if (!(tpc.isGrounded || tpc.CoyoteAvailable(coyoteTime))) return false;
            if (Time.time < recoveryUntil) return false;
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsTag("Root") || st.IsTag("Drop")) return false;   // vault/escalada em andamento
            if (anim.IsInTransition(0))
            {
                var nx = anim.GetNextAnimatorStateInfo(0);
                if (nx.IsTag("Root") || nx.IsTag("Drop")) return false;
            }
            if (ContextualAhead()) return false;
            return true;
        }

        /// <summary>
        /// Obstáculo de parkour à frente dentro do alcance proporcional à velocidade?
        /// Se sim, o press fica no buffer contextual do DPS (vault/reach/agarrão pegam
        /// quando o obstáculo entrar no alcance curto deles) em vez de virar pulo livre —
        /// sem isso, apertar Espaço 2 m antes de uma caixa fazia o Aren pular em cima dela
        /// (medido no teste de regressão da Fase 2).
        /// </summary>
        public bool ContextualAhead()
        {
            float speed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
            float range = anticipationBaseRange + speed * anticipationTime;
            int mask = climbableLayers.value != 0 ? climbableLayers.value : LayerMask.GetMask("Wall", "Ledge");
            Vector3 fwd = transform.forward;
            for (int i = 0; i < 2; i++)
            {
                Vector3 origin = transform.position + Vector3.up * (i == 0 ? 0.5f : 1.2f);
                if (!Physics.Raycast(origin, fwd, out var hit, range, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.rigidbody == rb) continue;
                if (Vector3.Dot(-hit.normal, fwd) < 0.5f) continue;   // de raspão não conta
                foreach (var t in contextualTags)
                    if (hit.collider.CompareTag(t)) return true;
                if (((1 << hit.collider.gameObject.layer) & mask) != 0) return true;
            }
            return false;
        }

        void DoJump()
        {
            float g = -Physics.gravity.y;
            Vector3 v = rb.linearVelocity;

            // Sai na direção da intenção com a velocidade atual (o CheckBoundaries do DPS
            // freia na beirada; sem isso "correr e pular do telhado" viraria pulo vertical).
            Vector2 m = input.movement;
            if (m.magnitude > 0.3f)
            {
                Vector3 fwd = tpc.mainCamera.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = tpc.mainCamera.right; right.y = 0; right.Normalize();
                Vector3 dir = (fwd * m.y + right * m.x).normalized;
                float speed = Mathf.Max(new Vector3(v.x, 0, v.z).magnitude, move.curSpeed * 0.85f);
                v.x = dir.x * speed;
                v.z = dir.z * speed;
            }
            v.y = Mathf.Sqrt(2f * g * jumpHeight);
            rb.linearVelocity = v;

            tpc.isJumping = true;
            IsFreeJumping = true;
            cutApplied = false;
            minVy = 0f;
            jumpStartY = transform.position.y;
            MaxHeightThisJump = 0f;
            LastJumpTime = Time.time;

            // O mesmo press não pode, depois, disparar um vault/agarrão pelo buffer contextual.
            input.ConsumeJumpBuffer();

            anim.SetBool("Land", false);
            anim.SetInteger("LandType", 0);
            anim.CrossFadeInFixedTime(jumpState, 0.08f, 0, jumpStateStartOffset);
            OnJump?.Invoke(v);
        }

        void HandleLanded()
        {
            // O DPS considera "no chão" com até 0.4 m de folga (raycast 0.3+0.7), então o
            // pouso é detectado antes do toque: estima a velocidade real de impacto
            // somando a queda que ainda falta (gravidade de queda = g * fallForce).
            float speed = -minVy;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f))
            {
                float gap = Mathf.Max(0f, hit.distance - 0.3f);
                float gFall = -Physics.gravity.y * Mathf.Max(1f, move.fallForce);
                speed = Mathf.Sqrt(speed * speed + 2f * gFall * gap);
            }
            int tier = speed >= heavyLandSpeed ? 2 : speed >= mediumLandSpeed ? 1 : 0;
            LastLandTier = tier;
            LastLandSpeed = speed;
            anim.SetInteger("LandType", tier);

            if (tier == 0)
            {
                // Afundamento proporcional à velocidade (squash de pouso procedural).
                dipVel = -Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(2f, mediumLandSpeed, speed));
            }
            else if (tier == 2)
            {
                recoveryUntil = Time.time + heavyRecoveryTime;
                var v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(v.x * 0.25f, v.y, v.z * 0.25f);
                move.ResetSpeed();
            }

            IsFreeJumping = false;
            minVy = 0f;
            OnLand?.Invoke(tier, speed);
        }

        // Mola crítica-amortecida para o afundamento do quadril no pouso leve.
        void LateUpdate()
        {
            float k = 180f, c = 2f * Mathf.Sqrt(k);
            float acc = -k * dip - c * dipVel;
            dipVel += acc * Time.deltaTime;
            dip += dipVel * Time.deltaTime;
            dip = Mathf.Clamp(dip, -softDipDepth, 0.02f);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || Mathf.Abs(dip) < 0.001f || tpc.dummy) return;
            anim.bodyPosition += Vector3.up * dip;
        }
    }
}

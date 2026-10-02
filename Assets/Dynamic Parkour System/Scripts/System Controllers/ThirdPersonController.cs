/*
MIT License

Copyright (c) 2023 Èric Canela
Contact: knela96@gmail.com or @knela96 twitter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (Dynamic Parkour System), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Climbing
{
    [RequireComponent(typeof(InputCharacterController))]
    [RequireComponent(typeof(MovementCharacterController))]
    [RequireComponent(typeof(AnimationCharacterController))]
    [RequireComponent(typeof(DetectionCharacterController))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(VaultingController))]

    public class ThirdPersonController : MonoBehaviour
    {
        [HideInInspector] public InputCharacterController characterInput;
        [HideInInspector] public MovementCharacterController characterMovement;
        [HideInInspector] public AnimationCharacterController characterAnimation;
        [HideInInspector] public DetectionCharacterController characterDetection;
        [HideInInspector] public VaultingController vaultingController;
        [HideInInspector] public bool isGrounded = false;
        [HideInInspector] public bool allowMovement = true;
        [HideInInspector] public bool onAir = false;
        [HideInInspector] public bool isJumping = false;
        [HideInInspector] public bool inSlope = false;
        [HideInInspector] public bool isVaulting = false;
        [HideInInspector] public bool dummy = false;

        [Header("Cameras")]
        public CameraController cameraController;
        public Transform mainCamera;
        public Transform freeCamera;

        [Header("Step Settings")]
        [Range(0, 10.0f)] public float stepHeight = 0.8f;
        public float stepVelocity = 0.2f;

        [Header("Colliders")]
        public CapsuleCollider normalCapsuleCollider;
        public CapsuleCollider slidingCapsuleCollider;

        [Header("Rotation Feel")]
        [Tooltip("Tempo que o corpo leva para acompanhar a direcao de movimento.")]
        [Range(0.03f, 0.3f)] public float turnSmoothTime = 0.1f;
        [Tooltip("Ignora ruido muito pequeno do analogico antes de girar o personagem.")]
        [Range(0f, 0.25f)] public float rotationDeadZone = 0.08f;

        private float turnSmoothVelocity;

        private void Awake()
        {
            characterInput = GetComponent<InputCharacterController>();
            characterMovement = GetComponent<MovementCharacterController>();
            characterAnimation = GetComponent<AnimationCharacterController>();
            characterDetection = GetComponent<DetectionCharacterController>();
            vaultingController = GetComponent<VaultingController>();

            if (cameraController == null)
                Debug.LogError("Attach the Camera Controller located in the Free Look Camera");
        }

        private void Start()
        {
            characterMovement.OnLanded += characterAnimation.Land;
            characterMovement.OnFall += characterAnimation.Fall;
        }

        //Coyote time: guarda o último instante em que o personagem esteve no chão,
        //pra ações que exigem "isGrounded" ainda funcionarem por uma janela curta
        //depois de sair de uma beirada.
        private float lastGroundedTime = -10f;
        public bool CoyoteAvailable(float window = 0.1f) => Time.time - lastGroundedTime <= window;

        void Update()
        {
            //Detect if Player is on Ground
            isGrounded = OnGround();
            if (isGrounded) lastGroundedTime = Time.time;

            //Get Input if controller and movement are not disabled
            if (!dummy && allowMovement)
            {
                AddMovementInput(characterInput.movement);

                //Detects if Joystick is being pushed hard
                if (characterInput.run && characterInput.movement.magnitude > 0.5f)
                {
                    ToggleRun();
                }
                else if (!characterInput.run)
                {
                    ToggleWalk();
                }
            }
        }

        private bool OnGround()
        {
            return characterDetection.IsGrounded(stepHeight);
        }

        public void AddMovementInput(Vector2 direction)
        {
            Vector3 translation = Vector3.zero;

            translation = GroundMovement(direction);

            characterMovement.SetVelocity(Vector3.ClampMagnitude(translation, 1.0f));
        }

        Vector3 GroundMovement(Vector2 input)
        {
            // Usa os eixos planos da camera diretamente. Antes o controller escrevia a
            // rotacao do proprio FreeLook a cada quadro para usa-lo como referencia; isso
            // disputava com o Cinemachine e podia produzir pequenos trancos na camera.
            Vector3 translation = CameraRelativeDirection(input);

            //Detects if player is moving to any direction
            if (translation.sqrMagnitude > rotationDeadZone * rotationDeadZone)
            {
                RotatePlayerWorld(translation);
                characterAnimation.animator.SetBool("Released", false);
            }
            else
            {
                ToggleWalk();
                characterAnimation.animator.SetBool("Released", true);
            }

            return translation;
        }

        //Remake Aren (Fase 2): antes girava só a transform e lia o ângulo atual da própria
        //transform. Com Rigidbody interpolado (Interpolate + FreezeRotation), a
        //interpolação sobrescreve a transform todo frame e a rotação só "pegava" de vez em
        //quando - medido ao vivo: rb.rotation atualizava a cada ~0.1s, o corpo girava em
        //degraus de ~20° (~10 Hz) numa virada de 180°. Agora o ângulo tem estado próprio e
        //é aplicado também no Rigidbody (MoveRotation, interpolado entre passos).
        private float currentYaw;
        private int lastRotateFrame = -10;

        /// <summary>Gira usando uma direcao local ao jogador/camera (usado tambem nos postes).</summary>
        public void RotatePlayer(Vector3 direction)
        {
            RotatePlayerWorld(CameraRelativeDirection(new Vector2(direction.x, direction.z)));
        }

        Vector3 CameraRelativeDirection(Vector2 input)
        {
            Transform reference = mainCamera != null ? mainCamera : transform;
            Vector3 forward = reference.forward;
            Vector3 right = reference.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            else forward.Normalize();
            if (right.sqrMagnitude < 0.0001f) right = transform.right;
            else right.Normalize();
            Vector3 world = forward * input.y + right * input.x;
            world.y = 0f;
            return Vector3.ClampMagnitude(world, 1f);
        }

        void RotatePlayerWorld(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            float targetAngle = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;

            //Se alguém girou o personagem por fora (vault, escalada) ou não girava no frame
            //anterior, recomeça do ângulo real.
            if (Time.frameCount - lastRotateFrame > 1)
            {
                currentYaw = transform.eulerAngles.y;
                turnSmoothVelocity = 0f;
            }
            lastRotateFrame = Time.frameCount;

            //Rotate Mesh to Movement
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
            transform.rotation = rot;
            Rigidbody body = characterMovement != null ? characterMovement.rb : null;
            if (body != null)
            {
                if (body.isKinematic) body.rotation = rot;
                else body.MoveRotation(rot);
            }
        }
        public Quaternion RotateToCameraDirection(Vector3 direction)
        {
            Vector3 worldDirection = CameraRelativeDirection(new Vector2(direction.x, direction.z));
            if (worldDirection.sqrMagnitude < 0.0001f) return transform.rotation;
            float targetAngle = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;

            //Rotate Mesh to Movement
            return Quaternion.Euler(0f, targetAngle, 0f);
        }

        public void ResetMovement()
        {
            characterMovement.ResetSpeed();
        }

        public void ToggleRun()
        {
            if (characterMovement.GetState() != MovementState.Running)
            {
                characterMovement.SetCurrentState(MovementState.Running);
                characterMovement.curSpeed = characterMovement.RunSpeed;
                characterAnimation.animator.SetBool("Run", true);
            }
        }
        public void ToggleWalk()
        {
            if (characterMovement.GetState() != MovementState.Walking)
            {
                characterMovement.SetCurrentState(MovementState.Walking);
                characterMovement.curSpeed = characterMovement.walkSpeed;
                characterAnimation.animator.SetBool("Run", false);
            }
        }


        public float GetCurrentVelocity()
        {
            return characterMovement.GetVelocity().magnitude;
        }

        public void DisableController()
        {
            characterMovement.SetKinematic(true);
            characterMovement.enableFeetIK = false;
            dummy = true;
            allowMovement = false;
        }
        public void EnableController()
        {
            characterMovement.SetKinematic(false);
            characterMovement.EnableFeetIK();
            characterMovement.ApplyGravity();
            characterMovement.stopMotion = false;
            dummy = false; 
            allowMovement = true;
        }
    }
}

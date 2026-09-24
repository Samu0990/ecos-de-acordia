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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace Climbing
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineCameraOffset cameraOffset;
        private CinemachineFreeLook freeLook;
        private MovementCharacterController playerMovement;

        public Vector3 _offset;
        public Vector3 _default;
        private Vector3 _target;
        private Vector3 _from;

        public float maxTime = 2.0f;
        private float curTime = 0.0f;
        private bool anim = false;

        [Header("FOV dinâmico na corrida")]
        public float baseFOV = 40f;
        public float runFOV = 48f;
        public float fovLerpSpeed = 3f;

        void Start()
        {
            cameraOffset = GetComponent<CinemachineCameraOffset>();
            freeLook = GetComponent<CinemachineFreeLook>();
            playerMovement = FindAnyObjectByType<MovementCharacterController>();
            if (freeLook != null)
                baseFOV = freeLook.m_Lens.FieldOfView;
        }


        void Update()
        {
            //Lerps Camera Position to the new offset
            //Antes usava cameraOffset.m_Offset (o próprio valor já interpolado) como
            //origem a cada frame, o que produz uma curva de ease-out estranha em vez
            //de uma transição limpa. Agora guarda o valor de partida uma vez (_from,
            //setado em newOffset) e interpola dele até _target.
            if (anim)
            {
                curTime += Time.deltaTime / maxTime;
                cameraOffset.m_Offset = Vector3.Lerp(_from, _target, curTime);
            }

            if (curTime >= 1.0f)
                anim = false;

            //FOV sobe um pouco na corrida e volta ao normal fora dela
            if (freeLook != null && playerMovement != null)
            {
                float speed = new Vector3(playerMovement.rb.linearVelocity.x, 0, playerMovement.rb.linearVelocity.z).magnitude;
                float t = Mathf.InverseLerp(playerMovement.walkSpeed, playerMovement.RunSpeed, speed);
                float targetFOV = Mathf.Lerp(baseFOV, runFOV, t);
                freeLook.m_Lens.FieldOfView = Mathf.Lerp(freeLook.m_Lens.FieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
            }
        }

        /// <summary>
        /// Adds Offset to the camera while being on Climbing or inGround
        /// </summary>
        public void newOffset(bool offset)
        {
            _from = cameraOffset.m_Offset;
            if (offset)
                _target = _offset;
            else
                _target = _default;

            anim = true;
            curTime = 0;
        }
    }
}
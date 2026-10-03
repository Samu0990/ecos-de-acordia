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

namespace Climbing
{

    [RequireComponent(typeof(ThirdPersonController))]
    public class DetectionCharacterController : MonoBehaviour
    {
        public bool showDebug = true;

        [Header("Layers")]
        public LayerMask ledgeLayer;
        public LayerMask climbLayer;

        [Header("Ground Probe")]
        [Tooltip("Camadas consideradas chao. Por padrao ignora Ignore Raycast, inimigos e o proprio jogador.")]
        public LayerMask groundLayer = ~((1 << 2) | (1 << 8) | (1 << 10));
        [Range(0.1f, 0.35f)] public float groundProbeRadius = 0.22f;
        readonly RaycastHit[] groundHits = new RaycastHit[12];

        [Header("Rays")]
        [SerializeField] private Vector3 OriginLedgeRay;
        [SerializeField] private Vector3 OriginFeetRay;
        [SerializeField] private float LedgeRayLength = 1.5f;
        [SerializeField] private float FeetRayLength = 0.6f;
        [SerializeField] private float FindLedgeNumRays = 7;
        [SerializeField] private float DropLedgeNumRays = 8;

        public bool FindLedgeCollision(out RaycastHit hit)
        {
            Vector3 rayOrigin = transform.TransformDirection(OriginLedgeRay) + transform.position;

            for(int i = 0; i < FindLedgeNumRays; i++)
            {
                bool ret = ThrowRayToLedge(rayOrigin + new Vector3(0, 0.15f * i, 0), out hit);

                if (ret)
                {
                    return true;
                }
            }

            //Set invalid hit
            Physics.Raycast(Vector3.zero, Vector3.forward, out hit, 0, -1);
            return false;
        }
        public bool FindDropLedgeCollision(out RaycastHit hit)
        {
            for (int i = 0; i < DropLedgeNumRays; i++)
            {
                Vector3 origin = transform.position + transform.forward * 0.8f - new Vector3(0, i * 0.15f, 0);

                Debug.DrawLine(origin, transform.position - new Vector3(0, i * 0.15f, 0));

                if(Physics.Raycast(origin, -transform.forward, out hit, 0.8f, ledgeLayer))
                {
                    if (showDebug) //Normal
                    {
                        Debug.DrawLine(hit.point, hit.point + hit.normal, Color.cyan);
                    }

                    if (hit.normal == -hit.transform.forward)
                    {
                        return true;
                    }
                }
            }

            //Set invalid hit
            Physics.Raycast(Vector3.zero, Vector3.forward, out hit, 0, -1);
            return false;
        }

        public bool FindFootCollision(Vector3 targetPos, Quaternion rot, Vector3 normal)
        {
            bool foundWall = true;

            Vector3 PointFoot1 = targetPos + rot * (new Vector3(-0.15f, -0.10f, 0) + OriginFeetRay);
            Vector3 PointFoot2 = targetPos + rot * (new Vector3(0.10f, 0, 0) + OriginFeetRay);

            RaycastHit hit;
            if (!Physics.Raycast(PointFoot1, -normal, out hit, FeetRayLength))
            {
                foundWall = false;
            }
            if (!Physics.Raycast(PointFoot2, -normal, out hit, FeetRayLength))
            {
                foundWall = false;
            }

            return foundWall;
        }

        public bool ThrowRayToLedge(Vector3 origin, out RaycastHit hit)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + transform.forward * LedgeRayLength, Color.green);
            }

            if (Physics.Raycast(origin, transform.forward, out hit, LedgeRayLength, ledgeLayer))
            {
                if (showDebug) //Normal
                {
                    Debug.DrawLine(hit.point, hit.point + hit.normal, Color.cyan);
                }

                if (hit.normal == hit.transform.forward || hit.normal == -hit.transform.forward)
                    return true;

            }
            return false;

        }
        public bool ThrowClimbRay(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {

            Vector3 origin1 = origin + new Vector3(0, 1.8f, 0);
            Vector3 origin2 = origin + new Vector3(0, 0.5f, 0);

            if (showDebug)
            {
                Debug.DrawLine(origin1, origin1 + direction * length, Color.green);
                Debug.DrawLine(origin2, origin2 + direction * length, Color.green);
            }

            if (!Physics.Raycast(origin1, direction, out hit, length) && !Physics.Raycast(origin2, direction, out hit, length)) //Check Forward
            {
                Vector3 origin3 = origin + direction * 0.15f + new Vector3(0,0.5f,0);

                if (showDebug)
                {
                    Debug.DrawLine(origin3, origin3 - Vector3.up * length, Color.cyan);
                }

                if (Physics.Raycast(origin3, -Vector3.up, out hit, length))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ThrowHandRayToLedge(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + transform.TransformDirection(direction) * length, Color.green);
            }

            return Physics.Raycast(origin, transform.TransformDirection(direction), out hit, length, ledgeLayer);
        }
        public bool ThrowFootRayToLedge(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + transform.TransformDirection(direction) * length, Color.green);
            }

            return Physics.Raycast(origin, transform.TransformDirection(direction), out hit, length, climbLayer);
        }

        public bool ThrowRayOnDirection(Vector3 origin, Vector3 direction, float length, out RaycastHit hit, LayerMask layer)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + direction * length, Color.green);
            }

            return Physics.Raycast(origin, direction, out hit, length, layer);
        }
        public bool ThrowRayOnDirection(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + direction * length, Color.green);
            }

            return Physics.Raycast(origin, direction, out hit, length);
        }

        public bool ThrowRayOnDirection(Vector3 origin, Vector3 direction, float length)
        {
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + direction * length, Color.green);
            }

            return Physics.Raycast(origin, direction, length);
        }

        public bool IsGrounded(float stepHeight) {
            float distance = 0.08f + Mathf.Clamp(stepHeight, 0.1f, 0.8f) * 0.5f;
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            if (showDebug)
            {
                Debug.DrawLine(origin, origin + Vector3.down * distance, Color.green);
            }
            int count = Physics.SphereCastNonAlloc(origin, groundProbeRadius, Vector3.down, groundHits,
                distance, groundLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = groundHits[i].collider;
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (Vector3.Dot(groundHits[i].normal, Vector3.up) < 0.25f) continue; // parede lateral nao e piso
                return true;
            }
            return false;
        }

        public void FindAheadPoints(ref List<HandlePoints> list)
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, 5);

            foreach (var item in cols)
            {
                //Antes comparava posições absolutas (Dot de dois pontos no mundo, não de uma
                //direção) - isso não filtra por "estar à frente", praticamente sempre dava
                //positivo. Agora compara a direção até o item com a direção que o personagem
                //está olhando.
                Vector3 toItem = item.transform.position - transform.position;
                if (Vector3.Dot(toItem.normalized, transform.forward) > 0)
                {
                    HandlePoints handle = item.GetComponentInChildren<HandlePoints>();
                    if (handle)
                        list.Add(handle);
                }
            }
        }
    }
}

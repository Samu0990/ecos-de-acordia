using UnityEngine;

namespace Elyndra.World
{
    /// <summary>Posição de câmera de cinema (cutscenes, apresentação de chefe, sobrevoo de chegada).</summary>
    public class CinematicCameraPoint : MonoBehaviour
    {
        public string label = "";
        public float fov = 45f;
        public Transform lookAt;
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, fov, 3f, 0.3f, 1.6f);
        }
    }
}

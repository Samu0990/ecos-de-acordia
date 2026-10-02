using UnityEngine;

namespace Campanula
{
    /// <summary>Gira em torno de um eixo local (pás do moinho).</summary>
    public class Spin : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degreesPerSecond = 18f;
        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

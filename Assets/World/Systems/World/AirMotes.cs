using UnityEngine;

namespace Elyndra.World
{
    /// <summary>Partículas do ar do reino (pólen, cinza, neve, brasas, poeira de cristal) sempre em volta da câmera.</summary>
    public class AirMotes : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c != null) transform.position = c.transform.position;
        }
    }
}

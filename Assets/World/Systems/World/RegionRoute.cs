using UnityEngine;

namespace Elyndra.World
{
    /// <summary>Rota principal da região (do portão de chegada aos pontos importantes). Usada pelo teste
    /// automático de travessia e como guia de design; desenhada no editor.</summary>
    public class RegionRoute : MonoBehaviour
    {
        public Vector3[] points = new Vector3[0];
        public string[] labels = new string[0];
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            for (int i = 0; i + 1 < points.Length; i++) Gizmos.DrawLine(points[i] + Vector3.up, points[i + 1] + Vector3.up);
        }
    }
}

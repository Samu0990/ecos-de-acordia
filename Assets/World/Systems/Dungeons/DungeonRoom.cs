using UnityEngine;

namespace Elyndra.World
{
    public enum DungeonRole { Entrada, Exploracao, Combate, Mecanica, Atalho, Miniboss, Final }

    /// <summary>Sala de masmorra com o papel dela no ritmo (entrada → exploração → combate → mecânica →
    /// atalho → miniboss → final). Serve para o construtor, para o teste automático e como guia de arte.</summary>
    public class DungeonRoom : MonoBehaviour
    {
        public DungeonRole role;
        public string title = "";
        public Vector3 size = new Vector3(14, 6, 14);
        void OnDrawGizmos()
        {
            Gizmos.color = role == DungeonRole.Miniboss ? new Color(1, 0.4f, 0.2f, 0.4f) : role == DungeonRole.Final ? new Color(1, 0.9f, 0.3f, 0.4f) : new Color(0.5f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
        }
    }
}

using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Marca um objeto como PROVISÓRIO (placeholder) e diz o que deve substituí-lo. O construtor do mundo
    /// coloca em tudo que ainda não tem asset final; o menu "Elyndra/Relatório de placeholders" lista todos.
    /// </summary>
    public class PlaceholderTag : MonoBehaviour
    {
        [TextArea] public string replaceWith = "";
        public string category = "Ambiente";   // Ambiente, Construção, Inimigo, Chefe, NPC, VFX, Prop
    }
}

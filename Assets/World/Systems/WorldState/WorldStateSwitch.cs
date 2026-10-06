using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Liga/desliga filhos conforme o WorldState (condição de texto, ver WorldState.Check). Usado para a
    /// MESMA região mudar sem reconstruir a cena: Campânula viva × tomada, portões abertos depois de Dó,
    /// áreas futuras bloqueadas, destroços depois do impacto etc.
    /// </summary>
    public class WorldStateSwitch : MonoBehaviour
    {
        [Tooltip("Condição do WorldState: \"fase:BrilhoCaiu\", \"nota:do\", \"flag:x\", \"!flag:x\"…")]
        public string condition = "";
        [Tooltip("Filhos ligados quando a condição é verdadeira")] public GameObject whenTrue;
        [Tooltip("Filhos ligados quando a condição é falsa")] public GameObject whenFalse;

        void OnEnable() { WorldState.OnChanged += Refresh; Refresh(); }
        void OnDisable() { WorldState.OnChanged -= Refresh; }

        public void Refresh()
        {
            bool v = WorldState.Check(condition);
            if (whenTrue != null && whenTrue.activeSelf != v) whenTrue.SetActive(v);
            if (whenFalse != null && whenFalse.activeSelf == v) whenFalse.SetActive(!v);
        }
    }
}

using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Atalho de mão única da masmorra: só abre pelo lado de dentro (chegando em <see cref="openFrom"/>), e
    /// fica aberto para sempre (WorldState). Aberto, vira uma "passagem de Eco" nos dois sentidos entre a
    /// sala do atalho e a entrada (<see cref="portalA"/> ↔ <see cref="portalB"/>) — funciona em qualquer
    /// forma de masmorra (descida, torre, anel…), sem depender de as salas serem vizinhas.
    /// </summary>
    public class ShortcutDoor : MonoBehaviour
    {
        public string id = "";
        public GameObject door;           // véu/grade que some ao abrir
        public Transform openFrom;
        public float radius = 2.8f;
        public Transform portalA, portalB;   // gatilhos (lado do atalho, lado da entrada)
        public GameObject portalVisualA, portalVisualB;
        bool open; float cooldown;
        string Flag => "atalho:" + gameObject.scene.name + ":" + id;

        void Start() { SetOpen(WorldState.Has(Flag)); }

        void SetOpen(bool v)
        {
            open = v;
            if (door != null) door.SetActive(!v);
            if (portalVisualA != null) portalVisualA.SetActive(v);
            if (portalVisualB != null) portalVisualB.SetActive(v);
        }

        void Update()
        {
            var flow = RegionFlow.Instance;
            if (flow == null || flow.Player == null) return;
            var p = flow.Player.position;
            if (!open)
            {
                if (openFrom != null && (p - openFrom.position).sqrMagnitude < radius * radius)
                {
                    SetOpen(true);
                    WorldState.SetFlag(Flag);
                    Aren.ArenAudio.Play(Aren.Sfx.Bell, transform.position, 0.6f, 0.6f);
                    Aren.UI.ArenHUD.Instance?.Toast("Atalho aberto (passagem de Eco)", Aren.UI.UIKit.Gold, 1.8f);
                }
                return;
            }
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;
            if (portalA != null && portalB != null)
            {
                if ((p - portalA.position).sqrMagnitude < 1.6f * 1.6f) { flow.Teleport(portalB.position + portalB.forward * 2.5f, portalB.eulerAngles.y); cooldown = 1.5f; }
                else if ((p - portalB.position).sqrMagnitude < 1.6f * 1.6f) { flow.Teleport(portalA.position + portalA.forward * 2.5f, portalA.eulerAngles.y); cooldown = 1.5f; }
            }
        }
    }
}

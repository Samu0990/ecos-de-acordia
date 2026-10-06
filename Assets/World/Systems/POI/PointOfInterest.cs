using UnityEngine;

namespace Elyndra.World
{
    public enum PoiKind
    {
        Artefato, Reliquia, Nota, Encantamento, Segredo, Recompensa,
        NPC, Loja, Missao, Taverna, Templo, Oficina, Mercado, Praca, Residencia,
        Marco, AreaFutura, Custodio
    }

    /// <summary>
    /// Ponto de interesse do mundo: lugar preparado para artefatos, Relíquias de Vael, encantamentos,
    /// segredos, recompensas, NPCs (ponto de spawn + papel), lojas, missões, templos, oficinas, tavernas…
    /// Tudo identificado (tipo, nome, id do cânone) para o conteúdo final entrar no lugar certo. Os que
    /// podem ser "pegos" (artefato, recompensa, segredo) gravam no WorldState; Relíquias NÃO são chaves:
    /// só registram que o Aren as encontrou (o que fazer com elas é decisão política/moral, com Custódio).
    /// </summary>
    public class PointOfInterest : MonoBehaviour
    {
        public PoiKind kind;
        public string title = "";
        [TextArea] public string description = "";
        public string canonId = "";          // id da relíquia, nota, artefato…
        public string role = "";             // NPCs: ferreiro, curadora, vendedor…
        public bool placeholder = true;
        public float interactRadius = 2.6f;
        public GameObject visual;
        bool done;

        string Flag => "poi:" + gameObject.scene.name + ":" + name;

        void Start()
        {
            done = WorldState.Has(Flag);
            if (done && visual != null && (kind == PoiKind.Artefato || kind == PoiKind.Recompensa)) visual.SetActive(false);
        }

        void Update()
        {
            if (done || RegionFlow.Instance == null || RegionFlow.Instance.Player == null) return;
            if (kind != PoiKind.Artefato && kind != PoiKind.Recompensa && kind != PoiKind.Segredo && kind != PoiKind.Reliquia && kind != PoiKind.Encantamento) return;
            if ((RegionFlow.Instance.Player.position - transform.position).sqrMagnitude > interactRadius * interactRadius) return;
            done = true;
            WorldState.SetFlag(Flag);
            var hud = Aren.UI.ArenHUD.Instance;
            switch (kind)
            {
                case PoiKind.Reliquia:
                    var r = WorldCanon.Relic(canonId);
                    WorldState.SetFlag("reliquia_vista:" + canonId);
                    if (r != null) { hud?.ShowArea(r.name, r.faceta + " · Custódio: " + r.custodio); hud?.ShowHint(r.capacidade + "\n<b>Preço:</b> " + r.preco + "\n<i>Reunir as facetas de Vael é uma escolha — e tem consequências.</i>", 9f); }
                    break;
                case PoiKind.Segredo:
                    hud?.Toast("Segredo: " + title, Aren.UI.UIKit.Violet, 2f);
                    Aren.World.Notas.Add(150);
                    break;
                default:
                    hud?.ShowArea(title, description);
                    Aren.World.Notas.Add(kind == PoiKind.Artefato ? 300 : 100);
                    if (visual != null && (kind == PoiKind.Artefato || kind == PoiKind.Recompensa)) visual.SetActive(false);
                    break;
            }
            Aren.ArenAudio.PlayUI(Aren.Sfx.UIConfirm, 0.8f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == PoiKind.Reliquia ? Color.yellow : kind == PoiKind.Segredo ? Color.magenta : kind == PoiKind.NPC ? Color.cyan : Color.white;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1, 2, 1));
        }
    }
}

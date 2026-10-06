using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Mecânica de masmorra (placas de ritmo): pisar nas placas NA ORDEM e dentro do compasso (cada placa
    /// acende e "toca" uma nota); errar a ordem ou demorar demais reinicia. Resolvido, a porta abre e fica
    /// aberta (WorldState). Cada masmorra usa a sua leitura do tema (comportas no aqueduto, pontes de
    /// cadência em Velária, registros em Miralume…) — a base é a mesma, a apresentação muda.
    /// </summary>
    public class RhythmPlates : MonoBehaviour
    {
        public string id = "";
        public List<Transform> plates = new List<Transform>();
        public float maxGap = 2.2f;
        public float plateRadius = 1.2f;
        public GameObject door;          // some quando resolvido
        public Light doneLight;
        int next; float lastStep; bool solved;
        readonly List<Renderer> rends = new List<Renderer>();
        MaterialPropertyBlock mpb;

        string Flag => "mecanica:" + gameObject.scene.name + ":" + id;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            foreach (var p in plates) rends.Add(p != null ? p.GetComponentInChildren<Renderer>() : null);
            solved = WorldState.Has(Flag);
            if (solved) Solve(false);
        }

        void Update()
        {
            if (solved || RegionFlow.Instance == null || RegionFlow.Instance.Player == null || plates.Count == 0) return;
            var p = RegionFlow.Instance.Player.position;
            if (next > 0 && Time.time - lastStep > maxGap) Fail();
            for (int i = 0; i < plates.Count; i++)
            {
                var pl = plates[i];
                if (pl == null) continue;
                var d = p - pl.position; d.y = 0;
                if (d.sqrMagnitude > plateRadius * plateRadius || Mathf.Abs(p.y - pl.position.y) > 1.6f) continue;
                if (i == next) { Step(i); break; }
                if (i != next - 1 && next > 0) { Fail(); break; }
            }
            for (int i = 0; i < rends.Count; i++)
            {
                if (rends[i] == null) continue;
                float lit = i < next ? 1f : (i == next ? 0.35f + 0.25f * Mathf.Sin(Time.time * 6f) : 0.08f);
                mpb.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.4f) * lit * 2f);
                mpb.SetColor("_Color", Color.Lerp(new Color(0.35f, 0.32f, 0.3f), new Color(1f, 0.85f, 0.6f), lit));
                rends[i].SetPropertyBlock(mpb);
            }
        }

        void Step(int i)
        {
            next = i + 1; lastStep = Time.time;
            Aren.ArenAudio.Play(Aren.Sfx.Bell, plates[i].position, 0.5f, 0.8f + 0.12f * i);
            if (next >= plates.Count) Solve(true);
        }

        void Fail()
        {
            if (next > 0) Aren.UI.ArenHUD.Instance?.Toast("Fora do compasso", new Color(0.8f, 0.4f, 0.5f), 1f);
            next = 0;
        }

        void Solve(bool announce)
        {
            solved = true;
            if (door != null) door.SetActive(false);
            if (doneLight != null) doneLight.enabled = true;
            if (announce)
            {
                WorldState.SetFlag(Flag);
                Aren.UI.ArenHUD.Instance?.Toast("A passagem responde", Aren.UI.UIKit.Gold, 2f);
                Aren.World.Notas.Add(200);
            }
        }
    }
}

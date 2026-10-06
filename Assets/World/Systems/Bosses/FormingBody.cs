using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Corpo provisório de uma Nota "ainda se formando" (Dó Partido em Forma de Queda: pedra, madeira e
    /// metal da região girando em volta de um núcleo pesado, que pulsa). PLACEHOLDER até o chefe existir.
    /// </summary>
    public class FormingBody : MonoBehaviour
    {
        public Transform core;
        public float spin = 9f, bob = 0.6f;
        Transform[] pieces; Vector3[] basePos; float[] phase;

        void Start()
        {
            int n = transform.childCount;
            pieces = new Transform[n]; basePos = new Vector3[n]; phase = new float[n];
            for (int i = 0; i < n; i++) { pieces[i] = transform.GetChild(i); basePos[i] = pieces[i].localPosition; phase[i] = i * 1.7f; }
        }

        void Update()
        {
            float t = Time.time;
            transform.localRotation = Quaternion.Euler(0, t * spin, 0);
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == core) continue;
                // o peso puxa para dentro e solta (a matéria ainda não aceitou a forma)
                float pull = 0.85f + 0.15f * Mathf.Sin(t * 0.9f + phase[i]);
                pieces[i].localPosition = basePos[i] * pull + Vector3.up * Mathf.Sin(t * 1.3f + phase[i]) * bob;
                pieces[i].Rotate(new Vector3(13f, 29f, 7f) * Time.deltaTime * (0.5f + (i % 3) * 0.3f), Space.Self);
            }
            if (core != null) core.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 2.2f));
        }
    }
}

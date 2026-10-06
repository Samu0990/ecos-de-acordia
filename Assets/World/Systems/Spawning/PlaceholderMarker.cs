using System.Collections.Generic;
using Aren.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Elyndra.World
{
    /// <summary>
    /// Silhueta provisória de um inimigo que ainda não tem modelo/rig: corpo escuro com o brilho da
    /// distorção e o nome por cima. Fica claramente identificado como PLACEHOLDER no jogo.
    /// </summary>
    public class PlaceholderMarker : MonoBehaviour
    {
        TextMesh text; Renderer body; float t0;
        static Material mat;

        public static GameObject Create()
        {
            var go = new GameObject("PLACEHOLDER inimigo");
            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(go.transform, false);
            b.transform.localPosition = new Vector3(0, 1f, 0);
            b.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            if (mat == null)
            {
                var sh = Shader.Find("Legacy Shaders/Self-Illumin/Diffuse") ?? Shader.Find("Standard");
                mat = new Material(sh) { color = new Color(0.08f, 0.06f, 0.1f) };
            }
            var r = b.GetComponent<Renderer>(); r.sharedMaterial = mat;
            var tgo = new GameObject("Nome");
            tgo.transform.SetParent(go.transform, false);
            tgo.transform.localPosition = new Vector3(0, 2.6f, 0);
            var tm = tgo.AddComponent<TextMesh>();
            tm.characterSize = 0.06f; tm.fontSize = 48; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.75f, 0.85f);
            var pm = go.AddComponent<PlaceholderMarker>();
            pm.text = tm; pm.body = r;
            return go;
        }

        public void Set(string name, Distortion d)
        {
            if (text != null) text.text = "PLACEHOLDER\n" + name + (d != Distortion.Nenhuma ? "  (" + d + ")" : "");
            t0 = Time.time;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null && text != null) text.transform.rotation = Quaternion.LookRotation(text.transform.position - cam.transform.position);
            if (body != null) body.transform.localScale = new Vector3(0.7f, 1f + 0.04f * Mathf.Sin((Time.time - t0) * 2.3f), 0.7f);
        }
    }
}

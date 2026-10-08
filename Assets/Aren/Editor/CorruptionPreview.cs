using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Aren.World;

namespace Aren.EditorTools
{
    /// <summary>
    /// Prévia sem Play mode da transformação do aldeão (shader Aren/Villager + CorruptionMorph + o círculo da
    /// Fenda): renderiza as fases lado a lado — corpo inteiro em cima, rosto embaixo — numa cena de prévia
    /// isolada (não mexe na cena aberta). Saída: Tools/cli/shots/corruption_stages.png.
    /// </summary>
    public static class CorruptionPreview
    {
        struct Stage { public string name; public float corrupt, tattoo, skull, morph; public string anim; public float t; public bool ring; }

        [MenuItem("Aren/Prévia/Transformação do aldeão")]
        public static string Render() => Render("Villager_M1");

        public static string Render(string villager)
        {
            var stages = new[]
            {
                new Stage { name = "vivo", anim = "Idle_FoldArms", t = 0.3f },
                new Stage { name = "anel subindo", corrupt = 0.45f, tattoo = 0.25f, anim = "Convulse", t = 0.25f, ring = true },
                new Stage { name = "tatuagens", corrupt = 1f, tattoo = 0.75f, skull = 0.25f, morph = 0.2f, anim = "Convulse", t = 0.55f, ring = true },
                new Stage { name = "crânio", corrupt = 1f, tattoo = 1f, skull = 0.75f, morph = 0.6f, anim = "Convulse", t = 0.8f },
                new Stage { name = "esticado", corrupt = 1f, tattoo = 1f, skull = 1f, morph = 1f, anim = "ZombieIdle", t = 0.4f },
                new Stage { name = "Eco (luta)", corrupt = 1f, tattoo = 1f, skull = 0.55f, anim = "ZombieIdle", t = 0.2f },
            };
            const int W = 360, H = 520, HF = 300;
            var sheet = new Texture2D(W * stages.Length, H + HF, TextureFormat.RGB24, false);
            var scene = EditorSceneManager.NewPreviewScene();
            var made = new List<GameObject>();
            GameObject Put(GameObject g) { EditorSceneManager.MoveGameObjectToScene(g, scene); made.Add(g); return g; }
            try
            {
                var key = Put(new GameObject("Lua")); var kl = key.AddComponent<Light>(); kl.type = LightType.Directional; kl.intensity = 0.55f; kl.color = new Color(0.7f, 0.75f, 1f);
                key.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
                var warm = Put(new GameObject("Lanterna")); var wl = warm.AddComponent<Light>(); wl.type = LightType.Point; wl.range = 8f; wl.intensity = 1.6f; wl.color = new Color(1f, 0.65f, 0.35f);
                var floor = Put(GameObject.CreatePrimitive(PrimitiveType.Plane)); floor.transform.localScale = Vector3.one * 2f;
                floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.18f, 0.17f, 0.17f) };
                var camGo = Put(new GameObject("Câmera")); var cam = camGo.AddComponent<Camera>();
                cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.035f, 0.03f, 0.05f);
                cam.allowHDR = true; cam.nearClipPlane = 0.02f;
                var src = Resources.Load<GameObject>("Villagers/" + villager);
                if (src == null) return "sem prefab " + villager;
                for (int i = 0; i < stages.Length; i++)
                {
                    var st = stages[i];
                    var v = Put((GameObject)Object.Instantiate(src));
                    v.transform.position = Vector3.zero;
                    var anim = v.GetComponent<Animator>();
                    // corpo de frente para a câmera (pelos ombros: a raiz do modelo olha para -Z)
                    AnimationClip clip = null;
                    string want = st.anim == "Convulse" ? "Zombie_Scratch" : st.anim == "ZombieIdle" ? "Zombie_Idle_Loop" : "Idle_FoldArms_Loop";
                    foreach (var c in anim.runtimeAnimatorController.animationClips) if (c != null && c.name == want) clip = c;
                    if (clip != null) clip.SampleAnimation(v, st.t * clip.length);
                    var ls = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position; var rs = anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                    var right = rs - ls; right.y = 0f; var fwd = Vector3.Cross(right.normalized, Vector3.up);
                    var morph = v.AddComponent<CorruptionMorph>(); morph.Init(); morph.amount = st.morph; morph.hover = 0f; morph.Pose();
                    var mpb = new MaterialPropertyBlock();
                    foreach (var r in v.GetComponentsInChildren<Renderer>())
                    {
                        r.GetPropertyBlock(mpb);
                        mpb.SetFloat("_Corrupt", st.corrupt); mpb.SetFloat("_Tattoo", st.tattoo); mpb.SetFloat("_Skull", st.skull); mpb.SetFloat("_Seed", 3.7f);
                        r.SetPropertyBlock(mpb);
                    }
                    var headT = anim.GetBoneTransform(HumanBodyBones.Head);
                    FendaRing ring = null;
                    if (st.ring)
                    {
                        var rg = new GameObject("anel"); EditorSceneManager.MoveGameObjectToScene(rg, scene); made.Add(rg);
                        ring = null;
                        // anel simples para a prévia (o componente usa Camera.main/LateUpdate): uma faixa do shader do anel
                        var mf = rg.AddComponent<MeshFilter>(); var mr = rg.AddComponent<MeshRenderer>();
                        mf.sharedMesh = RingMesh(0.55f, 0.11f);
                        var m = new Material(Shader.Find("Aren/FX/FendaRing")); m.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_perlin")); m.SetFloat("_Intensity", 1.2f);
                        mr.sharedMaterial = m;
                        rg.transform.position = new Vector3(0f, st.corrupt * 1.25f * 1.85f * 0.95f + 0.05f, 0f);
                    }
                    wl.transform.position = fwd * 1.6f + Vector3.right * 0.9f + Vector3.up * 1.9f;
                    // corpo inteiro
                    float top = headT.position.y + 0.25f;
                    cam.fieldOfView = 30f;
                    cam.transform.position = fwd * 4.6f + Vector3.up * (top * 0.52f);
                    cam.transform.rotation = Quaternion.LookRotation(Vector3.up * (top * 0.5f) - cam.transform.position);
                    Shoot(cam, sheet, i * W, HF, W, H);
                    // rosto
                    cam.fieldOfView = 24f;
                    var face = headT.position + Vector3.up * 0.06f;
                    cam.transform.position = face + fwd * 0.75f + Vector3.right * 0.12f;
                    cam.transform.rotation = Quaternion.LookRotation(face - cam.transform.position);
                    Shoot(cam, sheet, i * W, 0, W, HF);
                    Object.DestroyImmediate(v); made.Remove(v);
                }
            }
            finally
            {
                foreach (var g in made) if (g != null) Object.DestroyImmediate(g);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            sheet.Apply();
            string path = "Tools/cli/shots/corruption_stages.png";
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return path;
        }

        static void Shoot(Camera cam, Texture2D sheet, int x, int y, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            cam.targetTexture = rt; cam.aspect = w / (float)h;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, w, h), x, y);
            RenderTexture.active = prev; cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        static Mesh RingMesh(float r, float w)
        {
            const int seg = 72;
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[v.Length]; var t = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v[i * 2] = new Vector3(Mathf.Cos(a) * (r - w / 2), 0, Mathf.Sin(a) * (r - w / 2)); v[i * 2 + 1] = new Vector3(Mathf.Cos(a) * (r + w / 2), 0, Mathf.Sin(a) * (r + w / 2));
                uv[i * 2] = new Vector2(i / (float)seg, 0); uv[i * 2 + 1] = new Vector2(i / (float)seg, 1);
                if (i < seg) { int k = i * 2; t[i * 6] = k; t[i * 6 + 1] = k + 2; t[i * 6 + 2] = k + 1; t[i * 6 + 3] = k + 1; t[i * 6 + 4] = k + 2; t[i * 6 + 5] = k + 3; }
            }
            var m = new Mesh { vertices = v, uv = uv, triangles = t }; m.RecalculateBounds();
            return m;
        }
    }
}

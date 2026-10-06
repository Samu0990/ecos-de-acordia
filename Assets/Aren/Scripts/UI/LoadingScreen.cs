using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// Tela de carregamento (enquanto o jogo monta a noite da Ruptura, compila os shaders e prepara os
    /// sons — antes isso congelava o primeiro quadro). Fundo: o painel principal do storyboard do autor
    /// (Campanula e a Fenda dos Sete Brilhos) em faixa de cinema, com zoom lento e deriva; brasas subindo;
    /// frases da lore trocando devagar; barra dourada fina com a etapa atual. Tudo em tempo real (não
    /// depende do timeScale) e barato: uma imagem, alguns quadradinhos e textos.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        Canvas canvas;
        CanvasGroup group;
        RawImage bg;
        RectTransform bgRt, barFill, barGlow;
        Text step, lore, pct;
        float shown, target, progress, shownProgress;
        float alpha = 1f, alphaTarget = 1f;
        int loreIndex; float loreT;
        readonly List<(RectTransform rt, Image im, Vector2 v, float life, float age, float size)> embers = new List<(RectTransform, Image, Vector2, float, float, float)>();

        static readonly string[] Lore =
        {
            "Em Elyndra, cantar é tão normal quanto falar. Aren Vesper nunca teve voz.",
            "Campânula canta para trabalhar, curar e consertar. Aren responde com as mãos — e com a flauta.",
            "Instrumentos são raros: Edran Vael os criou para lutar sem uma voz que pudesse ser roubada.",
            "Elyan Vharos não controlava músculos. Ele mudava o que uma criatura queria fazer.",
            "Os Corrompidos não são monstros novos: são moradores, animais e plantas respondendo errado.",
            "Sete brilhos saíram da Fenda. Um caiu em Valtéria — longe o bastante para parecer mistério.",
            "Poder suficiente para resolver tudo também é poder suficiente para decidir tudo. — Edran Vael",
            "A Fenda fica muito além das montanhas. Um dia o caminho vai até lá.",
        };

        public static LoadingScreen Show()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Tela de carregamento");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<LoadingScreen>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            canvas = UIKit.MakeCanvas("Carregamento", 900);
            canvas.transform.SetParent(transform, false);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            var root = canvas.transform;
            var black = UIKit.Stretch("Preto", root).gameObject.AddComponent<Image>();
            black.color = Color.black; black.raycastTarget = true;

            // faixa de cinema com a arte (2028x784 ≈ 2.59:1), "cobrindo" a largura
            var tex = Resources.Load<Texture2D>("UI/Loading/loading_fenda");
            bgRt = UIKit.Rect("Arte", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1920, 742));
            bg = bgRt.gameObject.AddComponent<RawImage>();
            bg.texture = tex; bg.raycastTarget = false;
            bg.color = tex != null ? new Color(0.92f, 0.92f, 0.95f, 1f) : Color.clear;
            // escurece as bordas da faixa (vinheta horizontal) e o rodapé
            Grad(root, "Topo", new Vector2(0, 40 + 371 - 60), 120, true);
            Grad(root, "Base", new Vector2(0, 40 - 371 + 60), 120, false);

            var title = UIKit.Label("Titulo", root, UIKit.Spaced("ECOS DO CONTRACANTO", 2), UIKit.Display, 34, UIKit.Gold, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(480, -62), new Vector2(800, 50));
            UIKit.Label("Sub", root, "A Ruptura do Contracanto", UIKit.Serif, 20, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(480, -98), new Vector2(800, 30));
            lore = UIKit.Label("Lore", root, Lore[0], UIKit.Serif, 26, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1400, 80));
            lore.fontStyle = FontStyle.Italic;

            // barra: trilho escuro, preenchimento dourado e um brilho na ponta
            var track = UIKit.Rect("Trilho", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 86), new Vector2(760, 3));
            track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            barFill = UIKit.Rect("Preenchido", track, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 0));
            barFill.gameObject.AddComponent<Image>().color = UIKit.Gold;
            barGlow = UIKit.Rect("Brilho", track, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 18));
            var gi = barGlow.gameObject.AddComponent<Image>();
            gi.sprite = UIKit.S("ui_glow"); gi.color = new Color(1f, 0.85f, 0.55f, 0.9f); gi.raycastTarget = false;
            if (gi.sprite == null) gi.color = new Color(1f, 0.85f, 0.55f, 0.5f);
            step = UIKit.Label("Etapa", root, "", UIKit.Sans, 18, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(-30, 58), new Vector2(700, 30));
            pct = UIKit.Label("Pct", root, "", UIKit.Sans, 18, UIKit.GoldDim, TextAnchor.MiddleRight, new Vector2(0.5f, 0f), new Vector2(330, 58), new Vector2(100, 30));

            for (int i = 0; i < 34; i++)
            {
                var rt = UIKit.Rect("Brasa", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 4f);
                var im = rt.gameObject.AddComponent<Image>(); im.raycastTarget = false;
                im.sprite = UIKit.S("ui_glow");
                embers.Add((rt, im, Vector2.zero, 0f, 999f, 3f));
            }
            canvas.gameObject.SetActive(true);
        }

        static Texture2D gradTex;

        void Grad(Transform root, string n, Vector2 pos, float h, bool top)
        {
            // degradê vertical suave (textura 1x64 feita aqui: sem faixas visíveis)
            if (gradTex == null)
            {
                gradTex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < 64; y++) { float k = y / 63f; gradTex.SetPixel(0, y, new Color(0, 0, 0, Mathf.SmoothStep(0f, 1f, k) * 0.92f)); }
                gradTex.Apply();
            }
            var rt = UIKit.Rect(n, root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(2000, h));
            var im = rt.gameObject.AddComponent<RawImage>(); im.raycastTarget = false;
            im.texture = gradTex;
            im.uvRect = top ? new Rect(0, 0, 1, 1) : new Rect(0, 1, 1, -1);   // escuro na borda de fora
        }

        /// <summary>Avança a barra até 'p' (0..1) mostrando o que está sendo preparado.</summary>
        public void Step(float p, string what)
        {
            target = Mathf.Max(target, Mathf.Clamp01(p));
            if (step != null && !string.IsNullOrEmpty(what)) step.text = what;
        }

        /// <summary>Some em 'dur' segundos (o menu já está pronto por baixo) e se destrói.</summary>
        public void Hide(float dur = 0.9f)
        {
            target = 1f;
            alphaTarget = 0f;
            fadeSpeed = 1f / Mathf.Max(0.05f, dur);
        }
        float fadeSpeed = 1f;

        static bool ShotRequested { get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-loading-shot") return true; return false; } }
        int shots;

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            shown += dt;
            // teste (-eda-loading-shot): capturas da própria tela em ~/EcosBench/loading_N.png
            if (shots < 3 && shown > 0.8f + shots * 1.4f && ShotRequested)
            {
                string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "EcosBench");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "loading_" + shots + ".png"));
                shots++;
            }
            // barra: corre atrás do alvo (nunca para totalmente: dá sinal de vida durante uma etapa longa)
            shownProgress = Mathf.MoveTowards(shownProgress, target, dt * (0.35f + (target - shownProgress) * 5f));
            if (barFill != null)
            {
                barFill.sizeDelta = new Vector2(760f * shownProgress, 0);
                barGlow.anchoredPosition = new Vector2(760f * shownProgress, 0);
                barGlow.localScale = Vector3.one * (0.85f + 0.2f * Mathf.Sin(shown * 5f));
                pct.text = Mathf.RoundToInt(shownProgress * 100f) + "%";
            }
            // zoom lento e deriva (Ken Burns)
            if (bgRt != null)
            {
                float z = 1.04f + 0.05f * Mathf.SmoothStep(0f, 1f, shown / 24f);
                bgRt.localScale = new Vector3(z, z, 1f);
                bgRt.anchoredPosition = new Vector2(Mathf.Sin(shown * 0.07f) * 22f, 40f + Mathf.Sin(shown * 0.05f + 1f) * 8f);
            }
            // frases da lore (troca a cada ~6 s com fade)
            loreT += dt;
            if (lore != null)
            {
                float cyc = 6.5f, k = loreT % cyc;
                int idx = Mathf.FloorToInt(loreT / cyc) % Lore.Length;
                if (idx != loreIndex) { loreIndex = idx; lore.text = Lore[idx]; }
                float a = Mathf.Clamp01(k / 0.8f) * Mathf.Clamp01((cyc - k) / 0.8f);
                UIKit.SetAlpha(lore, a * 0.9f);
            }
            // brasas subindo da cidade
            for (int i = 0; i < embers.Count; i++)
            {
                var e = embers[i];
                e.age += dt;
                if (e.age > e.life)
                {
                    e.age = 0f; e.life = Random.Range(3f, 7f); e.size = Random.Range(7f, 16f);
                    e.rt.anchoredPosition = new Vector2(Random.Range(-900f, 900f), Random.Range(-330f, -120f));
                    e.v = new Vector2(Random.Range(-12f, 12f), Random.Range(25f, 70f));
                    e.im.color = Color.Lerp(new Color(1f, 0.7f, 0.35f), new Color(0.8f, 0.6f, 1f), Random.value * 0.35f);
                }
                e.rt.anchoredPosition += (e.v + new Vector2(Mathf.Sin(shown * 1.3f + i) * 10f, 0f)) * dt;
                e.rt.sizeDelta = Vector2.one * e.size;
                float fa = Mathf.Sin(Mathf.Clamp01(e.age / e.life) * Mathf.PI);
                var c = e.im.color; c.a = fa * 0.8f; e.im.color = c;
                embers[i] = e;
            }
            alpha = Mathf.MoveTowards(alpha, alphaTarget, dt * fadeSpeed);
            if (group != null) group.alpha = alpha;
            if (alphaTarget <= 0f && alpha <= 0.001f) { Instance = null; Destroy(gameObject); }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}

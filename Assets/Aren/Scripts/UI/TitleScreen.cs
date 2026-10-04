using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aren.UI
{
    // ---------------------------------------------------------------- layout (title_layout.json)
    [System.Serializable] public class TitleLayout
    {
        public int width, height, activeW, activeH, normalW, normalH, activeOffX, activeOffY;
        public TitleSlot[] slots; public TitleLabel[] labels; public TitlePt[] gems;
        public TitleBox title, panel; public TitlePt emblem; public TitleLight[] lights;
        public TitlePt[] pivots; public float bannerTop;
        public TitleDepthPt[] eyes; public TitleDrip[] drips;
    }
    [System.Serializable] public class TitleDepthPt { public float x, y, p; }
    [System.Serializable] public class TitleDrip { public float x, y, land, p; }
    [System.Serializable] public class TitleSlot { public int x, y; public string label; }
    [System.Serializable] public class TitleLabel { public string name; public int x, y, w, h; public float r, g, b; }
    [System.Serializable] public class TitlePt { public float x, y; }
    [System.Serializable] public class TitleBox { public float x0, y0, x1, y1; }
    [System.Serializable] public class TitleLight { public float x, y, r, i; public int t; public float p, g1, g2, g3; }

    /// <summary>
    /// Tela inicial fiel à arte de referência do autor (Assets/Aren/Resources/UI/Title, gerada
    /// por ArtSource/Menu/scripts/build_title.py). A arte é a própria imagem; por cima dela:
    /// os três botões recortados da arte (placa apagada / placa vermelha com joias), e os
    /// detalhes animados em pixel art — chamas tremulando, brasas e poeira subindo, névoa no
    /// chão, joias pulsando e um reflexo que passa pelas letras do título. O fundo é "motion"
    /// (shader UITitleMotion): a câmera passeia com paralaxe 2.5D, lanterna/lustre balançam, o
    /// estandarte ondula, o ar treme sobre as velas e feixes de luz caem do teto — o painel e os
    /// botões ficam parados. Por cima: chamas dançando, morcegos, Ecos fantasmas, gotas pingando
    /// das estalactites, correntes bem perto da câmera e os olhos da estátua. Na primeira vez a
    /// tela abre com um sino: a luz se espalha a partir do brasão e as velas acendem; depois, de
    /// tempos em tempos, a badalada distorce o cenário e acende o fundo de violeta. O mouse
    /// inclina a câmera e sopra as brasas.
    /// Tudo é posicionado em pixels da arte (1672x941) e escalado para cobrir a tela.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public const float ArtPixel = 2f;   // tamanho do "pixel" da pixel art em px da imagem
        public TitleLayout L { get; private set; }
        public Button First { get; private set; }
        public bool BackdropVisible => backdrop != null && backdrop.activeSelf;

        static Material artMat, addMat;
        public static Material ArtMat => artMat != null ? artMat : (artMat = MakeMat("Shaders/UITitleArt"));
        public static Material AddMat => addMat != null ? addMat : (addMat = MakeMat("Shaders/UITitleAdd"));

        GameObject backdrop;
        Image dim; float dimTarget;
        TitleGlows glows;
        TitleSparks bgSparks, btnSparks;
        Material fogNear, fogFar, shineMat, motionMat, raysMat;
        RectTransform artRT;
        TitleFlames flames; TitleBats bats; TitleWisps wisps; TitleDrips drips; TitleChains chains;
        static bool introPlayed;
        int introState;          // 0 nenhuma, 1 esperando o som, 2 revelando
        float introT0, revealT0, nextToll = 1e9f;
        readonly List<TitleButton> buttons = new List<TitleButton>();
        float shownAt, nextShine = 1.1f, shineStart = -10f;
        int selected;

        static Material MakeMat(string path)
        {
            var sh = Resources.Load<Shader>(path);
            if (sh == null || !sh.isSupported) { Debug.LogWarning("[Title] shader indisponível: " + path); return null; }
            return new Material(sh) { hideFlags = HideFlags.DontSave };
        }

        public static TitleLayout LoadLayout()
        {
            var ta = Resources.Load<TextAsset>("UI/Title/title_layout");
            return ta != null ? JsonUtility.FromJson<TitleLayout>(ta.text) : null;
        }

        /// <summary>Arte de fundo animada (fica atrás de todas as telas do menu).</summary>
        public static TitleScreen Build(Transform canvas)
        {
            var layout = LoadLayout();
            if (layout == null) { Debug.LogWarning("[Title] title_layout.json não encontrado"); return null; }
            var rt = UIKit.Stretch("TelaInicial_Arte", canvas);
            rt.SetAsFirstSibling();
            var ts = rt.gameObject.AddComponent<TitleScreen>();
            ts.L = layout;
            ts.backdrop = rt.gameObject;
            ts.BuildBackdrop(rt);
            return ts;
        }

        public static RectTransform ArtRoot(string name, Transform parent, TitleLayout l)
        {
            var rt = UIKit.Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(l.width, l.height));
            rt.gameObject.AddComponent<TitleArtSpace>().size = new Vector2(l.width, l.height);
            return rt;
        }

        /// <summary>Elemento em coordenadas da arte (canto superior esquerdo, y para baixo).</summary>
        public static RectTransform Place(string name, Transform parent, float x, float y, float w, float h)
            => UIKit.Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

        static RectTransform Fill(string name, Transform parent)
        {
            var rt = UIKit.Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            return rt;
        }

        void BuildBackdrop(RectTransform root)
        {
            var art = ArtRoot("Arte", root, L);
            artRT = art;
            var bg = Place("Fundo", art, 0, 0, L.width, L.height).gameObject.AddComponent<RawImage>();
            bg.texture = Resources.Load<Texture2D>("UI/Title/title_bg");
            bg.raycastTarget = false;
            motionMat = MakeMat("Shaders/UITitleMotion");
            var ma = Resources.Load<Texture2D>("UI/Title/motion_a");
            var mb = Resources.Load<Texture2D>("UI/Title/motion_b");
            var mc = Resources.Load<Texture2D>("UI/Title/motion_c");
            if (motionMat != null && ma != null && mb != null && mc != null)
            {
                motionMat.SetTexture("_MotionA", ma);
                motionMat.SetTexture("_MotionB", mb);
                motionMat.SetTexture("_MotionC", mc);
                motionMat.SetVector("_Art", new Vector4(L.width, L.height, L.width * 0.5f, L.height * 0.5f));
                if (L.pivots != null && L.pivots.Length >= 3)
                {
                    TitleMotion.P1 = new Vector2(L.pivots[0].x, L.pivots[0].y);
                    TitleMotion.P2 = new Vector2(L.pivots[1].x, L.pivots[1].y);
                    TitleMotion.P3 = new Vector2(L.pivots[2].x, L.pivots[2].y);
                }
                TitleMotion.Center = new Vector2(L.width * 0.5f, L.height * 0.5f);
                motionMat.SetVector("_Piv1", new Vector4(TitleMotion.P1.x, TitleMotion.P1.y, TitleMotion.P2.x, TitleMotion.P2.y));
                motionMat.SetVector("_Piv2", new Vector4(TitleMotion.P3.x, TitleMotion.P3.y, L.bannerTop, 0));
                bg.material = motionMat;
            }
            else { motionMat = null; bg.material = ArtMat; }

            // camada animada num Canvas próprio: redesenhar as chamas a cada quadro não
            // obriga a reconstruir o resto do menu
            var anim = Fill("Animado", art);
            anim.gameObject.AddComponent<Canvas>();

            glows = Fill("Luzes", anim).gameObject.AddComponent<TitleGlows>();
            glows.tex = Resources.Load<Sprite>("UI/Title/glow")?.texture;
            glows.material = AddMat; glows.raycastTarget = false;
            foreach (var l in L.lights) glows.Add(l.x, l.y, l.r, l.i, l.t, l.p, l.g1, l.g2, l.g3);
            glows.Add(L.emblem.x, L.emblem.y, 30f, 0.55f, TitleGlows.Gold, 0f, 0f, 0f, 0f);
            if (L.eyes != null) foreach (var e in L.eyes) glows.Add(e.x, e.y, 6f, 1f, TitleGlows.Eye, e.p, 0f, 0f, 0f);
            TitleMotion.RevealCenter = TitleMotion.TollCenter = new Vector2(L.emblem.x, L.emblem.y);

            // chamas dançando por cima das velas principais
            flames = Fill("Chamas", anim).gameObject.AddComponent<TitleFlames>();
            flames.material = AddMat; flames.raycastTarget = false;
            foreach (var l in L.lights) if (l.t == TitleGlows.Flame && l.i >= 0.5f) flames.Add(l.x, l.y, l.i, l.p, l.g1, l.g2, l.g3);

            // feixes de luz fria caindo do teto
            raysMat = MakeMat("Shaders/UITitleRays");
            if (raysMat != null)
            {
                var rays = Place("Feixes", anim, 0, 0, L.width, L.height).gameObject.AddComponent<RawImage>();
                rays.material = raysMat; rays.raycastTarget = false;
                raysMat.SetVector("_Art", new Vector4(L.width, L.height, 0, 0));
                raysMat.SetColor("_RayColor", new Color(0.62f, 0.68f, 0.98f, 0.2f));
            }

            // Ecos fantasmas e morcegos lá no fundo, atrás da névoa
            wisps = Fill("Ecos", anim).gameObject.AddComponent<TitleWisps>();
            wisps.tex = glows.tex; wisps.material = AddMat; wisps.raycastTarget = false;
            bats = Fill("Morcegos", anim).gameObject.AddComponent<TitleBats>();
            bats.tex = Resources.Load<Texture2D>("UI/Title/bat"); bats.raycastTarget = false;

            var fogTex = Resources.Load<Texture2D>("UI/Title/fog");
            fogFar = FogBand("NevoaAlta", anim, fogTex, 0, 540, L.width, 230, new Color(0.30f, 0.26f, 0.38f, 0.14f));

            drips = Fill("Gotas", anim).gameObject.AddComponent<TitleDrips>();
            drips.raycastTarget = false;
            if (L.drips != null) foreach (var d in L.drips) drips.Add(d.x, d.y, d.land, d.p);

            fogNear = FogBand("NevoaChao", anim, fogTex, 0, 750, L.width, 191, new Color(0.38f, 0.33f, 0.46f, 0.28f));

            bgSparks = Fill("Brasas", anim).gameObject.AddComponent<TitleSparks>();
            bgSparks.material = AddMat; bgSparks.raycastTarget = false;
            bgSparks.avoid = new Rect(L.panel.x0, L.panel.y0, L.panel.x1 - L.panel.x0, L.panel.y1 - L.panel.y0);
            var ember = new Color(1f, 0.55f, 0.22f, 0.9f); var emberEnd = new Color(0.85f, 0.12f, 0.1f, 0f);
            // velas do canto direito, altar, lustre, vela da esquerda, chão e poeira
            // (parallax = profundidade de onde nascem, igual ao mapa do fundo)
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1450, 640, 150, 90), rate = 2.6f, velMin = new Vector2(-6, -26), velMax = new Vector2(6, -12), life = new Vector2(2.5f, 5f), a = ember, b = emberEnd, parallax = LightDepth(1450, 640, 1600, 730) });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1340, 500, 150, 70), rate = 1.2f, velMin = new Vector2(-5, -20), velMax = new Vector2(5, -9), life = new Vector2(2f, 4f), a = ember, b = emberEnd, parallax = LightDepth(1340, 500, 1490, 570) });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1255, 335, 40, 40), rate = 0.7f, velMin = new Vector2(-4, -16), velMax = new Vector2(4, -7), life = new Vector2(2f, 3.5f), a = ember, b = emberEnd, parallax = LightDepth(1255, 335, 1295, 375) });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(380, 790, 80, 60), rate = 1.3f, velMin = new Vector2(-5, -24), velMax = new Vector2(5, -10), life = new Vector2(2.5f, 5f), a = ember, b = emberEnd, parallax = LightDepth(380, 790, 460, 850) });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(320, 255, 40, 30), rate = 0.5f, velMin = new Vector2(-3, -12), velMax = new Vector2(3, -5), life = new Vector2(1.5f, 3f), a = ember, b = emberEnd, parallax = LightDepth(320, 255, 360, 285) });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(0, 880, 1672, 61), rate = 2.2f, velMin = new Vector2(-8, -18), velMax = new Vector2(8, -6), life = new Vector2(3f, 6f), a = new Color(0.95f, 0.3f, 0.16f, 0.75f), b = emberEnd, parallax = 0.9f });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(0, 60, 1672, 820), rate = 2.4f, velMin = new Vector2(-4, -5), velMax = new Vector2(4, 2), life = new Vector2(5f, 9f), a = new Color(0.62f, 0.55f, 0.66f, 0.28f), b = new Color(0.5f, 0.45f, 0.6f, 0f), kind = 1, parallax = 0.2f });
            // brasas grandes bem perto da câmera, nos cantos de baixo (andam mais com o passeio)
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(0, 780, 420, 161), rate = 0.55f, velMin = new Vector2(-10, -42), velMax = new Vector2(10, -22), life = new Vector2(2f, 4f), a = new Color(1f, 0.5f, 0.2f, 0.55f), b = emberEnd, parallax = 1.6f, size = 3 });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1252, 780, 420, 161), rate = 0.55f, velMin = new Vector2(-10, -42), velMax = new Vector2(10, -22), life = new Vector2(2f, 4f), a = new Color(1f, 0.5f, 0.2f, 0.55f), b = emberEnd, parallax = 1.6f, size = 3 });
            bgSparks.Prewarm(6f);

            // correntes bem perto da câmera, nas bordas (silhuetas que andam muito com o passeio)
            chains = anim.gameObject.AddComponent<TitleChains>();
            AddChain(anim, "chain_b", new Vector2(40, -12), 0.020f, 1.05f, 0.3f);
            AddChain(anim, "chain_a", new Vector2(104, -12), 0.026f, 1.31f, 2.1f);
            AddChain(anim, "chain_c", new Vector2(1652, -12), 0.022f, 1.17f, 4.0f);

            var shine = Place("Reflexo", anim, L.title.x0, L.title.y0, L.title.x1 - L.title.x0, L.title.y1 - L.title.y0).gameObject.AddComponent<RawImage>();
            shine.texture = Resources.Load<Texture2D>("UI/Title/title_mask");
            var shineShader = Resources.Load<Shader>("Shaders/UITitleShine");
            if (shineShader != null && shineShader.isSupported)
            {
                shineMat = new Material(shineShader) { hideFlags = HideFlags.DontSave };
                shine.material = shineMat;
                shineMat.SetFloat("_Pos", -2f); shineMat.SetFloat("_Intensity", 1.35f); shineMat.SetFloat("_Width", 0.1f);
            }
            else shine.enabled = false;
            shine.raycastTarget = false;

            // escurece a arte quando Configurações/Controles/Créditos abrem por cima dela
            dim = UIKit.Img("Escurecer", root, null, new Color(0.01f, 0.006f, 0.014f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.enabled = false;
        }

        void AddChain(Transform parent, string tex, Vector2 anchor, float amp, float w, float phase)
        {
            var t = Resources.Load<Texture2D>("UI/Title/" + tex);
            if (t == null) return;
            var rt = Place("Corrente_" + tex, parent, anchor.x, anchor.y, t.width * 2f, t.height * 2f);
            rt.pivot = new Vector2(0.5f, 1f);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = t; img.raycastTarget = false;
            chains.chains.Add(new TitleChains.C { rt = rt, img = img, anchor = anchor, amp = amp, w = w, phase = phase });
        }

        /// <summary>Profundidade média das chamas dentro de uma área (para as brasas que nascem ali).</summary>
        float LightDepth(float x0, float y0, float x1, float y1)
        {
            float sum = 0f; int n = 0;
            foreach (var l in L.lights)
                if (l.x >= x0 - 20 && l.x <= x1 + 20 && l.y >= y0 - 20 && l.y <= y1 + 20) { sum += l.p; n++; }
            return n > 0 ? sum / n : 0.3f;
        }

        /// <summary>Arte escurecida atrás das telas secundárias abertas a partir do menu inicial.</summary>
        public void SetDim(bool on) => dimTarget = on ? 0.62f : 0f;

        Material FogBand(string name, Transform parent, Texture2D tex, float x, float y, float w, float h, Color c)
        {
            var sh = Resources.Load<Shader>("Shaders/UITitleFog");
            if (sh == null || !sh.isSupported || tex == null) return null;
            var ri = Place(name, parent, x, y, w, h).gameObject.AddComponent<RawImage>();
            ri.texture = tex; ri.raycastTarget = false;
            var m = new Material(sh) { hideFlags = HideFlags.DontSave };
            // blocos de 4 px da arte: quantas repetições da textura cabem na faixa
            m.SetVector("_Tiling", new Vector4(w / 4f / tex.width, h / 4f / tex.height, 0, 0));
            m.SetVector("_Band", new Vector4(x, y, w, h));
            m.SetVector("_Panel", new Vector4(L.panel.x0, L.panel.y0, L.panel.x1, L.panel.y1));
            m.SetColor("_FogColor", c);
            ri.material = m;
            return m;
        }

        /// <summary>Os três botões da arte, dentro da tela "Main" do GameMenus.</summary>
        public void BuildButtons(Transform screen, System.Action play, System.Action settings, System.Action quit)
        {
            var art = ArtRoot("ArteBotoes", screen, L);
            var normal = Resources.Load<Sprite>("UI/Title/btn_normal");
            var active = Resources.Load<Sprite>("UI/Title/btn_active");
            var glow = Resources.Load<Sprite>("UI/Title/glow");
            System.Action[] actions = { play, settings, quit };
            string[] names = { "Jogar", "Configuracoes", "Sair" };
            for (int i = 0; i < L.slots.Length && i < 3; i++)
            {
                var s = L.slots[i];
                TitleLabel lab = System.Array.Find(L.labels, x => x.name == s.label);
                float ax = s.x + L.activeOffX, ay = s.y + L.activeOffY;
                var root = Place("Botao_" + names[i], art, ax, ay, L.activeW, L.activeH);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = new Vector2(ax + L.activeW * 0.5f, -(ay + L.activeH * 0.5f));
                var tb = root.gameObject.AddComponent<TitleButton>();
                tb.owner = this; tb.index = i;
                tb.center = new Vector2(ax + L.activeW * 0.5f, ay + L.activeH * 0.5f);
                tb.group = root.gameObject.AddComponent<CanvasGroup>();
                tb.normal = Img("Placa", root, normal, -L.activeOffX, -L.activeOffY, L.normalW, L.normalH, ArtMat);
                tb.active = Img("PlacaAtiva", root, active, 0, 0, L.activeW, L.activeH, ArtMat);
                tb.active.color = new Color(1, 1, 1, 0);
                tb.label = Img("Texto", root, Resources.Load<Sprite>("UI/Title/lbl_" + s.label), lab.x - ax, lab.y - ay, lab.w, lab.h, ArtMat);
                // cor das letras: a apagada vem do SAIR/CONFIGURAÇÕES e a acesa do JOGAR da arte
                var on = System.Array.Find(L.labels, x => x.name == "jogar");
                var off = System.Array.Find(L.labels, x => x.name == "sair");
                tb.labelNormal = new Color(off.r, off.g, off.b, 1f);
                tb.labelActive = new Color(on.r, on.g, on.b, 1f);
                tb.label.color = tb.labelNormal;
                foreach (var g in L.gems)
                {
                    var gi = Img("BrilhoJoia", root, glow, g.x - 18, g.y - 18, 36, 36, AddMat);
                    gi.color = new Color(1f, 0.25f, 0.18f, 0f);
                    tb.gemGlows.Add(gi);
                }
                // área de clique: a placa inteira (com as joias), sem invadir os vizinhos
                var hit = Img("Area", root, null, 8, (L.activeH - L.normalH) * 0.5f, L.activeW - 16, L.normalH, null);
                hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
                var btn = root.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                int idx = i;
                btn.onClick.AddListener(() => actions[idx]?.Invoke());
                tb.button = btn;
                buttons.Add(tb);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count].button;
                nav.selectOnDown = buttons[(i + 1) % buttons.Count].button;
                buttons[i].button.navigation = nav;
            }
            First = buttons.Count > 0 ? buttons[0].button : null;

            btnSparks = Fill("Faiscas", art).gameObject.AddComponent<TitleSparks>();
            btnSparks.material = AddMat; btnSparks.raycastTarget = false;
            for (int k = 0; k < 2; k++)
                btnSparks.emitters.Add(new TitleSparks.Emitter
                {
                    rate = 3.2f, velMin = new Vector2(k == 0 ? -14 : 2, -20), velMax = new Vector2(k == 0 ? -2 : 14, -4),
                    life = new Vector2(0.5f, 1.3f), a = new Color(1f, 0.62f, 0.25f, 1f), b = new Color(0.9f, 0.15f, 0.1f, 0f), kind = 2
                });
            OnSelected(0);
        }

        static Image Img(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, Material mat)
        {
            var im = Place(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            im.sprite = sprite; im.raycastTarget = false;
            if (mat != null) im.material = mat;
            return im;
        }

        public void OnSelected(int i)
        {
            selected = i;
            if (btnSparks == null || i < 0 || i >= L.slots.Length) return;
            var s = L.slots[i];
            float ax = s.x + L.activeOffX, ay = s.y + L.activeOffY;
            for (int k = 0; k < 2 && k < L.gems.Length; k++)
                btnSparks.emitters[k].area = new Rect(ax + L.gems[k].x - 5, ay + L.gems[k].y - 5, 10, 10);
        }

        public void Burst(int i)
        {
            if (btnSparks == null || i < 0 || i >= L.slots.Length) return;
            var s = L.slots[i];
            float ax = s.x + L.activeOffX, ay = s.y + L.activeOffY;
            foreach (var g in L.gems) btnSparks.Burst(new Vector2(ax + g.x, ay + g.y), 22);
        }

        /// <summary>Tela Main acabou de aparecer: reinicia a entrada das luzes e o reflexo.</summary>
        public void OnShown()
        {
            shownAt = TitleClock.Now;
            nextShine = shownAt + 1.1f;
            if (!introPlayed && motionMat != null)
            {
                // primeira vez na sessão: tudo escuro até o sino tocar
                introPlayed = true;
                introState = 1; introT0 = shownAt;
                Debug.Log("[Title] abertura: esperando o sino");
                TitleMotion.RevealRadius = 0f;
                nextShine = 1e9f;
            }
            else if (glows != null) glows.fade = 0f;
            if (nextToll > 1e8f && introState == 0) nextToll = shownAt + Random.Range(9f, 14f);
            bats?.Begin(introState != 0 ? 7f : 4f);
        }

        void Toll(bool intro)
        {
            TitleMotion.Toll(new Vector2(L.emblem.x, L.emblem.y));
            if (ArenAudio.Ready) ArenAudio.PlayUI(Sfx.Bell, intro ? 0.45f : 0.3f, Random.Range(0.6f, 0.7f));
        }

        static bool AnyInputPressed()
        {
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame));
        }

        void UpdateIntroAndToll(float t)
        {
            if (introState == 1 && ((ArenAudio.Ready && t - introT0 > 0.35f) || t - introT0 > 1.6f))
            {
                introState = 2; revealT0 = t;
                Debug.Log("[Title] abertura: sino (áudio pronto=" + ArenAudio.Ready + ")");
                Toll(true);
                nextShine = t + 0.55f;
            }
            if (introState == 2)
            {
                float k = Mathf.Clamp01((t - revealT0) / 2.6f);
                TitleMotion.RevealRadius = 2100f * (1f - (1f - k) * (1f - k) * (1f - k));
                if (k >= 1f) EndIntro(t);
            }
            if (introState != 0 && AnyInputPressed()) { Debug.Log("[Title] abertura pulada por tecla/clique"); EndIntro(t); }   // qualquer tecla pula a abertura
            if (introState == 0 && dimTarget <= 0f && t >= nextToll)
            {
                Toll(false);
                nextToll = t + Random.Range(17f, 25f);
            }
        }

        void EndIntro(float t)
        {
            if (introState == 1) nextShine = t + 0.3f;
            introState = 0;
            TitleMotion.RevealRadius = 1e5f;
            nextToll = t + Random.Range(16f, 22f);
        }

        /// <summary>Testes/vídeo: posição de mouse simulada (px da tela) no lugar do cursor real.</summary>
        public static Vector2? FakeMouse;
        public void DebugToll() => Toll(false);
        public void DebugBats(float delay) => bats?.Begin(delay);

        void UpdateMouse(float dt)
        {
            var m = Mouse.current;
            Vector2 target = Vector2.zero;
            Vector2 mp = FakeMouse ?? (m != null ? m.position.ReadValue() : new Vector2(-1e5f, -1e5f));
            if ((m != null || FakeMouse.HasValue) && artRT != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(artRT, mp, null, out var local))
            {
                var art = new Vector2(local.x + L.width * 0.5f, L.height * 0.5f - local.y);
                TitleMotion.Mouse = art;
                // a câmera "olha" para o lado do cursor: o perto anda para o lado oposto
                target = new Vector2(-Mathf.Clamp((art.x - L.width * 0.5f) / (L.width * 0.5f), -1f, 1f) * 9f,
                                     -Mathf.Clamp((art.y - L.height * 0.5f) / (L.height * 0.5f), -1f, 1f) * 4.5f);
            }
            else TitleMotion.Mouse = new Vector2(-9999, -9999);
            TitleMotion.MouseCam = Vector2.Lerp(TitleMotion.MouseCam, target, 1f - Mathf.Exp(-dt * 2.2f));
        }

        // ------------------------------------------------------------ fundo e câmeras 3D

        struct CamState { public int mask; public CameraClearFlags flags; public Color bg; public bool scaler; }
        readonly Dictionary<Camera, CamState> muted = new Dictionary<Camera, CamState>();

        /// <summary>Mostra/esconde a arte. Com ela na tela a cena 3D fica invisível, então as
        /// câmeras param de desenhar o mundo (só limpam a tela): sobra GPU/CPU para carregar.</summary>
        public void SetBackdrop(bool on)
        {
            if (backdrop == null) return;
            if (on && !backdrop.activeSelf) { if (glows != null) glows.fade = 0f; }
            backdrop.SetActive(on);
            if (on) MuteCameras(); else RestoreCameras();
        }

        void MuteCameras()
        {
            foreach (var c in Camera.allCameras)
            {
                if (c == null || muted.ContainsKey(c)) continue;
                var rs = c.GetComponent<World.RenderScaler>();
                muted[c] = new CamState { mask = c.cullingMask, flags = c.clearFlags, bg = c.backgroundColor, scaler = rs != null && rs.enabled };
                c.cullingMask = 0; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
                if (rs != null) rs.enabled = false;
            }
        }

        void RestoreCameras()
        {
            foreach (var kv in muted)
            {
                var c = kv.Key;
                if (c == null) continue;
                c.cullingMask = kv.Value.mask; c.clearFlags = kv.Value.flags; c.backgroundColor = kv.Value.bg;
                var rs = c.GetComponent<World.RenderScaler>();
                if (rs != null && kv.Value.scaler) rs.enabled = true;
            }
            muted.Clear();
        }

        void OnDestroy() => RestoreCameras();

        void Update()
        {
            if (!BackdropVisible) return;
            MuteCameras();   // câmeras que ligaram depois (ex.: a do jogador) também
            float t = TitleClock.Now;
            UpdateMouse(TitleClock.Delta);
            UpdateIntroAndToll(t);
            TitleMotion.Step(t);
            var cam = TitleMotion.Cam;
            var reveal = new Vector4(TitleMotion.RevealCenter.x, TitleMotion.RevealCenter.y, TitleMotion.RevealRadius, 90f);
            foreach (var b in buttons) b.reveal = TitleMotion.Reveal(b.center, 140f);
            if (motionMat != null)
            {
                // a luz das velas no cenário segue as mesmas rajadas que fazem os halos tremularem
                float gl = Mathf.PerlinNoise(t * 0.55f + 400f * 0.004f, 9.2f), gr = Mathf.PerlinNoise(t * 0.55f + 1450f * 0.004f, 9.2f);
                float fl = Mathf.Clamp01(0.5f + (Mathf.PerlinNoise(t * 6.5f, 3.3f) - 0.5f) * 0.6f + (gl - 0.5f) * 0.9f);
                float fr = Mathf.Clamp01(0.5f + (Mathf.PerlinNoise(t * 6.1f, 7.7f) - 0.5f) * 0.6f + (gr - 0.5f) * 0.9f);
                motionMat.SetVector("_Cam", new Vector4(cam.x, cam.y, TitleMotion.Zoom, t));
                motionMat.SetVector("_Angles", TitleMotion.Angles);
                motionMat.SetVector("_Flicker", new Vector4(fl, fr, 1f, 0f));
                motionMat.SetVector("_Toll", new Vector4(TitleMotion.TollCenter.x, TitleMotion.TollCenter.y, TitleMotion.TollRadius, TitleMotion.TollStrength));
                motionMat.SetVector("_Flash", new Vector4(TitleMotion.Flash * 0.9f, 0, 0, 0));
                motionMat.SetVector("_Reveal", reveal);
            }
            else { TitleMotion.Cam = Vector2.zero; TitleMotion.Zoom = 0f; TitleMotion.Angles = Vector3.zero; cam = Vector2.zero; }
            if (raysMat != null) { raysMat.SetVector("_Cam", new Vector4(cam.x, cam.y, 0, t)); raysMat.SetVector("_Reveal", reveal); }
            if (fogNear != null) fogNear.SetVector("_Reveal", reveal);
            if (fogFar != null) fogFar.SetVector("_Reveal", reveal);
            if (glows != null) glows.fade = Mathf.MoveTowards(glows.fade, 1f, TitleClock.Delta * 0.8f);
            if (dim != null)
            {
                float a = Mathf.MoveTowards(dim.color.a, dimTarget, TitleClock.Delta * 3f);
                UIKit.SetAlpha(dim, a);
                dim.enabled = a > 0.001f;
            }
            // névoa: anda em passos de 2 px da arte (pixel art não desliza meio pixel)
            // (e acompanha a câmera: a névoa do chão está perto, a de cima quase no plano do painel)
            if (fogNear != null) fogNear.SetVector("_Scroll", new Vector4((Snap(t * 5.5f) - cam.x * 0.85f) / 640f, (Snap(t * 3.2f) + cam.x * 0.85f * 0.71f) / 640f, 0, 0));
            if (fogFar != null) fogFar.SetVector("_Scroll", new Vector4((-Snap(t * 2.6f) - cam.x * 0.15f) / 640f, (Snap(t * 1.7f) + cam.x * 0.15f * 0.71f) / 640f, 0, 0));
            // reflexo no título: atravessa em 1,6 s, de 7 em 7 s
            if (shineMat != null)
            {
                if (t >= nextShine) { shineStart = t; nextShine = t + 7.5f; }
                float k = (t - shineStart) / 1.6f;
                shineMat.SetFloat("_Pos", k >= 0f && k <= 1f ? Mathf.Lerp(-0.35f, 1.5f, k) : -2f);
            }
        }

        static float Snap(float v) => Mathf.Floor(v / ArtPixel) * ArtPixel;
    }

    /// <summary>Mantém um retângulo do tamanho da arte escalado para COBRIR o pai (sem faixas pretas).</summary>
    public class TitleArtSpace : MonoBehaviour
    {
        public Vector2 size = new Vector2(1672, 941);
        Vector2 lastParent;

        void LateUpdate()
        {
            var p = transform.parent as RectTransform;
            if (p == null) return;
            var r = p.rect.size;
            if (r == lastParent) return;
            lastParent = r;
            var rt = (RectTransform)transform;
            float s = Mathf.Max(r.x / size.x, r.y / size.y);
            rt.sizeDelta = size;
            rt.localScale = new Vector3(s, s, 1f);
        }

        void OnEnable() => lastParent = Vector2.zero;
    }

    /// <summary>Botão da arte: troca a placa apagada pela vermelha, acende as letras e as
    /// joias das pontas, solta faíscas e toca os sons da UI.</summary>
    public class TitleButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        public TitleScreen owner; public int index; public Button button;
        public Image normal, active, label;
        public readonly List<Image> gemGlows = new List<Image>();
        public Color labelNormal, labelActive;
        public Vector2 center; public CanvasGroup group; public float reveal = 1f;
        bool selected; float k, punch;
        static float lastMove;

        public void OnSelect(BaseEventData e)
        {
            selected = true;
            owner?.OnSelected(index);
            if (TitleClock.Now - lastMove > 0.05f) ArenAudio.PlayUI(Sfx.UIMove, 0.5f);
            lastMove = TitleClock.Now;
        }
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerEnter(PointerEventData e) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject); }
        public void OnSubmit(BaseEventData e) => Press();
        public void OnPointerClick(PointerEventData e) => Press();

        void Press()
        {
            ArenAudio.PlayUI(Sfx.UIConfirm, 0.7f);
            punch = 1f;
            owner?.Burst(index);
        }

        void OnDisable() { k = selected ? 1f : 0f; }

        void Update()
        {
            float dt = TitleClock.Delta;
            k = Mathf.MoveTowards(k, selected ? 1f : 0f, dt * 9f);
            float e = k * k * (3f - 2f * k);
            if (active != null) active.color = new Color(1, 1, 1, e);
            if (normal != null) normal.color = new Color(1, 1, 1, 1f - e * 0.85f);
            if (label != null) label.color = Color.Lerp(labelNormal, labelActive, e);
            float t = TitleClock.Now;
            float pulse = Mathf.Floor((0.55f + 0.3f * Mathf.Sin(t * 3.1f) + 0.15f * Mathf.Sin(t * 7.7f + 1.3f)) * 6f) / 6f;
            foreach (var g in gemGlows) g.color = new Color(1f, 0.28f, 0.18f, e * pulse * 0.9f);
            if (group != null) group.alpha = reveal;   // aparece quando a luz da abertura chega
            punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
            transform.localScale = Vector3.one * (1f - 0.025f * Mathf.Sin(punch * Mathf.PI));
        }
    }
}

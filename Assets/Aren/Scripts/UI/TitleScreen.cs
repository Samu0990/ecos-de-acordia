using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aren.UI
{
    // ---------------------------------------------------------------- layout (title_layout.json)
    [System.Serializable] public class TitleLayout
    {
        public int width, height, activeW, activeH, normalW, normalH, activeOffX, activeOffY;
        public TitleSlot[] slots; public TitleLabel[] labels; public TitlePt[] gems;
        public TitleBox title, panel; public TitlePt emblem; public TitleLight[] lights;
    }
    [System.Serializable] public class TitleSlot { public int x, y; public string label; }
    [System.Serializable] public class TitleLabel { public string name; public int x, y, w, h; public float r, g, b; }
    [System.Serializable] public class TitlePt { public float x, y; }
    [System.Serializable] public class TitleBox { public float x0, y0, x1, y1; }
    [System.Serializable] public class TitleLight { public float x, y, r, i; public int t; }

    /// <summary>
    /// Tela inicial fiel à arte de referência do autor (Assets/Aren/Resources/UI/Title, gerada
    /// por ArtSource/Menu/scripts/build_title.py). A arte é a própria imagem; por cima dela:
    /// os três botões recortados da arte (placa apagada / placa vermelha com joias), e os
    /// detalhes animados em pixel art — chamas tremulando, brasas e poeira subindo, névoa no
    /// chão, joias pulsando e um reflexo que passa pelas letras do título.
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
        Material fogNear, fogFar, shineMat;
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
            var bg = Place("Fundo", art, 0, 0, L.width, L.height).gameObject.AddComponent<RawImage>();
            bg.texture = Resources.Load<Texture2D>("UI/Title/title_bg");
            bg.material = ArtMat; bg.raycastTarget = false;

            // camada animada num Canvas próprio: redesenhar as chamas a cada quadro não
            // obriga a reconstruir o resto do menu
            var anim = Fill("Animado", art);
            anim.gameObject.AddComponent<Canvas>();

            glows = Fill("Luzes", anim).gameObject.AddComponent<TitleGlows>();
            glows.tex = Resources.Load<Sprite>("UI/Title/glow")?.texture;
            glows.material = AddMat; glows.raycastTarget = false;
            foreach (var l in L.lights) glows.Add(l.x, l.y, l.r, l.i, l.t);
            glows.Add(L.emblem.x, L.emblem.y, 30f, 0.55f, 2);

            var fogTex = Resources.Load<Texture2D>("UI/Title/fog");
            fogFar = FogBand("NevoaAlta", anim, fogTex, 0, 560, L.width, 210, new Color(0.30f, 0.26f, 0.38f, 0.10f));
            fogNear = FogBand("NevoaChao", anim, fogTex, 0, 765, L.width, 176, new Color(0.36f, 0.31f, 0.44f, 0.22f));

            bgSparks = Fill("Brasas", anim).gameObject.AddComponent<TitleSparks>();
            bgSparks.material = AddMat; bgSparks.raycastTarget = false;
            bgSparks.avoid = new Rect(L.panel.x0, L.panel.y0, L.panel.x1 - L.panel.x0, L.panel.y1 - L.panel.y0);
            var ember = new Color(1f, 0.55f, 0.22f, 0.9f); var emberEnd = new Color(0.85f, 0.12f, 0.1f, 0f);
            // velas do canto direito, altar, lustre, vela da esquerda, chão e poeira
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1450, 640, 150, 90), rate = 2.6f, velMin = new Vector2(-6, -26), velMax = new Vector2(6, -12), life = new Vector2(2.5f, 5f), a = ember, b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1340, 500, 150, 70), rate = 1.2f, velMin = new Vector2(-5, -20), velMax = new Vector2(5, -9), life = new Vector2(2f, 4f), a = ember, b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(1255, 335, 40, 40), rate = 0.7f, velMin = new Vector2(-4, -16), velMax = new Vector2(4, -7), life = new Vector2(2f, 3.5f), a = ember, b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(380, 790, 80, 60), rate = 1.3f, velMin = new Vector2(-5, -24), velMax = new Vector2(5, -10), life = new Vector2(2.5f, 5f), a = ember, b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(320, 255, 40, 30), rate = 0.5f, velMin = new Vector2(-3, -12), velMax = new Vector2(3, -5), life = new Vector2(1.5f, 3f), a = ember, b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(0, 880, 1672, 61), rate = 2.2f, velMin = new Vector2(-8, -18), velMax = new Vector2(8, -6), life = new Vector2(3f, 6f), a = new Color(0.95f, 0.3f, 0.16f, 0.75f), b = emberEnd });
            bgSparks.emitters.Add(new TitleSparks.Emitter { area = new Rect(0, 60, 1672, 820), rate = 2.4f, velMin = new Vector2(-4, -5), velMax = new Vector2(4, 2), life = new Vector2(5f, 9f), a = new Color(0.62f, 0.55f, 0.66f, 0.28f), b = new Color(0.5f, 0.45f, 0.6f, 0f), kind = 1 });
            bgSparks.Prewarm(6f);

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
            shownAt = Time.unscaledTime;
            nextShine = shownAt + 1.1f;
            if (glows != null) glows.fade = 0f;
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
            float t = Time.unscaledTime;
            if (glows != null) glows.fade = Mathf.MoveTowards(glows.fade, 1f, Time.unscaledDeltaTime * 0.8f);
            if (dim != null)
            {
                float a = Mathf.MoveTowards(dim.color.a, dimTarget, Time.unscaledDeltaTime * 3f);
                UIKit.SetAlpha(dim, a);
                dim.enabled = a > 0.001f;
            }
            // névoa: anda em passos de 2 px da arte (pixel art não desliza meio pixel)
            if (fogNear != null) fogNear.SetVector("_Scroll", new Vector4(Snap(t * 5.5f) / (4f * 160f), Snap(t * 3.2f) / (4f * 160f), 0, 0));
            if (fogFar != null) fogFar.SetVector("_Scroll", new Vector4(-Snap(t * 2.6f) / (4f * 160f), Snap(t * 1.7f) / (4f * 160f), 0, 0));
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

    /// <summary>Base: um Graphic que desenha muitos quads em coordenadas da arte (y para baixo)
    /// numa malha só — um draw call para todas as chamas/partículas.</summary>
    public abstract class TitleQuads : MaskableGraphic
    {
        public Texture tex;
        public override Texture mainTexture => tex != null ? tex : Texture2D.whiteTexture;

        protected static void Quad(VertexHelper vh, float x, float y, float w, float h, Color32 c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x, -y - h), c, new Vector2(0, 0));
            vh.AddVert(new Vector3(x, -y), c, new Vector2(0, 1));
            vh.AddVert(new Vector3(x + w, -y), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(x + w, -y - h), c, new Vector2(1, 0));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        protected static float Snap(float v) => Mathf.Round(v / TitleScreen.ArtPixel) * TitleScreen.ArtPixel;

        void Update() { Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f)); SetVerticesDirty(); }
        protected virtual void Tick(float dt) { }
    }

    /// <summary>Halos das velas, lanternas, brasas e do brasão: tremulam com ruído, em degraus.</summary>
    public class TitleGlows : TitleQuads
    {
        struct L { public float x, y, r, i, seed; public int t; }
        readonly List<L> lights = new List<L>();
        public float fade = 1f;
        static readonly Color Flame = new Color(1f, 0.56f, 0.24f), Ember = new Color(0.95f, 0.2f, 0.14f), Gold = new Color(1f, 0.78f, 0.42f);

        public void Add(float x, float y, float r, float i, int t) => lights.Add(new L { x = x, y = y, r = r, i = i, t = t, seed = lights.Count * 7.31f + 3.7f });

        float Flicker(in L l, float t)
        {
            switch (l.t)
            {
                case 0:   // chama: ruído rápido + rajadas de vento que passam pelas velas próximas juntas
                    float n = Mathf.PerlinNoise(t * 6.5f + l.seed, l.seed * 0.37f);
                    float n2 = Mathf.PerlinNoise(t * 17f + l.seed * 2f, 3.1f);
                    float gust = Mathf.PerlinNoise(t * 0.55f + l.x * 0.004f, 9.2f);
                    return Mathf.Clamp01((0.55f + 0.45f * n + 0.15f * (n2 - 0.5f)) * (0.75f + 0.35f * gust));
                case 1:   // brasa distante: respira devagar
                    return 0.45f + 0.55f * Mathf.PerlinNoise(t * 0.9f + l.seed, 1.7f);
                default:  // brasão dourado
                    return 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.15f + l.seed));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = Time.unscaledTime;
            foreach (var l in lights)
            {
                float f = Mathf.Floor(Flicker(l, t) * 8f) / 8f * fade;   // degraus de paleta
                var c = l.t == 0 ? Flame : l.t == 1 ? Ember : Gold;
                float halo = Snap(l.r * (l.t == 2 ? 2.2f : 2.6f));
                c.a = (l.t == 1 ? 0.16f : l.t == 2 ? 0.16f : 0.2f) * l.i * f;
                Quad(vh, Snap(l.x) - halo, Snap(l.y) - halo, halo * 2, halo * 2, c);
                if (l.t == 0)
                {
                    float core = Snap(Mathf.Max(4f, l.r * 0.55f));
                    c.a = 0.55f * l.i * f;
                    Quad(vh, Snap(l.x) - core, Snap(l.y) - core, core * 2, core * 2, c);
                }
            }
        }
    }

    /// <summary>Partículas quadradas em pixel art: brasas que sobem, poeira e faíscas das joias.</summary>
    public class TitleSparks : TitleQuads
    {
        public class Emitter
        {
            public Rect area; public float rate; public Vector2 velMin, velMax, life;
            public Color a, b; public int kind; public bool on = true; internal float acc;
        }
        struct P { public Vector2 pos, vel; public float age, life, seed; public Color a, b; public int kind, size; }
        public readonly List<Emitter> emitters = new List<Emitter>();
        public Rect avoid;   // painel central: as brasas do fundo apagam ao entrar nele
        readonly List<P> parts = new List<P>();
        const int Max = 260;

        void Spawn(Emitter e, Vector2? at = null)
        {
            if (parts.Count >= Max) return;
            var p = new P
            {
                pos = at ?? new Vector2(Random.Range(e.area.xMin, e.area.xMax), Random.Range(e.area.yMin, e.area.yMax)),
                vel = new Vector2(Random.Range(e.velMin.x, e.velMax.x), Random.Range(e.velMin.y, e.velMax.y)),
                life = Random.Range(e.life.x, e.life.y), seed = Random.value * 100f,
                a = e.a, b = e.b, kind = e.kind, size = e.kind == 1 || Random.value < 0.75f ? 1 : 2,
            };
            if (avoid.width > 0 && avoid.Contains(p.pos)) return;
            parts.Add(p);
        }

        public void Burst(Vector2 at, int n)
        {
            var e = emitters.Count > 0 ? emitters[0] : null;
            if (e == null) return;
            for (int i = 0; i < n && parts.Count < Max; i++)
            {
                var p = new P
                {
                    pos = at + Random.insideUnitCircle * 4f, vel = Random.insideUnitCircle.normalized * Random.Range(20f, 55f) + new Vector2(0, -12f),
                    life = Random.Range(0.45f, 1.1f), seed = Random.value * 100f, a = e.a, b = e.b, kind = 2, size = Random.value < 0.45f ? 1 : 2,
                };
                parts.Add(p);
            }
        }

        public void Prewarm(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.05f) Tick(0.05f);
        }

        protected override void Tick(float dt)
        {
            float t = Time.unscaledTime;
            foreach (var e in emitters)
            {
                if (!e.on) continue;
                e.acc += e.rate * dt;
                while (e.acc >= 1f) { e.acc -= 1f; Spawn(e); }
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.age += dt;
                switch (p.kind)
                {
                    case 0:   // brasa: sobe balançando e desacelera
                        p.pos += p.vel * dt + new Vector2(Mathf.Sin(t * 1.9f + p.seed) * 7f * dt, 0);
                        p.vel *= 1f - 0.12f * dt;
                        break;
                    case 1:   // poeira: deriva lenta
                        p.vel += new Vector2(Mathf.PerlinNoise(t * 0.3f, p.seed) - 0.5f, Mathf.PerlinNoise(p.seed, t * 0.3f) - 0.5f) * 6f * dt;
                        p.pos += p.vel * dt;
                        break;
                    default:  // faísca da joia: espalha e cai um pouco
                        p.pos += p.vel * dt;
                        p.vel *= 1f - 2.4f * dt;
                        p.vel.y += 10f * dt;
                        break;
                }
                if (avoid.width > 0 && p.kind != 2 && avoid.Contains(p.pos)) p.life = Mathf.Min(p.life, p.age + 0.15f);
                if (p.age >= p.life) { parts.RemoveAt(i); continue; }
                parts[i] = p;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = Time.unscaledTime;
            float px = TitleScreen.ArtPixel;
            foreach (var p in parts)
            {
                float k = p.age / p.life;
                var c = Color.Lerp(p.a, p.b, k);
                float fade = Mathf.Min(1f, p.age / 0.35f) * Mathf.Min(1f, (p.life - p.age) / 0.5f);
                if (p.kind == 0) fade *= 0.75f + 0.25f * Mathf.Sign(Mathf.Sin(t * 13f + p.seed));   // cintila
                c.a *= Mathf.Floor(fade * 5f) / 5f;
                if (c.a <= 0.01f) continue;
                float s = p.size * px;
                Quad(vh, Snap(p.pos.x), Snap(p.pos.y), s, s, c);
            }
        }
    }

    /// <summary>Botão da arte: troca a placa apagada pela vermelha, acende as letras e as
    /// joias das pontas, solta faíscas e toca os sons da UI.</summary>
    public class TitleButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        public TitleScreen owner; public int index; public Button button;
        public Image normal, active, label;
        public readonly List<Image> gemGlows = new List<Image>();
        public Color labelNormal, labelActive;
        bool selected; float k, punch;
        static float lastMove;

        public void OnSelect(BaseEventData e)
        {
            selected = true;
            owner?.OnSelected(index);
            if (Time.unscaledTime - lastMove > 0.05f) ArenAudio.PlayUI(Sfx.UIMove, 0.5f);
            lastMove = Time.unscaledTime;
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
            float dt = Time.unscaledDeltaTime;
            k = Mathf.MoveTowards(k, selected ? 1f : 0f, dt * 9f);
            float e = k * k * (3f - 2f * k);
            if (active != null) active.color = new Color(1, 1, 1, e);
            if (normal != null) normal.color = new Color(1, 1, 1, 1f - e * 0.85f);
            if (label != null) label.color = Color.Lerp(labelNormal, labelActive, e);
            float t = Time.unscaledTime;
            float pulse = Mathf.Floor((0.55f + 0.3f * Mathf.Sin(t * 3.1f) + 0.15f * Mathf.Sin(t * 7.7f + 1.3f)) * 6f) / 6f;
            foreach (var g in gemGlows) g.color = new Color(1f, 0.28f, 0.18f, e * pulse * 0.9f);
            punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
            transform.localScale = Vector3.one * (1f - 0.025f * Mathf.Sin(punch * Mathf.PI));
        }
    }
}

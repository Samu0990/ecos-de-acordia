using System.Collections.Generic;
using Aren.Combat;
using Aren.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// HUD de combate (doc §30–32): vida com rastro de dano, Ressonância como onda viva,
    /// 4 habilidades com recarga radial + flash quando ficam prontas, Cadência (combo),
    /// alvo, barras de inimigos/chefe, objetivo, nome da área, dicas e vinheta de dano.
    /// Ocupa só os cantos; o centro da tela é do jogo.
    /// </summary>
    public class ArenHUD : MonoBehaviour
    {
        public static ArenHUD Instance { get; private set; }

        Canvas canvas;
        CanvasGroup rootGroup;
        ArenHealth health; ArenCombat combat; ArenAbilities abilities;

        // vida / ressonância
        Image hpFill, hpGhost, emblem, emblemGlow;
        UIWaveform resWave;
        Text resText;
        float ghost = 1f, ghostHold;

        // habilidades
        class Slot
        {
            public RectTransform root; public Image bg, ring, icon, cd, glow; public Text key, cost, timer;
            public Image[] pips; public float flash; public bool wasReady;
        }
        readonly Slot[] slots = new Slot[4];

        // cadência
        RectTransform chainRoot; Text chainNum, chainLabel; Image chainBar; float chainPunch; int lastChain;
        CanvasGroup chainGroup;

        // alvo / inimigos
        RectTransform targetMarker;
        class EnemyBar { public RectTransform root; public Image fill, bg; public EnemyBase enemy; }
        readonly List<EnemyBar> bars = new List<EnemyBar>();
        RectTransform bossRoot; Image bossFill, bossGhost; Text bossName, bossSub; CanvasGroup bossGroup; float bossGhostV = 1f;

        // textos
        Text objective; CanvasGroup objGroup; RectTransform objRoot;
        Text areaTitle, areaSub; CanvasGroup areaGroup; float areaT = -1f;
        Text hint; CanvasGroup hintGroup; float hintUntil;
        Image vignette; float hurtFlash;
        Text fpsText; float fpsAcc; int fpsFrames; float fpsShown;
        Text toast; CanvasGroup toastGroup; float toastUntil;

        static readonly string[] Icons = { "icon_pulso", "icon_lamina", "icon_eco", "icon_contracanto" };
        static readonly Color[] SlotColors = { UIKit.Cyan, new Color(0.7f, 1f, 0.95f), UIKit.Violet, UIKit.Gold };

        void Awake()
        {
            Instance = this;
            canvas = UIKit.MakeCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            rootGroup.interactable = false; rootGroup.blocksRaycasts = false;
            Build();
        }

        public void Bind(GameObject player)
        {
            health = player.GetComponent<ArenHealth>();
            combat = player.GetComponent<ArenCombat>();
            abilities = player.GetComponent<ArenAbilities>();
            if (health != null) health.OnDamaged += (d, h) => { hurtFlash = 1f; };
            if (abilities != null) abilities.OnDenied += id => slots[(int)id].flash = -1f;
        }

        public void SetVisible(bool v) { rootGroup.alpha = v ? 1f : 0f; }

        // ------------------------------------------------------------ montagem

        void Build()
        {
            var t = canvas.transform;
            vignette = UIKit.Img("Vinheta", t, "ui_vignette", new Color(0.6f, 0.02f, 0.06f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var vr = vignette.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.sizeDelta = Vector2.zero;

            // --- canto superior esquerdo: composição da referência (brasão + Vida + Ressonância)
            var bl = UIKit.Rect("VidaRessonancia", t, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -34), new Vector2(560, 150));
            emblemGlow = UIKit.Img("EmblemaBrilho", bl, "ui_glow", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.0f), new Vector2(0, 0), new Vector2(62, 72), new Vector2(190, 190));
            UIKit.Img("EmblemaFundo", bl, "ui_disc", new Color(0.04f, 0.03f, 0.05f, 0.82f), new Vector2(0, 0), new Vector2(62, 72), new Vector2(112, 112));
            emblem = UIKit.Img("Emblema", bl, "ui_emblem", UIKit.Gold, new Vector2(0, 0), new Vector2(62, 72), new Vector2(118, 118));

            UIKit.Label("Nome", bl, UIKit.Spaced("VIDA"), UIKit.SerifBold, 19, UIKit.Bone, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(330, 128), new Vector2(360, 30));
            var hpBg = UIKit.Img("VidaFundo", bl, "ui_bar", new Color(0, 0, 0, 0.65f), new Vector2(0, 0), new Vector2(330, 102), new Vector2(370, 16), Image.Type.Sliced);
            hpGhost = UIKit.Img("VidaRastro", hpBg.transform, "ui_bar", new Color(1f, 0.92f, 0.8f, 0.85f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(370, 16), Image.Type.Sliced);
            hpGhost.rectTransform.pivot = new Vector2(0, 0.5f);
            hpFill = UIKit.Img("Vida", hpBg.transform, "ui_bar", new Color(0.82f, 0.16f, 0.24f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(370, 16), Image.Type.Sliced);
            hpFill.rectTransform.pivot = new Vector2(0, 0.5f);
            UIKit.Img("VidaMoldura", hpBg.transform, "ui_bar_frame", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(374, 20), Image.Type.Sliced);

            var resRt = UIKit.Rect("Ressonancia", bl, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(330, 58), new Vector2(370, 46));
            resWave = resRt.gameObject.AddComponent<UIWaveform>();
            resWave.color = UIKit.Cyan; resWave.thickness = 3.2f; resWave.cycles = 7f; resWave.raycastTarget = false;
            resText = UIKit.Label("ResValor", bl, "50", UIKit.SansBold, 18, UIKit.Cyan, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(545, 58), new Vector2(60, 30));
            UIKit.Label("ResRotulo", bl, UIKit.Spaced("RESSONÂNCIA"), UIKit.Sans, 12, UIKit.Muted, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(330, 26), new Vector2(370, 20));
            // marcas de custo das habilidades na onda
            foreach (var c in new[] { 15f, 25f, 35f })
                UIKit.Img("Marca" + c, resRt, "ui_bar", new Color(1, 1, 1, 0.22f), new Vector2(c / 100f, 0.5f), Vector2.zero, new Vector2(2, 34));

            // --- canto inferior direito: habilidades
            var br = UIKit.Rect("Habilidades", t, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 44), new Vector2(460, 150));
            string[] keys = { "Q", "E", "R", "SEGURAR" };
            for (int i = 0; i < 4; i++)
            {
                var s = new Slot();
                float x = 58 + i * 112;
                s.root = UIKit.Rect("Slot" + i, br, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(x, 82), new Vector2(96, 96));
                s.glow = UIKit.Img("Brilho", s.root, "ui_glow", new Color(1, 1, 1, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
                s.bg = UIKit.Img("Fundo", s.root, "ui_disc", new Color(0.05f, 0.04f, 0.07f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88));
                // Os ícones finais já trazem sua própria paleta. Mantê-los brancos aqui
                // evita uma segunda tintura que reduziria contraste e legibilidade.
                s.icon = UIKit.Img("Icone", s.root, Icons[i], Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62, 62));
                s.cd = UIKit.Img("Recarga", s.root, "ui_disc", new Color(0, 0, 0, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88), Image.Type.Filled);
                s.cd.fillMethod = Image.FillMethod.Radial360; s.cd.fillOrigin = (int)Image.Origin360.Top; s.cd.fillClockwise = false;
                s.ring = UIKit.Img("Anel", s.root, "ui_ring", UIKit.GoldDim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
                s.timer = UIKit.Label("Tempo", s.root, "", UIKit.SansBold, 22, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 40));
                s.key = UIKit.Label("Tecla", s.root, keys[i], UIKit.SansBold, i == 3 ? 12 : 17, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -64), new Vector2(110, 24));
                s.cost = UIKit.Label("Custo", s.root, "", UIKit.Sans, 13, UIKit.Cyan, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 58), new Vector2(80, 20));
                if (i == 3)
                {
                    s.pips = new Image[3];
                    for (int k = 0; k < 3; k++)
                        s.pips[k] = UIKit.Img("Nivel" + k, s.root, "ui_diamond", new Color(1, 1, 1, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(-22 + k * 22, 30), new Vector2(14, 14));
                }
                slots[i] = s;
            }

            // --- cadência (direita, meio)
            chainRoot = UIKit.Rect("Cadencia", t, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-60, 90), new Vector2(260, 140));
            chainGroup = chainRoot.gameObject.AddComponent<CanvasGroup>(); chainGroup.alpha = 0;
            chainNum = UIKit.Label("Numero", chainRoot, "0", UIKit.Display, 76, UIKit.Bone, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(-130, 10), new Vector2(260, 100));
            chainLabel = UIKit.Label("Rotulo", chainRoot, UIKit.Spaced("CADÊNCIA"), UIKit.Sans, 15, UIKit.Gold, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(-130, -42), new Vector2(260, 24));
            var cb = UIKit.Img("TempoFundo", chainRoot, "ui_bar", new Color(1, 1, 1, 0.12f), new Vector2(1, 0.5f), new Vector2(-70, -60), new Vector2(140, 4), Image.Type.Sliced);
            chainBar = UIKit.Img("Tempo", cb.transform, "ui_bar", UIKit.Gold, new Vector2(1, 0.5f), Vector2.zero, new Vector2(140, 4), Image.Type.Sliced);
            chainBar.rectTransform.pivot = new Vector2(1, 0.5f);

            // --- alvo
            targetMarker = UIKit.Img("Alvo", t, "ui_diamond", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.85f), new Vector2(0, 0), Vector2.zero, new Vector2(18, 18)).rectTransform;

            // --- chefe (topo)
            bossRoot = UIKit.Rect("Chefe", t, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(760, 90));
            bossGroup = bossRoot.gameObject.AddComponent<CanvasGroup>(); bossGroup.alpha = 0;
            bossName = UIKit.Label("Nome", bossRoot, "", UIKit.SerifBold, 24, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(760, 34));
            bossSub = UIKit.Label("Sub", bossRoot, "", UIKit.Serif, 14, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(760, 22));
            var bb = UIKit.Img("Fundo", bossRoot, "ui_bar", new Color(0, 0, 0, 0.7f), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(620, 12), Image.Type.Sliced);
            bossGhost = UIKit.Img("Rastro", bb.transform, "ui_bar", new Color(1f, 0.9f, 0.8f, 0.8f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(620, 12), Image.Type.Sliced);
            bossGhost.rectTransform.pivot = new Vector2(0, 0.5f);
            bossFill = UIKit.Img("Vida", bb.transform, "ui_bar", new Color(0.55f, 0.12f, 0.6f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(620, 12), Image.Type.Sliced);
            bossFill.rectTransform.pivot = new Vector2(0, 0.5f);
            UIKit.Img("Moldura", bb.transform, "ui_bar_frame", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(624, 16), Image.Type.Sliced);

            // --- objetivo (topo esquerdo)
            objRoot = UIKit.Rect("Objetivo", t, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -205), new Vector2(560, 80));
            objGroup = objRoot.gameObject.AddComponent<CanvasGroup>(); objGroup.alpha = 0;
            UIKit.Img("Marca", objRoot, "ui_diamond", UIKit.Gold, new Vector2(0, 1), new Vector2(10, -16), new Vector2(14, 14));
            UIKit.Label("Rotulo", objRoot, UIKit.Spaced("OBJETIVO"), UIKit.Sans, 12, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(300, -14), new Vector2(560, 20));
            objective = UIKit.Label("Texto", objRoot, "", UIKit.Serif, 20, UIKit.Bone, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(300, -48), new Vector2(560, 50));

            // --- nome da área (centro alto)
            var ar = UIKit.Rect("Area", t, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 160));
            areaGroup = ar.gameObject.AddComponent<CanvasGroup>(); areaGroup.alpha = 0;
            areaTitle = UIKit.Label("Titulo", ar, "", UIKit.Display, 58, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(1200, 80));
            UIKit.Img("Ornamento", ar, "ui_ornament", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(520, 32));
            areaSub = UIKit.Label("Sub", ar, "", UIKit.Serif, 20, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -54), new Vector2(1200, 30));

            // --- dica (centro baixo)
            var hr = UIKit.Rect("Dica", t, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(900, 60));
            hintGroup = hr.gameObject.AddComponent<CanvasGroup>(); hintGroup.alpha = 0;
            var hp = UIKit.Img("Fundo", hr, "ui_panel", new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 56), Image.Type.Sliced);
            hint = UIKit.Label("Texto", hr, "", UIKit.Serif, 20, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 50));

            // --- aviso curto (esquiva perfeita, contra-ataque)
            var tr = UIKit.Rect("Aviso", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(700, 50));
            toastGroup = tr.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0;
            toast = UIKit.Label("Texto", tr, "", UIKit.SerifBold, 26, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 50));

            fpsText = UIKit.Label("FPS", t, "", UIKit.Sans, 14, UIKit.Muted, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-70, -22), new Vector2(120, 24));
        }

        // ------------------------------------------------------------ API

        public void ShowObjective(string text)
        {
            objective.text = text;
            objGroup.alpha = text.Length > 0 ? 1 : 0;
            objRoot.anchoredPosition = new Vector2(44, -205);
            ArenAudio.PlayUI(Sfx.UIConfirm, 0.5f);
        }

        public void ShowArea(string title, string sub)
        {
            areaTitle.text = UIKit.Spaced(title.ToUpperInvariant(), 1);
            areaSub.text = sub;
            areaT = 0f;
        }

        public void ShowHint(string text, float seconds = 5f)
        {
            hint.text = text;
            hintUntil = Time.unscaledTime + seconds;
        }

        public void HideHint() => hintUntil = 0f;

        public void Toast(string text, Color color, float seconds = 1.2f)
        {
            toast.text = text; toast.color = color; toastUntil = Time.unscaledTime + seconds;
            toast.rectTransform.localScale = Vector3.one * 1.3f;
        }

        // ------------------------------------------------------------ atualização

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateFps(dt);
            if (health == null) return;

            // vida + rastro (o rastro espera um instante e desce: lê o tamanho do golpe)
            float hp = health.Normalized;
            hpFill.rectTransform.sizeDelta = new Vector2(370f * hp, 16);
            if (hp < ghost) { if (ghostHold <= 0) ghostHold = 0.45f; ghostHold -= dt; if (ghostHold <= 0) ghost = Mathf.MoveTowards(ghost, hp, dt * 0.6f); }
            else { ghost = hp; ghostHold = 0; }
            hpGhost.rectTransform.sizeDelta = new Vector2(370f * ghost, 16);
            hpFill.color = Color.Lerp(new Color(0.82f, 0.16f, 0.24f), new Color(1f, 0.35f, 0.3f), hp < 0.3f ? (Mathf.Sin(Time.unscaledTime * 8) * 0.5f + 0.5f) : 0);

            // ressonância: amplitude cresce com o recurso, cor vira ouro quando cheia
            float r = abilities != null ? abilities.ResonanceNormalized : 0;
            Color rc = Color.Lerp(UIKit.Cyan, UIKit.Gold, Mathf.InverseLerp(0.8f, 1f, r));
            resWave.color = rc;
            float jit = abilities != null && abilities.Charging ? 0.25f * (abilities.ChargeLevel + 1) : 0f;
            resWave.Set(0.15f + r * 0.85f, r, Time.unscaledTime * (0.6f + r * 1.6f), jit);
            resText.text = Mathf.FloorToInt(abilities != null ? abilities.Resonance : 0).ToString();
            resText.color = rc;
            float glowA = r >= 0.999f ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4) : (abilities != null && abilities.EchoActive ? 0.3f : 0f);
            UIKit.SetAlpha(emblemGlow, Mathf.MoveTowards(emblemGlow.color.a, glowA, dt * 2f));
            emblem.color = abilities != null && abilities.EchoActive ? Color.Lerp(UIKit.Gold, UIKit.Violet, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6)) : UIKit.Gold;

            UpdateSlots(dt);
            UpdateChain(dt);
            UpdateTargets();
            UpdateTexts(dt);

            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 2.2f);
            float low = hp < 0.3f ? (0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)) * (1f - hp / 0.3f) : 0f;
            UIKit.SetAlpha(vignette, Mathf.Max(hurtFlash * 0.75f, low));
        }

        void UpdateSlots(float dt)
        {
            if (abilities == null) return;
            for (int i = 0; i < 4; i++)
            {
                var s = slots[i];
                var id = (AbilityId)i;
                var def = abilities.Def(id);
                float cdN = abilities.CooldownNormalized(id);
                bool afford = abilities.CanAfford(id);
                bool ready = cdN <= 0f && afford;
                s.cd.fillAmount = cdN;
                float left = abilities.CooldownLeft(id);
                s.timer.text = left > 0.05f ? (left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0")) : "";
                s.cost.text = Mathf.RoundToInt(i == 3 ? abilities.chargeCosts[0] : def.cost).ToString();
                s.cost.color = afford ? UIKit.Cyan : UIKit.Crimson;
                Color ic = Color.white;
                if (!afford) ic = new Color(0.35f, 0.35f, 0.35f, 0.8f);
                s.icon.color = ic;
                // ficou pronto: flash + som + "soco" de escala
                if (ready && !s.wasReady) { s.flash = 1f; ArenAudio.PlayUI(Sfx.AbilityReady, 0.35f, 1f + i * 0.08f); }
                s.wasReady = ready;
                bool active = (id == AbilityId.Eco && abilities.EchoActive) || (id == AbilityId.Contracanto && abilities.Charging);
                Color ringC = active ? SlotColors[i] : (ready ? UIKit.Gold : UIKit.GoldDim * 0.7f);
                if (s.flash < 0f) { ringC = UIKit.Crimson; s.flash = Mathf.MoveTowards(s.flash, 0f, dt * 3f); }
                else s.flash = Mathf.MoveTowards(s.flash, 0f, dt * 2.5f);
                s.ring.color = ringC;
                float f = Mathf.Max(0f, s.flash);
                UIKit.SetAlpha(s.glow, active ? 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 8) : f * 0.8f);
                s.glow.color = new Color(SlotColors[i].r, SlotColors[i].g, SlotColors[i].b, s.glow.color.a);
                s.root.localScale = Vector3.one * (1f + f * 0.18f + (active ? 0.05f : 0f));
                if (id == AbilityId.Eco && abilities.EchoActive)
                    s.timer.text = Mathf.CeilToInt(abilities.EchoTimeLeft).ToString();
                if (s.pips != null)
                    for (int k = 0; k < 3; k++)
                        s.pips[k].color = abilities.Charging && abilities.ChargeLevel > k ? UIKit.Gold : new Color(1, 1, 1, abilities.Charging ? 0.35f : 0.12f);
            }
        }

        void UpdateChain(float dt)
        {
            if (combat == null) return;
            int c = combat.ChainCount;
            if (c != lastChain)
            {
                if (c > lastChain) chainPunch = 1f;
                lastChain = c;
            }
            chainPunch = Mathf.MoveTowards(chainPunch, 0f, dt * 5f);
            float target = c >= 2 ? 1f : 0f;
            chainGroup.alpha = Mathf.MoveTowards(chainGroup.alpha, target, dt * (target > 0 ? 8f : 2f));
            if (c >= 2) chainNum.text = c.ToString();
            chainNum.rectTransform.localScale = Vector3.one * (1f + chainPunch * 0.25f);
            chainNum.color = Color.Lerp(UIKit.Bone, UIKit.Gold, Mathf.Clamp01((c - 10) / 20f));
            chainBar.rectTransform.sizeDelta = new Vector2(140f * Mathf.Clamp01(combat.ChainTimeLeft / combat.chainTimeout), 4);
        }

        void UpdateTargets()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // marcador do alvo
            var tgt = combat != null ? combat.Target : null;
            if (tgt != null && CombatRegistry.IsValid(tgt) && combat.InCombatRecently)
            {
                Vector3 sp = cam.WorldToScreenPoint(tgt.AimPoint + Vector3.up * 1.15f);
                targetMarker.gameObject.SetActive(sp.z > 0);
                targetMarker.position = sp;
                targetMarker.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 90f);
            }
            else targetMarker.gameObject.SetActive(false);

            // barras dos inimigos (danificados recentemente ou alvo) + chefe no topo
            EnemyBase boss = null;
            int used = 0;
            var list = CombatRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i] as EnemyBase;
                if (e == null || !e.Alive) continue;
                if (e.isBoss) { boss = e; continue; }
                bool show = Time.time - e.LastDamagedTime < 4f || (object)e == tgt;
                if (!show) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.HeadPoint + Vector3.up * 0.2f);
                if (sp.z < 0) continue;
                if (used >= bars.Count) bars.Add(MakeBar());
                var b = bars[used++];
                b.root.gameObject.SetActive(true);
                b.root.position = sp;
                b.fill.rectTransform.sizeDelta = new Vector2(70f * e.NormalizedHealth, 6);
            }
            for (int i = used; i < bars.Count; i++) bars[i].root.gameObject.SetActive(false);

            bossGroup.alpha = Mathf.MoveTowards(bossGroup.alpha, boss != null ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (boss != null)
            {
                bossName.text = UIKit.Spaced(boss.displayName.ToUpperInvariant());
                bossSub.text = boss.subtitle;
                float h = boss.NormalizedHealth;
                bossFill.rectTransform.sizeDelta = new Vector2(620f * h, 12);
                bossGhostV = Mathf.MoveTowards(bossGhostV, h, Time.unscaledDeltaTime * 0.35f);
                if (bossGhostV < h) bossGhostV = h;
                bossGhost.rectTransform.sizeDelta = new Vector2(620f * bossGhostV, 12);
            }
        }

        EnemyBar MakeBar()
        {
            var b = new EnemyBar();
            b.root = UIKit.Rect("BarraInimigo", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74, 10));
            b.bg = UIKit.Img("Fundo", b.root, "ui_bar", new Color(0, 0, 0, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74, 10), Image.Type.Sliced);
            b.fill = UIKit.Img("Vida", b.root, "ui_bar", new Color(0.86f, 0.2f, 0.42f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(70, 6), Image.Type.Sliced);
            b.fill.rectTransform.pivot = new Vector2(0, 0.5f);
            return b;
        }

        void UpdateTexts(float dt)
        {
            // área: entra, segura, sai
            if (areaT >= 0f)
            {
                areaT += dt;
                float a = areaT < 0.8f ? areaT / 0.8f : (areaT < 3.4f ? 1f : 1f - (areaT - 3.4f) / 1.2f);
                areaGroup.alpha = Mathf.Clamp01(a);
                areaTitle.rectTransform.anchoredPosition = new Vector2(0, 22 + (1 - Mathf.Clamp01(areaT / 0.8f)) * 14);
                if (areaT > 4.6f) { areaT = -1f; areaGroup.alpha = 0; }
            }
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, Time.unscaledTime < hintUntil ? 1f : 0f, dt * 4f);
            toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.unscaledTime < toastUntil ? 1f : 0f, dt * 6f);
            toast.rectTransform.localScale = Vector3.Lerp(toast.rectTransform.localScale, Vector3.one, dt * 10f);
            objRoot.anchoredPosition = Vector2.Lerp(objRoot.anchoredPosition, new Vector2(44, -205), dt * 6f);
        }

        void UpdateFps(float dt)
        {
            fpsText.gameObject.SetActive(GameSettings.ShowFps);
            if (!GameSettings.ShowFps) return;
            fpsAcc += dt; fpsFrames++;
            if (fpsAcc >= 0.5f) { fpsShown = fpsFrames / fpsAcc; fpsAcc = 0; fpsFrames = 0; fpsText.text = Mathf.RoundToInt(fpsShown) + " FPS"; }
        }
    }
}

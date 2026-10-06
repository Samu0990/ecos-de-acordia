using Aren.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.Enemies
{
    /// <summary>
    /// Distorção (prancha do Sussurrante: "pode iniciar um grito que causa acúmulo de Distorção").
    /// Medidor 0–100 no Aren: o grito soma enquanto ele estiver dentro da onda; 2,5 s depois do
    /// último acúmulo começa a baixar sozinho. Na tela: vinheta violeta que pulsa no compasso
    /// errado, aberração cromática e, perto do limite, a imagem "respira". Em 100: RUPTURA —
    /// dano, empurrão, clarão, o som estala, e o medidor cai para 35.
    /// </summary>
    public class Distortion : MonoBehaviour
    {
        public static float Value { get; private set; }
        public static event System.Action OnRupture;
        public const float Max = 100f;

        static Distortion inst;
        float lastAdd = -10f;
        Vector3 lastSource;
        Canvas canvas;
        RawImage vignette, flash;
        float flashA, punch;
        bool wroteAberration;

        public static void Add(float amount, Vector3 source)
        {
            var p = CombatRegistry.Player;
            if (p == null || !CombatRegistry.IsValid(p) || amount <= 0f) return;
            Ensure();
            Value = Mathf.Min(Max, Value + amount);
            inst.lastAdd = Time.time;
            inst.lastSource = source;
            inst.punch = Mathf.Min(1f, inst.punch + amount / 40f);
            if (Value >= Max) inst.Rupture();
        }

        public static void Clear() { Value = 0f; }

        static void Ensure()
        {
            if (inst != null) return;
            inst = new GameObject("Distorção (Sussurrante)").AddComponent<Distortion>();
        }

        void Awake()
        {
            canvas = UI.UIKit.MakeCanvas("Distorção", 9);   // abaixo do HUD (10)
            canvas.transform.SetParent(transform, false);
            Destroy(canvas.GetComponent<GraphicRaycaster>());
            vignette = Full("Vinheta", VignetteTex());
            flash = Full("Clarão", Texture2D.whiteTexture);
            vignette.color = new Color(0.55f, 0.25f, 1f, 0f);
            flash.color = new Color(0.8f, 0.6f, 1f, 0f);
        }

        RawImage Full(string name, Texture tex)
        {
            var rt = UI.UIKit.Rect(name, canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Vinheta violeta com "rasgos" (ruído radial): mais forte nas bordas.</summary>
        static Texture2D VignetteTex()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "suss_vignette" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float ang = Mathf.Atan2(v, u);
                    float tear = Mathf.PerlinNoise(ang * 3.1f + 10f, r * 2.5f) * 0.35f + Mathf.PerlinNoise(ang * 9.7f, 3f) * 0.25f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f - tear * 0.4f, 1.25f, r));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        void Update()
        {
            var p = CombatRegistry.Player;
            bool alive = p != null && CombatRegistry.IsValid(p);
            if (!alive) Value = 0f;
            if (Time.time - lastAdd > 2.5f) Value = Mathf.Max(0f, Value - 14f * Time.deltaTime);
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 1.6f);
            flashA = Mathf.MoveTowards(flashA, 0f, Time.deltaTime * 2.2f);

            float k = Value / Max;
            // pulso fora do compasso (duas batidas e uma falha), mais rápido perto do limite
            float bpm = Mathf.Lerp(0.9f, 2.2f, k);
            float ph = Mathf.Repeat(Time.time * bpm, 1f);
            float beat = Mathf.Exp(-Mathf.Pow((ph - 0.1f) / 0.06f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((ph - 0.32f) / 0.07f, 2f));
            float a = Mathf.Clamp01(Mathf.Pow(k, 1.2f) * 0.62f + beat * k * 0.22f + punch * 0.25f);
            vignette.color = new Color(0.55f, 0.24f, 1f, a);
            flash.color = new Color(0.82f, 0.62f, 1f, flashA);

            if (k > 0.01f || punch > 0.01f)
            {
                Aren.World.RenderScaler.AberrationPunch = 0.05f + k * 0.22f + punch * 0.25f + beat * k * 0.08f;
                wroteAberration = true;
            }
            else if (wroteAberration)
            {
                Aren.World.RenderScaler.AberrationPunch = 0f;
                wroteAberration = false;
            }
        }

        void Rupture()
        {
            var p = CombatRegistry.Player;
            if (p != null && CombatRegistry.IsValid(p))
            {
                Vector3 dir = p.transform.position - lastSource; dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = -p.transform.forward;
                var h = new HitData
                {
                    damage = 18f, point = p.AimPoint, direction = dir.normalized, knockback = 7f, stagger = 0f,
                    kind = HitKind.Heavy, team = Team.Enemy, source = gameObject
                };
                p.TakeHit(h);
                ArenVFX.Ring(p.transform.position + Vector3.up * 0.1f, 0.2f, 3.2f, 0.45f, new Color(0.7f, 0.35f, 1f), 0.14f, true);
                ArenVFX.Distortion(p.AimPoint, 2.6f, 0.5f, 0.06f);
            }
            GameFeel.Shake(0.55f);
            GameFeel.FovPunch(5f, 0.45f);
            flashA = 0.55f;
            punch = 1f;
            var clip = Resources.Load<AudioClip>("Audio/Eleven/reality_shatter");
            if (clip != null && p != null) AudioSource.PlayClipAtPoint(clip, p.transform.position, 0.8f * ArenAudio.Effects);
            Value = 35f;
            OnRupture?.Invoke();
        }

        void OnDestroy()
        {
            if (inst == this) inst = null;
            if (wroteAberration) Aren.World.RenderScaler.AberrationPunch = 0f;
        }
    }
}

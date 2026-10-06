using System.Collections.Generic;
using Elyndra.World;
using UnityEditor;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Materiais do construtor do mundo, salvos em Assets/World/Materials (um por combinação, reaproveitados):
    /// pedra triplanar com as fotos CC0, cristal, brilho, mar de vidro, água por reino e variações tingidas do
    /// kit gótico de Campânula (cada reino tinge a pedra/telhado/madeira das casas do seu jeito).
    /// </summary>
    public static class WorldMats
    {
        public const string Dir = "Assets/World/Materials";
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static void ClearCache() => cache.Clear();

        static Material Get(string key, System.Func<Material> make)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            System.IO.Directory.CreateDirectory(Dir);
            string path = Dir + "/" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = make();
            if (existing != null) { existing.shader = fresh.shader; existing.CopyPropertiesFromMaterial(fresh); m = existing; EditorUtility.SetDirty(existing); }
            else { AssetDatabase.CreateAsset(fresh, path); m = fresh; }
            cache[key] = m;
            return m;
        }

        public static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        public static Texture2D PH(string name, bool normal = false) => Tex($"Assets/World/Textures/PH/{name}_{(normal ? "normal" : "albedo")}.jpg");
        public static Texture2D CampPH(string name, bool normal = false)
        {
            string b = $"Assets/Campanula/Textures/PH/{name}_{(normal ? "normal" : "albedo")}";
            return Tex(b + ".jpg") ?? Tex(b + ".png");
        }
        public static Texture2D Macro => Tex("Assets/Campanula/Textures/macro_noise.png");
        public static Texture2D Noise => Tex("Assets/Aren/Resources/VFX/noise_perlin.png");

        /// <summary>Pedra/terra triplanar. tex = nome em World/Textures/PH ("darkrock") ou "camp:gnd_rock" (Campânula).</summary>
        public static Material Stone(string tex, Color tint, float scale = 3f, float gloss = 0.12f, Color? emission = null)
        {
            string key = $"tri_{tex.Replace(':', '_')}_{ColorUtility.ToHtmlStringRGB(tint)}_{scale:0.#}" + (emission.HasValue ? "_e" + ColorUtility.ToHtmlStringRGB(emission.Value) : "");
            return Get(key, () =>
            {
                var m = new Material(Shader.Find("Elyndra/WorldTriplanar"));
                Texture2D a, n;
                if (tex.StartsWith("camp:")) { a = CampPH(tex.Substring(5)); n = CampPH(tex.Substring(5), true); }
                else { a = PH(tex); n = PH(tex, true); }
                m.SetTexture("_MainTex", a); m.SetTexture("_BumpMap", n); m.SetTexture("_Macro", Macro);
                m.SetColor("_Color", tint); m.SetFloat("_Scale", scale); m.SetFloat("_Glossiness", gloss);
                if (emission.HasValue) { m.SetColor("_Emission", emission.Value); m.EnableKeyword("_EMISSION"); }
                m.enableInstancing = true;
                return m;
            });
        }

        public static Material Crystal(Color body, Color rim, Color core, float opacity = 0.75f)
        {
            string key = $"crystal_{ColorUtility.ToHtmlStringRGBA(body)}_{ColorUtility.ToHtmlStringRGB(rim)}";
            return Get(key, () =>
            {
                var m = new Material(Shader.Find("Elyndra/Crystal"));
                m.SetColor("_Color", body); m.SetColor("_Rim", rim); m.SetColor("_Core", core); m.SetFloat("_Opacity", opacity);
                return m;
            });
        }

        public static Material Glow(Color c, float pulse = 0f, float soft = 2f)
        {
            string key = $"glow_{ColorUtility.ToHtmlStringRGB(c)}_{c.maxColorComponent:0.#}_{pulse:0.#}_{soft:0.#}";
            return Get(key, () =>
            {
                var m = new Material(Shader.Find("Elyndra/Glow"));
                m.SetColor("_Color", c); m.SetFloat("_Pulse", pulse); m.SetFloat("_Soft", soft);
                return m;
            });
        }

        public static Material Motes()
        {
            return Get("motes", () =>
            {
                var m = new Material(Shader.Find("Hidden/Aren/WorldParticle"));
                m.SetFloat("_Boost", 1.1f);
                return m;
            });
        }

        public static Material GlassSea()
        {
            return Get("glass_sea", () =>
            {
                var m = new Material(Shader.Find("Elyndra/GlassSea"));
                m.SetTexture("_Noise", Noise);
                return m;
            });
        }

        public static Material Water(string region, Color deep, Color sky)
        {
            return Get("water_" + region, () =>
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/Water.mat");
                var m = src != null ? new Material(src) : new Material(Shader.Find("Campanula/Water"));
                m.SetColor("_Deep", deep); m.SetColor("_Sky", sky);
                return m;
            });
        }

        public static Material Sky(RegionProfile p, Vector3 fendaDir, float fendaK)
        {
            return Get("sky_" + p.region, () =>
            {
                var m = new Material(Shader.Find("Campanula/SunsetSky"));
                m.SetTexture("_Clouds", Noise);
                m.SetTexture("_Stars", Tex("Assets/Aren/Resources/VFX/Space/space_stars_dense.png"));
                m.SetColor("_Zenith", p.skyTop);
                m.SetColor("_Mid", Color.Lerp(p.skyTop, p.skyHorizon, 0.55f));
                m.SetColor("_Horizon", p.skyHorizon);
                m.SetColor("_Ground", p.fogColor * 0.5f);
                m.SetColor("_SunColor", p.sunColor * (1.2f + p.sunIntensity));
                m.SetColor("_CloudColor", Color.Lerp(p.skyHorizon, Color.white, 0.25f));
                m.SetColor("_CloudShadow", p.skyTop * 0.9f);
                m.SetVector("_RiftDir", new Vector4(fendaDir.x, 0.18f + 0.3f * fendaK, fendaDir.z, 0));
                m.SetColor("_RiftTint", new Color(0.55f, 0.25f, 0.75f) * (0.3f + fendaK));
                return m;
            });
        }

        public static Material Rift()
        {
            return Get("rift_far", () =>
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/Rift.mat");
                return src != null ? new Material(src) : new Material(Shader.Find("Campanula/Rift"));
            });
        }

        /// <summary>Variação tingida de um material do kit de Campânula (CMP_*), por reino.</summary>
        public static Material KitVariant(Material src, string region, Color tint)
        {
            if (src == null) return null;
            string key = $"kit_{region}_{src.name}";
            return Get(key, () =>
            {
                var m = new Material(src);
                if (m.HasProperty("_Color")) m.SetColor("_Color", src.GetColor("_Color") * tint);
                return m;
            });
        }

        public static Material Terrain(string region, LayoutTextures t)
        {
            return Get("terrain_" + region, () =>
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/Terrain.mat");
                var m = src != null ? new Material(src) : new Material(Shader.Find("Campanula/Terrain"));
                void Set(string slot, string tex)
                {
                    if (string.IsNullOrEmpty(tex)) return;
                    bool camp = tex.StartsWith("camp:");
                    string n = camp ? tex.Substring(5) : tex;
                    var a = camp ? CampPH(n) : PH(n);
                    var nm = camp ? CampPH(n, true) : PH(n, true);
                    if (a != null) m.SetTexture(slot, a);
                    if (nm != null && m.HasProperty(slot + "N")) m.SetTexture(slot + "N", nm);
                }
                Set("_GNear", t.baseNear); Set("_GMeadow", t.baseFar); Set("_Path", t.path); Set("_Cobble", t.cobble); Set("_Rock", t.rock);
                if (!string.IsNullOrEmpty(t.field)) { var f = t.field.StartsWith("camp:") ? CampPH(t.field.Substring(5)) : PH(t.field); if (f != null) m.SetTexture("_Field", f); }
                m.SetColor("_GrassTint", t.baseTint);
                m.SetColor("_MeadowTint", t.farTint);
                m.SetColor("_DryTint", t.dryTint);
                m.SetVector("_Tiles", new Vector4(t.nearTile, t.farTile, 3.2f, 2f));
                return m;
            });
        }
    }

    /// <summary>Fotos do chão de um reino (slots do shader Campanula/Terrain) e tons.</summary>
    public class LayoutTextures
    {
        public string baseNear = "camp:gnd_near", baseFar = "camp:gnd_meadow", path = "camp:gnd_path", cobble = "", field = "", rock = "camp:gnd_rock";
        public Color baseTint = new Color(0.82f, 0.9f, 0.7f), farTint = new Color(0.8f, 0.86f, 0.72f), dryTint = new Color(1.12f, 0.98f, 0.72f);
        public float nearTile = 3f, farTile = 30f;
    }
}
